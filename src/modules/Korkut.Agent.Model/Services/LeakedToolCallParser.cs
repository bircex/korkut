using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Korkut.Contract.Agent.Model;

namespace Korkut.Agent.Model.Services;

/// <summary>
/// Salvages tool calls that a model emitted as plain TEXT instead of structured tool_calls.
/// deepseek-chat, in particular, sometimes streams its tool call into the content channel using a
/// pseudo-markup block (e.g. <c>&lt;｜｜DSML｜｜tool_calls&gt; &lt;｜｜DSML｜｜invoke name="fetch_document"&gt;
/// &lt;｜｜DSML｜｜parameter name="source" string="true"&gt;yargitay&lt;/…&gt; …</c>) rather than the OpenAI
/// tool_calls field. Left alone that raw block reaches the user as garbage and the tool never runs.
/// This parser detects such a block, extracts the invoked tools and their parameters, and lets the
/// provider convert them into real <see cref="ToolInvocation"/>s (and strip them from visible text).
/// Format-tolerant on purpose: it keys off the invoke/parameter markers and ignores the surrounding
/// special-token noise, so a slightly different delimiter still parses.
/// </summary>
internal static class LeakedToolCallParser
{
    // A real leak carries an "invoke name=..." plus either the tool_calls wrapper or at least one
    // "parameter name=..."; that pair keeps ordinary prose containing the word "invoke" from matching.
    public static bool LooksLikeToolCall(string? text)
        => !string.IsNullOrEmpty(text)
           && text.Contains("invoke name=", StringComparison.Ordinal)
           && (text.Contains("tool_calls", StringComparison.Ordinal)
               || text.Contains("parameter name=", StringComparison.Ordinal));

    private static readonly Regex InvokeRx = new(
        """invoke\s+name="(?<name>[^"]+)"(?<body>.*?)(?:</[^>]*?invoke>|\z)""",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ParamRx = new(
        """parameter\s+name="(?<pname>[^"]+)"(?<attrs>[^>]*)>(?<val>.*?)</[^>]*?parameter>""",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // parameter blocks can carry string="true|false"; string="false" means the value is a raw scalar
    // (number/bool), matching the shape the model emitted.
    private static readonly Regex StringAttrRx = new("string=\"(?<v>true|false)\"", RegexOptions.Compiled);

    /// <summary>Extract every tool call from a leaked text block. Empty when nothing parses.</summary>
    public static IReadOnlyList<ToolInvocation> Parse(string text)
    {
        var calls = new List<ToolInvocation>();
        var index = 0;
        foreach (Match invoke in InvokeRx.Matches(text))
        {
            var name = invoke.Groups["name"].Value.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var args = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (Match param in ParamRx.Matches(invoke.Groups["body"].Value))
            {
                var pname = param.Groups["pname"].Value.Trim();
                if (pname.Length == 0)
                {
                    continue;
                }

                var raw = System.Net.WebUtility.HtmlDecode(param.Groups["val"].Value).Trim();
                var asString = true;
                var attr = StringAttrRx.Match(param.Groups["attrs"].Value);
                if (attr.Success && attr.Groups["v"].Value == "false")
                {
                    asString = false;
                }

                args[pname] = asString ? raw : ParseScalar(raw);
            }

            var element = JsonSerializer.SerializeToElement(args);
            calls.Add(new ToolInvocation($"leaked_{index++}", name, element));
        }

        return calls;
    }

    /// <summary>The visible text with the leaked tool-call block removed (any real prose before it kept).</summary>
    public static string Strip(string text)
    {
        var at = text.IndexOf("tool_calls", StringComparison.Ordinal);
        if (at < 0)
        {
            at = text.IndexOf("invoke name=", StringComparison.Ordinal);
        }

        if (at < 0)
        {
            return text.Trim();
        }

        var open = text.LastIndexOf('<', Math.Min(at, text.Length - 1));
        var head = open >= 0 ? text[..open] : text[..at];
        return head.Trim();
    }

    private static object? ParseScalar(string raw)
    {
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return l;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }

        if (bool.TryParse(raw, out var b))
        {
            return b;
        }

        return raw;
    }
}
