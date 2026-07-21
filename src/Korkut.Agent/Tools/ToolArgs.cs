using System.Text.Encodings.Web;
using System.Text.Json;

namespace Korkut.Agent.Tools;

/// <summary>Reads typed values out of a tool call's JSON arguments + serializes tool output (snake_case).</summary>
internal static class ToolArgs
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

    /// <summary>Serialize tool output to the shared snake_case JSON the model reads.</summary>
    public static string Result(object value) => JsonSerializer.Serialize(value, Json);
}
