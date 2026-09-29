namespace OpdSimulator.App.Services;

using OpdSimulator.App.Models;
using OpdSimulator.Core.Calendar;

/// <summary>
/// How much OPERATING time a loaded data file covers (D-172).
/// </summary>
/// <param name="OperatingDays">Count of clinic-open sessions the file covers.</param>
/// <param name="OperatingMinutes">Those sessions in minutes, each worth 165.</param>
/// <remarks>
/// Operating time, never wall-clock time. The clinic is shut overnight and at the
/// weekend, so a file that runs Monday to the following Monday spans SEVEN
/// calendar days but only SIX operating sessions, and the difference is 165
/// minutes that no patient could ever have arrived in.
/// </remarks>
public sealed record ObservationWindow(int OperatingDays, double OperatingMinutes);

/// <summary>Which observation-window preset the user picked (D-172).</summary>
public enum ObservationWindowSelection
{
    /// <summary>Use whatever the loaded file says.</summary>
    Auto,
    /// <summary>One operating session — 165 minutes.</summary>
    OneDay,
    /// <summary>Three operating sessions — 495 minutes.</summary>
    ThreeDays,
    /// <summary>One operating week (Mon–Thu + Sat) — 5 sessions, 825 minutes.</summary>
    OneWeek,
    /// <summary>Two operating weeks — 10 sessions, 1650 minutes.</summary>
    TwoWeeks,
    /// <summary>Four operating weeks — 20 sessions, 3300 minutes.</summary>
    OneMonth,
    /// <summary>A user-entered number of operating HOURS.</summary>
    Custom,
}

/// <summary>
/// Derives the data observation window from a loaded file, and resolves the
/// user's preset against it (D-172).
/// </summary>
/// <remarks>
/// <para>
/// This is INPUT-side. It says how much operating time the historical data
/// covers, which is what makes the window-based arrival rate meaningful. It has
/// no authority over how long a SIMULATION runs — that belongs to the run mode
/// and its own duration field (D-174), and mixing the two is the defect this
/// service was extracted to prevent.
/// </para>
/// <para>
/// Pure and static on purpose: the same file must always yield the same window,
/// and the result feeds both the fit panel and the run parameters, so a second
/// source of truth for it would be a second answer.
/// </para>
/// </remarks>
public static class ObservationWindowService
{
    /// <summary>Minutes in one operating session: 08:15–11:00 (D-172).</summary>
    public const double MinutesPerOperatingDay =
        ClinicCalendar.DefaultWindowEndMinutes - ClinicCalendar.DefaultWindowStartMinutes;

    /// <summary>Operating HOURS per session — 165/60 = 2.75 (D-172).</summary>
    public const double HoursPerOperatingDay = MinutesPerOperatingDay / 60.0;

    /// <summary>Dropdown labels in display order (D-172).</summary>
    public static readonly IReadOnlyList<string> SelectionOptions =
        new[] { "Auto (from file)", "1 day", "3 days", "1 week", "2 weeks", "1 month", "Custom…" };

    /// <summary>Maps a dropdown label back to its preset, defaulting to Auto.</summary>
    public static ObservationWindowSelection ParseSelection(string? label) =>
        label switch
        {
            "1 day" => ObservationWindowSelection.OneDay,
            "3 days" => ObservationWindowSelection.ThreeDays,
            "1 week" => ObservationWindowSelection.OneWeek,
            "2 weeks" => ObservationWindowSelection.TwoWeeks,
            "1 month" => ObservationWindowSelection.OneMonth,
            "Custom…" => ObservationWindowSelection.Custom,
            _ => ObservationWindowSelection.Auto,
        };

    /// <summary>The label for a preset, so the dropdown and the enum cannot drift.</summary>
    public static string SelectionLabel(ObservationWindowSelection selection) =>
        selection switch
        {
            ObservationWindowSelection.OneDay => "1 day",
            ObservationWindowSelection.ThreeDays => "3 days",
            ObservationWindowSelection.OneWeek => "1 week",
            ObservationWindowSelection.TwoWeeks => "2 weeks",
            ObservationWindowSelection.OneMonth => "1 month",
            ObservationWindowSelection.Custom => "Custom…",
            _ => "Auto (from file)",
        };

    /// <summary>
    /// Derives the observation window from a loaded binding.
    /// </summary>
    /// <param name="binding">The analysed data file, or null when nothing is loaded.</param>
    /// <returns>
    /// The detected window; null when the binding is absent, or when every
    /// session it names falls on a day the clinic is closed (D-172 ruling 4).
    /// </returns>
    public static ObservationWindow? FromFile(DataBindingResult? binding)
        => binding is null ? null : FromSessionDates(binding.SessionDates);

    /// <summary>
    /// The core rule: distinct session dates, filtered to clinic-open days.
    /// </summary>
    /// <param name="sessionDates">
    /// Distinct-or-not dates taken from the file's <c>session_date</c> column;
    /// null or empty means the file did not declare one.
    /// </param>
    /// <returns>
    /// One operating session when the file names no dates (a file without the
    /// column is a single session — the pre-8O behaviour); the counted open
    /// sessions when it does; null when it names dates but NONE of them are open.
    /// </returns>
    /// <remarks>
    /// Closed days are dropped here rather than in the validator because
    /// <c>DataBindingResult.IsUsable</c> is false whenever the validator reports
    /// ANY issue, so flagging a Friday row as a problem would reject the whole
    /// file and refuse the fit. Ruling 5 says such rows are permitted; the
    /// exclusion has to live somewhere that can exclude without rejecting.
    /// </remarks>
    public static ObservationWindow? FromSessionDates(IReadOnlyList<DateOnly>? sessionDates)
    {
        if (sessionDates is null || sessionDates.Count == 0)
            return new ObservationWindow(1, MinutesPerOperatingDay);

        int openDays = sessionDates
            .Distinct()
            .Count(date => ClinicCalendar.DefaultOpenDays.Contains(date.DayOfWeek));

        // Every session landed on a Friday or Sunday: there is no operating time
        // to divide by, and returning a zero-minute window would make the
        // window rate infinite. The caller shows "no operating sessions".
        return openDays == 0
            ? null
            : new ObservationWindow(openDays, openDays * MinutesPerOperatingDay);
    }

    /// <summary>
    /// The window for a binding under a given preset: auto-detect first, then
    /// let the preset override.
    /// </summary>
    /// <param name="binding">The analysed data file, or null when nothing is loaded.</param>
    /// <param name="selection">The user's preset.</param>
    /// <returns>The resolved window, or null when it cannot be known yet.</returns>
    public static ObservationWindow? FromFile(
        DataBindingResult? binding,
        ObservationWindowSelection selection)
        => Resolve(FromFile(binding), selection, null);

    /// <summary>
    /// A window for a user-entered number of operating HOURS (ruling 3).
    /// </summary>
    /// <param name="operatingHours">Operating hours the user typed.</param>
    /// <returns>
    /// The window, or null when the hours are absent or not positive — see
    /// <see cref="Resolve"/> for why null rather than zero.
    /// </returns>
    public static ObservationWindow? FromCustomHours(double operatingHours)
        => Resolve(null, ObservationWindowSelection.Custom, operatingHours);

    /// <summary>
    /// A one-line label for a window, for the fit panel and the run summary.
    /// </summary>
    /// <param name="window">The window to describe.</param>
    /// <returns>
    /// e.g. "6 operating days (990 operating minutes)" — minutes named too,
    /// because the window λ divides by them and a user checking the arithmetic
    /// needs the divisor in front of them.
    /// </returns>
    public static string Describe(ObservationWindow window) =>
        $"{window.OperatingDays} operating " +
        $"{(window.OperatingDays == 1 ? "day" : "days")} " +
        $"({window.OperatingMinutes:F0} operating minutes)";

    /// <summary>
    /// Resolves the window for the current selection (D-172).
    /// </summary>
    /// <param name="autoDetected">What <see cref="FromFile"/> found, if anything.</param>
    /// <param name="selection">The user's preset.</param>
    /// <param name="customOperatingHours">Operating hours entered for Custom; null otherwise.</param>
    /// <returns>The resolved window, or null when it cannot be known yet.</returns>
    /// <remarks>
    /// Returns null — rather than a zero-minute window — for a Custom value that
    /// is absent, zero or negative. The field's own validation already refuses
    /// those, so this is defence in depth; the alternative is a divide-by-zero
    /// downstream that surfaces as an infinity in the fit panel.
    /// </remarks>
    public static ObservationWindow? Resolve(
        ObservationWindow? autoDetected,
        ObservationWindowSelection selection,
        double? customOperatingHours)
    {
        switch (selection)
        {
            case ObservationWindowSelection.OneDay:
                return new ObservationWindow(1, MinutesPerOperatingDay);

            case ObservationWindowSelection.ThreeDays:
                return new ObservationWindow(3, 3 * MinutesPerOperatingDay);

            // 5 and 20, not 6 and 26: a week is the days the clinic is OPEN. The
            // old Time-span presets counted calendar days here, so "1 week" ran a
            // day longer than the clinic opens in a week (D-174).
            case ObservationWindowSelection.OneWeek:
                return new ObservationWindow(5, 5 * MinutesPerOperatingDay);

            case ObservationWindowSelection.TwoWeeks:
                return new ObservationWindow(10, 10 * MinutesPerOperatingDay);

            // "One month" is an approximation by construction: four operating
            // weeks. 4 x 5 x 165 = 3300. There is no calendar month of this
            // clinic to count, so the number is declared rather than derived.
            case ObservationWindowSelection.OneMonth:
                return new ObservationWindow(20, 20 * MinutesPerOperatingDay);

            case ObservationWindowSelection.Custom:
                if (customOperatingHours is not { } hours || !(hours > 0))
                    return null;
                // Days are floored for internal arithmetic, and the UI shows HOURS
                // for this selection — the user typed hours, so reporting "3
                // operating days" back at them for 10 hours would misstate their
                // own input (D-172 ruling 3).
                return new ObservationWindow(
                    (int)(hours / HoursPerOperatingDay),
                    hours * 60.0);

            case ObservationWindowSelection.Auto:
            default:
                return autoDetected;
        }
    }
}
