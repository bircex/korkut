using Korkut.Application.Auditing;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Persistence;
using Korkut.SharedKernel.Time;

namespace Korkut.Api.Setup;

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
        services.AddScoped<OfficeCoreStore>();
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
        services.AddScoped<CalendarStore>();
        services.AddScoped<OfficeSettingsStore>();
        services.AddScoped<ReportsStore>();
        services.AddScoped<ChannelStore>();
        services.AddScoped<TemplateOverrideStore>();
        services.AddSingleton(_ => new Korkut.Infrastructure.Templates.TemplateCatalog());
        services.AddScoped<OutboxStore>();
        // Members (Clean Architecture: repository port + service; owner-safety in the service).
        services.AddScoped<Korkut.Application.Abstractions.Persistence.IMemberRepository, Korkut.Infrastructure.Persistence.Repositories.MemberRepository>();
        services.AddScoped<Korkut.Application.Features.Membership.IMembersService, Korkut.Application.Features.Membership.MembersService>();
        services.AddScoped<InviteStore>();
        services.AddScoped<EfInviteResolver>();

        return services;
    }
}
