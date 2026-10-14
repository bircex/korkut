using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization.Integration.Tests;

/// <summary>
/// User-profile parity: /api/me returns full_name + bar_registry_no, and PATCH /api/me
/// (updateMe) sets them — Python-contract fields that were missing on the .NET user model.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class UserProfileEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task Me_defaults_null_profile_then_update_persists_it()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var before = await client.GetFromJsonAsync<JsonElement>("/api/me", Json);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("full_name").ValueKind);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("bar_registry_no").ValueKind);

        var patched = await client.PatchAsJsonAsync("/api/me", new { fullName = "Av. Recep Kızılarslan", barRegistryNo = "34567" }, Json);
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        var body = await patched.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Av. Recep Kızılarslan", body.GetProperty("full_name").GetString());
        Assert.Equal("34567", body.GetProperty("bar_registry_no").GetString());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/me", Json);
        Assert.Equal("Av. Recep Kızılarslan", after.GetProperty("full_name").GetString());
        Assert.Equal("34567", after.GetProperty("bar_registry_no").GetString());
    }

    [Fact]
    public async Task Me_reports_password_only_sign_in()
    {
        var (office, user, _) = await factory.SeedUserWithPasswordAsync("Parola-1234!");
        var me = await factory.ClientFor(office, user).GetFromJsonAsync<JsonElement>("/api/me", Json);

        Assert.True(me.GetProperty("has_password").GetBoolean());
        Assert.False(me.GetProperty("google_linked").GetBoolean());
    }

    [Fact]
    public async Task Me_reports_google_only_sign_in()
    {
        var (office, user, _) = await factory.SeedGoogleUserAsync();
        var me = await factory.ClientFor(office, user).GetFromJsonAsync<JsonElement>("/api/me", Json);

        Assert.False(me.GetProperty("has_password").GetBoolean());
        Assert.True(me.GetProperty("google_linked").GetBoolean());
    }

    [Fact]
    public async Task Me_reports_both_sign_in_methods()
    {
        var (office, user, _) = await factory.SeedUserWithPasswordAsync("Parola-1234!");
        await using (var db = factory.OwnerContext())
        {
            var row = await db.Users.SingleAsync(u => u.Id == user);
            row.LinkGoogle($"google-sub-{Guid.CreateVersion7():N}");
            await db.SaveChangesAsync();
        }

        var me = await factory.ClientFor(office, user).GetFromJsonAsync<JsonElement>("/api/me", Json);

        Assert.True(me.GetProperty("has_password").GetBoolean());
        Assert.True(me.GetProperty("google_linked").GetBoolean());
    }

    [Fact]
    public async Task Update_me_requires_auth()
    {
        var anon = factory.CreateClient();
        var res = await anon.PatchAsJsonAsync("/api/me", new { fullName = "x" }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
