using System.Security.Cryptography;
using System.Text;

namespace Korkut.Application.Differential;

/// <summary>Row count + content checksum for one table in a snapshot (KDM-034).</summary>
public sealed record TableFingerprint(string Table, int RowCount, string Checksum);

/// <summary>
/// Proves data-migration parity between two snapshots (Python-migrated vs
/// .NET-migrated) via row counts and per-table content checksums (KDM-034).
///
/// PII columns are pseudonymized to a stable HMAC token BEFORE anything is
/// serialized, so no raw PII/secret ever enters the checksum input or the emitted
/// artifact — yet identical logical data still yields identical checksums, which is
/// exactly what a parity proof needs (acceptance 3).
/// </summary>
public static class DataParity
{
    /// <summary>
    /// Fingerprint one table. <paramref name="rows"/> is column→value per row;
    /// <paramref name="piiColumns"/> are pseudonymized. Row order is not significant
    /// (rows are canonically sorted), so two runtimes that insert in a different order
    /// still match.
    /// </summary>
    public static TableFingerprint Fingerprint(string table, IEnumerable<IReadOnlyDictionary<string, string?>> rows, ISet<string> piiColumns)
    {
        var canonicalRows = rows
            .Select(r => CanonicalRow(r, piiColumns))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", canonicalRows)));
        return new TableFingerprint(table, canonicalRows.Count, Convert.ToHexString(digest).ToLowerInvariant());
    }

    /// <summary>Compare two snapshots; returns the tables whose count or checksum differ.</summary>
    public static IReadOnlyList<string> Diff(IEnumerable<TableFingerprint> a, IEnumerable<TableFingerprint> b)
    {
        var mapB = b.ToDictionary(x => x.Table, StringComparer.Ordinal);
        var diffs = new List<string>();
        foreach (var fa in a)
        {
            if (!mapB.TryGetValue(fa.Table, out var fb))
            {
                diffs.Add($"{fa.Table}: missing in second snapshot");
            }
            else if (fa.RowCount != fb.RowCount)
            {
                diffs.Add($"{fa.Table}: row count {fa.RowCount} vs {fb.RowCount}");
            }
            else if (fa.Checksum != fb.Checksum)
            {
                diffs.Add($"{fa.Table}: checksum differs");
            }
        }

        return diffs;
    }

    private static string CanonicalRow(IReadOnlyDictionary<string, string?> row, ISet<string> piiColumns)
    {
        var sb = new StringBuilder();
        foreach (var col in row.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var value = row[col];
            var emitted = value is null
                ? "\0null"
                : piiColumns.Contains(col)
                    ? Pseudonym(value) // raw PII never serialized
                    : value;
            sb.Append(col).Append('=').Append(emitted).Append(';');
        }

        return sb.ToString();
    }

    // Deterministic keyed pseudonym: equal inputs → equal tokens (parity preserved),
    // but the token is not reversible to the original value.
    private static readonly byte[] PseudonymKey = "korkut-parity-pseudonym-v1"u8.ToArray();

    private static string Pseudonym(string value)
    {
        var mac = HMACSHA256.HashData(PseudonymKey, Encoding.UTF8.GetBytes(value));
        return "pii:" + Convert.ToHexString(mac.AsSpan(0, 8)).ToLowerInvariant();
    }
}
