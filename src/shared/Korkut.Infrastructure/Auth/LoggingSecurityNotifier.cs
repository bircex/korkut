using Korkut.Application.Auth;
using Microsoft.Extensions.Logging;

namespace Korkut.Infrastructure.Auth;

/// <summary>Structured-log security notifier: it records that a password changed, never what to.</summary>
public sealed partial class LoggingSecurityNotifier(ILogger<LoggingSecurityNotifier> logger) : ISecurityNotifier
{
    public Task NotifyPasswordChangedAsync(Guid officeId, string email, CancellationToken ct = default)
    {
        LogPasswordChanged(logger, officeId);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Password changed for a user in office {OfficeId}")]
    private static partial void LogPasswordChanged(ILogger logger, Guid officeId);
}
