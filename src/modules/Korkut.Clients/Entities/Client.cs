using Korkut.Core;

namespace Korkut.Clients.Entities;

/// <summary>
/// A tenant-owned client / müvekkil (individual or corporate). Retired by soft delete so the
/// matters and documents that reference it keep their history.
/// </summary>
public sealed class Client : Entity, ITenantOwned, ISoftDeletable, IAggregateRoot
{
    public Client(
        Guid officeId, string fullName, string kind, string? email, string? phone,
        string? nationalId, string? address, DateOnly? dateOfBirth, DateTimeOffset createdAt, Guid? createdBy = null)
        : base(NewId())
    {
        CreatedBy = createdBy;
        OfficeId = officeId;
        FullName = fullName;
        Kind = kind;
        Email = email;
        Phone = phone;
        NationalId = nationalId;
        Address = address;
        DateOfBirth = dateOfBirth;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Client()
    {
    }

    public Guid OfficeId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>"individual" | "corporate".</summary>
    public string Kind { get; private set; } = "individual";

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    /// <summary>National identity number (TR TCKN for individuals); optional.</summary>
    public string? NationalId { get; private set; }

    public string? Address { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Who added the client; they see it even before it has a matter.</summary>
    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(
        string fullName, string kind, string? email, string? phone,
        string? nationalId, string? address, DateOnly? dateOfBirth)
    {
        FullName = fullName;
        Kind = kind;
        Email = email;
        Phone = phone;
        NationalId = nationalId;
        Address = address;
        DateOfBirth = dateOfBirth;
    }

    public void SoftDelete(DateTimeOffset deletedAt) => DeletedAt = deletedAt;
}
