namespace Korkut.Application.Settings.Responses;

/// <summary>A memory item with its author resolved to a display value (the user's e-mail).</summary>
public sealed record MemoryItemResponse(
    Guid Id, string Kind, string Text, string Status, string? Author, DateTimeOffset CreatedAt);
