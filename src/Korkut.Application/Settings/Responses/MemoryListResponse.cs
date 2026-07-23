namespace Korkut.Application.Settings.Responses;

/// <summary>The office memory list, newest first.</summary>
public sealed record MemoryListResponse(IReadOnlyList<MemoryItemResponse> Items);
