using OpdSimulator.Core.Patients;
using OpdSimulator.Core.Queues;

namespace OpdSimulator.Core.Tests;

/// <summary>
/// Tests for <see cref="Queue"/> — the FIFO per-stage waiting line.
/// </summary>
public class QueueTests
{
    [Fact]
    public void NewQueue_IsEmpty()
    {
        var queue = new Queue();
        Assert.True(queue.IsEmpty);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Enqueue_FollowsFifoOrder()
    {
        var queue = new Queue();
        var p1 = new Patient(1, 0.0, 0);
        var p2 = new Patient(2, 1.0, 0);
        var p3 = new Patient(3, 2.0, 0);

        queue.Enqueue(p1);
        queue.Enqueue(p2);
        queue.Enqueue(p3);

        Assert.Equal(3, queue.Count);
        Assert.Same(p1, queue.Dequeue());
        Assert.Same(p2, queue.Dequeue());
        Assert.Same(p3, queue.Dequeue());
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public void Peek_DoesNotRemove()
    {
        var queue = new Queue();
        var p1 = new Patient(1, 0.0, 0);
        queue.Enqueue(p1);

        Assert.Same(p1, queue.Peek());
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void Dequeue_OnEmpty_Throws()
    {
        var queue = new Queue();
        Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
    }

    [Fact]
    public void Peek_OnEmpty_Throws()
    {
        var queue = new Queue();
        Assert.Throws<InvalidOperationException>(() => queue.Peek());
    }
}