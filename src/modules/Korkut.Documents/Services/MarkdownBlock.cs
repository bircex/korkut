namespace Korkut.Documents.Services;

public enum BlockKind
{
    Paragraph,
    Heading,
    Bullet,
    Table,
}

/// <summary>
/// A coarse markdown block for export. The parser covers the structure the Korkut legal
/// templates use: ATX headings, paragraphs, bullet lists, and pipe tables. Inline bold
/// (<c>**…**</c>) is preserved for the run splitter; other inline markup is left as-is.
/// </summary>
public sealed record MarkdownBlock(BlockKind Kind, string Text, int Level = 0, IReadOnlyList<IReadOnlyList<string>>? Rows = null)
{
    public static IReadOnlyList<MarkdownBlock> Parse(string markdown)
    {
        var blocks = new List<MarkdownBlock>();
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var paragraph = new List<string>();
        var tableRows = new List<IReadOnlyList<string>>();

        void FlushParagraph()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new MarkdownBlock(BlockKind.Paragraph, string.Join(" ", paragraph)));
                paragraph.Clear();
            }
        }

        void FlushTable()
        {
            if (tableRows.Count > 0)
            {
                blocks.Add(new MarkdownBlock(BlockKind.Table, string.Empty, Rows: [.. tableRows]));
                tableRows.Clear();
            }
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();

            if (IsTableRow(trimmed))
            {
                FlushParagraph();
                if (!IsTableSeparator(trimmed))
                {
                    tableRows.Add(SplitRow(trimmed));
                }

                continue;
            }

            FlushTable();

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
            }
            else if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushParagraph();
                blocks.Add(new MarkdownBlock(BlockKind.Heading, trimmed[4..].Trim(), 3));
            }
            else if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushParagraph();
                blocks.Add(new MarkdownBlock(BlockKind.Heading, trimmed[3..].Trim(), 2));
            }
            else if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                FlushParagraph();
                blocks.Add(new MarkdownBlock(BlockKind.Heading, trimmed[2..].Trim(), 1));
            }
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushParagraph();
                blocks.Add(new MarkdownBlock(BlockKind.Bullet, trimmed[2..].Trim()));
            }
            else
            {
                paragraph.Add(trimmed);
            }
        }

        FlushParagraph();
        FlushTable();
        return blocks;
    }

    private static bool IsTableRow(string line) => line.StartsWith('|') && line.Contains('|');

    private static bool IsTableSeparator(string line) => line.Replace("|", "").Replace("-", "").Replace(":", "").Trim().Length == 0
        && line.Contains('-');

    private static List<string> SplitRow(string line) =>
        line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();
}

/// <summary>A bold/plain inline run, produced by splitting on markdown <c>**bold**</c> spans.</summary>
public readonly record struct InlineRun(string Text, bool Bold)
{
    public static IReadOnlyList<InlineRun> Split(string text)
    {
        var runs = new List<InlineRun>();
        var i = 0;
        while (i < text.Length)
        {
            var open = text.IndexOf("**", i, StringComparison.Ordinal);
            if (open < 0)
            {
                runs.Add(new InlineRun(text[i..], false));
                break;
            }

            if (open > i)
            {
                runs.Add(new InlineRun(text[i..open], false));
            }

            var close = text.IndexOf("**", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                runs.Add(new InlineRun(text[open..], false));
                break;
            }

            runs.Add(new InlineRun(text[(open + 2)..close], true));
            i = close + 2;
        }

        return runs.Count == 0 ? [new InlineRun(text, false)] : runs;
    }
}
