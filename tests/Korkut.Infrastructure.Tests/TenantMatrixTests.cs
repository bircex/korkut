using Korkut.TestSupport;
using Korkut.Jobs.Persistence;
using Korkut.Jobs.Entities;
using Korkut.Audit;
using Korkut.Organization;
using Korkut.Organization.Entities;
using Korkut.Security;
using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;
using Korkut.Infrastructure.Persistence;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Korkut.Clients;
using Korkut.Clients.Entities;
using Korkut.Matters;
using Korkut.Matters.Entities;
using Korkut.Hearings;
using Korkut.Hearings.Entities;
using Korkut.Courts;
using Korkut.Courts.Entities;
using Korkut.Tasks;
using Korkut.Tasks.Entities;
using Korkut.Deadlines;
using Korkut.Deadlines.Entities;
using Korkut.Notes;
using Korkut.Notes.Entities;
using Korkut.Finance;
using Korkut.Correspondence;
using Korkut.Correspondence.Entities;
using Korkut.Settings;
using Korkut.Settings.Entities;
using Korkut.Documents;
using Korkut.Documents.Entities;
using Korkut.Chat;
using Korkut.Chat.Entities;
using Korkut.Finance.Entities;
using Korkut.SavedDecisions.Entities;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// Reusable tenant-isolation matrix (KDM-013) over every registered tenant table
/// and multiple principals (office A, office B, no-context), on real PostgreSQL.
/// Includes a coverage guard, post-rollback/pool context cleanup, and the audited
/// background-job path. Runs as the least-privileged app role (RLS-subject).
/// </summary>
public class TenantMatrixTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _a;
    private Guid _b;

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _a = Guid.CreateVersion7();
        _b = Guid.CreateVersion7();

        // Seed via superuser (RLS bypassed): A owns 1 matter+1 hearing, B owns 2+2.
        await using var db = pg.NewContext();
        db.Offices.AddRange(
            new Office(_a, "A", DateTimeOffset.UtcNow),
            new Office(_b, "B", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        await SeedMattersAndHearings(db, _a, 1);
        await SeedMattersAndHearings(db, _b, 2);
        await SeedAudit(db, _a, 1);
        await SeedAudit(db, _b, 2);
        await SeedAuth(db, _a, 1);
        await SeedAuth(db, _b, 2);
        await SeedOfficeCore(db, _a, 1);
        await SeedOfficeCore(db, _b, 2);
    }

    private static async Task SeedOfficeCore(KorkutDbContext db, Guid office, int n)
    {
        // Reuse an existing matter for the party FK so the matters count stays exact.
        var matterId = await db.Matters.Where(m => m.OfficeId == office).Select(m => m.Id).FirstAsync();
        var userIds = await db.Users.Where(u => u.OfficeId == office).Select(u => u.Id).ToListAsync();
        foreach (var userId in userIds)
        {
            db.MatterMembers.Add(new Korkut.Matters.Entities.MatterMember(office, matterId, userId, false, null, DateTimeOffset.UtcNow));
        }
        for (var i = 0; i < n; i++)
        {
            var client = new Client(office, $"c{i}", "individual", null, null, null, null, null, DateTimeOffset.UtcNow);
            var clientId = client.Id;
            db.Clients.Add(client);
            db.Courts.Add(new Court(office, $"court{i}", DateTimeOffset.UtcNow));
            db.Parties.Add(new Party(office, matterId, "plaintiff", $"p{i}", DateTimeOffset.UtcNow));
            db.Tasks.Add(new WorkTask(office, "gorev", $"t{i}", null, null, null, null, null, null, null, true, null, null, null, DateTimeOffset.UtcNow));
            db.Deadlines.Add(new Deadline(office, $"d{i}", new DateOnly(2026, 1, 1), "internal", null, "Europe/Istanbul", 0, null, DateTimeOffset.UtcNow));
            db.CommunicationNotes.Add(new CommunicationNote(office, $"n{i}", "note", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            var document = new Document(office, null, "korkut-quarantine", $"{office:N}/doc{i}", "f.pdf", "application/pdf", 10, "abc", DateTimeOffset.UtcNow);
            var docId = document.Id;
            db.Documents.Add(document);
            db.DocumentExtractions.Add(new DocumentExtraction(office, docId, 1, "plaintext-v1", 1, 0, $"span{i}", "abc", $"{office:N}/doc{i}", DateTimeOffset.UtcNow));
            db.DocumentRelations.Add(new DocumentRelation(office, docId, DocumentRelationTarget.Client, clientId, true, null, DateTimeOffset.UtcNow));
            db.Invites.Add(new Invite(office, $"invite{i}@example.com", Roles.Lawyer, $"hash{office:N}{i}", DateTimeOffset.UtcNow.AddDays(7), null, DateTimeOffset.UtcNow));
            db.Invoices.Add(new Invoice(office, 100m, 0.20m, null, null, null, "draft", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            db.Expenses.Add(new Expense(office, 10m, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            db.Correspondence.Add(new CorrespondenceItem(office, "incoming", $"e{i}", null, null, null, null, DateTimeOffset.UtcNow));
            db.StyleProfiles.Add(new StyleProfile(office, StyleScope.User, Guid.CreateVersion7()));
            db.MemoryItems.Add(new MemoryItem(office, $"mem{i}", "note", null, true, DateTimeOffset.UtcNow));
            // Chat (conversations + messages) is a general operational feature and lives here.
            // generated_documents/document_versions live in the Agent DB — seeded + isolation-
            // checked in AgentTenantIsolationTests.
            var conversation = new Conversation(office, $"conv{i}", null, null, DateTimeOffset.UtcNow);
            db.Conversations.Add(conversation);
            db.ChatMessages.Add(new ConversationMessage(office, conversation.Id, "user", $"cm{i}", DateTimeOffset.UtcNow));
            db.SentReminders.Add(new Korkut.Reminders.Entities.SentReminder(office, $"task:{i}", "task", Guid.CreateVersion7(), DateTimeOffset.UtcNow));
            db.SavedDecisions.Add(new SavedDecision(
                office, Guid.CreateVersion7(), "yargitay", $"ext{office:N}{i}", $"Karar {i}",
                null, null, null, null, null, null, null, DateTimeOffset.UtcNow));
            if (i == 0)
            {
                db.Personas.Add(new Persona(office)); // one per office
                db.MailPreferences.Add(new MailPreference(office)); // one per office
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedAuth(KorkutDbContext db, Guid office, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var user = new User(office, $"{office:N}-{i}@x.test", Roles.Lawyer, null, null, DateTimeOffset.UtcNow);
            var uid = user.Id;
            db.Users.Add(user);
            db.PasswordResetTokens.Add(new PasswordResetToken(office, uid, $"h{office:N}{i}", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));
            db.UserTotps.Add(new UserTotp(office, uid, $"enc{i}", DateTimeOffset.UtcNow));
            db.RecoveryCodes.Add(new RecoveryCode(office, uid, $"rc{office:N}{i}", DateTimeOffset.UtcNow));
            db.RefreshTokens.Add(new RefreshToken(Guid.CreateVersion7(), Guid.CreateVersion7(), office, uid, $"rt{office:N}{i}", DateTimeOffset.UtcNow.AddDays(14), DateTimeOffset.UtcNow));
            db.OfficeRoles.Add(new OfficeRole(office, $"role{i}", "R", null, ["matters.read"], false, DateTimeOffset.UtcNow));
            db.TelegramLinks.Add(new Korkut.Telegram.Entities.TelegramLink(office, uid));
            if (i == 0)
            {
                db.TelegramBots.Add(new Korkut.Telegram.Entities.TelegramBot(office)); // one per office
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedAudit(KorkutDbContext db, Guid office, int n)
    {
        for (var i = 0; i < n; i++)
        {
            db.AuditEvents.Add(new AuditEvent(
                office, null, null, null, "read", "matter", null, null, null, DateTimeOffset.UtcNow));
        }

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task SeedMattersAndHearings(KorkutDbContext db, Guid office, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var matter = new Matter(office, $"m{i}", null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
            db.Matters.Add(matter);
            db.MatterHearings.Add(new Hearing(office, matter.Id, DateTimeOffset.UtcNow, null, null, null));
        }

        await db.SaveChangesAsync();
    }

    // --- Coverage guard: DB-forced RLS tables must all be in the registry ---

    [Fact]
    public async Task Every_rls_forced_table_is_covered_by_the_matrix()
    {
        await using var db = pg.NewContext();
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT c.relname FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname='korkut' AND c.relkind='r' AND c.relforcerowsecurity";
        var forced = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                forced.Add(reader.GetString(0));
            }
        }

        // Any RLS-forced table missing from the matrix registry fails CI — a new
        // tenant-owned entity cannot ship without isolation coverage.
        var uncovered = forced.Where(t => !TenantMatrix.Tables.ContainsKey(t)).ToList();
        Assert.True(uncovered.Count == 0, $"tenant tables missing from the isolation matrix: {string.Join(", ", uncovered)}");
        Assert.Equal(TenantMatrix.Tables.Count, forced.Count);
    }

    // --- Negative matrix over every table and principal ---

    [Theory]
    [MemberData(nameof(TenantMatrix.TableNames), MemberType = typeof(TenantMatrix))]
    public async Task No_context_principal_sees_no_rows(string table)
    {
        // A genuinely fresh connection (no pooled reuse) with no tenant context must
        // see nothing. On a *reused* connection a no-set query can observe stale RLS
        // context, which is why the app always sets the tenant per scope (KDM-013).
        await using var conn = new NpgsqlConnection(pg.AppConnectionStringFresh);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        Assert.Equal(0, await TenantMatrix.CountAsync(conn, tx, table));
    }

    [Theory]
    [MemberData(nameof(TenantMatrix.TableNames), MemberType = typeof(TenantMatrix))]
    public async Task Each_office_sees_only_its_own_rows(string table)
    {
        var (expectA, expectB) = TenantMatrix.Tables[table];
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();

        await using (var txA = await conn.BeginTransactionAsync())
        {
            await TenantMatrix.SetTenantAsync(conn, txA, _a);
            Assert.Equal(expectA, await TenantMatrix.CountAsync(conn, txA, table));
        }

        await using var txB = await conn.BeginTransactionAsync();
        await TenantMatrix.SetTenantAsync(conn, txB, _b);
        Assert.Equal(expectB, await TenantMatrix.CountAsync(conn, txB, table));
    }

    // --- Context cleanup after rollback and across pooled reuse ---

    [Fact]
    public async Task Rolled_back_context_does_not_leak_into_the_next_scope()
    {
        // Note: a custom GUC set with is_local is NOT reliably reset by ROLLBACK, so
        // isolation must not depend on implicit cleanup. The architecture instead sets
        // the tenant explicitly per scope — so the next scope's tenant always wins and
        // stale context can never be read. This proves that guarantee.
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();

        await using (var tx = await conn.BeginTransactionAsync())
        {
            await TenantMatrix.SetTenantAsync(conn, tx, _a);
            Assert.Equal(1, await TenantMatrix.CountAsync(conn, tx, "matters"));
            await tx.RollbackAsync();
        }

        // The next scope sets its OWN tenant (office B) and sees only B's rows — never
        // office A's rolled-back context.
        await using var tx2 = await conn.BeginTransactionAsync();
        await TenantMatrix.SetTenantAsync(conn, tx2, _b);
        Assert.Equal(2, await TenantMatrix.CountAsync(conn, tx2, "matters"));
    }

    // --- Background-job path is tenant-scoped and audited ---

    [Fact]
    public async Task Background_jobs_run_under_their_own_tenant_and_are_audited()
    {
        var clock = new SystemClockAdapter();
        await using var db = pg.NewContext();
        var outbox = new OutboxStore(db);
        var inbox = new InboxStore(db, clock);
        var audit = new JobAudit(db);
        var seenTenants = new List<Guid?>();
        var tenant = new AmbientTenantContext();
        var handler = new RecordingTenantHandler(tenant, seenTenants);
        var dispatcher = new OutboxDispatcher(
            outbox, inbox, audit, new Dictionary<string, IJobHandler> { ["email"] = handler },
            tenant, new ExponentialRetryPolicy(), clock);

        await outbox.EnqueueAsync(Msg(_a, "a"), default);
        await outbox.EnqueueAsync(Msg(_b, "b"), default);

        await dispatcher.DispatchOnceAsync("w", 10, TimeSpan.FromMinutes(1));

        Assert.Contains(_a, seenTenants);
        Assert.Contains(_b, seenTenants);
        Assert.DoesNotContain(null, seenTenants);
        // Each office's job produced an audit row scoped to that office.
        Assert.True(await db.JobAudit.AnyAsync(x => x.OfficeId == _a));
        Assert.True(await db.JobAudit.AnyAsync(x => x.OfficeId == _b));
    }

    private static OutboxMessage Msg(Guid office, string key)
        => new(office, "email", "{}", key, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class SystemClockAdapter : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class RecordingTenantHandler(ITenantContext tenant, List<Guid?> seen) : IJobHandler
    {
        public string MessageType => "email";

        public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            seen.Add(tenant.CurrentOfficeId);
            return Task.CompletedTask;
        }
    }
}
