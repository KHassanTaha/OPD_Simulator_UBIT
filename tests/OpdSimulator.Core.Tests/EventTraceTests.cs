using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace OpdSimulator.Core.Tests;

using Engine = OpdSimulator.Core.Engine.Engine;

/// <summary>
/// Verifies FR-VAL-4: the engine emits a Debug-level event log where every event
/// line carries time, type, patient id, queue length and server states, and the
/// RNG draws (inter-arrival and service time) are logged.
/// </summary>
public class EventTraceTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    [Fact]
    public void Run_EveryEventLineHasTimeTypePatientQueueAndServerState()
    {
        LogEvent[] events = RunAndCapture();

        // Event lines are distinguished by the Type property (named "Type" in the trace).
        var eventLines = events.Where(e => e.Properties.ContainsKey("Type")).ToArray();
        Assert.NotEmpty(eventLines);

        Assert.All(eventLines, e =>
        {
            Assert.True(e.Properties.ContainsKey("Clock"), "event line must log the simulation time");
            Assert.True(e.Properties.ContainsKey("Type"), "event line must log the event type");
            Assert.True(e.Properties.ContainsKey("PatientId"), "event line must log the patient id");
            Assert.True(e.Properties.ContainsKey("QueueLen"), "event line must log the queue length");
            Assert.True(e.Properties.ContainsKey("ServerStates"), "event line must log the server states");
        });
    }

    [Fact]
    public void Run_LogsArrivalAndServiceRngDraws()
    {
        LogEvent[] events = RunAndCapture();

        Assert.Contains(events,
            e => e.MessageTemplate.Text.Contains("inter-arrival draw") && e.Properties.ContainsKey("Draw"));
        Assert.Contains(events,
            e => e.MessageTemplate.Text.Contains("service-time draw") && e.Properties.ContainsKey("Draw"));
    }

    private static LogEvent[] RunAndCapture()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var config = new EngineConfig(3.0, 4.0, serverCount: 1, horizonMinutes: 100, seed: 42);
        new Engine(config, new SeededRandomSource(), logger).Run();
        return sink.Events.ToArray();
    }
}