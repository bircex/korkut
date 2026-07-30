namespace Korkut.Contract.Offices;

/// <summary>An office as the platform-admin console lists it.</summary>
public sealed record OfficeResponse(Guid Id, string Name, bool Disabled, bool RequireTwoFactor, DateTimeOffset CreatedAt);

/// <summary>Platform-wide office counts for the admin console.</summary>
public sealed record OfficeStatsResponse(int Offices, int ActiveOffices, int DisabledOffices);

/// <summary>The ids of a newly provisioned (disabled) office and its owner.</summary>
public sealed record ProvisionedOffice(Guid OfficeId, Guid UserId);

/// <summary>
/// Platform-admin office management (cross-tenant by design; nothing here runs in a tenant scope).
/// Calls RETURN a <see cref="Result"/>.
/// </summary>
public interface IOfficeAdminService
{
    Task<Result<IReadOnlyList<OfficeResponse>>> ListAsync(CancellationToken ct = default);

    Task<Result> SetDisabledAsync(Guid officeId, bool disabled, CancellationToken ct = default);

    Task<Result<OfficeStatsResponse>> StatsAsync(CancellationToken ct = default);
}

/// <summary>
/// Creates a brand-new tenant — a disabled office plus its owner — for self-serve signup, before
/// any session/tenant context exists (through a narrow, auditable RLS bypass). Null when the e-mail
/// or Google subject already has an account.
/// </summary>
public interface IOfficeProvisioner
{
    Task<ProvisionedOffice?> ProvisionAsync(string email, string googleSub, string officeName, CancellationToken ct = default);

    /// <summary>Local (e-mail + password) self-serve signup: a disabled office + its owner, whose
    /// password hash is stored so they can sign in once a platform admin enables the office. Null
    /// when the e-mail already has an account.</summary>
    Task<ProvisionedOffice?> ProvisionLocalAsync(string email, string passwordHash, string officeName, CancellationToken ct = default);
}
