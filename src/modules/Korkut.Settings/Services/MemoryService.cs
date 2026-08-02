using Korkut.Contract;
using Korkut.Contract.Settings;
using Korkut.Core;
using Korkut.Settings.Entities;
using Korkut.Settings.Repositories;

namespace Korkut.Settings.Services;

/// <summary>
/// Office-memory orchestration over the repository port. Whether a new item lands active is decided
/// here, from the caller's permissions — the aggregate is only told which it is. Returns Result.
/// </summary>
internal sealed class MemoryService(
    IMemoryRepository repository,
    Korkut.Contract.Organization.IUserLookup authors,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IMemoryService
{
    public Task<Result<MemoryListResponse>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<MemoryListResponse>.Ok(new MemoryListResponse(await ToResponsesAsync(office, await repository.ListAsync(office, ct), ct))), ct);

    public Task<Result<MemoryItemResponse>> AddAsync(CreateMemoryRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var active = currentUser.Permissions.Contains(Permissions.OfficeSettings);
            var item = new MemoryItem(office, request.Text!.Trim(), request.Kind, currentUser.UserId, active, clock.UtcNow);
            await repository.AddAsync(item, ct);
            return Result<MemoryItemResponse>.Ok(await ToResponseAsync(office, item, ct));
        }, ct);

    public Task<Result<MemoryItemResponse>> ApproveAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var item = await repository.FindForUpdateAsync(office, id, ct);
            if (item is null)
            {
                return Result<MemoryItemResponse>.NotFound("Not bulunamadı.");
            }

            item.Approve();
            await repository.SaveChangesAsync(ct);
            return Result<MemoryItemResponse>.Ok(await ToResponseAsync(office, item, ct));
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await repository.RemoveAsync(office, id, ct) ? Result.Ok() : Result.NotFound("Not bulunamadı."), ct);

    private async Task<MemoryItemResponse> ToResponseAsync(Guid officeId, MemoryItem item, CancellationToken ct)
        => (await ToResponsesAsync(officeId, [item], ct))[0];

    /// <summary>Authors are resolved in one batch, so a long list is still a single lookup.</summary>
    private async Task<IReadOnlyList<MemoryItemResponse>> ToResponsesAsync(
        Guid officeId, IReadOnlyList<MemoryItem> items, CancellationToken ct)
    {
        var authorIds = items.Where(i => i.AuthorId is not null).Select(i => i.AuthorId!.Value).Distinct().ToList();
        var emails = await authors.DisplayNamesAsync(officeId, authorIds, ct);
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
