using Korkut.Contract;
using Korkut.Contract.Channels;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Channels.Controllers;

/// <summary>
/// Messaging-channel configuration (e-mail, Telegram, WhatsApp). Anyone who can read the office may
/// see the configuration; changing it requires office.settings. Secrets are write-only.
/// </summary>
[ApiController]
[Route("api/channels")]
public sealed class ChannelsController(IChannelService channels) : ControllerBase
{
    [HttpGet("email")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> GetEmail(CancellationToken ct)
        => (await channels.GetEmailAsync(ct)).ToActionResult();

    [HttpPut("email")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> SaveEmail([FromBody] SaveEmailChannelRequest body, CancellationToken ct)
        => (await channels.SaveEmailAsync(body, ct)).ToActionResult();

    [HttpPut("telegram")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> SaveTelegram([FromBody] SaveTelegramChannelRequest body, CancellationToken ct)
        => (await channels.SaveTelegramAsync(body, ct)).ToActionResult();

    [HttpPut("whatsapp")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> SaveWhatsApp([FromBody] SaveWhatsAppChannelRequest body, CancellationToken ct)
        => (await channels.SaveWhatsAppAsync(body, ct)).ToActionResult();
}
