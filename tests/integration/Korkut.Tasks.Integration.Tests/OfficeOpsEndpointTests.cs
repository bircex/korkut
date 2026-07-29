using Korkut.TestSupport;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Korkut.Tasks.Tests;

/// <summary>
/// Office-ops endpoints (tasks, deadlines, notes) over the real API + real PostgreSQL
/// with RLS (endpoint migration, slice 4).
/// </summary>
[Collection(IntegrationSuite.Name)]
public class OfficeOpsEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Task_create_complete_and_delete()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var create = await client.PostAsJsonAsync("/api/tasks", new { title = "Dilekçe hazırla" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var patch = await client.PatchAsJsonAsync($"/api/tasks/{id}", new { status = "done" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var done = await patch.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("done", done.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, done.GetProperty("completed_at").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tasks/{id}")).StatusCode);
    }

    [Fact]
    public async Task Deadline_create_list_and_complete()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var create = await client.PostAsJsonAsync("/api/deadlines", new { title = "İtiraz süresi", due_date = "2026-09-01", kind = "statutory", reminder_days_before = 3 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/deadlines", Json);
        Assert.Contains(list!, d => d.GetProperty("id").GetGuid() == id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/deadlines/{id}/complete", null)).StatusCode);
        // Completing again is a 404 (already completed).
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/deadlines/{id}/complete", null)).StatusCode);
    }

    [Fact]
    public async Task Note_create_and_list_are_tenant_isolated()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();

        var a = factory.ClientFor(officeA, userA);
        var create = await a.PostAsJsonAsync("/api/notes", new { body = "Müvekkil arandı", channel = "phone" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var b = factory.ClientFor(officeB, userB);
        var bList = await b.GetFromJsonAsync<List<JsonElement>>("/api/notes", Json);
        Assert.DoesNotContain(bList!, n => n.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Invalid_task_status_is_422()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var create = await client.PostAsJsonAsync("/api/tasks", new { title = "x" });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var patch = await client.PatchAsJsonAsync($"/api/tasks/{id}", new { status = "bogus" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, patch.StatusCode);
    }
}
