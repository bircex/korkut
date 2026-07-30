using Korkut.Organization.Services;
using Korkut.Organization.Repositories;
using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Contract;
using Korkut.Contract.Notifications;
using Korkut.Contract.Organization;
using Korkut.Core;
using Microsoft.Extensions.Logging;

namespace Korkut.Organization.Services;

/// <summary>
/// Invitation orchestration over the repository port. It owns the token: minted here, stored only as
/// a hash, and returned exactly once. Accepting runs in the scope of the office the token resolved
/// to, not the caller's — there is no caller. Every call RETURNS a <see cref="Result"/>.
/// </summary>
public sealed partial class InviteService(
    IInviteRepository repository,
    IInviteResolver resolver,
    IUserRepository users,
    ITokenHasher tokenHasher,
    IEmailSender emailSender,
    MailLinks mailLinks,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    ILogger<InviteService> logger) : IInviteService
{
    /// <summary>How long a fresh invite stays valid.</summary>
    private TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(7);

    public Task<Result<IReadOnlyList<InviteResponse>>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(currentUser.OfficeId, async office =>
            Result<IReadOnlyList<InviteResponse>>.Ok((await repository.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public async Task<Result<InviteCreatedResponse>> CreateAsync(CreateInviteRequest request, CancellationToken ct = default)
    {
        var email = request.Email!.Trim().ToLowerInvariant();
        var rawToken = tokenHasher.NewRawToken();
        var result = await InScopeAsync(currentUser.OfficeId, async office =>
        {
            var now = clock.UtcNow;
            var invite = new Invite(
                office, email, request.Role ?? Roles.Lawyer,
                tokenHasher.Hash(rawToken), now + Lifetime, currentUser.UserId, now);
            await repository.AddAsync(invite, ct);
            return Result<InviteCreatedResponse>.Ok(new InviteCreatedResponse(
                invite.Id, invite.Email, invite.Role, Redeemed: false, rawToken, mailLinks.InviteLink(rawToken), invite.ExpiresAt));
        }, ct);

        // Deliver the invite link by e-mail (best-effort: the link is still returned in the
        // response, so a mail outage never blocks inviting). Sent outside the tenant tx.
        if (result.IsSuccess)
        {
            await SendInviteEmailAsync(email, rawToken, ct);
        }

        return result;
    }

    private async Task SendInviteEmailAsync(string address, string rawToken, CancellationToken ct)
    {
        var link = mailLinks.InviteLink(rawToken);
        const string subject = "Korkut'a davet edildiniz";
        var body =
            "<p>Merhaba,</p>" +
            "<p>Bir Korkut ofisine katılmanız için davet edildiniz. Daveti kabul etmek için aşağıdaki bağlantıya tıklayın:</p>" +
            $"<p><a href=\"{link}\">{link}</a></p>" +
            "<p>Bu bağlantı sınırlı bir süre geçerlidir.</p>" +
            "<p>Korkut</p>";
        try
        {
            await emailSender.SendAsync(address, subject, body, ct);
        }
        catch (Exception ex)
        {
            LogInviteEmailFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send invite e-mail.")]
    private static partial void LogInviteEmailFailed(ILogger logger, Exception ex);

    public Task<Result> RevokeAsync(Guid inviteId, CancellationToken ct = default)
        => InScopeAsync(currentUser.OfficeId, async office =>
        {
            // An invite that was already accepted is history, not something to revoke.
            var invite = await repository.FindAsync(office, inviteId, ct);
            if (invite is null || !invite.IsPending)
            {
                return Result.NotFound("Davet bulunamadı.");
            }

            return await repository.RemoveAsync(office, inviteId, ct) ? Result.Ok() : Result.NotFound("Davet bulunamadı.");
        }, ct);

    public async Task<Result<InviteAcceptedResponse>> AcceptAsync(AcceptInviteRequest request, CancellationToken ct = default)
    {
        // Resolved across tenants: an unknown, expired or used token is all one answer.
        var grant = await resolver.ResolveAsync(tokenHasher.Hash(request.Token!), ct);
        if (grant is null)
        {
            return Result<InviteAcceptedResponse>.BadRequest("Davet geçersiz, süresi dolmuş veya kullanılmış.");
        }

        return await InScopeAsync(grant.OfficeId, async office =>
        {
            // Idempotent: if the invitee is already a member, just consume the invite.
            if (!await users.ExistsWithEmailAsync(office, grant.Email, ct))
            {
                await users.AddAsync(new User(office, grant.Email, grant.Role, null, null, clock.UtcNow), ct);
            }

            var invite = await repository.FindForUpdateAsync(office, grant.Id, ct);
            if (invite is not null && invite.Accept(clock.UtcNow))
            {
                await repository.SaveChangesAsync(ct);
            }

            return Result<InviteAcceptedResponse>.Ok(new InviteAcceptedResponse(true, grant.Email));
        }, ct);
    }

    private static InviteResponse ToResponse(Invite i) =>
        new(i.Id, i.Email, i.Role, i.AcceptedAt is not null, i.ExpiresAt, i.CreatedAt);

    private Task<T> InScopeAsync<T>(Guid officeId, Func<Guid, Task<T>> work, CancellationToken ct)
        => unitOfWork.InTenantScopeAsync(officeId, () => work(officeId), ct);
}
