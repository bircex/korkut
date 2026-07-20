using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;

namespace Korkut.Api.Endpoints;

public sealed record MemberResponse(Guid Id, string Email, string Role, bool TwoFactorEnabled, DateTimeOffset CreatedAt);

public sealed record MemberCreateRequest
{
    [Required, EmailAddress]
    public string? Email { get; init; }

    [Required, RegularExpression("owner|lawyer", ErrorMessage = "role 'owner' veya 'lawyer' olmalıdır.")]
    public string? Role { get; init; }
}

public sealed record MemberRoleRequest
{
    [Required, RegularExpression("owner|lawyer", ErrorMessage = "role 'owner' veya 'lawyer' olmalıdır.")]
    public string? Role { get; init; }
}

/// <summary>
/// Office membership endpoints (KDM-020). All require the office-scoped
/// <c>members.manage</c> permission (owner-only). The store enforces the office-keeps-
/// an-owner and no-self-removal invariants; this layer maps those to safe status codes.
/// </summary>
public static class MembersEndpoints
{
    public static IEndpointRouteBuilder MapMembersEndpoints(this IEndpointRouteBuilder app)
    {
        var members = app.MapGroup("/api/members").RequireAuthorization(Permissions.MembersManage);

        members.MapGet("", async (ClaimsPrincipal user, KorkutDbContext db, MembersStore store, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db, async o => (await store.ListAsync(o, ct)).Select(ToDto).ToList(), ct)));

        members.MapPost("", async (MemberCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, MembersStore store, CancellationToken ct) =>
        {
            var invalid = DtoValidation.Validate(body);
            if (invalid is not null)
            {
                return invalid;
            }

            var created = await user.InTenantScopeAsync(db, async o =>
            {
                var id = await store.CreateAsync(o, body.Email!.Trim().ToLowerInvariant(), body.Role!, ct);
                return (await store.ListAsync(o, ct)).First(x => x.Id == id);
            }, ct);
            return Results.Created($"/api/members/{created.Id}", ToDto(created));
        });

        members.MapPatch("/{id:guid}", async (Guid id, MemberRoleRequest body, ClaimsPrincipal user, KorkutDbContext db, MembersStore store, CancellationToken ct) =>
        {
            var invalid = DtoValidation.Validate(body);
            if (invalid is not null)
            {
                return invalid;
            }

            var result = await user.InTenantScopeAsync(db, o => store.ChangeRoleAsync(o, user.UserId(), id, body.Role!, ct), ct);
            return Map(result);
        });

        members.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, MembersStore store, CancellationToken ct) =>
        {
            var result = await user.InTenantScopeAsync(db, o => store.RemoveAsync(o, user.UserId(), id, ct), ct);
            return Map(result);
        });

        return app;
    }

    private static IResult Map(MemberMutation result) => result switch
    {
        MemberMutation.Ok => Results.NoContent(),
        MemberMutation.NotFound => throw ApiException.NotFound("Üye bulunamadı."),
        MemberMutation.LastOwner => throw ApiException.Conflict("Ofiste en az bir sahip kalmalıdır."),
        MemberMutation.SelfForbidden => throw ApiException.Forbidden("Kendinizi çıkaramazsınız."),
        _ => throw ApiException.Conflict("İşlem tamamlanamadı."),
    };

    private static MemberResponse ToDto(UserEntity u) => new(u.Id, u.Email, u.Role, u.TwoFactorEnabled, u.CreatedAt);
}
