namespace OpdSimulator.App.ViewModels;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Fitting;
using OpdSimulator.Data.Parameters;
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

    /// <summary>
    /// Suppresses the notation ⇄ family sync while a family is assigned from outside
    /// the row (Phase 8K, task 8K.4, "Apply to all stages"). Needed because that
    /// assignment intentionally ends with <see cref="SelectedModel"/> = "Custom",
    /// which in non-advanced mode would otherwise be parsed as a notation — and
    /// "Custom" is not one, so the parse would throw and log a spurious warning.
    /// </summary>
    private bool _suppressNotationSync;

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
    /// Whether this row's editable μ field is shown. Since Phase 9P it is shown
    /// whenever the panel is enabled, in both modes: in fit mode its placeholder
    /// states the fitted value, so a blank field visibly means "use the data" and a
    /// typed one is a real override (D-242).
    /// </summary>
    [ObservableProperty]
    private bool isMuVisible = true;

    /// <summary>
    /// The placeholder shown in the μ field. In fit mode it carries the fitted
    /// value in the shape the current parameter mode shows, so the field can be
    /// left blank without the user losing the number (Phase 9P, D-242).
    /// </summary>
    [ObservableProperty]
    private string _muPlaceholder = "e.g., 0.8";

    /// <summary>Raised whenever <see cref="MuValue"/> changes so the parent VM can re-gate Start (7C.6).</summary>
    public event EventHandler? MuValueChanged;

    partial void OnMuValueChanged(string value)
    {
        // FR-UI-17: editing clears the stale error; blur re-validates.
        MuHasError = false;
        MuError = string.Empty;
        // A non-blank edit is the user's own value. The parent downgrades it to
        // Default when there is no fit for it to override (D-242), and
        // ApplyFittedSpec writes Fitted after its own assignment, so a fit's fill is
        // never mistaken for a typed one.
        MuProvenance = string.IsNullOrWhiteSpace(value) ? MuProvenance.Default : MuProvenance.UserSet;
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
    /// Read-only derived inflow for this stage — "Inflow λᵢ = … ρᵢ = …".
    /// The numbers come from 9P's preview topology; 9A only formats them, so the
    /// panel can never disagree with the engine about a stage's arrival rate
    /// (Phase 9A, D-229/D-243).
    /// </summary>
    [ObservableProperty]
    private string _inflowLabel = "Inflow λᵢ = —   ρᵢ = —";

    /// <summary>
    /// This row's μ-field caption, in the shape the current parameter mode shows
    /// (a rate rate-wise, a duration mean-wise). Pushed by
    /// <c>ConfigPanelViewModel.RefreshStageSourceLabels</c> because an extracted
    /// control cannot resolve a parent binding (Phase 9A, G6d).
    /// </summary>
    [ObservableProperty]
    private string _muLabel = string.Empty;

    /// <summary>The unit badge for <see cref="MuLabel"/>, from <see cref="FittingTimeUnits"/>.</summary>
    [ObservableProperty]
    private string _muUnit = string.Empty;

    /// <summary>
    /// This row's spread-field caption (σ or the uniform half-width w) with the
    /// current time unit. A spread is a duration in either parameter mode (G6e).
    /// </summary>
    [ObservableProperty]
    private string _spreadLabel = string.Empty;

    /// <summary>The unit badge for <see cref="SpreadLabel"/>, from <see cref="FittingTimeUnits"/>.</summary>
    [ObservableProperty]
    private string _spreadUnit = string.Empty;

    /// <summary>
    /// Kendall-notation shortcut chosen for this stage (Phase 7B), e.g.
    /// "M/M/2". Ignored while <see cref="UseAdvancedSetup"/> is on.
    /// </summary>
    [ObservableProperty]
    private string _selectedModel = "M/M/1";

    /// <summary>
    /// The one inline error channel for this row (Phase 8K, D-151). A spread
    /// value the build path will refuse and an auto-fit that could not run are
    /// both "this row cannot be built, and here is why" — giving them one field
    /// means the row can never show two different complaints at once, and the
    /// user always has one place to look.
    /// </summary>
    [ObservableProperty]
    private string? _inlineError;

    /// <summary>
    /// What the G/G/c auto-fit decided for this stage, e.g.
    /// "Best fit: Gamma (AIC 41, p=0.312)". Null until a G/G/c selection has been
    /// fitted. Cleared whenever the family or the model changes, so a badge can
    /// never describe a stage it no longer describes (Phase 8K, D-151).
    /// </summary>
    [ObservableProperty]
    private string? _autoFitBadge;

    /// <summary>
    /// Where this row's service family came from (Phase 9C, D-232). A
    /// <see cref="FieldProvenance.UserSet"/> family is never overwritten by a
    /// later load; the parent warns instead. μ has its own provenance because
    /// its value is resolved separately by 9P's resolver.
    /// </summary>
    [ObservableProperty]
    private FieldProvenance _familyProvenance = FieldProvenance.Default;

    /// <summary>The family the uploaded data chose for this stage, or null when no acceptable fit exists.</summary>
    [ObservableProperty]
    private DistributionFamily? _bestFitFamily;

    /// <summary>AIC of <see cref="BestFitFamily"/>, printed in the disclosure sentence.</summary>
    [ObservableProperty]
    private double _bestFitAic;

    /// <summary>
    /// The μ implied by the uploaded data for this stage (per minute), pushed by
    /// the panel so the μ override warning can name it without a second fit.
    /// </summary>
    [ObservableProperty]
    private double? _fittedMuPerMinute;

    /// <summary>The winning spec kept so `[Use the data fit]` can restore the exact shape.</summary>
    [ObservableProperty]
    private DistributionSpec? _bestFitSpec;

    /// <summary>Whether a load supplied a winner this button can restore (Phase 9C, D-232).</summary>
    public bool HasDataFit => BestFitSpec is not null;

    partial void OnBestFitSpecChanged(DistributionSpec? value) => OnPropertyChanged(nameof(HasDataFit));

    /// <summary>
    /// The deviation sentence shown when the user's service family differs from
    /// the data's winner (Phase 9C, §4.3). Null when the two agree.
    /// </summary>
    [ObservableProperty]
    private string? _fitOverrideWarning;

    /// <summary>
    /// The deviation sentence shown when a typed μ differs from the fitted μ
    /// (Phase 9C, §4.3). Null when the two agree.
    /// </summary>
    [ObservableProperty]
    private string? _muOverrideWarning;

    /// <summary>
    /// Names the consequence when the loaded file has no timings for this stage
    /// (Phase 9C, D-236). Null when the data covers it.
    /// </summary>
    [ObservableProperty]
    private string? _dataCoverageWarning;

    /// <summary>
    /// The one sentence under the service-family field stating what a load did
    /// (Phase 9C, §4.3). Computed by the panel so its wording has one home.
    /// </summary>
    [ObservableProperty]
    private string? _fitDisclosure;

    /// <summary>
    /// Where this row's effective μ came from (Phase 9P, D-242). It decides the
    /// precedence the resolver applies and how <see cref="ServiceRateLabel"/>
    /// reads. Provenance, not non-emptiness: a value typed before any file was
    /// loaded is a fallback, not an override, which is why the parent sets this
    /// from what it knows rather than from "the field is not blank".
    /// </summary>
    [ObservableProperty]
    private MuProvenance _muProvenance = MuProvenance.Default;

    /// <summary>
    /// The notation this row held before the current selection, so a G/G/c
    /// auto-fit that cannot run can put the dropdown back where the user left it
    /// instead of leaving a G/G/c that never described anything.
    /// </summary>
    private string? _previousModel;

    /// <summary>
    /// When false the arrival/service families and the server count are derived
    /// from <see cref="SelectedModel"/>; when true the two family dropdowns are
    /// revealed and edited independently (Phase 7B).
    /// </summary>
    [ObservableProperty]
    private bool _useAdvancedSetup = false;

    /// <summary>
    /// Arrival family recorded for this stage (Phase 8K, D-150). Typed as the
    /// Core enum rather than a display string so the value can be carried into a
    /// <see cref="DistributionSpec"/> without a name lookup that could fail.
    /// </summary>
    /// <remarks>
    /// <b>Recorded, not applied.</b> One external arrival process is chosen in the
    /// Model section and shared by the whole run; a patient reaching this stage is
    /// the same entity the previous stage released (Phase 9A, D-229). This value
    /// exists only so the Kendall notation parser has a family to record — the
    /// panel pushes it from <c>InterArrivalDistribution</c> and no sampler reads it.
    /// </remarks>
    [ObservableProperty]
    private DistributionFamily _arrivalFamily = DistributionFamily.Exponential;

    /// <summary>
    /// Service distribution family for this stage — the one part of the notation
    /// the engine actually honours per stage (Phase 8K, D-150).
    /// </summary>
    [ObservableProperty]
    private DistributionFamily _serviceFamily = DistributionFamily.Exponential;

    /// <summary>
    /// Standard deviation for the Normal and Lognormal families, as typed text
    /// (null when not applicable or not entered). A string rather than a
    /// <c>double?</c> so an invalid entry can be held and reported by the field
    /// instead of collapsing to null and looking blank.
    /// </summary>
    [ObservableProperty]
    private string? _serviceStdDev;

    /// <summary>
    /// Gamma shape k, as typed text (null when not applicable). The matching scale
    /// is derived as Mean / k rather than typed (Phase 8K, D-150 corrected).
    /// </summary>
    [ObservableProperty]
    private string? _serviceShape;

    /// <summary>
    /// Uniform half-width w in minutes, as typed text (null when not applicable).
    /// The bounds are derived as Mean − w and Mean + w rather than typed.
    /// </summary>
    /// <remarks>
    /// A separate property from <see cref="ServiceShape"/> rather than a shared field
    /// with a swapped label: k and w are different quantities in different units, and
    /// one field serving both would let a stale k read as a valid w after a family
    /// change.
    /// </remarks>
    [ObservableProperty]
    private string? _serviceSpread;

    /// <summary>
    /// Whether this row's family needs a spread parameter in addition to μ. Drives
    /// the visibility of the spread input in <c>StageRowControl</c> (Phase 8K).
    /// </summary>
    /// <remarks>
    /// Deliberately a computed property rather than a stored flag: a stored one
    /// would need invalidating on every family change, and a missed invalidation
    /// would silently show the wrong input. Derived, it cannot drift.
    /// </remarks>
    public bool NeedsShapeParameters =>
        ServiceFamily is DistributionFamily.Normal
            or DistributionFamily.Lognormal
            or DistributionFamily.Gamma
            or DistributionFamily.Uniform;

    /// <summary>True when the family takes a standard deviation (Normal, Lognormal).</summary>
    public bool NeedsStdDev =>
        ServiceFamily is DistributionFamily.Normal or DistributionFamily.Lognormal;

    /// <summary>True when the family takes a Gamma shape k.</summary>
    public bool NeedsShape => ServiceFamily == DistributionFamily.Gamma;

    /// <summary>True when the family takes a Uniform half-width w.</summary>
    public bool NeedsSpread => ServiceFamily == DistributionFamily.Uniform;

    /// <summary>
    /// True while <see cref="InlineError"/> holds a message. Bound by the spread
    /// fields and by the auto-fit error line: one channel and one flag, so an error
    /// can never appear under an input it does not belong to.
    /// </summary>
    public bool HasInlineError => !string.IsNullOrEmpty(InlineError);

    /// <summary>Keeps the derived error flag in step with the message it reads.</summary>
    partial void OnInlineErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasInlineError));
        OnPropertyChanged(nameof(ShowRowLevelError));
    }

    /// <summary>
    /// Whether the row's inline error must be drawn at row level because no spread
    /// field is on screen to carry it. A spread error is already shown under its own
    /// field, so repeating it here would print one message twice; an auto-fit refusal
    /// belongs to no field, and on an Exponential or Deterministic stage — where no
    /// spread field exists at all — it would otherwise never be seen.
    /// </summary>
    public bool ShowRowLevelError => HasInlineError && !NeedsShapeParameters;

    /// <summary>
    /// Announces that every family-dependent derived property may have changed. A
    /// computed property that nobody raises is a property the UI never re-reads.
    /// </summary>
    private void RaiseDerivedSpreadNotifications()
    {
        OnPropertyChanged(nameof(NeedsShapeParameters));
        OnPropertyChanged(nameof(NeedsStdDev));
        OnPropertyChanged(nameof(NeedsShape));
        OnPropertyChanged(nameof(NeedsSpread));
        OnPropertyChanged(nameof(ShowRowLevelError));
    }

    /// <summary>
    /// Blur validation for whichever spread parameter the selected family needs
    /// (Phase 8K, D-150). Families that need none are left alone.
    /// </summary>
    /// <remarks>
    /// Same rule the run path enforces in <c>BuildSpec</c>, moved here so the user is
    /// told at the field rather than only when a run is refused.
    /// </remarks>
    public void ValidateSpread()
    {
        var (label, text) = ServiceFamily switch
        {
            DistributionFamily.Normal or DistributionFamily.Lognormal => ("σ", ServiceStdDev),
            DistributionFamily.Gamma => ("shape k", ServiceShape),
            DistributionFamily.Uniform => ("half-width w", ServiceSpread),
            _ => (string.Empty, null),
        };

        if (label.Length == 0)
        {
            InlineError = string.Empty;
            return;
        }

        if (double.TryParse(text, out var value) && value > 0)
        {
            InlineError = string.Empty;
        }
        else
        {
            InlineError = $"{ServiceFamily} needs {label} greater than 0. " +
                          $"You entered \"{text ?? "blank"}\".";
        }
    }

    /// <summary>
    /// The Kendall first token for this row, derived from the engine-wide arrival
    /// family recorded on <see cref="ArrivalFamily"/>: M for Exponential (Poisson),
    /// D for Deterministic, G for anything fitted. A row can never claim an
    /// arrival process the run does not use (Phase 9A, D-229).
    /// </summary>
    public string ArrivalLetter => ArrivalToken(ArrivalFamily);

    /// <summary>
    /// Kendall model shortcuts offered by the row's dropdown. Service-side
    /// choices are filtered to the derived <see cref="ArrivalLetter"/>: a
    /// Markovian front door offers M/M/c and M/D/c, a deterministic one D/M/c.
    /// The G/G/c auto-fit notations stay available on every row: G names no
    /// family at all, it asks the loaded data to choose one, so it never claims
    /// an arrival process the engine does not use (D-229).
    /// </summary>
    public IReadOnlyList<string> ModelOptions => ModelNotationParser.StandardModels
        .Where(model => model.StartsWith($"{ArrivalLetter}/", StringComparison.Ordinal)
                        || ModelNotationParser.IsGeneralModel(model))
        .ToList();

    /// <summary>Maps a recorded arrival family to its Kendall first token.</summary>
    /// <param name="family">The engine-wide arrival family.</param>
    /// <returns>M, D or G.</returns>
    private static string ArrivalToken(DistributionFamily family) => family switch
    {
        DistributionFamily.Exponential => "M",
        DistributionFamily.Deterministic => "D",
        _ => "G",
    };

    /// <summary>
    /// Distribution families offered when Advanced setup is on — all six the engine
    /// can sample, by name so the dropdown shows "Lognormal" rather than "2".
    /// </summary>
    public IReadOnlyList<DistributionFamily> DistributionFamilies { get; } =
        Enum.GetValues<DistributionFamily>();

    /// <summary>
    /// Captures the outgoing notation before it changes, so
    /// <see cref="RevertSelectedModel"/> can restore it when an auto-fit refuses.
    /// </summary>
    partial void OnSelectedModelChanging(string value) => _previousModel = SelectedModel;

    /// <summary>
    /// Applies a model-notation shortcut to the row: arrival family, service
    /// family and server count. Skipped while Advanced setup owns those fields.
    /// </summary>
    partial void OnSelectedModelChanged(string value)
    {
        // G/G/c names no family, so it has to be fitted before anything else looks at
        // the value — including the Advanced check below. Restricting auto-fit to Model
        // mode would mean the same dropdown did two different things depending on which
        // panel it sat in, and Advanced is where a user assembles a mixed network, which
        // is exactly where a per-stage fit is wanted. _suppressNotationSync is not
        // consulted here on purpose: nothing inside the sync writes a G/G notation, so
        // honouring it would only add a way for the trigger to be silently skipped.
        if (ModelNotationParser.IsGeneralModel(value))
        {
            AutoFitRequested?.Invoke(this, ModelAutoFitRequestedEventArgs.ForStage(StageName, value));
            return;
        }

        if (UseAdvancedSetup || _suppressNotationSync)
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

        // A null family can only come from a G notation, and every G notation was
        // already handled by the trigger above.
        //
        // The suppression is essential, not defensive: assigning the families fires
        // OnArrivalFamilyChanged / OnServiceFamilyChanged, and in Advanced mode those
        // write SelectedModel = "Custom". Without this guard, picking M/D/2 in
        // Advanced mode would immediately replace the user's own notation with
        // "Custom" — and picking it outside Advanced mode would too, because the
        // families genuinely change. Either way the dropdown would lie about the row.
        _suppressNotationSync = true;
        try
        {
            // 9A D-229: the first letter belongs to the Model section, not to a
            // row. Accept a notation's arrival family only when it agrees with the
            // derived letter (the standalone-row case); never let a row retune the
            // engine-wide arrival process. G/G/c parses to null and is left alone.
            if (parsed.ArrivalFamily is { } parsedArrival
                && ArrivalToken(parsedArrival) != ArrivalLetter)
            {
                ArrivalFamily = parsedArrival;
            }

            ServiceFamily = parsed.ServiceFamily ?? DistributionFamily.Exponential;
            ClearShapeParametersFor(ServiceFamily);
        }
        finally
        {
            _suppressNotationSync = false;
        }

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
    /// Raised when the user picks a G/G/c notation, so the parent view model can
    /// auto-fit a family from the loaded data (Phase 8K, task 8K.6).
    /// </summary>
    /// <remarks>
    /// A stage row deliberately cannot answer this itself: it does not know
    /// whether a file is loaded, holds no samples, and must not depend on the
    /// Data layer. The event keeps that knowledge in one place.
    /// </remarks>
    public event EventHandler<ModelAutoFitRequestedEventArgs>? AutoFitRequested;

    /// <summary>
    /// When the user edits the service family directly (Advanced mode), the
    /// Kendall notation no longer describes the row, so it reads "Custom"
    /// (Phase 8K). The reverse sync stops here so it cannot overwrite the family
    /// the user just chose.
    /// </summary>
    partial void OnServiceFamilyChanged(DistributionFamily value)
    {
        // A fit writes this field under _suppressNotationSync, so only an unguarded
        // write is the user's own choice (Phase 9C, D-232).
        if (!_suppressNotationSync)
        {
            FamilyProvenance = FieldProvenance.UserSet;
        }

        // A spread that no longer applies must go, or a Normal's σ would be read as
        // Gamma's k after the switch (0.4 is a legal-looking but wrong model).
        ClearShapeParametersFor(value);

        // The visibility properties are computed, and a computed property is invisible
        // to the binding system until somebody says it changed. Without these raises the
        // user changes the family and the row keeps showing the previous family's
        // input — a silent failure that reads as a broken binding.
        RaiseDerivedSpreadNotifications();

        // A badge describes one family, so a family change retires it. The parent
        // writes the badge *after* calling ApplyFittedSpec, so the auto-fit path
        // repopulates it immediately and this only fires for a manual change.
        AutoFitBadge = null;

        // Only Advanced mode has the user editing the family directly. Outside it the
        // notation owns the family, so a family write is never a reason to disown the
        // notation.
        if (!UseAdvancedSetup || _suppressNotationSync)
        {
            return;
        }

        SelectedModel = CustomModel;
    }

    partial void OnArrivalFamilyChanged(DistributionFamily value)
    {
        // The letter and the option list are computed properties: nothing raises
        // for them when the recorded family changes. A missed raise would leave
        // a stale M/… list under a D/… row — the lie this phase removes.
        OnPropertyChanged(nameof(ArrivalLetter));
        OnPropertyChanged(nameof(ModelOptions));
        RebaseModelNotation();

        if (!UseAdvancedSetup || _suppressNotationSync)
        {
            return;
        }

        SelectedModel = CustomModel;
    }

    /// <summary>
    /// Rewrites the row's notation so its first letter matches the derived one.
    /// Without this, changing the Model section from Exponential to Deterministic
    /// would leave "M/M/1" on screen while the row's arrival letter says D.
    /// </summary>
    private void RebaseModelNotation()
    {
        if (UseAdvancedSetup || _suppressNotationSync)
        {
            return;
        }

        // A G/G/c notation asks the data to choose a family; it names no arrival
        // process, so there is no first letter to rebase. Rewriting it would erase
        // an explicit auto-fit request whenever the Model section changed.
        if (ModelNotationParser.IsGeneralModel(SelectedModel))
        {
            return;
        }

        if (string.IsNullOrEmpty(SelectedModel))
        {
            // The two-way ComboBox pushes a null SelectedItem back when its Items
            // are replaced by the family-driven ModelOptions. Re-seed a valid
            // notation instead of dereferencing the null (Phase 9C load path).
            SelectedModel = ModelOptions.FirstOrDefault() ?? "M/M/1";
            return;
        }

        var parts = SelectedModel.Split('/');
        if (parts.Length != 3)
        {
            return;
        }

        var options = ModelOptions;
        var candidate = $"{ArrivalLetter}/{parts[1]}/{parts[2]}";
        _suppressNotationSync = true;
        try
        {
            SelectedModel = options.Contains(candidate) ? candidate : options[0];
        }
        finally
        {
            _suppressNotationSync = false;
        }
    }

    /// <summary>
    /// Assigns both families as one deliberate change and marks the notation
    /// "Custom", without the notation sync reacting to any of it (Phase 8K, task 8K.4).
    /// </summary>
    /// <remarks>
    /// The single place a family is pushed onto rows that already exist. Keeping it on
    /// the row means the "Apply to all" button cannot forget the Custom step, and the
    /// suppression is scoped to this one call rather than left on the row.
    /// </remarks>
    /// <param name="arrival">Arrival family to assign.</param>
    /// <param name="service">Service family to assign.</param>
    public void ApplyFamilies(DistributionFamily arrival, DistributionFamily service)
    {
        _suppressNotationSync = true;
        try
        {
            ArrivalFamily = arrival;
            ServiceFamily = service;
            SelectedModel = CustomModel;
        }
        finally
        {
            _suppressNotationSync = false;
        }
    }

    /// <summary>
    /// Applies only a service family, leaving the engine-wide arrival family alone.
    /// Used by "Apply to all stages" after Phase 9A removed the per-stage arrival
    /// dropdown (D-229); the shorthand becomes "Custom" for the same reason as
    /// <see cref="ApplyFamilies"/>.
    /// </summary>
    /// <param name="service">Service family to assign to this row.</param>
    public void ApplyServiceFamily(DistributionFamily service)
    {
        // "Apply to all stages" and a notation shortcut are user decisions even
        // though their writes are suppressed (Phase 9C, D-232). Set provenance
        // FIRST: the panel recomputes its deviation warning from the ServiceFamily
        // PropertyChanged, so it must already see UserSet at that moment.
        FamilyProvenance = FieldProvenance.UserSet;

        _suppressNotationSync = true;
        try
        {
            ServiceFamily = service;
            SelectedModel = CustomModel;
        }
        finally
        {
            _suppressNotationSync = false;
        }
    }

    /// <summary>
    /// Restores the family and spread the data chose, clears a typed μ so 9P's
    /// resolver can use the fitted one again, and retires the deviation warning
    /// (Phase 9C, D-232). No fit is recomputed here: the winner is the one
    /// already disclosed next to the field.
    /// </summary>
    [RelayCommand]
    private void UseDataFit()
    {
        if (BestFitSpec is not { } spec)
        {
            return;
        }

        ApplyFittedShape(spec);
        FamilyProvenance = FieldProvenance.AutoFitted;
        MuValue = string.Empty;
        MuProvenance = MuProvenance.Default;
        FitOverrideWarning = null;
        MuOverrideWarning = null;
    }

    /// <summary>
    /// Applies a fitted family and its <b>spread only</b> to this row. Never touches
    /// μ (Phase 9P, D-245).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the method every load path calls: a file supplies a shape, not a
    /// service rate, and the run's μ comes from the resolver. Phase 8K's single
    /// <see cref="ApplyFittedSpec"/> wrote <c>1.0 / spec.Mean</c> unconditionally,
    /// which would have put a duration into a field that holds a rate in mean-wise
    /// mode and would have created a second μ authority alongside the fitter
    /// (seam S2).
    /// </para>
    /// <para>
    /// The fitted spec's <c>Mean</c> is deliberately discarded. It is the mean of
    /// the observed data, whereas the run's location parameter is μ and the mean
    /// is derived from it as <c>1/μ</c> (D-150). Writing the data's mean onto the
    /// row would make <c>ServiceRate</c> and <c>Mean</c> disagree and trip D-147 the
    /// first time an auto-fitted stage was run.
    /// </para>
    /// </remarks>
    /// <param name="spec">The winning fit. Its family and spread parameters are read; its mean is not.</param>
    public void ApplyFittedShape(DistributionSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        // The notation sync is suppressed because in Advanced mode a family write
        // marks the row "Custom" — which would erase the G/G/c the user just chose and
        // hide the fact that an auto-fit had run at all.
        _suppressNotationSync = true;
        try
        {
            ServiceFamily = spec.Family;
            switch (spec.Family)
            {
                // Exponential and Deterministic are fully described by their mean, so
                // there is no spread to carry across.
                case DistributionFamily.Exponential:
                case DistributionFamily.Deterministic:
                    break;
                case DistributionFamily.Normal:
                case DistributionFamily.Lognormal:
                    ServiceStdDev = FormatNumber(spec.StdDev);
                    break;
                case DistributionFamily.Gamma:
                    // Scale is derived from Mean/k, so carrying the fitted θ across
                    // would contradict the μ the run is actually configured with.
                    ServiceShape = FormatNumber(spec.Shape);
                    break;
                case DistributionFamily.Uniform:
                    // The fitted bounds are turned back into the half-width the user
                    // sees, because the bounds are derived from μ at build time.
                    ServiceSpread = spec.Min is { } lo && spec.Max is { } hi
                        ? FormatNumber((hi - lo) / 2.0)
                        : null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(spec),
                        spec.Family,
                        "The fitter returned a family this row cannot represent.");
            }
        }
        finally
        {
            _suppressNotationSync = false;
        }

        InlineError = null;
    }

    /// <summary>
    /// Applies a G/G/c auto-fit result to this row — shape <b>and</b> a blank μ —
    /// for the explicit auto-fit path only (Phase 9P, D-245).
    /// </summary>
    /// <remarks>
    /// <para>
    /// μ is the run's only location parameter, so it decides the stage's mean. If
    /// the user already set one it is kept — a typed μ is a deliberate answer and
    /// the fitter does not get to overwrite it. A blank one is filled from the fit,
    /// which is the whole reason a G/G/c stage can run at all.
    /// </para>
    /// <para>
    /// The fill is mode-aware: mean-wise the field holds a duration, so it receives
    /// <c>spec.Mean</c>; rate-wise it receives <c>1.0 / spec.Mean</c>. Phase 8K
    /// wrote the rate in both modes, which put a rate where a duration was expected.
    /// No load path may call this method — the file loader calls
    /// <see cref="ApplyFittedShape"/> and lets the resolver own μ.
    /// </para>
    /// </remarks>
    /// <param name="spec">The winning fit.</param>
    /// <param name="mode">How the μ field is interpreted (rate-wise or mean-wise).</param>
    public void ApplyFittedSpec(DistributionSpec spec, ParameterMode mode)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ApplyFittedShape(spec);

        if (string.IsNullOrWhiteSpace(MuValue) && spec.Mean > 0)
        {
            double value = mode == ParameterMode.MeanWise ? spec.Mean : 1.0 / spec.Mean;
            MuValue = value.ToString("R", CultureInfo.InvariantCulture);
            // Written after the assignment: the change handler marks a non-blank edit
            // as the user's, and this write is the fitter's, so provenance is set last
            // and wins (D-242).
            MuProvenance = MuProvenance.Fitted;
        }
    }

    /// <summary>
    /// Puts the model notation back to what it was before the change that
    /// triggered an auto-fit, so a G/G/c selection that could not be fitted does
    /// not leave the row claiming a family it never received.
    /// </summary>
    public void RevertSelectedModel() => SelectedModel = _previousModel ?? "M/M/1";

    /// <summary>
    /// Records why this row cannot be built, in the row's single inline error
    /// channel.
    /// </summary>
    /// <param name="message">Cause and remedy, phrased for the user.</param>
    public void SetInlineError(string message) => InlineError = message;

    /// <summary>
    /// Formats a fitted parameter for the row's text field. Invariant culture and
    /// the round-trip ("R") format, because the value is parsed straight back by
    /// <see cref="ConfigPanelViewModel.BuildSpec"/>; a locale decimal comma would
    /// make the row silently unbuildable on the machine that fitted it.
    /// </summary>
    private static string? FormatNumber(double? value) =>
        value is { } v && v > 0 ? v.ToString("R", CultureInfo.InvariantCulture) : null;

    /// <summary>Notation shown when the families no longer match any shorthand.</summary>
    public const string CustomModel = "Custom";

    /// <summary>
    /// Drops the spread parameter when it does not apply to the new family, so a
    /// switch from Gamma to Normal cannot silently keep a stale k and read it as σ.
    /// </summary>
    /// <param name="family">The family now selected.</param>
    private void ClearShapeParametersFor(DistributionFamily family)
    {
        if (family is not (DistributionFamily.Normal or DistributionFamily.Lognormal))
        {
            ServiceStdDev = null;
        }

        if (family != DistributionFamily.Gamma)
        {
            ServiceShape = null;
        }

        if (family != DistributionFamily.Uniform)
        {
            ServiceSpread = null;
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

    /// <summary>
    /// True while the read-only "where did this stage's μ come from" line should be
    /// shown. Pushed onto the row by
    /// <see cref="ConfigPanelViewModel.RefreshStageSourceLabels"/> rather than bound
    /// through <c>$parent[ItemsControl]</c>, which cannot resolve a typed binding from
    /// inside the extracted <c>StageRowControl</c> (Phase 8K, D-150).
    /// </summary>
    [ObservableProperty]
    private bool _isRateSourceVisible;

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

/// <summary>
/// Carries a G/G/c auto-fit request from a <see cref="StageRow"/> to the parent
/// view model (Phase 8K, task 8K.6). The row reports the intent and the stage
/// name; resolving it needs the loaded dataset, which only the panel holds.
/// </summary>
/// <param name="StageName">Name of the stage the request came from.</param>
/// <param name="Notation">The notation chosen, e.g. "G/G/2".</param>
public sealed record ModelAutoFitRequestedEventArgs(string StageName, string Notation)
{
    /// <summary>Builds the args for one stage.</summary>
    /// <param name="stageName">Name of the stage the request came from.</param>
    /// <param name="notation">The notation chosen.</param>
    /// <returns>The event arguments.</returns>
    public static ModelAutoFitRequestedEventArgs ForStage(string stageName, string notation) =>
        new(stageName, notation);
}
