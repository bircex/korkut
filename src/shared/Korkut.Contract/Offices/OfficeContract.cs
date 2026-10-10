namespace Korkut.Contract.Offices;

/// <summary>The ids of a newly created office and its owner.</summary>
public sealed record ProvisionedOffice(Guid OfficeId, Guid UserId);

/// <summary>The office this install serves.</summary>
public sealed record InstallOffice(Guid Id, string Name, bool Disabled);

/// <summary>
/// Creates the install's office before any session/tenant context exists (through a narrow,
/// auditable RLS bypass): the first-run setup of a fresh install.
/// </summary>
public interface IOfficeProvisioner
{
    /// <summary>Whether this install already has its office (first-run setup is done).</summary>
    Task<bool> IsSetupCompleteAsync(CancellationToken ct = default);

    /// <summary>First-run setup: the install's office, enabled, and its owner. Null when an office
    /// already exists, so it can only ever succeed once.</summary>
    Task<ProvisionedOffice?> SetupFirstOfficeAsync(string email, string passwordHash, string officeName, string fullName, CancellationToken ct = default);

    /// <summary>The install's office, for work that runs outside any request (no signed-in caller
    /// to name it). Null before first-run setup.</summary>
    Task<InstallOffice?> InstallOfficeAsync(CancellationToken ct = default);
}
