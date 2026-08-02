using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Korkut.Tools.Core;

/// <summary>Reads typed values out of a tool call's JSON arguments and serializes tool output (snake_case).</summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name",
    Justification = "String/Int/Bool/Date read the argument of that type; the names mirror the JSON shape a tool author reasons about.")]
public static class ToolArgs
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Tool output goes to the model as readable text; don't \u-escape Turkish characters.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string? String(JsonElement args, string name)
        => args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static bool TryGuid(JsonElement args, string name, out Guid value)
    {
        value = default;
        return String(args, name) is { } s && Guid.TryParse(s, out value);
    }

    public static int? Int(JsonElement args, string name)
        => args.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : null;

    public static decimal? Decimal(JsonElement args, string name)
        => args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : null;

    public static bool Bool(JsonElement args, string name)
        => args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    public static DateTimeOffset? DateTime(JsonElement args, string name)
        => String(args, name) is { } s && DateTimeOffset.TryParse(s, out var d) ? d : null;

    public static DateOnly? Date(JsonElement args, string name)
        => String(args, name) is { } s && DateOnly.TryParse(s, out var d) ? d : null;

    /// <summary>Serialize tool output to the shared snake_case JSON the model reads.</summary>
    public static string Result(object value) => JsonSerializer.Serialize(value, Json);
}
