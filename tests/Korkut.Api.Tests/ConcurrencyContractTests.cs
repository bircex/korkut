using System.Text.Json;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Korkut.Api.Tests;

/// <summary>
/// A DbUpdateConcurrencyException must surface to the client as a 409 with the
/// { detail } contract — never a silent overwrite (KDM-014).
/// </summary>
public class ConcurrencyContractTests
{
    [Fact]
    public async Task Concurrency_exception_maps_to_409_detail()
    {
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(ctx, new DbUpdateConcurrencyException(), default);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, ctx.Response.StatusCode);

        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        using var doc = JsonDocument.Parse(await new StreamReader(ctx.Response.Body).ReadToEndAsync());
        Assert.True(doc.RootElement.TryGetProperty("detail", out var detail));
        Assert.False(string.IsNullOrWhiteSpace(detail.GetString()));
    }
}
