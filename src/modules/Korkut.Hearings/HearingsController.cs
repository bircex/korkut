using Korkut.Contract.Hearings;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Hearings;

/// <summary>Hearings (duruşmalar) of a matter. Forwards to <see cref="IHearingService"/>, maps Result to HTTP.</summary>
[ApiController]
[Route("api/matters/{id:guid}/hearings")]
public sealed class HearingsController(IHearingService hearings) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create(Guid id, [FromBody] CreateHearingRequest body, CancellationToken ct)
        => (await hearings.AddAsync(id, body, ct)).ToActionResult(h => Created($"/api/matters/{id}/hearings/{h.Id}", h));
}
