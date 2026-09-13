namespace OpdSimulator.Data.Tests;

using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Validation;
using Xunit;

public class DataValidatorTests
{
    private static DataSet Valid() => Make(
        new[] { "arrival_time", "departure_stage", "screening_start", "screening_end" },
        new Dictionary<string, string>[]
        {
            new() { ["arrival_time"] = "8:15", ["departure_stage"] = "Screening", ["screening_start"] = "8:20", ["screening_end"] = "8:30" },
            new() { ["arrival_time"] = "8:40", ["departure_stage"] = "Doctor", ["screening_start"] = "8:42", ["screening_end"] = "8:55" },
        });

    private static DataSet Make(string[] columns, IReadOnlyDictionary<string, string>[] rows)
        => new("mem://test", DateTime.UtcNow, columns, rows);

    [Fact]
    public void Valid_HasNoIssues()
    {
        var issues = DataValidator.ValidateReturningIssues(Valid());
        Assert.Empty(issues);
    }

    [Fact]
    public void MissingRequiredColumn_IsReported()
    {
        var data = Make(
            new[] { "departure_stage", "screening_start", "screening_end" },
            new Dictionary<string, string>[]
            {
                new() { ["departure_stage"] = "Screening", ["screening_start"] = "8:20", ["screening_end"] = "8:30" },
            });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Contains(issues, i => i.ColumnName == "arrival_time" && i.RowNumber == 0);
    }

    [Fact]
    public void UnmatchedStageColumn_IsReported()
    {
        var data = Make(
            new[] { "arrival_time", "departure_stage", "screening_start" },
            new Dictionary<string, string>[]
            {
                new() { ["arrival_time"] = "8:15", ["departure_stage"] = "Screening", ["screening_start"] = "8:20" },
            });

        var issues = DataValidator.ValidateReturningIssues(data);

        // No complete pair is present — both the missing-pair rule and the
        // unmatched-column rule fire. The unmatched rule pinpoints the culprit.
        Assert.Contains(issues, i => i.ColumnName == "screening_start");
    }

    [Fact]
    public void EmptyRow_IsReported()
    {
        var data = Make(
            new[] { "arrival_time", "departure_stage", "screening_start", "screening_end" },
            new Dictionary<string, string>[]
            {
                new() { ["arrival_time"] = "8:15", ["departure_stage"] = "Screening", ["screening_start"] = "8:20", ["screening_end"] = "8:30" },
                new() { }, // completely blank row
            });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Contains(issues, i => i.RowNumber == 2 && i.Reason.Contains("Empty row"));
    }

    [Fact]
    public void MissingCell_InRequiredColumn_IsReported()
    {
        var data = Make(
            new[] { "arrival_time", "departure_stage", "screening_start", "screening_end" },
            new Dictionary<string, string>[]
            {
                new() { ["arrival_time"] = "8:15", ["departure_stage"] = "Screening", ["screening_start"] = "8:20", ["screening_end"] = "" },
            });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Contains(issues, i => i.RowNumber == 1 && i.ColumnName == "screening_end");
    }

    [Fact]
    public void InvalidDepartureStage_IsRejected()
    {
        var data = Make(
            new[] { "arrival_time", "departure_stage", "screening_start", "screening_end" },
            new Dictionary<string, string>[]
            {
                new() { ["arrival_time"] = "8:15", ["departure_stage"] = "Laboratory", ["screening_start"] = "8:20", ["screening_end"] = "8:30" },
            });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Contains(issues, i => i.ColumnName == "departure_stage"
                                     && i.Reason.Contains("Screening"));
    }

    [Fact]
    public void OutOfOrderArrival_IsReported()
    {
        var data = Make(
            new[] { "arrival_time", "departure_stage", "screening_start", "screening_end" },
            new Dictionary<string, string>[]
            {
                new() { ["arrival_time"] = "8:40", ["departure_stage"] = "Screening", ["screening_start"] = "8:42", ["screening_end"] = "8:55" },
                new() { ["arrival_time"] = "8:15", ["departure_stage"] = "Screening", ["screening_start"] = "8:20", ["screening_end"] = "8:30" },
            });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Contains(issues, i => i.ColumnName == "arrival_time" && i.Reason.Contains("out of order"));
    }

    [Fact]
    public void ServiceEndBeforeStart_IsReported()
    {
        var data = Make(
            new[] { "arrival_time", "departure_stage", "screening_start", "screening_end" },
            new Dictionary<string, string>[]
            {
                new() { ["arrival_time"] = "8:15", ["departure_stage"] = "Screening", ["screening_start"] = "8:30", ["screening_end"] = "8:20" },
            });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Contains(issues, i => i.Reason.Contains("before service start"));
    }

    [Fact]
    public void AllIssuesCollected_NotStoppedAtFirst()
    {
        var data = Make(
            new[] { "arrival_time", "departure_stage", "screening_start", "screening_end" },
            new Dictionary<string, string>[]
            {
                new() { ["arrival_time"] = "not-a-time", ["departure_stage"] = "X", ["screening_start"] = "8:30", ["screening_end"] = "8:20" },
            });

        var issues = DataValidator.ValidateReturningIssues(data);

        // Unparseable arrival + bad departure stage + inverted service pair: all three surface.
        Assert.Equal(3, issues.Count);
        Assert.All(issues, i => Assert.Equal(1, i.RowNumber));
    }

    [Fact]
    public void Validate_ThrowsDataValidationException_WhenIssuesExist()
    {
        var data = Make(
            new[] { "arrival_time", "departure_stage", "screening_start", "screening_end" },
            new Dictionary<string, string>[]
            {
                new() { ["arrival_time"] = "", ["departure_stage"] = "Screening", ["screening_start"] = "8:20", ["screening_end"] = "8:30" },
            });

        var ex = Assert.Throws<DataValidationException>(() => DataValidator.Validate(data));
        Assert.Single(ex.Issues);
    }

    [Fact]
    public void ThreeStageFile_BlankDoctorCellsForScreeningExit_AreAccepted()
    {
        // Real clinic flow (CONTEXT §1.2): a patient who exits at Screening has
        // no doctor visit, so doctor_* cells are legitimately blank. Reception
        // and Screening cells must always be filled (everyone passes through).
        var data = Make(
            new[] { "arrival_time", "departure_stage", "reception_start", "reception_end", "screening_start", "screening_end", "doctor_start", "doctor_end" },
            new Dictionary<string, string>[]
            {
                new() { ["arrival_time"] = "8:15", ["departure_stage"] = "Screening", ["reception_start"] = "8:16", ["reception_end"] = "8:18", ["screening_start"] = "8:19", ["screening_end"] = "8:23" },
                new() { ["arrival_time"] = "8:20", ["departure_stage"] = "Doctor", ["reception_start"] = "8:21", ["reception_end"] = "8:23", ["screening_start"] = "8:24", ["screening_end"] = "8:28", ["doctor_start"] = "8:29", ["doctor_end"] = "8:35" },
            });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Empty(issues);
    }

    [Fact]
    public void BlankReceptionCellForDoctorExit_IsStillReported()
    {
        // The relaxation is directional: only stages strictly AFTER the
        // departure stage may be blank. A Doctor exit still had to pass
        // Reception, so a blank reception cell stays an issue.
        var data = Make(
            new[] { "arrival_time", "departure_stage", "reception_start", "reception_end", "screening_start", "screening_end", "doctor_start", "doctor_end" },
            new Dictionary<string, string>[]
            {
                new() { ["arrival_time"] = "8:15", ["departure_stage"] = "Doctor", ["reception_start"] = "", ["reception_end"] = "8:18", ["screening_start"] = "8:19", ["screening_end"] = "8:23", ["doctor_start"] = "8:24", ["doctor_end"] = "8:30" },
            });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Contains(issues, i => i.RowNumber == 1 && i.ColumnName == "reception_start"
                                     && i.Reason.Contains("Missing value"));
    }
}