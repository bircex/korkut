namespace Korkut.Application.Reports.Responses;

/// <summary>Work in flight: open, done, and the ones already past their due date.</summary>
public sealed record TaskStatsResponse(int Open, int Done, int Overdue);
