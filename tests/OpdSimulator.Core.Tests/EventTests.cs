using OpdSimulator.Core.Events;

namespace OpdSimulator.Core.Tests;

/// <summary>
/// Tests for <see cref="Event"/> ordering: time first, then event type, then patient id.
/// </summary>
public class EventTests
{
    [Fact]
    public void CompareTo_OrdersByTime()
    {
        var early = new Event(1.0, EventType.Arrival, 1);
        var late = new Event(2.0, EventType.Arrival, 2);

        Assert.True(early.CompareTo(late) < 0);
        Assert.True(late.CompareTo(early) > 0);
    }

    [Fact]
    public void CompareTo_BreaksEqualTimeTieByEventType()
    {
        // Arrival (0) sorts before ReceptionEnd (1) at the same time.
        var arrival = new Event(5.0, EventType.Arrival, 1);
        var end = new Event(5.0, EventType.ReceptionEnd, 1);

        Assert.True(arrival.CompareTo(end) < 0);
        Assert.True(end.CompareTo(arrival) > 0);
    }

    [Fact]
    public void CompareTo_BreaksEqualTimeAndTypeTieByPatientId()
    {
        // Same time, same type → smaller patient id first.
        var p1 = new Event(5.0, EventType.ReceptionEnd, 1);
        var p2 = new Event(5.0, EventType.ReceptionEnd, 2);

        Assert.True(p1.CompareTo(p2) < 0);
        Assert.True(p2.CompareTo(p1) > 0);
    }

    [Fact]
    public void CompareTo_SameEvent_IsZero()
    {
        var a = new Event(5.0, EventType.ReceptionEnd, 1);
        var b = new Event(5.0, EventType.ReceptionEnd, 1);

        Assert.Equal(0, a.CompareTo(b));
    }
}