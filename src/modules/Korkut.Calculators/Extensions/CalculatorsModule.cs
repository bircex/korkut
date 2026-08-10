using Korkut.Calculators.Services;
using Korkut.Calculators.Services.Rates;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Calculators.Extensions;

/// <summary>Self-registration for the Calculators module: the calculator service + the TCMB rates client its exchange-rate calculator needs.</summary>
public sealed class CalculatorsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TcmbRatesClient>();
        services.AddScoped<ICalculatorService, CalculatorService>();

        // TCMB daily exchange-rate bulletins (get_exchange_rates / calc_doviz_kurlari). Public
        // portal, no API key; a plain timeout is enough (low request volume, no shared-load concern
        // like the LawClients sources).
        services.AddHttpClient("tcmb", c =>
        {
            c.BaseAddress = new Uri("https://www.tcmb.gov.tr");
            c.Timeout = TimeSpan.FromSeconds(30);
        });
    }
}
