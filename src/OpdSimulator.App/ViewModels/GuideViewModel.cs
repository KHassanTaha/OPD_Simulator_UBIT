namespace OpdSimulator.App.ViewModels;

using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Services;

/// <summary>
/// The in-program user guide (AGENTS §17.1): an overlay whose left column lists
/// the sections (searchable) and whose right column renders the selected
/// section. Content comes from the embedded copy of docs/USER_MANUAL.md, so the
/// guide cannot drift from the manual (drift guarded by a test).
/// </summary>
public partial class GuideViewModel : ViewModelBase
{
    private const string EmbeddedMarkdown =
        "OpdSimulator.App.Assets.UserManual.md";

    private readonly IReadOnlyList<GuideSection> _allSections;

    /// <summary>Creates the guide state from a markdown document.</summary>
    /// <param name="markdown">The manual text to present.</param>
    public GuideViewModel(string markdown)
    {
        _allSections = GuideMarkdown.ParseSections(markdown);
        foreach (var section in _allSections)
        {
            SectionTitles.Add(section.Title);
        }
        Selected = _allSections.FirstOrDefault();
    }

    /// <summary>Loads the guide from the embedded USER_MANUAL.md resource.</summary>
    public static GuideViewModel FromEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(EmbeddedMarkdown);
        if (stream is null)
        {
            return new GuideViewModel("# User Manual\n\nThe embedded manual could not be loaded.");
        }
        using var reader = new StreamReader(stream);
        return new GuideViewModel(reader.ReadToEnd());
    }

    /// <summary>Gets the section titles for the section list column.</summary>
    public ObservableCollection<string> SectionTitles { get; } = new();

    /// <summary>Gets or sets the section currently displayed.</summary>
    [ObservableProperty]
    private GuideSection? _selected;

    /// <summary>Gets or sets the selected section title (what the ListBox binds).</summary>
    [ObservableProperty]
    private string? _selectedTitle;

    /// <summary>Keeps <see cref="Selected"/> aligned with the list selection.</summary>
    partial void OnSelectedTitleChanged(string? value)
    {
        if (value is not null)
        {
            Selected = _allSections.FirstOrDefault(s => s.Title == value);
        }
    }

    /// <summary>Keeps <see cref="SelectedTitle"/> aligned when the section is chosen in code.</summary>
    partial void OnSelectedChanged(GuideSection? value)
    {
        if (value is not null)
        {
            SelectedTitle = value.Title;
        }
    }

    /// <summary>Gets or sets the search query filtering both list and body.</summary>
    [ObservableProperty]
    private string? _searchText;

    /// <summary>Selects the section with the given anchor id, if present.</summary>
    public bool TryOpenAnchor(string? anchor)
    {
        if (string.IsNullOrEmpty(anchor))
        {
            return false;
        }
        var section = _allSections.FirstOrDefault(s => s.Id == anchor);
        if (section is null)
        {
            return false;
        }
        SearchText = string.Empty;
        Selected = section;
        return true;
    }

    partial void OnSearchTextChanged(string? value)
    {
        // Re-filter the section list; keep the selection unless it fell out.
        var filtered = GuideMarkdown.Search(_allSections, value);
        SectionTitles.Clear();
        foreach (var section in filtered)
        {
            SectionTitles.Add(section.Title);
        }
        if (Selected is not null && filtered.All(s => s.Id != Selected.Id) && filtered.Count > 0)
        {
            Selected = filtered[0];
        }
    }
}