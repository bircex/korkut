using Korkut.Core;

namespace Korkut.Domain.Invoices;

/// <summary>
/// A tenant-owned invoice. <see cref="Amount"/> is net; <see cref="Gross"/> applies the VAT
/// rate and is the figure every roll-up uses, so rounding happens in exactly one place.
/// </summary>
public sealed class Invoice : Entity, ITenantOwned, IAggregateRoot
{
    public Invoice(
        Guid officeId, decimal amount, decimal vatRate, Guid? clientId, Guid? matterId,
        string? description, string status, DateTimeOffset issuedAt, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Amount = amount;
        VatRate = vatRate;
        ClientId = clientId;
        MatterId = matterId;
        Description = description;
        Status = status;
        IssuedAt = issuedAt;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Invoice()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid? ClientId { get; private set; }

    public Guid? MatterId { get; private set; }

    /// <summary>Net amount, before VAT.</summary>
    public decimal Amount { get; private set; }

    /// <summary>VAT rate as a fraction (0.20 = 20%).</summary>
    public decimal VatRate { get; private set; }

    public string? Description { get; private set; }

    /// <summary>"draft" | "sent" | "paid" | "cancelled".</summary>
    public string Status { get; private set; } = "draft";

    public DateTimeOffset? IssuedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Amount including VAT — the single rounding point for every total.</summary>
    public decimal Gross => Math.Round(Amount * (1m + VatRate), 2, MidpointRounding.ToEven);

    public bool IsCancelled => Status == "cancelled";

    public bool IsPaid => Status == "paid";

    /// <summary>Applies the supplied changes; omitted fields keep their current value.</summary>
    public void Update(string? status, decimal? amount)
    {
        if (status is not null)
        {
            Status = status;
        }

        if (amount is not null)
        {
            Amount = amount.Value;
        }
    }
}
