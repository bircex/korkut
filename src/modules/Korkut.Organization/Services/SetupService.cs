using Korkut.Contract;
using Korkut.Contract.Offices;
using Korkut.Contract.Organization;
using Korkut.Core;

namespace Korkut.Organization.Services;

/// <summary>
/// First-run setup of a fresh install: whoever opens it first names the office and becomes its
/// owner. It works exactly once; the database refuses a second office, even from two browsers
/// racing on the same fresh install.
/// </summary>
public sealed class SetupService(IOfficeProvisioner provisioner, IPasswordHasher passwordHasher) : ISetupService
{
    public async Task<SetupStatusResponse> StatusAsync(CancellationToken ct = default)
        => new(!await provisioner.IsSetupCompleteAsync(ct));

    public async Task<Result<SetupResponse>> SetupAsync(SetupRequest request, CancellationToken ct = default)
    {
        var created = await provisioner.SetupFirstOfficeAsync(
            request.Email!.Trim(), passwordHasher.Hash(request.Password!),
            request.OfficeName!.Trim(), request.FullName!.Trim(), ct);

        return created is null
            ? Result<SetupResponse>.Conflict("Bu kurulum zaten tamamlanmış. Giriş yapın.")
            : Result<SetupResponse>.Ok(new SetupResponse(created.OfficeId));
    }
}
