using Korkut.Infrastructure.Observability;
using Korkut.SharedKernel.Redaction;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace Korkut.Api.Tests;

/// <summary>
/// The KDM-007 canary test: a secret/PII value written to a log must never appear
/// in the exported telemetry. Wires the real <see cref="RedactingLogProcessor"/>
/// ahead of an in-memory exporter and asserts the canary is masked on export.
/// </summary>
public class LogRedactionTests
{
    [Fact]
    public void Canary_secret_is_redacted_before_export()
    {
        var exported = new List<LogRecord>();

        // Assemble canaries at runtime (never committed as literals).
        var apiKey = "sk-ant-" + new string('a', 28);
        var tckn = new string('1', 11);
        var email = "muvekkil" + "@example.test";

        using (var factory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.AddProcessor(new RedactingLogProcessor());
                options.AddInMemoryExporter(exported);
            });
        }))
        {
            var logger = factory.CreateLogger("canary");
            logger.LogInformation("calling provider with key {Key} for client {Tckn} <{Email}>", apiKey, tckn, email);
        }

        Assert.NotEmpty(exported);
        foreach (var record in exported)
        {
            var text = record.FormattedMessage ?? record.Body ?? string.Empty;
            var attrs = string.Join("|", record.Attributes?.Select(a => a.Value?.ToString()) ?? []);
            Assert.DoesNotContain(apiKey, text + attrs, StringComparison.Ordinal);
            Assert.DoesNotContain(tckn, text + attrs, StringComparison.Ordinal);
            Assert.DoesNotContain(email, text + attrs, StringComparison.Ordinal);
        }

        // Sanity: the redactor really would have flagged these (guards a broken canary).
        Assert.True(SensitiveDataRedactor.ContainsSensitive(apiKey));
    }
}
