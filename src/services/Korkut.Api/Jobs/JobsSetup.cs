using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;

namespace Korkut.Api.Jobs;

public static class JobsSetup
{
    /// <summary>Setting that turns the background job runner off (tests that drive jobs themselves).</summary>
    public const string EnabledSetting = "KORKUT_BACKGROUND_JOBS";

    /// <summary>
    /// The job handlers, and the runner that executes them from the outbox. The handlers are
    /// always registered so a caller can run a job directly; the runner only when enabled.
    /// </summary>
    public static IServiceCollection AddKorkutBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ITenantContext, AmbientTenantContext>();
        services.AddSingleton<IRetryPolicy>(new ExponentialRetryPolicy());
        services.AddScoped<IJobHandler, DocumentIndexJobHandler>();
        services.AddScoped<IJobHandler, DocumentDeindexJobHandler>();

        if (!string.Equals(configuration[EnabledSetting], "false", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHostedService<OutboxProcessorService>();
        }

        return services;
    }
}
