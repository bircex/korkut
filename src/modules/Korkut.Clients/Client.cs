using Korkut.Core;

namespace Korkut.Clients;

/// <summary>
/// A tenant-owned client / müvekkil (individual or corporate). Retired by soft delete so the
/// matters and documents that reference it keep their history.
/// </summary>
public sealed class Client : Entity, ITenantOwned, ISoftDeletable, IAggregateRoot
{
    public Client(Guid officeId, string name, string kind, string? email, string? phone, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Name = name;
        Kind = kind;
        Email = email;
        Phone = phone;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Client()
    {
    }

    public Guid OfficeId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>"individual" | "corporate".</summary>
    public string Kind { get; private set; } = "individual";

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(string name, string kind, string? email, string? phone)
    {
        Name = name;
        Kind = kind;
        Email = email;
        Phone = phone;
    }

    public void SoftDelete(DateTimeOffset deletedAt) => DeletedAt = deletedAt;
}
