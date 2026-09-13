namespace OpdSimulator.Core.Patients;

/// <summary>
/// Represents a single patient moving through the serial stage network.
/// </summary>
/// <remarks>
/// State is intentionally minimal: the engine owns the balances of time.
/// A patient begins as a data record (id, arrival time, target stage) and
/// the engine marks service milestones on it as the simulation progresses.
/// <see cref="AdvanceToStage"/> records movement to the next stage's queue,
/// so the same object follows the whole Reception → Screening → Doctor path
/// while <see cref="SystemArrivalTime"/> keeps the clock of the patient's very
/// first entry into the system.
/// </remarks>
public sealed class Patient
{
    /// <summary>
    /// Creates a new patient.
    /// </summary>
    /// <param name="id">Unique sequential identifier, assigned by the engine.</param>
    /// <param name="arrivalTime">Clock time (in minutes from t=0) at which the patient reached the stage queue.</param>
    /// <param name="stageIndex">Zero-based index of the service stage the patient is currently on.</param>
    public Patient(int id, double arrivalTime, int stageIndex)
    {
        Id = id;
        SystemArrivalTime = arrivalTime;
        ArrivalTime = arrivalTime;
        StageIndex = stageIndex;
    }

    /// <summary>Unique identifier, assigned by the engine in arrival order.</summary>
    public int Id { get; }

    /// <summary>Clock time (minutes from t=0) at which the patient first entered the system.</summary>
    public double SystemArrivalTime { get; }

    /// <summary>Clock time (minutes from t=0) at which the patient entered the current stage's queue.</summary>
    public double ArrivalTime { get; private set; }

    /// <summary>Zero-based index of the stage this patient is waiting for / being served at.</summary>
    public int StageIndex { get; private set; }

    /// <summary>Clock time at which service actually began, once started (null while queued).</summary>
    public double? ServiceStartTime { get; private set; }

    /// <summary>Clock time at which the patient finished service and left the system (null until then).</summary>
    public double? ServiceEndTime { get; private set; }

    /// <summary>Whether service has started for this patient.</summary>
    public bool HasStartedService => ServiceStartTime.HasValue;

    /// <summary>Whether the patient has been fully served (service ended).</summary>
    public bool HasCompletedService => ServiceEndTime.HasValue;

    /// <summary>Time this patient waited in the queue before service started (minutes).</summary>
    public double WaitTimeMinutes => HasStartedService ? ServiceStartTime!.Value - ArrivalTime : 0;

    /// <summary>Total time the patient spent in the system, queue included (minutes).</summary>
    public double SystemTimeMinutes => HasCompletedService ? ServiceEndTime!.Value - ArrivalTime : 0;

    /// <summary>
    /// Marks the moment a server begins serving this patient.
    /// </summary>
    /// <param name="now">Current simulation clock time.</param>
    public void MarkServiceStarted(double now)
    {
        if (HasStartedService)
            throw new InvalidOperationException($"Patient {Id} already started service.");
        ServiceStartTime = now;
    }

    /// <summary>
    /// Marks the moment a server finishes serving this patient.
    /// </summary>
    /// <param name="now">Current simulation clock time.</param>
    public void MarkServiceCompleted(double now)
    {
        if (HasCompletedService)
            throw new InvalidOperationException($"Patient {Id} already completed service.");
        ServiceEndTime = now;
    }

    /// <summary>
    /// Moves the patient to the next stage's queue, recording when it got there.
    /// </summary>
    /// <remarks>
    /// Resets the per-visit service milestones (they belong to the stage just
    /// left) and re-anchors <see cref="ArrivalTime"/> at the new stage queue,
    /// so wait-time bookkeeping per stage stays correct. <see cref="SystemArrivalTime"/>
    /// is preserved so the engine can measure the patient's whole journey.
    /// </remarks>
    /// <param name="stageIndex">Zero-based index of the next stage.</param>
    /// <param name="arrivalTime">Clock time (minutes from t=0) the patient joined the new stage's queue.</param>
    public void AdvanceToStage(int stageIndex, double arrivalTime)
    {
        if (arrivalTime < SystemArrivalTime)
            throw new ArgumentException($"Stage arrival {arrivalTime} precedes the patient's system arrival {SystemArrivalTime}.", nameof(arrivalTime));

        StageIndex = stageIndex;
        ArrivalTime = arrivalTime;
        ServiceStartTime = null;
        ServiceEndTime = null;
    }
}