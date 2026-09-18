# OPD Clinic Queue Simulator

A discrete-event simulation of patient flow through an Outpatient Department
(Reception → Screening → Doctor), with data-driven distribution fitting,
chi-square goodness-of-fit, and an Avalonia desktop UI.

## Quick Start

See [`docs/DEV_LAUNCH.md`](docs/DEV_LAUNCH.md) for full instructions.

```bash
git clone <repo-url>
cd opd-simulator
dotnet restore OpdSimulator.sln
dotnet run --project src/OpdSimulator.App
```

## Documentation

| File | Purpose |
|------|---------|
| [`docs/DEV_LAUNCH.md`](docs/DEV_LAUNCH.md) | Launch from a dead state |
| [`docs/USER_MANUAL.md`](docs/USER_MANUAL.md) | End-user guide |
| [`AGENTS.md`](AGENTS.md) | Coding agent instructions |
| [`docs/PRD.md`](docs/PRD.md) | Product requirements |
| [`docs/CONTEXT.md`](docs/CONTEXT.md) | Theory and domain knowledge |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | Design decision log |
| [`docs/TODO.md`](docs/TODO.md) | Task list |
| [`docs/PROGRESS.md`](docs/PROGRESS.md) | Progress log |
| [`docs/BLOCKERS.md`](docs/BLOCKERS.md) | Active blockers |
| [`docs/DEFINITION_OF_DONE.md`](docs/DEFINITION_OF_DONE.md) | Definition of done and acceptance checklist |
| [`docs/WORKFLOW_DIAGRAM.md`](docs/WORKFLOW_DIAGRAM.md) | End-to-end system/workflow diagram |
| [`docs/RESULTS_PANEL_STRUCTURE.md`](docs/RESULTS_PANEL_STRUCTURE.md) | Results-panel widget structure and grouping |

## Tech Stack

- C# on .NET 8 (LTS)
- Avalonia UI (MVVM)
- MathNet.Numerics
- ClosedXML + CsvHelper