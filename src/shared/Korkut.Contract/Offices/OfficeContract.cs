namespace Korkut.Contract.Offices;

/// <summary>An office as the platform-admin console lists it.</summary>
public sealed record OfficeResponse(Guid Id, string Name, bool Disabled, bool RequireTwoFactor, DateTimeOffset CreatedAt);

/// <summary>Platform-wide office counts for the admin console.</summary>
public sealed record OfficeStatsResponse(int Offices, int ActiveOffices, int DisabledOffices);

/// <summary>The ids of a newly provisioned (disabled) office and its owner.</summary>
public sealed record ProvisionedOffice(Guid OfficeId, Guid UserId);

/// <summary>
/// Platform-admin office management (cross-tenant by design; nothing here runs in a tenant scope).
/// Calls RETURN a <see cref="Result"/>.
/// </summary>
public interface IOfficeAdminService
{
    Task<Result<IReadOnlyList<OfficeResponse>>> ListAsync(CancellationToken ct = default);

    Task<Result> SetDisabledAsync(Guid officeId, bool disabled, CancellationToken ct = default);

    Task<Result<OfficeStatsResponse>> StatsAsync(CancellationToken ct = default);
}

/// <summary>
/// Creates a brand-new tenant — a disabled office plus its owner — for self-serve signup, before
/// any session/tenant context exists (through a narrow, auditable RLS bypass). Null when the e-mail
/// or Google subject already has an account.
/// </summary>
public interface IOfficeProvisioner
{
    Task<ProvisionedOffice?> ProvisionAsync(string email, string googleSub, string officeName, CancellationToken ct = default);

    /// <summary>Local (e-mail + password) self-serve signup: a disabled office + its owner, whose
    /// password hash is stored so they can sign in once a platform admin enables the office. Null
    /// when the e-mail already has an account.</summary>
    Task<ProvisionedOffice?> ProvisionLocalAsync(string email, string passwordHash, string officeName, string fullName, CancellationToken ct = default);
}

/// <summary>Admin-console stats in the SPA's shape (pending = disabled offices awaiting approval).</summary>
public sealed record AdminStatsResponse(int Offices, int Active, int Pending, int Users);

/// <summary>An office row as the admin console lists it (owner + member count + assigned model).</summary>
public sealed record AdminOfficeResponse(
    Guid Id, string Name, bool Disabled, string? OwnerEmail, int Members, DateTimeOffset? CreatedAt,
    AdminOfficeModelState Model);

/// <summary>An office that a catalog model is assigned to (the model → offices reverse view).</summary>
public sealed record AdminModelOfficeRef(Guid Id, string Name, bool Enabled);

/// <summary>A catalog model — an admin-editable row of the DB catalog (no API keys) — plus the
/// offices it is currently assigned to. <see cref="Provider"/> names the registered
/// IChatProviderService this entry routes to ("claude" | "deepseek" | "gemini" | "gpt" | "groq" |
/// "ollama"); an office assignment (or the platform default) resolves to a real chat-model
/// selection via this field (see Korkut.Contract.Agent.Model.IOfficeModelOverrideService).
/// <see cref="ProviderConfigured"/> reports whether that provider's key is present in the
/// environment — an unconfigured entry is listed but cannot serve traffic.</summary>
public sealed record AdminModelResponse(
    string Key, string Label, string Model, string BaseUrl, bool IsLocal, string Provider,
    IReadOnlyList<AdminModelOfficeRef> AssignedOffices, int? MaxTokens = null, bool Enabled = true,
    int SortOrder = 0, bool ProviderConfigured = true, bool IsPlatformDefault = false);

public sealed record AdminOfficeMemberResponse(
    Guid Id, string Email, string? Name, string Role, int Chats, int Messages, AdminUsageTotals Usage);

/// <summary>A user row as the admin console lists it — platform-wide, across every office.</summary>
public sealed record AdminUserResponse(
    Guid Id, string Email, string? Name, string Role,
    Guid OfficeId, string OfficeName, bool OfficeDisabled, DateTimeOffset CreatedAt);

/// <summary>
/// An office's model assignment as the console shows it. <see cref="Enabled"/> false (or a null
/// <see cref="ModelKey"/>) means the office is NOT pinned and follows the platform default —
/// <see cref="EffectiveModelKey"/> then names whatever it actually resolves to, so the console can
/// show the live answer rather than making the admin infer it.
/// </summary>
public sealed record AdminOfficeModelState(
    bool Enabled, string? ModelKey, string? Label,
    string? EffectiveModelKey = null, string? EffectiveLabel = null, string? EffectiveSource = null);

public sealed record AdminOfficeTotals(int Chats, int Messages, long Tokens, decimal Cost = 0m);

/// <summary>Office drill-down for the admin console.</summary>
public sealed record AdminOfficeDetailResponse(
    Guid Id, string Name, bool Disabled, DateTimeOffset? CreatedAt,
    AdminOfficeModelState Model, AdminOfficeTotals Totals, IReadOnlyList<AdminOfficeMemberResponse> Members);

/// <summary>
/// The admin console's data surface (cross-tenant; per-office reads open their own short tenant
/// scope). Model assignment is LIVE: what an admin pins here is what the chat router uses, ahead of
/// the platform default (see <see cref="IModelCatalogService"/>) and the env-driven specialty router.
/// </summary>
public interface IAdminConsoleService
{
    Task<Result<AdminStatsResponse>> StatsAsync(CancellationToken ct = default);

    Task<Result<IReadOnlyList<AdminOfficeResponse>>> OfficesAsync(CancellationToken ct = default);

    Task<Result<AdminOfficeDetailResponse>> OfficeDetailAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Every user, across every office — read-only (bkz. AdminUserResponse).</summary>
    Task<Result<IReadOnlyList<AdminUserResponse>>> UsersAsync(CancellationToken ct = default);

    Task<Result<IReadOnlyList<AdminModelResponse>>> ModelsAsync(CancellationToken ct = default);

    Task<Result<AdminOfficeDetailResponse>> AssignModelAsync(
        Guid officeId, string? modelKey, bool enabled, CancellationToken ct = default);
}
