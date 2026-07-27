using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Clients;
using Korkut.Clients.Entities;
using Korkut.Matters;
using Korkut.Hearings;
using Korkut.Tasks;
using Korkut.Tasks.Entities;
using Korkut.Deadlines;
using Korkut.Correspondence;

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
            db.Matters.Add(new Matter(office, "M1", null, null, null, now));
            db.Matters.Add(new Matter(office, "M2", null, null, null, now));
            var closedMatter = new Matter(office, "M3", null, null, null, now);
            closedMatter.Close();
            db.Matters.Add(closedMatter);
            var goneMatter = new Matter(office, "gone", null, null, null, now);
            goneMatter.SoftDelete(now);
            db.Matters.Add(goneMatter); // excluded
            db.Clients.Add(new Client(office, "C1", "individual", null, null, now));

            db.Tasks.Add(new WorkTask(office, "t-open", null, null, null, now));
            db.Tasks.Add(new WorkTask(office, "t-overdue", null, null, now.AddDays(-1), now));
            var doneTask = new WorkTask(office, "t-done", null, null, null, now);
            doneTask.SetStatus("done", now);
            db.Tasks.Add(doneTask);

            db.MatterHearings.Add(new Hearing(office, db.Matters.Local.First().Id, now.AddDays(3), null, null, null));
            db.Deadlines.Add(new Deadline(office, "d-upcoming", today.AddDays(5), "internal", null, "Europe/Istanbul", 0, now));
            db.Deadlines.Add(new Deadline(office, "d-overdue", today.AddDays(-3), "internal", null, "Europe/Istanbul", 0, now));

            db.Correspondence.Add(new CorrespondenceItem(office, "incoming", "e1", null, null, null, null, now));
            db.Correspondence.Add(new CorrespondenceItem(office, "incoming", "e2", null, null, null, null, now));
            db.Correspondence.Add(new CorrespondenceItem(office, "outgoing", "e3", null, null, null, null, now));
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
