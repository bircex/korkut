using System.Net.Http.Headers;
using Korkut.Core;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>
/// The AI settings screen's way to the model gateway's management API: providers and their keys,
/// which models are enabled, the route sets Korkut asks for, prices and usage. A narrow pass-through
/// to llmr's <c>/manage</c>, for the office owner only (<c>office.settings</c>). The gateway's
/// token is added here and never reaches the browser, and nothing the browser sends besides the
/// method, path, query and JSON body is forwarded.
/// </summary>
[ApiController]
[Route("api/settings/ai/manage")]
[Authorize(Policy = Permissions.IntegrationsManage)]
public sealed class AiSettingsController(IHttpClientFactory http) : ControllerBase
{
    public const string HttpClientName = "llmr-manage";

    // The parts of llmr's management API the screen uses. Anything else is not forwarded.
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "status", "provider-types", "providers", "models", "routes", "usage", "prices",
    };

    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE", Route = "{**path}")]
    public async Task<IActionResult> Forward(string? path, CancellationToken ct)
    {
        // The raw path keeps encoded characters (model ids may hold slashes, sent as %2F) exactly
        // as the browser wrote them; the route value would have decoded them.
        var raw = Request.Path.Value!["/api/settings/ai/manage".Length..].TrimStart('/');
        var segments = raw.Split('/');
        if (string.IsNullOrEmpty(path) || !Allowed.Contains(segments[0])
            || segments.Any(s => s is "" or "." or ".." || s.Contains("%2e", StringComparison.OrdinalIgnoreCase)))
        {
            return NotFound();
        }

        var client = http.CreateClient(HttpClientName);
        if (client.BaseAddress is null)
        {
            throw ApiException.ServiceUnavailable("Yapay zeka ağ geçidi (llmr) yapılandırılmamış.");
        }

        using var request = new HttpRequestMessage(new HttpMethod(Request.Method), $"manage/{raw}{Request.QueryString}");
        if (Request.ContentLength > 0 || Request.Headers.TransferEncoding.Count > 0)
        {
            request.Content = new StreamContent(Request.Body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw ApiException.ServiceUnavailable("Yapay zeka ağ geçidine (llmr) ulaşılamadı.");
        }

        using (response)
        {
            // The gateway's answer as it is: its status, its JSON, its error envelope.
            Response.StatusCode = (int)response.StatusCode;
            Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            await response.Content.CopyToAsync(Response.Body, ct);
            return new EmptyResult();
        }
    }
}
