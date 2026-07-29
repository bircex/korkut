namespace Korkut.Contract.Pii;

/// <summary>
/// A redaction scope with a shared placeholder map. Mask any number of texts; then restore a
/// reply (e.g. the model's output) by putting the original values back in place of their
/// placeholders. Not thread-safe — one scope per logical unit of work (e.g. one chat turn).
/// </summary>
public interface IPiiRedactionScope
{
    /// <summary>Mask <paramref name="text"/>, reusing placeholders already assigned in this scope.</summary>
    string Mask(string text);

    /// <summary>Replace this scope's placeholders in <paramref name="text"/> with their original values.</summary>
    string Restore(string text);

    /// <summary>Every match assigned in this scope so far.</summary>
    IReadOnlyCollection<PiiMatch> Matches { get; }
}
