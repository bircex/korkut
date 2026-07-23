using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Outbox;

namespace Korkut.Api.Tests;

/// <summary>
/// The outbound-message review queue over the real API + real PostgreSQL with RLS
/// (endpoint migration, slice 18c). A human edits/confirms/cancels the assistant's
/// drafts. Drafts are seeded directly (owner ctx) since the assistant is their producer.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class OutboxEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private async Task<Guid> SeedDraftAsync(Guid office)
    {
        await using var db = factory.OwnerContext();
        var message = new OutboundMessage(
            office, "email", "muvekkil@example.com", "Duruşma", "Sayın müvekkil…", null, DateTimeOffset.UtcNow);
        db.OutboundMessages.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    [Fact]
    public async Task List_get_edit_confirm_flow()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var id = await SeedDraftAsync(office);

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/outbox", Json);
        Assert.Contains(list!, m => m.GetProperty("id").GetGuid() == id);

        var got = await client.GetFromJsonAsync<JsonElement>($"/api/outbox/{id}", Json);
        Assert.Equal("muvekkil@example.com", got.GetProperty("to").GetString());
        Assert.Equal("draft", got.GetProperty("status").GetString());

        // Edit the draft.
        var edited = await (await client.PutAsJsonAsync($"/api/outbox/{id}",
            new { toAddr = "yeni@example.com", subject = "Güncellendi", body = "Yeni gövde", sendAt = (string?)null }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("yeni@example.com", edited.GetProperty("to").GetString());
        Assert.Equal("Güncellendi", edited.GetProperty("subject").GetString());

        // Confirm → queued; editing a non-draft now 404s.
        var confirmed = await (await client.PostAsync($"/api/outbox/{id}/confirm", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("queued", confirmed.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"/api/outbox/{id}", new { toAddr = "x@y.z", body = "b" }, Json)).StatusCode);
    }

    [Fact]
    public async Task Cancel_marks_the_message_cancelled()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var id = await SeedDraftAsync(office);

        var cancelled = await (await client.PostAsync($"/api/outbox/{id}/cancel", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
    }

    [Fact]
    public async Task One_office_never_sees_another_offices_outbox()
    {
        var (officeA, _) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientB = factory.ClientFor(officeB, userB);
        var id = await SeedDraftAsync(officeA);

        var listB = await clientB.GetFromJsonAsync<List<JsonElement>>("/api/outbox", Json);
        Assert.DoesNotContain(listB!, m => m.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/outbox/{id}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/outbox")).StatusCode);
    }
}
