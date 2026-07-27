using System.Security.Cryptography;
using System.Text;
using Korkut.Contract;
using Korkut.Contract.Channels;
using Korkut.Core;

namespace Korkut.Channels;

/// <summary>
/// Channel configuration over the repository port. Secrets are encrypted here, on the way in, so
/// the aggregate and the database only ever hold ciphertext; the pairing code is minted from a
/// cryptographic RNG. Every call RETURNS a <see cref="Result"/> (unknown kind → BadRequest).
/// </summary>
public sealed class ChannelService(
    IChannelRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ISecretEncryptor encryptor,
    IClock clock) : IChannelService
{
    public Task<Result<EmailChannelResponse>> GetEmailAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<EmailChannelResponse>.Ok(ToEmailResponse(await repository.FindAsync(office, ChannelKind.Email, ct), CanEdit)), ct);

    public Task<Result<EmailChannelResponse>> SaveEmailAsync(SaveEmailChannelRequest request, CancellationToken ct = default)
        => SaveAsync(ChannelKind.Email, request.AppPassword, channel =>
            channel.ConfigureEmail(request.Enabled, request.FromAddress, request.FromName, clock.UtcNow),
            channel => ToEmailResponse(channel, canEdit: true), ct);

    public Task<Result<ChannelSavedResponse>> SaveTelegramAsync(SaveTelegramChannelRequest request, CancellationToken ct = default)
        => SaveAsync(ChannelKind.Telegram, request.BotToken, channel =>
            channel.ConfigureTelegram(request.Enabled, clock.UtcNow),
            channel => new ChannelSavedResponse(true, channel.HasSecret), ct);

    public Task<Result<ChannelSavedResponse>> SaveWhatsAppAsync(SaveWhatsAppChannelRequest request, CancellationToken ct = default)
        => SaveAsync(ChannelKind.WhatsApp, request.Token, channel =>
            channel.ConfigureWhatsApp(request.Enabled, request.BaseUrl, clock.UtcNow),
            channel => new ChannelSavedResponse(true, channel.HasSecret), ct);

    public Task<Result<IntegrationsResponse>> ListIntegrationsAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var canEdit = CanEdit;
            var configured = (await repository.ListAsync(office, ct)).ToDictionary(c => c.Kind);
            var rows = ChannelKind.All.Select(kind =>
            {
                configured.TryGetValue(kind, out var channel);
                return new IntegrationChannelResponse(
                    kind, channel?.Enabled ?? false, channel?.HasSecret ?? false, channel?.BaseUrl ?? "",
                    channel?.Linked ?? false, channel?.LinkedId ?? "", canEdit);
            }).ToList();
            return Result<IntegrationsResponse>.Ok(new IntegrationsResponse(rows));
        }, ct);

    public Task<Result<LinkCodeResponse>> CreateLinkAsync(string kind, CancellationToken ct = default)
    {
        var normalized = ChannelKind.Normalize(kind);
        if (normalized is null)
        {
            return Task.FromResult(Result<LinkCodeResponse>.BadRequest("Geçersiz kanal türü."));
        }

        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)); // 8 hex chars
        return InScopeAsync(async office =>
        {
            var channel = await FindOrAddAsync(office, normalized, ct);
            channel.StartLink(code, clock.UtcNow);
            await repository.SaveChangesAsync(ct);
            return Result<LinkCodeResponse>.Ok(new LinkCodeResponse(code, normalized));
        }, ct);
    }

    public Task<Result> DeleteLinkAsync(string kind, CancellationToken ct = default)
    {
        var normalized = ChannelKind.Normalize(kind);
        if (normalized is null)
        {
            return Task.FromResult((Result)Result.BadRequest("Geçersiz kanal türü."));
        }

        return InScopeAsync(async office =>
        {
            var channel = await repository.FindForUpdateAsync(office, normalized, ct);
            if (channel is null)
            {
                return Result.NotFound("Bağlantı bulunamadı.");
            }

            channel.Unlink(clock.UtcNow);
            await repository.SaveChangesAsync(ct);
            return Result.Ok();
        }, ct);
    }

    private Task<Result<TResponse>> SaveAsync<TResponse>(
        string kind, string? rawSecret, Action<Channel> configure, Func<Channel, TResponse> toResponse, CancellationToken ct)
        => InScopeAsync(async office =>
        {
            var channel = await FindOrAddAsync(office, kind, ct);
            configure(channel);
            channel.ApplyEncryptedSecret(SecretUpdate.From(rawSecret).Map(Encrypt));
            await repository.SaveChangesAsync(ct);
            return Result<TResponse>.Ok(toResponse(channel));
        }, ct);

    /// <summary>An office has at most one channel per kind; a first save creates it.</summary>
    private async Task<Channel> FindOrAddAsync(Guid officeId, string kind, CancellationToken ct)
    {
        var channel = await repository.FindForUpdateAsync(officeId, kind, ct);
        if (channel is null)
        {
            channel = new Channel(officeId, kind);
            await repository.AddAsync(channel, ct);
        }

        return channel;
    }

    private string Encrypt(string plaintext) => encryptor.Encrypt(Encoding.UTF8.GetBytes(plaintext));

    private bool CanEdit => currentUser.Permissions.Contains(Permissions.OfficeSettings);

    private static EmailChannelResponse ToEmailResponse(Channel? channel, bool canEdit) =>
        new(ChannelKind.Email, channel?.Enabled ?? false, channel?.FromAddress ?? "",
            channel?.FromName ?? "", channel?.HasSecret ?? false, canEdit);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
