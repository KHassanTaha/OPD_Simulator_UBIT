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
/// <summary>
/// D-179: the exact blank-cell rules that let a direct-to-Doctor patient be
/// loaded instead of rejected as dirty.
/// </summary>
/// <remarks>
/// Each case is one row of the owner's validity table. The interesting ones are
/// the last two: they prove the validator is not simply forgiving blanks, but
/// using the presence of the doctor record as evidence that the patient really
/// went somewhere.
/// </remarks>
public class BypassValidityTests
{
    private static DataSet Row(
        string departure,
        bool reception = true,
        bool screening = true,
        bool doctor = true)
        => new(
            "mem://bypass-validity",
            DateTime.UtcNow,
            new[] { "arrival_time", "departure_stage", "reception_start", "reception_end",
                    "screening_start", "screening_end", "doctor_start", "doctor_end" },
            new IReadOnlyDictionary<string, string>[]
            {
                new Dictionary<string, string>
                {
                    ["arrival_time"] = "8:15",
                    ["departure_stage"] = departure,
                    ["reception_start"] = reception ? "8:16" : "",
                    ["reception_end"] = reception ? "8:18" : "",
                    ["screening_start"] = screening ? "8:19" : "",
                    ["screening_end"] = screening ? "8:22" : "",
                    ["doctor_start"] = doctor ? "8:23" : "",
                    ["doctor_end"] = doctor ? "8:28" : "",
                },
            });

    [Theory]
    // Screening exiters have no doctor visit. Reception is always recorded.
    [InlineData("Screening", true, true, false)]
    // Direct-to-doctor: no screening record, doctor pair present.
    [InlineData("Doctor", true, false, true)]
    // Screened, then seen by a doctor.
    [InlineData("Doctor", true, true, true)]
    // Seen by a doctor, doctor_end not stamped in the capture.
    [InlineData("Doctor", true, true, false)]
    public void BlankStageCells_OnADepartureRow_AreValid(string departure, bool reception, bool screening, bool doctor)
    {
        var issues = DataValidator.ValidateReturningIssues(Row(departure, reception, screening, doctor));

        Assert.True(issues.Count == 0, $"expected no issues, got: {string.Join("; ", issues.Select(i => $"{i.RowNumber}/{i.ColumnName}"))}");
    }

    [Theory]
    // The rejected half of the owner's validity table. Every combination of cells
    // below is invalid, and each names the column that refuses it.
    //
    // A Reception departure is rejected **whatever its cells hold** — including
    // the two rows where the cells would otherwise be perfectly acceptable. That
    // is the point: D-008's value check and D-180's cell check are independent
    // mechanisms, and a cell check cannot rescue a value the value check refuses.
    // `reception: true, screening: true, doctor: true` is the strongest form of
    // that claim — a complete, clean row that is still invalid, because leaving
    // at Reception is reneging and reneging is out of scope.
    [InlineData("Reception", true, false, false, "departure_stage")]
    [InlineData("Reception", true, true, true, "departure_stage")]
    [InlineData("Reception", false, true, true, "departure_stage")]
    [InlineData("Doctor", false, true, true, "reception_start")]
    [InlineData("Doctor", false, false, false, "reception_start")]
    [InlineData("Doctor", true, false, false, "screening_start")]
    public void TheOwnersValidityTable_RejectedRows_AreRefused(
        string departure, bool reception, bool screening, bool doctor, string expectedColumn)
    {
        var issues = DataValidator.ValidateReturningIssues(Row(departure, reception, screening, doctor));

        Assert.Contains(issues, i => i.ColumnName == expectedColumn);
    }

    [Fact]
    public void DoctorDepartureWithNoScreeningAndNoDoctor_IsDirty()
    {
        // The pair that must still be rejected: a patient recorded as reaching a
        // doctor who has neither a screening nor a doctor timestamp. This is not a
        // bypass — it is a row that recorded nothing, and forgiving it would let a
        // genuinely incomplete capture through as real routing.
        var issues = DataValidator.ValidateReturningIssues(Row("Doctor", reception: true, screening: false, doctor: false));

        // The blank screening cells are what fails, precisely because there is no
        // doctor record to vouch for the bypass.
        Assert.Contains(issues, i => i.ColumnName == "screening_start");
        Assert.Contains(issues, i => i.ColumnName == "screening_end");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void DoctorDepartureWithBlankReception_IsDirty_WhateverElseIsPresent(bool screening, bool doctor)
    {
        // Reception is the entry to the clinic: a row without it cannot describe a
        // patient who walked in, so no combination of later cells explains it.
        var issues = DataValidator.ValidateReturningIssues(Row("Doctor", reception: false, screening: screening, doctor: doctor));

        Assert.Contains(issues, i => i.ColumnName == "reception_start");
        Assert.Contains(issues, i => i.ColumnName == "reception_end");
    }

    [Fact]
    public void ReceptionDeparture_IsRejectedOnTheDepartureValueItself()
    {
        // Blank-cell forgiveness does not rescue a Reception departure. Leaving at
        // Reception is reneging, which D-008 puts out of scope, so the row is
        // refused on the departure_stage VALUE before any blank rule is consulted —
        // independently of the fact that its screening and doctor cells are blank.
        var issues = DataValidator.ValidateReturningIssues(Row("Reception", reception: true, screening: false, doctor: false));

        Assert.Contains(issues, i => i.ColumnName == "departure_stage");
    }

    [Fact]
    public void ScreeningExit_WithoutScreeningEnd_IsStillReported()
    {
        // The doctor blank is forgiven, but screening_end is not: that timestamp is
        // the service sample the stage's fitted μ is computed from.
        var data = new DataSet(
            "mem://screening-exit",
            DateTime.UtcNow,
            new[] { "arrival_time", "departure_stage", "screening_start", "screening_end" },
            new IReadOnlyDictionary<string, string>[]
            {
                new Dictionary<string, string>
                {
                    ["arrival_time"] = "8:15",
                    ["departure_stage"] = "Screening",
                    ["screening_start"] = "8:20",
                    ["screening_end"] = "",
                },
            });

        var issues = DataValidator.ValidateReturningIssues(data);

        Assert.Contains(issues, i => i.ColumnName == "screening_end");
    }
}
