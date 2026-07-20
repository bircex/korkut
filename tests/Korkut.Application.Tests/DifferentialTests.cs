using System.Text.Json.Nodes;
using Korkut.Application.Differential;

namespace Korkut.Application.Tests;

/// <summary>
/// Cross-runtime differential harness (KDM-034): normalization, mismatch severity,
/// cutover/rollback policy, and PII-safe data-parity fingerprints.
/// </summary>
public class DifferentialTests
{
    // --- Normalization (parity with tests/compat/normalize.py) ---

    [Fact]
    public void Volatile_values_are_masked_to_type_tokens_not_dropped()
    {
        var body = JsonNode.Parse("""
            {
              "id": "0193e5a1-7b3c-7c8e-9f21-2b1c9a0d4e55",
              "created_at": "2026-07-19T10:00:00Z",
              "reference": "0193e5a1-7b3c-7c8e-9f21-2b1c9a0d4e55",
              "name": "Dava X",
              "count": 3
            }
            """);
        var norm = ResponseNormalizer.NormalizeBody(body)!.AsObject();

        // Volatile KEYS (id, created_at) → plain type token (a shape change still diffs;
        // a value change does not), exactly like the Python normalizer.
        Assert.Equal("<str>", (string?)norm["id"]);
        Assert.Equal("<str>", (string?)norm["created_at"]);
        // A volatile-LOOKING value under a non-volatile key → scalar mask (still no raw value).
        Assert.Equal("<uuid>", (string?)norm["reference"]);
        // Non-volatile values preserved.
        Assert.Equal("Dava X", (string?)norm["name"]);
        Assert.Equal(3, (int?)norm["count"]);
    }

    [Fact]
    public void Normalization_is_stable_regardless_of_key_order()
    {
        var a = ResponseNormalizer.NormalizeBody(JsonNode.Parse("""{"b":1,"a":2}"""));
        var b = ResponseNormalizer.NormalizeBody(JsonNode.Parse("""{"a":2,"b":1}"""));
        Assert.Equal(a!.ToJsonString(), b!.ToJsonString());
    }

    [Fact]
    public void A_masked_response_contains_no_raw_id_or_token_values()
    {
        var raw = """{"session":"secret-cookie-abc","user_id":"0193e5a1-7b3c-7c8e-9f21-2b1c9a0d4e55"}""";
        var norm = ResponseNormalizer.NormalizeBody(JsonNode.Parse(raw))!.ToJsonString();
        Assert.DoesNotContain("secret-cookie-abc", norm, StringComparison.Ordinal);
        Assert.DoesNotContain("0193e5a1", norm, StringComparison.Ordinal);
    }

    // --- Comparison + severity ---

    [Fact]
    public void Identical_normalized_responses_have_no_mismatch()
    {
        var left = ResponseNormalizer.NormalizeResult(200, new Dictionary<string, string>(), JsonNode.Parse("""{"ok":true,"items":[1,2]}"""));
        var right = ResponseNormalizer.NormalizeResult(200, new Dictionary<string, string>(), JsonNode.Parse("""{"items":[1,2],"ok":true}"""));
        Assert.Empty(DifferentialComparator.Compare(left, right));
    }

    [Fact]
    public void Type_field_length_and_value_differences_are_high_severity()
    {
        var left = JsonNode.Parse("""{"status":"open","meta":{"k":1},"tags":["a","b"]}""");
        var typeChange = JsonNode.Parse("""{"status":"open","meta":[1],"tags":["a","b"]}""");     // object → array
        var missing = JsonNode.Parse("""{"status":"open","tags":["a","b"]}""");                    // field dropped
        var valueChange = JsonNode.Parse("""{"status":"closed","meta":{"k":1},"tags":["a","b"]}"""); // scalar value
        var lengthChange = JsonNode.Parse("""{"status":"open","meta":{"k":1},"tags":["a"]}""");    // array length

        Assert.Contains(DifferentialComparator.Compare(left, typeChange), m => m.Kind == "type" && m.Severity == MismatchSeverity.High);
        Assert.Contains(DifferentialComparator.Compare(left, missing), m => m.Kind == "field" && m.Severity == MismatchSeverity.High);
        Assert.Contains(DifferentialComparator.Compare(left, valueChange), m => m.Kind == "value" && m.Severity == MismatchSeverity.High);
        Assert.Contains(DifferentialComparator.Compare(left, lengthChange), m => m.Kind == "length" && m.Severity == MismatchSeverity.High);
    }

    [Fact]
    public void A_set_like_reorder_is_low_severity_only()
    {
        var left = JsonNode.Parse("""{"tags":["a","b","c"]}""");
        var reordered = JsonNode.Parse("""{"tags":["c","a","b"]}""");
        var diffs = DifferentialComparator.Compare(left, reordered);
        Assert.All(diffs, m => Assert.Equal(MismatchSeverity.Low, m.Severity));
        Assert.True(DifferentialComparator.NoHighSeverity(diffs));
    }

    // --- Rollout / rollback policy ---

    [Fact]
    public void Cutover_is_blocked_by_any_high_severity_mismatch()
    {
        var high = new[] { new Mismatch("$.n", "type", MismatchSeverity.High, "<int>", "<str>") };
        var low = new[] { new Mismatch("$.tags", "order", MismatchSeverity.Low, "reordered", "reordered") };

        var tHigh = RolloutPolicy.Summarize(high);
        Assert.False(RolloutPolicy.CanCutOver(tHigh));
        Assert.True(RolloutPolicy.MustRollback(tHigh));
        Assert.Equal(1, tHigh.High);

        var tLow = RolloutPolicy.Summarize(low);
        Assert.True(RolloutPolicy.CanCutOver(tLow));
        Assert.False(RolloutPolicy.MustRollback(tLow));
    }

    // --- Data parity (PII-safe) ---

    private static Dictionary<string, string?> Row(string id, string name, string status) =>
        new() { ["id"] = id, ["client_name"] = name, ["status"] = status };

    [Fact]
    public void Identical_data_produces_identical_fingerprints_regardless_of_row_order()
    {
        var pii = new HashSet<string> { "client_name" };
        var a = DataParity.Fingerprint("matters", [Row("1", "Ahmet Yılmaz", "open"), Row("2", "Ayşe Kaya", "closed")], pii);
        var b = DataParity.Fingerprint("matters", [Row("2", "Ayşe Kaya", "closed"), Row("1", "Ahmet Yılmaz", "open")], pii);
        Assert.Equal(a.Checksum, b.Checksum);
        Assert.Equal(2, a.RowCount);
        Assert.Empty(DataParity.Diff([a], [b]));
    }

    [Fact]
    public void A_changed_row_is_detected_as_a_parity_diff()
    {
        var pii = new HashSet<string> { "client_name" };
        var a = DataParity.Fingerprint("matters", [Row("1", "Ahmet Yılmaz", "open")], pii);
        var b = DataParity.Fingerprint("matters", [Row("1", "Ahmet Yılmaz", "closed")], pii);
        Assert.NotEmpty(DataParity.Diff([a], [b]));
    }

    [Fact]
    public void Pii_never_appears_in_the_fingerprint_and_still_matches_when_equal()
    {
        var pii = new HashSet<string> { "client_name" };
        var fp = DataParity.Fingerprint("matters", [Row("1", "Ahmet Yılmaz", "open")], pii);
        // The checksum is a hash; the PII value cannot be present.
        Assert.DoesNotContain("Ahmet", fp.Checksum, StringComparison.OrdinalIgnoreCase);
        // Equal PII → equal fingerprint (parity preserved through pseudonymization).
        var same = DataParity.Fingerprint("matters", [Row("1", "Ahmet Yılmaz", "open")], pii);
        Assert.Equal(fp.Checksum, same.Checksum);
    }
}
