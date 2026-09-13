namespace OpdSimulator.Core.Patients;

/// <summary>
/// Represents a single patient moving through the serial stage network.
/// </summary>
/// <remarks>
/// State is intentionally minimal: the engine owns the balances of time.
/// A patient begins as a data record (id, arrival time, target stage) and
/// the engine marks service milestones on it as the simulation progresses.
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
        ArrivalTime = arrivalTime;
        StageIndex = stageIndex;
    }

    /// <summary>Unique identifier, assigned by the engine in arrival order.</summary>
    public int Id { get; }

    /// <summary>Clock time (minutes from t=0) at which the patient entered the stage queue.</summary>
    public double ArrivalTime { get; }

    /// <summary>Zero-based index of the stage this patient is waiting for / being served at.</summary>
    public int StageIndex { get; }

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
}