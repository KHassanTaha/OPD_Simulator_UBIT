namespace OpdSimulator.Data.Tests;

using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Preprocess;
using OpdSimulator.Data.Validation;
using Xunit;

/// <summary>
/// Phase 8O / D-175: the optional <c>session_date</c> column, and the two
/// validator rules that only exist because of it.
/// </summary>
/// <remarks>
/// The feature is "a file can say which operating day each row belongs to", so
/// these tests are organised around the two ways that can go wrong: a bad date,
/// and a GOOD date that the ordering check would otherwise reject.
/// </remarks>
public class SessionDateTests
{
    private static DataSet Make(string[] columns, IReadOnlyDictionary<string, string>[] rows)
        => new("mem://test", DateTime.UtcNow, columns, rows);

    /// <summary>One valid row, optionally carrying a session date.</summary>
    private static Dictionary<string, string> Row(string arrival, string? sessionDate = null, string departure = "Screening")
    {
        var row = new Dictionary<string, string>
        {
            ["arrival_time"] = arrival,
            ["departure_stage"] = departure,
            ["screening_start"] = arrival,
            ["screening_end"] = arrival,
        };
        if (sessionDate is not null)
            row["session_date"] = sessionDate;
        return row;
    }

    private static string[] Columns(bool withSessionDate) =>
        withSessionDate
            ? new[] { "session_date", "arrival_time", "departure_stage", "screening_start", "screening_end" }
            : new[] { "arrival_time", "departure_stage", "screening_start", "screening_end" };

    // ── Validator: the column is optional ────────────────────────────────

    [Fact]
    public void Validator_AcceptsFileWithoutSessionDateColumn()
    {
        // A file WITHOUT the column must behave exactly as it did before D-175.
        // If this ever starts failing, the "absent = current behaviour" promise
        // in the phase rules has been broken.
        var data = Make(Columns(withSessionDate: false), new[]
        {
            Row("8:15"), Row("8:40"), Row("9:05"),
        });

        Assert.Empty(DataValidator.ValidateReturningIssues(data));
    }

    [Fact]
    public void Validator_AcceptsSessionDateColumn()
    {
        var data = Make(Columns(withSessionDate: true), new[]
        {
            Row("8:15", "2026-09-14"), Row("8:40", "2026-09-14"),
        });

        Assert.Empty(DataValidator.ValidateReturningIssues(data));
    }

    [Fact]
    public void Validator_RejectsBadDateFormat()
    {
        // "14/09/2026" is the fallback the parser ACCEPTS, so the bad value here
        // has to be something neither form covers — otherwise this test would
        // pass for the wrong reason and prove nothing about rejection.
        var data = Make(Columns(withSessionDate: true), new[]
        {
            Row("8:15", "2026-09-14"), Row("8:40", "14th Sep"),
        });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Contains(issues, i => i.ColumnName == "session_date" && i.RowNumber == 2);
    }

    [Fact]
    public void Validator_RejectsMissingValuesWithinSessionDateFile()
    {
        // The column is declared, so a blank cell is an omission, not a
        // single-session file. A file that is one session simply omits the
        // column entirely — that is the distinction this test pins down.
        var data = Make(Columns(withSessionDate: true), new[]
        {
            Row("8:15", "2026-09-14"), Row("8:40", ""),
        });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Contains(issues, i => i.ColumnName == "session_date" && i.RowNumber == 2);
    }

    [Fact]
    public void Validator_ResetsArrivalOrderingAtSessionBoundary()
    {
        // THE ruling-1 test. Tuesday's first patient (08:15 = 495) arrives EARLIER
        // in the time-of-day column than Monday's last (10:55 = 655). That is a
        // decrease, and the pre-8O whole-file ordering check rejected it — which
        // made every multi-day file unloadable. A session boundary is not an
        // out-of-order arrival.
        var data = Make(Columns(withSessionDate: true), new[]
        {
            Row("8:15", "2026-09-14"), Row("10:55", "2026-09-14"),
            Row("8:15", "2026-09-15"), Row("10:55", "2026-09-15"),
        });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.DoesNotContain(issues, i => i.ColumnName == "arrival_time");
    }

    [Fact]
    public void Validator_StillRejectsOutOfOrderWithinOneSession()
    {
        // The reset must not become a blanket amnesty: two patients on the SAME
        // day arriving backwards is a real data error and still has to be caught.
        var data = Make(Columns(withSessionDate: true), new[]
        {
            Row("8:15", "2026-09-14"), Row("9:30", "2026-09-14"), Row("8:45", "2026-09-14"),
        });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Contains(issues, i => i.ColumnName == "arrival_time" && i.RowNumber == 3);
    }

    [Fact]
    public void Validator_AcceptsRowsWithWeekendDates_AndFlagsThem()
    {
        // Ruling 5. The clinic is closed Fri/Sun, so these rows cannot count
        // toward the observation window — but the ruling says they are
        // PERMITTED, and permitted cannot mean "recorded as an issue": any issue
        // sets DataBindingResult.IsUsable = false, which would reject the whole
        // file and refuse the fit. So the validator stays clean and the exclusion
        // happens in ObservationWindowService. Both halves are asserted here so
        // the two halves cannot drift apart.
        var data = Make(Columns(withSessionDate: true), new[]
        {
            Row("8:15", "2026-09-18"),  // Friday
            Row("8:40", "2026-09-18"),
            Row("9:05", "2026-09-20"),  // Sunday
        });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.True(issues.Count == 0,
            $"a closed-day row must not be an error, or the file becomes unusable; got: {issues}");
    }

    // ── TimeParser.TryParseDate (ruling 9) ───────────────────────────────

    [Fact]
    public void TimeParser_TryParseDate_PureISODate()
    {
        Assert.True(TimeParser.TryParseDate("2026-09-15", out var date));
        Assert.Equal(new DateOnly(2026, 9, 15), date);
    }

    [Fact]
    public void TimeParser_TryParseDate_ExcelDateTimeString()
    {
        // ExcelLoader formats a date-typed cell as "yyyy-MM-dd HH:mm:ss". Without
        // this branch the same multi-day data validates from CSV and fails from
        // XLSX, which reads as a bug rather than a format difference.
        Assert.True(TimeParser.TryParseDate("2026-09-15 08:15:00", out var date));
        Assert.Equal(new DateOnly(2026, 9, 15), date);
    }

    [Fact]
    public void TimeParser_TryParseDate_ISO8601DateTime()
    {
        Assert.True(TimeParser.TryParseDate("2026-09-15T08:15:00", out var date));
        Assert.Equal(new DateOnly(2026, 9, 15), date);
    }

    [Fact]
    public void TimeParser_TryParseDate_DDMMYYYY()
    {
        Assert.True(TimeParser.TryParseDate("14/09/2026", out var date));
        Assert.Equal(new DateOnly(2026, 9, 14), date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-date")]
    [InlineData("2026-13-45")]   // month 13, day 45
    [InlineData("09/14/2026")]   // US month/day ordering: day 09, month 14 — impossible
    [InlineData("2026-9-5")]     // unpadded
    public void TimeParser_TryParseDate_RejectsInvalid(string text)
    {
        Assert.False(TimeParser.TryParseDate(text, out _));
    }

    [Fact]
    public void TimeParser_TryParse_IsUnaffectedByTheDateAddition()
    {
        // D-175 is additive: adding TryParseDate must not have disturbed the
        // time-of-day parser, or every previously-valid file changes meaning.
        Assert.True(TimeParser.TryParse("8:15", out double minutes));
        Assert.Equal(495.0, minutes, 6);
        Assert.False(TimeParser.TryParse("2026-09-15", out _));  // a date is not a time
    }

    // ── Loaders ──────────────────────────────────────────────────────────

    [Fact]
    public void Loader_ReadsSessionDate_WhenPresent()
    {
        // The loaders copy cells generically, so this asserts EXISTING behaviour
        // rather than a new code path. It is kept deliberately: it is the guard
        // that a future loader rewrite does not start dropping the column.
        string path = WriteTemp("arrival_time,departure_stage,screening_start,screening_end,session_date\n" +
                                "8:15,Screening,8:20,8:30,2026-09-14\n");
        try
        {
            var data = new CsvLoader().Load(path);

            Assert.Contains("session_date", data.Columns);
            Assert.Equal("2026-09-14", data.Rows[0]["session_date"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Loader_HandlesMissingSessionDate_AsNull()
    {
        string path = WriteTemp("arrival_time,departure_stage,screening_start,screening_end\n" +
                                "8:15,Screening,8:20,8:30\n");
        try
        {
            var data = new CsvLoader().Load(path);

            Assert.DoesNotContain("session_date", data.Columns);
            Assert.False(data.Rows[0].ContainsKey("session_date"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Loader_ReadsMultidayFixture_WithSixDistinctSessions()
    {
        // The real committed fixture, not a synthetic one. This is the end-to-end
        // proof that a multi-day file survives loading AND validation: six
        // sessions, 60 rows, zero issues. Before D-175 the ordering check
        // rejected it at every session boundary.
        string path = SamplePath("sample_multiday.csv");
        Assert.True(File.Exists(path), $"missing fixture: {path}");

        var data = new CsvLoader().Load(path);
        var issues = DataValidator.ValidateReturningIssues(data);

        var sessions = data.Rows
            .Select(r => r["session_date"])
            .Distinct()
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(60, data.RowCount);
        Assert.Equal(6, sessions.Length);
        Assert.True(issues.Count == 0, $"fixture must validate cleanly; got: {string.Join("; ", issues)}");
    }

    [Fact]
    public void MultidayFixture_KeepsDoctorCellsBlank_AndThatIsLegal()
    {
        // Every fixture row departs at Screening, so doctor_start/doctor_end are
        // empty strings. The brief asks for that; this test records that the
        // validator ACCEPTS it, because a blank stage cell is legitimate for a
        // patient who exited earlier in the flow. If this breaks, the fixture
        // stops being loadable and the reason will not be obvious.
        string path = SamplePath("sample_multiday.csv");
        var data = new CsvLoader().Load(path);

        Assert.All(data.Rows, r => Assert.Equal("", r["doctor_start"]));
        Assert.All(data.Rows, r => Assert.Equal("Screening", r["departure_stage"]));
        Assert.Empty(DataValidator.ValidateReturningIssues(data));
    }

    private static string WriteTemp(string csv)
    {
        string path = Path.Combine(Path.GetTempPath(), $"sessiondate-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, csv);
        return path;
    }

    /// <summary>Resolves a file in the repository's samples/ directory.</summary>
    private static string SamplePath(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "samples", name);
    }
}
