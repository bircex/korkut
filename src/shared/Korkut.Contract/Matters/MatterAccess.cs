namespace Korkut.Contract.Matters;

/// <summary>
/// Which matters the caller may see: all of them (matters.view_all), or the ones whose team they
/// are on. Records tied to a matter follow it; records tied to none belong to the people named on
/// them (who created it, who it is assigned to), and to those who see everything.
/// </summary>
public sealed record MatterScope(Guid UserId, bool AllMatters, IReadOnlySet<Guid> MatterIds)
{
    public bool CanSee(Guid matterId) => AllMatters || MatterIds.Contains(matterId);

    /// <summary>A record's visibility: its matter's, or, with no matter, whether the caller is named on it.</summary>
    public bool CanSee(Guid? matterId, params Guid?[] people)
        => matterId is { } id ? CanSee(id) : AllMatters || people.Any(p => p == UserId);
}

/// <summary>
/// The caller's matter scope (cross-module contract). Computed once per request; call it inside the
/// tenant scope.
/// </summary>
public interface IMatterAccess
{
    Task<MatterScope> ScopeAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Whether the caller may change this matter's team: they see every matter and may
    /// manage teams, or they are the matter's lead and may manage teams.</summary>
    Task<bool> CanManageTeamAsync(Guid officeId, Guid matterId, CancellationToken ct = default);
}

/// <summary>A member of a matter's team.</summary>
public sealed record MatterTeamMember(Guid UserId, string Name, bool IsLead, DateTimeOffset AddedAt);

/// <summary>Someone who can be added to a matter's team.</summary>
public sealed record MatterTeamCandidate(Guid UserId, string Name);

/// <summary>A matter's team and whether the caller may change it.</summary>
public sealed record MatterTeamResponse(IReadOnlyList<MatterTeamMember> Members, bool CanManage);

/// <summary>Body for adding someone to a matter's team.</summary>
public sealed record AddTeamMemberRequest
{
    public Guid UserId { get; init; }

    public bool IsLead { get; init; }
}

/// <summary>Body for making a member the matter's lead, or not.</summary>
public sealed record SetTeamLeadRequest
{
    public bool IsLead { get; init; }
}

/// <summary>A matter's team (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface IMatterTeamService
{
    Task<Result<MatterTeamResponse>> GetAsync(Guid matterId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<MatterTeamCandidate>>> CandidatesAsync(Guid matterId, CancellationToken ct = default);

    Task<Result<MatterTeamResponse>> AddAsync(Guid matterId, AddTeamMemberRequest request, CancellationToken ct = default);

    Task<Result<MatterTeamResponse>> SetLeadAsync(Guid matterId, Guid userId, SetTeamLeadRequest request, CancellationToken ct = default);

    Task<Result<MatterTeamResponse>> RemoveAsync(Guid matterId, Guid userId, CancellationToken ct = default);
}
