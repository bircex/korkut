using System.Runtime.CompilerServices;
using Korkut.Agent.Application.Ai;
using Korkut.Agent.Application.Abstractions.Ai;
using Korkut.Agent.Application.Abstractions.Persistence;
using Korkut.Agent.Application.Abstractions.Sessions;
using Korkut.Agent.Application.Sessions;
using Korkut.Agent.Application.Chat;
using Korkut.Application.Ai;
using Korkut.Agent.Infrastructure.Ai;
using Korkut.Agent.Infrastructure.Ai.Providers;
using Korkut.Agent.Infrastructure.Ai.Routing;
using Korkut.Agent.Infrastructure.Persistence;
using Korkut.Pii;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// The cloud-provider PII decorator: the inner (cloud) provider must receive MASKED text, and the
/// caller must get the reply with originals RESTORED. Covers the non-streaming path and the
/// streaming path where a placeholder is split across deltas.
/// </summary>
public class RedactingChatProviderTests
{
    private static readonly ModelSelection Selection = new("test", "test-model", 1000);
    private const string Tckn = "10000000146";

    [Fact]
    public async Task Inner_provider_sees_masked_text_and_the_reply_is_restored()
    {
        var capture = new CapturingProvider();
        var provider = new RedactingChatProvider(capture, new PiiRedactor());

        var request = new ChatRequest(
            System: "Büro e-postası info@buro.com.",
            Messages: [new ChatMessage("user", $"Müvekkil ahmet@ornek.com, TCKN {Tckn}.")],
            Specialty: ModelSpecialty.Dispatcher);

        var reply = await provider.CompleteAsync(Selection, request, ct: default);

        // The cloud provider never saw raw PII.
        Assert.DoesNotContain("info@buro.com", capture.SeenSystem);
        Assert.DoesNotContain("ahmet@ornek.com", capture.SeenUser);
        Assert.DoesNotContain(Tckn, capture.SeenUser);
        Assert.Contains("[[PII_EMAIL_", capture.SeenUser);

        // The reply (which echoed the placeholders) is restored for the caller.
        Assert.Contains("ahmet@ornek.com", reply.Text);
        Assert.Contains(Tckn, reply.Text);
        Assert.DoesNotContain("[[PII_", reply.Text);
    }

    [Fact]
    public async Task Streaming_restores_a_placeholder_split_across_deltas()
    {
        // The fake streams the (masked) echo one CHARACTER at a time, so every placeholder is
        // split across many deltas — the carry buffer must still restore it cleanly.
        var echo = new CharStreamProvider();
        var provider = new RedactingChatProvider(echo, new PiiRedactor());

        var request = new ChatRequest(
            System: string.Empty,
            Messages: [new ChatMessage("user", "Mail: ahmet@ornek.com")],
            Specialty: ModelSpecialty.Dispatcher);

        var full = new System.Text.StringBuilder();
        await foreach (var ev in provider.StreamAsync(Selection, request, ct: default))
        {
            if (ev.Delta is { } d)
            {
                full.Append(d);
            }
        }

        Assert.Contains("ahmet@ornek.com", full.ToString());
        Assert.DoesNotContain("[[PII_", full.ToString());
    }

    /// <summary>Captures the masked request it receives and echoes it back verbatim.</summary>
    private sealed class CapturingProvider : IChatProvider
    {
        public string SeenSystem { get; private set; } = string.Empty;

        public string SeenUser { get; private set; } = string.Empty;

        public string Name => "test";

        public Task<ChatReply> CompleteAsync(ModelSelection selection, ChatRequest request, CancellationToken ct = default)
        {
            SeenSystem = request.System;
            SeenUser = request.Messages.Last(m => m.Role == "user").Content;
            // A cloud model would answer referencing the placeholders it saw.
            return Task.FromResult(new ChatReply($"{request.System} {SeenUser}", ChatUsage.None));
        }

        public IAsyncEnumerable<ChatStreamEvent> StreamAsync(ModelSelection selection, ChatRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    /// <summary>Streams the masked user turn back one character at a time, then a terminal usage.</summary>
    private sealed class CharStreamProvider : IChatProvider
    {
        public string Name => "test";

        public Task<ChatReply> CompleteAsync(ModelSelection selection, ChatRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
            ModelSelection selection, ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
        {
            var masked = request.Messages.Last(m => m.Role == "user").Content;
            foreach (var c in masked)
            {
                await Task.Yield();
                yield return ChatStreamEvent.Text(c.ToString());
            }

            yield return ChatStreamEvent.Final(ChatUsage.None);
        }
    }
}
