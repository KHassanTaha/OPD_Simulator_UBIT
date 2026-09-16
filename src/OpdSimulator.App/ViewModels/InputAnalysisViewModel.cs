namespace OpdSimulator.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// Drives the Input Analysis tab (Phase 6C). The tab is an empty state until a
/// data file is uploaded and analysed; when one is, it hosts the P1 input
/// charts — histogram + fitted PDF overlay, chi-square observed-vs-expected
/// bars, and the per-server utilisation chart — each in its own
/// <see cref="OpdSimulator.App.Controls.ChartCard"/>. Chart data is prepared
/// on the background thread; the chart controls themselves are created on the
/// UI thread (G5).
/// </summary>
/// <remarks>
/// The 6c.1 scaffold deliberately carries no chart types yet: series and cards
/// land with their owner sub-phases (6c.2 histogram, 6c.3 chi-square, 6c.4
/// utilisation). <see cref="IsEmpty"/> is the single toggle — the view shows
/// either the empty message or the chart stack, never both.
/// </remarks>
public partial class InputAnalysisViewModel : ObservableObject
{
    /// <summary>
    /// The exact wording of the tab's empty state (shown until a data file is
    /// loaded); asserted verbatim by the UI test.
    /// </summary>
    public string EmptyMessage { get; } = "Load a data file to see fit analysis.";

    /// <summary>
    /// True while no analysed data exists yet; false once a usable data file
    /// is loaded and the chart cards are populated.
    /// </summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>
    /// Inverse of <see cref="IsEmpty"/>, provided so the view can bind the
    /// chart stack without a negating converter.
    /// </summary>
    public bool HasData => !IsEmpty;

    partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(HasData));
}