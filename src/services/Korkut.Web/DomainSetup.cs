using Korkut.Core;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Persistence;

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
        new Korkut.Courts.CourtsModule().Register(services, configuration);
        new Korkut.Matters.MattersModule().Register(services, configuration);
        new Korkut.Hearings.HearingsModule().Register(services, configuration);

        // Modular monolith: self-registering vertical modules. Clients is the reference slice —
        // it binds its own repository + service; its EF mapping + controller are picked up by the
        // shared context and MVC application parts (see AddKorkutControllers).
        new Korkut.Clients.ClientsModule().Register(services, configuration);

        // Offices (the tenant root) are owned by the Organization module (registered below); the
        // caller's own profile still lives in the Users application service for now.

        // Clean-architecture cross-cutting: the caller identity + the tenant-pinned unit of work
        // every application service runs inside.
        services.AddHttpContextAccessor();
        services.AddScoped<Korkut.Core.ICurrentUser, Korkut.Web.Identity.HttpCurrentUser>();
        services.AddScoped<Korkut.Core.IUnitOfWork, Korkut.Persistence.UnitOfWork>();
        // Office-ops (Clean Architecture: repository ports + service).
        new Korkut.Notes.NotesModule().Register(services, configuration);
        new Korkut.Tasks.TasksModule().Register(services, configuration);
        new Korkut.Deadlines.DeadlinesModule().Register(services, configuration);
        // Finance: one module for invoices + expenses + the money roll-up (IFinanceService). The
        // reports dashboard reads finance totals through the module's IFinanceLookup.
        new Korkut.Finance.FinanceModule().Register(services, configuration);
        new Korkut.Correspondence.CorrespondenceModule().Register(services, configuration);
        // Calendar module. The cross-module read-model (ICalendarQuery: events ∪ deadlines ∪
        // hearings) is a host-level registration since its impl spans other modules' tables.
        new Korkut.Calendar.CalendarModule().Register(services, configuration);
        services.AddScoped<Korkut.Contract.Calendar.ICalendarQuery, Korkut.Infrastructure.Calendar.CalendarQuery>();
        // Office settings module (persona, style layers, memory). The memory author lookup impl
        // stays in Infrastructure (it reads the users table) and is registered here.
        new Korkut.Settings.SettingsModule().Register(services, configuration);
        // Organization (identity/tenant) module — grows in stages; office roles land first.
        new Korkut.Organization.OrganizationModule().Register(services, configuration);
        services.AddScoped<Korkut.Contract.Settings.IMemoryAuthorLookup, Korkut.Infrastructure.Settings.MemoryAuthorLookup>();
        // Reports (Clean Architecture: read-only aggregation query + service; finance is
        // computed from the same invoice/expense figures the finance screens use).
        services.AddScoped<Korkut.Application.Reports.IReportsQuery, Korkut.Infrastructure.Reports.ReportsQuery>();
        services.AddScoped<Korkut.Application.Reports.IReportsService, Korkut.Application.Reports.ReportsService>();
        new Korkut.Channels.ChannelsModule().Register(services, configuration);
        // The static legal catalog, and the two read-only services derived from it:
        // coverage (what the assistant can draft) and markdown export.
        services.AddSingleton(_ => new Korkut.Infrastructure.Templates.TemplateCatalog());
        services.AddScoped<Korkut.Application.Coverage.ICoverageCatalog, Korkut.Infrastructure.Coverage.CatalogCoverageReader>();
        services.AddScoped<Korkut.Application.Coverage.ICoverageService, Korkut.Application.Coverage.CoverageService>();
        services.AddSingleton<Korkut.Application.Export.IDocumentExporter, Korkut.Infrastructure.Export.MarkdownExporter>();
        services.AddScoped<Korkut.Application.Export.IExportService, Korkut.Application.Export.ExportService>();
        // Outbox review queue (module; distinct from the jobs-side IOutboxStore/OutboxStore that dispatches/sends).
        new Korkut.Outbox.OutboxModule().Register(services, configuration);
        // Members (owner-safety lives in the service). The user aggregate + IUserRepository are
        // owned by the Organization module (registered above).
        // Invites (Clean Architecture: repository port + service + cross-tenant resolver port).
        services.AddScoped<Korkut.Application.Invites.IInviteResolver, Korkut.Infrastructure.Invites.InviteResolver>();
        services.AddScoped<Korkut.Application.Invites.IInviteService, Korkut.Application.Invites.InviteService>();

        return services;
    }
}
