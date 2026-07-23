using Korkut.Application.Settings.Requests;
using Korkut.Application.Settings.Responses;

namespace Korkut.Application.Settings;

/// <summary>
/// Office-memory use-cases: anyone who works the office's matters may propose a fact for
/// the assistant to remember, but only someone who can change office settings decides what
/// becomes active. An owner's own item is active the moment it is added.
/// </summary>
public interface IMemoryService
{
    Task<MemoryListResponse> ListAsync(CancellationToken ct = default);

    Task<MemoryItemResponse> AddAsync(CreateMemoryRequest request, CancellationToken ct = default);

    Task<MemoryItemResponse> ApproveAsync(Guid id, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
