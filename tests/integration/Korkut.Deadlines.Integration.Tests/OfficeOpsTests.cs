using Korkut.Core;
using Korkut.Deadlines.Entities;
using Korkut.Organization.Entities;
using Korkut.Tasks.Entities;
using Korkut.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Deadlines.Integration.Tests;

public class OfficeOpsTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _office;

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _office = Guid.CreateVersion7();
        await using var db = pg.NewContext();
        db.Offices.Add(new Office(_office, "O", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- task lifecycle + tenant isolation (real Postgres) ---

    [Fact]
    public async Task Task_status_lifecycle_persists()
    {
        Guid id;
        await using (var db = pg.NewContext())
        {
            var task = new WorkTask(_office, "gorev", "Dilekçe yaz", null, null, null, null, null, null, null, true, null, null, null, DateTimeOffset.UtcNow);
            db.Tasks.Add(task);
            await db.SaveChangesAsync();
            id = task.Id;
        }

        await using (var db = pg.NewContext())
        {
            await db.Tasks.Where(x => x.Id == id).ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, "done").SetProperty(x => x.CompletedAt, DateTimeOffset.UtcNow), default);
        }

        await using var check = pg.NewContext();
        var t = await check.Tasks.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("done", t.Status);
        Assert.NotNull(t.CompletedAt);
    }
}
