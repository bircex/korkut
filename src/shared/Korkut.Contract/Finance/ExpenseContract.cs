using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Finance;

/// <summary>Transport shape for an expense.</summary>
public sealed record ExpenseResponse(
    Guid Id, Guid? MatterId, Guid? ClientId, decimal Amount, string? Kind, string? Description, DateTimeOffset? SpentAt);

/// <summary>Body for recording an expense.</summary>
public sealed record CreateExpenseRequest
{
    [Range(0, 100_000_000)]
    public decimal Amount { get; init; }

    public Guid? MatterId { get; init; }

    public Guid? ClientId { get; init; }

    [MaxLength(50)]
    public string? Kind { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    public DateTimeOffset? SpentAt { get; init; }
}
