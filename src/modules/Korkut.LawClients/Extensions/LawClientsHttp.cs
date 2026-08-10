using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.RateLimiting;

namespace Korkut.LawClients.Extensions;

/// <summary>
/// The named HTTP clients every source client dispatches through, with the "settings" the shared
/// clients were missing: a timeout, retry + circuit breaker (matching Korkut.Ai/AiSetup.cs's
/// "ai" client), and a QUEUING rate limiter — a politeness gate against the upstream public
/// databases (Bedesten has an observed ~1 request/3.5-4s tolerance; others get a conservative
/// default). Queuing (not rejecting) matters here: this same pipeline now serves both the agent
/// tool loop AND direct UI calls (Karar Arama), and a UI caller should wait briefly rather than
/// see a hard error just because another office is searching at the same moment.
/// </summary>
internal static class LawClientsHttp
{
    public static void Register(IServiceCollection services)
    {
        services.AddHttpClient("bedesten", c =>
            {
                c.BaseAddress = new Uri("https://bedesten.adalet.gov.tr");
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddResilienceHandler("lawclients-bedesten", builder => Configure(builder,
                CreateLimiter(permitLimit: 1, window: TimeSpan.FromSeconds(4))));

        // The Rekabet/GİB/KVKK/BDDK/Anayasa/Sayıştay/Uyuşmazlık clients each build their own
        // absolute URL per call (no shared BaseAddress) — "legal" for the normal-TLS sources.
        services.AddHttpClient("legal", c => c.Timeout = TimeSpan.FromSeconds(30))
            .AddResilienceHandler("lawclients-legal", builder => Configure(builder,
                CreateLimiter(permitLimit: 5, window: TimeSpan.FromSeconds(10))));

        // Kept as its own named client (its own rate-limit bucket) for Uyuşmazlık, which was
        // grouped with KİK historically even though — unlike KİK — it never actually needed any
        // special TLS handling (kararlar.uyusmazlik.gov.tr completes a normal handshake).
        services.AddHttpClient("legal-insecure", c => c.Timeout = TimeSpan.FromSeconds(30))
            .AddResilienceHandler("lawclients-legal-insecure", builder => Configure(builder,
                CreateLimiter(permitLimit: 5, window: TimeSpan.FromSeconds(10))));

        services.AddHttpClient("tavily", c => c.Timeout = TimeSpan.FromSeconds(30))
            .AddResilienceHandler("lawclients-tavily", builder => Configure(builder,
                CreateLimiter(permitLimit: 10, window: TimeSpan.FromSeconds(10))));

        // KİK is proxied through Korkut.KikProxy (KORKUT_KIK_PROXY_URL), a separate process/
        // container so its relaxed OpenSSL SECLEVEL (needed for ekap.kik.gov.tr's legacy TLS —
        // see docker/Dockerfile.kikproxy) never applies to this host's own outbound calls. This is
        // a plain internal call, so no public-API politeness limiter — the proxy already paces
        // its own upstream requests.
        services.AddHttpClient("kik-proxy", c => c.Timeout = TimeSpan.FromSeconds(30))
            .AddResilienceHandler("lawclients-kik-proxy", builder => builder
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 2,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions()));
    }

    // A shared, queuing sliding-window limiter: at most `permitLimit` requests per `window` per
    // host process, later callers wait in line (oldest first) instead of failing outright.
    private static SlidingWindowRateLimiter CreateLimiter(int permitLimit, TimeSpan window) => new(
        new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            SegmentsPerWindow = 4,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            // A bounded queue: past this, an interactive UI caller should see a clear "busy" error
            // rather than hang for however long a deep backlog would take to drain.
            QueueLimit = 20,
        });

    private static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder, RateLimiter limiter) => builder
        .AddRateLimiter(limiter)
        .AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        })
        .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions());
}
