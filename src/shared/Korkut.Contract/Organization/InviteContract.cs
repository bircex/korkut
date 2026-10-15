using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Organization;

/// <summary>An invitation in an owner's listing. Carries no token material. <see cref="Redeemed"/>
/// is true once accepted.</summary>
public sealed record InviteResponse(
    Guid Id, string Email, string Role, bool Redeemed, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt,
    string? FullName = null);

/// <summary>
/// A freshly created invitation. This is the ONLY time the raw token is returned — it is stored
/// hashed. <see cref="Path"/> is the ready-to-share relative link the UI appends to its origin.
/// </summary>
public sealed record InviteCreatedResponse(
    Guid Id, string Email, string Role, bool Redeemed, string Token, string Path, DateTimeOffset ExpiresAt,
    string? FullName = null);

/// <summary>The outcome of accepting an invitation: which e-mail may now sign in.</summary>
public sealed record InviteAcceptedResponse(bool Ok, string Email);

/// <summary>Davet linkine tıklayan kişiye gösterilecek bilgi — token TÜKETİLMEZ.</summary>
public sealed record InvitePeekResponse(string OfficeName, string Email, string? FullName, string Role);

/// <summary>Parola belirleyerek daveti kabul: hesap açılır ve oturum verilecek kullanıcı döner.</summary>
public sealed record InviteJoinedUser(Guid OfficeId, Guid UserId, string Role, Guid SecurityStamp, string Email);

/// <summary>Davet linkinden parolayla katılım gövdesi.</summary>
public sealed record AcceptInviteLocalRequest
{
    [Required(ErrorMessage = "Davet kodu zorunludur.")]
    public string? Token { get; init; }

    [Required(ErrorMessage = "Parola zorunludur.")]
    [MinLength(10, ErrorMessage = "Parola en az 10 karakter olmalıdır.")]
    [MaxLength(200)]
    public string? Password { get; init; }

    [StringLength(200, ErrorMessage = "Ad soyad en fazla 200 karakter olabilir.")]
    public string? FullName { get; init; }
}

/// <summary>Body for inviting someone to the office.</summary>
public sealed record CreateInviteRequest
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string? Email { get; init; }

    /// <summary>Optional; defaults to "lawyer" when omitted.</summary>
    [RegularExpression("^[a-z0-9-]{1,50}$", ErrorMessage = "Geçersiz rol.")]
    public string? Role { get; init; }

    /// <summary>Davet edilenin adı-soyadı; kabulde hesap bu adla açılır.</summary>
    [StringLength(200, ErrorMessage = "Ad soyad en fazla 200 karakter olabilir.")]
    public string? FullName { get; init; }
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

    /// <summary>Token'ı tüketmeden davet bilgisini döndürür (katılım ekranı için).</summary>
    Task<Result<InvitePeekResponse>> PeekAsync(string token, CancellationToken ct = default);

    /// <summary>Parola belirleyerek katılım: hesabı açar, daveti tüketir, oturumluk kullanıcıyı döner.</summary>
    Task<Result<InviteJoinedUser>> AcceptLocalAsync(AcceptInviteLocalRequest request, CancellationToken ct = default);
}
