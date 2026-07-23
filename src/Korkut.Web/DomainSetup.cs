using Korkut.Application.Auditing;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Persistence;
using Korkut.Domain.Time;

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
        // Office-core (Clean Architecture: clients + matters + hearings). Owns the
        // court-title snapshot rule in MatterService.
        services.AddScoped<Korkut.Application.Clients.IClientRepository, Korkut.Infrastructure.Clients.ClientRepository>();
        services.AddScoped<Korkut.Application.Matters.IMatterRepository, Korkut.Infrastructure.Matters.MatterRepository>();
        services.AddScoped<Korkut.Application.Courts.ICourtRepository, Korkut.Infrastructure.Courts.CourtRepository>();
        services.AddScoped<Korkut.Application.Hearings.IHearingRepository, Korkut.Infrastructure.Hearings.HearingRepository>();
        services.AddScoped<Korkut.Application.Clients.IClientService, Korkut.Application.Clients.ClientService>();

        // Offices (the tenant root: platform-admin management, cross-tenant by design) and
        // the caller's own profile.
        services.AddScoped<Korkut.Application.Offices.IOfficeRepository, Korkut.Infrastructure.Offices.OfficeRepository>();
        services.AddScoped<Korkut.Application.Offices.IOfficeAdminService, Korkut.Application.Offices.OfficeAdminService>();
        services.AddScoped<Korkut.Application.Users.IProfileService, Korkut.Application.Users.ProfileService>();

        // Clean-architecture cross-cutting: the caller identity + the tenant-pinned unit of work
        // every application service runs inside.
        services.AddHttpContextAccessor();
        services.AddScoped<Korkut.Application.Common.ICurrentUser, Korkut.Web.Identity.HttpCurrentUser>();
        services.AddScoped<Korkut.Application.Common.IUnitOfWork, Korkut.Infrastructure.Common.UnitOfWork>();
        services.AddScoped<Korkut.Application.Matters.IMatterService, Korkut.Application.Matters.MatterService>();
        services.AddScoped<Korkut.Application.Hearings.IHearingService, Korkut.Application.Hearings.HearingService>();
        // Office-ops (Clean Architecture: repository ports + service).
        services.AddScoped<Korkut.Application.Tasks.ITaskRepository, Korkut.Infrastructure.Tasks.TaskRepository>();
        services.AddScoped<Korkut.Application.Deadlines.IDeadlineRepository, Korkut.Infrastructure.Deadlines.DeadlineRepository>();
        services.AddScoped<Korkut.Application.Notes.INoteRepository, Korkut.Infrastructure.Notes.NoteRepository>();
        services.AddScoped<Korkut.Application.Tasks.ITaskService, Korkut.Application.Tasks.TaskService>();
        services.AddScoped<Korkut.Application.Deadlines.IDeadlineService, Korkut.Application.Deadlines.DeadlineService>();
        services.AddScoped<Korkut.Application.Notes.INoteService, Korkut.Application.Notes.NoteService>();
        // Finance (Clean Architecture: repository ports + service). The pattern rolls out
        // to the other modules incrementally.
        services.AddScoped<Korkut.Application.Invoices.IInvoiceRepository, Korkut.Infrastructure.Invoices.InvoiceRepository>();
        services.AddScoped<Korkut.Application.Expenses.IExpenseRepository, Korkut.Infrastructure.Expenses.ExpenseRepository>();
        services.AddScoped<Korkut.Application.Invoices.IInvoiceService, Korkut.Application.Invoices.InvoiceService>();
        services.AddScoped<Korkut.Application.Expenses.IExpenseService, Korkut.Application.Expenses.ExpenseService>();
        services.AddScoped<Korkut.Application.FinanceSummary.IFinanceSummaryService, Korkut.Application.FinanceSummary.FinanceSummaryService>();
        services.AddScoped<Korkut.Application.Correspondence.ICorrespondenceRepository, Korkut.Infrastructure.Correspondence.CorrespondenceRepository>();
        services.AddScoped<Korkut.Application.Correspondence.ICorrespondenceService, Korkut.Application.Correspondence.CorrespondenceService>();
        // Calendar (Clean Architecture: repository port + service; unified read view).
        services.AddScoped<Korkut.Application.Calendar.ICalendarEventRepository, Korkut.Infrastructure.Calendar.CalendarEventRepository>();
        services.AddScoped<Korkut.Application.Calendar.ICalendarQuery, Korkut.Infrastructure.Calendar.CalendarQuery>();
        services.AddScoped<Korkut.Application.Calendar.ICalendarService, Korkut.Application.Calendar.CalendarService>();
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
        // Channels (Clean Architecture: repository port + service; secrets encrypted in the service).
        services.AddScoped<Korkut.Application.Channels.IChannelRepository, Korkut.Infrastructure.Channels.ChannelRepository>();
        services.AddScoped<Korkut.Application.Channels.IChannelService, Korkut.Application.Channels.ChannelService>();
        // The static legal catalog, and the two read-only services derived from it:
        // coverage (what the assistant can draft) and markdown export.
        services.AddSingleton(_ => new Korkut.Infrastructure.Templates.TemplateCatalog());
        services.AddScoped<Korkut.Application.Coverage.ICoverageCatalog, Korkut.Infrastructure.Coverage.CatalogCoverageReader>();
        services.AddScoped<Korkut.Application.Coverage.ICoverageService, Korkut.Application.Coverage.CoverageService>();
        services.AddSingleton<Korkut.Application.Export.IDocumentExporter, Korkut.Infrastructure.Export.MarkdownExporter>();
        services.AddScoped<Korkut.Application.Export.IExportService, Korkut.Application.Export.ExportService>();
        // Outbox review queue (Clean Architecture: repository port + service; distinct from
        // the jobs-side IOutboxStore/EfOutboxStore that dispatches/sends).
        services.AddScoped<Korkut.Application.Outbox.IOutboxRepository, Korkut.Infrastructure.Outbox.OutboxRepository>();
        services.AddScoped<Korkut.Application.Outbox.IOutboxService, Korkut.Application.Outbox.OutboxService>();
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
