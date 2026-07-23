namespace Korkut.Domain.Offices;

/// <summary>The ids of a newly provisioned (disabled) office and its owner.</summary>
public sealed record ProvisionedOffice(Guid OfficeId, Guid UserId);
