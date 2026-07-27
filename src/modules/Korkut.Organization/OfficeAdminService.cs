using Korkut.Contract;
using Korkut.Contract.Offices;

namespace Korkut.Organization;

/// <summary>Office administration over the repository port. Returns Result.</summary>
public sealed class OfficeAdminService(IOfficeRepository repository) : IOfficeAdminService
{
    public async Task<Result<IReadOnlyList<OfficeResponse>>> ListAsync(CancellationToken ct = default)
        => Result<IReadOnlyList<OfficeResponse>>.Ok((await repository.ListAsync(ct)).Select(ToResponse).ToList());

    public async Task<Result> SetDisabledAsync(Guid officeId, bool disabled, CancellationToken ct = default)
    {
        var office = await repository.FindForUpdateAsync(officeId, ct);
        if (office is null)
        {
            return Result.NotFound("Ofis bulunamadı.");
        }

        if (disabled)
        {
            office.Disable();
        }
        else
        {
            office.Enable();
        }

        await repository.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<OfficeStatsResponse>> StatsAsync(CancellationToken ct = default)
    {
        var offices = await repository.ListAsync(ct);
        var disabled = offices.Count(o => o.Disabled);
        return Result<OfficeStatsResponse>.Ok(new OfficeStatsResponse(offices.Count, offices.Count - disabled, disabled));
    }

    private static OfficeResponse ToResponse(Office o) =>
        new(o.Id, o.Name, o.Disabled, o.RequireTwoFactor, o.CreatedAt);
}
