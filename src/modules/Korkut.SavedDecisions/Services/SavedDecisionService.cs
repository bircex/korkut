using Korkut.Contract;
using Korkut.Contract.SavedDecisions;
using Korkut.Core;
using Korkut.SavedDecisions.Entities;
using Korkut.SavedDecisions.Repositories;

namespace Korkut.SavedDecisions.Services;

/// <summary>One service for the office's saved decisions. Every call runs in the caller's tenant scope.</summary>
internal sealed class SavedDecisionService(
    ISavedDecisionRepository decisions, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock)
    : ISavedDecisionService
{
    public Task<Result<IReadOnlyList<SavedDecisionResponse>>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<SavedDecisionResponse>>.Ok((await decisions.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<SavedDecisionResponse>> SaveAsync(SaveDecisionRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var existing = await decisions.FindBySourceAsync(office, request.Source, request.ExternalId, ct);
            if (existing is not null)
            {
                return Result<SavedDecisionResponse>.Ok(ToResponse(existing));
            }

            var saved = new SavedDecision(
                office, currentUser.UserId, request.Source, request.ExternalId, request.Title, request.Date,
                request.CaseNumber, request.DecisionNumber, request.Court, request.Chamber, request.Url,
                request.Snippet, clock.UtcNow);
            await decisions.AddAsync(saved, ct);
            return Result<SavedDecisionResponse>.Ok(ToResponse(saved));
        }, ct);

    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await decisions.RemoveAsync(office, id, ct) ? Result.Ok() : Result.NotFound("Kaydedilmiş karar bulunamadı."), ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static SavedDecisionResponse ToResponse(SavedDecision d) => new(
        d.Id, d.Source, d.ExternalId, d.Title, d.Date, d.CaseNumber, d.DecisionNumber, d.Court, d.Chamber,
        d.Url, d.Snippet, d.CreatedAt);
}
