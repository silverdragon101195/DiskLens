using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DiskLens.Views;

/// <summary>
/// Converts the Markdown subset chat models produce (headings, bullet and numbered lists, quotes,
/// fenced code, **bold**, *italic*, `code`) into FlowDocument blocks. Anything else renders as text.
/// </summary>
public static partial class MarkdownRenderer
{
    private static readonly FontFamily CodeFont = new("Cascadia Mono, Consolas");

    public static List<Block> Render(string markdown)
    {
        var blocks = new List<Block>();
        var paragraph = (Paragraph?)null;
        var code = (List<string>?)null;

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                paragraph = null;
                if (code is null)
                {
                    code = [];
                }
                else
                {
                    blocks.Add(CodeBlock(code));
                    code = null;
                }
                continue;
            }

            if (code is not null)
            {
                code.Add(rawLine);
                continue;
            }

            if (line.Length == 0)
            {
                paragraph = null;
                continue;
            }

            if (HeadingPattern().Match(line) is { Success: true } heading)
            {
                paragraph = null;
                var level = heading.Groups[1].Length;
                var block = new Paragraph { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };
                block.FontSize = level switch { 1 => 20, 2 => 18, _ => 16 };
                AddInlines(block.Inlines, heading.Groups[2].Value);
                blocks.Add(block);
                continue;
            }

            if (ListItemPattern().Match(line) is { Success: true } item)
            {
                paragraph = null;
                var depth = item.Groups[1].Value.Replace("\t", "    ").Length / 2;
                var marker = item.Groups[2].Value;
                var bullet = char.IsDigit(marker[0]) ? marker + " " : "•  ";
                var block = new Paragraph
                {
                    Margin = new Thickness(8 + depth * 18, 2, 0, 2),
                    TextIndent = -12,
                    Padding = new Thickness(12, 0, 0, 0),
                };
                block.Inlines.Add(new Run(bullet) { Foreground = Brush("AccentBrush") });
                AddInlines(block.Inlines, item.Groups[3].Value);
                blocks.Add(block);
                continue;
            }

            if (line.StartsWith('>'))
            {
                paragraph = null;
                var block = new Paragraph
                {
                    Margin = new Thickness(0, 4, 0, 4),
                    Padding = new Thickness(10, 2, 0, 2),
                    BorderBrush = Brush("PanelBorderBrush"),
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Foreground = Brush("SecondaryTextBrush"),
                };
                AddInlines(block.Inlines, line.TrimStart('>', ' '));
                blocks.Add(block);
                continue;
            }

            if (HorizontalRulePattern().IsMatch(line))
            {
                paragraph = null;
                continue;
            }

            if (paragraph is null)
            {
                paragraph = new Paragraph { Margin = new Thickness(0, 4, 0, 4) };
                blocks.Add(paragraph);
            }
            else
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            AddInlines(paragraph.Inlines, line);
        }

        // An unterminated fence is still arriving from the stream; show what has come so far.
        if (code is not null)
            blocks.Add(CodeBlock(code));

        return blocks;
    }

    private static Paragraph CodeBlock(List<string> lines)
    {
        var block = new Paragraph
        {
            FontFamily = CodeFont,
            FontSize = 13,
            Background = Brush("WindowBrush"),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 6, 0, 6),
        };
        block.Inlines.Add(new Run(string.Join("\n", lines)));
        return block;
    }

    private static void AddInlines(InlineCollection inlines, string text)
    {
        foreach (var part in InlinePattern().Split(text))
        {
            if (part.Length == 0)
                continue;

            if (part.Length > 4 && part.StartsWith("**", StringComparison.Ordinal) && part.EndsWith("**", StringComparison.Ordinal))
                inlines.Add(new Bold(new Run(part[2..^2])));
            else if (part.Length > 2 && part[0] == '`' && part[^1] == '`')
                inlines.Add(new Run(part[1..^1]) { FontFamily = CodeFont, Background = Brush("WindowBrush") });
            else if (part.Length > 2 && part[0] is '*' or '_' && part[^1] == part[0])
                inlines.Add(new Italic(new Run(part[1..^1])));
            else
                inlines.Add(new Run(part));
        }
    }

    private static Brush Brush(string key) => (Brush)Application.Current.FindResource(key);

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^(\s*)([-*+]|\d+[.)])\s+(.*)$")]
    private static partial Regex ListItemPattern();

    [GeneratedRegex(@"^\s*([-*_])(\s*\1){2,}\s*$")]
    private static partial Regex HorizontalRulePattern();

    [GeneratedRegex(@"(\*\*[^*]+\*\*|`[^`]+`|\*[^*\s][^*]*\*|\b_[^_\s][^_]*_\b)")]
    private static partial Regex InlinePattern();
}
