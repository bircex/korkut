namespace Korkut.Api.Contracts;

/// <summary>
/// The list-response envelope used across the API: matches the Python contract
/// <c>{ "items": [...], "total": &lt;int&gt; }</c>. A transport DTO — endpoints map
/// domain objects into item DTOs; domain entities are never serialized directly.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total);

/// <summary>Non-generic factory helpers for <see cref="PagedResult{T}"/>.</summary>
public static class PagedResult
{
    public static PagedResult<T> From<T>(IReadOnlyList<T> items) => new(items, items.Count);
}
