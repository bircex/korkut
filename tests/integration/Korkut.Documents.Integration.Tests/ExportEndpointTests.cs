using Korkut.TestSupport;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Korkut.Documents.Tests;

/// <summary>
/// Document export (markdown → .docx / .udf) over the real API (endpoint migration,
/// slice 16b). Verifies the produced files are valid ZIP+XML containers carrying the
/// content, the right MIME type/filename, format validation, and auth.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class ExportEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Markdown = "# Dava Dilekçesi\n\nSayın mahkeme, işbu **kalın** talebimizdir.\n\n- İlk madde\n- İkinci madde";

    [Fact]
    public async Task Docx_export_is_a_valid_openxml_package_with_the_content()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var res = await client.PostAsJsonAsync("/api/export", new { format = "docx", title = "Test Belgesi", markdown = Markdown }, Json);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", res.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Test-Belgesi.docx", res.Content.Headers.ContentDisposition!.FileNameStar ?? res.Content.Headers.ContentDisposition!.FileName);

        var bytes = await res.Content.ReadAsByteArrayAsync();
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        Assert.NotNull(zip.GetEntry("_rels/.rels"));
        var doc = ReadEntry(zip, "word/document.xml");
        Assert.Contains("Dava Dilekçesi", doc);
        Assert.Contains("kalın", doc);
        Assert.Contains("İlk madde", doc);
    }

    [Fact]
    public async Task Udf_export_is_a_zip_with_content_xml_carrying_the_text()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var res = await client.PostAsJsonAsync("/api/export", new { format = "udf", title = "Dilekçe", markdown = Markdown }, Json);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var bytes = await res.Content.ReadAsByteArrayAsync();
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var content = ReadEntry(zip, "content.xml");
        Assert.Contains("<template", content);
        Assert.Contains("Dava Dilekçesi", content); // heading text in the CDATA buffer
        Assert.Contains("startOffset", content);     // offset-based elements present
    }

    [Fact]
    public async Task Invalid_format_is_rejected()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var res = await client.PostAsJsonAsync("/api/export", new { format = "pdf", title = "x", markdown = "y" }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        var res = await anon.PostAsJsonAsync("/api/export", new { format = "docx", title = "x", markdown = "y" }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private static string ReadEntry(ZipArchive zip, string path)
    {
        using var s = zip.GetEntry(path)!.Open();
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    }
}
