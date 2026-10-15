namespace Korkut.Contract.Clients;

/// <summary>
/// Transport shape for a client. Serialized snake_case by the global JSON contract. Contact details
/// (e-mail, phone, address) and identity details (national id, date of birth) come back empty, with
/// the matching flag set, to a caller without the permission to see them.
/// </summary>
public sealed record ClientResponse(
    Guid Id,
    string FullName,
    string Kind,
    string? Email,
    string? Phone,
    string? NationalId,
    string? Address,
    DateOnly? DateOfBirth,
    DateTimeOffset CreatedAt,
    bool ContactHidden = false,
    bool IdentityHidden = false)
{
    /// <summary>The same client with the details the caller may not see taken out.</summary>
    public ClientResponse Masked(bool canSeeContact, bool canSeeIdentity) => this with
    {
        Email = canSeeContact ? Email : null,
        Phone = canSeeContact ? Phone : null,
        Address = canSeeContact ? Address : null,
        ContactHidden = !canSeeContact,
        NationalId = canSeeIdentity ? NationalId : null,
        DateOfBirth = canSeeIdentity ? DateOfBirth : null,
        IdentityHidden = !canSeeIdentity,
    };
}
