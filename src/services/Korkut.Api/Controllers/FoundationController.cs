using Korkut.Core;
using System.ComponentModel.DataAnnotations;
using Korkut.Api.Contracts;
using Korkut.Security;
using Korkut.Web;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Controllers;

/// <summary>Public bootstrap flags: what the sign-in screen may offer.</summary>
public sealed record ConfigResponse(bool GoogleConfigured);

/// <summary>A sample list item (a transport shape, deliberately not a domain entity).</summary>
public sealed record SampleItem(Guid Id, string Name);

/// <summary>A sample request with validation, so the 422 contract is exercised end to end.</summary>
public sealed record EchoRequest
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MaxLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
    public string? Title { get; init; }
}

public sealed record EchoResponse(string Title);

/// <summary>
/// The conventions every other controller inherits, pinned by tests: the JSON contract,
/// the paged envelope, validation → 422, the <c>{ detail }</c> error body, rate limiting,
/// and deny-by-default permissions. These routes carry no business meaning — they exist so
/// a change to the plumbing fails here first, in one obvious place, rather than somewhere
/// in a real domain.
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class FoundationController : ControllerBase
{
    /// <summary>A stable public contract: what the sign-in screen may offer. Served at both
    /// <c>/api/config</c> (what the SPA calls) and the versioned <c>/api/v1/config</c>.</summary>
    [HttpGet("config")]
    [HttpGet("/api/config")]
    [AllowAnonymous]
    public IActionResult Config([FromServices] Korkut.Contract.Organization.IGoogleSignInService google)
        => Ok(new ConfigResponse(GoogleConfigured: google.IsConfigured));

    /// <summary>The paged envelope: { items, total }.</summary>
    [HttpGet("samples")]
    [AllowAnonymous]
    public IActionResult Samples()
    {
        var items = new List<SampleItem>
        {
            new(Guid.CreateVersion7(), "sample-a"),
            new(Guid.CreateVersion7(), "sample-b"),
        };
        return Ok(PagedResult.From(items));
    }

    /// <summary>Validation → 422 with the parity-contract validation body.</summary>
    [HttpPost("echo")]
    [AllowAnonymous]
    public IActionResult Echo([FromBody] EchoRequest request) => Ok(new EchoResponse(request.Title!));

    /// <summary>The { detail } error contract, via a thrown ApiException.</summary>
    [HttpGet("not-found-demo")]
    [AllowAnonymous]
    public IActionResult NotFoundDemo() => throw ApiException.NotFound("Kayıt bulunamadı");

    /// <summary>Rate limiting, as applied to login, reset, 2FA, setup and upload.</summary>
    [HttpGet("sensitive-demo")]
    [AllowAnonymous]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public IActionResult SensitiveDemo() => Ok(OkResponse.Done);

    /// <summary>Deny-by-default permissions: this one needs matters.write.</summary>
    [HttpPost("matters-demo")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public IActionResult MattersDemo() => Ok(OkResponse.Done);
}
