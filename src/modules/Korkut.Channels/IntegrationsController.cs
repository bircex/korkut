using Korkut.Contract.Channels;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Channels;

/// <summary>
/// The integrations screen: every channel kind with its configuration and pairing state, plus
/// linking an external account (a one-time code) and unlinking it. Reads need matters.read,
/// pairing changes need office.settings.
/// </summary>
[ApiController]
[Route("api/integrations")]
public sealed class IntegrationsController(IChannelService channels) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await channels.ListIntegrationsAsync(ct)).ToActionResult();

    [HttpPost("{kind}/link")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> CreateLink(string kind, CancellationToken ct)
        => (await channels.CreateLinkAsync(kind, ct)).ToActionResult();

    [HttpDelete("{kind}/link")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> DeleteLink(string kind, CancellationToken ct)
        => (await channels.DeleteLinkAsync(kind, ct)).ToActionResult();
}
