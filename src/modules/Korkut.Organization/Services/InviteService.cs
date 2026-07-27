using Korkut.Organization.Services;
using Korkut.Organization.Repositories;
using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Contract;
using Korkut.Contract.Organization;
using Korkut.Core;

namespace Korkut.Organization.Services;

/// <summary>
/// Invitation orchestration over the repository port. It owns the token: minted here, stored only as
/// a hash, and returned exactly once. Accepting runs in the scope of the office the token resolved
/// to, not the caller's — there is no caller. Every call RETURNS a <see cref="Result"/>.
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

    public Task<Result<IReadOnlyList<InviteResponse>>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(currentUser.OfficeId, async office =>
            Result<IReadOnlyList<InviteResponse>>.Ok((await repository.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<InviteCreatedResponse>> CreateAsync(CreateInviteRequest request, CancellationToken ct = default)
        => InScopeAsync(currentUser.OfficeId, async office =>
        {
            var rawToken = tokenHasher.NewRawToken();
            var now = clock.UtcNow;
            var invite = new Invite(
                office, request.Email!.Trim().ToLowerInvariant(), request.Role!,
                tokenHasher.Hash(rawToken), now + Lifetime, currentUser.UserId, now);
            await repository.AddAsync(invite, ct);
            return Result<InviteCreatedResponse>.Ok(new InviteCreatedResponse(invite.Id, invite.Email, invite.Role, rawToken, invite.ExpiresAt));
        }, ct);

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
        new(i.Id, i.Email, i.Role, i.ExpiresAt, i.CreatedAt);

    private Task<T> InScopeAsync<T>(Guid officeId, Func<Guid, Task<T>> work, CancellationToken ct)
        => unitOfWork.InTenantScopeAsync(officeId, () => work(officeId), ct);
}
