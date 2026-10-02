namespace OpdSimulator.App.ViewModels;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;

/// <summary>
/// One recorded server count for the Input tab: the number of servers that were
/// open at a stage when the loaded data was collected (Phase 8Q.1, D-178).
/// </summary>
/// <remarks>
/// <para>
/// This is the <b>historical</b> count. It answers "how busy was this stage while
/// the data was being recorded?" and feeds
/// <see cref="HistoricalMetricsService"/> only. It is deliberately a different
/// number from the Stages section's server count, which answers "how many servers
/// should the simulation have?" — one is a recollection about the past, the other
/// is a decision about the model, and the UI says which is which (rulings 6).
/// </para>
/// <para>
/// The two are seeded from each other once, by "Use for simulation", and never
/// re-synchronised: editing this field afterwards must not silently rewrite a
/// configuration the user may since have changed by hand.
/// </para>
/// </remarks>
public partial class HistoricalServerCountRow : ObservableObject
{
    /// <summary>Creates a row for one stage.</summary>
    /// <param name="stageName">The detected clinic stage name.</param>
    /// <param name="servers">The recorded count; defaults to 1 when not supplied.</param>
    public HistoricalServerCountRow(string stageName, int servers = 1)
    {
        StageName = stageName;
        _servers = servers.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The detected clinic stage this count belongs to.</summary>
    public string StageName { get; }

    /// <summary>
    /// The recorded count as typed. Free text so the field can hold an invalid
    /// entry long enough to explain it (FR-UI-17), rather than refusing the
    /// keystroke.
    /// </summary>
    [ObservableProperty]
    private string _servers;

    /// <summary>True when the typed value is not a whole number of at least 1.</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>The error in cause-and-remedy form (FR-UI-17).</summary>
    [ObservableProperty]
    private string _errorMessage = "";

    /// <summary>Tooltip naming what the count is for, per-stage (AGENTS §16.2).</summary>
    public string Tooltip =>
        $"Servers (tables, doctors or counters) open at {StageName} when this data was "
        + "recorded. Used to compute historical utilisation.";

    /// <summary>The count as a number, or null when the typed value is unusable.</summary>
    public int? ParsedServers =>
        int.TryParse(Servers, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= 1
            ? value
            : null;

    /// <summary>The recorded counts, for a stage that has a usable value.</summary>
    public int ValidatedServers => ParsedServers ?? 1;

    /// <summary>Validates the typed value on blur, as FR-UI-17 requires.</summary>
    public void Validate()
    {
        if (ParsedServers is { } value)
        {
            HasError = false;
            ErrorMessage = "";
            return;
        }

        HasError = true;
        ErrorMessage =
            $"Servers must be a whole number of at least 1. You entered \"{Servers}\".";
    }

    /// <summary>Accepts a value written by "Use for simulation", clearing any error.</summary>
    /// <param name="servers">The count to apply.</param>
    public void ApplySeeded(int servers)
    {
        Servers = servers.ToString(CultureInfo.InvariantCulture);
        HasError = false;
        ErrorMessage = "";
    }

    partial void OnServersChanged(string value)
    {
        // Correcting an invalid entry clears its error immediately (FR-UI-17),
        // rather than waiting for the next blur. Validation still runs on blur for
        // the initial bad keystroke, so the two do not fight.
        if (HasError && ParsedServers is not null)
        {
            HasError = false;
            ErrorMessage = "";
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the user edits the count.</summary>
    public event EventHandler? Changed;
}

/// <summary>
/// Historical utilisation for the loaded file: the recorded busy time per stage
/// against the recorded server count and operating time (Phase 8Q.1, D-178).
/// </summary>
public partial class HistoricalMetricsViewModel : ObservableObject
{
    /// <summary>Nothing loaded — the wording says so rather than showing zeros.</summary>
    public const string EmptyMessage =
        "Load a data file and record the servers open at each stage to see historical utilisation.";

    /// <summary>One row per stage that has both a count and usable service times.</summary>
    public ObservableCollection<HistoricalStageRow> Stages { get; } = new();

    /// <summary>True when there is something to show.</summary>
    public bool HasMetrics => Stages.Count > 0;

    /// <summary>Which divisor produced the figures (ruling 3).</summary>
    [ObservableProperty]
    private string _basisText = "";

    /// <summary>True when the figures divide by the observed window.</summary>
    [ObservableProperty]
    private bool _usesObservedWindow;

    /// <summary>True when a spanned window was used because the file has no session column.</summary>
    [ObservableProperty]
    private bool _usesSpannedWindow;

    /// <summary>The note explaining what is and is not knowable per server.</summary>
    [ObservableProperty]
    private string _perServerNote = "";

    /// <summary>Publishes the metrics for a binding and its recorded counts.</summary>
    /// <param name="binding">The analysed data file, or null to blank the section.</param>
    /// <param name="counts">Stage name → recorded server count.</param>
    public void Apply(DataBindingResult? binding, IReadOnlyDictionary<string, int> counts)
    {
        Stages.Clear();

        if (binding is null || binding.DataSet is null)
        {
            BasisText = "";
            UsesObservedWindow = false;
            UsesSpannedWindow = false;
            PerServerNote = "";
            RaiseHasMetrics();
            return;
        }

        var (minutes, basis) = HistoricalMetricsService.OperatingTimeFor(binding);
        UsesObservedWindow = basis == HistoricalOperatingBasis.ObservedWindow;
        UsesSpannedWindow = basis == HistoricalOperatingBasis.SpannedWindow;
        BasisText = HistoricalMetricsService.DescribeBasis(basis, binding.ObservedWindow);

        foreach (var metric in HistoricalMetricsService.ComputeFor(binding, counts))
            Stages.Add(new HistoricalStageRow(metric));

        PerServerNote = Stages.Count > 0
            ? "Per-server utilisation is assumed even; server-ID columns are not present "
              + "in the loaded file, so a stage's busy time cannot be split between its "
              + "individual servers."
            : "";

        RaiseHasMetrics();
    }

    private void RaiseHasMetrics()
    {
        OnPropertyChanged(nameof(HasMetrics));
        OnPropertyChanged(nameof(EmptyMessage));
    }
}

/// <summary>One stage's displayed historical-utilisation row.</summary>
public partial class HistoricalStageRow : ObservableObject
{
    /// <summary>Creates a row from computed metrics.</summary>
    /// <param name="metrics">The stage's computed metrics.</param>
    public HistoricalStageRow(HistoricalStageMetrics metrics)
    {
        StageName = metrics.StageName;
        ServerCount = metrics.ServerCount;
        TotalServiceMinutes = metrics.TotalServiceMinutes;
        OperatingMinutes = metrics.OperatingMinutes;
        Utilisation = metrics.StageUtilisation;

        Headline = $"{metrics.StageName} (c = {metrics.ServerCount})";
        BusyTimeText = $"{metrics.TotalServiceMinutes:F1} min";
        OperatingTimeText = $"{metrics.OperatingMinutes:F1} min";
        UtilisationText = $"{metrics.StageUtilisation * 100:F1}%";

        // The division is shown, not just its result, so the figure is checkable on
        // screen (D-176: a receipt whose numbers do not divide to each other).
        CalculationText =
            $"{metrics.StageUtilisation * 100:F1}% = {metrics.TotalServiceMinutes:F1} ÷ "
          + $"({metrics.ServerCount} × {metrics.OperatingMinutes:F1})";

        HasCapacityWarning = metrics.ExceedsCapacity;
        CapacityWarning = metrics.CapacityWarning;
    }

    /// <summary>The detected clinic stage name.</summary>
    public string StageName { get; }

    /// <summary>Recorded servers at this stage.</summary>
    public int ServerCount { get; }

    /// <summary>Total recorded service minutes.</summary>
    public double TotalServiceMinutes { get; }

    /// <summary>Operating minutes the figure was divided by.</summary>
    public double OperatingMinutes { get; }

    /// <summary>The stage utilisation fraction.</summary>
    public double Utilisation { get; }

    /// <summary>"Screening (c = 2)" — the stage with its recorded count.</summary>
    public string Headline { get; }

    /// <summary>Total time in service.</summary>
    public string BusyTimeText { get; }

    /// <summary>Operating time.</summary>
    public string OperatingTimeText { get; }

    /// <summary>Stage utilisation as a percentage.</summary>
    public string UtilisationText { get; }

    /// <summary>The division behind <see cref="UtilisationText"/>, shown for checking.</summary>
    public string CalculationText { get; }

    /// <summary>True when the recorded busy time exceeds the recorded capacity.</summary>
    public bool HasCapacityWarning { get; }

    /// <summary>Why the figure is above 100%, naming the likely causes.</summary>
    public string CapacityWarning { get; }
}