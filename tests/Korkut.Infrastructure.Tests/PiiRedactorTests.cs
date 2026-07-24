using Korkut.Application.Pii;
using Korkut.Infrastructure.Pii;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// The Turkish PII redactor — pure, deterministic. Covers detection of each kind, the TCKN
/// checksum guard (no false positives on random 11-digit numbers), the prefix/separator rule that
/// keeps bare digit runs from masquerading as phones, round-trip restore, and scope-shared
/// placeholders across multiple masked texts.
/// </summary>
public class PiiRedactorTests
{
    private readonly PiiRedactor _redactor = new();

    // 10000000146 is a canonical checksum-valid test TCKN; 12345678901 fails the checksum.
    private const string ValidTckn = "10000000146";
    private const string InvalidTckn = "12345678901";

    [Fact]
    public void Detects_email_iban_tckn_and_phone()
    {
        var text = $"Müvekkil ahmet@ornek.com, TCKN {ValidTckn}, IBAN TR33 0006 1005 1978 6457 8413 26, tel +90 532 123 45 67.";
        var result = _redactor.Redact(text);

        Assert.DoesNotContain("ahmet@ornek.com", result.Text);
        Assert.DoesNotContain(ValidTckn, result.Text);
        Assert.DoesNotContain("532 123 45 67", result.Text);
        Assert.DoesNotContain("6457 8413", result.Text);

        Assert.Contains(result.Matches, m => m.Kind == PiiKind.Email);
        Assert.Contains(result.Matches, m => m.Kind == PiiKind.Iban);
        Assert.Contains(result.Matches, m => m.Kind == PiiKind.Tckn);
        Assert.Contains(result.Matches, m => m.Kind == PiiKind.Phone);
    }

    [Fact]
    public void Restore_round_trips_to_the_original()
    {
        var text = $"Sayın ahmet@ornek.com ({ValidTckn}), IBAN TR330006100519786457841326, GSM 0532 123 45 67.";
        var result = _redactor.Redact(text);
        Assert.NotEqual(text, result.Text);

        var scope = _redactor.NewScope();
        scope.Mask(text);
        Assert.Equal(text, scope.Restore(scope.Mask(text)));
    }

    [Fact]
    public void Invalid_tckn_is_not_redacted()
    {
        var result = _redactor.Redact($"Dosya numarası {InvalidTckn} olarak kaydedildi.");
        Assert.Contains(InvalidTckn, result.Text);
        Assert.DoesNotContain(result.Matches, m => m.Kind == PiiKind.Tckn);
    }

    [Theory]
    [InlineData("+90 532 123 45 67")]
    [InlineData("0532 123 45 67")]
    [InlineData("532-123-45-67")]
    [InlineData("0 (212) 555 12 34")]
    public void Phone_shapes_are_detected(string phone)
    {
        var result = _redactor.Redact($"Telefon: {phone}");
        Assert.Contains(result.Matches, m => m.Kind == PiiKind.Phone);
    }

    [Fact]
    public void A_bare_ten_digit_run_is_not_a_phone()
    {
        // No +90/0 prefix and no grouping separators → a case/reference number, not a phone.
        var result = _redactor.Redact("Referans no 5321234567 ile işlem yapıldı.");
        Assert.DoesNotContain(result.Matches, m => m.Kind == PiiKind.Phone);
        Assert.Contains("5321234567", result.Text);
    }

    [Fact]
    public void Text_without_pii_is_returned_unchanged()
    {
        const string clean = "Kira sözleşmesinin feshine ilişkin ihtarname hazırlanacaktır.";
        var result = _redactor.Redact(clean);
        Assert.Equal(clean, result.Text);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void A_repeated_value_gets_one_stable_placeholder()
    {
        var result = _redactor.Redact("ahmet@ornek.com ve tekrar ahmet@ornek.com");
        var emails = result.Matches.Where(m => m.Kind == PiiKind.Email).ToList();
        Assert.Single(emails);
        // Both occurrences masked to the same token.
        Assert.Equal(2, CountOccurrences(result.Text, emails[0].Placeholder));
    }

    [Fact]
    public void A_scope_shares_placeholders_across_texts_so_a_reply_restores()
    {
        var scope = _redactor.NewScope();
        var maskedSystem = scope.Mask("Büro e-postası info@buro.com.");
        var maskedMessage = scope.Mask("Cevabı info@buro.com adresine gönder.");

        // The same value → the same placeholder in both texts.
        var placeholder = scope.Matches.Single(m => m.Kind == PiiKind.Email).Placeholder;
        Assert.Contains(placeholder, maskedSystem);
        Assert.Contains(placeholder, maskedMessage);

        // A model "reply" echoing the placeholder restores to the real value.
        Assert.Equal("Gönderildi: info@buro.com", scope.Restore($"Gönderildi: {placeholder}"));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
