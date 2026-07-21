using Korkut.Application.Abstractions.Directory;
using Korkut.Application.Abstractions.Knowledge;
using Korkut.Application.Abstractions.Persistence;
using Korkut.Application.Documents;
using Korkut.Application.Features.Agent;
using Korkut.Application.Features.Settings;
using Korkut.Agent.Knowledge;
using Korkut.Agent.Tools;
using Korkut.Agent.Tools.Office;
using Korkut.Application.Features.Finance;
using Korkut.Application.Features.Membership;
using Korkut.Application.Features.OfficeCore;
using Korkut.Application.Features.OfficeOps;
using Korkut.Application.Features.Reports;
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

        // Operational office-core services the domain tools wrap (matters + clients), over the
        // operational DbContext. Read/written inside a per-office tenant scope (IOfficeScope).
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IMatterRepository, MatterRepository>();
        services.AddScoped<IHearingRepository, HearingRepository>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IMatterService, MatterService>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IDeadlineRepository, DeadlineRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<IOfficeOpsService, OfficeOpsService>();
        services.AddScoped<IReportsRepository, ReportsRepository>();
        services.AddScoped<IReportsService, ReportsService>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IMembersService, MembersService>();
        services.AddScoped<IOfficeScope, OfficeScope>();

        // Assistant tools + the key-gated catalog the tool-calling loop resolves them through.
        // Each tool is one IAgentTool + one registration here; the loop never changes.
        services.AddScoped<IAgentTool, KnowledgeSearchTool>();
        services.AddScoped<IAgentTool, ListMattersTool>();
        services.AddScoped<IAgentTool, GetMatterTool>();
        services.AddScoped<IAgentTool, CreateMatterTool>();
        services.AddScoped<IAgentTool, ListClientsTool>();
        services.AddScoped<IAgentTool, CreateClientTool>();
        services.AddScoped<IAgentTool, AddInvoiceTool>();
        services.AddScoped<IAgentTool, ListInvoicesTool>();
        services.AddScoped<IAgentTool, AddExpenseTool>();
        services.AddScoped<IAgentTool, ListExpensesTool>();
        services.AddScoped<IAgentTool, FinanceSummaryTool>();
        services.AddScoped<IAgentTool, AddTaskTool>();
        services.AddScoped<IAgentTool, ListTasksTool>();
        services.AddScoped<IAgentTool, CompleteTaskTool>();
        services.AddScoped<IAgentTool, AddDeadlineTool>();
        services.AddScoped<IAgentTool, ListDeadlinesTool>();
        services.AddScoped<IAgentTool, CompleteDeadlineTool>();
        services.AddScoped<IAgentTool, AddNoteTool>();
        services.AddScoped<IAgentTool, ListNotesTool>();
        services.AddScoped<IAgentTool, OfficeOverviewTool>();
        services.AddScoped<IAgentTool, ListMembersTool>();
        services.AddScoped<IToolCatalog, ToolCatalog>();

        return services;
    }
}
