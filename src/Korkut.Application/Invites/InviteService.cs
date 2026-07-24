using Korkut.Core;
using Korkut.Application.Errors;
using Korkut.Application.Invites.Requests;
using Korkut.Application.Invites.Responses;
using Korkut.Application.Security;
using Korkut.Application.Users;
using Korkut.Domain.Invites;
using Korkut.Domain.Users;

namespace Korkut.Application.Invites;

/// <summary>
/// Invitation orchestration over the repository port. It owns the token: minted here,
/// stored only as a hash, and returned to the caller exactly once. Accepting runs in the
/// scope of the office the token resolved to, not the caller's — there is no caller.
/// </summary>
public sealed class InviteService(
    IInviteRepository repository,
    IInviteResolver resolver,
    IUserRepository users,
    ITokenHasher tokenHasher,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IInviteService
{
    /// <summary>How long a fresh invite stays valid.</summary>
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(7);

    public Task<IReadOnlyList<InviteResponse>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(currentUser.OfficeId, async office =>
            (IReadOnlyList<InviteResponse>)(await repository.ListAsync(office, ct)).Select(ToResponse).ToList(), ct);

    public Task<InviteCreatedResponse> CreateAsync(CreateInviteRequest request, CancellationToken ct = default)
        => InScopeAsync(currentUser.OfficeId, async office =>
        {
            var rawToken = tokenHasher.NewRawToken();
            var now = clock.UtcNow;
            var invite = new Invite(
                office, request.Email!.Trim().ToLowerInvariant(), request.Role!,
                tokenHasher.Hash(rawToken), now + Lifetime, currentUser.UserId, now);
            await repository.AddAsync(invite, ct);
            return new InviteCreatedResponse(invite.Id, invite.Email, invite.Role, rawToken, invite.ExpiresAt);
        }, ct);

    public Task RevokeAsync(Guid inviteId, CancellationToken ct = default)
        => InScopeAsync(currentUser.OfficeId, async office =>
        {
            // An invite that was already accepted is history, not something to revoke.
            var invite = await repository.FindAsync(office, inviteId, ct);
            if (invite is null || !invite.IsPending)
            {
                throw new NotFoundException("Davet bulunamadı.");
            }

            return await repository.RemoveAsync(office, inviteId, ct)
                ? true
                : throw new NotFoundException("Davet bulunamadı.");
        }, ct);

    public async Task<InviteAcceptedResponse> AcceptAsync(AcceptInviteRequest request, CancellationToken ct = default)
    {
        // Resolved across tenants: an unknown, expired or used token is all one answer, so
        // a caller cannot probe for which offices exist.
        var grant = await resolver.ResolveAsync(tokenHasher.Hash(request.Token!), ct)
            ?? throw new BadRequestException("Davet geçersiz, süresi dolmuş veya kullanılmış.");

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

            return new InviteAcceptedResponse(true, grant.Email);
        }, ct);
    }

    private static InviteResponse ToResponse(Invite i) =>
        new(i.Id, i.Email, i.Role, i.ExpiresAt, i.CreatedAt);

    private Task<T> InScopeAsync<T>(Guid officeId, Func<Guid, Task<T>> work, CancellationToken ct)
        => unitOfWork.InTenantScopeAsync(officeId, () => work(officeId), ct);
}
