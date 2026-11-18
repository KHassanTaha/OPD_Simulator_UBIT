namespace OpdSimulator.Core.Queues;

using OpdSimulator.Core.Patients;

/// <summary>
/// A single FIFO (first-in, first-out) queue per stage, holding patients waiting for service.
/// </summary>
/// <remarks>
/// This is the "single queue per stage" structure described in CONTEXT §1.2.
/// A patient is enqueued when no server is idle, and dequeued (FIFO) when a
/// server becomes free. Queue discipline = first-come, first-served, which
/// matches the OPD token system.
/// </remarks>
public sealed class Queue
{
    private readonly System.Collections.Generic.Queue<Patient> _queue = new();

    /// <summary>Number of patients currently waiting in this queue.</summary>
    public int Count => _queue.Count;

    /// <summary>Whether no patients are waiting.</summary>
    public bool IsEmpty => _queue.Count == 0;

    /// <summary>
    /// Adds a patient to the back of the queue.
    /// </summary>
    /// <param name="patient">The patient to wait.</param>
    public void Enqueue(Patient patient) => _queue.Enqueue(patient);

    /// <summary>
    /// Removes the patient at the front of the queue (has waited longest).
    /// </summary>
    /// <returns>The patient that has waited longest.</returns>
    public Patient Dequeue()
    {
        if (_queue.Count == 0)
            throw new InvalidOperationException("Cannot dequeue from an empty queue.");
        return _queue.Dequeue();
    }

    /// <summary>
    /// Patients waiting in FIFO order, as a copy for end-of-run inspection.
    /// </summary>
    /// <returns>A new array; mutating it cannot change engine state.</returns>
    /// <remarks>
    /// Called only after the FEL loop has stopped (Phase 8Z, D-224). A copy is
    /// returned rather than the backing <see cref="System.Collections.Generic.Queue{T}"/>
    /// so a consumer cannot dequeue or reorder patients behind the engine's back.
    /// </remarks>
    public Patient[] Snapshot() => _queue.ToArray();

    /// <summary>
    /// Returns the patient at the front of the queue without removing it.
    /// </summary>
    /// <returns>The patient that has waited longest.</returns>
    public Patient Peek()
    {
        if (_queue.Count == 0)
            throw new InvalidOperationException("Cannot peek an empty queue.");
        return _queue.Peek();
    }
}