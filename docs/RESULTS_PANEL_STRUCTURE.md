# Results Panel Structure

Widgets are grouped under headings in this order. All widgets
except the Overview metrics are toggleable via the Customise
results selector.

| Group | Widget | Data source | Toggleable |
|-------|--------|-------------|-----------|
| Overview | Patients served | `SimulationResult.Summary` | No |
| Overview | Average waiting time | `Summary` | No |
| Overview | Average service time | `Summary` | No |
| Overview | Average time in system | `Summary` | No |
| Overview | Throughput | `Summary` | No |
| Stage Performance | Per-stage metrics table | `StageMetrics[]` | Yes |
| Server Performance | Per-server utilisation | `StageMetrics[].PerServerUtilisation` | Yes |
| Statistical Validation | Input chi-square table | `ChiSquareResult[]` from fit | Yes |
| Simulation Verification | Output chi-square + histograms | `SimulationVerificationService` on `SimulationResult.Generated*Samples` | Yes |
| Analytical Validation | M/M/c comparison table | `AnalyticalValidationService`, guarded to horizon ≥ 100,000 min | Yes |
| Charts | Queue length over time | `StageMetrics[].QueueLengthSeries` | Yes |
| Charts | Waiting-time histogram | `StageMetrics[].WaitingTimeSamples` | Yes |
| Event Trace | Trace viewer | `CollectionTraceSink.Lines` | Yes |

Total toggleable widgets: 8.

Note: the Input tab (separate from the Results panel) holds
upload, preview, validation, and distribution-fit histograms.
