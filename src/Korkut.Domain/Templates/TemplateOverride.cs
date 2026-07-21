namespace Korkut.Domain.Templates;

/// <summary>The two layers at which a built-in template can be overridden.</summary>
public static class TemplateScope
{
    public const string Office = "office";
    public const string User = "user";
}

/// <summary>
/// A per-office/user override of a built-in document template (domain model). The built-in
/// defaults live in the static catalog; an override takes precedence over them.
/// </summary>
public sealed record TemplateOverride(string Scope, Guid UserId, string Kind, string TypeId, string Content);
