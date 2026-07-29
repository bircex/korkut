using Korkut.TestSupport;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Matters;
using Korkut.Matters.Entities;
using Korkut.Hearings;
using Korkut.Hearings.Entities;
using Korkut.Deadlines;
using Korkut.Deadlines.Entities;

namespace Korkut.Api.Tests;

/// <summary>
/// Calendar + agenda over the real API + real PostgreSQL with RLS (endpoint migration,
/// slice 12). Covers calendar_events CRUD, the unified read view (which also surfaces
/// real hearings + deadlines read-only), the agenda look-ahead, and tenant isolation.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class CalendarEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>Seed a matter, a hearing on it, and an open deadline directly (owner ctx bypasses RLS).</summary>
    private async Task<(Guid MatterId, Guid HearingId, Guid DeadlineId)> SeedMatterAgendaAsync(
        Guid office, DateTimeOffset hearingAt, DateOnly deadlineDue)
    {
        await using var db = factory.OwnerContext();
        var matter = new Matter(office, "Dava X", null, null, null, DateTimeOffset.UtcNow);
        db.Matters.Add(matter);
        var hearing = new Hearing(office, matter.Id, hearingAt, null, null, null);
        db.MatterHearings.Add(hearing);
        var deadline = new Deadline(office, "Cevap süresi", deadlineDue, "internal", matter.Id, "Europe/Istanbul", 0, DateTimeOffset.UtcNow);
        db.Deadlines.Add(deadline);
        await db.SaveChangesAsync();
        return (matter.Id, hearing.Id, deadline.Id);
    }

    [Fact]
    public async Task Appointment_crud_roundtrip()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var created = await (await client.PostAsJsonAsync("/api/calendar/events",
            new { category = "appointment", title = "Müvekkil görüşmesi", startAt = "2026-08-01T10:00:00+00:00", location = "Büro", remindBeforeMin = 30 }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();
        Assert.True(created.GetProperty("editable").GetBoolean());
        Assert.Equal("appointment", created.GetProperty("category").GetString());
        Assert.Equal(30, created.GetProperty("remind_before_min").GetInt32());

        var patch = await client.PatchAsJsonAsync($"/api/calendar/events/{id}", new { status = "done", result = "Tamamlandı" }, Json);
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal("done", (await patch.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/calendar/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/calendar/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Unified_view_surfaces_hearings_and_deadlines_readonly()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var (matterId, hearingId, deadlineId) = await SeedMatterAgendaAsync(
            office, DateTimeOffset.UtcNow.AddDays(2), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));

        var events = await client.GetFromJsonAsync<List<JsonElement>>("/api/calendar/events", Json);
        var hearing = events!.Single(e => e.GetProperty("id").GetGuid() == hearingId);
        var deadline = events!.Single(e => e.GetProperty("id").GetGuid() == deadlineId);
        Assert.Equal("hearing", hearing.GetProperty("category").GetString());
        Assert.Equal("Dava X", hearing.GetProperty("matter_title").GetString());
        Assert.False(hearing.GetProperty("editable").GetBoolean());
        Assert.Equal("deadline", deadline.GetProperty("category").GetString());
        Assert.False(deadline.GetProperty("editable").GetBoolean());

        // Category filter narrows correctly.
        var onlyHearings = await client.GetFromJsonAsync<List<JsonElement>>("/api/calendar/events?category=hearing", Json);
        Assert.All(onlyHearings!, e => Assert.Equal("hearing", e.GetProperty("category").GetString()));

        // A read-only hearing cannot be patched/deleted through the calendar (its own domain owns it).
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/calendar/events/{hearingId}")).StatusCode);
    }

    [Fact]
    public async Task Agenda_returns_upcoming_hearings_and_deadlines()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var (_, hearingId, deadlineId) = await SeedMatterAgendaAsync(
            office, DateTimeOffset.UtcNow.AddDays(2), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));

        var agenda = await client.GetFromJsonAsync<JsonElement>("/api/calendar/upcoming?days=7", Json);
        Assert.Equal(7, agenda.GetProperty("days").GetInt32());
        var items = agenda.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("id").GetGuid() == hearingId && i.GetProperty("kind").GetString() == "hearing");
        Assert.Contains(items, i => i.GetProperty("id").GetGuid() == deadlineId && i.GetProperty("kind").GetString() == "deadline");
    }

    [Fact]
    public async Task One_office_never_sees_another_offices_calendar()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(officeA, userA);
        var clientB = factory.ClientFor(officeB, userB);

        var created = await (await clientA.PostAsJsonAsync("/api/calendar/events",
            new { category = "reminder", title = "Gizli", startAt = "2026-08-01T09:00:00+00:00" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();

        var listB = await clientB.GetFromJsonAsync<List<JsonElement>>("/api/calendar/events", Json);
        Assert.DoesNotContain(listB!, e => e.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/calendar/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/calendar/upcoming")).StatusCode);
    }
}
