using Korkut.Application.Errors;
using Korkut.Application.Offices.Responses;
using Korkut.Domain.Offices;

namespace Korkut.Application.Offices;

/// <summary>Office administration over the repository port.</summary>
public sealed class OfficeAdminService(IOfficeRepository repository) : IOfficeAdminService
{
    public async Task<IReadOnlyList<OfficeResponse>> ListAsync(CancellationToken ct = default)
        => (await repository.ListAsync(ct)).Select(ToResponse).ToList();

    public async Task SetDisabledAsync(Guid officeId, bool disabled, CancellationToken ct = default)
    {
        var office = await repository.FindForUpdateAsync(officeId, ct)
            ?? throw new NotFoundException("Ofis bulunamadı.");
        if (disabled)
        {
            office.Disable();
        }
        else
        {
            office.Enable();
        }

        await repository.SaveChangesAsync(ct);
    }

    public async Task<OfficeStatsResponse> StatsAsync(CancellationToken ct = default)
    {
        var offices = await repository.ListAsync(ct);
        var disabled = offices.Count(o => o.Disabled);
        return new OfficeStatsResponse(offices.Count, offices.Count - disabled, disabled);
    }

    private static OfficeResponse ToResponse(Office o) =>
        new(o.Id, o.Name, o.Disabled, o.RequireTwoFactor, o.CreatedAt);
}
