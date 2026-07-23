using Korkut.Application.Common;
using Korkut.Application.Notes.Requests;
using Korkut.Application.Notes.Responses;
using Korkut.Domain.Notes;
using Korkut.Domain.Time;

namespace Korkut.Application.Notes;

/// <summary>Note use-cases over the repository port; an omitted occurrence time defaults to now.</summary>
public sealed class NoteService(
    INoteRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : INoteService
{
    public Task<IReadOnlyList<NoteResponse>> ListAsync(Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
            (IReadOnlyList<NoteResponse>)(await repository.ListAsync(office, matterId, ct)).Select(ToResponse).ToList(), ct);

    public Task<NoteResponse> CreateAsync(CreateNoteRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var now = clock.UtcNow;
            var note = new CommunicationNote(
                office, request.Body!.Trim(), request.Channel, request.MatterId, request.ClientId,
                request.OccurredAt ?? now, now);
            await repository.AddAsync(note, ct);
            return ToResponse(note);
        }, ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static NoteResponse ToResponse(CommunicationNote n) =>
        new(n.Id, n.Body, n.Channel, n.MatterId, n.ClientId, n.OccurredAt, n.CreatedAt);
}
