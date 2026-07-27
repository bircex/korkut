using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Settings;

/// <summary>A memory item with its author resolved to a display value (the user's e-mail).</summary>
public sealed record MemoryItemResponse(
    Guid Id, string Kind, string Text, string Status, string? Author, DateTimeOffset CreatedAt);

/// <summary>The office memory list, newest first.</summary>
public sealed record MemoryListResponse(IReadOnlyList<MemoryItemResponse> Items);

/// <summary>Body for adding a memory item.</summary>
public sealed record CreateMemoryRequest
{
    [Required(ErrorMessage = "Metin zorunludur.")]
    [MaxLength(4000, ErrorMessage = "Metin en fazla 4000 karakter olabilir.")]
    public string? Text { get; init; }

    [MaxLength(50, ErrorMessage = "Tür en fazla 50 karakter olabilir.")]
    public string Kind { get; init; } = "note";
}

/// <summary>Office-memory use-cases (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface IMemoryService
{
    Task<Result<MemoryListResponse>> ListAsync(CancellationToken ct = default);

    Task<Result<MemoryItemResponse>> AddAsync(CreateMemoryRequest request, CancellationToken ct = default);

    Task<Result<MemoryItemResponse>> ApproveAsync(Guid id, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
