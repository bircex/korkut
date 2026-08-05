using Korkut.Calculators.Services;
using Korkut.Calculators.Services.Rates;

namespace Korkut.Calculators.Unit.Tests;

/// <summary>Shared real CalculatorService for the pure (non-HTTP) calculators' tool tests.</summary>
internal static class TestCalc
{
    public static CalculatorService Service { get; } = new(new TcmbRatesClient(new NoHttpFactory()));

    private sealed class NoHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("Not expected to be called by a non-exchange-rate calculator test.");
    }
}
