namespace OpdSimulator.Data.Tests;

using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Validation;
using Xunit;

/// <summary>
/// End-to-end checks against the committed fixtures: the generated sample files
/// must load and validate cleanly, and the dirty fixture must be rejected with
/// the exact per-row issues it was built to contain. These guard the demo path.
/// </summary>
public class FixtureTests
{
    private static string RepoRoot()
    {
        // Walk up from the test bin/ dir to the OpdSimulator.sln marker (the
        // project tree is layered deeper than a single hop).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpdSimulator.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void SampleCsv_LoadsAndValidates()
    {
        string path = Path.Combine(RepoRoot(), "samples", "sample_patients.csv");
        Assert.True(File.Exists(path), $"Sample CSV not found at {path} — run scripts/make-sample-data.sh");

        var data = new CsvLoader().Load(path);

        Assert.Equal(4, data.Columns.Count);
        Assert.Equal(60, data.RowCount);
        Assert.Empty(DataValidator.ValidateReturningIssues(data));
    }

    [Fact]
    public void SampleXlsx_LoadsAndValidates()
    {
        string path = Path.Combine(RepoRoot(), "samples", "sample_patients.xlsx");
        Assert.True(File.Exists(path), $"Sample XLSX not found at {path} — run scripts/make-sample-data.sh");

        var data = new ExcelLoader().Load(path);

        Assert.Equal(60, data.RowCount);
        Assert.Empty(DataValidator.ValidateReturningIssues(data));
    }

    [Fact]
    public void DirtyFixture_IsRejected_WithEveryRowIdentified()
    {
        string path = Path.Combine(RepoRoot(), "tests", "OpdSimulator.Data.Tests", "Fixtures", "dirty_missing.xlsx");
        Assert.True(File.Exists(path), $"Dirty fixture not found at {path} — run scripts/make-sample-data.sh");

        var data = new ExcelLoader().Load(path);
        var issues = DataValidator.ValidateReturningIssues(data);

        // Data row 1 (spreadsheet row 2) is clean; each later row was built with
        // exactly one defect and must be named: row 2 missing arrival, row 3 bad
        // stage, row 4 end before start, row 5 empty row, row 6 out-of-order.
        Assert.Contains(issues, i => i.RowNumber == 2 && i.ColumnName == "arrival_time");
        Assert.Contains(issues, i => i.RowNumber == 3 && i.ColumnName == "departure_stage");
        Assert.Contains(issues, i => i.RowNumber == 4 && i.Reason.Contains("before service start"));
        Assert.Contains(issues, i => i.RowNumber == 5 && i.Reason.Contains("Empty row"));
        Assert.Contains(issues, i => i.RowNumber == 6 && i.ColumnName == "arrival_time" && i.Reason.Contains("out of order"));
        Assert.DoesNotContain(issues, i => i.RowNumber == 1); // the clean row is never flagged
    }
}