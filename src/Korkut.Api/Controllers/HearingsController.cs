using Korkut.Core;
using Korkut.Application.Hearings;
using Korkut.Application.Hearings.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>Hearings (duruşmalar) of a matter. Forwards to <see cref="IHearingService"/>.</summary>
[ApiController]
[Route("api/matters/{id:guid}/hearings")]
public sealed class HearingsController(IHearingService hearings) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create(Guid id, [FromBody] CreateHearingRequest body, CancellationToken ct)
    {
        var created = await hearings.AddAsync(id, body, ct);
        return Created($"/api/matters/{id}/hearings/{created.Id}", created);
    }
}
