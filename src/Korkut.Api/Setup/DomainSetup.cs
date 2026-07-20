using Korkut.Application.Auditing;
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
        services.AddScoped<OfficeOpsStore>();
        services.AddScoped<FinanceStore>();
        services.AddScoped<CorrespondenceStore>();
        services.AddScoped<CalendarStore>();
        services.AddScoped<OfficeSettingsStore>();
        services.AddScoped<ReportsStore>();
        services.AddScoped<ChannelStore>();
        services.AddScoped<MembersStore>();
        services.AddScoped<InviteStore>();
        services.AddScoped<EfInviteResolver>();

        return services;
    }
}
