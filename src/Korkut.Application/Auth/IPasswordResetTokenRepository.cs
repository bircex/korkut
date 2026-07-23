using Korkut.Application.Common;
using Korkut.Domain.Auth;

namespace Korkut.Application.Auth;

/// <summary>Persistence port for password-reset tokens.</summary>
public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken>
{
    /// <summary>The unused, unexpired token with this hash; null if there is none.</summary>
    Task<PasswordResetToken?> FindUsableAsync(Guid officeId, string tokenHash, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Marks the token used, atomically. False when it was already consumed, so a replay or
    /// a concurrent reset loses the race instead of both succeeding.
    /// </summary>
    Task<bool> TryConsumeAsync(Guid officeId, Guid tokenId, DateTimeOffset usedAt, CancellationToken ct = default);
}
