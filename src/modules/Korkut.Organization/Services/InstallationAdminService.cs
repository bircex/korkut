using Korkut.Contract.Offices;
using Korkut.Contract.Organization;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.Organization.Repositories;

namespace Korkut.Organization.Services;

/// <summary>What starting the app did about the installation admin.</summary>
public enum InstallationAdminOutcome
{
    /// <summary>The admin already existed; nothing changed.</summary>
    Unchanged,

    /// <summary>The office and its admin were created (a fresh installation), or the admin was added to it.</summary>
    Created,

    /// <summary>The admin's password was set back to the installer's (a recovery the operator asked for).</summary>
    PasswordReset,
}

/// <summary>
/// Makes sure the installation has its office and the admin account (<see cref="InstallationAdmin.UserName"/>)
/// with the password the installer generated. Runs when the app starts, so there is no first-run
/// screen for a stranger to reach first. The password is only set on creation, or when the operator
/// asks for a reset; after that the admin changes it in the app.
/// </summary>
public sealed class InstallationAdminService(
    IOfficeProvisioner provisioner, IUserRepository users, IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, IClock clock)
{
    public const string DefaultOfficeName = "Hukuk Bürosu";
    private const string AdminName = "Yönetici";

    public async Task<InstallationAdminOutcome> EnsureAsync(
        string password, string? officeName, bool resetPassword, CancellationToken ct = default)
    {
        var hash = passwordHasher.Hash(password);
        var office = await provisioner.InstallOfficeAsync(ct);
        if (office is null)
        {
            var name = string.IsNullOrWhiteSpace(officeName) ? DefaultOfficeName : officeName.Trim();
            if (await provisioner.SetupFirstOfficeAsync(InstallationAdmin.UserName, hash, name, AdminName, ct) is not null)
            {
                return InstallationAdminOutcome.Created;
            }

            // Another instance created it a moment ago.
            office = await provisioner.InstallOfficeAsync(ct)
                ?? throw new InvalidOperationException("The installation's office could not be created.");
        }

        return await unitOfWork.InTenantScopeAsync(office.Id, async () =>
        {
            var admin = await users.FindByEmailForUpdateAsync(office.Id, InstallationAdmin.UserName, ct);
            if (admin is null)
            {
                // An install from before the admin existed: its owners keep their accounts, and the
                // admin joins them.
                admin = new User(office.Id, InstallationAdmin.UserName, Roles.OfficeOwner, AdminName, null, clock.UtcNow);
                admin.SetPassword(hash);
                await users.AddAsync(admin, ct);
                await users.SaveChangesAsync(ct);
                return InstallationAdminOutcome.Created;
            }

            if (!resetPassword)
            {
                return InstallationAdminOutcome.Unchanged;
            }

            admin.SetPassword(hash); // also ends the admin's sessions
            await users.SaveChangesAsync(ct);
            return InstallationAdminOutcome.PasswordReset;
        }, ct);
    }
}
