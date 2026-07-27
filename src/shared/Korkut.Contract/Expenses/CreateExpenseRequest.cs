using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Expenses;

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
