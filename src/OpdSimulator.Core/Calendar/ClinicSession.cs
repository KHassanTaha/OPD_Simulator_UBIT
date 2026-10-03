namespace OpdSimulator.Core.Calendar;

/// <summary>
/// One operating session of the clinic: the nth <i>open</i> day a calendar run
/// covers (FR-SIM-12).
/// </summary>
/// <remarks>
/// <para>
/// This type exists because "day" meant two different things in the engine, and
/// only one of them was the thing a user counts. The <b>clock</b> advances in
/// 1440-minute blocks, one per calendar day, whether the clinic is open or not.
/// The <b>horizon</b> is what the user typed into the Days field, and from
/// 2026-10-04 (D-199) that number counts <i>operating sessions</i>: from a
/// Saturday start, <c>Days = 4</c> covers Saturday, Monday, Tuesday and
/// Wednesday, and the Sunday block in between is skipped without being counted.
/// </para>
/// <para>
/// So a session carries both coordinates, and every per-session figure the
/// results report is indexed by <see cref="Ordinal"/> — never by
/// <see cref="BlockIndex"/>, which counts closed blocks too. Indexing a series by
/// block is what let a closed Sunday contribute a zero backlog and a zero drain
/// to the averages of every multi-day run.
/// </para>
/// </remarks>
/// <param name="Ordinal">1-based position of this session in the run's session
/// list, which is the number a user would call "day n".</param>
/// <param name="BlockIndex">0-based 1440-minute block this session occupies,
/// counting closed blocks — i.e. the value the clock arithmetic uses.</param>
/// <param name="DayOfWeek">Weekday of <paramref name="BlockIndex"/>, carried here
/// so labels such as "Day 3 (Tue)" need no second calendar lookup.</param>
public readonly record struct ClinicSession(int Ordinal, int BlockIndex, DayOfWeek DayOfWeek);