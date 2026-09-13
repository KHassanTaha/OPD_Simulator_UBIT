using OpdSimulator.Core.Calendar;

namespace OpdSimulator.Core.Tests;

/// <summary>
/// Unit tests for <see cref="ClinicCalendar"/>: weekdays, the arrival window
/// gate and the real-clock formatter (CONTEXT §1.1, §5.1).
/// </summary>
public class ClinicCalendarTests
{
    [Fact]
    public void Defaults_OpenMondayThroughThursdayAndSaturday_ClosedFridayAndSunday()
    {
        var calendar = new ClinicCalendar();

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Saturday })
            Assert.True(calendar.IsOpenDay(day), $"{day} should be open");

        foreach (var day in new[] { DayOfWeek.Friday, DayOfWeek.Sunday })
            Assert.False(calendar.IsOpenDay(day), $"{day} should be closed");
    }

    [Fact]
    public void Defaults_ArrivalWindow_Is0815To1100()
    {
        var calendar = new ClinicCalendar();

        Assert.Equal(495, calendar.WindowStartMinutes); // 08:15
        Assert.Equal(660, calendar.WindowEndMinutes);   // 11:00
        Assert.Equal(165, calendar.OpenDurationMinutes);
    }

    [Theory]
    [InlineData(0.0, true)]             // 08:15 exactly, window start
    [InlineData(100.0, true)]           // 09:55
    [InlineData(164.999, true)]         // just before 11:00
    [InlineData(165.0, false)]          // 11:00 exactly, window end exclusive
    [InlineData(200.0, false)]          // 11:20, after the window
    [InlineData(-1.0, false)]           // before the anchor
    public void IsInArrivalWindow_WithinMondayBlock_UsesOpenDuration(double minutes, bool expected)
    {
        var calendar = new ClinicCalendar(); // day 0 = Monday

        Assert.Equal(expected, calendar.IsInArrivalWindow(minutes));
    }

    [Fact]
    public void IsInArrivalWindow_FridayAndSundayBlocks_AlwaysClose()
    {
        var calendar = new ClinicCalendar(); // day 0 = Monday

        // Day 4 = Friday, day 6 = Sunday; 09:00 in that block (t = d*1440 + 45).
        Assert.False(calendar.IsInArrivalWindow(4 * 1440 + 45));
        Assert.False(calendar.IsInArrivalWindow(6 * 1440 + 45));

        // The surrounding days are open.
        Assert.True(calendar.IsInArrivalWindow(3 * 1440 + 45)); // Thursday
        Assert.True(calendar.IsInArrivalWindow(5 * 1440 + 45)); // Saturday
    }

    [Fact]
    public void StartDayOfWeek_ShiftsTheWeekdayOfEveryBlock()
    {
        var calendar = new ClinicCalendar(startDayOfWeek: DayOfWeek.Saturday);

        Assert.Equal(DayOfWeek.Saturday, calendar.DayOfWeekAt(0));
        Assert.Equal(DayOfWeek.Sunday, calendar.DayOfWeekAt(1));
        Assert.Equal(DayOfWeek.Monday, calendar.DayOfWeekAt(2));
        Assert.Equal(DayOfWeek.Friday, calendar.DayOfWeekAt(6));
        Assert.Equal(DayOfWeek.Saturday, calendar.DayOfWeekAt(7)); // wraps

        // With the Saturday anchor, the Sunday block (day 1) must be closed
        // while Monday (day 2) is open.
        Assert.False(calendar.IsInArrivalWindow(1 * 1440 + 45));
        Assert.True(calendar.IsInArrivalWindow(2 * 1440 + 45));
    }

    [Theory]
    [InlineData(0, "08:15")]
    [InlineData(150, "10:45")]
    [InlineData(165, "11:00")]
    [InlineData(1445, "08:20")] // 1440 into the run = next day's 08:15 + 5 min
    public void FormatClock_RendersWallClockTime(int minutes, string expected)
    {
        var calendar = new ClinicCalendar();

        Assert.Equal(expected, calendar.FormatClock(minutes));
    }

    [Fact]
    public void Constructor_RejectsEmptyOpenDays_InvalidWindow_AndNegativeCapInputs()
    {
        Assert.Throws<ArgumentException>(() => new ClinicCalendar(openDays: Array.Empty<DayOfWeek>()));
        Assert.Throws<ArgumentException>(() => new ClinicCalendar(windowStartMinutes: 660, windowEndMinutes: 495));
        Assert.Throws<ArgumentException>(() => new ClinicCalendar(windowStartMinutes: -1, windowEndMinutes: 660));
        Assert.Throws<ArgumentException>(() => new ClinicCalendar(windowStartMinutes: 495, windowEndMinutes: 1500));
    }
}