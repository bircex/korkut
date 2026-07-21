using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Features.Membership;
using Korkut.Application.Security;
using Korkut.Domain.Membership;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Api.Endpoints;

public sealed record MemberResponse(
    Guid Id, string Email, string? FullName, string? BarRegistryNo, string Role,
    bool TwoFactorEnabled, bool IsActive, bool Bound, bool IsSelf, DateTimeOffset CreatedAt);

public sealed record MemberCreateRequest
{
    [Required, EmailAddress]
    public string? Email { get; init; }

    [Required, RegularExpression("owner|lawyer", ErrorMessage = "role 'owner' veya 'lawyer' olmalıdır.")]
    public string? Role { get; init; }

    [MaxLength(200)]
    public string? FullName { get; init; }

    [MaxLength(50)]
    public string? BarRegistryNo { get; init; }
}

public sealed record MemberRoleRequest
{
    [Required, RegularExpression("owner|lawyer", ErrorMessage = "role 'owner' veya 'lawyer' olmalıdır.")]
    public string? Role { get; init; }
}

/// <summary>
/// Office membership endpoints (KDM-020). All require the office-scoped
/// <c>members.manage</c> permission (owner-only). The service enforces the office-keeps-
/// an-owner and no-self-removal invariants; this layer maps those to safe status codes.
/// </summary>
public static class MembersEndpoints
{
    public static IEndpointRouteBuilder MapMembersEndpoints(this IEndpointRouteBuilder app)
    {
        var members = app.MapGroup("/api/members").RequireAuthorization(Permissions.MembersManage);

        members.MapGet("", async (ClaimsPrincipal user, KorkutDbContext db, IMembersService svc, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db, async o => (await svc.ListAsync(o, ct)).Select(m => ToDto(m, user.UserId())).ToList(), ct)));

        members.MapPost("", async (MemberCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, IMembersService svc, CancellationToken ct) =>
        {
            var invalid = DtoValidation.Validate(body);
            if (invalid is not null)
            {
                return invalid;
            }

            var created = await user.InTenantScopeAsync(db, o =>
                svc.CreateAsync(o, body.Email!.Trim().ToLowerInvariant(), body.Role!, body.FullName?.Trim(), body.BarRegistryNo?.Trim(), ct), ct);
            return Results.Created($"/api/members/{created.Id}", ToDto(created, user.UserId()));
        });

        members.MapPatch("/{id:guid}", async (Guid id, MemberRoleRequest body, ClaimsPrincipal user, KorkutDbContext db, IMembersService svc, CancellationToken ct) =>
        {
            var invalid = DtoValidation.Validate(body);
            if (invalid is not null)
            {
                return invalid;
            }

            var result = await user.InTenantScopeAsync(db, o => svc.ChangeRoleAsync(o, user.UserId(), id, body.Role!, ct), ct);
            return Map(result);
        });

        members.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, IMembersService svc, CancellationToken ct) =>
        {
            var result = await user.InTenantScopeAsync(db, o => svc.RemoveAsync(o, user.UserId(), id, ct), ct);
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

    private static MemberResponse ToDto(Member m, Guid selfId) =>
        new(m.Id, m.Email, m.FullName, m.BarRegistryNo, m.Role, m.TwoFactorEnabled, m.IsActive, m.Bound, m.Id == selfId, m.CreatedAt);
}
