using Korkut.Contract;
using Korkut.Contract.Notes;
using Korkut.Core;

namespace Korkut.Notes;

/// <summary>
/// Note use-cases over the repository port; an omitted occurrence time defaults to now. Every call
/// runs in the caller's tenant scope and RETURNS a <see cref="Result"/>.
/// </summary>
public sealed class NoteService(
    INoteRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : INoteService
{
    public Task<Result<IReadOnlyList<NoteResponse>>> ListAsync(Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<NoteResponse>>.Ok((await repository.ListAsync(office, matterId, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<NoteResponse>> CreateAsync(CreateNoteRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var now = clock.UtcNow;
            var note = new CommunicationNote(
                office, request.Body!.Trim(), request.Channel, request.MatterId, request.ClientId,
                request.OccurredAt ?? now, now);
            await repository.AddAsync(note, ct);
            return Result<NoteResponse>.Ok(ToResponse(note));
        }, ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static NoteResponse ToResponse(CommunicationNote n) =>
        new(n.Id, n.Body, n.Channel, n.MatterId, n.ClientId, n.OccurredAt, n.CreatedAt);
}
