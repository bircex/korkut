using Korkut.Application.Notes.Requests;
using Korkut.Application.Notes.Responses;

namespace Korkut.Application.Notes;

/// <summary>Communication-note use-cases.</summary>
public interface INoteService
{
    Task<IReadOnlyList<NoteResponse>> ListAsync(Guid? matterId, CancellationToken ct = default);

    Task<NoteResponse> CreateAsync(CreateNoteRequest request, CancellationToken ct = default);
}
