using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Web.Errors;

/// <summary>The Python-compatible error body for handled failures: <c>{ "detail": "..." }</c>.</summary>
public sealed record ErrorResponse(string Detail);

/// <summary>
/// Centralized exception → HTTP mapping. <see cref="ApiException"/> becomes its
/// declared status with a <c>{ detail }</c> body; anything unexpected becomes a
/// 500 with a generic detail (never leaks internals). Keeps every endpoint's error
/// shape consistent with the parity contract.
/// </summary>
public sealed partial class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, detail) = exception switch
        {
            ApiException api => (api.StatusCode, api.Detail),
            // Optimistic-concurrency conflict: never a silent overwrite — the client is
            // told to refresh and retry (KDM-014).
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Kayıt başka bir işlem tarafından değiştirildi. Lütfen yenileyip tekrar deneyin."),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu."),
        };

        if (status >= 500)
        {
            LogUnhandled(logger, exception);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ErrorResponse(detail), cancellationToken);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception mapped to 500")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
