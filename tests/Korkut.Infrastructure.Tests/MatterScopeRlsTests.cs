using Korkut.Core;
using Korkut.Matters.Entities;
using Korkut.Organization.Entities;
using Korkut.Tasks.Entities;
using Korkut.TestSupport;
using Npgsql;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// The matter scope held by the database itself, as the app role, with no application code in
/// between: a member's transaction sees only their matters and those matters' rows, one who sees
/// every matter sees them all, and a transaction with nobody behind it keeps the office boundary
/// only (background jobs). Rows with no matter are left to the app.
/// </summary>
public class MatterScopeRlsTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _office;
    private Guid _ali;
    private Guid _ayse;
    private Guid _alisMatter;
    private Guid _aysesMatter;

    public async Task InitializeAsync()
    {
        _office = Guid.CreateVersion7();
        await using var db = pg.NewContext();
        db.Offices.Add(new Office(_office, "O", DateTimeOffset.UtcNow));
        var ali = new User(_office, $"ali-{_office:N}@x.test", Roles.Lawyer, null, null, DateTimeOffset.UtcNow);
        var ayse = new User(_office, $"ayse-{_office:N}@x.test", Roles.Lawyer, null, null, DateTimeOffset.UtcNow);
        db.Users.AddRange(ali, ayse);
        (_ali, _ayse) = (ali.Id, ayse.Id);
        var alis = new Matter(_office, "Ali'nin dosyası", null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        var ayses = new Matter(_office, "Ayşe'nin dosyası", null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        db.Matters.AddRange(alis, ayses);
        (_alisMatter, _aysesMatter) = (alis.Id, ayses.Id);
        await db.SaveChangesAsync();
        db.MatterMembers.Add(new MatterMember(_office, alis.Id, ali.Id, true, null, DateTimeOffset.UtcNow));
        db.MatterMembers.Add(new MatterMember(_office, ayses.Id, ayse.Id, true, null, DateTimeOffset.UtcNow));
        foreach (var matter in new Guid?[] { alis.Id, ayses.Id, null })
        {
            db.Tasks.Add(new WorkTask(_office, "gorev", $"t-{matter}", null, null, matter, null, null, null, null, true, null, null, null, DateTimeOffset.UtcNow));
        }

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<List<Guid>> VisibleAsync(string sql, Guid? user, bool allMatters = false)
    {
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using (var set = conn.CreateCommand())
        {
            set.Transaction = tx;
            set.CommandText = "SELECT set_config('app.current_office', @o, true), set_config('app.current_user', @u, true), set_config('app.all_matters', @a, true)";
            set.Parameters.AddWithValue("o", _office.ToString());
            set.Parameters.AddWithValue("u", user?.ToString() ?? string.Empty);
            set.Parameters.AddWithValue("a", allMatters || user is null ? "1" : "0");
            await set.ExecuteNonQueryAsync();
        }

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.IsDBNull(0) ? Guid.Empty : reader.GetGuid(0));
        }

        return ids;
    }

    [Fact]
    public async Task A_member_sees_only_their_matters_and_their_rows()
    {
        Assert.Equal([_alisMatter], await VisibleAsync("SELECT id FROM korkut.matters", _ali));
        Assert.Equal([_aysesMatter], await VisibleAsync("SELECT id FROM korkut.matters", _ayse));

        // Ali's matter's task and the task with no matter (that one is the app's to judge).
        var tasks = await VisibleAsync("SELECT matter_id FROM korkut.tasks", _ali);
        Assert.Equivalent(new[] { _alisMatter, Guid.Empty }, tasks);
        Assert.Equal([_ali], await VisibleAsync("SELECT user_id FROM korkut.matter_members", _ali));
    }

    [Fact]
    public async Task Seeing_every_matter_and_running_as_nobody_see_the_whole_office()
    {
        Assert.Equal(2, (await VisibleAsync("SELECT id FROM korkut.matters", _ali, allMatters: true)).Count);
        Assert.Equal(2, (await VisibleAsync("SELECT id FROM korkut.matters", null)).Count);
        Assert.Equal(3, (await VisibleAsync("SELECT id FROM korkut.tasks", null)).Count);
    }

    [Fact]
    public async Task A_member_cannot_change_or_delete_another_matters_rows()
    {
        Assert.Empty(await VisibleAsync($"UPDATE korkut.matters SET title = 'x' WHERE id = '{_aysesMatter}' RETURNING id", _ali));
        Assert.Empty(await VisibleAsync($"DELETE FROM korkut.tasks WHERE matter_id = '{_aysesMatter}' RETURNING id", _ali));
    }
}
