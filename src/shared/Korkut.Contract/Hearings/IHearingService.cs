namespace Korkut.Contract.Hearings;

/// <summary>Hearing (duruşma) use-cases, always in the context of a matter. Calls RETURN a <see cref="Result"/>.</summary>
public interface IHearingService
{
    /// <summary>Adds a hearing to the matter; NotFound if the matter is missing.</summary>
    Task<Result<HearingResponse>> AddAsync(Guid matterId, CreateHearingRequest request, CancellationToken ct = default);
}
