using Korkut.TestSupport;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Korkut.Finance.Tests;

/// <summary>
/// Finance (invoices, expenses, summary) over the real API + real PostgreSQL with RLS
/// (endpoint migration, slice 10). Verifies CRUD, the derived gross, the money summary
/// invariant, tenant isolation, and least-privilege (billing permissions).
/// </summary>
[Collection(IntegrationSuite.Name)]
public class FinanceEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Invoice_lifecycle_and_derived_gross()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        // Create: 1000 net @ 20% VAT → gross 1200.
        var create = await client.PostAsJsonAsync("/api/finance/invoices",
            new { amount = 1000m, vatRate = 0.20m, description = "Danışmanlık", status = "sent" }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invoice = await create.Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = invoice.GetProperty("id").GetGuid();
        Assert.Equal(1200m, invoice.GetProperty("gross").GetDecimal());
        Assert.Equal("sent", invoice.GetProperty("status").GetString());

        // Patch to paid.
        var patch = await client.PatchAsJsonAsync($"/api/finance/invoices/{id}", new { status = "paid" }, Json);
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal("paid", (await patch.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString());

        // List reflects it.
        var list = (await client.GetFromJsonAsync<Envelope<JsonElement>>("/api/finance/invoices", Json))!.Items;
        Assert.Contains(list!, i => i.GetProperty("id").GetGuid() == id);

        // Delete.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/finance/invoices/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/finance/invoices/{id}", new { status = "draft" }, Json)).StatusCode);
    }

    [Fact]
    public async Task Summary_reflects_paid_outstanding_and_expenses()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        // 1000@20% paid (gross 1200), 500@20% sent (gross 600), 100@20% cancelled (excluded).
        await client.PostAsJsonAsync("/api/finance/invoices", new { amount = 1000m, vatRate = 0.20m, status = "paid" }, Json);
        await client.PostAsJsonAsync("/api/finance/invoices", new { amount = 500m, vatRate = 0.20m, status = "sent" }, Json);
        await client.PostAsJsonAsync("/api/finance/invoices", new { amount = 100m, vatRate = 0.20m, status = "cancelled" }, Json);
        await client.PostAsJsonAsync("/api/finance/expenses", new { amount = 250m, kind = "harç" }, Json);

        var s = await client.GetFromJsonAsync<JsonElement>("/api/finance/summary", Json);
        Assert.Equal(1800m, s.GetProperty("invoiced").GetDecimal());      // 1200 + 600
        Assert.Equal(1200m, s.GetProperty("paid").GetDecimal());
        Assert.Equal(600m, s.GetProperty("outstanding").GetDecimal());     // 1800 - 1200
        Assert.Equal(250m, s.GetProperty("expenses").GetDecimal());
    }

    [Fact]
    public async Task Expenses_are_created_listed_and_deleted()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var created = await (await client.PostAsJsonAsync("/api/finance/expenses",
            new { amount = 75.50m, kind = "yol", description = "Duruşma" }, Json)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(75.50m, created.GetProperty("amount").GetDecimal());

        var list = (await client.GetFromJsonAsync<Envelope<JsonElement>>("/api/finance/expenses", Json))!.Items;
        Assert.Contains(list!, e => e.GetProperty("id").GetGuid() == id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/finance/expenses/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/finance/expenses/{id}")).StatusCode);
    }

    [Fact]
    public async Task One_office_never_sees_another_offices_invoices()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(officeA, userA);
        var clientB = factory.ClientFor(officeB, userB);

        var created = await (await clientA.PostAsJsonAsync("/api/finance/invoices",
            new { amount = 999m, vatRate = 0.0m }, Json)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();

        // B's list excludes A's invoice, and B cannot patch/delete it (RLS → not found).
        var listB = (await clientB.GetFromJsonAsync<Envelope<JsonElement>>("/api/finance/invoices", Json))!.Items;
        Assert.DoesNotContain(listB!, i => i.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/finance/invoices/{id}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/finance/summary")).StatusCode);
    }
}
