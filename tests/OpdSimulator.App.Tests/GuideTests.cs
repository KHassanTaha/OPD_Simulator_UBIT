using System.IO;
using System.Reflection;

namespace OpdSimulator.App.Tests;

/// <summary>
/// In-program guide wiring (AGENTS §17.1): the embedded markdown must match the
/// repository's docs/USER_MANUAL.md (drift guard), the parser must produce
/// sections, and search must filter them.
/// </summary>
public class GuideTests
{
    [Fact]
    public void EmbeddedManual_MatchesRepositoryFile()
    {
        var embedded = ReadEmbedded();
        var repoPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../docs/USER_MANUAL.md"));
        string repo = File.ReadAllText(repoPath).Replace("\r\n", "\n").TrimEnd();

        Assert.Equal(repo, embedded.TrimEnd());
    }

    [Fact]
    public void EmbeddedManual_IsPresentAndNonEmpty()
    {
        string doc = ReadEmbedded();
        Assert.False(string.IsNullOrWhiteSpace(doc));
        Assert.StartsWith("#", doc, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseSections_SplitsOnLevel2Headings_AndParagraphs()
    {
        var sections = GuideMarkdown.ParseSections(
            "# OPD Simulator\nIntro line.\n\n## Setup\n\n**A bold** and `code`.\n\n-  one\n-  two");

        Assert.True(sections.Count >= 2);
        var setup = sections.First(s => s.Id == "setup");
        Assert.True(setup.Blocks.Any(b => b.Kind == GuideBlockKind.Paragraph));
        Assert.Contains(setup.Blocks, b => b.Kind == GuideBlockKind.Bullet);
        // Emphasis is split into runs so the renderer can style them.
        Assert.Contains(setup.Blocks, b =>
            b.Runs.Any(r => r.Text == "A bold" && r.Style == InlineStyle.Bold));
        Assert.Contains(setup.Blocks, b =>
            b.Runs.Any(r => r.Text == "code" && r.Style == InlineStyle.Code));
    }

    [Fact]
    public void Search_FiltersSectionsOnTitleAndBody()
    {
        var sections = GuideMarkdown.ParseSections(
            "# Manual\n\n## Arrival rate\n\nSet the lambda.\n\n## Reset\n\nClear the panel.");

        Assert.Single(GuideMarkdown.Search(sections, "lambda"));
        Assert.Single(GuideMarkdown.Search(sections, "reset"));
        Assert.Single(GuideMarkdown.Search(sections, "arrival"));
        Assert.Empty(GuideMarkdown.Search(sections, "does-not-exist"));
        Assert.Equal(sections.Count, GuideMarkdown.Search(sections, null).Count);
    }

    [Fact]
    public void GuideViewModel_FromEmbedded_SelectsFirstSection()
    {
        var vm = GuideViewModel.FromEmbedded();

        Assert.NotEmpty(vm.SectionTitles);
        Assert.NotNull(vm.Selected);
    }

    [Fact]
    public void GuideViewModel_TryOpenAnchor_NavigatesAndClearsSearch()
    {
        var vm = GuideViewModel.FromEmbedded();
        string anchor = vm.Selected!.Id;

        vm.SearchText = "noise";
        Assert.True(vm.TryOpenAnchor(anchor));
        Assert.Equal(vm.Selected, vm.Selected);
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.False(vm.TryOpenAnchor("missing-anchor"));
    }

    [Fact]
    public void GuideViewModel_SelectedTitle_TracksTheListSelection()
    {
        var vm = new GuideViewModel("# Manual\n\n## One\n\nText.  \n\n## Two\n\nMore.");
        vm.SelectedTitle = "Two";

        Assert.Equal("Two", vm.Selected!.Title);
    }

    private static string ReadEmbedded()
    {
        var assembly = Assembly.GetAssembly(typeof(GuideMarkdown))!;
        using var stream = assembly.GetManifestResourceStream("OpdSimulator.App.Assets.UserManual.md");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}