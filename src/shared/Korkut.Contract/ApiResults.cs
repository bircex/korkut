using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Contract;

/// <summary>The handled-failure body, identical to the rest of the API: <c>{ "detail": "..." }</c>.</summary>
public sealed record ProblemBody(string Detail);

/// <summary>The list envelope the UI destructures: <c>{ "items": [...] }</c>.</summary>
public sealed record ListEnvelope<T>(IReadOnlyList<T> Items);

/// <summary>The paged list envelope: <c>{ "items": [...], "total": n }</c>.</summary>
public sealed record PagedEnvelope<T>(IReadOnlyList<T> Items, int Total);

/// <summary>
/// Maps a service <see cref="Result"/> / <see cref="Result{T}"/> onto the HTTP contract, so a module
/// controller stays a pure transport: success yields the value (or 204), and a non-Ok status becomes
/// the matching code with a <c>{ detail }</c> body — the same shape the exception handler emits.
/// </summary>
public static class ApiResults
{
    private static int ToStatusCode(ResultStatus status) => status switch
    {
        ResultStatus.Ok => StatusCodes.Status200OK,
        ResultStatus.NotFound => StatusCodes.Status404NotFound,
        ResultStatus.BadRequest => StatusCodes.Status400BadRequest,
        ResultStatus.Conflict => StatusCodes.Status409Conflict,
        ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
        ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
        ResultStatus.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static ObjectResult Failure(Result result) =>
        new(new ProblemBody(result.Error?.Detail ?? "Beklenmeyen bir hata oluştu."))
        {
            StatusCode = ToStatusCode(result.Status),
        };

    /// <summary>A valueless result: success -> 204 No Content, failure -> { detail } at the mapped status.</summary>
    public static IActionResult ToActionResult(this Result result) =>
        result.IsSuccess ? new NoContentResult() : Failure(result);

    /// <summary>
    /// A value result: success -> 200 with the value (or <paramref name="onSuccess"/> for a custom
    /// success shape, e.g. 201 Created), failure -> { detail } at the mapped status.
    /// </summary>
    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, IActionResult>? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess is null ? new OkObjectResult(result.Value) : onSuccess(result.Value!)
            : Failure(result);

    /// <summary>A list result: success -> 200 with <c>{ items }</c>, failure -> { detail }. Matches the
    /// UI's list-destructuring convention (the same shape as memory/sessions/calendar).</summary>
    public static IActionResult ToListActionResult<T>(this Result<IReadOnlyList<T>> result) =>
        result.IsSuccess ? new OkObjectResult(new ListEnvelope<T>(result.Value!)) : Failure(result);

    /// <summary>A paged list result: success -> 200 with <c>{ items, total }</c>, failure -> { detail }.</summary>
    public static IActionResult ToPagedActionResult<T>(this Result<IReadOnlyList<T>> result) =>
        result.IsSuccess ? new OkObjectResult(new PagedEnvelope<T>(result.Value!, result.Value!.Count)) : Failure(result);
}
