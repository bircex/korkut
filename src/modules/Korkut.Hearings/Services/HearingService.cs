using Korkut.Hearings.Repositories;
using Korkut.Hearings.Entities;
using Korkut.Contract;
using Korkut.Contract.Hearings;
using Korkut.Contract.Matters;
using Korkut.Core;

namespace Korkut.Hearings.Services;

/// <summary>
/// Hearing use-cases over the repository port. The matter must exist and be active before a hearing
/// can be attached (checked via the cross-module <see cref="IMatterLookup"/>; the tenant-aware
/// composite FK enforces the office on top of that). Every call RETURNS a <see cref="Result"/>.
/// </summary>
internal sealed class HearingService(
    IHearingRepository hearings, IMatterLookup matters, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    : IHearingService
{
    public Task<Result<HearingResponse>> AddAsync(Guid matterId, CreateHearingRequest request, CancellationToken ct = default)
    {
        var office = currentUser.OfficeId;
        
        return unitOfWork.InTenantScopeAsync(office, async () =>
        {
            if (!await matters.ExistsAsync(office, matterId, ct))
            {
                return Result<HearingResponse>.NotFound("Dosya bulunamadı.");
            }

            // Date is required; time is optional (defaults to midnight). Treated as UTC to match
            // the agenda/calendar comparisons, which are all against UtcNow.
            var scheduledAt = new DateTimeOffset(
                request.Date!.Value.ToDateTime(request.Time ?? TimeOnly.MinValue), TimeSpan.Zero);

            var hearing = new Hearing(office, matterId, scheduledAt, request.Type, request.Location, request.Notes);
            await hearings.AddAsync(hearing, ct);
            return Result<HearingResponse>.Ok(HearingMapping.ToResponse(hearing));
        }, ct);
    }
}
