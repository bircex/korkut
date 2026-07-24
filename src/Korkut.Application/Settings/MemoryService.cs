using Korkut.Core;
using Korkut.Application.Errors;
using Korkut.Application.Security;
using Korkut.Application.Settings.Requests;
using Korkut.Application.Settings.Responses;
using Korkut.Domain.Settings;

namespace Korkut.Application.Settings;

/// <summary>
/// Office-memory orchestration over the repository port. Whether a new item lands active is
/// decided here, from the caller's permissions — the aggregate is only told which it is.
/// </summary>
public sealed class MemoryService(
    IMemoryRepository repository,
    IMemoryAuthorLookup authors,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IMemoryService
{
    public Task<MemoryListResponse> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            new MemoryListResponse(await ToResponsesAsync(office, await repository.ListAsync(office, ct), ct)), ct);

    public Task<MemoryItemResponse> AddAsync(CreateMemoryRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var active = currentUser.Permissions.Contains(Permissions.OfficeSettings);
            var item = new MemoryItem(office, request.Text!.Trim(), request.Kind, currentUser.UserId, active, clock.UtcNow);
            await repository.AddAsync(item, ct);
            return await ToResponseAsync(office, item, ct);
        }, ct);

    public Task<MemoryItemResponse> ApproveAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var item = await repository.FindForUpdateAsync(office, id, ct) ?? throw NotFound();
            item.Approve();
            await repository.SaveChangesAsync(ct);
            return await ToResponseAsync(office, item, ct);
        }, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await repository.RemoveAsync(office, id, ct) ? true : throw NotFound(), ct);

    private static NotFoundException NotFound() => new("Not bulunamadı.");

    private async Task<MemoryItemResponse> ToResponseAsync(Guid officeId, MemoryItem item, CancellationToken ct)
        => (await ToResponsesAsync(officeId, [item], ct))[0];

    /// <summary>Authors are resolved in one batch, so a long list is still a single lookup.</summary>
    private async Task<IReadOnlyList<MemoryItemResponse>> ToResponsesAsync(
        Guid officeId, IReadOnlyList<MemoryItem> items, CancellationToken ct)
    {
        var authorIds = items.Where(i => i.AuthorId is not null).Select(i => i.AuthorId!.Value).Distinct().ToList();
        var emails = await authors.EmailsAsync(officeId, authorIds, ct);
        return items.Select(i => new MemoryItemResponse(
            i.Id, i.Kind, i.Text, i.Status,
            i.AuthorId is not null && emails.TryGetValue(i.AuthorId.Value, out var email) ? email : null,
            i.CreatedAt)).ToList();
    }

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
