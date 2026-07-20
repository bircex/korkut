using Korkut.Application.Security;
using Korkut.Infrastructure.Templates;

namespace Korkut.Api.Endpoints;

public sealed record CoverageCourtResponse(string Label, int Total);

public sealed record CoverageResponse(
    int TotalDecisions, IReadOnlyList<CoverageCourtResponse> ByCourt, IReadOnlyList<string> DecisionSources, string Legislation);

/// <summary>
/// Assistant coverage: what document/legal ground the catalog covers (total types, a
/// per-court petition breakdown, source legislation). Derived from the static catalog,
/// so it is the same for every office. Authenticated (matters.read).
/// </summary>
public static class CoverageEndpoints
{
    public static IEndpointRouteBuilder MapCoverageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/coverage", (TemplateCatalog catalog) =>
            {
                var c = catalog.Coverage();
                return Results.Ok(new CoverageResponse(
                    c.TotalTypes,
                    c.ByCourt.Select(x => new CoverageCourtResponse(x.Label, x.Total)).ToList(),
                    c.Sources,
                    c.Legislation));
            })
            .RequireAuthorization(Permissions.MattersRead);

        return app;
    }
}
