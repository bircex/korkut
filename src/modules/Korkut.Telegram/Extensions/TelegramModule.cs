using Korkut.Contract.Telegram;
using Korkut.Core;
using Korkut.Telegram.Repositories;
using Korkut.Telegram.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Telegram.Extensions;

/// <summary>
/// Self-registration for the Telegram module. The poller itself is a hosted service the app host
/// adds with its other background work (<see cref="TelegramPollingService"/>).
/// </summary>
public sealed class TelegramModule : IModule
{
    /// <summary>Where the Bot API lives; tests and air-gapped setups point it at a stand-in.</summary>
    public const string ApiUrlSetting = "KORKUT_TELEGRAM_API_URL";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITelegramRepository, TelegramRepository>();
        services.AddScoped<ITelegramSettingsService, TelegramSettingsService>();
        services.AddScoped<ITelegramLinkService, TelegramLinkService>();
        services.AddScoped<TelegramUpdateHandler>();
        services.AddScoped<TelegramGroupHandler>();
        services.AddScoped<TelegramGroupGuard>();
        services.AddScoped<ITelegramNotifier, TelegramNotifier>();
        services.AddSingleton<ITelegramApi, TelegramApi>();
        services.AddSingleton<TelegramBotStatus>();

        var apiUrl = configuration[ApiUrlSetting];
        services.AddHttpClient(TelegramApi.HttpClientName, client =>
            {
                client.BaseAddress = new Uri((string.IsNullOrWhiteSpace(apiUrl) ? "https://api.telegram.org" : apiUrl).TrimEnd('/') + "/");
                // Longer than the 25-second long poll.
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            // The bot token is part of every request URL: keep it out of the logs.
            .RemoveAllLoggers();
    }
}
