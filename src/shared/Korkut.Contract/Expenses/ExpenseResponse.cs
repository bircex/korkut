namespace Korkut.Contract.Expenses;

/// <summary>Transport shape for an expense.</summary>
public sealed record ExpenseResponse(
    Guid Id, Guid? MatterId, Guid? ClientId, decimal Amount, string? Kind, string? Description, DateTimeOffset? SpentAt);
