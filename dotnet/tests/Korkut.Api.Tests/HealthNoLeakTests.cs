using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Korkut.Api.Tests;

/// <summary>
/// Health endpoints must expose only an overall status — never check names,
/// descriptions, exception text, durations, or config (KDM-007).
/// </summary>
public class HealthNoLeakTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_exposes_only_status(string path)
    {
        var body = await factory.CreateClient().GetStringAsync(path);

        using var doc = JsonDocument.Parse(body);
        // Exactly one property, "status", with a plain enum name.
        var props = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(["status"], props);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());

        // No leakage of internal detail keywords.
        foreach (var forbidden in new[] { "exception", "stack", "description", "duration", "ConnectionString", "Npgsql" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
