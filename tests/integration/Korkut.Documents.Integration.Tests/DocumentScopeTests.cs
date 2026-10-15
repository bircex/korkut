using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Korkut.Contract.Documents;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Documents.Integration.Tests;

/// <summary>
/// Who sees a document, over the real API and PostgreSQL: whoever uploaded it, the team of a matter
/// it is linked to, and whoever sees every matter. Anyone else gets not found, for reading,
/// downloading and linking alike, and nobody links a document to a matter they cannot see.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class DocumentScopeTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private async Task<Guid> AddLawyerAsync(Guid office)
    {
        await using var db = factory.OwnerContext();
        var user = new User(office, $"avukat-{Guid.CreateVersion7():N}@example.com", Roles.Lawyer, null, null, DateTimeOffset.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> UploadAsync(HttpClient client, string name)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes($"%PDF-1.7\n{name}\n%%EOF"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", name);
        var up = await client.PostAsync("/api/documents", form);
        Assert.Equal(HttpStatusCode.Created, up.StatusCode);
        return (await up.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    private static async Task<HashSet<Guid>> ListAsync(HttpClient client, string url = "/api/documents")
        => (await client.GetFromJsonAsync<List<JsonElement>>(url, Json))!.Select(d => d.GetProperty("id").GetGuid()).ToHashSet();

    [Fact]
    public async Task A_document_is_seen_by_its_uploader_and_its_matters_team()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var (aId, bId) = (await AddLawyerAsync(office), await AddLawyerAsync(office));
        var owner = factory.ClientFor(office, ownerId);
        var a = factory.ClientFor(office, aId, Roles.Lawyer);
        var b = factory.ClientFor(office, bId, Roles.Lawyer);
        var matter = (await (await owner.PostAsJsonAsync("/api/matters", new { title = "Dosya" }, Json)).Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        Assert.True((await owner.PostAsJsonAsync($"/api/matters/{matter}/team", new { user_id = aId }, Json)).IsSuccessStatusCode);

        var onMatter = await UploadAsync(owner, "dilekce.pdf");
        Assert.True((await owner.PostAsJsonAsync($"/api/documents/{onMatter}/links", new { target_type = "matter", target_id = matter }, Json)).IsSuccessStatusCode);
        var bsOwn = await UploadAsync(b, "taslak.pdf");

        // The team member sees the matter's document, also when listing by matter; B does not.
        Assert.Contains(onMatter, await ListAsync(a));
        Assert.Equal([onMatter], await ListAsync(a, $"/api/documents?matterId={matter}"));
        Assert.DoesNotContain(onMatter, await ListAsync(b));
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/documents/{onMatter}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.PostAsJsonAsync($"/api/documents/{onMatter}/links", new { target_type = "client", target_id = Guid.NewGuid() }, Json)).StatusCode);

        // B's own upload is B's; the owner sees everything.
        Assert.Contains(bsOwn, await ListAsync(b));
        Assert.DoesNotContain(bsOwn, await ListAsync(a));
        Assert.Superset(new HashSet<Guid> { onMatter, bsOwn }, await ListAsync(owner));

        // Nobody links their document to a matter they cannot see.
        var linked = await b.PostAsJsonAsync($"/api/documents/{bsOwn}/links", new { target_type = "matter", target_id = matter }, Json);
        Assert.Equal(HttpStatusCode.NotFound, linked.StatusCode);

        // Search over documents follows the same rule, and a matter's conversation only reaches that matter's.
        var aOwn = await UploadAsync(a, "notlar.pdf");
        Assert.Equivalent(new[] { onMatter, aOwn }, await SearchableAsync(office, aId, null));
        Assert.Equal([onMatter], await SearchableAsync(office, aId, matter));
        Assert.Equal([bsOwn], await SearchableAsync(office, bId, null));
        Assert.Null(await SearchableAsync(office, ownerId, null, Roles.OfficeOwner));
        Assert.Equal([onMatter], await SearchableAsync(office, ownerId, matter, Roles.OfficeOwner));
    }

    private async Task<IReadOnlySet<Guid>?> SearchableAsync(Guid office, Guid user, Guid? matter, string role = Roles.Lawyer)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ActingUser>().Set(office, user, role, Roles.BuiltIn[role]);
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().InTenantScopeAsync(office,
            () => scope.ServiceProvider.GetRequiredService<IDocumentAccess>().VisibleDocumentIdsAsync(office, matter));
    }
}
