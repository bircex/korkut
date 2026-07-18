using System.Text.Json;
using System.Text.Json.Serialization;

namespace Korkut.Api.Json;

/// <summary>
/// Central JSON contract for the API, bound to the Python parity baseline
/// (docs/migration/compat/CONTRACTS.md): snake_case property names, enums as their
/// string values, nulls preserved (the Python API returns explicit nulls), and
/// ISO-8601 UTC timestamps. ASP.NET Core's default is camelCase, so this override
/// is load-bearing for client compatibility.
/// </summary>
public static class KorkutJson
{
    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        // Preserve nulls: the Python contract returns explicit null fields.
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        // Enums serialize as their string value (e.g. "owner", "open"), snake_cased.
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        options.PropertyNameCaseInsensitive = true;
    }
}
