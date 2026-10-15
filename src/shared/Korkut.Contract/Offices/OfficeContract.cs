namespace Korkut.Contract.Offices;

/// <summary>The ids of a newly created office and its owner.</summary>
public sealed record ProvisionedOffice(Guid OfficeId, Guid UserId);

/// <summary>The office this install serves.</summary>
public sealed record InstallOffice(Guid Id, string Name, bool Disabled);

/// <summary>
/// Creates the install's office before any session/tenant context exists (through a narrow,
/// auditable RLS bypass): creating the installation's office and admin.
/// </summary>
public interface IOfficeProvisioner
{
    /// <summary>The install's office, enabled, and its first owner, created at installation. Null when
    /// an office already exists, so it can only ever succeed once.</summary>
    Task<ProvisionedOffice?> SetupFirstOfficeAsync(string email, string passwordHash, string officeName, string fullName, CancellationToken ct = default);

    /// <summary>The install's office, for work that runs outside any request (no signed-in caller
    /// to name it). Null until the app has started once.</summary>
    Task<InstallOffice?> InstallOfficeAsync(CancellationToken ct = default);
}
