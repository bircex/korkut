namespace Korkut.Api.Middleware;

/// <summary>
/// Ensures every request/response carries a correlation id. Reuses an inbound
/// <c>X-Correlation-ID</c> when present (so a value set at the edge/proxy flows
/// through), otherwise mints one. The id is echoed on the response and pushed into
/// the log scope so all logs for a request are correlated.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming)
                ? incoming.ToString()
                : Guid.CreateVersion7().ToString();

        context.Response.Headers[HeaderName] = correlationId;

        // Tag the active trace with the correlation id — an opaque, tenant-safe id
        // (never PII), so a request's logs and spans line up in the backend.
        System.Diagnostics.Activity.Current?.SetTag("korkut.correlation_id", correlationId);

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
