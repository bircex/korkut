using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Core;
using Korkut.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization.Integration.Tests;

/// <summary>A database of its own: first-run setup is only meaningful on an install with no office.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FreshInstallSuite : ICollectionFixture<KorkutApiFactory>
{
    public const string Name = "fresh-install";
}

/// <summary>
/// First-run setup over the real API and a real, empty PostgreSQL connected as the
/// least-privileged app role: the status a fresh install reports, the one setup that creates
/// an enabled office and its owner, the ordinary sign-in that follows, and that setup can never
/// run a second time, not even from two browsers at once. One ordered scenario, because the
/// install only starts empty once.
/// </summary>
[Collection(FreshInstallSuite.Name)]
public class FirstRunSetupTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static object Setup(string email, string officeName = "Deneme Hukuk Bürosu") => new
    {
        office_name = officeName,
        full_name = "Av. Deniz Yılmaz",
        email,
        password = "kurulum-parolasi-2026",
    };

    private async Task<bool> SetupRequiredAsync()
    {
        var status = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/setup");
        return status.GetProperty("setup_required").GetBoolean();
    }

    [Fact]
    public async Task A_fresh_install_is_set_up_once_and_its_owner_signs_in()
    {
        Assert.True(await SetupRequiredAsync());

        // Two browsers racing on the fresh install: exactly one office comes out of it.
        var anonymous = factory.CreateClient();
        var raced = await Task.WhenAll(
            anonymous.PostAsJsonAsync("/api/setup", Setup("sahip@ornek.av.tr")),
            anonymous.PostAsJsonAsync("/api/setup", Setup("ikinci@ornek.av.tr", "Başka Büro")));
        Assert.Single(raced, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(raced, r => r.StatusCode == HttpStatusCode.Conflict);
        var winner = raced[0].StatusCode == HttpStatusCode.OK ? "sahip@ornek.av.tr" : "ikinci@ornek.av.tr";

        await using (var db = factory.OwnerContext())
        {
            var office = await db.Offices.AsNoTracking().SingleAsync();
            Assert.False(office.Disabled); // ready to use, no approval step
            var owner = await db.Users.AsNoTracking().SingleAsync();
            Assert.Equal(winner, owner.Email);
            Assert.Equal(Roles.OfficeOwner, owner.Role);
        }

        Assert.False(await SetupRequiredAsync());

        // The owner signs in through the ordinary login with what they chose at setup.
        var client = factory.CookieClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = winner, password = "kurulum-parolasi-2026" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal(winner, me.GetProperty("email").GetString());

        // Once set up, setup is closed for good.
        var again = await factory.CreateClient().PostAsJsonAsync("/api/setup", Setup("saldirgan@ornek.com"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        await using (var db = factory.OwnerContext())
        {
            Assert.Equal(1, await db.Offices.CountAsync());
        }
    }

    [Fact]
    public async Task Setup_validates_its_input()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/setup", new
        {
            office_name = "",
            full_name = "Av. Deniz Yılmaz",
            email = "e-posta-degil",
            password = "kisa",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
