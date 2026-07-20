using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;

namespace Korkut.Api.Endpoints;

// --- Email ---
public sealed record EmailChannelResponse(string Kind, bool Enabled, string FromAddress, string FromName, bool HasPassword, bool CanEdit);

public sealed record EmailChannelInput
{
    public bool Enabled { get; init; }

    [MaxLength(320)]
    public string FromAddress { get; init; } = string.Empty;

    [MaxLength(200)]
    public string FromName { get; init; } = string.Empty;

    /// <summary>null → keep stored password; "" → clear; value → replace.</summary>
    public string? AppPassword { get; init; }
}

public sealed record TelegramChannelInput
{
    public bool Enabled { get; init; }

    /// <summary>null → keep; "" → clear; value → replace.</summary>
    public string? BotToken { get; init; }
}

public sealed record WhatsAppChannelInput
{
    public bool Enabled { get; init; }

    [MaxLength(400)]
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>null → keep; "" → clear; value → replace.</summary>
    public string? Token { get; init; }
}

public sealed record OkConfiguredResponse(bool Ok, bool Configured);

// --- Integrations ---
public sealed record IntegrationChannelResponse(
    string Kind, bool Enabled, bool Configured, string BaseUrl, bool Linked, string LinkedId, bool CanEdit);

public sealed record IntegrationsResponse(IReadOnlyList<IntegrationChannelResponse> Channels);

public sealed record LinkCodeResponse(string Code, string Kind);

/// <summary>
/// Messaging-channel configuration + external-account linking. Authenticated, tenant-
/// scoped (RLS). All writes require office.settings. Provider secrets are write-only —
/// stored encrypted and surfaced only as a <c>has_password</c>/<c>configured</c> flag.
/// </summary>
public static class ChannelEndpoints
{
    public static IEndpointRouteBuilder MapChannelEndpoints(this IEndpointRouteBuilder app)
    {
        MapChannels(app);
        MapIntegrations(app);
        return app;
    }

    private static void MapChannels(IEndpointRouteBuilder app)
    {
        var channels = app.MapGroup("/api/channels");

        channels.MapGet("/email", async (ClaimsPrincipal user, KorkutDbContext db, ChannelStore store, CancellationToken ct) =>
            {
                var canEdit = user.HasPermission(Permissions.OfficeSettings);
                var c = await user.InTenantScopeAsync(db, o => store.GetAsync(o, ChannelStore.Email, ct), ct);
                return Results.Ok(new EmailChannelResponse("email", c?.Enabled ?? false, c?.FromAddress ?? "", c?.FromName ?? "", c?.EncryptedSecret != null, canEdit));
            })
            .RequireAuthorization(Permissions.MattersRead);

        channels.MapPut("/email", async (EmailChannelInput body, ClaimsPrincipal user, KorkutDbContext db, ChannelStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var c = await user.InTenantScopeAsync(db, o => store.SaveEmailAsync(o, body.Enabled, body.FromAddress, body.FromName, SecretUpdate.From(body.AppPassword), ct), ct);
                return Results.Ok(new EmailChannelResponse("email", c.Enabled, c.FromAddress ?? "", c.FromName ?? "", c.EncryptedSecret != null, true));
            })
            .RequireAuthorization(Permissions.OfficeSettings);

        channels.MapPut("/telegram", async (TelegramChannelInput body, ClaimsPrincipal user, KorkutDbContext db, ChannelStore store, CancellationToken ct) =>
            {
                var c = await user.InTenantScopeAsync(db, o => store.SaveTelegramAsync(o, body.Enabled, SecretUpdate.From(body.BotToken), ct), ct);
                return Results.Ok(new OkConfiguredResponse(true, c.EncryptedSecret != null));
            })
            .RequireAuthorization(Permissions.OfficeSettings);

        channels.MapPut("/whatsapp", async (WhatsAppChannelInput body, ClaimsPrincipal user, KorkutDbContext db, ChannelStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var c = await user.InTenantScopeAsync(db, o => store.SaveWhatsAppAsync(o, body.Enabled, body.BaseUrl, SecretUpdate.From(body.Token), ct), ct);
                return Results.Ok(new OkConfiguredResponse(true, c.EncryptedSecret != null));
            })
            .RequireAuthorization(Permissions.OfficeSettings);
    }

    private static void MapIntegrations(IEndpointRouteBuilder app)
    {
        var integrations = app.MapGroup("/api/integrations");

        integrations.MapGet("", async (ClaimsPrincipal user, KorkutDbContext db, ChannelStore store, CancellationToken ct) =>
            {
                var canEdit = user.HasPermission(Permissions.OfficeSettings);
                var rows = await user.InTenantScopeAsync(db, o => store.ListAsync(o, ct), ct);
                var byKind = rows.ToDictionary(r => r.Kind);
                var kinds = new[] { ChannelStore.Telegram, ChannelStore.WhatsApp, ChannelStore.Email };
                var channels = kinds.Select(k =>
                {
                    byKind.TryGetValue(k, out var c);
                    return new IntegrationChannelResponse(
                        k, c?.Enabled ?? false, c?.EncryptedSecret != null, c?.BaseUrl ?? "",
                        c?.Linked ?? false, c?.LinkedId ?? "", canEdit);
                }).ToList();
                return Results.Ok(new IntegrationsResponse(channels));
            })
            .RequireAuthorization(Permissions.MattersRead);

        integrations.MapPost("/{kind}/link", async (string kind, ClaimsPrincipal user, KorkutDbContext db, ChannelStore store, CancellationToken ct) =>
            {
                var normalized = Normalize(kind);
                var code = await user.InTenantScopeAsync(db, o => store.CreateLinkAsync(o, normalized, ct), ct);
                return Results.Ok(new LinkCodeResponse(code, normalized));
            })
            .RequireAuthorization(Permissions.OfficeSettings);

        integrations.MapDelete("/{kind}/link", async (string kind, ClaimsPrincipal user, KorkutDbContext db, ChannelStore store, CancellationToken ct) =>
            {
                var normalized = Normalize(kind);
                var ok = await user.InTenantScopeAsync(db, o => store.DeleteLinkAsync(o, normalized, ct), ct);
                return ok ? Results.NoContent() : throw ApiException.NotFound("Bağlantı bulunamadı.");
            })
            .RequireAuthorization(Permissions.OfficeSettings);
    }

    private static string Normalize(string kind) => kind.ToLowerInvariant() switch
    {
        ChannelStore.Telegram => ChannelStore.Telegram,
        ChannelStore.WhatsApp => ChannelStore.WhatsApp,
        ChannelStore.Email => ChannelStore.Email,
        _ => throw ApiException.BadRequest("Geçersiz kanal türü."),
    };
}
