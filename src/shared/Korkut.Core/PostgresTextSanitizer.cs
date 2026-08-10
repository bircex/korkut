namespace Korkut.Core;

/// <summary>
/// Strips bytes Postgres' "text" columns reject outright, or that are noise from a binary/OCR
/// misread. NUL (0x00) fails every insert/update with 22021 ("invalid byte sequence for
/// encoding UTF8: 0x00"); other C0 control characters are stripped too since real prose never
/// carries them — <c>\n</c>, <c>\r</c> and <c>\t</c> are the only ones kept. Apply this at every
/// DB write boundary that persists text derived from an upload, OCR pass, or LLM output — none
/// of those sources can be trusted not to carry one.
/// </summary>
public static class PostgresTextSanitizer
{
    public static string StripInvalidChars(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        Span<char> buffer = text.Length <= 4096 ? stackalloc char[text.Length] : new char[text.Length];
        var written = 0;
        foreach (var ch in text)
        {
            if (ch == '\0' || (char.IsControl(ch) && ch is not ('\n' or '\r' or '\t')))
            {
                continue;
            }

            buffer[written++] = ch;
        }

        return written == text.Length ? text : new string(buffer[..written]);
    }
}
