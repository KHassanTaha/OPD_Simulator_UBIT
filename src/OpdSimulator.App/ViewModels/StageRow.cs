namespace OpdSimulator.App.ViewModels;

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Services;
using Serilog;

/// <summary>
/// One configurable simulation stage row (Section 4). A dedicated nested
/// view-model (not a bare tuple) so each row owns its validated Servers field,
/// its display name and its read-only service-rate source label. Since Phase
/// 5d (D-112) the stages section is topology ONLY — the editable service-rate
/// field was removed; the single manual μ entry lives in Parameters and the
/// label below reports where this stage's effective μ comes from.
/// </summary>
public partial class StageRow : ObservableObject
{
    /// <summary>
    /// Guards the two-way <see cref="SelectedModel"/> ⇄ <see cref="Servers"/>
    /// sync against re-entrancy. Writing one side raises the other side's
    /// change notification, which would otherwise bounce straight back and
    /// either recurse or thrash the field the user is still typing in
    /// (Phase 8F, D-143).
    /// </summary>
    private bool _suppressModelServerSync;

    /// <summary>Wires the servers field into the model-notation sync (Phase 8F, D-143).</summary>
    public StageRow()
    {
        Servers.ValueChanged += (_, _) => OnServersValueChanged();
    }

    [ObservableProperty]
    private string _stageName = "";

    /// <summary>Servers-count input (integer ≥ 1; default 1).</summary>
    public ConfigFieldViewModel Servers { get; } = new() { Value = "1" };

    /// <summary>
    /// Manual service rate μ for this stage, interpreted per the current
    /// Parameter mode and Time unit. Authoritative in EnterManually mode; in
    /// FitFromData mode it is the fallback shown only for a stage with no
    /// fitted rate (7C.4).
    /// </summary>
    [ObservableProperty]
    private string muValue = string.Empty;

    /// <summary>True while <see cref="MuValue"/> is non-blank and not a positive number.</summary>
    [ObservableProperty]
    private bool muHasError;

    /// <summary>Inline cause+remedy for an invalid <see cref="MuValue"/> (blank is valid — it is simply not entered yet).</summary>
    [ObservableProperty]
    private string muError = string.Empty;

    /// <summary>
    /// Whether this row's editable μ field is shown: always in manual mode, and
    /// in fit mode only when the loaded data has no fitted rate for this stage
    /// (7C.5). Set by <see cref="ConfigPanelViewModel.RefreshStageSourceLabels"/>.
    /// </summary>
    [ObservableProperty]
    private bool isMuVisible;

    /// <summary>Raised whenever <see cref="MuValue"/> changes so the parent VM can re-gate Start (7C.6).</summary>
    public event EventHandler? MuValueChanged;

    partial void OnMuValueChanged(string value)
    {
        // FR-UI-17: editing clears the stale error; blur re-validates.
        MuHasError = false;
        MuError = string.Empty;
        MuValueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Blur validation for the manual per-stage μ: blank is valid (not entered); otherwise it must parse as a positive number.</summary>
    public void ValidateMu()
    {
        if (string.IsNullOrWhiteSpace(MuValue) || (double.TryParse(MuValue, out var mu) && mu > 0))
        {
            MuHasError = false;
            MuError = string.Empty;
        }
        else
        {
            MuHasError = true;
            MuError = $"μ must be a positive number. You entered \"{MuValue}\".";
        }
    }

    /// <summary>
    /// Read-only service-rate source for this stage: the fitted value with
    /// "(from data)", the Parameters manual entry with "(manual)", or
    /// "μ = — (no source)". Set by <see cref="ConfigPanelViewModel"/>
    /// whenever the source could change (5d.1).
    /// </summary>
    [ObservableProperty]
    private string _serviceRateLabel = "μ = — (no source)";

    /// <summary>
    /// Kendall-notation shortcut chosen for this stage (Phase 7B), e.g.
    /// "M/M/2". Ignored while <see cref="UseAdvancedSetup"/> is on.
    /// </summary>
    [ObservableProperty]
    private string _selectedModel = "M/M/1";

    /// <summary>
    /// When false the arrival/service families and the server count are derived
    /// from <see cref="SelectedModel"/>; when true the two family dropdowns are
    /// revealed and edited independently (Phase 7B).
    /// </summary>
    [ObservableProperty]
    private bool _useAdvancedSetup = false;

    /// <summary>Arrival distribution family ("Exponential", "Deterministic" or "General").</summary>
    [ObservableProperty]
    private string _arrivalFamily = "Exponential";

    /// <summary>Service distribution family ("Exponential", "Deterministic" or "General").</summary>
    [ObservableProperty]
    private string _serviceFamily = "Exponential";

    /// <summary>Kendall model shortcuts offered by the per-stage model dropdown.</summary>
    public IReadOnlyList<string> ModelOptions => ModelNotationParser.StandardModels;

    /// <summary>Distribution families offered when Advanced setup is on.</summary>
    public IReadOnlyList<string> DistributionFamilies { get; } =
        new[] { "Exponential", "Deterministic", "General" };

    /// <summary>
    /// Applies a model-notation shortcut to the row: arrival family, service
    /// family and server count. Skipped while Advanced setup owns those fields.
    /// </summary>
    partial void OnSelectedModelChanged(string value)
    {
        if (UseAdvancedSetup)
        {
            return;
        }

        // A model set programmatically (including by the servers sync below)
        // may not be one of the listed notations. Parse throws on anything it
        // does not recognise, so a bad value must not be able to crash a run
        // from a plain field edit.
        ModelNotationParser.ParsedModel parsed;
        try
        {
            parsed = ModelNotationParser.Parse(value);
        }
        catch (ArgumentException)
        {
            Log.Warning("Stage '{Stage}': ignoring unparseable model notation '{Model}'.", StageName, value);
            return;
        }

        ArrivalFamily = parsed.ArrivalFamily;
        ServiceFamily = parsed.ServiceFamily;

        // Writing Servers re-enters this row through Servers.ValueChanged; the
        // guard stops the round trip from rewriting SelectedModel underneath
        // the user (Phase 8F, D-143).
        _suppressModelServerSync = true;
        try
        {
            Servers.Value = parsed.ServerCount.ToString();
        }
        finally
        {
            _suppressModelServerSync = false;
        }
    }

    /// <summary>
    /// Reverse half of the model-notation sync: typing a server count rewrites
    /// the trailing number of the notation, so M/M/2 + 3 servers reads M/M/3
    /// instead of leaving a stale model selected (Phase 8F, D-143).
    /// </summary>
    private void OnServersValueChanged()
    {
        if (_suppressModelServerSync || UseAdvancedSetup)
        {
            return;
        }

        var current = SelectedModel;
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        // Only whole counts ≥ 1 can be represented, and the dropdown only
        // offers 1–5 servers. Anything else (a blank field mid-edit, "0", "9",
        // "abc") leaves the notation alone rather than fabricating an option
        // that is not in the list — the inline field error owns that case.
        if (!int.TryParse(Servers.Value, out var count) || count < 1)
        {
            return;
        }

        var slash = current.LastIndexOf('/');
        if (slash < 0)
        {
            return;
        }

        var candidate = $"{current[..(slash + 1)]}{count}";
        if (!ModelNotationParser.StandardModels.Contains(candidate) || candidate == current)
        {
            return;
        }

        _suppressModelServerSync = true;
        try
        {
            SelectedModel = candidate;
        }
        finally
        {
            _suppressModelServerSync = false;
        }
    }

    /// <summary>True while this row's servers field is invalid.</summary>
    public bool HasErrors => Servers.HasError;

    /// <summary>Blur validation for the servers count: integer ≥ 1.</summary>
    public void ValidateServers()
    {
        var value = Servers.Value;
        if (int.TryParse(value, out var n) && n >= 1)
        {
            Servers.ClearError();
        }
        else
        {
            Servers.SetError($"Servers must be a whole number of at least 1. You entered \"{value}\".");
        }
    }
}
