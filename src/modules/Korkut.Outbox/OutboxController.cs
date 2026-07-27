using Korkut.Contract.Outbox;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Outbox;

/// <summary>
/// The outbound-message review queue: a human reviews the assistant's drafts, edits them, then
/// confirms (draft → queued) or cancels. Reads use matters.read, writes matters.write.
/// </summary>
[ApiController]
[Route("api/outbox")]
public sealed class OutboxController(IOutboxService outbox) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await outbox.ListAsync(ct)).ToActionResult();

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => (await outbox.GetAsync(id, ct)).ToActionResult();

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Edit(Guid id, [FromBody] EditOutboxMessageRequest body, CancellationToken ct)
        => (await outbox.EditDraftAsync(id, body, ct)).ToActionResult();

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
        => (await outbox.ConfirmAsync(id, ct)).ToActionResult();

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => (await outbox.CancelAsync(id, ct)).ToActionResult();
}
