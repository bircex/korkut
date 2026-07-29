namespace Korkut.Contract.Channels;

/// <summary>
/// Messaging-channel use-cases: per-kind configuration (email / telegram / whatsapp) and
/// external-account pairing. Provider secrets are write-only — they go in encrypted and come back
/// only as a configured flag. Calls RETURN a <see cref="Result"/>: an unknown kind is BadRequest;
/// unlinking a channel that was never linked is NotFound.
/// </summary>
public interface IChannelService
{
    Task<Result<EmailChannelResponse>> GetEmailAsync(CancellationToken ct = default);

    Task<Result<EmailChannelResponse>> SaveEmailAsync(SaveEmailChannelRequest request, CancellationToken ct = default);

    Task<Result<ChannelSavedResponse>> SaveTelegramAsync(SaveTelegramChannelRequest request, CancellationToken ct = default);

    Task<Result<ChannelSavedResponse>> SaveWhatsAppAsync(SaveWhatsAppChannelRequest request, CancellationToken ct = default);

    Task<Result<IntegrationsResponse>> ListIntegrationsAsync(CancellationToken ct = default);

    /// <summary>Mints and stores a fresh one-time pairing code for a kind.</summary>
    Task<Result<LinkCodeResponse>> CreateLinkAsync(string kind, CancellationToken ct = default);

    Task<Result> DeleteLinkAsync(string kind, CancellationToken ct = default);
}
