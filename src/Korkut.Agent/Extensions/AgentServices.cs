using Korkut.Agent.Services.Core.Documents;
using Korkut.Agent.Services.Core.Chat;
using Korkut.Application.Abstractions.Directory;
using Korkut.Application.Abstractions.Knowledge;
using Korkut.Application.Abstractions.Persistence;
using Korkut.Application.Documents;
using Korkut.Agent.Application.Sessions;
using Korkut.Domain.Agent.Conversations;
using Korkut.Domain.Agent.Documents;
using Korkut.Agent.Identity;
using Korkut.Application.Features.Settings;
using Korkut.Agent.Knowledge;
using Korkut.Agent.Tools;
using Korkut.Agent.Tools.Law;
using Korkut.Agent.Tools.Office;
using Korkut.Application.Features.Calendar;
using Korkut.Application.Features.Correspondence;
using Korkut.Application.Features.Finance;
using Korkut.Application.Features.Membership;
using Korkut.Application.Features.OfficeCore;
using Korkut.Application.Features.OfficeOps;
using Korkut.Application.Features.Outbox;
using Korkut.Application.Features.Reports;
using Korkut.Agent.Services.Implementations.Ai;
using Korkut.Agent.Repositories.Core;
using Korkut.Agent.Repositories.Implementations;
using Korkut.Agent.Services.Core;
using Korkut.Agent.Services.Core.Ai;
using Korkut.Agent.Services.Core.Sessions;
using Korkut.Agent.Services.Implementations.Chat;
using Korkut.Agent.Services.Implementations.Documents;
using Korkut.Agent.Services.Implementations.Sessions;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Persistence.Repositories;
using Korkut.Infrastructure.Templates;

namespace Korkut.Agent.Extensions;

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
        services.AddScoped<ILlmCallRepository, LlmCallRepository>();
        services.AddScoped<ILlmCallLogService, LlmCallLogService>();

        // Governance: per-office token budget (from the ledger) + concurrency cap. The gate holds
        // the per-office semaphores process-wide (singleton); the governor reads the ledger (scoped).
        services.AddSingleton<IOfficeConcurrencyGate, OfficeConcurrencyGate>();
        services.AddScoped<IUsageGovernorService, UsageGovernorService>();

        // AI orchestration over the model (IChatModel from AddKorkutAi) + office settings.
        services.AddScoped<IAgentUnitOfWork, AgentUnitOfWork>();
        services.AddScoped<ISessionChatService, SessionChatService>();
        services.AddScoped<IDraftingService, DraftingService>();

        // Application services the controllers delegate to: they own the tenant scope, orchestration,
        // author resolution, and response-DTO mapping, so the controllers stay thin (request → result).
        // The caller identity comes from the trusted access token via ICurrentUser.
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<ISessionAppService, SessionAppService>();
        services.AddScoped<IChatAppService, ChatAppService>();
        services.AddScoped<IDocumentAppService, DocumentAppService>();

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
        services.AddScoped<ICalendarRepository, CalendarRepository>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<ICorrespondenceRepository, CorrespondenceRepository>();
        services.AddScoped<ICorrespondenceService, CorrespondenceService>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IOutboxService, OutboxService>();
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
        services.AddScoped<IAgentTool, CreateEventTool>();
        services.AddScoped<IAgentTool, ListEventsTool>();
        services.AddScoped<IAgentTool, CompleteEventTool>();
        services.AddScoped<IAgentTool, UpcomingAgendaTool>();
        services.AddScoped<IAgentTool, AddHearingTool>();
        services.AddScoped<IAgentTool, AddEvrakTool>();
        services.AddScoped<IAgentTool, ListEvrakTool>();
        services.AddScoped<IAgentTool, SendEmailTool>();
        services.AddScoped<IAgentTool, RememberTool>();
        services.AddScoped<IAgentTool, OpenEditorTool>();
        // Legal-external (Tools/Law): composite search/fetch over the ISourceClient adapters
        // registered below. The adapters are added per source (Bedesten/Tavily/... — incrementally).
        services.AddScoped<IAgentTool, SearchDecisionsTool>();
        services.AddScoped<IAgentTool, FetchDocumentTool>();
        AddLawSources(services);
        services.AddScoped<IToolCatalog, ToolCatalog>();
        services.AddScoped<ToolCallingLoopService>();
        services.AddScoped<IAgentToolLoopService>(sp => sp.GetRequiredService<ToolCallingLoopService>());

        return services;
    }

    /// <summary>
    /// Registers the public-legal-database adapters the composite <c>search_decisions</c> /
    /// <c>fetch_document</c> tools dispatch to. The Bedesten cluster (Yargıtay / Danıştay / Emsal /
    /// KYB / Mevzuat) shares one browser-headed HttpClient against the Ministry of Justice host.
    /// Further sources (Anayasa, GİB, KİK, Tavily-backed BDDK/KVKK/Sigorta Tahkim, …) register here.
    /// </summary>
    private static void AddLawSources(IServiceCollection services)
    {
        services.AddHttpClient("bedesten", c =>
        {
            c.BaseAddress = new Uri("https://bedesten.adalet.gov.tr");
            c.Timeout = TimeSpan.FromSeconds(60);
            var h = c.DefaultRequestHeaders;
            h.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36");
            h.Add("Accept", "*/*");
            h.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
            h.Add("AdaletApplicationName", "UyapMevzuat");
            h.Add("Origin", "https://mevzuat.adalet.gov.tr");
            h.Add("Referer", "https://mevzuat.adalet.gov.tr/");
        });

        // Browser-headed clients for the other portals: "legal" for well-behaved HTTPS hosts;
        // "legal-insecure" tolerates the legacy TLS stacks several government hosts still serve.
        services.AddHttpClient("legal", ConfigureBrowser);
        services.AddHttpClient("legal-insecure", ConfigureBrowser)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                AllowAutoRedirect = true,
            });

        // Tavily web-search (api.tavily.com): the single provider behind the key-gated sources
        // (BDDK / KVKK / Sigorta Tahkim), which discover documents on portals with no real API.
        services.AddHttpClient("tavily", c =>
        {
            c.BaseAddress = new Uri("https://api.tavily.com");
            c.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddScoped<TavilyClient>();

        // Bedesten court cluster: one endpoint, per-court itemTypeList; chamber (birimAdi) filter
        // applies only to the higher courts. All support full-text fetch by documentId.
        services.AddScoped<ISourceClient>(sp => new BedestenClient(
            sp.GetRequiredService<IHttpClientFactory>(), "yargitay", ["YARGITAYKARARI"], supportsChamber: true));
        services.AddScoped<ISourceClient>(sp => new BedestenClient(
            sp.GetRequiredService<IHttpClientFactory>(), "danistay", ["DANISTAYKARAR"], supportsChamber: true));
        services.AddScoped<ISourceClient>(sp => new BedestenClient(
            sp.GetRequiredService<IHttpClientFactory>(), "emsal", ["YERELHUKUK", "ISTINAFHUKUK"], supportsChamber: false));
        services.AddScoped<ISourceClient>(sp => new BedestenClient(
            sp.GetRequiredService<IHttpClientFactory>(), "kyb", ["KYB"], supportsChamber: false));
        services.AddScoped<ISourceClient, MevzuatClient>();       // legislation (search + fetch)
        services.AddScoped<ISourceClient, AnayasaClient>();       // Constitutional Court (JSON)
        services.AddScoped<ISourceClient, GibClient>();           // tax rulings / özelge (JSON)
        services.AddScoped<ISourceClient, BddkClient>();          // banking (Tavily + fetch)
        services.AddScoped<ISourceClient, KvkkClient>();          // data protection (Tavily + fetch)
        services.AddScoped<SigortaTahkimClient>();                // concrete: search_within_sigorta_tahkim depends on it
        services.AddScoped<ISourceClient>(sp => sp.GetRequiredService<SigortaTahkimClient>()); // insurance arbitration (Tavily + PDF fetch)
        services.AddScoped<ISourceClient, RekabetClient>();       // competition authority (HTML scrape)
        services.AddScoped<ISourceClient, SayistayClient>();      // court of accounts (DataTables)
        services.AddScoped<ISourceClient, UyusmazlikClient>();    // jurisdictional disputes (WebForms + PDF)
        services.AddScoped<ISourceClient, KikClient>();           // public procurement (WebForms)

        // Standalone legal tool: keyword search within one Sigorta Tahkim journal issue.
        services.AddScoped<IAgentTool, SearchWithinSigortaTahkimTool>();
    }

    private static void ConfigureBrowser(HttpClient c)
    {
        c.Timeout = TimeSpan.FromSeconds(120);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36");
        c.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
    }
}
