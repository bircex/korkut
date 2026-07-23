using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.OfficeCore.Clients;

namespace Korkut.Api.Tests;

/// <summary>
/// Reports overview over the real API + real PostgreSQL with RLS (endpoint migration,
/// slice 14). Seeds matters/clients/tasks/deadlines/hearings/evrak + invoices, then
/// asserts the dashboard roll-ups and tenant scoping.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class ReportsEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task Overview_rolls_up_every_section()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // Seed directly (owner ctx bypasses RLS) for the non-calendar/finance bits.
        await using (var db = factory.OwnerContext())
        {
            db.Matters.Add(new MatterEntity { Id = Guid.CreateVersion7(), OfficeId = office, Title = "M1", Status = "open", CreatedAt = now });
            db.Matters.Add(new MatterEntity { Id = Guid.CreateVersion7(), OfficeId = office, Title = "M2", Status = "open", CreatedAt = now });
            db.Matters.Add(new MatterEntity { Id = Guid.CreateVersion7(), OfficeId = office, Title = "M3", Status = "closed", CreatedAt = now });
            db.Matters.Add(new MatterEntity { Id = Guid.CreateVersion7(), OfficeId = office, Title = "gone", Status = "open", DeletedAt = now, CreatedAt = now }); // excluded
            db.Clients.Add(new Client(office, "C1", "individual", null, null, now));

            db.Tasks.Add(new TaskEntity { Id = Guid.CreateVersion7(), OfficeId = office, Title = "t-open", Status = "open", CreatedAt = now });
            db.Tasks.Add(new TaskEntity { Id = Guid.CreateVersion7(), OfficeId = office, Title = "t-overdue", Status = "open", DueAt = now.AddDays(-1), CreatedAt = now });
            db.Tasks.Add(new TaskEntity { Id = Guid.CreateVersion7(), OfficeId = office, Title = "t-done", Status = "done", CompletedAt = now, CreatedAt = now });

            db.MatterHearings.Add(new MatterHearingEntity { Id = Guid.CreateVersion7(), OfficeId = office, MatterId = db.Matters.Local.First().Id, ScheduledAt = now.AddDays(3) });
            db.Deadlines.Add(new DeadlineEntity { Id = Guid.CreateVersion7(), OfficeId = office, Title = "d-upcoming", DueDate = today.AddDays(5), CreatedAt = now });
            db.Deadlines.Add(new DeadlineEntity { Id = Guid.CreateVersion7(), OfficeId = office, Title = "d-overdue", DueDate = today.AddDays(-3), CreatedAt = now });

            db.Correspondence.Add(new CorrespondenceEntity { Id = Guid.CreateVersion7(), OfficeId = office, Direction = "incoming", Title = "e1", CreatedAt = now });
            db.Correspondence.Add(new CorrespondenceEntity { Id = Guid.CreateVersion7(), OfficeId = office, Direction = "incoming", Title = "e2", CreatedAt = now });
            db.Correspondence.Add(new CorrespondenceEntity { Id = Guid.CreateVersion7(), OfficeId = office, Direction = "outgoing", Title = "e3", CreatedAt = now });
            await db.SaveChangesAsync();
        }

        // Finance via the API (exercises the same summary path the report reuses).
        await client.PostAsJsonAsync("/api/finance/invoices", new { amount = 1000m, vatRate = 0.20m, status = "paid" }, Json);
        await client.PostAsJsonAsync("/api/finance/expenses", new { amount = 200m }, Json);

        var r = await client.GetFromJsonAsync<JsonElement>("/api/reports/overview?days=14", Json);

        Assert.Equal(3, r.GetProperty("matters").GetProperty("total").GetInt32()); // soft-deleted excluded
        Assert.Equal(2, r.GetProperty("matters").GetProperty("by_status").GetProperty("open").GetInt32());
        Assert.Equal(1, r.GetProperty("clients").GetProperty("total").GetInt32());

        Assert.Equal(1200m, r.GetProperty("finance").GetProperty("invoiced").GetDecimal());
        Assert.Equal(1200m, r.GetProperty("finance").GetProperty("paid").GetDecimal());
        Assert.Equal(200m, r.GetProperty("finance").GetProperty("expenses").GetDecimal());

        Assert.Equal(2, r.GetProperty("tasks").GetProperty("open").GetInt32());     // t-open + t-overdue
        Assert.Equal(1, r.GetProperty("tasks").GetProperty("done").GetInt32());
        Assert.Equal(1, r.GetProperty("tasks").GetProperty("overdue").GetInt32());

        var agenda = r.GetProperty("agenda");
        Assert.Equal(14, agenda.GetProperty("days").GetInt32());
        Assert.Equal(1, agenda.GetProperty("hearings_upcoming").GetInt32());
        Assert.Equal(1, agenda.GetProperty("deadlines_upcoming").GetInt32());
        Assert.Equal(1, agenda.GetProperty("deadlines_overdue").GetInt32());

        Assert.Equal(3, r.GetProperty("evrak").GetProperty("total").GetInt32());
        Assert.Equal(2, r.GetProperty("evrak").GetProperty("by_direction").GetProperty("incoming").GetInt32());
    }

    [Fact]
    public async Task Overview_is_scoped_to_the_office()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(officeA, userA);
        var clientB = factory.ClientFor(officeB, userB);

        await clientA.PostAsJsonAsync("/api/finance/invoices", new { amount = 5000m, vatRate = 0.0m, status = "paid" }, Json);

        // B's dashboard doesn't see A's money.
        var rB = await clientB.GetFromJsonAsync<JsonElement>("/api/reports/overview", Json);
        Assert.Equal(0m, rB.GetProperty("finance").GetProperty("invoiced").GetDecimal());
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/reports/overview")).StatusCode);
    }
}
