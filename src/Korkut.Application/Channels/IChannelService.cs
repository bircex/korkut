using Korkut.Application.Channels.Requests;
using Korkut.Application.Channels.Responses;

namespace Korkut.Application.Channels;

/// <summary>
/// Messaging-channel use-cases: per-kind configuration (email / telegram / whatsapp) and
/// external-account pairing. Provider secrets are write-only — they go in encrypted and
/// come back only as a configured flag. An unknown kind is a bad request; unlinking a
/// channel that was never linked reads as not found.
/// </summary>
public interface IChannelService
{
    Task<EmailChannelResponse> GetEmailAsync(CancellationToken ct = default);

    Task<EmailChannelResponse> SaveEmailAsync(SaveEmailChannelRequest request, CancellationToken ct = default);

    Task<ChannelSavedResponse> SaveTelegramAsync(SaveTelegramChannelRequest request, CancellationToken ct = default);

    Task<ChannelSavedResponse> SaveWhatsAppAsync(SaveWhatsAppChannelRequest request, CancellationToken ct = default);

    Task<IntegrationsResponse> ListIntegrationsAsync(CancellationToken ct = default);

    /// <summary>Mints and stores a fresh one-time pairing code for a kind.</summary>
    Task<LinkCodeResponse> CreateLinkAsync(string kind, CancellationToken ct = default);

    Task DeleteLinkAsync(string kind, CancellationToken ct = default);
}
