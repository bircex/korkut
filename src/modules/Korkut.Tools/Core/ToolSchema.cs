using System.Text.Json;

namespace Korkut.Tools.Core;

/// <summary>Parses a JSON-Schema literal into a detached <see cref="JsonElement"/> for a tool's parameters.</summary>
public static class ToolSchema
{
    public static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
