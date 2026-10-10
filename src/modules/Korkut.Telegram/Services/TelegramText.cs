namespace Korkut.Telegram.Services;

/// <summary>Cuts a reply into messages Telegram accepts (at most 4096 characters each).</summary>
internal static class TelegramText
{
    public const int MaxLength = 4096;

    /// <summary>
    /// Splits at the last paragraph break, else line break, else space that fits, so a long
    /// answer reads as consecutive messages rather than words cut in half.
    /// </summary>
    public static IReadOnlyList<string> Split(string text, int maxLength = MaxLength)
    {
        var parts = new List<string>();
        var rest = text.Trim();
        while (rest.Length > maxLength)
        {
            var cut = Boundary(rest, maxLength);
            parts.Add(rest[..cut].TrimEnd());
            rest = rest[cut..].TrimStart();
        }

        if (rest.Length > 0)
        {
            parts.Add(rest);
        }

        return parts;
    }

    private static int Boundary(string text, int maxLength)
    {
        var window = text[..maxLength];
        foreach (var separator in new[] { "\n\n", "\n", " " })
        {
            var at = window.LastIndexOf(separator, StringComparison.Ordinal);
            if (at > maxLength / 2)
            {
                return at + separator.Length;
            }
        }

        // No break in the second half: cut hard, but never between the two halves of a surrogate pair.
        return char.IsHighSurrogate(text[maxLength - 1]) ? maxLength - 1 : maxLength;
    }
}
