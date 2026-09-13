namespace OpdSimulator.Core.Servers;

/// <summary>
/// A single service station (one receptionist, one screening table, or one doctor).
/// </summary>
/// <remarks>
/// <see cref="Server"/> models the busy/idle state of one parallel service
/// channel at a stage. It accumulates <see cref="BusyTimeMinutes"/> so the
/// engine can later derive per-server utilisation
/// (util = busy time / operating time, see D-017). The class itself does not
/// make scheduling decisions — the engine tells it when service starts and ends.
/// </remarks>
public sealed class Server
{
    /// <summary>
    /// Creates a server with a given zero-based index within its stage.
    /// </summary>
    /// <param name="id">Zero-based index of this server within its stage.</param>
    public Server(int id)
    {
        Id = id;
    }

    /// <summary>Zero-based index of this server within its stage.</summary>
    public int Id { get; }

    /// <summary>Whether the server is currently serving a patient.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>Accumulated busy time (minutes) over the whole simulation.</summary>
    public double BusyTimeMinutes { get; private set; }

    /// <summary>Cumulative number of patients this server has completed.</summary>
    public int PatientsServed { get; private set; }

    /// <summary>Clock time the current service began (meaningful only while busy).</summary>
    public double ServiceSince { get; private set; }

    /// <summary>
    /// Marks service starting at the given clock time.
    /// </summary>
    /// <param name="now">Current simulation clock time.</param>
    public void StartService(double now)
    {
        if (IsBusy)
            throw new InvalidOperationException($"Server {Id} is already busy.");
        IsBusy = true;
        ServiceSince = now;
    }

    /// <summary>
    /// Marks service ending at the given clock time and accumulates busy time.
    /// </summary>
    /// <param name="now">Current simulation clock time.</param>
    public void EndService(double now)
    {
        if (!IsBusy)
            throw new InvalidOperationException($"Server {Id} is not busy.");
        if (now < ServiceSince)
            throw new ArgumentException($"End time {now} precedes start time {ServiceSince}.");

        BusyTimeMinutes += now - ServiceSince;
        IsBusy = false;
        PatientsServed++;
    }

    /// <summary>
    /// Fraction of the given operating window the server was busy.
    /// Used by the statistics module to derive utilisation. Asserts 0 ≤ util ≤ 1 (FR-VAL-2).
    /// </summary>
    /// <param name="operatingTimeMinutes">Length of the operating window (D-017).</param>
    /// <returns>Utilisation in [0, 1].</returns>
    public double Utilisation(double operatingTimeMinutes)
    {
        if (operatingTimeMinutes <= 0)
            throw new ArgumentException("Operating time must be positive to compute utilisation.", nameof(operatingTimeMinutes));

        double util = BusyTimeMinutes / operatingTimeMinutes;
        if (util < 0 || util > 1)
            throw new InvalidOperationException($"Server {Id} utilisation {util:F4} outside [0, 1].");

        return util;
    }
}