using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Core;
using Korkut.TestSupport;

namespace Korkut.Settings.Integration.Tests;

/// <summary>
/// Office mail preference over the real API + real PostgreSQL with RLS. Covers the upsert,
/// the owner/member permission split (office.settings), and tenant isolation.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class MailPreferenceEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task MailPreference_upsert_and_can_edit_flag()
    {
        var (office, owner) = await factory.SeedOfficeAsync();
        var ownerClient = factory.ClientFor(office, owner); // owner → office.settings

        // Default: notifications on, no address, owner can_edit true.
        var initial = await ownerClient.GetFromJsonAsync<JsonElement>("/api/mail-preferences", Json);
        Assert.True(initial.GetProperty("notifications_enabled").GetBoolean());
        Assert.True(initial.GetProperty("notify_email").ValueKind == JsonValueKind.Null);
        Assert.True(initial.GetProperty("can_edit").GetBoolean());

        var put = await ownerClient.PutAsJsonAsync("/api/mail-preferences",
            new { notificationsEnabled = false, notifyEmail = "bildirim@buro.test" }, Json);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var saved = await ownerClient.GetFromJsonAsync<JsonElement>("/api/mail-preferences", Json);
        Assert.False(saved.GetProperty("notifications_enabled").GetBoolean());
        Assert.Equal("bildirim@buro.test", saved.GetProperty("notify_email").GetString());

        // A lawyer can read but not edit (403 on PUT; can_edit false).
        var lawyer = factory.ClientFor(office, owner, Roles.Lawyer);
        var lawyerView = await lawyer.GetFromJsonAsync<JsonElement>("/api/mail-preferences", Json);
        Assert.False(lawyerView.GetProperty("can_edit").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await lawyer.PutAsJsonAsync("/api/mail-preferences", new { notificationsEnabled = true }, Json)).StatusCode);
    }

    [Fact]
    public async Task One_office_never_sees_another_offices_mail_preference()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(officeA, userA);
        var clientB = factory.ClientFor(officeB, userB);

        await clientA.PutAsJsonAsync("/api/mail-preferences",
            new { notificationsEnabled = false, notifyEmail = "a@gizli.test" }, Json);

        var bPref = await clientB.GetFromJsonAsync<JsonElement>("/api/mail-preferences", Json);
        Assert.True(bPref.GetProperty("notifications_enabled").GetBoolean());   // B's default, untouched
        Assert.True(bPref.GetProperty("notify_email").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/mail-preferences")).StatusCode);
    }
}
