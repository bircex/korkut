using Korkut.Application.Outbox;
using Korkut.Application.Outbox.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Outbox;

/// <summary>
/// The outbound-message review queue: a human reviews the assistant's drafts, edits them,
/// then confirms (draft → queued) or cancels. Reads use matters.read, writes matters.write.
/// </summary>
[ApiController]
[Route("api/outbox")]
public sealed class OutboxController(IOutboxService outbox) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await outbox.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await outbox.GetAsync(id, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Edit(Guid id, [FromBody] EditOutboxMessageRequest body, CancellationToken ct)
        => Ok(await outbox.EditDraftAsync(id, body, ct));

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
        => Ok(await outbox.ConfirmAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => Ok(await outbox.CancelAsync(id, ct));
}
