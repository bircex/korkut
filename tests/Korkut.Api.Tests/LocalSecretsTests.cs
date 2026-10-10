using Korkut.Web;
using Microsoft.Extensions.Configuration;

namespace Korkut.Api.Tests;

/// <summary>
/// A local install starts with no configuration: missing host secrets are generated once into a
/// shared directory and read back on every later start, by every host that points at it.
/// </summary>
public sealed class LocalSecretsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "korkut-secrets-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private ConfigurationManager Start(params (string Key, string Value)[] environment)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(
            environment.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value))
                .Append(new KeyValuePair<string, string?>(LocalSecrets.DirectorySetting, _directory)));
        configuration.AddKorkutLocalSecrets();
        return configuration;
    }

    [Fact]
    public void Missing_secrets_are_generated_in_the_shapes_the_hosts_require()
    {
        var configuration = Start();

        Assert.Equal(64, configuration["KORKUT_SECRET"]!.Length); // 32 bytes, hex
        Assert.Equal(32, Convert.FromBase64String(configuration["KORKUT_TOTP_KEY"]!).Length);
    }

    [Fact]
    public void Secret_files_are_readable_by_their_owner_only()
    {
        Start();

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(Path.Combine(_directory, "korkut_secret")));
        }
    }

    [Fact]
    public void A_restart_reads_the_same_secrets_back()
    {
        var first = Start();
        var second = Start();

        Assert.Equal(first["KORKUT_SECRET"], second["KORKUT_SECRET"]);
        Assert.Equal(first["KORKUT_TOTP_KEY"], second["KORKUT_TOTP_KEY"]);
    }

    [Fact]
    public void A_value_from_the_environment_wins_and_is_not_written()
    {
        var configuration = Start(("KORKUT_SECRET", "from-the-environment-at-least-32-bytes"));

        Assert.Equal("from-the-environment-at-least-32-bytes", configuration["KORKUT_SECRET"]);
        Assert.False(File.Exists(Path.Combine(_directory, "korkut_secret")));
        Assert.NotNull(configuration["KORKUT_TOTP_KEY"]);
    }

    [Fact]
    public async Task Hosts_starting_together_agree_on_one_value()
    {
        var secrets = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => Start()["KORKUT_SECRET"])));

        Assert.Single(secrets.Distinct());
        Assert.NotNull(secrets[0]);
    }

    [Fact]
    public void An_unusable_directory_leaves_the_setting_missing()
    {
        // A file where the directory should be: nothing can be created under it.
        File.WriteAllText(_directory, "not a directory");
        try
        {
            Assert.Null(Start()["KORKUT_SECRET"]);
        }
        finally
        {
            File.Delete(_directory);
        }
    }
}
