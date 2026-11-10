namespace OpdSimulator.Data.Tests;

using OpdSimulator.Data.Fitting;
using Xunit;

/// <summary>
/// Phase 8W: the fitting unit is a scale choice, so the tests pin the divisors,
/// the rate direction and the labels that appear on screen and in the CLI.
/// </summary>
public class FittingTimeUnitTests
{
    [Fact]
    public void MinutesPerUnit_PinsEveryUnit()
    {
        Assert.Equal(1.0, FittingTimeUnits.MinutesPerUnit(FittingTimeUnit.Minutes));
        Assert.Equal(1.0 / 60.0, FittingTimeUnits.MinutesPerUnit(FittingTimeUnit.Seconds), 12);
        Assert.Equal(60.0, FittingTimeUnits.MinutesPerUnit(FittingTimeUnit.Hours));
        Assert.Equal(1440.0, FittingTimeUnits.MinutesPerUnit(FittingTimeUnit.Days));
        Assert.Equal(165.0, FittingTimeUnits.MinutesPerUnit(FittingTimeUnit.Sessions));
    }

    [Fact]
    public void RateFromPerMinute_ScalesUpForCoarserUnits()
    {
        // 0.2 patients/min is 12 per hour, 288 per day and 33 per 165-min session.
        Assert.Equal(12.0, FittingTimeUnits.RateFromPerMinute(0.2, FittingTimeUnit.Hours), 12);
        Assert.Equal(288.0, FittingTimeUnits.RateFromPerMinute(0.2, FittingTimeUnit.Days), 12);
        Assert.Equal(33.0, FittingTimeUnits.RateFromPerMinute(0.2, FittingTimeUnit.Sessions), 12);
        // 0.2 per minute is 0.00333 per second: a second is a finer unit, so a
        // per-second rate must be SMALLER, not larger.
        Assert.Equal(0.2 / 60.0, FittingTimeUnits.RateFromPerMinute(0.2, FittingTimeUnit.Seconds), 12);
    }

    [Fact]
    public void Scale_ConvertsSamples_AndLeavesMinutesUntouched()
    {
        var minutes = new[] { 30.0, 60.0, 90.0 };

        Assert.Same(minutes, FittingTimeUnits.Scale(minutes, FittingTimeUnit.Minutes));

        var hours = FittingTimeUnits.Scale(minutes, FittingTimeUnit.Hours);
        Assert.Equal(new[] { 0.5, 1.0, 1.5 }, hours);

        var sessions = FittingTimeUnits.Scale(minutes, FittingTimeUnit.Sessions);
        Assert.Equal(30.0 / 165.0, sessions[0], 12);
    }

    [Fact]
    public void Parse_AcceptsEveryTokenAndRejectsNonsense()
    {
        Assert.True(FittingTimeUnits.TryParse("hours", out var hours));
        Assert.Equal(FittingTimeUnit.Hours, hours);

        Assert.True(FittingTimeUnits.TryParse("SESSION", out var session));
        Assert.Equal(FittingTimeUnit.Sessions, session);

        Assert.False(FittingTimeUnits.TryParse("fortnights", out var fallback));
        Assert.Equal(FittingTimeUnit.Minutes, fallback);

        Assert.Equal(FittingTimeUnit.Days, FittingTimeUnits.ParseLabel("days"));
        Assert.Equal(FittingTimeUnit.Minutes, FittingTimeUnits.ParseLabel("not-a-unit"));
    }

    [Fact]
    public void Labels_StateTheDayAndSessionDefinitions()
    {
        Assert.Equal("patients/hour", FittingTimeUnits.RateLabel(FittingTimeUnit.Hours));
        Assert.Equal("patients/day (24 h)", FittingTimeUnits.RateLabel(FittingTimeUnit.Days));
        Assert.Equal("patients/session (165 min)", FittingTimeUnits.RateLabel(FittingTimeUnit.Sessions));
        Assert.Equal(" (1 day = 24 h)", FittingTimeUnits.DefinitionNote(FittingTimeUnit.Days));
        Assert.Equal(" (1 session = 165 operating minutes)", FittingTimeUnits.DefinitionNote(FittingTimeUnit.Sessions));
        Assert.Equal(string.Empty, FittingTimeUnits.DefinitionNote(FittingTimeUnit.Minutes));
    }
}
