namespace Korkut.Domain.Settings;

/// <summary>The office-wide AI persona (domain model): how the assistant should sound.</summary>
public sealed record Persona(string Tone, string Expertise, string ExtraRules)
{
    public static readonly Persona Empty = new(string.Empty, string.Empty, string.Empty);
}

/// <summary>The four drafting-style fields, used at every style layer.</summary>
public readonly record struct StyleFields(string Letterhead, string Signature, string Tone, string Boilerplate)
{
    public static readonly StyleFields Empty = new(string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>Overlay: this layer's non-empty fields win over <paramref name="baseline"/>.</summary>
    public StyleFields Over(StyleFields baseline) => new(
        string.IsNullOrWhiteSpace(Letterhead) ? baseline.Letterhead : Letterhead,
        string.IsNullOrWhiteSpace(Signature) ? baseline.Signature : Signature,
        string.IsNullOrWhiteSpace(Tone) ? baseline.Tone : Tone,
        string.IsNullOrWhiteSpace(Boilerplate) ? baseline.Boilerplate : Boilerplate);
}

/// <summary>A memory item with its author resolved to a display value (the user's email).</summary>
public sealed record MemoryItem(Guid Id, string Kind, string Text, string Status, string? Author, DateTimeOffset CreatedAt);
