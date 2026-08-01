using Korkut.Organization;
using Korkut.Security;

namespace Korkut.Api.Auth;

/// <summary>
/// The platform-admin Google OIDC flow. Same client + flow store as the office flow but a
/// SEPARATE pre-registered redirect URI (KORKUT_ADMIN_REDIRECT_URI — the admin origin's
/// /admin/api/auth/google/callback), because Google binds the code exchange to the exact
/// redirect it authorized. Completion delegates to <see cref="AdminSignInService"/> (allowlist
/// gate + mandatory-TOTP routing); a dedicated exchanger instance carries the admin redirect.
/// </summary>
public sealed class AdminGoogleFlow
{
    private readonly GoogleOidcOptions _options;
    private readonly IOidcFlowStore _flowStore;
    private readonly AdminSignInService _signIn;
    private readonly Korkut.Core.IClock _clock;

    public AdminGoogleFlow(
        HttpClient http,
        GoogleOidcOptions officeOptions,
        IConfiguration configuration,
        IOidcFlowStore flowStore,
        IGoogleIdTokenValidator idTokenValidator,
        AdminTwoFactorService enrollment,
        Korkut.Core.IClock clock)
    {
        _options = new GoogleOidcOptions
        {
            ClientId = officeOptions.ClientId,
            ClientSecret = officeOptions.ClientSecret,
            RedirectUri = configuration["KORKUT_ADMIN_REDIRECT_URI"] ?? string.Empty,
        };
        _flowStore = flowStore;
        _clock = clock;
        _signIn = new AdminSignInService(flowStore, new GoogleTokenExchanger(http, _options), idTokenValidator, enrollment);
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<string> BeginAsync(CancellationToken ct = default)
    {
        var challenge = OidcChallenge.Create();
        await _flowStore.SaveAsync(challenge, _clock.UtcNow + TimeSpan.FromMinutes(10), ct);

        var pairs = new (string Key, string Value)[]
        {
            ("client_id", _options.ClientId),
            ("redirect_uri", _options.RedirectUri),
            ("response_type", "code"),
            ("scope", "openid email profile"),
            ("state", challenge.State),
            ("nonce", challenge.Nonce),
            ("code_challenge", challenge.CodeChallenge),
            ("code_challenge_method", "S256"),
            ("access_type", "online"),
            ("prompt", "select_account"),
        };
        var query = string.Join('&', pairs.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"https://accounts.google.com/o/oauth2/v2/auth?{query}";
    }

    public Task<AdminSignInResult> CompleteAsync(string state, string code, CancellationToken ct = default)
        => _signIn.CompleteAsync(state, code, ct);
}
