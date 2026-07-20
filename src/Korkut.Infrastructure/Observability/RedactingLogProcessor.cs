using Korkut.SharedKernel.Redaction;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Korkut.Infrastructure.Observability;

/// <summary>
/// OpenTelemetry log processor that scrubs every emitted record — formatted
/// message, body and string attribute values — through
/// <see cref="SensitiveDataRedactor"/>, so secrets/PII can never reach any log
/// exporter (OTLP, console, or the in-memory test exporter). Runs last in the
/// pipeline, before export.
/// </summary>
public sealed class RedactingLogProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        if (data.FormattedMessage is not null)
        {
            data.FormattedMessage = SensitiveDataRedactor.Redact(data.FormattedMessage);
        }

        if (data.Body is not null)
        {
            data.Body = SensitiveDataRedactor.Redact(data.Body);
        }

        if (data.Attributes is { Count: > 0 })
        {
            var redacted = new List<KeyValuePair<string, object?>>(data.Attributes.Count);
            foreach (var attribute in data.Attributes)
            {
                redacted.Add(attribute.Value is string s
                    ? new KeyValuePair<string, object?>(attribute.Key, SensitiveDataRedactor.Redact(s))
                    : attribute);
            }

            data.Attributes = redacted;
        }

        base.OnEnd(data);
    }
}
