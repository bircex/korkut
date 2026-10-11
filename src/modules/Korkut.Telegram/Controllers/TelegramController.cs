using Korkut.Contract;
using Korkut.Contract.Telegram;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Telegram.Controllers;

/// <summary>The office bot, for the owner (office.settings).</summary>
[ApiController]
[Route("api/settings/telegram")]
[Authorize(Policy = Permissions.OfficeSettings)]
public sealed class TelegramSettingsController(ITelegramSettingsService settings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => (await settings.GetAsync(ct)).ToActionResult();

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] SaveTelegramSettingsRequest body, CancellationToken ct)
        => (await settings.SaveAsync(body, ct)).ToActionResult();

    [HttpDelete]
    public async Task<IActionResult> Remove(CancellationToken ct)
        => (await settings.RemoveAsync(ct)).ToActionResult();

    [HttpDelete("members/{userId:guid}")]
    public async Task<IActionResult> UnlinkMember(Guid userId, CancellationToken ct)
        => (await settings.UnlinkMemberAsync(userId, ct)).ToActionResult();
}

/// <summary>The caller's own Telegram link; every member may manage theirs.</summary>
[ApiController]
[Route("api/telegram/link")]
[Authorize(Policy = Permissions.MattersRead)]
public sealed class TelegramLinkController(ITelegramLinkService links) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => (await links.GetAsync(ct)).ToActionResult();

    [HttpPost("code")]
    public async Task<IActionResult> CreateCode(CancellationToken ct)
        => (await links.CreateCodeAsync(ct)).ToActionResult();

    [HttpDelete]
    public async Task<IActionResult> Unlink(CancellationToken ct)
        => (await links.UnlinkAsync(ct)).ToActionResult();
}
