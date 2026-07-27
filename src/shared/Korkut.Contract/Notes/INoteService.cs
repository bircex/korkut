namespace Korkut.Contract.Notes;

/// <summary>Communication-note use-cases (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface INoteService
{
    Task<Result<IReadOnlyList<NoteResponse>>> ListAsync(Guid? matterId, CancellationToken ct = default);

    Task<Result<NoteResponse>> CreateAsync(CreateNoteRequest request, CancellationToken ct = default);
}
