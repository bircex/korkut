namespace Korkut.TestSupport;

/// <summary>
/// Test-side mirror of the API's list envelopes: domain list endpoints return <c>{ items: [...] }</c>
/// (and matters additionally <c>{ items, total }</c>). Deserialize a list response as
/// <c>Envelope&lt;T&gt;</c> and read <c>.Items</c>.
/// </summary>
public sealed record Envelope<T>(List<T> Items);

/// <summary>Paged variant: <c>{ items, total }</c>.</summary>
public sealed record PagedEnvelope<T>(List<T> Items, int Total);
