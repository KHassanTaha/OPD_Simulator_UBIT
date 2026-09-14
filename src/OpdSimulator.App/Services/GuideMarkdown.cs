namespace OpdSimulator.App.Services;

using System.Text.RegularExpressions;

/// <summary>Inline emphasis style of one text run.</summary>
public enum InlineStyle
{
    /// <summary>Plain text.</summary>
    Normal,

    /// <summary>Bold (**text**).</summary>
    Bold,

    /// <summary>Inline code (`text`).</summary>
    Code,
}

/// <summary>The kinds of block a guide section can contain.</summary>
public enum GuideBlockKind
{
    /// <summary>A section header; <see cref="GuideBlock.Level"/> 2-6.</summary>
    Heading,

    /// <summary>A prose paragraph.</summary>
    Paragraph,

    /// <summary>A bullet-list item.</summary>
    Bullet,

    /// <summary>A numbered-list item.</summary>
    Numbered,

    /// <summary>A fenced code block; <see cref="GuideBlock.Text"/> holds the raw lines.</summary>
    Code,

    /// <summary>A horizontal rule.</summary>
    Divider,
}

/// <summary>One inline run with its emphasis style (kept pure for tests).</summary>
public sealed record InlineRun(string Text, InlineStyle Style);

/// <summary>One block of rendered guide content.</summary>
public sealed record GuideBlock(
    GuideBlockKind Kind,
    IReadOnlyList<InlineRun> Runs,
    string Text = "",
    int Level = 0);

/// <summary>One searchable guide section — everything under a <c>## Heading</c>.</summary>
public sealed record GuideSection(string Id, string Title, IReadOnlyList<GuideBlock> Blocks);

/// <summary>
/// Reads the guide's markdown (docs/USER_MANUAL.md) as plain data: splits it
/// into <see cref="GuideSection"/>s on <c>##</c> headings and converts their
/// bodies into <see cref="GuideBlock"/>s with inline emphasis resolved. No UI
/// types are involved, so the parser and the section search are unit-testable
/// and the rendered view simply projects blocks onto Avalonia controls.
/// </summary>
public static class GuideMarkdown
{
    private static readonly Regex Heading = new(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex Bullet = new(@"^[-*]\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex Numbered = new(@"^\d+[.)]\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex InlineToken = new(@"(\*\*[^*]+\*\*|`[^`]+`)", RegexOptions.Compiled);

    /// <summary>
    /// Parses a markdown document into sections. A single <c>#</c> document
    /// title is consumed as the first section heading so its paragraphs still
    /// belong to the intro section.
    /// </summary>
    public static IReadOnlyList<GuideSection> ParseSections(string markdown)
    {
        var sections = new List<GuideSection>();
        GuideSection? current = null;
        var blocks = new List<GuideBlock>();
        List<string>? codeBuffer = null;

        void CloseCurrent()
        {
            if (current is not null)
            {
                current = current with { Blocks = blocks.ToArray() };
                sections.Add(current);
            }
            current = null;
            blocks.Clear();
        }

        foreach (string rawLine in markdown.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');

            if (codeBuffer is not null)
            {
                if (line.Trim() == "```")
                {
                    blocks.Add(new GuideBlock(GuideBlockKind.Code, Array.Empty<InlineRun>(), string.Join("\n", codeBuffer)));
                    codeBuffer = null;
                }
                else
                {
                    codeBuffer.Add(line);
                }
                continue;
            }

            if (line.Trim() == "```")
            {
                CloseCurrent();
                codeBuffer = new List<string>();
                continue;
            }

            var heading = Heading.Match(line);
            if (heading.Success && heading.Groups[1].Value.Length >= 2)
            {
                CloseCurrent();
                string title = heading.Groups[2].Value.Trim();
                current = new GuideSection(toId(title), title, Array.Empty<GuideBlock>());
                blocks.Add(new GuideBlock(GuideBlockKind.Heading,
                    InlineRuns(heading.Groups[2].Value), Level: heading.Groups[1].Value.Length));
                continue;
            }

            if (heading.Success)
            {
                // The document title (# ). Two or more empty sections would be
                // an author error; just drop in and treat as an intro heading.
                if (current is null)
                {
                    current = new GuideSection("top", heading.Groups[2].Value.Trim(), Array.Empty<GuideBlock>());
                }
                blocks.Add(new GuideBlock(GuideBlockKind.Heading,
                    InlineRuns(heading.Groups[2].Value), Level: 1));
                continue;
            }

            if (current is null)
            {
                current = new GuideSection("top", "Introduction", Array.Empty<GuideBlock>());
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.Trim() == "---")
            {
                blocks.Add(new GuideBlock(GuideBlockKind.Divider, Array.Empty<InlineRun>()));
                continue;
            }

            var bullet = Bullet.Match(line);
            if (bullet.Success)
            {
                blocks.Add(new GuideBlock(GuideBlockKind.Bullet, InlineRuns(bullet.Groups[1].Value)));
                continue;
            }

            var numbered = Numbered.Match(line);
            if (numbered.Success)
            {
                blocks.Add(new GuideBlock(GuideBlockKind.Numbered, InlineRuns(numbered.Groups[1].Value)));
                continue;
            }

            blocks.Add(new GuideBlock(GuideBlockKind.Paragraph, InlineRuns(line)));
        }

        CloseCurrent();
        return sections;
    }

    /// <summary>
    /// Returns the sections whose title or block text contains the query
    /// (case-insensitive). An empty query returns every section.
    /// </summary>
    public static IReadOnlyList<GuideSection> Search(IEnumerable<GuideSection> sections, string? query)
    {
        string needle = (query ?? string.Empty).Trim();
        if (needle.Length == 0)
        {
            return sections.ToArray();
        }

        return sections
            .Where(s => s.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || s.Blocks.Any(b => b.Text.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || b.Runs.Any(r => r.Text.Contains(needle, StringComparison.OrdinalIgnoreCase))))
            .ToArray();
    }

    private static IReadOnlyList<InlineRun> InlineRuns(string text)
    {
        var runs = new List<InlineRun>();
        foreach (string part in InlineToken.Split(text))
        {
            if (part.Length == 0)
            {
                continue;
            }

            if (part.Length >= 4 && part.StartsWith("**", StringComparison.Ordinal) && part.EndsWith("**", StringComparison.Ordinal))
            {
                runs.Add(new InlineRun(part[2..^2], InlineStyle.Bold));
            }
            else if (part.Length >= 2 && part.StartsWith('`') && part.EndsWith('`'))
            {
                runs.Add(new InlineRun(part[1..^1], InlineStyle.Code));
            }
            else
            {
                runs.Add(new InlineRun(part, InlineStyle.Normal));
            }
        }

        return runs;
    }

    private static string toId(string title)
        => Regex.Replace(title.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
}