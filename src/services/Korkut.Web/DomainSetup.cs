using Korkut.Contract.Templates;
using Korkut.Contract.Export;
using Korkut.Core;
using Korkut.Infrastructure.Ai;
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

        // IClock is registered by the composition root; audit + stores are scoped.
        services.AddScoped<IAuditLog, Korkut.Infrastructure.Auditing.AuditLog>();
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
        new Korkut.Channels.Extensions.ChannelsModule().Register(services, configuration);
        // The static legal catalog, and the two read-only services derived from it:
        // coverage (what the assistant can draft) and markdown export.
        services.AddSingleton(_ => new Korkut.Contract.Templates.TemplateCatalog());
        services.AddScoped<Korkut.Contract.Coverage.ICoverageCatalog, Korkut.Infrastructure.Coverage.CatalogCoverageReader>();
        services.AddScoped<Korkut.Contract.Coverage.ICoverageService, Korkut.Infrastructure.Coverage.CoverageService>();
        services.AddSingleton<Korkut.Contract.Export.IDocumentExporter, Korkut.Infrastructure.Export.MarkdownExporter>();
        services.AddScoped<Korkut.Contract.Export.IExportService, Korkut.Infrastructure.Export.ExportService>();
        // Outbox review queue (module; distinct from the jobs-side IOutboxStore/OutboxStore that dispatches/sends).
        new Korkut.Outbox.Extensions.OutboxModule().Register(services, configuration);
        // Members (owner-safety lives in the service). The user aggregate + IUserRepository are
        // owned by the Organization module (registered above).
        // Invites (Clean Architecture: repository port + service + cross-tenant resolver port).
        services.AddScoped<Korkut.Organization.IInviteResolver, Korkut.Infrastructure.Invites.InviteResolver>();
        // Self-serve onboarding provisions a disabled office for a verified Google identity (raw RLS-bypass SQL).
        services.AddScoped<Korkut.Contract.Offices.IOfficeProvisioner, Korkut.Infrastructure.Offices.OfficeProvisioner>();

        return services;
    }
}
