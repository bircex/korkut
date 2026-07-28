using System.Text.Json;

namespace Korkut.Tools.Core;

/// <summary>
/// A tool's self-description handed to the model so it knows when and how to call it: the
/// <see cref="Name"/>, a human <see cref="Description"/>, and a JSON Schema for its arguments.
/// </summary>
public sealed record ToolSpec(string Name, string Description, JsonElement Parameters);
