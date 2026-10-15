using Korkut.Organization.Services;

namespace Korkut.Api.Setup;

/// <summary>
/// On start, makes sure the installation's office and its admin exist, before the app takes
/// requests. The installer generates <c>KORKUT_ADMIN_PASSWORD</c>; without it (tests, a host that
/// manages accounts itself) nothing is created.
/// </summary>
public sealed partial class InstallationAdminBootstrap(
    IServiceScopeFactory scopes, IConfiguration configuration, ILogger<InstallationAdminBootstrap> logger) : IHostedService
{
    public const string PasswordSetting = "KORKUT_ADMIN_PASSWORD";
    public const string OfficeNameSetting = "KORKUT_OFFICE_NAME";

    /// <summary>Set by <c>./install.sh --reset-admin-password</c> for one start: puts the admin's
    /// password back to <see cref="PasswordSetting"/>.</summary>
    public const string ResetSetting = "KORKUT_ADMIN_PASSWORD_RESET";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var password = configuration[PasswordSetting];
        if (string.IsNullOrWhiteSpace(password))
        {
            NoPassword(logger);
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var outcome = await scope.ServiceProvider.GetRequiredService<InstallationAdminService>().EnsureAsync(
            password, configuration[OfficeNameSetting],
            string.Equals(configuration[ResetSetting], "1", StringComparison.Ordinal)
                || string.Equals(configuration[ResetSetting], "true", StringComparison.OrdinalIgnoreCase),
            cancellationToken);
        if (outcome != InstallationAdminOutcome.Unchanged)
        {
            Ensured(logger, outcome);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Installation admin (korkut-admin): {Outcome}.")]
    private static partial void Ensured(ILogger logger, InstallationAdminOutcome outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "KORKUT_ADMIN_PASSWORD is not set, so no installation admin was created; run ./install.sh.")]
    private static partial void NoPassword(ILogger logger);
}
