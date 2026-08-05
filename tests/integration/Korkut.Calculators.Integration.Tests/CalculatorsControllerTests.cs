using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Korkut.TestSupport;

namespace Korkut.Calculators.Tests;

/// <summary>
/// Routing + auth + DI wiring for /api/calc/* through the real API host: proves CalculatorsModule
/// is actually reachable (ControllerSetup application part, DomainSetup registration) and that a
/// real request round-trips correctly end-to-end. Computation correctness itself is covered
/// exhaustively by Korkut.Calculators.Unit.Tests.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class CalculatorsControllerTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private static StringContent Body(object value) =>
        new(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Calculation_without_authentication_is_rejected()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/calc/nafaka", Body(new { nafaka_turu = "yoksulluk", yukumlu_net_gelir = 100000 }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Alimony_calculation_round_trips_with_the_correct_amounts()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var response = await client.PostAsync("/api/calc/nafaka", Body(new { nafaka_turu = "yoksulluk", yukumlu_net_gelir = 100000 }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(20000m, body.GetProperty("tahmini_alt_sinir_tl").GetDecimal());
        Assert.Equal(25000m, body.GetProperty("tahmini_ust_sinir_tl").GetDecimal());
    }

    [Fact]
    public async Task Invalid_input_is_a_bad_request()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var response = await client.PostAsync("/api/calc/nafaka", Body(new { nafaka_turu = "yoksulluk", yukumlu_net_gelir = -1 }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
