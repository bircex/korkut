using Korkut.Core;

namespace Korkut.Core.Tests;

/// <summary>
/// Shared boundary sanitizer used by every text-write path fed by untrusted upload/OCR/LLM
/// content (document extraction, chat messages/titles, draft documents, Knowledge ingestion,
/// OCR output) to prevent Postgres error 22021 (invalid byte sequence for encoding UTF8: 0x00).
/// </summary>
public class PostgresTextSanitizerTests
{
    [Fact]
    public void Strips_embedded_nul_bytes()
    {
        var result = PostgresTextSanitizer.StripInvalidChars("once\0upon\0a time");

        Assert.DoesNotContain('\0', result);
        Assert.Equal("onceupona time", result);
    }

    [Fact]
    public void Strips_other_control_bytes_that_are_not_nul()
    {
        // A stray C0 control byte from a binary misread, not NUL specifically, but the
        // same class of bug.
        var bel = (char)7;
        var input = "a" + bel + "bc";

        var result = PostgresTextSanitizer.StripInvalidChars(input);

        Assert.Equal("abc", result);
    }

    [Fact]
    public void Keeps_newlines_carriage_returns_and_tabs()
    {
        var result = PostgresTextSanitizer.StripInvalidChars("line1\nline2\r\nindented\ttext");

        Assert.Equal("line1\nline2\r\nindented\ttext", result);
    }

    [Fact]
    public void Clean_text_is_returned_unchanged()
    {
        const string clean = "Vekaletname ornegi, madde 1.";

        var result = PostgresTextSanitizer.StripInvalidChars(clean);

        Assert.Same(clean, result); // no allocation when nothing needed stripping
    }

    [Fact]
    public void Empty_string_returns_empty_string()
    {
        Assert.Equal(string.Empty, PostgresTextSanitizer.StripInvalidChars(string.Empty));
    }

    [Fact]
    public void All_control_characters_stripped_leaves_empty_string()
    {
        Assert.Equal(string.Empty, PostgresTextSanitizer.StripInvalidChars("\0"));
    }

    [Fact]
    public void Handles_text_longer_than_the_stack_allocation_threshold()
    {
        // Exercises the heap-allocated fallback path (text.Length > 4096).
        var large = string.Concat(Enumerable.Repeat("legal text ", 500)) + "\0tail";

        var result = PostgresTextSanitizer.StripInvalidChars(large);

        Assert.True(large.Length > 4096);
        Assert.DoesNotContain('\0', result);
        Assert.EndsWith("tail", result);
    }
}
