using Korkut.Core;
using Korkut.Application.Channels;
using Korkut.Application.Channels.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>
/// Messaging-channel configuration (e-mail, Telegram, WhatsApp). Anyone who can read the
/// office may see the configuration; changing it requires office.settings. Provider secrets
/// are write-only — stored encrypted and reported only as a flag.
/// </summary>
[ApiController]
[Route("api/channels")]
public sealed class ChannelsController(IChannelService channels) : ControllerBase
{
    [HttpGet("email")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> GetEmail(CancellationToken ct)
        => Ok(await channels.GetEmailAsync(ct));

    [HttpPut("email")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> SaveEmail([FromBody] SaveEmailChannelRequest body, CancellationToken ct)
        => Ok(await channels.SaveEmailAsync(body, ct));

    [HttpPut("telegram")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> SaveTelegram([FromBody] SaveTelegramChannelRequest body, CancellationToken ct)
        => Ok(await channels.SaveTelegramAsync(body, ct));

    [HttpPut("whatsapp")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> SaveWhatsApp([FromBody] SaveWhatsAppChannelRequest body, CancellationToken ct)
        => Ok(await channels.SaveWhatsAppAsync(body, ct));
}
