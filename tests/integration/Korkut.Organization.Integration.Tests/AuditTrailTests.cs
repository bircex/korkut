using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.TestSupport;

namespace Korkut.Organization.Integration.Tests;

/// <summary>
/// The access log over the real API and PostgreSQL: looking at a client's details or the office's
/// money, changing a member's role or a matter's team, and being refused all leave a line, which
/// only those with audit.read can read. Client details are recorded by field name, never by value.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class AuditTrailTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private async Task<Guid> AddMemberAsync(Guid office, string role)
    {
        await using var db = factory.OwnerContext();
        var user = new User(office, $"uye-{Guid.CreateVersion7():N}@example.com", role, "Av. Deniz", null, DateTimeOffset.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<List<JsonElement>> LogAsync(HttpClient client, string? action = null)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/audit{(action is null ? "" : $"?action={action}")}", Json);
        return page.GetProperty("items").EnumerateArray().ToList();
    }

    [Fact]
    public async Task Sensitive_looks_and_changes_are_logged_for_those_who_may_read_the_log()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var lawyerId = await AddMemberAsync(office, Roles.Lawyer);
        var owner = factory.ClientFor(office, ownerId);
        var lawyer = factory.ClientFor(office, lawyerId, Roles.Lawyer);

        // A client's contact details in view.
        var client = await (await owner.PostAsJsonAsync("/api/clients", new { full_name = "Ayşe Yılmaz", email = "ayse@example.com" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var clientId = client.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/clients/{clientId}")).StatusCode);

        // The office's money, a refused look at it, a team change and a role change.
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/finance/invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.GetAsync("/api/finance/invoices")).StatusCode);
        var matter = (await (await owner.PostAsJsonAsync("/api/matters", new { title = "Dosya" }, Json)).Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("id").GetGuid();
        Assert.True((await owner.PostAsJsonAsync($"/api/matters/{matter}/team", new { user_id = lawyerId }, Json)).IsSuccessStatusCode);
        Assert.True((await owner.PatchAsJsonAsync($"/api/members/{lawyerId}", new { role = Roles.Intern }, Json)).IsSuccessStatusCode);

        var log = await LogAsync(owner);
        var viewed = log.Single(e => e.GetProperty("action").GetString() == "client.details_viewed");
        Assert.Equal(clientId, viewed.GetProperty("entity_id").GetGuid());
        Assert.Contains("email", viewed.GetProperty("details").GetString());
        Assert.DoesNotContain("ayse@example.com", viewed.GetProperty("details").GetString());
        Assert.Contains(log, e => e.GetProperty("action").GetString() == "finance.viewed");
        var denied = log.Single(e => e.GetProperty("action").GetString() == "access.denied");
        Assert.Equal(lawyerId, denied.GetProperty("actor_id").GetGuid());
        Assert.Equal("Av. Deniz", denied.GetProperty("actor_name").GetString());
        Assert.Contains("finance.read", denied.GetProperty("details").GetString());
        Assert.Contains(log, e => e.GetProperty("action").GetString() == "matter.team_added" && e.GetProperty("entity_id").GetGuid() == matter);
        Assert.Contains(log, e => e.GetProperty("action").GetString() == "member.role_changed" && e.GetProperty("entity_id").GetGuid() == lawyerId);

        // Newest first, and filterable by kind.
        Assert.Equal("member.role_changed", log[0].GetProperty("action").GetString());
        Assert.All(await LogAsync(owner, "matter"), e => Assert.StartsWith("matter.", e.GetProperty("action").GetString()));

        // Reading the log takes audit.read (and the attempt is itself logged).
        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.GetAsync("/api/audit")).StatusCode);
    }
}
