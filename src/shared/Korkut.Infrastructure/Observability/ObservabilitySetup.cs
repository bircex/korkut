using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Korkut.Infrastructure.Observability;

/// <summary>
/// Host-agnostic OpenTelemetry wiring shared by the API and Worker: a common
/// resource (service name/version), traces (HttpClient + runtime), metrics, and
/// logs that are <b>redacted before export</b>. The OTLP exporter is added only
/// when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is configured, so nothing is exported
/// (or attempted) in tests/dev without an explicit collector.
/// </summary>
public static class ObservabilitySetup
{
    /// <summary>ActivitySource name for app-authored spans.</summary>
    public const string ActivitySourceName = Korkut.Core.Observability.ActivitySourceName;

    public static IHostApplicationBuilder AddKorkutObservability(
        this IHostApplicationBuilder builder,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null)
    {
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var resource = ResourceBuilder.CreateDefault().AddService(serviceName);

        // Logs: redact BEFORE any exporter sees the record.
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resource);
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.AddProcessor(new RedactingLogProcessor());
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                options.AddOtlpExporter();
            }
        });

        var otel = builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService(serviceName));

        otel.WithTracing(tracing =>
        {
            tracing.AddSource(ActivitySourceName)
                .AddHttpClientInstrumentation(o =>
                    // The Telegram Bot API carries the bot token in the URL path (/bot<token>/...).
                    o.FilterHttpRequestMessage = request =>
                        request.RequestUri?.AbsolutePath.StartsWith("/bot", StringComparison.Ordinal) != true);
            configureTracing?.Invoke(tracing);
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                tracing.AddOtlpExporter();
            }
        });

        otel.WithMetrics(metrics =>
        {
            metrics.AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation();
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                metrics.AddOtlpExporter();
            }
        });

        return builder;
    }
}
