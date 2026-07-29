using Korkut.TestSupport;
using Korkut.Core;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Security;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Api.Tests;

/// <summary>
/// Messaging channels + integrations over the real API + real PostgreSQL with RLS
/// (endpoint migration, slice 15). Verifies config upserts, the keep/clear/set secret
/// semantics, that secrets are NEVER returned (only a configured flag) yet stored
/// encrypted, integration listing, link/unlink, the owner-only write gate, and tenant
/// isolation.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class ChannelEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task Email_secret_is_write_only_and_stored_encrypted()
    {
        var (office, owner) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, owner);

        var saved = await (await client.PutAsJsonAsync("/api/channels/email",
            new { enabled = true, fromAddress = "buro@example.com", fromName = "Büro", appPassword = "s3cr3t-app-pw" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(saved.GetProperty("has_password").GetBoolean());
        Assert.Equal("buro@example.com", saved.GetProperty("from_address").GetString());
        // The plaintext secret must never appear in the response payload.
        Assert.DoesNotContain("s3cr3t-app-pw", saved.GetRawText());

        // Stored value is encrypted (not the plaintext).
        await using (var db = factory.OwnerContext())
        {
            var stored = await db.Channels.AsNoTracking().SingleAsync(c => c.OfficeId == office && c.Kind == "email");
            Assert.NotNull(stored.EncryptedSecret);
            Assert.DoesNotContain("s3cr3t-app-pw", stored.EncryptedSecret!);
        }

        // null appPassword keeps the secret; "" clears it.
        await client.PutAsJsonAsync("/api/channels/email", new { enabled = false, fromAddress = "buro@example.com", fromName = "Büro", appPassword = (string?)null }, Json);
        var kept = await client.GetFromJsonAsync<JsonElement>("/api/channels/email", Json);
        Assert.True(kept.GetProperty("has_password").GetBoolean());
        Assert.False(kept.GetProperty("enabled").GetBoolean());

        await client.PutAsJsonAsync("/api/channels/email", new { enabled = false, fromAddress = "buro@example.com", fromName = "Büro", appPassword = "" }, Json);
        var cleared = await client.GetFromJsonAsync<JsonElement>("/api/channels/email", Json);
        Assert.False(cleared.GetProperty("has_password").GetBoolean());
    }

    [Fact]
    public async Task Telegram_and_whatsapp_report_configured()
    {
        var (office, owner) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, owner);

        var tg = await (await client.PutAsJsonAsync("/api/channels/telegram", new { enabled = true, botToken = "123:ABC" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(tg.GetProperty("ok").GetBoolean());
        Assert.True(tg.GetProperty("configured").GetBoolean());

        var wa = await (await client.PutAsJsonAsync("/api/channels/whatsapp", new { enabled = true, baseUrl = "https://wa.example.com", token = "wa-tok" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(wa.GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task Integrations_list_link_and_unlink()
    {
        var (office, owner) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, owner);

        await client.PutAsJsonAsync("/api/channels/telegram", new { enabled = true, botToken = "123:ABC" }, Json);

        var link = await (await client.PostAsync("/api/integrations/telegram/link", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("telegram", link.GetProperty("kind").GetString());
        Assert.False(string.IsNullOrEmpty(link.GetProperty("code").GetString()));

        var list = await client.GetFromJsonAsync<JsonElement>("/api/integrations", Json);
        var telegram = list.GetProperty("channels").EnumerateArray().Single(c => c.GetProperty("kind").GetString() == "telegram");
        Assert.True(telegram.GetProperty("enabled").GetBoolean());
        Assert.True(telegram.GetProperty("configured").GetBoolean());
        Assert.True(telegram.GetProperty("can_edit").GetBoolean());
        Assert.Equal(3, list.GetProperty("channels").GetArrayLength()); // telegram, whatsapp, email

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/integrations/telegram/link")).StatusCode);
    }

    [Fact]
    public async Task Members_can_read_but_not_write_channels()
    {
        var (office, owner) = await factory.SeedOfficeAsync();
        var lawyer = factory.ClientFor(office, owner, Roles.Lawyer);

        Assert.Equal(HttpStatusCode.OK, (await lawyer.GetAsync("/api/channels/email")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await lawyer.PutAsJsonAsync("/api/channels/email", new { enabled = true, fromAddress = "x@y.z", fromName = "X" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.PostAsync("/api/integrations/telegram/link", null)).StatusCode);
    }

    [Fact]
    public async Task One_office_never_sees_another_offices_channels()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(officeA, userA);
        var clientB = factory.ClientFor(officeB, userB);

        await clientA.PutAsJsonAsync("/api/channels/email", new { enabled = true, fromAddress = "a@a.a", fromName = "A", appPassword = "pw" }, Json);
        var bEmail = await clientB.GetFromJsonAsync<JsonElement>("/api/channels/email", Json);
        Assert.False(bEmail.GetProperty("has_password").GetBoolean());
        Assert.Equal("", bEmail.GetProperty("from_address").GetString());
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/integrations")).StatusCode);
    }
}
