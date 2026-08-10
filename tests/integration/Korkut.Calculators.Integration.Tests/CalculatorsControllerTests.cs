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

    /// <summary>
    /// The schema endpoint is the FE's single source of truth for required/min/max (KDM roadmap
    /// Faz 3) — this round-trips it through the real routing + JSON pipeline to make sure the
    /// route-slug keys and snake_case field names actually line up with what calcDefinitions.ts
    /// (and the request bodies above) expect, not just what the reflection code intends.
    /// </summary>
    [Fact]
    public async Task Schema_exposes_required_and_range_constraints_keyed_by_route_slug()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var response = await client.GetAsync("/api/calc/schema");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        var judicialFine = body.GetProperty("adli-para-cezasi");
        var gunSayisi = judicialFine.GetProperty("gun_sayisi");
        Assert.True(gunSayisi.GetProperty("required").GetBoolean());
        Assert.Equal(5, gunSayisi.GetProperty("minimum").GetInt32());
        Assert.Equal(730, gunSayisi.GetProperty("maximum").GetInt32());

        var taksit = judicialFine.GetProperty("taksit");
        Assert.True(taksit.GetProperty("required").GetBoolean());
        Assert.Equal(JsonValueKind.Null, taksit.GetProperty("minimum").ValueKind);

        var salary = body.GetProperty("brut-net-maas");
        var ay = salary.GetProperty("ay");
        Assert.False(ay.GetProperty("required").GetBoolean()); // int? Ay — optional
        Assert.Equal(1, ay.GetProperty("minimum").GetInt32());
        Assert.Equal(12, ay.GetProperty("maximum").GetInt32());

        // GET-only endpoints with no [FromBody] request (exchange rates) have nothing to render as a form.
        Assert.False(body.TryGetProperty("doviz-kurlari", out _));
    }
}
