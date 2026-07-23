using Korkut.Application.Hearings.Requests;
using Korkut.Application.Hearings.Responses;

namespace Korkut.Application.Hearings;

/// <summary>Hearing (duruşma) use-cases, always in the context of a matter.</summary>
public interface IHearingService
{
    /// <summary>Adds a hearing to the matter; raises <c>NotFoundException</c> if the matter is missing.</summary>
    Task<HearingResponse> AddAsync(Guid matterId, CreateHearingRequest request, CancellationToken ct = default);
}
