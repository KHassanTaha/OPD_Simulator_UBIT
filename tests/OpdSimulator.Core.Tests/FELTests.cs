using OpdSimulator.Core.Events;

namespace OpdSimulator.Core.Tests;

/// <summary>
/// Tests for <see cref="FEL"/> — dequeue must always return the earliest event,
/// with deterministic tie-breaking.
/// </summary>
public class FELTests
{
    [Fact]
    public void Dequeue_ReturnsEarliestFirst()
    {
        var fel = new FEL();
        fel.Enqueue(new Event(10.0, EventType.ReceptionEnd, 3));
        fel.Enqueue(new Event(2.0, EventType.Arrival, 1));
        fel.Enqueue(new Event(7.0, EventType.ReceptionEnd, 2));

        Assert.Equal(3, fel.Count);
        Assert.Equal(2.0, fel.Dequeue().Time);
        Assert.Equal(7.0, fel.Dequeue().Time);
        Assert.Equal(10.0, fel.Dequeue().Time);
        Assert.True(fel.IsEmpty);
    }

    [Fact]
    public void Dequeue_BreaksTimeTieDeterministically()
    {
        var fel = new FEL();
        // Same time 5.0: Arrival(0) → ReceptionEnd(1) → ScreeningEnd(2) → DoctorEnd(3) by type.
        fel.Enqueue(new Event(5.0, EventType.DoctorEnd, 2));
        fel.Enqueue(new Event(5.0, EventType.Arrival, 1));
        fel.Enqueue(new Event(5.0, EventType.ReceptionEnd, 1));
        // Same type + same time → smaller patient id first.
        fel.Enqueue(new Event(5.0, EventType.ScreeningEnd, 3));
        fel.Enqueue(new Event(5.0, EventType.ScreeningEnd, 2));

        Assert.Equal(EventType.Arrival, fel.Dequeue().Type);
        Assert.Equal(EventType.ReceptionEnd, fel.Dequeue().Type);
        Assert.Equal(2, fel.Dequeue().PatientId); // ScreeningEnd ids {2,3} → 2 first
        Assert.Equal(3, fel.Dequeue().PatientId);
        Assert.Equal(EventType.DoctorEnd, fel.Dequeue().Type);
    }

    [Fact]
    public void Peek_ReturnsEarliestWithoutRemoving()
    {
        var fel = new FEL();
        fel.Enqueue(new Event(3.0, EventType.Arrival, 1));
        fel.Enqueue(new Event(1.0, EventType.Arrival, 2));

        Assert.Equal(1.0, fel.Peek().Time);
        Assert.Equal(2, fel.Count);
    }

    [Fact]
    public void Dequeue_OnEmpty_Throws()
    {
        var fel = new FEL();
        Assert.Throws<InvalidOperationException>(() => fel.Dequeue());
    }
}