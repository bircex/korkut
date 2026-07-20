using System.ComponentModel.DataAnnotations;
using Korkut.Api.Contracts;
using Korkut.Api.Errors;

namespace Korkut.Api.Endpoints;

/// <summary>Public bootstrap flags DTO — mirrors the Python <c>/api/config</c> body.</summary>
public sealed record ConfigResponse(bool DemoEnabled, bool GoogleConfigured);

/// <summary>A sample list-item DTO (transport shape, not a domain entity).</summary>
public sealed record SampleItem(Guid Id, string Name);

/// <summary>A sample request DTO with validation (title required, bounded length).</summary>
public sealed record EchoRequest
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MaxLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
    public string? Title { get; init; }
}

public sealed record EchoResponse(string Title);

/// <summary>
/// Representative endpoints exercising the foundation conventions (JSON contract,
/// versioning, validation, error mapping, paged envelope). The real office/matter
/// endpoints are added per domain module in KDM-023+; these prove the plumbing and
/// pin its behavior with tests, and deliberately map DTOs — never domain entities.
/// </summary>
public static class FoundationEndpoints
{
    public static IEndpointRouteBuilder MapFoundationEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        // Public bootstrap flags (example of a stable public contract).
        v1.MapGet("/config", () => Results.Ok(new ConfigResponse(DemoEnabled: true, GoogleConfigured: false)));

        // Paged envelope example: { items, total }.
        v1.MapGet("/samples", () =>
        {
            var items = new List<SampleItem>
            {
                new(Guid.CreateVersion7(), "sample-a"),
                new(Guid.CreateVersion7(), "sample-b"),
            };
            return Results.Ok(PagedResult.From(items));
        });

        // DTO validation → 422 with the parity-contract validation body.
        v1.MapPost("/echo", (EchoRequest request) =>
        {
            var invalid = DtoValidation.Validate(request);
            if (invalid is not null)
            {
                return invalid;
            }

            return Results.Ok(new EchoResponse(request.Title!));
        });

        // Example of the {detail} error contract via a thrown ApiException.
        v1.MapGet("/not-found-demo", IResult () =>
            throw ApiException.NotFound("Kayıt bulunamadı"));

        // Rate-limited endpoint (stands in for login/reset/2FA/upload/admin).
        v1.MapGet("/sensitive-demo", () => Results.Ok(new { ok = true }))
            .RequireRateLimiting(Setup.ApiSetup.SensitivePolicy);

        // Permission-protected endpoint: deny-by-default, requires matters.write.
        v1.MapPost("/matters-demo", () => Results.Ok(new { ok = true }))
            .RequireAuthorization(Application.Security.Permissions.MattersWrite);

        // Platform-admin endpoint: separate policy/scheme + rate limit. Office tokens
        // are rejected here (different audience).
        app.MapGet("/api/admin/ping", () => Results.Ok(new { admin = true }))
            .RequireAuthorization(Auth.AdminAuthHandler.SchemeName)
            .RequireRateLimiting(Setup.ApiSetup.SensitivePolicy);

        return app;
    }
}
