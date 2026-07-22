using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Services;
using Korkut.Domain.Agent.Conversations;
using Korkut.Domain.Agent.Documents;
using Korkut.SharedKernel.Time;

namespace Korkut.Agent.Tests;

/// <summary>
/// Unit coverage for the agent use-case services over fake repositories: they delegate to
/// the port and, crucially, stamp the clock's timestamp on the write paths (so persistence
/// stays clock-free and deterministic).
/// </summary>
public class AgentServicesTests
{
    private static readonly DateTimeOffset Fixed = new(2026, 7, 21, 9, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Fixed;
    }

    // --- Sessions ---

    private sealed class FakeSessionRepo : ISessionRepository
    {
        public DateTimeOffset? LastNow { get; private set; }

        public string? LastTitle { get; private set; }

        public Task<Guid> CreateAsync(Guid officeId, Guid createdBy, Guid? matterId, DateTimeOffset now, CancellationToken ct = default)
        {
            LastNow = now;
            return Task.FromResult(Guid.CreateVersion7());
        }

        public Task AppendMessageAsync(Guid officeId, Guid conversationId, string role, string content, DateTimeOffset now, CancellationToken ct = default)
        {
            LastNow = now;
            return Task.CompletedTask;
        }

        public Task<bool> SetTitleIfDefaultAsync(Guid officeId, Guid id, string title, CancellationToken ct = default)
        {
            LastTitle = title;
            return Task.FromResult(true);
        }

        public Task<Conversation?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default) =>
            Task.FromResult<Conversation?>(new Conversation(id, officeId, "T", null, null, Fixed, Fixed));

        public Task<(IReadOnlyList<SessionBrief> Items, int Total)> ListAsync(Guid officeId, int limit, int offset, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<SessionBrief>, int)>((Array.Empty<SessionBrief>(), 0));

        public Task<SessionBrief?> GetBriefAsync(Guid officeId, Guid id, CancellationToken ct = default) => Task.FromResult<SessionBrief?>(null);

        public Task<IReadOnlyList<ChatTurn>> MessagesAsync(Guid officeId, Guid conversationId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ChatTurn>>(Array.Empty<ChatTurn>());

        public Task<bool> RenameAsync(Guid officeId, Guid id, string title, DateTimeOffset now, CancellationToken ct = default)
        {
            LastNow = now;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default) => Task.FromResult(true);

        public Task<Guid?> CloneAsync(Guid officeId, Guid id, Guid createdBy, DateTimeOffset now, CancellationToken ct = default)
        {
            LastNow = now;
            return Task.FromResult<Guid?>(Guid.CreateVersion7());
        }
    }

    [Fact]
    public async Task Appending_a_message_stamps_the_clock()
    {
        var repo = new FakeSessionRepo();
        var svc = new SessionService(repo, new FixedClock());

        await svc.AppendMessageAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), "user", "selam");

        Assert.Equal(Fixed, repo.LastNow);
    }

    [Fact]
    public async Task Auto_title_delegates_without_a_clock()
    {
        var repo = new FakeSessionRepo();
        var svc = new SessionService(repo, new FixedClock());

        var ok = await svc.SetTitleIfDefaultAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), "Boşanma davası");

        Assert.True(ok);
        Assert.Equal("Boşanma davası", repo.LastTitle);
    }

    // --- Drafts ---

    private sealed class FakeDraftRepo : IDraftDocumentRepository
    {
        public DateTimeOffset? LastNow { get; private set; }

        public Task<Guid?> StagePendingAsync(Guid officeId, Guid docId, string contentMd, string summary, DateTimeOffset now, CancellationToken ct = default)
        {
            LastNow = now;
            return Task.FromResult<Guid?>(Guid.CreateVersion7());
        }

        public Task<bool> AcceptPendingAsync(Guid officeId, Guid docId, string? author, DateTimeOffset now, CancellationToken ct = default)
        {
            LastNow = now;
            return Task.FromResult(true);
        }

        public Task<GeneratedDocument> GetOrCreateForSessionAsync(Guid officeId, Guid conversationId, string title, DateTimeOffset now, CancellationToken ct = default)
        {
            LastNow = now;
            return Task.FromResult(new GeneratedDocument(Guid.CreateVersion7(), officeId, conversationId, title, "", "draft", null, null, null, now, now));
        }

        public Task<GeneratedDocument?> GetAsync(Guid officeId, Guid docId, CancellationToken ct = default) =>
            Task.FromResult<GeneratedDocument?>(new GeneratedDocument(docId, officeId, Guid.CreateVersion7(), "T", "body", "draft", null, null, null, Fixed, Fixed));

        public Task<IReadOnlyList<DocumentVersion>> VersionsAsync(Guid officeId, Guid docId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentVersion>>(Array.Empty<DocumentVersion>());

        public Task<bool> RevertPendingAsync(Guid officeId, Guid docId, DateTimeOffset now, CancellationToken ct = default)
        {
            LastNow = now;
            return Task.FromResult(true);
        }

        public Task<bool> SaveAsync(Guid officeId, Guid docId, string title, string contentMd, string? author, DateTimeOffset now, CancellationToken ct = default)
        {
            LastNow = now;
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task Staging_a_pending_edit_stamps_the_clock()
    {
        var repo = new FakeDraftRepo();
        var svc = new DraftDocumentService(repo, new FixedClock());

        var pendingId = await svc.StagePendingAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), "yeni içerik", "özet");

        Assert.NotNull(pendingId);
        Assert.Equal(Fixed, repo.LastNow);
    }

    [Fact]
    public async Task Get_delegates_to_the_repository()
    {
        var repo = new FakeDraftRepo();
        var svc = new DraftDocumentService(repo, new FixedClock());

        var doc = await svc.GetAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.NotNull(doc);
        Assert.Equal("body", doc!.ContentMd);
    }
}
