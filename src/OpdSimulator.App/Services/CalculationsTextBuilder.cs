namespace OpdSimulator.App.Services;

using System.Globalization;
using System.Text;
using OpdSimulator.App.Models;
using OpdSimulator.Core.Engine;

/// <summary>What a <see cref="CalculationRow"/> line represents (Phase 8N, D-165).</summary>
public enum CalculationRowKind
{
    /// <summary>A section heading that spans both columns of the dialog.</summary>
    Section,

    /// <summary>A <c>label : value</c> pair occupying both grid columns.</summary>
    Field,
}

/// <summary>
/// One line of the calculations body, structured for a two-column render
/// (Phase 8N, D-165).
/// </summary>
/// <remarks>
/// <para>
/// The dialog used to render the flat string from <see cref="CalculationsTextBuilder.Build"/>
/// inside a single monospace <c>TextBlock</c> with wrapping switched off, which
/// clipped the right-hand values and cut the buttons off. A flat string cannot
/// fix that: there are no columns to widen and no way to let one value wrap
/// without wrapping its label too.
/// </para>
/// <para>
/// These rows are the same content in the same order, split so the renderer can
/// lay each label and value out independently. The words are unchanged —
/// <see cref="CalculationsTextBuilder.Build"/> is now rendered FROM these rows, so
/// the clipboard text and the on-screen text cannot drift apart.
/// </para>
/// </remarks>
/// <param name="kind">Whether this is a section heading or a label/value field.</param>
/// <param name="Label">The heading text, or the field's label. Never carries leading spaces.</param>
/// <param name="Value">The field's value. Empty for <see cref="CalculationRowKind.Section"/>.</param>
/// <param name="IndentLevel">Nesting depth. The flat text conveyed this with leading spaces.</param>
public sealed record CalculationRow(
    CalculationRowKind Kind,
    string Label,
    string Value,
    int IndentLevel);

/// <summary>
/// Builds the plain-text "View calculations" body (Phase 8M, D-164, FR-UI-29).
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately a PURE function taking plain data and returning a string.
/// The rules it writes down — <c>ρ = λ / (c·μ)</c>, <c>utilisation = busy / elapsed</c>,
/// busy time = contribution × operating time — are the same numbers the engine used,
/// so putting them in a view model or a dialog would make them untestable. Here they
/// can be asserted headlessly, and the dialog is a renderer of this text.
/// </para>
/// <para>
/// Every figure comes from the finished <see cref="SimulationResult"/> or the
/// <see cref="SimulationParameters"/> that produced it. Nothing is recomputed from a
/// value the reader cannot see, and nothing is hardcoded per network: a two-stage run
/// prints two service rows, an eleven-bar run prints eleven utilisation rows.
/// </para>
/// </remarks>
public static class CalculationsTextBuilder
{
    /// <summary>Culture-invariant percent, e.g. 0.4231 → "42.31%".</summary>
    private static string Percent(double value) =>
        (value * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";

    /// <summary>Culture-invariant fixed-point, used for rates and times.</summary>
    /// <summary>
    /// The run mode as the user named it on the Horizon radios (D-174).
    /// </summary>
    /// <param name="mode">The recorded mode, or null when no run is recorded.</param>
    /// <returns>A user-facing label, never the enum member name.</returns>
    private static string RunModeLabel(RunMode? mode) =>
        mode switch
        {
            RunMode.ClinicDay => "Single day",
            RunMode.MultiDay => "Multi-day",
            RunMode.DiagnosticTrace => "Diagnostic trace",
            _ => "(not recorded)",
        };

    private static string Num(double value, int decimals = 4) =>
        value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>Builds the calculations body for a finished run, as flat monospace text.</summary>
    /// <remarks>
    /// Phase 8N (D-165) renders this FROM <see cref="BuildRows"/> rather than walking the
    /// data a second time. One traversal, two shapes, no chance of the clipboard text and
    /// the on-screen text disagreeing about what the run did.
    /// </remarks>
    /// <param name="result">The finished run, or null when nothing has been run yet.</param>
    /// <param name="parameters">The parameters the run was launched with, if still known.</param>
    /// <param name="sourceDescription">
    /// Where the parameters came from, in the user's own terms — e.g. "fitted from
    /// sample_patients.csv" or "entered manually". Shown verbatim so the reader can
    /// tell which of the two configuration paths (AGENTS §19) produced these numbers.
    /// </param>
    public static string Build(
        SimulationResult? result,
        SimulationParameters? parameters,
        string? sourceDescription,
        DataBindingResult? binding = null)
    {
        var rows = BuildRows(result, parameters, sourceDescription, binding);
        return rows.Count == 0 ? NoRunMessage : FlatText(rows);
    }

    /// <summary>
    /// The single-source wording shown when nothing has been simulated yet. Both the
    /// flat text and the dialog read it from here so the two cannot say different things.
    /// </summary>
    public static string NoRunMessage =>
        "No simulation has been run yet, so there are no calculations to show.\n"
        + "Configure a run and press Start Calculation.";

    /// <summary>
    /// Renders rows back into the flat, monospace, clipboard-friendly form. This is
    /// the layout <see cref="Build"/> produced before Phase 8N and is unchanged:
    /// two leading spaces, the label padded to 42, a colon, the value, a newline.
    /// The only thing not reproduced is the nesting, which is re-created as the
    /// leading spaces the flat format used.
    /// </summary>
    private static string FlatText(IReadOnlyList<CalculationRow> rows)
    {
        var text = new StringBuilder();
        foreach (var row in rows)
        {
            if (row.Kind == CalculationRowKind.Section)
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append(row.Label).Append('\n');
                text.Append(new string('-', row.Label.Length)).Append('\n');
                continue;
            }

            var label = row.IndentLevel == 0
                ? row.Label
                : new string(' ', row.IndentLevel * 2) + row.Label;
            text.Append("  ").Append(label.PadRight(42)).Append(": ").Append(row.Value).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// The same content as <see cref="Build"/>, structured for a two-column render
    /// (Phase 8N, D-165). Every word is identical to the flat form; only the shape
    /// differs, so the dialog can widen the label column and wrap the value column
    /// independently.
    /// </summary>
    /// <param name="result">The finished run, or null when nothing has been run yet.</param>
    /// <param name="parameters">The parameters the run was launched with, if still known.</param>
    /// <param name="sourceDescription">Where the parameters came from, in the user's terms.</param>
    /// <returns>Rows in document order; empty when no run exists yet.</returns>
    public static IReadOnlyList<CalculationRow> BuildRows(
        SimulationResult? result,
        SimulationParameters? parameters,
        string? sourceDescription,
        DataBindingResult? binding = null)
    {
        if (result is null)
        {
            return Array.Empty<CalculationRow>();
        }

        var text = new List<CalculationRow>();

        Section(text, "RUN CONFIGURATION");
        Field(text, "Parameter source", string.IsNullOrWhiteSpace(sourceDescription)
            ? "(not recorded)"
            : sourceDescription!);
        Field(text, "Random seed", parameters?.Seed.ToString(CultureInfo.InvariantCulture) ?? "(not recorded)");

        // D-174: the enum member name is an implementation detail ("ClinicDay"
        // is what the class is called). The user picked from three radios, so the
        // label says what they picked.
        Field(text, "Run mode", RunModeLabel(parameters?.RunMode));
        if (parameters is not null)
        {
            Field(text, "Start day", parameters.StartDay.ToString());
            Field(text, "Days generated", parameters.GeneratorDays.ToString(CultureInfo.InvariantCulture));
            if (parameters.RunMode == RunMode.DiagnosticTrace)
            {
                // Named "Duration" to match the dropdown the user set it in.
                Field(text, "Duration (arrival window)", $"{Num(parameters.HorizonMinutes, 2)} min");
            }

            if (parameters.DailyCap is int cap)
            {
                Field(text, "Daily cap", cap.ToString(CultureInfo.InvariantCulture));
            }
        }

        Section(text, "ARRIVAL PROCESS");
        var lambda = parameters?.ManualArrivalRate;
        Field(text, "Arrival rate λ", lambda is null
            ? "fitted from the loaded data file"
            : $"{Num(lambda.Value, 5)} patients/min");
        if (lambda is double l and > 0)
        {
            Field(text, "Mean inter-arrival 1/λ", $"{Num(1 / l, 3)} min");
            Field(text, "Rule", "i.i.d. inter-arrival times; sample the gap, then schedule the arrival");
        }

        // D-173: both estimates exist for a loaded file, and the run used one of
        // them. The three λ lines are the point of this block — a reader who sees
        // only the value that was used cannot tell that a second, materially
        // different estimate was available, and would assume the choice was
        // arbitrary.
        //
        // The window line reports the estimate the RUN used, which is the
        // override when the user picked a window other than the file's own. The
        // file's auto-detected figures are printed as well, under
        // "from the file", so the receipt states both the observed window and
        // the selected one instead of silently reporting the run's λ beside the
        // file's divisor. Printing the override against the file's window would
        // be worse than the defect it replaces: the reader could divide the
        // printed arrivals by the printed minutes and fail to reproduce the λ
        // printed above it.
        if (parameters is not null && binding is not null)
        {
            Field(text, "λ source", parameters.LambdaSource == LambdaSource.Window
                ? "window (arrivals ÷ operating minutes)"
                : "MLE (1 ÷ mean within-session inter-arrival gap)");

            if (binding.FittedArrivalRate is double mle and > 0)
            {
                Field(text, "λ — MLE", $"{Num(mle, 5)} patients/min");
                Field(text, "  MLE rule", "λ = 1 ÷ mean gap. Cross-session gaps are excluded: "
                    + "an inter-arrival distribution is a within-session property, so an "
                    + "overnight closure is not an inter-arrival time.");
            }

            // The run's window λ: the selected-window figure when one was carried,
            // else the binding's own. Null for the CLI, which has no Input tab.
            double? appliedWindow = parameters.WindowLambdaOverride ?? binding.WindowLambda;
            ObservationWindow? appliedFrom = parameters.SelectedWindow ?? binding.ObservedWindow;
            bool selectedDiffersFromFile = parameters.WindowLambdaOverride is double sel
                && binding.WindowLambda is double auto
                && Math.Abs(sel - auto) > 1e-12;

            if (appliedWindow is double win and > 0)
            {
                Field(text, selectedDiffersFromFile ? "λ — window (used)" : "λ — window",
                    $"{Num(win, 5)} patients/min");
                // The division is printed, not just the rule, whenever both halves
                // of it are known — a rule the reader cannot run is not a receipt.
                int arrivals = binding.DataSet?.RowCount ?? 0;
                if (appliedFrom is { } win2 && arrivals > 0)
                {
                    Field(text, "  window rule",
                        $"λ = {arrivals} arrivals ÷ {Num(win2.OperatingMinutes, 0)} operating "
                        + $"minutes across {ObservationWindowService.Describe(win2)}.");
                }
                else
                {
                    Field(text, "  window rule",
                        "λ = total arrivals ÷ operating minutes in the observation window.");
                }
            }

            if (binding.ObservedWindow is { } window)
            {
                Field(text, "Observation window",
                    $"{window.OperatingDays} operating " +
                    $"{(window.OperatingDays == 1 ? "day" : "days")} " +
                    $"({Num(window.OperatingMinutes, 0)} operating minutes)");
            }

            if (selectedDiffersFromFile && appliedFrom is { } chosen)
            {
                Field(text, "Selected window (used)",
                    $"{ObservationWindowService.Describe(chosen)} — the run used this divisor, "
                    + $"not the file's own window above");
            }

            // Divergence is about the two ESTIMATES, so it compares the run's
            // window λ against the MLE beside it — not the file's auto-detected
            // figure, which is no longer the one under discussion.
            if (binding.FittedArrivalRate is double a and > 0
                && appliedWindow is double b and > 0
                && Math.Abs(a - b) > 1e-9)
            {
                Field(text, "Estimate divergence",
                    $"{(b > a ? "Window" : "MLE")} is " +
                    $"{Math.Abs(b - a) / a * 100:F1}% " +
                    $"{(b > a ? "lower" : "higher")} than the other; the run used " +
                    $"{(parameters.LambdaSource == LambdaSource.Window ? "window" : "MLE")}.");
            }
        }

        Section(text, "SERVICE PROCESSES");
        var stageMetrics = result.StageMetrics;
        foreach (var stage in stageMetrics)
        {
            var index = stageMetrics.ToList().IndexOf(stage);
            var mu = StageServiceRate(parameters, index);
            Field(text, $"{stage.StageName} — servers c", stage.ServerCount.ToString(CultureInfo.InvariantCulture));
            Field(text, $"{stage.StageName} — rate μ per server", mu is null
                ? "fitted from the loaded data file"
                : $"{Num(mu.Value, 5)} patients/min");
            if (mu is double m and > 0)
            {
                Field(text, $"{stage.StageName} — capacity c·μ", $"{Num(stage.ServerCount * m, 5)} patients/min");
                Field(text, $"{stage.StageName} — mean service 1/μ", $"{Num(1 / m, 3)} min");
            }

            Field(text, $"{stage.StageName} — family", ServiceFamilyOf(parameters, index) ?? "(engine default: Exponential)");
        }

        Section(text, "UTILISATION");
        Field(text, "Rule", "utilisation = busy time ÷ operating time, measured over the whole run");
        Field(text, "Operating time T", $"{Num(result.OperatingTimeMinutes, 2)} min");

        // D-171: two different numbers get called "utilisation", and the
        // distinction is the whole point of the per-server block.
        //   server utilisation = busy_i / T              how busy THAT server was
        //   contribution       = busy_i / (c × T)        that server's share of the
        //                                                  stage's capacity
        // Per-server utilisations do NOT sum; contributions DO, and their sum is
        // the stage utilisation. The arithmetic is printed rather than asserted
        // because a reader in the viva who cannot reproduce the sum will not
        // believe it.
        Field(text, "Two values per server", "server utilisation = busy ÷ T; "
            + "contribution = busy ÷ (c × T). Utilisations do not sum; contributions "
            + "sum to the stage utilisation.");
        foreach (var stage in stageMetrics)
        {
            Field(text, $"{stage.StageName} — observed utilisation", Percent(stage.StageUtilisation));

            // The busy-time figure is a display derivation, not a stored metric:
            // the engine records per-server utilisation, and T is the only way back
            // to minutes. It is labelled as derived so nobody reads it as raw output.
            var perServer = stage.PerServerUtilisation;
            var serverCount = perServer.Count > 0 ? perServer.Count : stage.ServerCount;
            for (int i = 0; i < perServer.Count; i++)
            {
                Field(
                    text,
                    $"  {stage.StageName} S{i + 1} — busy (derived)",
                    $"{Num(perServer[i] * result.OperatingTimeMinutes, 2)} min"
                    + $"  (utilisation {Percent(perServer[i])})");
                Field(
                    text,
                    $"  {stage.StageName} S{i + 1} — contribution",
                    serverCount > 0
                        ? $"{Percent(perServer[i] / serverCount)} of stage capacity"
                            + $"  ({Num(perServer[i] * result.OperatingTimeMinutes, 2)} ÷ "
                            + $"({serverCount} × {Num(result.OperatingTimeMinutes, 2)}))"
                        : "(server count unknown — contribution not computable)");
            }

            if (serverCount > 0 && perServer.Count > 0)
            {
                var sum = perServer.Sum(u => u / serverCount);
                Field(
                    text,
                    $"{stage.StageName} — contributions sum",
                    string.Join(" + ", perServer.Select(u => Percent(u / serverCount)))
                    + $" = {Percent(sum)}  (matches the stage utilisation above)");
            }
        }

        Section(text, "FLOW BALANCE");
        Field(text, "Patients served", result.TotalPatientsServed.ToString(CultureInfo.InvariantCulture));
        Field(text, "Throughput", $"{Num(result.ThroughputPerMinute, 5)} patients/min");
        Field(text, "Mean queue length (time-weighted)", Num(result.AverageQueueLength, 4));
        Field(text, "Mean wait", $"{Num(result.AverageWaitMinutes, 3)} min");
        if (parameters?.PExitOverride is double pexit)
        {
            Field(text, "Exit probability p_exit", Percent(pexit));
            Field(text, "  expected share leaving", Percent(pexit));
            Field(text, "  expected share continuing", Percent(1 - pexit));
        }

        Section(text, "PER-STAGE RESULT");
        foreach (var stage in stageMetrics)
        {
            Field(text, $"{stage.StageName} — served", stage.PatientsServed.ToString(CultureInfo.InvariantCulture));
            Field(text, $"{stage.StageName} — mean wait", $"{Num(stage.AverageWaitMinutes, 3)} min");
            Field(text, $"{stage.StageName} — mean queue", Num(stage.AverageQueueLength, 4));
        }

        return text;
    }

    /// <summary>μ actually configured for a stage, preferring the per-stage rate list.</summary>
    private static double? StageServiceRate(SimulationParameters? parameters, int index)
    {
        if (parameters is null)
        {
            return null;
        }

        if (index < parameters.ServiceRates.Count && parameters.ServiceRates[index] is double configured)
        {
            return configured;
        }

        return index < parameters.ManualServiceRates.Count ? parameters.ManualServiceRates[index] : null;
    }

    /// <summary>The service distribution family configured for a stage, if any.</summary>
    private static string? ServiceFamilyOf(SimulationParameters? parameters, int index)
    {
        if (parameters is null || index >= parameters.ServiceFamilies.Count)
        {
            return null;
        }

        var family = parameters.ServiceFamilies[index]?.Family;
        return family is null ? null : family.ToString();
    }

    /// <summary>Adds a section heading row. The blank line the flat text used is
    /// implied by section boundaries in the grid render (D-165).</summary>
    private static void Section(List<CalculationRow> rows, string title) =>
        rows.Add(new CalculationRow(CalculationRowKind.Section, title, string.Empty, 0));

    /// <summary>Adds one <c>label : value</c> row.</summary>
    /// <remarks>
    /// Call sites bake the nesting into the label as leading spaces, which is how the
    /// flat monospace text indented it. Those spaces are read back here as an indent
    /// level and stripped from the label, so the dialog can indent the row with layout
    /// instead of with invisible characters that a proportional font renders wrongly.
    /// The flat renderer re-adds them, so <see cref="Build"/> is byte-identical.
    /// </remarks>
    private static void Field(List<CalculationRow> rows, string label, string value)
    {
        var trimmed = label.TrimStart();
        var indent = (label.Length - trimmed.Length) / 2;
        rows.Add(new CalculationRow(CalculationRowKind.Field, trimmed, value, indent));
    }
}
