namespace Korkut.Organization;

/// <summary>Whose password a reset token can change, resolved before any session exists.</summary>
public sealed record ResetTokenOwner(Guid OfficeId, Guid UserId);
