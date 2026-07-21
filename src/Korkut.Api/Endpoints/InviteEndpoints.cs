using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Api.Endpoints;

public sealed record InviteResponse(Guid Id, string Email, string Role, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);

public sealed record InviteCreateResponse(Guid Id, string Email, string Role, string Token, DateTimeOffset ExpiresAt);

public sealed record InviteCreateRequest
{
    [Required, EmailAddress]
    public string? Email { get; init; }

    [Required, RegularExpression("owner|lawyer", ErrorMessage = "role 'owner' veya 'lawyer' olmalıdır.")]
    public string? Role { get; init; }
}

public sealed record InviteAcceptRequest
{
    [Required]
    public string? Token { get; init; }
}

/// <summary>
/// Office invitations (KDM-020). Owners manage invites within their tenant (RLS);
/// accepting is anonymous and resolves the opaque token across tenants, then creates
/// the member so the invitee can sign in. Single-use.
/// </summary>
public static class InviteEndpoints
{
    public static IEndpointRouteBuilder MapInviteEndpoints(this IEndpointRouteBuilder app)
    {
        var invites = app.MapGroup("/api/invites");

        invites.MapGet("", async (ClaimsPrincipal user, KorkutDbContext db, InviteStore store, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db, async o => (await store.ListAsync(o, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.MembersManage);

        invites.MapPost("", async (InviteCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, InviteStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var issued = await user.InTenantScopeAsync(db,
                    o => store.CreateAsync(o, user.UserId(), body.Email!.Trim().ToLowerInvariant(), body.Role!, ct), ct);
                // The raw token is returned ONCE so the caller can build the invite link.
                return Results.Created($"/api/invites/{issued.Id}",
                    new InviteCreateResponse(issued.Id, body.Email!.Trim().ToLowerInvariant(), body.Role!, issued.RawToken, issued.ExpiresAt));
            })
            .RequireAuthorization(Permissions.MembersManage);

        invites.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, InviteStore store, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, o => store.RevokeAsync(o, id, ct), ct);
                return ok ? Results.NoContent() : throw ApiException.NotFound("Davet bulunamadı.");
            })
            .RequireAuthorization(Permissions.MembersManage);

        // Anonymous: accept an invite → create the member so they can then sign in.
        invites.MapPost("/accept", async (
            InviteAcceptRequest body, KorkutDbContext db, EfInviteResolver resolver, ITokenHasher hasher,
            InviteStore invites, Korkut.Application.Features.Membership.IMembersService members, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var grant = await resolver.ResolveAsync(hasher.Hash(body.Token!), ct);
                if (grant is null)
                {
                    return Results.Json(new { detail = "Davet geçersiz, süresi dolmuş veya kullanılmış." }, statusCode: 400);
                }

                await using var scope = await db.BeginTenantScopeAsync(grant.OfficeId, ct);
                // Idempotent: if a user with this email already exists in the office, just
                // consume the invite; otherwise create the member shell.
                var exists = await db.Users.AsNoTracking().AnyAsync(x => x.OfficeId == grant.OfficeId && x.Email == grant.Email, ct);
                if (!exists)
                {
                    await members.CreateAsync(grant.OfficeId, grant.Email, grant.Role, null, null, ct);
                }

                await invites.MarkAcceptedAsync(grant.OfficeId, grant.Id, ct);
                await scope.CommitAsync(ct);
                return Results.Ok(new { ok = true, email = grant.Email });
            })
            .AllowAnonymous()
            .RequireRateLimiting(Setup.ApiSetup.SensitivePolicy);

        return app;
    }

    private static InviteResponse ToDto(InviteEntity i) => new(i.Id, i.Email, i.Role, i.ExpiresAt, i.CreatedAt);
}
