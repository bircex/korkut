using Korkut.Contract.Hearings;

namespace Korkut.Hearings;

/// <summary>Shared Hearing → HearingResponse mapping (used by the service and the cross-module lookup).</summary>
internal static class HearingMapping
{
    public static HearingResponse ToResponse(Hearing h) =>
        new(h.Id, h.ScheduledAt, h.Type, h.Location, h.Result, h.Notes, h.ReminderStatus);
}
