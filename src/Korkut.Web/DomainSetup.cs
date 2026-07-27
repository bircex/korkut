using Korkut.Application.Auditing;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Persistence;
using Korkut.Core;

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

        // Offices (the tenant root: platform-admin management, cross-tenant by design) and
        // the caller's own profile.
        services.AddScoped<Korkut.Application.Offices.IOfficeRepository, Korkut.Infrastructure.Offices.OfficeRepository>();
        services.AddScoped<Korkut.Application.Offices.IOfficeAdminService, Korkut.Application.Offices.OfficeAdminService>();
        services.AddScoped<Korkut.Application.Users.IProfileService, Korkut.Application.Users.ProfileService>();

        // Clean-architecture cross-cutting: the caller identity + the tenant-pinned unit of work
        // every application service runs inside.
        services.AddHttpContextAccessor();
        services.AddScoped<Korkut.Core.ICurrentUser, Korkut.Web.Identity.HttpCurrentUser>();
        services.AddScoped<Korkut.Core.IUnitOfWork, Korkut.Persistence.UnitOfWork>();
        // Office-ops (Clean Architecture: repository ports + service).
        new Korkut.Notes.NotesModule().Register(services, configuration);
        new Korkut.Tasks.TasksModule().Register(services, configuration);
        new Korkut.Deadlines.DeadlinesModule().Register(services, configuration);
        // Finance modules. The finance read-models (FinanceSummary, Reports) stay in
        // Application/Infrastructure and read the modules' repository ports transitionally.
        new Korkut.Invoices.InvoicesModule().Register(services, configuration);
        new Korkut.Expenses.ExpensesModule().Register(services, configuration);
        services.AddScoped<Korkut.Application.FinanceSummary.IFinanceSummaryService, Korkut.Application.FinanceSummary.FinanceSummaryService>();
        new Korkut.Correspondence.CorrespondenceModule().Register(services, configuration);
        // Calendar module. The cross-module read-model (ICalendarQuery: events ∪ deadlines ∪
        // hearings) is a host-level registration since its impl spans other modules' tables.
        new Korkut.Calendar.CalendarModule().Register(services, configuration);
        services.AddScoped<Korkut.Contract.Calendar.ICalendarQuery, Korkut.Infrastructure.Calendar.CalendarQuery>();
        // Office settings (Clean Architecture: persona, style layers and memory, one slice each).
        services.AddScoped<Korkut.Application.Settings.IPersonaRepository, Korkut.Infrastructure.Settings.PersonaRepository>();
        services.AddScoped<Korkut.Application.Settings.IPersonaService, Korkut.Application.Settings.PersonaService>();
        services.AddScoped<Korkut.Application.Settings.IStyleProfileRepository, Korkut.Infrastructure.Settings.StyleProfileRepository>();
        services.AddScoped<Korkut.Application.Settings.IStyleService, Korkut.Application.Settings.StyleService>();
        services.AddScoped<Korkut.Application.Settings.IMemoryRepository, Korkut.Infrastructure.Settings.MemoryRepository>();
        services.AddScoped<Korkut.Application.Settings.IMemoryAuthorLookup, Korkut.Infrastructure.Settings.MemoryAuthorLookup>();
        services.AddScoped<Korkut.Application.Settings.IMemoryService, Korkut.Application.Settings.MemoryService>();
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
        // Users + members (Clean Architecture: the user aggregate, viewed as the office's
        // members; owner-safety lives in the service).
        services.AddScoped<Korkut.Application.Users.IUserRepository, Korkut.Infrastructure.Users.UserRepository>();
        services.AddScoped<Korkut.Application.Members.IMemberService, Korkut.Application.Members.MemberService>();
        // Invites (Clean Architecture: repository port + service + cross-tenant resolver port).
        services.AddScoped<Korkut.Application.Invites.IInviteRepository, Korkut.Infrastructure.Invites.InviteRepository>();
        services.AddScoped<Korkut.Application.Invites.IInviteResolver, Korkut.Infrastructure.Invites.InviteResolver>();
        services.AddScoped<Korkut.Application.Invites.IInviteService, Korkut.Application.Invites.InviteService>();

        return services;
    }
}
