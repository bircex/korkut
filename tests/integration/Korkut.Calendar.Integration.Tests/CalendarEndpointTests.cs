using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Deadlines.Entities;
using Korkut.Hearings.Entities;
using Korkut.Matters.Entities;
using Korkut.TestSupport;

namespace Korkut.Calendar.Integration.Tests;

/// <summary>
/// Calendar + agenda over the real API + real PostgreSQL with RLS. The calendar owns no table:
/// editable records (görev/hatırlatma/randevu) are created through <c>/api/tasks</c> and surface
/// on the calendar; hearings + deadlines surface read-only from their own domains. Covers the
/// unified read view, the agenda look-ahead, and tenant isolation.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class CalendarEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>Seed a matter, a hearing on it, and an open deadline directly (owner ctx bypasses RLS).</summary>
    private async Task<(Guid MatterId, Guid HearingId, Guid DeadlineId)> SeedMatterAgendaAsync(
        Guid office, DateTimeOffset hearingAt, DateOnly deadlineDue)
    {
        await using var db = factory.OwnerContext();
        var matter = new Matter(office, "Dava X", null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        db.Matters.Add(matter);
        var hearing = new Hearing(office, matter.Id, hearingAt, null, null, null);
        db.MatterHearings.Add(hearing);
        var deadline = new Deadline(office, "Cevap süresi", deadlineDue, "internal", matter.Id, "Europe/Istanbul", 0, null, DateTimeOffset.UtcNow);
        db.Deadlines.Add(deadline);
        await db.SaveChangesAsync();
        return (matter.Id, hearing.Id, deadline.Id);
    }

    [Fact]
    public async Task Appointment_created_as_task_surfaces_on_calendar_editable()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        // Editable calendar records are agenda records owned by Tasks.
        var created = await (await client.PostAsJsonAsync("/api/tasks",
            new { category = "randevu", title = "Müvekkil görüşmesi", dueDate = "2026-08-01T10:00:00+00:00", allDay = false, location = "Büro", remindBeforeMin = 30 }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("randevu", created.GetProperty("category").GetString());
        Assert.Equal(30, created.GetProperty("remind_before_min").GetInt32());

        // It shows up on the unified calendar view, marked editable.
        var events = (await client.GetFromJsonAsync<Envelope<JsonElement>>("/api/calendar/events", Json))!.Items;
        var entry = events!.Single(e => e.GetProperty("id").GetGuid() == id);
        Assert.True(entry.GetProperty("editable").GetBoolean());
        Assert.Equal("randevu", entry.GetProperty("category").GetString());
        Assert.Equal(30, entry.GetProperty("remind_before_min").GetInt32());

        // Edited + deleted through the Tasks API.
        var patch = await client.PatchAsJsonAsync($"/api/tasks/{id}", new { status = "done" }, Json);
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal("done", (await patch.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tasks/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/tasks/{id}")).StatusCode);
    }

    [Fact]
    public async Task Unified_view_surfaces_hearings_and_deadlines_readonly()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var (matterId, hearingId, deadlineId) = await SeedMatterAgendaAsync(
            office, DateTimeOffset.UtcNow.AddDays(2), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));

        var events = (await client.GetFromJsonAsync<Envelope<JsonElement>>("/api/calendar/events", Json))!.Items;
        var hearing = events!.Single(e => e.GetProperty("id").GetGuid() == hearingId);
        var deadline = events!.Single(e => e.GetProperty("id").GetGuid() == deadlineId);
        Assert.Equal("hearing", hearing.GetProperty("category").GetString());
        Assert.Equal("Dava X", hearing.GetProperty("matter_title").GetString());
        Assert.False(hearing.GetProperty("editable").GetBoolean());
        Assert.Equal("deadline", deadline.GetProperty("category").GetString());
        Assert.False(deadline.GetProperty("editable").GetBoolean());

        // Category filter narrows correctly.
        var onlyHearings = (await client.GetFromJsonAsync<Envelope<JsonElement>>("/api/calendar/events?category=hearing", Json))!.Items;
        Assert.All(onlyHearings!, e => Assert.Equal("hearing", e.GetProperty("category").GetString()));
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

        var created = await (await clientA.PostAsJsonAsync("/api/tasks",
            new { category = "hatirlatma", title = "Gizli", dueDate = "2026-08-01T09:00:00+00:00" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();

        var listB = (await clientB.GetFromJsonAsync<Envelope<JsonElement>>("/api/calendar/events", Json))!.Items;
        Assert.DoesNotContain(listB!, e => e.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/tasks/{id}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/calendar/upcoming")).StatusCode);
    }
}
