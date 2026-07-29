using Korkut.TestSupport;
using Korkut.Core;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Security;

namespace Korkut.Api.Tests;

/// <summary>
/// Member parity: the members list now carries full_name / bar_registry_no (settable on
/// create) plus the derived is_active / bound / is_self flags — Python-contract fields
/// that were missing on the .NET member response.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class MemberParityTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task Create_and_list_expose_profile_and_derived_flags()
    {
        var (office, owner) = await factory.SeedOfficeAsync(); // owner is seeded (unbound, active)
        var client = factory.ClientFor(office, owner, Roles.OfficeOwner);

        // Add a member with the new profile fields.
        var created = await (await client.PostAsJsonAsync("/api/members",
            new { email = "yeni@example.com", role = "lawyer", fullName = "Av. Yeni", barRegistryNo = "99999" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Av. Yeni", created.GetProperty("full_name").GetString());
        Assert.Equal("99999", created.GetProperty("bar_registry_no").GetString());
        Assert.False(created.GetProperty("bound").GetBoolean());   // no Google link yet
        Assert.True(created.GetProperty("is_active").GetBoolean()); // not locked out
        Assert.False(created.GetProperty("is_self").GetBoolean());  // not the caller

        // The caller sees themselves flagged is_self.
        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/members", Json);
        var self = list!.Single(m => m.GetProperty("id").GetGuid() == owner);
        Assert.True(self.GetProperty("is_self").GetBoolean());
    }
}
