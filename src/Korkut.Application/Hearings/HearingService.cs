using Korkut.Application.Common;
using Korkut.Application.Errors;
using Korkut.Application.Hearings.Requests;
using Korkut.Application.Hearings.Responses;
using Korkut.Application.Matters;
using Korkut.Domain.Hearings;

namespace Korkut.Application.Hearings;

/// <summary>
/// Hearing use-cases over the repository ports. The matter must exist and be active before a
/// hearing can be attached (the tenant-aware composite FK enforces the office on top of that).
/// </summary>
public sealed class HearingService(
    IHearingRepository hearings, IMatterRepository matters, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    : IHearingService
{
    public Task<HearingResponse> AddAsync(Guid matterId, CreateHearingRequest request, CancellationToken ct = default)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, async () =>
        {
            if (await matters.FindAsync(office, matterId, ct) is null)
            {
                throw new NotFoundException("Dosya bulunamadı.");
            }

            // Date is required; time is optional (defaults to midnight). Treated as UTC to match
            // the agenda/calendar comparisons, which are all against UtcNow.
            var scheduledAt = new DateTimeOffset(
                request.Date!.Value.ToDateTime(request.Time ?? TimeOnly.MinValue), TimeSpan.Zero);

            var hearing = new Hearing(office, matterId, scheduledAt, request.Type, request.Location, request.Notes);
            await hearings.AddAsync(hearing, ct);
            return ToResponse(hearing);
        }, ct);
    }

    internal static HearingResponse ToResponse(Hearing h) =>
        new(h.Id, h.ScheduledAt, h.Type, h.Location, h.Result, h.Notes, h.ReminderStatus);
}
