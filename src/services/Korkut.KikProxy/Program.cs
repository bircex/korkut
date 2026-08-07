using Korkut.KikProxy;

var builder = WebApplication.CreateBuilder(args);

// Timeout only — no resilience/retry/rate-limit pipeline here. Korkut.LawClients' own
// "legal"-family clients already retry/rate-limit the call INTO this proxy; retrying twice
// (here AND at the caller) would just make a slow upstream slower.
builder.Services.AddHttpClient("kik", c => c.Timeout = TimeSpan.FromSeconds(25));

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.KikProxy", status = "ok" }));
app.MapGet("/health/live", () => Results.Ok());
app.MapGet("/health/ready", () => Results.Ok());

app.MapGet("/search", async (
    string query,
    string? decisionType,
    int? page,
    int? limit,
    string? kararNo,
    string? basvuran,
    string? idareAdi,
    string? subject,
    string? dateStart,
    string? dateEnd,
    string? year,
    IHttpClientFactory httpFactory,
    CancellationToken ct) =>
{
    var client = httpFactory.CreateClient("kik");
    try
    {
        var results = await KikScraper.SearchAsync(
            client, query, decisionType, page, limit,
            kararNo, basvuran, idareAdi, subject, dateStart, dateEnd, year, ct);
        return Results.Ok(new { results });
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.Run();

public partial class Program;
