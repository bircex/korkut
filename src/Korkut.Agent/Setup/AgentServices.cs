using Korkut.Application.Abstractions.Directory;
using Korkut.Application.Abstractions.Knowledge;
using Korkut.Application.Abstractions.Persistence;
using Korkut.Application.Documents;
using Korkut.Application.Features.Agent;
using Korkut.Application.Features.Settings;
using Korkut.Agent.Knowledge;
using Korkut.Agent.Tools;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Persistence.Repositories;
using Korkut.Infrastructure.Templates;

namespace Korkut.Agent.Setup;

/// <summary>
/// Registers the Agent host's own services: the assistant's Clean-Architecture stores over
/// the Agent DB (sessions + drafted documents), the AI orchestration services that drive a
/// chat/drafting turn, the plain-text extractor for uploads, and the operational user-name
/// lookup used to resolve a session/version author id to a display name. The persona/style/
/// memory reads these compose on come from the operational domain services registered by
/// <c>AddKorkutDomain</c>; the model itself comes from <c>AddKorkutAi</c>.
/// </summary>
public static class AgentServices
{
    public static IServiceCollection AddKorkutAgentServices(this IServiceCollection services)
    {
        // Assistant stores (Agent DB, schema korkut_agent) → Clean use-case services.
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IDraftDocumentRepository, DraftDocumentRepository>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IDraftDocumentService, DraftDocumentService>();

        // Operational settings the assistant composes into its prompts (persona / style / memory)
        // + the legal template catalog drafting seeds from. Read over the operational DbContext.
        services.AddScoped<IOfficeSettingsRepository, OfficeSettingsRepository>();
        services.AddScoped<IOfficeSettingsService, OfficeSettingsService>();
        services.AddSingleton(_ => new TemplateCatalog());

        // Per-model-call cost/telemetry ledger (llm_calls in the Agent DB) + a first-class span.
        services.AddScoped<Korkut.Application.Ai.ILlmCallLog, EfLlmCallLog>();

        // Governance: per-office token budget (from the ledger) + concurrency cap. The gate holds
        // the per-office semaphores process-wide (singleton); the governor reads the ledger (scoped).
        services.AddSingleton<OfficeConcurrencyGate>();
        services.AddScoped<Korkut.Application.Ai.IUsageGovernor, UsageGovernor>();

        // AI orchestration over the model (IChatModel from AddKorkutAi) + office settings.
        services.AddScoped<SessionChatService>();
        services.AddScoped<DraftingService>();

        // Upload text extraction (embedded text only; OCR is a later, separate service).
        services.AddSingleton<ITextExtractor, PlainTextExtractor>();

        // Operational lookup: resolve an author id → display name/e-mail (Agent DB never joins Users).
        services.AddScoped<IUserLookup, UserLookup>();

        // Retrieval: the Agent grounds a chat turn by calling the internal Knowledge /search over
        // HTTP, forwarding the caller's token. Unconfigured (no KORKUT_KNOWLEDGE_URL) → ungrounded.
        services.AddHttpContextAccessor();
        services.AddHttpClient<IKnowledgeSearch, HttpKnowledgeSearch>();

        // Assistant tools + the key-gated catalog the tool-calling loop resolves them through.
        // Each tool is one IAgentTool + one registration here; the loop never changes.
        services.AddScoped<IAgentTool, KnowledgeSearchTool>();
        services.AddScoped<IToolCatalog, ToolCatalog>();

        return services;
    }
}
