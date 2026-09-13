namespace OpdSimulator.Data.Tests;

using OpdSimulator.Data.Preprocess;
using Xunit;

public class TimeParserTests
{
    [Theory]
    [InlineData("8:15", 495.0)]
    [InlineData("08:15", 495.0)]
    [InlineData("08:15:30", 495.5)]
    [InlineData("8:17:30", 497.5)]
    [InlineData("0:00", 0.0)]
    [InlineData("23:59", 1439.0)]
    public void TryParse_24HourFormats(string text, double expectedMinutes)
    {
        Assert.True(TimeParser.TryParse(text, out double minutes));
        Assert.Equal(expectedMinutes, minutes, 6);
    }

    [Theory]
    [InlineData("8:15 AM", 495.0)]
    [InlineData("8:15 am", 495.0)]
    [InlineData("8:15 PM", 1215.0)]
    public void TryParse_12HourWithMeridiem(string text, double expectedMinutes)
    {
        Assert.True(TimeParser.TryParse(text, out double minutes));
        Assert.Equal(expectedMinutes, minutes, 6);
    }

    [Fact]
    public void TryParse_ExcelDayFraction_BecomesMinutes()
    {
        // 08:15 / 24 h = 0.34375 — how ClosedXML serialises an Excel time cell.
        Assert.True(TimeParser.TryParse("0.34375", out double minutes));
        Assert.Equal(495.0, minutes, 6);
    }

    [Fact]
    public void TryParse_BareNumberIsMinutesSinceMidnight()
    {
        Assert.True(TimeParser.TryParse("495", out double minutes));
        Assert.Equal(495.0, minutes, 6);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("25:99")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_InvalidValues_ReturnFalse(string text)
    {
        Assert.False(TimeParser.TryParse(text, out _));
    }
}