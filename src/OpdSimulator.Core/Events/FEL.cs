namespace OpdSimulator.Core.Events;

/// <summary>
/// Future Event List — the ordered set of pending simulation events.
/// </summary>
/// <remarks>
/// Wraps <see cref="System.Collections.Generic.PriorityQueue{TElement,TPriority}"/>
/// (a binary heap, .NET 8 built-in) where each event is both element and priority.
/// Because <see cref="Event"/> implements <see cref="IComparable{T}"/>, the heap
/// always yields the earliest event first, breaking ties deterministically by
/// event type then patient id. This is the realisation of the DES "jump from
/// event to event" idea in CONTEXT §4.3.
/// </remarks>
public sealed class FEL
{
    private readonly System.Collections.Generic.PriorityQueue<Event, Event> _queue = new();

    /// <summary>Number of pending events.</summary>
    public int Count => _queue.Count;

    /// <summary>Whether no events are currently scheduled.</summary>
    public bool IsEmpty => _queue.Count == 0;

    /// <summary>
    /// Schedules a new event.
    /// </summary>
    /// <param name="evt">The event to schedule.</param>
    public void Enqueue(Event evt) => _queue.Enqueue(evt, evt);

    /// <summary>
    /// Removes and returns the earliest pending event.
    /// </summary>
    /// <returns>The event with the smallest (time, type, patient id).</returns>
    public Event Dequeue()
    {
        if (_queue.Count == 0)
            throw new InvalidOperationException("Cannot dequeue from an empty FEL.");
        return _queue.Dequeue();
    }

    /// <summary>
    /// Returns the earliest pending event without removing it.
    /// </summary>
    /// <returns>The event with the smallest (time, type, patient id).</returns>
    public Event Peek()
    {
        if (_queue.Count == 0)
            throw new InvalidOperationException("Cannot peek an empty FEL.");
        return _queue.Peek();
    }
}