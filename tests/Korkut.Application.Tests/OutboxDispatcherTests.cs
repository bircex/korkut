using Korkut.Jobs.Entities;
using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;

namespace Korkut.Application.Tests;

public class OutboxDispatcherTests
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

    private sealed class Harness
    {
        public TestClock Clock { get; } = new();
        public InMemoryOutboxStore Outbox { get; } = new();
        public InMemoryInboxStore Inbox { get; } = new();
        public InMemoryJobAudit Audit { get; } = new();
        public AmbientTenantContext Tenant { get; } = new();
        public ExponentialRetryPolicy Retry { get; } = new(maxAttempts: 3, baseSeconds: 10);

        public OutboxDispatcher Build(params RecordingHandler[] handlers)
        {
            var map = handlers.ToDictionary(h => h.MessageType, h => (IJobHandler)h);
            return new OutboxDispatcher(Outbox, Inbox, Audit, map, Tenant, Retry, Clock);
        }

        public OutboxMessage Msg(Guid office, string type, string key)
            => new(office, type, "{}", key, Clock.UtcNow, Clock.UtcNow);
    }

    [Fact]
    public async Task Duplicate_delivery_produces_a_single_effect()
    {
        var h = new Harness();
        var office = Guid.CreateVersion7();
        var handler = new RecordingHandler("email", h.Tenant);
        var dispatcher = h.Build(handler);

        // Two distinct messages, same office + idempotency key.
        await h.Outbox.EnqueueAsync(h.Msg(office, "email", "welcome-1"), default);
        await h.Outbox.EnqueueAsync(h.Msg(office, "email", "welcome-1"), default);

        await dispatcher.DispatchOnceAsync("w1", 10, Lease);

        Assert.Equal(1, handler.Invocations); // handled exactly once
        Assert.All(h.Outbox.Snapshot(), m => Assert.Equal(JobStatus.Completed, m.Status));
        Assert.Contains(h.Audit.Events, e => e.Kind == JobAuditKind.DuplicateSkipped);
    }

    [Fact]
    public async Task Redelivery_after_a_crash_does_not_re_run_the_handler()
    {
        var h = new Harness();
        var office = Guid.CreateVersion7();
        var handler = new RecordingHandler("email", h.Tenant);
        var dispatcher = h.Build(handler);

        // Simulate: the effect already completed on a prior delivery (inbox marked),
        // but the outbox row wasn't marked complete before the worker crashed.
        await h.Inbox.MarkProcessedAsync(office, "welcome-1", default);
        await h.Outbox.EnqueueAsync(h.Msg(office, "email", "welcome-1"), default);

        await dispatcher.DispatchOnceAsync("w1", 10, Lease);

        Assert.Equal(0, handler.Invocations); // not re-run
        Assert.All(h.Outbox.Snapshot(), m => Assert.Equal(JobStatus.Completed, m.Status));
    }

    [Fact]
    public async Task Expired_lease_is_re_leased_after_worker_crash()
    {
        var h = new Harness();
        var office = Guid.CreateVersion7();
        var handler = new RecordingHandler("email", h.Tenant);
        var dispatcher = h.Build(handler);
        await h.Outbox.EnqueueAsync(h.Msg(office, "email", "k"), default);

        // First worker leases but "crashes" (never completes): lease it, then don't process.
        await h.Outbox.LeaseBatchAsync("dead-worker", Lease, h.Clock.UtcNow, 10, default);
        Assert.Empty((await h.Outbox.LeaseBatchAsync("w2", Lease, h.Clock.UtcNow, 10, default))); // still leased

        // After the lease expires, a healthy worker re-leases and completes it.
        h.Clock.Advance(Lease + TimeSpan.FromSeconds(1));
        var handled = await dispatcher.DispatchOnceAsync("w2", 10, Lease);

        Assert.Equal(1, handled);
        Assert.Equal(1, handler.Invocations);
    }

    [Fact]
    public async Task Transient_failure_retries_with_backoff_then_succeeds()
    {
        var h = new Harness();
        var office = Guid.CreateVersion7();
        var handler = new RecordingHandler("ocr", h.Tenant, throwFirst: 1); // fail once, then succeed
        var dispatcher = h.Build(handler);
        await h.Outbox.EnqueueAsync(h.Msg(office, "ocr", "doc-1"), default);

        // First pass: handler throws → message failed and scheduled into the future.
        await dispatcher.DispatchOnceAsync("w1", 10, Lease);
        var failed = h.Outbox.Snapshot().Single();
        Assert.Equal(JobStatus.Failed, failed.Status);
        Assert.Equal(1, failed.Attempts);
        Assert.True(failed.AvailableAt > h.Clock.UtcNow, "backoff pushes AvailableAt into the future");

        // Not due yet → nothing leased.
        Assert.Equal(0, await dispatcher.DispatchOnceAsync("w1", 10, Lease));

        // After backoff elapses → retried and completed.
        h.Clock.Advance(TimeSpan.FromSeconds(30));
        await dispatcher.DispatchOnceAsync("w1", 10, Lease);
        Assert.Equal(JobStatus.Completed, h.Outbox.Snapshot().Single().Status);
        Assert.Equal(2, handler.Invocations);
    }

    [Fact]
    public async Task Poison_message_is_dead_lettered_and_does_not_block_other_jobs()
    {
        var h = new Harness();
        var office = Guid.CreateVersion7();
        var poison = new RecordingHandler("poison", h.Tenant) { AlwaysThrow = true };
        var good = new RecordingHandler("good", h.Tenant);
        var dispatcher = h.Build(poison, good);

        var poisonId = h.Msg(office, "poison", "p1");
        await h.Outbox.EnqueueAsync(poisonId, default);
        await h.Outbox.EnqueueAsync(h.Msg(office, "good", "g1"), default);

        // Run enough passes (advancing past each backoff) to exhaust the poison's attempts.
        for (var i = 0; i < 5; i++)
        {
            await dispatcher.DispatchOnceAsync("w1", 10, Lease);
            h.Clock.Advance(TimeSpan.FromMinutes(5));
        }

        var snapshot = h.Outbox.Snapshot();
        Assert.Equal(JobStatus.DeadLettered, snapshot.Single(m => m.Type == "poison").Status);
        Assert.Equal(JobStatus.Completed, snapshot.Single(m => m.Type == "good").Status); // not blocked
        Assert.Contains(h.Audit.Events, e => e.Kind == JobAuditKind.DeadLettered && e.Type == "poison");
    }

    [Fact]
    public async Task Handlers_see_only_their_own_tenant_context()
    {
        var h = new Harness();
        var officeA = Guid.CreateVersion7();
        var officeB = Guid.CreateVersion7();
        var handler = new RecordingHandler("email", h.Tenant);
        var dispatcher = h.Build(handler);

        await h.Outbox.EnqueueAsync(h.Msg(officeA, "email", "a"), default);
        await h.Outbox.EnqueueAsync(h.Msg(officeB, "email", "b"), default);

        await dispatcher.DispatchOnceAsync("w1", 10, Lease);

        // Each handler invocation saw exactly the office of its own message; never cross-tenant.
        Assert.Equal(2, handler.SeenTenants.Count);
        Assert.Contains(officeA, handler.SeenTenants);
        Assert.Contains(officeB, handler.SeenTenants);
        Assert.DoesNotContain(null, handler.SeenTenants);
        // Outside any job, the ambient tenant is unset.
        Assert.Null(h.Tenant.CurrentOfficeId);
    }

    [Fact]
    public async Task Dead_letter_replay_is_audited_and_reprocesses()
    {
        var h = new Harness();
        var office = Guid.CreateVersion7();
        var handler = new RecordingHandler("ocr", h.Tenant) { AlwaysThrow = true };
        var dispatcher = h.Build(handler);
        var msg = h.Msg(office, "ocr", "doc-1");
        await h.Outbox.EnqueueAsync(msg, default);

        for (var i = 0; i < 5; i++)
        {
            await dispatcher.DispatchOnceAsync("w1", 10, Lease);
            h.Clock.Advance(TimeSpan.FromMinutes(5));
        }

        Assert.Equal(JobStatus.DeadLettered, h.Outbox.Snapshot().Single().Status);

        // Operator replay: the message becomes pending again and audit records it.
        var replayed = await h.Outbox.ReplayAsync(msg.Id, h.Clock.UtcNow, default);
        Assert.NotNull(replayed);
        await h.Audit.RecordAsync(
            new JobAuditEvent(msg.Id, office, "ocr", JobAuditKind.Replayed, "manual", h.Clock.UtcNow), default);

        Assert.Contains(h.Audit.Events, e => e.Kind == JobAuditKind.Replayed);
        Assert.Equal(JobStatus.Pending, h.Outbox.Snapshot().Single().Status);
    }
}
