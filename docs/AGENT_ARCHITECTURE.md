# Agent architecture

Status: **proposal / design** (2026-07-27). Nothing here is wired into a running host yet;
the agent code exists but is intentionally uncomposed. This document is the plan we build to,
one green step at a time.

## Goal

Split the agent from one scattered `Infrastructure/Agent/Ai` + `Application/Agent` blob into
clear, independently-testable **layers**, each a `Korkut.Agent.*` module, with a strict
one-way dependency flow. A layer knows the layer(s) below it and nothing above.

## Layers (bottom to top)

```
                    ┌───────────────────────────────────────────┐
   host             │ Korkut.Agent.Host  (DI root, HTTP: chat/   │
                    │   SSE / upload / drafting endpoints)        │
                    └───────────────┬───────────────────────────┘
                                    │ composes + serves
   use-cases   ┌────────────────────┴───────────┬──────────────────┐
               │ Korkut.Agent.Chat              │ Korkut.Agent.     │
               │  session + chat orchestration   │  Drafting (svc)   │
               └───────────┬────────────────────┴─────────┬────────┘
                           │ calls the brain               │
   reasoning        ┌──────┴───────────────────────────────┐
                    │ Korkut.Agent.Brain                    │
                    │  tool-calling loop, system prompts,   │
                    │  Knowledge grounding                  │
                    └───┬───────────────────────────┬──────┘
                        │ asks the model            │ runs tools
   capabilities  ┌──────┴─────────────┐    ┌────────┴─────────┐
                 │ Korkut.Agent.Model │    │ Korkut.Tools ✓   │
                 │  providers+router+ │    │  pure tools +    │
                 │  facade+decorators │    │  abstractions +  │
                 └──────┬─────────────┘    │  ToolCatalog     │
                        │ writes usage      └──────────────────┘
   data / infra  ┌──────┴──────┬───────────────┬──────────────┐
                 │ Agent.Ledger│ Chat (convos) │ Agent.Drafting│  Knowledge · Pii
                 │  (llm_calls)│  (data)        │  (docs, data) │  Contract · Core
                 └─────────────┴───────────────┴───────────────┘
```

Dependency rule: an arrow only ever points **down**. `Korkut.Tools` already obeys this (it
depends on Contract/Core/Outbox/Knowledge, nothing agent-specific). No cycles, enforced by an
architecture test.

## The modules

### Korkut.Tools ✓ (done)
Pure capabilities (`IAgentTool` impls: Office + Law), the tool abstractions
(`IAgentTool`, `ToolArgs`, `ToolResult`, `ToolSchema`, `ToolSpec`, `AgentToolContext`,
`IToolCatalog`) and `ToolCatalog`. Self-contained: no dependency on the model or the brain.

### Korkut.Agent.Model  (new)
The **only** place that talks to an LLM. Owns:
- **Abstractions**: `IChatModelService` (the facade the brain calls), `IChatProviderService`
  (one backend), `IModelRouterService`.
- **DTOs / models**: `ChatMessage/ChatRequest/ChatReply/ChatStreamEvent/ChatUsage`,
  `ToolDefinition/ToolInvocation/ToolOutput`, `ModelRouting`, `ModelSelection`, `LlmFeature`,
  `ModelSpecialty`, `PromptFingerprint`. (These leave `Application/Agent/Models` and
  `Application/Agent/Abstractions`.)
- **Providers**: `AnthropicChatProviderService`, `OpenAiCompatibleChatProviderService`.
- **Router**: `ModelRouterService` + `ModelRouting` (specialty → provider+model, cloud/local
  fallback), `ModelPricing`.
- **Facade**: `RoutingChatModelService` implements `IChatModelService` over the router+providers.
- **Cross-cutting decorators** (see below).

Depends on: Core, Contract, **Korkut.Pii** (redaction), **Korkut.Agent.Ledger** (usage write).

### Korkut.Agent.Brain  (new) — the reasoning layer
The agent loop. Owns:
- `IAgentToolLoopService` + `ToolCallingLoopService` (the model ⇄ tools loop).
- System-prompt construction and **Knowledge grounding** (retrieval → prompt).
- Later: planning / multi-step reasoning strategies live here.

Depends on: **Korkut.Agent.Model** + **Korkut.Tools** + **Korkut.Knowledge**. It orchestrates
the two capability layers; they never know about each other or about the brain.

### Korkut.Agent.Chat  (new) — chat use-case
Session + chat orchestration: `ISessionChatService`/`SessionChatService`, `ChatAppService`,
`SessionService`/`SessionAppService`, the chat request/response DTOs. Calls the brain, persists
turns to the **Korkut.Chat** data module. (Naming: `Korkut.Chat` = conversation *store*;
`Korkut.Agent.Chat` = the agent *orchestration* over it.)

### Korkut.Agent.Drafting  (extend the existing data module)
Already holds the `GeneratedDocument`/`DocumentVersion` entities. Fold in the drafting
use-case services (`DraftingService`, `DraftDocumentService`) so the drafting bounded context
is one module: it calls the brain/model to draft, and persists versions.

### Korkut.Agent.Ledger ✓ (data) + the log writer
Holds `LlmCall`. The ledger **writer** (`LlmCallLogService`, `ModelPricing`) lives here or in
Model; the Model decorator calls it after each model call.

### Korkut.Agent.Host  (new, later)
Composition root + HTTP surface (chat, SSE stream, upload, drafting). Wires the decorator
chain, registers every layer, owns the `AgentDbContext` connection. This is what makes the
agent actually run — deferred until the layers are in place.

## Model cross-cutting: the decorator chain

`IChatModelService` is resolved as a **chain of decorators** wrapping the real router+provider,
composed in DI. Each is optional and independently testable. Proposed order (outermost first):

```
brain → [Governance] → [Redaction] → [Ledger-log] → Router → Provider(Anthropic|OpenAI) → LLM
```

- **Governance** (`UsageGovernorService` + `OfficeConcurrencyGate`): enforce the per-office
  token budget + concurrency *before* spending a call; 429 when over. Outermost so it short-
  circuits early.
- **Redaction** (`RedactingChatProviderService`, uses `Korkut.Pii`): mask PII in the outbound
  request before it leaves the process.
- **Ledger-log** (`LlmCallLogService` + `ModelPricing`): after the call, record
  tokens/cost/latency/fingerprint into `Korkut.Agent.Ledger`.
- **Router** (`RoutingChatModelService`): pick provider+model for the feature/specialty.
- **Provider**: the actual API call.

Order is tunable (e.g. redaction could wrap the provider only), but the principle holds: these
are decorators over one interface, not tangled into the providers.

## What moves where (from today's scattered state)

| Today (scattered) | Target module |
|---|---|
| `Application/Agent/Abstractions/{IChatModelService,IChatProviderService,IModelRouterService}` | Agent.Model |
| `Application/Agent/Models/{ChatModels,ModelRouting,ModelSelection,LlmFeature,PromptFingerprint}` | Agent.Model |
| `Infra/Agent/Ai/{Anthropic,OpenAiCompatible}ChatProviderService` | Agent.Model |
| `Infra/Agent/Ai/{ModelRouterService,RoutingChatModelService,ModelPricing}` | Agent.Model |
| `Infra/Agent/Ai/{RedactingChatProviderService,UsageGovernorService}` + `OfficeConcurrencyGate` | Agent.Model (decorators) |
| `Infra/Agent/Ai/LlmCallLogService` | Agent.Ledger (writer) |
| `Infra/Agent/Ai/{IAgentToolLoopService,ToolCallingLoopService}` | Agent.Brain |
| `Infra/Agent/Ai/SessionChatService` + `Application/Agent/{Chat,Sessions,Services/SessionService}` | Agent.Chat |
| `Infra/Agent/Ai/DraftingService` + `Application/Agent/Services/DraftDocumentService` | Agent.Drafting |

Everything under `Infra/Agent/Ai` and `Application/Agent` is drained; `Application` keeps only
truly shared cross-cutting.

## Build order (each step green, own commit)

1. **Agent.Model** — pull the model layer (abstractions + DTOs + providers + router + facade)
   into the module; keep behaviour identical, no decorators yet.
2. **Decorators** — express redaction/governance/ledger as decorators over `IChatModelService`,
   chained in the module's DI extension.
3. **Agent.Brain** — move the tool-calling loop + grounding; depends on Model + Tools + Knowledge.
4. **Agent.Chat / Agent.Drafting** — fold the use-case services into their modules.
5. **Agent.Host** — compose it all, add the HTTP surface, wire `AgentDbContext`. The agent
   goes live here.
6. **Architecture test** — assert the one-way dependency flow (no layer references a layer above).

## Open questions

- `LlmCallLogService`: in Agent.Ledger (data owner) or Agent.Model (the caller)? Leaning Ledger.
- Do system prompts / persona (from `Korkut.Settings`) belong to Brain, or a small
  `Korkut.Agent.Prompting` piece?
- Streaming (SSE) shape: keep `ChatStreamEvent` in Model, or a thin transport type in the Host?
