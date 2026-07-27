using Korkut.Core;

namespace Korkut.Expenses;

/// <summary>A tenant-owned expense, optionally attributed to a matter or a client.</summary>
public sealed class Expense : Entity, ITenantOwned, IAggregateRoot
{
    public Expense(
        Guid officeId, decimal amount, Guid? matterId, Guid? clientId, string? kind,
        string? description, DateTimeOffset spentAt, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Amount = amount;
        MatterId = matterId;
        ClientId = clientId;
        Kind = kind;
        Description = description;
        SpentAt = spentAt;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Expense()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid? MatterId { get; private set; }

    public Guid? ClientId { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>Free-form category ("harç", "yol", ...).</summary>
    public string? Kind { get; private set; }

    public string? Description { get; private set; }

    public DateTimeOffset? SpentAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
