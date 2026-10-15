using Korkut.Contract.Audit;
using Korkut.Contract.Templates;
using Korkut.Contract.Export;
using Korkut.Core;
using Korkut.Ai;
using Korkut.Mail;
using Korkut.Infrastructure.Persistence;
using Korkut.Notes.Extensions;
using Korkut.Reports.Extensions;
using Korkut.Settings.Extensions;
using Korkut.Tasks.Extensions;

namespace Korkut.Web;

/// <summary>
/// Registers the domain persistence + services the real endpoints use. The DbContext
/// connects as the LEAST-PRIVILEGED app role (KORKUT_DB_URL); it never migrates on
/// startup — only Korkut.Migrator applies schema (KDM-011). Services are scoped so
/// each request gets a DbContext whose tenant is pinned per-request (see TenantRequest).
/// </summary>
public static class DomainSetup
{
    public static IServiceCollection AddKorkutDomain(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration["KORKUT_DB_URL"]
            ?? throw new InvalidOperationException("KORKUT_DB_URL is required for the API (the least-privileged app-role connection).");

        services.AddKorkutPersistence(connection);

        // Transactional e-mail: the single operator SMTP account (KORKUT_SMTP_*). Binds the
        // Contract IEmailSender to the MailKit sender, or a log-only no-op when SMTP is unset.
        // Registered here so every host that wires the domain (API + Agent host) can send mail.
        services.AddKorkutMail(configuration);

        // IClock is registered by the composition root; audit + stores are scoped.
        services.AddScoped<IAuditLog, Korkut.Audit.AuditLog>();
        services.AddScoped<IAuditQuery, Korkut.Audit.AuditQuery>();
        // Refused requests (403) are recorded in the same trail.
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, Korkut.Web.Auth.DeniedAccessAudit>();
        // Office-core modules (clients + courts + matters/parties + hearings). Cross-module FKs are
        // configured in Infrastructure; cross-module reads go through Contract lookups
        // (ICourtLookup, IHearingLookup, IMatterLookup).
        new Korkut.Courts.Extensions.CourtsModule().Register(services, configuration);
        new Korkut.Matters.Extensions.MattersModule().Register(services, configuration);
        new Korkut.Hearings.Extensions.HearingsModule().Register(services, configuration);

        // Modular monolith: self-registering vertical modules. Clients is the reference slice —
        // it binds its own repository + service; its EF mapping + controller are picked up by the
        // shared context and MVC application parts (see AddKorkutControllers).
        new Korkut.Clients.Extensions.ClientsModule().Register(services, configuration);

        // Offices (the tenant root) are owned by the Organization module (registered below); the
        // caller's own profile still lives in the Users application service for now.

        // Clean-architecture cross-cutting: the caller identity + the tenant-pinned unit of work
        // every application service runs inside.
        services.AddHttpContextAccessor();
        services.AddScoped<Korkut.Core.ActingUser>();
        services.AddSingleton<Korkut.Core.IActingUserCredential, Korkut.Web.Identity.ActingUserCredential>();
        services.AddScoped<Korkut.Core.ICurrentUser, Korkut.Web.Identity.HttpCurrentUser>();
        services.AddScoped<Korkut.Core.IUnitOfWork, Korkut.Persistence.UnitOfWork>();
        // Office-ops (Clean Architecture: repository ports + service).
        new NotesModule().Register(services, configuration);
        new TasksModule().Register(services, configuration);
        new Korkut.Deadlines.Extensions.DeadlinesModule().Register(services, configuration);
        // Finance: one module for invoices + expenses + the money roll-up (IFinanceService). The
        // reports dashboard reads finance totals through the module's IFinanceLookup.
        new Korkut.Finance.Extensions.FinanceModule().Register(services, configuration);
        new Korkut.Correspondence.Extensions.CorrespondenceModule().Register(services, configuration);
        // Calendar module. Its unified view (events ∪ deadlines ∪ hearings) is composed inside
        // the service via each owner's Contract lookup — no host-level read-model to register.
        new Korkut.Calendar.Extensions.CalendarModule().Register(services, configuration);
        new SettingsModule().Register(services, configuration);
        // Organization (identity/tenant) module — grows in stages; office roles land first.
        new Korkut.Organization.Extensions.OrganizationModule().Register(services, configuration);
        // Reports module (read-only office dashboard). It composes the overview from each domain's
        // Contract lookup, so there is no host-level aggregation query to register.
        new ReportsModule().Register(services, configuration);
        // LawClients (public Turkish legal database search/fetch) + Calculators (legal/financial
        // calculators): stateless capability modules, consumed both by this host's own Controllers
        // (UI/REST) and by the agent tool loop (Korkut.Tools, through the same Contract interface —
        // see Korkut.Agent.Host's AddKorkutDomain call, which wires the same modules in-process).
        new Korkut.LawClients.Extensions.LawClientsModule().Register(services, configuration);
        new Korkut.Calculators.Extensions.CalculatorsModule().Register(services, configuration);
        // Kararlarım: the office's bookmarked decisions from Karar Arama / İlgili Kararlar.
        new Korkut.SavedDecisions.Extensions.SavedDecisionsModule().Register(services, configuration);
        // Telegram: the office bot, members' links, and the replies to a linked member's messages.
        new Korkut.Telegram.Extensions.TelegramModule().Register(services, configuration);
        // Reminders: deadlines, hearings and agenda records, sent through Telegram and e-mail.
        new Korkut.Reminders.Extensions.RemindersModule().Register(services, configuration);
        // The static legal catalog, and the two read-only services derived from it:
        // coverage (what the assistant can draft) and markdown export.
        services.AddSingleton(_ => new Korkut.Contract.Templates.TemplateCatalog());
        services.AddScoped<Korkut.Contract.Coverage.ICoverageCatalog, Korkut.Coverage.CatalogCoverageReader>();
        services.AddScoped<Korkut.Contract.Coverage.ICoverageService, Korkut.Coverage.CoverageService>();
        services.AddSingleton<Korkut.Contract.Export.IDocumentExporter, Korkut.Documents.Export.MarkdownExporter>();
        services.AddScoped<Korkut.Contract.Export.IExportService, Korkut.Documents.Export.ExportService>();
        // Members (owner-safety lives in the service). The user aggregate + IUserRepository are
        // owned by the Organization module (registered above).
        // Invites (Clean Architecture: repository port + service + cross-tenant resolver port).
        services.AddScoped<Korkut.Organization.IInviteResolver, Korkut.Organization.Services.InviteResolver>();
        // The installation's one office and its admin, created before any session exists (RLS-bypass SQL).
        services.AddScoped<Korkut.Contract.Offices.IOfficeProvisioner, Korkut.Organization.Services.OfficeProvisioner>();

        return services;
    }
}
