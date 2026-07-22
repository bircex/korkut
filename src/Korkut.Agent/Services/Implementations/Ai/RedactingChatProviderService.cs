using System.Runtime.CompilerServices;
using System.Text;
using Korkut.Application.Ai;
using Korkut.Agent.Application.Sessions;
using Korkut.Agent.Services.Core.Ai;
using Korkut.Agent.Services.Implementations.Ai;
using Korkut.Pii;

namespace Korkut.Agent.Services.Implementations.Ai;

/// <summary>
/// A PII-redacting decorator around a CLOUD <see cref="IChatProviderService"/>. Before the request leaves
/// the office it masks the system prompt + every message (TCKN, phone, e-mail, IBAN → placeholder
/// tokens); it restores the originals in the reply. So the cloud model never sees raw PII, yet the
/// user sees a clean answer. On-prem/local providers are NOT wrapped (their data never leaves), so
/// they keep full fidelity. Streaming is restored incrementally with a carry buffer, so a
/// placeholder split across two deltas is never emitted half-restored.
/// </summary>
public sealed class RedactingChatProviderService(IChatProviderService inner, IPiiRedactor redactor) : IChatProviderService
{
    public string Name => inner.Name;

    public async Task<ChatReply> CompleteAsync(ModelSelection selection, ChatRequest request, CancellationToken ct = default)
    {
        var scope = redactor.NewScope();
        var reply = await inner.CompleteAsync(selection, Redact(scope, request), ct);
        return reply with { Text = scope.Restore(reply.Text) };
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ModelSelection selection, ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var scope = redactor.NewScope();
        var stream = new PlaceholderStream(scope);
        await foreach (var ev in inner.StreamAsync(selection, Redact(scope, request), ct))
        {
            if (ev.Delta is { } delta)
            {
                var emit = stream.Push(delta);
                if (emit.Length > 0)
                {
                    yield return ChatStreamEvent.Text(emit);
                }
            }
            else
            {
                // Terminal usage event: flush any held (partial-placeholder) tail first.
                var tail = stream.Flush();
                if (tail.Length > 0)
                {
                    yield return ChatStreamEvent.Text(tail);
                }

                yield return ev;
            }
        }
    }

    private static ChatRequest Redact(IPiiRedactionScope scope, ChatRequest request)
    {
        var system = scope.Mask(request.System);
        // Preserve tool-call linkage (ids/names/args) so the loop stays valid; mask the tool-result
        // content, which carries office data (names, IBANs, …) that must not reach the cloud raw.
        var messages = request.Messages.Select(m => new ChatMessage(
            m.Role,
            scope.Mask(m.Content),
            m.ToolCalls,
            m.ToolResults?.Select(r => new ToolOutput(r.ToolCallId, scope.Mask(r.Content), r.IsError)).ToList())).ToList();
        return request with { System = system, Messages = messages };
    }

    /// <summary>
    /// Restores placeholders in a token stream, holding back a trailing fragment that might be the
    /// start of a placeholder split across deltas (e.g. <c>[[PII_EMA</c> now, <c>IL_1]]</c> next).
    /// </summary>
    private sealed class PlaceholderStream(IPiiRedactionScope scope)
    {
        private readonly StringBuilder _pending = new();

        public string Push(string delta)
        {
            _pending.Append(delta);
            var buf = _pending.ToString();
            var hold = FirstUnclosedMarker(buf);
            var emit = scope.Restore(buf[..hold]);
            _pending.Clear();
            _pending.Append(buf[hold..]);
            return emit;
        }

        public string Flush()
        {
            var rest = scope.Restore(_pending.ToString());
            _pending.Clear();
            return rest;
        }

        // The offset from which the tail might still grow into a placeholder, so must be held back:
        // an open "[[…" not yet closed by "]]", or a trailing single "[" that could become "[[".
        private static int FirstUnclosedMarker(string buf)
        {
            var hold = buf.Length;

            // A trailing "[" could be the first bracket of a placeholder still arriving.
            if (buf.EndsWith('['))
            {
                hold = buf.Length - 1;
            }

            // An opened "[[" with no closing "]]" yet — hold from its start (the earliest wins).
            var open = buf.LastIndexOf("[[", StringComparison.Ordinal);
            if (open >= 0 && buf.IndexOf("]]", open, StringComparison.Ordinal) < 0)
            {
                hold = Math.Min(hold, open);
            }

            return hold;
        }
    }
}
