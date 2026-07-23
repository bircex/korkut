using Korkut.Domain.Offices;

namespace Korkut.Application.Offices;

/// <summary>
/// Creates a brand-new tenant — a disabled office plus its owner — for self-serve signup.
/// This happens before any session or tenant context exists, so the adapter goes through a
/// narrow, auditable RLS bypass rather than the app role. Null when the e-mail or Google
/// subject already has an account: nothing is created.
/// </summary>
public interface IOfficeProvisioner
{
    Task<ProvisionedOffice?> ProvisionAsync(string email, string googleSub, string officeName, CancellationToken ct = default);
}
