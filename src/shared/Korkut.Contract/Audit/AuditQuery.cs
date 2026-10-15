using Korkut.Contract.Clients;

namespace Korkut.Contract.Audit;

/// <summary>One line of the office's access log, as the "Erişim kayıtları" screen shows it.</summary>
public sealed record AuditEventResponse(
    Guid Id, DateTimeOffset At, Guid? ActorId, string? ActorName, string Action, string EntityType, Guid? EntityId, string? Details);

public sealed record AuditPageResponse(IReadOnlyList<AuditEventResponse> Items, int Total);

/// <summary>Reads the office's audit trail, newest first (audit.read).</summary>
public interface IAuditQuery
{
    Task<AuditPageResponse> ListAsync(int limit, int offset, string? action = null, CancellationToken ct = default);
}

/// <summary>The access events the audit trail records (docs/MATTER_RBAC_PLAN.md §8).</summary>
public static class AuditActions
{
    public const string ClientDetailsViewed = "client.details_viewed";
    public const string FinanceViewed = "finance.viewed";
    public const string MemberRoleChanged = "member.role_changed";
    public const string MemberRemoved = "member.removed";
    public const string TeamMemberAdded = "matter.team_added";
    public const string TeamLeadChanged = "matter.team_lead";
    public const string TeamMemberRemoved = "matter.team_removed";
    public const string AccessDenied = "access.denied";

    /// <summary>
    /// Which of a client's contact and identity details a response shows: the names of the
    /// fields, never their values (the trail must not become a copy of what it guards).
    /// </summary>
    public static IReadOnlyList<string> ShownDetails(ClientResponse client)
        => new (bool Shown, string Name)[]
            {
                (!string.IsNullOrEmpty(client.Email), "email"),
                (!string.IsNullOrEmpty(client.Phone), "phone"),
                (!string.IsNullOrEmpty(client.Address), "address"),
                (!string.IsNullOrEmpty(client.NationalId), "national_id"),
                (client.DateOfBirth is not null, "date_of_birth"),
            }
            .Where(f => f.Shown).Select(f => f.Name).ToList();
}
