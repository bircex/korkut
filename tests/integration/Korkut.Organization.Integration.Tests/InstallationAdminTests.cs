using System.Net;
using System.Net.Http.Json;
using Korkut.Contract.Organization;
using Korkut.Core;
using Korkut.Organization.Services;
using Korkut.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Organization.Integration.Tests;

/// <summary>A database of its own: the installation admin is only created on an install with no office.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FreshInstallSuite : ICollectionFixture<KorkutApiFactory>
{
    public const string Name = "fresh-install";
}

/// <summary>
/// The account every installation starts with, over the real API and a real, empty PostgreSQL
/// connected as the least-privileged app role: created once on start with the installer's
/// password, signed in with its user name, its password changed in the app, recovered with the
/// installer's reset, and added to an older install that had no admin. One ordered scenario,
/// because the install only starts empty once.
/// </summary>
[Collection(FreshInstallSuite.Name)]
public class InstallationAdminTests(KorkutApiFactory factory)
{
    private const string FirstPassword = "Kurulum1Parola2026x";

    private async Task<InstallationAdminOutcome> EnsureAsync(string password, bool reset = false, string? officeName = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<InstallationAdminService>().EnsureAsync(password, officeName, reset);
    }

    private static async Task<HttpStatusCode> SignInAsync(HttpClient client, string password)
        => (await client.PostAsJsonAsync("/api/auth/login", new { email = InstallationAdmin.UserName, password })).StatusCode;

    [Fact]
    public async Task The_installation_admin_is_created_once_and_runs_the_office()
    {
        // First start of a fresh install: the office and its admin.
        Assert.Equal(InstallationAdminOutcome.Created, await EnsureAsync(FirstPassword, officeName: "Yılmaz Hukuk"));
        Assert.Equal(InstallationAdminOutcome.Unchanged, await EnsureAsync("ignored-on-later-starts"));
        await using (var db = factory.OwnerContext())
        {
            var office = await db.Offices.AsNoTracking().SingleAsync();
            Assert.Equal("Yılmaz Hukuk", office.Name);
            Assert.False(office.Disabled);
            var admin = await db.Users.AsNoTracking().SingleAsync();
            Assert.Equal(InstallationAdmin.UserName, admin.Email);
            Assert.Equal(Roles.OfficeOwner, admin.Role);
        }

        // The admin signs in with the user name; the profile carries it as the user name.
        var client = factory.CookieClient();
        Assert.Equal(HttpStatusCode.OK, await SignInAsync(client, FirstPassword));
        var me = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/me");
        Assert.Equal(InstallationAdmin.UserName, me.GetProperty("username").GetString());
        Assert.Equal("owner", me.GetProperty("role").GetString());

        // Changing the password needs the current one, and keeps this session signed in.
        var wrong = await client.PostAsJsonAsync("/api/me/password", new { current_password = "not-it-at-all", new_password = "Yeni-Parola-2026" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var changed = await client.PostAsJsonAsync("/api/me/password", new { current_password = FirstPassword, new_password = "Yeni-Parola-2026" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await SignInAsync(factory.CookieClient(), FirstPassword));
        Assert.Equal(HttpStatusCode.OK, await SignInAsync(factory.CookieClient(), "Yeni-Parola-2026"));

        // A later start does not touch the changed password; the installer's reset does.
        Assert.Equal(InstallationAdminOutcome.Unchanged, await EnsureAsync(FirstPassword));
        Assert.Equal(HttpStatusCode.OK, await SignInAsync(factory.CookieClient(), "Yeni-Parola-2026"));
        Assert.Equal(InstallationAdminOutcome.PasswordReset, await EnsureAsync("Sifirlandi-2026-abc", reset: true));
        Assert.Equal(HttpStatusCode.Unauthorized, await SignInAsync(factory.CookieClient(), "Yeni-Parola-2026"));
        Assert.Equal(HttpStatusCode.OK, await SignInAsync(factory.CookieClient(), "Sifirlandi-2026-abc"));

        // An install from before the admin existed gets one, next to its owners, in the same office.
        await using (var db = factory.OwnerContext())
        {
            await db.Users.Where(u => u.Email == InstallationAdmin.UserName).ExecuteDeleteAsync();
        }

        Assert.Equal(InstallationAdminOutcome.Created, await EnsureAsync(FirstPassword));
        await using (var db = factory.OwnerContext())
        {
            Assert.Equal(1, await db.Offices.CountAsync());
            Assert.Equal(1, await db.Users.CountAsync(u => u.Email == InstallationAdmin.UserName));
        }

        Assert.Equal(HttpStatusCode.OK, await SignInAsync(factory.CookieClient(), FirstPassword));
    }
}
