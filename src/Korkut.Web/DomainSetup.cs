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
        services.AddScoped<IAuditLog, EfAuditLog>();
        // Office-core (Clean Architecture: clients + matters + hearings). Owns the
        // court-title snapshot rule in MatterService.
        services.AddScoped<Korkut.Application.Clients.IClientRepository, Korkut.Infrastructure.Clients.ClientRepository>();
        services.AddScoped<Korkut.Application.Matters.IMatterRepository, Korkut.Infrastructure.Matters.MatterRepository>();
        services.AddScoped<Korkut.Application.Courts.ICourtRepository, Korkut.Infrastructure.Courts.CourtRepository>();
        services.AddScoped<Korkut.Application.Hearings.IHearingRepository, Korkut.Infrastructure.Hearings.HearingRepository>();
        services.AddScoped<Korkut.Application.Clients.IClientService, Korkut.Application.Clients.ClientService>();

        // Clean-architecture cross-cutting: the caller identity + the tenant-pinned unit of work
        // every application service runs inside.
        services.AddHttpContextAccessor();
        services.AddScoped<Korkut.Application.Common.ICurrentUser, Korkut.Web.Identity.HttpCurrentUser>();
        services.AddScoped<Korkut.Application.Common.IUnitOfWork, Korkut.Infrastructure.Common.UnitOfWork>();
        services.AddScoped<Korkut.Application.Matters.IMatterService, Korkut.Application.Matters.MatterService>();
        services.AddScoped<Korkut.Application.Hearings.IHearingService, Korkut.Application.Hearings.HearingService>();
        // Office-ops (Clean Architecture: repository ports + service).
        services.AddScoped<Korkut.Application.Abstractions.Persistence.ITaskRepository, Korkut.Infrastructure.Persistence.Repositories.TaskRepository>();
        services.AddScoped<Korkut.Application.Abstractions.Persistence.IDeadlineRepository, Korkut.Infrastructure.Persistence.Repositories.DeadlineRepository>();
        services.AddScoped<Korkut.Application.Abstractions.Persistence.INoteRepository, Korkut.Infrastructure.Persistence.Repositories.NoteRepository>();
        services.AddScoped<Korkut.Application.Features.OfficeOps.IOfficeOpsService, Korkut.Application.Features.OfficeOps.OfficeOpsService>();
        // Finance (Clean Architecture: repository ports + service). The pattern rolls out
        // to the other modules incrementally.
        services.AddScoped<Korkut.Application.Abstractions.Persistence.IInvoiceRepository, Korkut.Infrastructure.Persistence.Repositories.InvoiceRepository>();
        services.AddScoped<Korkut.Application.Abstractions.Persistence.IExpenseRepository, Korkut.Infrastructure.Persistence.Repositories.ExpenseRepository>();
        services.AddScoped<Korkut.Application.Features.Finance.IFinanceService, Korkut.Application.Features.Finance.FinanceService>();
        services.AddScoped<Korkut.Application.Abstractions.Persistence.ICorrespondenceRepository, Korkut.Infrastructure.Persistence.Repositories.CorrespondenceRepository>();
        services.AddScoped<Korkut.Application.Features.Correspondence.ICorrespondenceService, Korkut.Application.Features.Correspondence.CorrespondenceService>();
        // Calendar (Clean Architecture: repository port + service; unified read view).
        services.AddScoped<Korkut.Application.Abstractions.Persistence.ICalendarRepository, Korkut.Infrastructure.Persistence.Repositories.CalendarRepository>();
        services.AddScoped<Korkut.Application.Features.Calendar.ICalendarService, Korkut.Application.Features.Calendar.CalendarService>();
        // Office settings (Clean Architecture: persona + style layers + memory).
        services.AddScoped<Korkut.Application.Abstractions.Persistence.IOfficeSettingsRepository, Korkut.Infrastructure.Persistence.Repositories.OfficeSettingsRepository>();
        services.AddScoped<Korkut.Application.Features.Settings.IOfficeSettingsService, Korkut.Application.Features.Settings.OfficeSettingsService>();
        // Reports (Clean Architecture: aggregation port + service; finance composed from IFinanceService).
        services.AddScoped<Korkut.Application.Abstractions.Persistence.IReportsRepository, Korkut.Infrastructure.Persistence.Repositories.ReportsRepository>();
        services.AddScoped<Korkut.Application.Features.Reports.IReportsService, Korkut.Application.Features.Reports.ReportsService>();
        // Channels (Clean Architecture: repository port + service; secrets encrypted in the adapter).
        services.AddScoped<Korkut.Application.Abstractions.Persistence.IChannelRepository, Korkut.Infrastructure.Persistence.Repositories.ChannelRepository>();
        services.AddScoped<Korkut.Application.Features.Channels.IChannelService, Korkut.Application.Features.Channels.ChannelService>();
        // Template overrides (Clean Architecture: repository port + service).
        services.AddScoped<Korkut.Application.Abstractions.Persistence.ITemplateOverrideRepository, Korkut.Infrastructure.Persistence.Repositories.TemplateOverrideRepository>();
        services.AddScoped<Korkut.Application.Features.Templates.ITemplateOverrideService, Korkut.Application.Features.Templates.TemplateOverrideService>();
        services.AddSingleton(_ => new Korkut.Infrastructure.Templates.TemplateCatalog());
        // Outbox review queue (Clean Architecture: repository port + service; distinct from
        // the jobs-side IOutboxStore/EfOutboxStore that dispatches/sends).
        services.AddScoped<Korkut.Application.Abstractions.Persistence.IOutboxRepository, Korkut.Infrastructure.Persistence.Repositories.OutboxRepository>();
        services.AddScoped<Korkut.Application.Features.Outbox.IOutboxService, Korkut.Application.Features.Outbox.OutboxService>();
        // Members (Clean Architecture: repository port + service; owner-safety in the service).
        services.AddScoped<Korkut.Application.Abstractions.Persistence.IMemberRepository, Korkut.Infrastructure.Persistence.Repositories.MemberRepository>();
        services.AddScoped<Korkut.Application.Features.Membership.IMembersService, Korkut.Application.Features.Membership.MembersService>();
        // Invites (Clean Architecture: repository port + service + cross-tenant resolver port).
        services.AddScoped<Korkut.Application.Abstractions.Persistence.IInviteRepository, Korkut.Infrastructure.Persistence.Repositories.InviteRepository>();
        services.AddScoped<Korkut.Application.Abstractions.Persistence.IInviteResolver, Korkut.Infrastructure.Persistence.Repositories.EfInviteResolver>();
        services.AddScoped<Korkut.Application.Features.Membership.IInviteService, Korkut.Application.Features.Membership.InviteService>();

        return services;
    }
}
