using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Organization;

/// <summary>A pending invitation in an owner's listing. Carries no token material.</summary>
public sealed record InviteResponse(Guid Id, string Email, string Role, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);

/// <summary>
/// A freshly created invitation. This is the ONLY time the raw token is returned — it is stored
/// hashed — so the caller must build the invite link from this response.
/// </summary>
public sealed record InviteCreatedResponse(Guid Id, string Email, string Role, string Token, DateTimeOffset ExpiresAt);

/// <summary>The outcome of accepting an invitation: which e-mail may now sign in.</summary>
public sealed record InviteAcceptedResponse(bool Ok, string Email);

/// <summary>Body for inviting someone to the office.</summary>
public sealed record CreateInviteRequest
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Rol zorunludur.")]
    [RegularExpression("owner|lawyer", ErrorMessage = "role 'owner' veya 'lawyer' olmalıdır.")]
    public string? Role { get; init; }
}

/// <summary>Body for accepting an invitation: the raw token from the invite link.</summary>
public sealed record AcceptInviteRequest
{
    [Required(ErrorMessage = "Davet kodu zorunludur.")]
    public string? Token { get; init; }
}

/// <summary>
/// Office-invitation use-cases (cross-module contract). An owner creates/lists/revokes invites in
/// their own office; accepting is anonymous (the token names the office). Calls RETURN a <see cref="Result"/>.
/// </summary>
public interface IInviteService
{
    Task<Result<IReadOnlyList<InviteResponse>>> ListAsync(CancellationToken ct = default);

    Task<Result<InviteCreatedResponse>> CreateAsync(CreateInviteRequest request, CancellationToken ct = default);

    Task<Result> RevokeAsync(Guid inviteId, CancellationToken ct = default);

    Task<Result<InviteAcceptedResponse>> AcceptAsync(AcceptInviteRequest request, CancellationToken ct = default);
}
