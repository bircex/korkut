using Korkut.TestSupport;
using Korkut.Core;
using Korkut.Organization;
using Korkut.Organization.Entities;
using Korkut.Security.Auth;
using Korkut.Infrastructure.Persistence;
using Korkut.Security;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// The cross-tenant Google sign-in resolvers must work when called AS the app role
/// (which is itself subject to RLS) — proving the SECURITY DEFINER + BYPASSRLS
/// resolver pattern, not a broad app-role bypass.
/// </summary>
public class GoogleResolverTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _officeA;
    private Guid _officeBDisabled;
    private Guid _linkedUser;
    private Guid _unlinkedUser;

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _officeA = Guid.CreateVersion7();
        _officeBDisabled = Guid.CreateVersion7();

        await using var db = pg.NewContext(); // superuser seed
        db.Offices.Add(new Office(_officeA, "A", DateTimeOffset.UtcNow));
        var disabledOffice = new Office(_officeBDisabled, "B", DateTimeOffset.UtcNow);
        disabledOffice.Disable();
        db.Offices.Add(disabledOffice);
        var linked = new User(_officeA, "linked@x.test", Roles.Lawyer, null, null, DateTimeOffset.UtcNow);
        linked.LinkGoogle("sub-linked");
        linked.EnableTwoFactor();
        _linkedUser = linked.Id;
        var unlinked = new User(_officeBDisabled, "unlinked@x.test", Roles.Lawyer, null, null, DateTimeOffset.UtcNow);
        _unlinkedUser = unlinked.Id;
        db.Users.Add(linked);
        db.Users.Add(unlinked);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task App_role_resolves_across_tenants_via_the_definer_functions()
    {
        // Connected as korkut_app (RLS-subject) with NO tenant context: a direct query
        // would see nothing, but the SECURITY DEFINER resolvers still find the user.
        await using var db = pg.NewAppContext();
        var dir = new GoogleAccountDirectory(db);
        var accounts = new AccountLocator(db);

        var bySub = await dir.FindByGoogleSubAsync("sub-linked");
        Assert.NotNull(bySub);
        Assert.Equal(_linkedUser, bySub!.UserId);

        // Only unlinked accounts are returned for first-time email linking.
        var byEmail = await accounts.FindByEmailAsync("unlinked@x.test");
        Assert.Single(byEmail);
        Assert.Empty(await accounts.FindByEmailAsync("linked@x.test")); // already linked

        Assert.True(await dir.IsOfficeDisabledAsync(_officeBDisabled));
        Assert.False(await dir.IsOfficeDisabledAsync(_officeA));
        Assert.True(await dir.UserHasTwoFactorAsync(_linkedUser));
    }

    [Fact]
    public async Task App_role_can_link_a_google_subject_via_the_definer_function()
    {
        await using var db = pg.NewAppContext();
        await new GoogleAccountDirectory(db).LinkGoogleSubAsync(_unlinkedUser, "sub-new");

        await using var check = pg.NewContext();
        var u = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstAsync(check.Users, x => x.Id == _unlinkedUser);
        Assert.Equal("sub-new", u.GoogleSub);
    }
}
