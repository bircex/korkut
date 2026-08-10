using System.Text.Json;

namespace Korkut.LawClients.Services.Shared;

/// <summary>
/// Reads typed values out of a raw JSON arguments element. Local copy of the agent tool loop's
/// <c>Korkut.Tools.Core.ToolArgs</c> reader methods — this module must not depend on Korkut.Tools
/// (the dependency runs the other way: tools call INTO this module via Contract), so the tiny
/// JsonElement-reading helper each source client needs is duplicated here rather than shared.
/// </summary>
internal static class ToolArgs
{
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
}
