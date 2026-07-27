namespace Korkut.Channels;

/// <summary>
/// How a secret field on a channel should change: keep the stored value, clear it, or
/// set a new value. Mirrors the UI contract where the secret is <c>null</c> to keep,
/// <c>""</c> to clear, or a value to replace.
/// </summary>
public readonly record struct SecretUpdate(bool Keep, bool Clear, string? NewValue)
{
    public static SecretUpdate From(string? raw) => raw switch
    {
        null => new SecretUpdate(true, false, null),
        "" => new SecretUpdate(false, true, null),
        _ => new SecretUpdate(false, false, raw),
    };

    /// <summary>
    /// The same intent carrying a transformed value — used to hand the aggregate a secret
    /// that has already been encrypted, so plaintext never reaches the entity.
    /// </summary>
    public SecretUpdate Map(Func<string, string> transform) =>
        NewValue is null ? this : this with { NewValue = transform(NewValue) };
}
