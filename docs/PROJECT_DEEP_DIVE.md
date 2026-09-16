# PROJECT DEEP DIVE — OPD Clinic Queue Simulator

**Viva-preparation study guide.** Every claim below cites a file path, and where
useful a line number. If you cannot verify a claim here, it is not written. This
document grows with the project: each future sub-phase appends an update section
(see the Incremental Update Rule at the end), and the chapters land in five
sessions.

## Document Status

| Session | Chapters | State |
|---------|----------|-------|
| A | 1–6 (cover, executive summary, architecture, repo map, packages, build/run/test) | **written** 2026-09-16 |
| B | 7–9 (Core, Data, Cli) | **written** 2026-09-16 |
| C | 10–11 (App, Tests) | **written** 2026-09-16 |
| D | 12–15 (workflows, algorithms, invariants, decision summary) | awaiting session |
| E | 16–18 (viva bank, limitations, glossary) + PDF | awaiting session |

---

# 1. Cover Page

**Project:** OPD Clinic Queue Simulator — a discrete-event simulation of patient
flow through an Outpatient Department.

**Course:** Simulation & Modelling (CS-577). **Course details source:**
`src/OpdSimulator.App/CourseInfo.cs:9-31`.

**Professor:** Dr. Shaista Rais (`CourseInfo.cs:17`).

**Group members** (`CourseInfo.cs:23-31`):

| # | Member |
|---|--------|
| 1 | Taha Hassan Khan |
| 2 | Anas Shoaib |
| 3 | Hamza Wahaj |
| 4 | M. Shayan Ghouri |
| 5 | Zayan Ali |
| 6 | Yahya Arif Butt |

**Date:** 2026-09-16.

**Current state as of 2026-09-16 — phases 1 through 6c.3 of the GUI rebuild.**

The GUI rebuild (phases 1–5d on `feat/gui-rebuild`, then 6c.1–6c.3 on
`feat/milestone-6c-input-analysis-charts`) has delivered the theme + motion
contract (phase 1), nine reusable controls (phase 2), the four-tab shell
(phase 3), the configuration panel (4/4b/4c/5c/5d), the results panel (5/5c),
and the Input Analysis chart suite (6c.1–6c.3). Sub-phases 6c.4–6c.6, Phase 6
(help + presets) and Phases 7–8 are pending — see Chapter 17 (landed in Session E).

---

# 2. Executive Summary

## 2.1 What the simulator does — in plain English

A patient visits an Outpatient Department. They arrive, take a token at
**Reception** (1 server), move to **Screening** (2 servers), and — depending on
a probability `p_exit` — either leave or continue to **Doctor Consultation**
(3 servers). This is a serial three-stage queueing network
(`docs/CONTEXT.md:27-46`).

The simulator lets a user model that network and asks: *if arrivals and service
times follow these distributions, how long do patients wait, how busy are the
servers, and where is the bottleneck?* The user can either upload historical
data (an `.xlsx` or `.csv` exported from Google Sheets) or type the model
parameters directly.

Instead of approximating with time steps, the program uses **discrete-event
simulation**: it jumps from event to event, keeping a Future Event List (FEL) of
what happens next (`docs/CONTEXT.md:112-125`). This is exact and fast.

## 2.2 The problem it solves

Queueing theory gives closed-form answers for idealised queues (`M/M/c`). But a
real OPD has three stages in series, probabilistic early exits, defined opening
hours and closed days. Those violate the textbooks' assumptions, so a closed
form is not available. Simulation handles the messy reality; the analytical
models are kept as the **validation baseline** — the simulator's output for an
ideal case is compared against the M/M/c formula to prove the engine is correct
(`docs/CONTEXT.md:55-61`, PRD FR-STAT-5 `docs/PRD.md:402`).

The simulator is data-driven where the professor's brief demands it: fitted
distributions with Maximum Likelihood estimation and chi-square goodness-of-fit,
so every statistical choice is defensible in the viva.

## 2.3 Methodology

1. **Input modelling** — load data, fit candidate distributions (exponential,
   normal, lognormal, gamma, uniform) by MLE, test the fit with Pearson's
   chi-square over equal-probability bins (`docs/PRD.md:88-94, 398-414`).
2. **Discrete-event simulation** — an N-stage generic engine driven by a Future
   Event List, with a deterministic random seed (default 42) so every run
   reproduces (`docs/PRD.md:386-397`, D-035).
3. **Analytic validation** — refuse unstable configurations (any stage ρ ≥ 1)
   and compare simulation output against the analytical M/M/c values
   (`docs/PRD.md:101-104`, `docs/PRD.md:414`).
4. **GUI workflow** — an Avalonia desktop app: configure on the left, results on
   the right, and an Input Analysis tab that shows the fit before you run
   (`docs/PRD.md:124-166`).

## 2.4 Shape of the code

Eight projects split into four libraries/apps and four test suites
(`OpdSimulator.sln:6-24`):

| Project | Role |
|---------|------|
| `OpdSimulator.Core` | simulation engine, domain logic — no UI (`src/OpdSimulator.Core/`) |
| `OpdSimulator.Data` | loaders, validation, fitting, chi-square (`src/OpdSimulator.Data/`) |
| `OpdSimulator.App` | Avalonia desktop UI (MVVM) (`src/OpdSimulator.App/`) |
| `OpdSimulator.Cli` | headless entry point / subcommand dispatch (`src/OpdSimulator.Cli/`) |
| `tests/*` | one xUnit suite per project (Core, Data, Cli, App) |

---

# 3. System Architecture

## 3.1 The four projects and their dependencies

The solution file declares eight projects (`OpdSimulator.sln:8-24`). Dependency
edges are declared only in `.csproj` `ProjectReference` elements — there is no
other coupling.

| Project | ProjectReferences | Packages (own csproj) |
|---------|-------------------|------------------------|
| Core | — (none) | MathNet.Numerics 5.0.0, Serilog 4.4.0 (`src/OpdSimulator.Core/OpdSimulator.Core.csproj:10-12`) |
| Data | — (none) | ClosedXML 0.105.1, CsvHelper 33.1.0, MathNet.Numerics 5.0.0 (`src/OpdSimulator.Data/OpdSimulator.Data.csproj:10-13`) |
| App | Core, Data | Avalonia 11.3.3 + Desktop + Fluent, CommunityToolkit.Mvvm 8.4.2, LiveCharts 2.0.5, Serilog + sinks (`src/OpdSimulator.App/OpdSimulator.App.csproj:13-26`) |
| Cli | Core, Data | Serilog + Console + File sinks (`src/OpdSimulator.Cli/OpdSimulator.Cli.csproj:11-19`) |

Both App and Cli depend on Core and Data; Core and Data have **no project
references at all**. That is the whole dependency story.

## 3.2 Dependency direction — ASCII diagram

```
                      ┌──────────────────────────┐
                      │  OpdSimulator.Core       │  Engine, FEL, Server,
                      │  (math + domain, no UI)  │  Patient, Trace, Calendar
                      └────────────┬─────────────┘
                                   ▲ ▲
                 ┌─────────────────┘ └─────────────────┐
                 │                                     │
   ┌─────────────┴─────────────┐       ┌───────────────┴──────────────┐
   │  OpdSimulator.Data        │       │  OpdSimulator.App           │
   │  loaders, fitting, χ²     │       │  Avalonia UI (MVVM)          │
   │  (no project references)  │       │  references Core + Data      │
   └─────────────┬─────────────┘       └───────────────┬──────────────┘
                 │                                     │
                 │   ┌──────────────────────┐         │
                 └──►│  OpdSimulator.Cli    ├─────────┘
                     │  headless entry point│
                     └──────────┬───────────┘
                                │
                     ┌──────────▼──────────┐
                     │  tests/* (xUnit)    │  Core.Tests · Data.Tests ·
                     │                     │  Cli.Tests · App.Tests
                     └─────────────────────┘
```

Key points to memorise for the viva:

- **Core never references App.** Core has only two packages: MathNet.Numerics
  and Serilog (`Core.csproj:10-12`). There is no `Avalonia` string anywhere in
  Core. That is what "UI-free" means in practice, and it is why Core can be
  unit-tested headlessly.
- **Data is a leaf too.** It reads files and produces statistics but depends on
  nothing we wrote (`Data.csproj:10-13`). Core and Data are siblings, not a
  stack — fitting does not need the engine, the engine does not need fitting.
- **App and Cli are the composition roots.** Both reference Core + Data; App
  adds Avalonia and charts on top, Cli adds Serilog sink configuration. One
  engine + one data layer, two front ends.
- **Tests depend on their target, and only through public APIs** — except
  App.Tests, which additionally uses `InternalsVisibleTo` to reach non-public
  members (`App.csproj:33`).

## 3.3 Why Core is UI-free (the viva answer)

Three reasons, in increasing order of importance:

1. **Testability.** The simulation loop, the FEL ordering and the stability
   refusal are logic that must be provable from a terminal
   (`tests/OpdSimulator.Core.Tests/`). A UI dependency would force every engine
   test to spin up a window.
2. **Reproducibility.** The engine's only randomness source is injectable
   (`IRandomSource`); nothing about a UI thread can leak into a run. The GUI
   runs the engine on a **background thread** (PRD FR-UI-3, `docs/PRD.md:154-155`),
   which is only safe if the engine owns its own thread affinity.
3. **The dependency rule is one-directional by construction.** `App → Core`
   is a compile-time fact. The reverse direction cannot exist because Core has
   no App reference — a reviewer can check `Core.csproj:10-12` and see exactly
   two packages. Milestone 4's trace sink (`ITraceSink`) is the only
   engine-facing observer, and it is defined in Core
   (`src/OpdSimulator.Core/Trace/ITraceSink.cs`) — the App implements it, never
   the reverse.

The parked M5 GUI used a different architecture (M5-B controls); it was
disposed and rebuilt under D-086 because the first attempt conflated view and
logic. The rebuild rule — Core stays pure — is documented in
`docs/M5_FAILURES.md` and D-086.

---

# 4. Repository Map

## 4.1 Top level

| Path | Purpose |
|------|---------|
| `OpdSimulator.sln` | The solution: 8 projects (`OpdSimulator.sln:8-24`) |
| `global.json` | Pins the .NET SDK feature band (8.0.100, `global.json:3-4`) |
| `AGENTS.md` | Rules for the coding agent (conventions, docs discipline) |
| `README.md` | Short human-readable overview + quick start |
| `VIVA_ANSWERS.md` | Root-level viva Q&A scratchpad (`VIVA_ANSWERS.md`) |
| `appsettings.template.json` | The only committed config; copy to `appsettings.json` |
| `.gitignore` | Never tracks bin/obj/logs/secrets/real data |
| `docs/` | All living documentation (specs, log, traceability) |
| `samples/` | Committed demo data (`sample_patients.{xlsx,csv}`, `sample_3stage_clinic.csv`) |
| `scripts/` | Launchers (`run.sh`/`run.ps1`), sample generator, `make-sample-data.sh` |
| `src/` | The four source projects |
| `tests/` | The four test projects |
| `scripts/sample-data-generator/` | Standalone fixture generator (NOT in the sln) |

## 4.2 `src/` folders

| Folder | One-line purpose |
|--------|------------------|
| `src/OpdSimulator.Core/Calendar/` | Clinic opening-hours model (`ClinicCalendar.cs`) |
| `src/OpdSimulator.Core/Distributions/` | RNG abstraction + exponential sampler |
| `src/OpdSimulator.Core/Engine/` | DES loop, config, result records, stability refusal |
| `src/OpdSimulator.Core/Events/` | `Event`, `EventType`, the FEL priority queue |
| `src/OpdSimulator.Core/Patients/` | Patient record + routing |
| `src/OpdSimulator.Core/Queues/` | FIFO queue used by every stage |
| `src/OpdSimulator.Core/Servers/` | Server model + idle-selection policies |
| `src/OpdSimulator.Core/Stages/` | Stage spec, topology, network |
| `src/OpdSimulator.Core/Trace/` | Milestone-4 deterministic event trace |
| `src/OpdSimulator.Data/Loaders/` | CsvLoader / ExcelLoader / factory |
| `src/OpdSimulator.Data/Validation/` | Per-row validator + issues |
| `src/OpdSimulator.Data/Preprocess/` | Times, inter-arrival, service times, `p_exit` |
| `src/OpdSimulator.Data/Fitting/` | 5 fitters + chi-square + bins |
| `src/OpdSimulator.Data/Parameters/` | Rate-vs-mean mode validator |
| `src/OpdSimulator.Data/Export/` | Result exporter |
| `src/OpdSimulator.App/Assets/` | **Theme.axaml**, Motion.axaml, ControlStyles.axaml, ChartTheme.axaml, fonts, logos |
| `src/OpdSimulator.App/Controls/` | Nine reusable controls (ValidatedField … DataPreviewTable) |
| `src/OpdSimulator.App/ViewModels/` | MainViewModel + panel/field/chart VMs |
| `src/OpdSimulator.App/Services/` | Coordinator, data analysis, fits, charts, crash reporter, presets |
| `src/OpdSimulator.App/Views/` | MainWindow + panels + welcome card + demo |
| `src/OpdSimulator.App/Models/` | Run-mode, parameters, binding result, fit report |
| `src/OpdSimulator.Cli/Commands/` | One file per subcommand (simulate-params, verify, fit, …) |

## 4.3 The rule for what belongs where

> **Core = computation with no I/O side effects and no UI. Data = file + stats.
> App = presentation (MVVM). Cli = headless orchestration. Tests = one per
> project. If code imports Avalonia, it lives in App. If code only does math, it
> lives in Core. If it reads a spreadsheet, it lives in Data. Nothing depends on
> App except App.Tests.** (Architecture section, AGENTS.md §4; D-086.)

The one deliberate exception: `App.csproj:33` grants `InternalsVisibleTo` to
`OpdSimulator.App.Tests`, so headless UI tests can reach internal machinery.
Core has no such affordance (`Core.csproj` declares nothing) — its surface is
public API only.

---

# 5. Package Inventory

All versions are exact pins, not ranges (D-026). The complete package list is
the union of the four `.csproj` files. LiveCharts was dropped in Phase 1 and
re-pinned in 6c.1 (D-116); Markdig is **not** present — the in-program guide
uses a hand-rolled renderer (D-082).

## 5.1 By project

### `OpdSimulator.Core` (`Core.csproj:10-12`)

| Package | Version | Purpose |
|---------|---------|---------|
| MathNet.Numerics | 5.0.0 | Distributions, CDF/quantiles, chi-square p-values |
| Serilog | 4.4.0 | Structured event logging (Debug trace in the engine) |

Core needs exactly two packages because the exponential sampler is hand-rolled
(inverse CDF) and the FEL uses the BCL's `PriorityQueue` — see Chapter 13.

### `OpdSimulator.Data` (`Data.csproj:10-13`)

| Package | Version | Purpose |
|---------|---------|---------|
| ClosedXML | 0.105.1 | Read `.xlsx` (Google Sheets export) |
| CsvHelper | 33.1.0 | Read `.csv` fallback |
| MathNet.Numerics | 5.0.0 | Distribution instances + chi-square CDF |

### `OpdSimulator.App` (`App.csproj:18-26`)

| Package | Version | Purpose |
|---------|---------|---------|
| Avalonia | 11.3.3 | UI framework (XAML, MVVM bindings) |
| Avalonia.Desktop | 11.3.3 | Desktop platform integration (X11/Wayland/Win32) |
| Avalonia.Themes.Fluent | 11.3.3 | Fluent control themes (restyled by Theme.axaml) |
| CommunityToolkit.Mvvm | 8.4.2 | `ObservableObject`, `RelayCommand`, `[ObservableProperty]` |
| LiveChartsCore.SkiaSharpView.Avalonia | 2.0.5 | Charts (Input Analysis + results) |
| Serilog + Sinks.Console 6.1.1 + Sinks.File 7.0.0 | 4.4.0 | 3-sink logging per AGENTS §12.1 |

The App also embeds `docs/USER_MANUAL.md` as a resource (`App.csproj:37-40`) —
the guide's canonical source.

### `OpdSimulator.Cli` (`Cli.csproj:11-14`)

| Package | Version | Purpose |
|---------|---------|---------|
| Serilog | 4.4.0 | Logging core |
| Serilog.Sinks.Console | 6.1.1 | Metrics table + refusal line to stdout/stderr |
| Serilog.Sinks.File | 7.0.0 | Rolling `app-*.log` + error-only file |

### Test projects (all four use the same trio)

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.NET.Test.Sdk | 17.6.0 | `dotnet test` host |
| xunit | 2.4.2 | Test framework (D-005) |
| xunit.runner.visualstudio | 2.4.5 | VS runner interop |
| coverlet.collector | 6.0.0 | Code coverage collection |
| Avalonia.Headless + Avalonia.Headless.XUnit | 11.3.3 | UI tests without a display (App.Tests only) |
| Avalonia | 11.3.3 | Types for the headless session (App.Tests only) |

## 5.2 Why each choice (alternatives considered)

| Package | Why it won | Alternatives rejected |
|---------|-----------|------------------------|
| **Avalonia 11.3.3** | Only mainstream .NET XAML framework with genuinely supported Linux desktop; MVVM-native; one codebase for Linux + Windows (D-002). Pinned to 11.3.3 not 12.1.2 — 12.x bundled analyzers need Roslyn 4.14, newer than the .NET 8 SDK's compiler, producing `CS9057` warnings that violate the zero-warning policy (D-026). | .NET MAUI (no official Linux desktop — rejected), Uno Platform (heavier, WinUI-oriented — rejected) |
| **CommunityToolkit.Mvvm 8.4.2** | Standard MVVM toolkit: source generators turn plain fields into observable properties with near-zero boilerplate, keeps VMs readable for the viva. | Hand-rolled `INotifyPropertyChanged` (repetitive across 10+ VMs — rejected) |
| **MathNet.Numerics 5.0.0** | Cross-platform, pure managed, documented; provides CDFs needed for expected-bin counts and the χ² p-value. The *calling* code is still hand-written so it can be defended line-by-line (D-003). | Meta.Numerics (smaller ecosystem — rejected), fully hand-rolled math (unbounded scope — rejected) |
| **ClosedXML 0.105.1** | MIT-licensed `.xlsx` reader; the demo data comes from Google Sheets (`.xlsx`) so this is the primary loader (D-004). | EPPlus (paid licence for commercial use — rejected), NPOI (heavier — rejected) |
| **CsvHelper 33.1.0** | MIT, robust escaping/quoting for the `.csv` fallback — the zero-dependency twin used in terminal workflows (D-045/D-004). | Hand-rolled CSV parser (quoting edge cases — rejected) |
| **Serilog 4.4.0 + sinks** | Structured logging with console + rolling file + error-file sinks in one config (D-021). Crash logs are the viva's debugging evidence (AGENTS §12.6). | Microsoft.Extensions.Logging (no Serilog-level structured sinks — replaced), plain file writes (no envelope/rolling — rejected) |
| **LiveChartsCore.SkiaSharpView.Avalonia 2.0.5** | MVVM-native charts; 2.0.5 is the latest *stable* of the 2.0.x line and its Avalonia ≥ 11.0.0 contract matches our 11.3.3 exactly; re-pin matches the parked M6 branch's proven pair (D-116). | 2.1.x pre-release (unstable — rejected), OxyPlot (visually dated — rejected), ScottPlot (overkill — rejected), hand-rolled Skia (too slow to defend — rejected) |
| **xUnit 2.4.2 + SDK** | Default, lightweight, great `dotnet test` + CI integration (D-005). | NUnit, MSTest (no benefit here — rejected) |
| **Avalonia.Headless / .Headless.XUnit** | Let the App.Tests instantiate real Avalonia controls on a headless session — essential because the original M5 GUI was disposed partly for testability (D-086, D-089 Wayland constraint). | Subprocess screenshotting (slow, OS-dependent — rejected) |
| **Markdig — absent** | The guide ships without a markdown renderer; a small hand-rolled parser meets the guide's needs and keeps the dependency surface minimal (D-082). | Markdig (functional but unnecessary; superseded by D-082) |

## 5.3 The zero-warning pin contract

Every csproj pins exact versions. This is a deliberate contract (D-026):

1. Restore is reproducible — a fresh clone gets identical assemblies.
2. Version creep cannot silently break the validated build.
3. The viva's "external libraries" disclosure is the union of five csproj files
   (four projects + App.Tests headless packages).
4. Any future major bump (e.g. Avalonia 12/13) is a **reviewed, documented
   change**, not an accidental side effect of `dotnet restore`.

---

# 6. Build, Run, Test

This chapter gives the one-page mental model. The authoritative, verified
step-by-step is `docs/DEV_LAUNCH.md` — cross-reference, do not duplicate.

## 6.1 What the SDK pin means

`global.json:3-4` pins SDK `8.0.100` with `rollForward: latestFeature` (D-013).
Consequence: any installed 8.0.1xx SDK works (the dev machine runs 8.0.131,
`DEV_LAUNCH.md:6`), but a machine with only an 8.0.4xx band rolls forward to
the newest 8.0.x — and a machine with no 8.0.x SDK refuses to build loudly.
This is intentional: build failures on the wrong SDK are preferable to silent
version drift.

## 6.2 The three commands

| Command | What it does here |
|---------|-------------------|
| `dotnet restore OpdSimulator.sln` | Downloads the exact pinned packages into NuGet cache (first run needs internet; `DEV_LAUNCH.md:65-72`) |
| `dotnet build OpdSimulator.sln -c Debug` | Compiles 8 projects. **Zero-warning policy**: any warning is treated as an error in review (`DEV_LAUNCH.md:94-98`) |
| `dotnet test OpdSimulator.sln` | Runs all four test suites. As of 6c.3: **278 green** — Core 85, Data 58, Cli 35, App 100 (`DEV_LAUNCH.md:6` gives the 278 total and the per-project catalogue runs across `DEV_LAUNCH.md:177-256`) |

Before *any* commit the gate is build + test both passing (AGENTS §11.3).

## 6.3 Run paths

| Path | Command | Notes |
|------|---------|-------|
| GUI | `dotnet run --project src/OpdSimulator.App` | Avalonia window; on Linux needs X11/fontconfig libs (`DEV_LAUNCH.md:23`) |
| CLI | `dotnet run --project src/OpdSimulator.Cli -- <subcommand> …` | e.g. `simulate-params --lambda 3 --mu 4 --servers 1 --horizon 10000 --seed 42` (`DEV_LAUNCH.md:112-134`) |
| Convenience | `scripts/run.sh` / `scripts/run.ps1` | Wrappers around the App launch (`DEV_LAUNCH.md:157-160`) |

## 6.4 Config and logs — the one-time copy step

`appsettings.template.json` is the only committed config (D-027). A fresh clone
must `cp appsettings.template.json appsettings.json` once (`DEV_LAUNCH.md:56-63`).
The local `appsettings.json` is gitignored. Logs land in `logs/`:
`app-YYYYMMDD.log` (rolling, 7 days), `errors-YYYYMMDD.log` (warnings+),
`crash-YYYYMMDD.log` (unhandled exceptions) (`DEV_LAUNCH.md:429-437`).

## 6.5 Verification discipline

`DEV_LAUNCH.md` records the exact commands actually executed, per milestone,
with the OS and date ("verified on Ubuntu 24.04, SDK 8.0.131"). The dead-state
rule (AGENTS §10.4) requires deleting all `bin/`/`obj/` and re-running the
documented path before a milestone is marked done. Windows verification is
recorded as TBD (`DEV_LAUNCH.md:6`).

---

# 7. Core Deep Walk (`OpdSimulator.Core`)

**30 files** — the simulation engine and every data structure it touches.
No UI, no I/O beyond `System.IO` for trace files; the project reference rule is
**Core knows nothing about Data, Cli, or App** (ch. 3). Every public member is
XML-documented (AGENTS §3). Determinism, the trace, and the ρ ≥ 1 refusal are
the three properties a viva answer must be able to justify (D-035, D-033,
D-007/D-015, D-034).

| Folder | Files | Owns |
|--------|-------|------|
| `Engine/` | 5 | The DES loop, config, the unstable-system refusal, result bundles |
| `Events/` | 3 | `Event`, `EventType`, the FEL priority queue |
| `Queues/` | 1 | The FIFO `Queue<T>` with queue-length sampling |
| `Patients/` | 1 | Patient identity + wait-time bookkeeping |
| `Servers/` | 4 | The server model + idle-server selection policies |
| `Stages/` | 3 | Topology, per-stage specs, ρ computation (D-007, D-015) |
| `Calendar/` | 1 | The clinic-day model (D-051) |
| `Distributions/` | 3 | RNG abstraction + deterministic exponential sampler |
| `Trace/` | 9 | The event trace sink feature (D-055…D-058) |

## 7.1 Engine/Engine.cs — the DES loop

The heart. Purpose: run a network of serial stages against a stream of
exponential arrivals and collect time-integrated statistics, deterministically.
Class remarks (`Engine.cs:17-44`) spell out the **event-invariant** design:
the loop is generic over N stages and the trace and the statistics are two
independent observers of the same event stream (D-058).

Three constructors (`Engine.cs:69-103`): the network one takes
`ITraceSink? traceSink` plus a `IServerSelectionPolicy`; the single-stage
one is legacy convenience for M1 tests (D-049). Three `Run` overloads:

| Overload | Line | What it runs |
|----------|------|--------------|
| `Run()` | `Engine.cs:109-122` | Legacy single-stage M/M/1 |
| `Run(topology, seed, horizon, …)` | `Engine.cs:141-152` | N-stage network, minutes horizon |
| `Run(topology, calendar, generatorDays, …)` | `Engine.cs:186-201` | Clinic-day model, block-relative days (D-051) |

All three funnel into **`RunCore`** (`Engine.cs:203-375`) — single pass over
the future event list:

1. `FEL.Dequeue()` → process at earliest time; `gate.AdvanceTo(clock)`
   updates per-stage time-integrated queue area and appends a `QueueSample`
   per stage for the charts.
2. Gate admission check: **only during the calendar arrival window**; outside
   the window arrivals are still drawn but dropped (gated demand, FR-SIM-6).
3. Switch on `event.Type` → `HandleArrival` / `StartService` /
   `HandleServiceEnd`.

`HandleArrival` (`Engine.cs:377-437`): reject late arrivals, pick an idle
server (`_stageServerSelectors`), else enqueue; schedule the next arrival
inside the window. `StartService` (`Engine.cs:439-468`): draws the service
time from `_serviceSources[stage]`, records samples, schedules the End event.
`HandleServiceEnd` (`Engine.cs:470-529`): calls `EndService`, then the
routing decision — an exit draw (`p_exit`) sends the patient into the patient
total, otherwise `RouteToNextStage` (`Engine.cs:531-562`). Unstable stages
throw `UnstableSystemException` before the loop starts (`RunCore` guard,
D-034).

`EmitTrace` / `EmitRngDraw` / `EmitRngDrawIfDrawn` (`Engine.cs:576-603`):
the engine emits the *same* `TraceEvent` stream regardless of the sink —
`sink?.Write` and an internal per-run collector (`CollectedEvents`) both
consume it, so the trace and the metrics cannot disagree (D-058).

The inner **`CalendarGate`** (`Engine.cs:617-726`) owns the day model:
`StopTime` (day-0 window end), `TryAdmit(clock)` (window check),
`NoteServiceEnd(clock)` (day rollover), `AdvanceTo` (queue-area stepping),
`OperatingTimeMinutes` (first-arrival → last-service-end).
**Note `_stageWaitSamples`/`_stageQueueSeries`** (`Engine.cs:229-244`) —
the chart buffers; waiting-time samples per stage and queue-length series
per stage, collected for the GUI, part of `SimulationResult` after the run.

> **Viva questions (Engine.cs).** 1) Why does the loop never use a timer —
> only `FEL.Dequeue()`? (DES jumps clock to next event, never polls.)
> 2) How do we guarantee the trace and the statistics say the same thing?
> (Both consume the same event stream at the same point, D-058.) 3) Where is
> the ρ ≥ 1 refusal raised? (`UnstableSystemException` before the loop, D-034)

## 7.2 Engine/EngineConfig.cs

Purpose: validate a single-stage M/M/1 config and present it to
`SimulationResult`. Public surface (`EngineConfig.cs:27-76`): gets/sets for
`Lambda`, `Mu`, `ServiceDistribution` (a `MathNet.Numerics.Distributions` type),
`Servers`, `Seed`, `Horizon`; `Rho` property (`EngineConfig.cs:69`) computes
λ/(c·μ); `Validate()` (`EngineConfig.cs:76-89`) throws for λ ≤ 0, μ ≤ 0,
servers < 1, and ρ ≥ 1. The ρ ≥ 1 check is duplicated per-stage in
`NetworkTopology.Validate` for the network path.

## 7.3 Engine/UnstableSystemException.cs

Purpose: fail loud instead of letting a queue grow without bound (D-034).
Contains the `UnstableStage` record (`UnstableSystemException.cs:6`) —
`Name`, `Lambda`, `Mu`, `Servers`, `Rho`. Exception constructors
(`UnstableSystemException.cs:24-43`) build a list either from a config or
from `IEnumerable<UnstableStage>`; `BuildMessage` (`UnstableSystemException.cs:60-71`)
renders `'<name>' is unstable: λ=…, μ=…, c=… → ρ=… ≥ 1`. The CLI shows the
message verbatim on its clean refusal line; the GUI shows it in the error
banner (D-112, M5 result flow).

> **Viva:** Why throw, not warn, when ρ ≥ 1? (λ ≥ cμ ⇒ mean queue → ∞; the
> M/M/c closed forms assume ρ < 1, so the numbers would be meaningless and the
> Lq formula would divide by 0 — CONTEXT §2.3.)

## 7.4 Engine/SimulationResult.cs

Purpose: the run's output bundle + the chart sample stream. The `QueueSample`
record (`SimulationResult.cs:10`) — `TimeMinutes`, `StageIndex`, `QueueLength`
— backs the queue-length chart. `SimulationResult` (`SimulationResult.cs:21-71`)
holds `PatientsArrived`, `PatientsServed`, `AvgWaitTime`, `SystemTimeMinutes`
(total time-in-system across patients), the per-stage `StageMetrics[]`, queue
samples, waiting-time samples, and `Stages` (the topology used). `SystemTimeSeconds`
convenience divides by 60.

## 7.5 Engine/StageMetrics.cs

Purpose: per-stage statistics without per-server detail. `StageMetrics`
(`StageMetrics.cs:14-55`): `SystemArrivals`, `Served`, `AvgWait`, `AvgService`,
`AvgQueueLength`, `MaxQueueLength`, `Utilisation` (per-server average),
`PerServer`. `PerServer` derives→ from simulation; historical per-server lives
in Data (CONTEXT §5.7). The engine fills it via `StageAccumulator` internals
in `RunCore`.

## 7.6 Events/Event.cs

Purpose: one future list entry. Construction (`Event.cs:23-28`) takes
`Time`, `Type`, `PatientId`, `Stage`; the **sorting contract lives in
`CompareTo`** (`Event.cs:44-58`): time → event type → patient id — the
three-level tie-break the golden trace depends on (D-033). `ToString`
(`Event.cs:61`) prints `[t=…] patient X: <Type>` for logs.

## 7.7 Events/EventType.cs

Purpose: the closed set of event kinds (`EventType.cs:14-27`). **Critical
implementation detail:** the enum's underlying values are *significant* —
`Arrival = 0`, `ReceptionEnd = 1`, `ScreeningEnd = 2`, `DoctorEnd = 3`.
Stage `i`'s completion event is the enum value `i + 1`, which is exactly how
`Engine.EndEventTypeForStage` (`Engine.cs:564-568`) maps `StageIndex` →
`EventType` without a lookup table. This is why Reception must be stage 0
(ClinicStageOrder, ch. 8).

> **Viva:** Why does the enum hardcode only the three clinic stages if the
> engine is N-stage generic? (Generic N is carried by `stage: int` on each
> event; the enum labels only the clinic's canonical stages for readability —
> the first three; D-006.)

## 7.8 Events/FEL.cs

Purpose: the Future Event List — a thin wrapper over
`PriorityQueue<Event, Event>` (`FEL.cs:14-51`) with `Enqueue`, `Dequeue`,
`PeekTime`, `Count`, and a debug-only peek of min. **The comparison is
forwarded to `Event.CompareTo`** — so `PriorityQueue`'s "popped item" ordering
is exactly the D-033 contract. FEL is the only data structure the loop reads.

## 7.9 Queues/Queue.cs

Purpose: FIFO queue with time-sampling for statistics. `Queue<T>`
(`Queue.cs:14-51`): `Enqueue`, `Dequeue`, `Count`, `Peek` + `RemainingTime`
(average time-in-queue for formed waiting-time totals). The waiting-time
bookkeeping is done in `StartService` (`Engine.cs:447-455`) via
`Patient.WaitTimeMinutes`.

## 7.10 Patients/Patient.cs

Purpose: identity + time bookkeeping across stages. `Patient`
(`Patient.cs:14-103`): `Id`, `SystemArrivalTime`, `StageIndex`; time fields
are private-set — service/wait times are *derived* from the frame times
recorded by `MarkServiceStarted` (`Patient.cs:65-70`) and
`MarkServiceCompleted` (`Patient.cs:76-81`), never stored independently, so
wait + service always sums to system time. `WaitTimeMinutes`
(`Patient.cs:56`), `SystemTimeMinutes` (`Patient.cs:59`),
`AdvanceToStage` (`Patient.cs:94-103`). Single-file, single-class — trivially
oral-defensible.

## 7.11 Servers/Server.cs

Purpose: one server. `Server` (`Server.cs:14-83`): `ServerId`, `IsBusy`,
`PatientsServed`, `BusyTimeMinutes`, `ServiceSince`; `StartService`
(`Server.cs:43-49`) sets busy + busy-time start; `EndService` (`Server.cs:55-65`)
accumulates busy time; `Utilisation = busy/operatingTime` (`Server.cs:73-83`)
— **the operating-time denominator comes from the caller** (calendar gate),
never the window, so close-time overtime can't push utilisation ≥ 1
(D-018, CONTEXT §5.7).

> **Viva:** Why does `Server` have `Utilisation` taking an operating-time
> argument instead of computing it internally? (The engine knows first-arrival
> → last-service-end; server doesn't. History-derived utilisation uses the
> same denominator — CONTEXT §5.7.)

## 7.12 Servers/IServerSelectionPolicy.cs

Purpose: decouple "which idle server gets the next job". One method,
`SelectIdleServer(IReadOnlyList<IServer> idleServers)` (`IServerSelectionPolicy.cs:19-28`);
returns the chosen index. The policy is injected into the engine, so
assignment is testable in isolation.

## 7.13 Servers/RandomIdleSelection.cs

Purpose: the production policy — **random among idle** (D-017/D-050). The
M1 prototype picked the first idle server (lowest ID), which *systematically
skews the first server hot* and drifts from a fair M/M/c; random-among-idle
has no such bias (D-050 records the fix; `LowestIdSelection` remains only for
the regression test that proves the bug is gone). The random draw uses the
engine's RNG, so `SeededRandomSource` keeps the run reproducible.

## 7.14 Servers/LowestIdSelection.cs

Purpose: **test-only** reproduction of the M1 behaviour. `LowestIdSelection`
(`LowestIdSelection.cs:17-22`) returns index 0 of the idle list whenever any
server is idle. Its presence is deliberate: the negative test
`EngineTests.Run_NetworkSymmetricServers2_LowestIdPolicy_RecreatesImbalance`
proves the imbalance is due to the policy, not the engine (D-050).

## 7.15 Stages/StageSpec.cs

Purpose: immutable per-stage configuration. `StageSpec` (`StageSpec.cs:12-42`):
`Name`, `ServerCount` (≥ 1), `ServiceRate` (per-server μ), and validation in
the ctor. Used to build `Stage` objects; no dependencies on other Core types.

## 7.16 Stages/Stage.cs

Purpose: runtime stage: the complaint queue + its servers. `Stage`
(`Stage.cs:14-67`): `Spec`, `Servers` (list), `Queue` (a `Queue<Patient>`),
`HasIdleServer` (`Stage.cs:67`). `CreateStage(player)` on
`NetworkTopology` (`NetworkTopology.cs:103-104`) is the factory the engine
uses to instantiate stages with the selection policy.

## 7.17 Stages/NetworkTopology.cs

Purpose: the serial network: `Lambda0` (external rate) + ordered `StageSpec`s
+ optional `ExitProbability` after the second-to-last stage
(`NetworkTopology.cs:38-53`). **`EffectiveArrivalRate`** (`NetworkTopology.cs:72-81`)
is where the routing product rule lives (D-007/D-015):
λᵢ = λ₀ · Πⱼ₌₀..ᵢ₋₁ (1 − exitProbabilityⱼ). `RhoFor` (`NetworkTopology.cs:88-95`)
divides by cᵢ·μᵢ; `Validate` (`NetworkTopology.cs:114-126`) throws
`UnstableSystemException` with per-stage detail if any ρ ≥ 1 — the FR-VAL-1
refusal. `CreateSingleStage` (`NetworkTopology.cs:137-138`) builds the
no-exit M/M/c case for M1-style runs.

> **Viva:** Why is λ_doctor = λ₀·(1 − p_exit) but λ_screening = λ₀?
> (Exit only exists after Screening, so only downstream demand is thinned;
> Reception and Screening see full external load. A second exit after the
> final stage would factor in the same product formula.)

## 7.18 Calendar/ClinicCalendar.cs

Purpose: encode the real clinic (CONTEXT §1.1) as a block-relative day model
(D-051). Key constants and properties:

| Member | Line | Meaning |
|--------|------|---------|
| `MinutesPerDay = 1440` | `ClinicCalendar.cs:32` | One day block on the absolute clock |
| `DefaultWindowStartMinutes = 8*60+15` | `ClinicCalendar.cs:35` | Arrival window opens 08:15 |
| `DefaultWindowEndMinutes = 11*60` | `ClinicCalendar.cs:38` | Arrival window closes 11:00 |
| `DefaultOpenDays` | `ClinicCalendar.cs:41-44` | Mon–Thu + Sat (CONTEXT §1.1) |
| `OpenDurationMinutes` | `ClinicCalendar.cs:92` | 165 min window length |
| `DayOfWeekAt(dayIndex)` | `ClinicCalendar.cs:100-105` | `(StartDayOfWeek + dayIndex) mod 7` |
| `IsOpenDay` | `ClinicCalendar.cs:112` | Weekday membership test |
| `IsInArrivalWindow(t)` | `ClinicCalendar.cs:121-132` | Open day AND within window |
| `FormatClock(t)` | `ClinicCalendar.cs:140-150` | Renders wall-clock HH:mm |

**`t = 0` is the start of the day-0 arrival window** (`ClinicCalendar.cs:83` —
the block containing t = 0; 08:15 by default). Day `d` occupies the seconds
`[d·1440, (d+1)·1440)` of the absolute clock, and the weekday of a block is
`(StartDayOfWeek + d) mod 7` — Friday and Sunday simply have no window, so the
gate admits nobody (closed days are *passed over*, never *skipped*. The
calendar turns "Monday 08:15" from CONTEXT into arithmetic on a single linear
clock — no `DateTime`, so the DES loop stays pure and deterministic.

## 7.19 Distributions/IRandomSource.cs

Purpose: the *only* way the engine obtains randomness — `NextDouble()`
(`IRandomSource.cs:19-24`). Everything that draws goes through this interface,
so a `SeededRandomSource` or a `TraceRandomSource` slips underneath with zero
changes to the engine (D-035, D-057).

## 7.20 Distributions/SeededRandomSource.cs

Purpose: deterministic `System.Random` wrapper. `DefaultSeed = 42`
(`SeededRandomSource.cs:15`); `SetSeed` (`SeededRandomSource.cs:29-32`)
re-arms the generator — the seed-42 regression test (served 29892,
wait 0.724) depends on this. `NextDouble` forwards to `Random.NextDouble`.
System.Random is sequential, cheap, and reproducible on both Linux and
Windows — the viva can defend it as "deterministic by construction, good
enough for a teaching simulator" (D-035).

## 7.21 Distributions/ExponentialSampler.cs

Purpose: inverse-CDF exponential sampling. `Sample(rate)`
(`ExponentialSampler.cs:18-36`): `-ln(U)/λ` with **`U` clamped to
`double.Epsilon`** so `ln(0)` can never produce `Infinity`. No MathNet call —
a 3-line closed form, exactly what the viva formula requires
(CONTEXT §4.4: `X = −ln(U)/λ`).

> **Viva:** Why clamp `U` to `double.Epsilon`, not 0.0001?
> (We must not bias the tail; Epsilon is the smallest representable
> positive double, so the clamp only fires when the RNG truly returned 0.)

## 7.22 Trace/ITraceSink.cs

Purpose: the trace feature's seam (D-055): `Write(TraceEvent)` + `Flush()`.
The engine holds an *optional* sink — the trace is a **first-class sink
feature**, not a Serilog side-effect. Test sinks capture events for
assertion; the `trace` CLI command uses a `TextWriterTraceSink` with a
file-only logger (D-059).

## 7.23 Trace/TraceEvent.cs

Purpose: a plain data record for one row of the trace
(`TraceEvent.cs:31-81`, D-056): `Clock`, `Type` (`TraceEventType`),
`Stage`, `PatientId`, plus semantically-nullable fields (`QueueLength`,
`WaitTime`, `ServiceTime`, `RngValue`, `RngType`). The schema is fixed
as "five patient rows + one RNG row" (D-056): two Arrival variants
(t0030/t0031), StartService, EndService (t0035?), Route, Exit, and RngDraw.
**The same event object is both written to the sink and consumed by the
internal collector** — that single object is why trace and stats agree.

## 7.24 Trace/TraceEventType.cs

Purpose: the trace row vocabulary (`TraceEventType.cs:13-32`): `Arrival`,
`StartService`, `EndService`, `Route`, `Exit`, `RngDraw` (`Rng`),
`QueueLength` (a `State` marker), `System` (open/close bookends). The
`Rng`-family rows record *what the RNG produced and for what* — the trace
proves the simulation's randomness is fully audited (D-056).

## 7.25 Trace/TraceLevel.cs

Purpose: the verbosity ladder **Events < State < Rng** (`TraceLevel.cs:16-36`).
Default `State` includes intermediate queue-length rows; `Events` only the
five patient rows; `Rng` adds the RNG draw rows. **The engine always emits
the identical `TraceEvent` stream regardless of level — filtering happens in
`TraceFormatter`, never in the engine** — so level choice can't silently
change the simulation (it can't: it's display-only).

## 7.26 Trace/TraceClock.cs

Purpose: render the simulation clock as a wall clock with seconds. Static
`Format(double t, double realStartMinutes = 08:15)` (`TraceClock.cs:23-38`):
t = 0 maps to the day-0 window start (08:15 default, following the clinic day
model), wall time wraps at 24 h, and seconds are computed by **truncation —
`(int)(fraction × 60)`, never round** (`TraceClock.cs:35`), so a value of
1.345 minutes reads `08:16:20`, not `08:16:21` — keeps trace files
byte-stable across machines and runtimes.

## 7.27 Trace/TraceFormatter.cs

Purpose: the only place a `TraceEvent` becomes text. `Format`
(`TraceFormatter.cs:50-81`): fixed, invariant-culture string; D-054's
golden-byte rule means the text is part of the test contract — a formatting
change is a deliberate, test-driven change. Every numeric is formatted with
an explicit `CultureInfo.InvariantCulture`.

## 7.28 Trace/TraceRandomSource.cs

Purpose: `IRandomSource` that *wraps* another RNG and emits an RngDraw
`TraceEvent` per draw — without consuming any extra randomness
(`TraceRandomSource.cs:27-64`, D-057). It asks the wrapped source for
`NextDouble()` and *uses that same value*, so wrapping changes **nothing**
about the run — the trace observes, never perturbs. `NextDouble` draws
diagnostic state before each consumer draw, and `TraceRandomSource` carries
the `bool`-marker history (`RngType`) so the trace reader can see which
family (arrival vs service vs exit) drew.

## 7.29 Trace/TextWriterTraceSink.cs

Purpose: write trace rows to a `TextWriter` (`TextWriterTraceSink.cs:14-43`):
`Write` formats one row + newline; `Flush` flushes the writer. Used by the
CLI `trace` command — the file IS the deliverable for the viva (D-059).

## 7.30 Trace/NullTraceSink.cs

Purpose: no-op sink (`NullTraceSink.cs:11-29`) — the default when no sink is
configured; lets the engine always call `sink.Write` without null-checks.
`IsEnabled => false` is the reason the engine's `EmitTrace` uses
`tracelSink_?.Write` and skips work when no sink exists.

> **Chapter 7 take-away for the viva:** every mechanism in the engine
> (ρ refusal, RNG, tie-breaks, the operating-time denominator, the trace
> level ladder) exists because SOMETHING in the requirements forced it
> (FR-VAL-1, D-035, D-033, D-018, D-056). Defence = name the requirement.

# 8. Data Deep Walk (`OpdSimulator.Data`)

**30 files** — load, validate, pre-process, fit, test, and export the
uploaded data. **Core is not referenced and Core never references Data** —
the flow goes: user file → `DataLoader` → `DataSet` (raw) → `DataValidator`
→ cleaned `DataSet` → `InterArrivalCalculator`/`ServiceTimeCalculator` →
fitters → `ChiSquareResult` → fed to the engine by the caller.

| Folder | Files | Owns |
|--------|-------|------|
| `Loaders/` | 5 | CSV/XLSX → `DataSet` (raw, opaque rows) |
| `Validation/` | 3 | The validator + issue list + exception |
| `Preprocess/` | 9 | Time parsing, stage-pair discovery, p_exit, inter-arrival/service calculation |
| `Fitting/` | 10 | MLE/MoM fitters, bins, chi-square test, factory |
| `Parameters/` | 2 | Rate-wise vs mean-wise mode, soft-mode validator |
| `Export/` | 1 | Normalised CSV export |

## 8.1 Loaders/IDataSource.cs

Purpose: the loader seam — `Load()` returns a `DataSet`;
`Id`/`DisplayName` for messages (`IDataSource.cs:11-31`). A loader is moved
by `DataLoaderFactory`; tests inject fake data sources straight in.

## 8.2 Loaders/DataSet.cs

Purpose: the raw table — rows as `IReadOnlyDictionary<string, string>`,
keyed by column name (**cells stay strings**; parsing happens downstream)
(`DataSet.cs:12-47`). `ColumnNames` + `Rows` is the whole surface. Keeping
cells as strings means the validator can report *raw* values verbatim and
no parse decision is locked in early.

## 8.3 Loaders/CsvLoader.cs

Purpose: read CSV via **CsvHelper** (D-004). `Load` (`CsvLoader.cs:16-54`):
parses header row, then every data row into a dict, `MissingFieldFound`/
`BadDataFound` set to null so malformed rows surface as *data issues*, not
thrown exceptions. Culture is fixed to `CultureInfo.InvariantCulture`.

## 8.4 Loaders/ExcelLoader.cs

Purpose: read the first worksheet of an `.xlsx` via **ClosedXML** (D-004).
`Load` (`ExcelLoader.cs:15-74`): takes the first sheet, reads each `IXLCell`
as raw text (`GetFormattedString`), builds the same dict-per-row contract as
`CsvLoader`. `DataLoaderFactory` routes `.xlsx`. Only the first sheet is
read (PRD FR-UI-20 rule enforced at the loader).

## 8.5 Loaders/DataLoaderFactory.cs

Purpose: extension-based dispatch (`DataLoaderFactory.cs:10-37`): `.xlsx` →
`ExcelLoader`, `.csv` → `CsvLoader`, anything else → `ArgumentException`.
Three lines of switch logic — viva-trivial.

## 8.6 Validation/ValidationIssue.cs

Purpose: one human-readable finding (`ValidationIssue.cs:10-20`):
`RowNumber` (null for file-level), `Column`, `Reason`; `ToString` renders
`Row N, column 'x': reason` or `Column 'x': reason`. **Errors and warnings
are the same record** — the severity lives in the validator's issue list
per rule.

## 8.7 Validation/DataValidationException.cs

Purpose: throwable full of issues (`DataValidationException.cs:11-35`).
`Message` = `"... but validation found N issue(s)"`; `Issues` is the list.
The CLI's `VerifyCommand` and the data path in the App surface every issue
to the user; a designed message (never a bare stack trace) keeps the refusal
clean (D-037).

## 8.8 Validation/DataValidator.cs

Purpose: the M2 gatekeeper (`DataValidator.cs:34-183`). Two public entries:
`ValidateReturningIssues(dataSet)` (`DataValidator.cs:42`) returns every
issue (empty = valid) and is the API the CLI uses; `Validate(dataSet)`
(`DataValidator.cs:136`) throws `DataValidationException` when any issue
exists. Both share the same single walk over the rows (`DataValidator.cs:63-72`
onward), so the two callers can never disagree. Rules (each a named,
single-purpose check — testable and defensible):

| Rule | Rationale |
|------|-----------|
| Required columns: `arrival_time`, `departure_stage`, ≥1 stage pair | The engine needs them all (D-052) |
| No empty rows | Cleanliness |
| No blank cells | Every cell is either a value or an anomaly |
| Blank `<stage>_start/_end` when patient didn't reach that stage | Expected for exited patients — blank allowed *only* according to `ClinicStageOrder` (a patient exited at Screening has NO Doctor times) |
| Each present time parses via `TimeParser` | Otherwise garbage-in |
| `departure_stage ∈ {Screening, Doctor}` — **Reception rejected as a data anomaly** | CONTEXT §5.4; the validator is deliberately *stricter* than the "warn and exclude" D-008 — the M2 kickoff B1 rule takes precedence, and the split is recorded (D-038, and the `NOTE` in the validator's header) |
| `arrival_time` monotonically non-decreasing | A queue replay must not jump backwards |
| `end ≥ start` per stage | Service time must be ≥ 0 |

The validator returns the issue list (`IReadOnlyList<ValidationIssue>`) — no
wrapper type. It is the same git-committed source the GUI and CLI both
consume — one gatekeeper.

> **Viva:** Why does the validator REJECT `departure_stage = Reception`
> instead of excluding the row like D-008 says? (Two rules touch the same
> column: D-008 says warn-and-exclude for p_exit; the M2 kickoff says a
> patient leaving at Reception is out of scope and must block the load. Both
> are honoured: D-038 keeps the load-time refusal; the p_exit *calculator*
> ignores nothing because the row never gets that far. The split is
> intentional and logged.)

## 8.9 Preprocess/TimeParser.cs

Purpose: turn wall-clock strings into minutes-since-midnight
(`TimeParser.cs:21-71`). Accepted formats (D-039, `TimeParser.cs:23-31`):

| Input | Interpretation |
|-------|----------------|
| `H:mm`, `HH:mm`, `H:mm:ss`, `HH:mm:ss` | 24-hour clock |
| `h:mm tt` (e.g. `8:15 AM`) | 12-hour clock |
| bare number `< 1` | **Excel fraction of a day** (0.5 = noon) |
| bare number `≥ 1` | minutes since midnight (495 = 08:15) |

Implemented with `DateTime.ParseExact` (not `TimeSpan`, whose parser rejects
`AM/PM` and can't express "fraction of a day"). See the comment block
`TimeParser.cs:15-21` for the import-context reasoning.

## 8.10 Preprocess/StagePair.cs

Purpose: a `record struct` (`Preprocess/StagePair.cs:9`) connecting one
stage's `_start` + `_end` columns — `Name`, `StartColumn`, `EndColumn`.
It's what `StagePairDetector` yields: the stage-column contract downstream
UIs display.

## 8.11 Preprocess/StagePairDetector.cs

Purpose: discover stage pairs by **naming convention** — a column ending in
`_start`/`_end` (case-insensitive) paired by stem (`StagePairDetector.cs:22-56`).
Unmatched columns are preserved in `StageColumnSet` so nothing is silently
dropped. `Detect` returns pairs in file order — the pair list IS the stage
order the rest of the pipeline uses.

## 8.12 Preprocess/ClinicStageOrder.cs

Purpose: the canonical order `Reception → Screening → Doctor`
(`ClinicStageOrder.cs:21`). `FlowIndex(name)` returns the index or
`int.MaxValue` for unknown (so downstream loops skip unknowns cleanly).
The engine's `EventType` numbering (ch. 7.7) mirrors this order.

## 8.13 Preprocess/InterArrivalCalculator.cs

Purpose: inter-arrival times from a validated data set. `Compute(data)`
(`InterArrivalCalculator.cs:20-31`): sorts by arrival time, takes
consecutive differences, returns `n−1` times (the first patient has no
predecessor). Feeding `n−1` (not `n`) inter-arrival times to the exponential
fitter is the correct MLE pairing.

## 8.14 Preprocess/ServiceTimeCalculator.cs

Purpose: per-stage service times. `Compute(stagePair, rows)` — for each
patient who has *both* start and end at the stage (i.e., reached that stage),
`end − start` in minutes (`ServiceTimeCalculator.cs:22-51`). Patients who
exited earlier have no times — the calculator just skips them (the validator
already guaranteed the blanks are legal).

## 8.15 Preprocess/PExitCalculator.cs

Purpose: estimate `p_exit` from `departure_stage`. **The D-008 rule lives
here** (`PExitCalculator.cs:22-53`): `p_exit = Screening / (Screening +
Doctor)`; **Reception rows counted but excluded** from both the numerator
and denominator — a Reception departure is reneging, out of scope (D-008).
`PExitResult` carries `PExit` + counts.

## 8.16 Preprocess/PExitResult.cs

Purpose: the estimator's output bundle (`Preprocess/PExitResult.cs:11-16`):
`PExit`, `ScreeningCount`, `DoctorCount`, `ReceptionCount`.  The GUI's
"estimated from data" line and the CLI's `VerifyCommand` p_exit report both
read this.

## 8.17 Fitting/IDistributionFitter.cs

Purpose: the fitting seam (`Fitting/IDistributionFitter.cs:13-26`):
`Fit(double[] data)` returns `FittedDistribution`. The engine consumes a
fitted distribution's sampler; the chi-square consumes its CDF. Test
fixtures inject fakes.

## 8.18 Fitting/FittedDistribution.cs

Purpose: a fitted continuous distribution **plus the math the tests and
charts need**. `FittedDistribution` (`Fitting/FittedDistribution.cs:27-75`)
captures `LateX`, parameters, `Mean`, `StdDev`, and — the D-042 design —
**CDF/inverse-CDF as delegates**, because `IContinuousDistribution` from
MathNet has CDF but no inverse and the sampler needs `InverseCDF(U)`.
`AIC` (`FittedDistribution.cs:43`) computes Akaike's Information Criterion
for cross-family comparison; `ParametersText` renders e.g.
`λ = 0.2500` for the results panel.

## 8.19 Fitting/ExponentialFitter.cs

Purpose: MLE exponential fit (`Fitting/ExponentialFitter.cs:18-33`):
**λ̂ = 1/mean(data)** — the closed-form MLE (CONTEXT §3.4). Zero-mean input
would divide by zero; the caller validates mean > 0 (D-044's fail-loud
principle).

## 8.20 Fitting/NormalFitter.cs

Purpose: Normal fit. MLE form (`Fitting/NormalFitter.cs:19-35`):
**μ̂ = mean, σ̂ = √(Σ(x−μ)² / n)** — denominator **n, not n−1** (D-043).
This is deliberate: MLE, not Bessel-corrected sample std-dev; the fit is
to the *population*, not an estimator of a larger population's variance.

## 8.21 Fitting/LognormalFitter.cs

Purpose: LogNormal fit by **MLE on log(x)** (`Fitting/LognormalFitter.cs:19-38`):
μ̂ = mean(ln x), σ̂ = √(Σ(ln x − μ̂)² / n) — the standard transformation,
D-043 preserved. Positive-data guard: `ln` of a non-positive value is NaN,
and the validator upstream already rejects non-positive service times.

## 8.22 Fitting/GammaFitter.cs

Purpose: Gamma fit **by method-of-moments** (`Fitting/GammaFitter.cs:21-41`,
D-041): shape k̂ = x̄²/var, rate θ̂ = x̄/var. Gamma has no closed-form MLE,
so MoM is the defensible, monotone choice; the viva answer is exactly
":  moments equal-the-moments". (`MathNet` gamma MLE exists but is iterative
and slower to defend.)

## 8.23 Fitting/UniformFitter.cs

Purpose: Uniform fit (`Fitting/UniformFitter.cs:20-35`): â = min, b̂ = max —
trivial MLE. Present for the "sanity check" distribution (CONTEXT §3.3).

## 8.24 Fitting/BinSelector.cs

Purpose: chi-square binning, the **D-040** rule:
- **Bin count** `k = ceil(√n)`, clamped to [5, 20] (`BinSelector.cs:22-27`)
- Containers defined by **equal-probability edges under the fitted CDF**
  (`BinSelector.cs:36-55`): interior edges = fitted = quantiles i/k
  (i = 1…k−1); endpoints = sample min/max
- `ObservedFrequencies` counts data within each bin
  (`BinSelector.cs:63-83`)

Equal-probability bins (not equal-width) are the D-040 choice: they keep
every expected frequency large enough for the χ² approximation to hold for
skewed exponential data.

> **Viva:** Why clamp k to [5, 20]? (k too small loses sensitivity; k too
> large pushes expected frequencies below 1 for exponential tail data and
> breaks the χ² approximation — D-040/D-044.)

## 8.25 Fitting/ChiSquareTest.cs

Purpose: the goodness-of-fit test proper. `Run(samples, fitted, alpha)`
(`Fitting/ChiSquareTest.cs:33-76`): asks `BinSelector` for the bin count and
edges, bins observed vs expected (expected = n/k per equal-probability bin),
χ² = Σ (O−E)²/E, df = k − 1 − p (D-040). **The p-value comes from
`MathNet.Numerics.Distributions.ChiSquared.CDF` (`ChiSquareTest.cs:71`) —
NOT a lookup table — the viva can say "it's continuous, exact, no table
interpolation".** **Fail-loud guards** (D-044): empty sample / α outside
(0,1) throw `ArgumentException`; any `Eᵢ < 1` throws `InvalidOperationException`
(`ChiSquareTest.cs:48-50`) rather than silently reporting a bogus p-value.
The verdict is the p-value vs the caller's α.

## 8.26 Fitting/ChiSquareResult.cs

Purpose: the test output bundle (`Fitting/ChiSquareResult.cs:14-26`):
`Statistic`, `Cdf`, `PValue`, `Df`, `Decision` ("Reject" vs "Fail to reject"),
`BinEdges`, `Observed`, `Expected`. `BinEdges`/`Observed` are **the same
arrays the histogram chart reuses verbatim** (D-118). A `Caption` string
renders the row shown in the GUI: `χ² = X, df = d, p = p — Decision`.

## 8.27 Fitting/DistributionFitterFactory.cs

Purpose: name → fitter dispatch
(`Fitting/DistributionFitterFactory.cs:10-41`):
`"exponential"` → `ExponentialFitter`, `"normal"`/`"lognormal"`/`"gamma"`/
`"uniform"` likewise, else `NotSupportedException`. The set of names the
CLI's `--distribution` flag and the GUI's dropdown both speak.

## 8.28 Parameters/ParameterMode.cs

Purpose: the user's input convention — `RateWise` (λ, μ per minute) vs
`MeanWise` (1/λ, 1/μ minutes) (`ParameterMode.cs:13-20`). The single source
of truth for "is this number a rate or a mean" across CLI + GUI.

## 8.29 Parameters/ModeValidator.cs

Purpose: *soft* mode sanity (`Parameters/ModeValidator.cs:19-68`). `IsValid`
(`ModeValidator.cs:30`) takes mode + arrival/service parameters + server count
and returns `bool` + a `warning` out-string. Warns, never blocks (kickoff
rule F): rate-wise values must be strictly positive; mean-wise values must
also respect ρ < 1 — the check is a warning in mean-wise mode because the
user may still know better. Zero/negative never produce a silent pass.

## 8.30 Export/DataExporter.cs

Purpose: normalized CSV output — `patient_id` (1-based), `arrival_minutes`,
`inter_arrival_minutes`, per-stage `*_start_minutes` / `*_end_minutes` /
`*_service_minutes`. Public entry `ExportToCsv(dataSet, outputPath)`
(`Export/DataExporter.cs:24-92`). Invariant culture, `Environment.NewLine`
(cross-platform), Numbers in minutes. The upstream is validated data, so the
exporter assumes clean rows (guard: skips unparsable rows with a warning —
the exporter never throws on data).

> **Chapter 8 take-away:** the pipeline is a *sequence of single-purpose
> transforms* — load → validate → parse → calculate → fit → test → export.
> Each step is small enough that a viva question on any one function gets a
> one-rule answer with a decision ID.

# 9. CLI Deep Walk (`OpdSimulator.Cli`)

**9 files.** A subcommand dispatcher (`Program.cs`) + eight command classes
in `Commands/`. Every command shares the same shape: `TryParseArgs` →
`Run` → `return exitCode` (0 = success, 1 = operational failure, 2 = refused
input). **A refusal prints exactly one clean line to stderr and the stack
trace goes to file logs only** (D-037). Logging is Serilog with
console + rolling + error sinks per AGENTS §12; commands that emit pure
output (trace, verify) use a file-only logger so stdout stays machine-parseable
(D-059).

## 9.1 Program.cs

Entry point. `Main` (`Program.cs:48-69`) builds the Serilog logger, reads
`args`, and dispatches on the first token to the matching command class:
`simulate-params`, `verify`, `fit`, `simulate-data`, `simulate-network`,
`trace`, `export` (`Program.cs:80-106`). Unknown/no subcommand → prints
usage + exit 2. After the run it prints `Total time`, and exits 0/1/2.
Output helpers: `PrintMetrics` (`Program.cs:119-135`) formats the M/M/1
row (+ analytical comparison); `PrintNetworkMetrics` (`Program.cs:143-169`)
the per-stage table; the logger is created in `Program.cs:171-192`.

**The `--help`/usage contract:** every command prints its flags; the exact
strings are asserted by Cli tests (the viva can point at the test file).
`Program.cs:18-25` comments the exit-code contract.

## 9.2 Commands/CliShared.cs

Purpose: helpers shared by all commands. `TryLoadValidated`
(`CliShared.cs:19-41`): loader-factory → validate → refuse with the issue
list on failure. `TryParsePositive` (`CliShared.cs:57-71`): parse a flag
value as strictly-positive double — the shared "refused input" pattern so
every command gives the same clean refusal.

## 9.3 Commands/SimulateParamsCommand.cs

Purpose: the **M1 validation command** — single-stage M/M/c from explicit
flags (`--lambda`, `--mu`, `--servers`, `--horizon`, `--seed`). `Run`
(`SimulateParamsCommand.cs:22-52`) builds a `NetworkTopology` with
`CreateSingleStage`, runs the engine with a `SeededRandomSource(seed)`,
prints the metrics table. It's the "compare against the analytical M/M/c
formulas" command — the viva's analytical-validation exhibit (M7, ch. 15).

## 9.4 Commands/VerifyCommand.cs

Purpose: validate a data file and stop. `Run` (`VerifyCommand.cs:20-69`):
load + validate, print every issue (or "File OK"), exit 0 / 1. No
simulation; it exists so the user (and the marker) can see the validator's
output before any fitting — "garbage-in" is refused *before* it reaches a
fitter (D-052 principle).

## 9.5 Commands/FitCommand.cs

Purpose: distribution fitting + chi-square from a data file.
`Run` (`FitCommand.cs:32-126`): loads validated data, computes
inter-arrival times and per-stage service times, fits the family
(`--distribution`, default exponential), runs chi-square, prints the report
and writes `logs/fit-*.json` (`WriteJson`, `FitCommand.cs:192-229`) with
fitted parameters, bin edges, observed/expected — the machine-readable
evidence dump.  `--stage all|screening|doctor` controls which service group
is fit. p_exit is always computed and reported (D-008 in `PrintPExit`).

## 9.6 Commands/SimulateDataCommand.cs

Purpose: **stage-aware simulation from a data file** — the M3/M2
workhorse (D-052). `Run` (`SimulateDataCommand.cs:51-225`):

- single-stage sweep (`--servers 1,2,3`): M/M/1 vs M/M/c analytical
  comparison per sweep value (D-054: the single-stage *text* is normalized;
  the metrics are the contract);
- stage-aware M3 mode: per-stage μᵢ from the fitted service columns,
  `p_exit` from `departure_stage` bound to Screening (second-to-last stage;
  requires ≥3 stages), exponential-only service for the sweep.

The command reuses the same `FitsService`/`Engine` path as the GUI — one
semantics, two front-ends.

## 9.7 Commands/SimulateNetworkCommand.cs

Purpose: parameter-driven multi-stage + clinic-day simulation (D-053).
`Run` (`SimulateNetworkCommand.cs:49-201`): inline flags are the config
(next to a JSON file) — `--lambda`, `--mu`, `--servers`,
`--horizon`/`--days`, `--p-exit`, `--seed`, `--trace-level`; `--days`
selects the `ClinicCalendar` overload (D-009); `--verbose` prints the
**pre-run ρᵢ** table (`PrintPreRunRho`, `SimulateNetworkCommand.cs:208-219`)
so the user sees the D-007/D-015 effective rates before running.
λ_doctor = λ₀(1 − p_exit) is applied here exactly as in `NetworkTopology`.

## 9.8 Commands/TraceCommand.cs

Purpose: the **M4 viva artifact** — a deterministic, human-readable event
trace. `Run` (`TraceCommand.cs:47-200`):

- runs the engine with a `TraceRandomSource` + a `TextWriterTraceSink`
  to `--output` (file-only logger, D-059, so stdout is 100% trace);
- `--level events|state|rng` selects the verbosity (ch. 7.25);
- stops after `--patients N` have fully exited the system (not after
  horizon) — a bounded, hand-walkable trace;
- format = `TraceFormatter` verbatim (D-054 golden bytes).

The viva exhibit: `trace --patients 5 --level state` → read every line
aloud; the trace and the metrics table must tell the same story (D-058).

## 9.9 Commands/ExportCommand.cs

Purpose: normalize raw data into the public minutes-based CSV
(`ExportCommand.cs:19-57`). Flags: `--input`, `--output` (default
`<input stem>_export.csv` next to the input). Uses `DataExporter` with the
cleaned rows — the exporter never sees unvalidated data.

> **Chapter 9 take-away:** the CLI is not a second implementation — it is a
> *thin shell* over Core + Data. Every command can be reduced to "parse
> flags → call the same class the GUI calls → print". The viva defence for
> any CLI behaviour is the corresponding Core/Data class, not this folder.

**Chapters 7–9 verified against:** all 30 Core source files, all 30 Data
source files, all 9 Cli source files (2026-09-16), with decision IDs
cross-checked against `DECISIONS.md` (D-004, D-006, D-007, D-008, D-009,
D-015, D-017, D-018, D-033, D-034, D-035, D-037, D-038, D-039, D-040, D-041,
D-042, D-043, D-044, D-046, D-048, D-049, D-050, D-051, D-052, D-053, D-054,
D-055, D-056, D-057, D-058, D-059).

# 10. OpdSimulator.App — the Avalonia GUI

## 10.0 Second-floor map

**64 files: 42 `.cs` + 22 `.axaml`** (glob-confirmed 2026-09-16). The App
depends on Core + Data (`OpdSimulator.App.csproj:13-15`) — the reverse
dependency never exists, which is exactly why Core's DES loop (ch. 7) is
unit-testable without any UI.

| Folder | `.cs` | `.axaml` | Role |
|---|---|---|---|
| `Program.cs` + `App.axaml(.cs)` | 2 | 1 | Process entry, Serilog, global crash handlers |
| `Models/` | 4 | — | Flat data-seam records (RunMode, SimulationParameters, FitReport, DataBindingResult) |
| `Services/` | 8 | — | Run/fit/analyse/preferences/crash coordination |
| `ViewModels/` | 9 | — | MVVM state (CommunityToolkit.Mvvm, `ObservableObject`) |
| `Views/` | 7 | 6 | Window + tab views |
| `Controls/` | 11 + 1 models | 11 | Reusable controls (AGENTS §16.5) |
| `Assets/` | — | 4 | Theme.axaml, Motion.axaml, ChartTheme.axaml, ControlStyles.axaml (+ 2 logos `.png`) |

Packages (`OpdSimulator.App.csproj:18-25`): Avalonia 11.3.3, Avalonia.Desktop,
Avalonia.Themes.Fluent, CommunityToolkit.Mvvm 8.4.2, LiveChartsCore
.SkiaSharpView.Avalonia 2.0.5, Serilog + Console + File sinks. There is no
`Markdig` reference and no preset store: `docs/USER_MANUAL.md` is embedded as
an Avalonia resource (`OpdSimulator.App.csproj:37-40`) but not rendered
in-app, and AGENTS §17.2's `PresetStore` does not exist in source (the only
per-user persistence is `WidgetPreferences`, §10.6).

## 10.1 Process entry and crash handling

### 10.1.1 Program.cs (68 lines)

`internal static class Program` — `[STAThread]` (Win32 requirement for the
Avalonia lifetime) `Main(string[] args)` at `Program.cs:18-19`:

1. `ConfigureLogging()` (:21), then start Avalonia inside `try/finally`.
2. `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)` (:24) — `.UsePlatformDetect()` chooses X11/Wayland/Win32 by OS (:38), `.LogToTrace()` (:39) routes Avalonia's own log to Serilog's Debug trace.
3. `Log.CloseAndFlush()` in `finally` (:28) so buffered file lines survive shutdown.

**`ConfigureLogging()` (:47-67)** — AGENTS §12.1 exactly:

- `logs/` directory = `Path.Combine(Environment.CurrentDirectory, "logs")`, created on demand (:49-50) — the *working directory*, not the exe, so the app runs from a fresh clone the same way the CLI does.
- Minimum level **Debug** (:53), console sink (:54), rolling `app-.log` with 7-day retention and `shared: true` (:55-59).
- A **sub-logger** filtered to `Level >= Warning` writes `errors-.log` (:60-65) — the error-only file the viva cites as "the debugging evidence" (AGENTS §12.6).

### 10.1.2 App.axaml (29 lines)

`RequestedThemeVariant="Light"` (:4), `FluentTheme` (:6), merges
`ControlStyles.axaml` (:9) and a global `ToolTip.ShowDelay = 500` (:11-13) —
the mandatory hover-delay of AGENTS §16.2. The resource dictionary merges
**Theme.axaml, Motion.axaml, ChartTheme.axaml** (:23-26): "hex values appear
in exactly one file" is enforced by this merge point.

### 10.1.3 App.axaml.cs (74 lines) — the crash-handler install point

`App : Application`, with a **static constructor** that installs the two
process-wide handlers (AGENTS §12.3) *before any window exists*:

- `AppDomain.CurrentDomain.UnhandledException` → `CrashReporter.Report(ex, "AppDomain")` (`App.axaml.cs:23-27`).
- `TaskScheduler.UnobservedTaskException` (:29-44) — with the special case at :35-40: **D-107** admits that the missing Ubuntu global-menu service raises a benign `com.canonical.AppMenu.Registrar` DBus error on Wayland builds. That quirk is *not* a crash: it is marked observed and logged as Information, never surfaced.
- `Initialize()` (:48-60) loads the XAML and hooks `Dispatcher.UIThread.UnhandledException` → Report + `e.Handled = true` (`:52-59`).
- `OnFrameworkInitializationCompleted()` (:63-73): for a `IClassicDesktopStyleApplicationLifetime`, creates `MainWindow` (:67) and logs start/exit.

> **Viva box (10.1).** Q: Why a *static* constructor in `App`, not an instance one? A: `Application.Initialize` runs after type ctor but the handlers must be live before *any* task can fault; static ctor is the earliest seat in the class. Q: Why is `Report`'s dialog wrapped in its own catch? A: it may run while the app is already unwinding (`CrashReporter.cs:47-51`) — the log has already been written, so a failed dialog must not cause a second failure.

### 10.1.4 CrashReporter.cs (157 lines)

`internal static class CrashReporter` (`Services/CrashReporter.cs:16`):

- `Report(exception, source, simulationState = "N/A …")` (:27-53): writes the log, then posts a dialog via `Dispatcher.UIThread.Post` so it can be called from any thread.
- **Log format (AGENTS §12.5)** in `WriteCrashLog` (:84-103): app version (assembly), full command line, `Environment.OSVersion` / .NET version, the simulation-state snapshot, then the whole exception; appended to `logs/crash-YYYYMMDD.log`, never truncated (:101).
- `IsIgnorableWaylandQuirk` (:63-82) walks the inner chain and returns true only if the message names `com.canonical.AppMenu.Registrar` **and** the CLR type is `ServiceUnknown` or the full `org.freedesktop.DBus.Error.ServiceUnknown` name appears — deliberately narrow so a *real* DBus failure is still reported.
- `BuildDialog` (:117-156): a plain non-resizable `Window` (640×260) showing the message and the crash-log path — user-friendly, never a stack trace on screen.

## 10.2 View models (MVVM)

The App uses **CommunityToolkit.Mvvm**: `[ObservableProperty]` fields generate
change notifications; `[RelayCommand]` turns methods into `ICommand`. Views are
declared XAML with `x:DataType` so **compiled bindings** are on
(`AvaloniaUseCompiledBindingsByDefault=true`, `OpdSimulator.App.csproj:9`) —
a binding typo becomes a build error, not a silent blank.

### 10.2.1 MainViewModel.cs (129 lines) — the window-level orchestrator

Exposes three panels: `Config` (`MainViewModel.cs:19`), `Results`
(:22, constructed with `WidgetPreferences.Load()` so FR-UI-14 visibility
restores), `InputAnalysis` (:25).

**Start-click flow** (`OnRunRequested`, :89-128):

1. `Config.TryBuildRunParameters()` (:91) — if null, post a clean banner (defence in depth; normally unreachable because Start is gated).
2. `Results.StartRun()` (:102) hides the welcome card and shows the busy spinner; `SetChiSquareAlpha` (:103) stamps the α into the chi-square caption; `SetPreview` (:104) feeds the data-preview widget.
3. **`Task.Run(...)`** (:106-127) runs the fit + engine on a *worker thread*; every UI touch is marshalled with `Dispatcher.UIThread.Post` (:112, :126). Refusals return as an outcome; a genuinely unexpected exception is `Log.Error`'d and converted into a clean banner (:119-123) — the UI never sees an unhandled exception (G3/G4).

**Live Input-Analysis refresh**: changing a distribution dropdown (:41-45), the data binding (:49), or α (:55-61) calls `SyncInputAnalysis()` (:68-73), which projects the current binding + distribution choices into the Input Analysis tab.

**Clear All** (`ResetAll`, :82-87): `Config.ResetToDefaults()` + `Results.Reset()` — the FR-UI-21 fresh-launch state.

### 10.2.2 ConfigFieldViewModel.cs (68 lines) — the validated-field wrapper

One class for every numeric/text input. The `Value` setter (:27-39) clears any
pending error **immediately** (FR-UI-17 "error clears the moment the field
becomes valid") and raises `ValueChanged` so live derived output (e.g. the ρ
summary) recomputes. `SetError`/`ClearError` (:56-67) drive `HasError` +
`ErrorMessage`, which `ValidatedField.axaml` binds.

### 10.2.3 ConfigPanelViewModel.cs (1038 lines) — the config brain

Options it owns:

| Thing | Value | Source |
|---|---|---|
| Distribution dropdowns | Exponential, Poisson, Normal, Uniform | `ConfigPanelViewModel.cs:29-30` |
| Start days | Mon–Thu, Sat | :33-34 |
| Trace levels | None, Events, State, Rng | :37-38 |
| Default stage names | Reception, Screening, Doctor | :26 |
| Default stages | 3 | :54 |

Events that the *view* resolves (file picker, dialogs): `UploadRequested` (:57),
`ClearAllRequested` (:60), `RunRequested` (:67, `[RelayCommand] StartCalculation`
:70-71). `ApplyLoadedFile(filePath)` (:545-567) calls `DataAnalyzer.Analyze`,
sets the status line (loaded-rows / not-loadable / issue count) and refreshes
stage labels + mismatch state.

**Stage/data mismatch warning (5d.3, D-114)** — `RecomputeStagesMismatch`
(:575-604): when the *configured* stage count differs from the *detected* one,
an amber banner explains the consequence ("…will have no service rate" vs
"…will be ignored"); `SyncStagesToData` (:611-630) replaces the stage list with
the data's after user confirmation.

**`ResetToDefaults` (:644-683)** resets every field to the factory default —
seed 42, Exponential/Exponential, α 0.05, rate-wise, 3 default stages, trace
State, days 1 — and clears every field's error via `AllFieldErrors` (:685-700).

**`TryBuildRunParameters()` (:905-989)** is the whole config → run seam:

- Guards: stage count 1–5 (:907), horizon ≥ 1 (:912).
- **Mode conversion** (:917-918): `Convert(v)` = `v` if rate-wise else `1/v` — mean-wise λ and μ enter as minutes and come out as rates.
- Manual μ is a **single comma list** (:928-934): blank entries become null (fitted fallback). This is **D-112** — the per-row μ inputs were removed in 5d.1; stages are topology only.
- Server counts parsed per row (:945-950); p_exit parsed into `[0, 1)` (:955-958); run mode/day/horizon/cap/seed/trace resolved (:960-988) — then the `SimulationParameters` record is constructed (:973-988).

The nested **`StageRow`** (:1006-1038) owns a `Servers` field (default "1"),
its name, and a read-only `ServiceRateLabel` ("(from data)" / "(manual)" /
"μ = — (no source)"). Phrasing contract: the *effective μ source* is shown, the
μ *value* lives in Parameters.

### 10.2.4 ResultsPanelViewModel.cs (348 lines)

Drives the Simulation-tab results column. Row records: `MetricRow`
(`ResultsPanelViewModel.cs:12`), `StageMetricRow` (:15-24), `ChiSquareRow`
(:27-33). Lifecycle:

- `StartRun()` (:73-80) — welcome card off, busy on, "Running simulation…".
- `CompleteRun(outcome)` (:84-111) — busy off, banner text set from
  `outcome.Error` (:91), chi-square rows rebuilt (:97-104), trace text joined
  (:106-108), metrics recomputed via `SetMetrics` (:143-172: 6 system metrics —
  served / average wait / average queue / avg system time / throughput /
  operating time — plus one `StageMetricRow` per stage incl. ρ and stage
  utilisation).
- `Reset()` (:122-141) — back to the welcome state, everything wiped.
- Widget visibility (FR-UI-14): 4 flags (:284-294), a `ToggleWidget(key)` (:306-323), and persistence: any change raises `WidgetVisibilityChanged` and saves via `_preferences.Save()` (:338-342). Default visible set: `["metrics", "chiSquare", "trace"]` (:346-347).

### 10.2.5 InputAnalysisViewModel.cs (147 lines)

The Input Analysis tab (Phase 6C). **Empty state** text asserted verbatim by a
UI test: `"Load a data file to see fit analysis."` (:33).

The critical threading trick is the **generation counter** (:51, :67): every
`ApplyAsync` (background `Task.Run`, then UI-thread apply — G5) bumps
`_generation`, and the posted apply is dropped unless `generation == _generation`
(:73-80). A stale background fit can never overwrite a newer refresh.
`Prepare` (:130-146) is pure projection (fits → chart data, no UI types);
`ApplyPrepared` (:107-122) builds LiveCharts controls **on the UI thread** via
`ChartControlBuilder`. A synchronous `Apply` (tests / immediate updates) also
works (:90-98).

### 10.2.6 WelcomeCardViewModel.cs (46 lines) + CourseInfo.cs (41 lines)

The welcome card (FR-UI-5, AGENTS §16.6) reads everything from the single
`CourseInfo` constants file (`CourseInfo.cs:9-41`): course name "Simulation &
Modelling", code "CS-577", professor "Dr. Shaista Rais" (:17), six member names
(:23-31), and the two `avares://` logo URIs (:37-40). `WelcomeCardViewModel`
resolves logos lazily but *eagerly enough* to log a warning if an asset is
missing (:15-27) — the card degrades to text, it never crashes at render time.

The other two view models/records (`ControlsDemoViewModel.cs`, `ToastItem.cs`,
`InputAnalysisChartViewModel.cs`) back the controls showroom and the chart
cards respectively.

## 10.3 Services — the coordination layer

### 10.3.1 SimulationCoordinator.cs (243 lines) — the run, in one place

`RunOutcome` record (`SimulationCoordinator.cs:21-26`): engine report + fits +
rendered trace + effective exit probability + clean error string (null on
success). **This is the GUI twin of the CLI `simulate-network` stage (D-104).**

`Run(parameters, binding, status, significanceLevel)` (:77-139) order of
operations:

1. fits first (:79-80) — the status callback feeds the progress line;
2. resolve p_exit (manual > fitted > default `0.4`), with the **p_exit = 1.0
   special case** (:84-92) — only a *fitted* value can be 1.0 (manual is
   validated `< 1`), so the banner explains the real meaning: *every data row
   exits after Screening, no downstream route* (`FittedPExitEqualsOneMessage`,
   :67-68);
3. arrival-rate guard (:95-99) — banner `MissingArrivalRateMessage` (:59-60);
4. per-stage specs (:101-106) — a stage with neither manual nor fitted μ names
   itself in `MissingServiceRateMessage` (:63-64, **5d.1/5d.2, D-112/D-113**);
5. **topology built *inside* the try** (:113-115) — G4: a
   `NetworkTopology` ctor failure (fitted p_exit = 1.0 →
   `ArgumentOutOfRangeException("exitProbability")`) becomes a banner, never an
   unobserved exception;
6. trace level + `CollectionTraceSink` (:117-118); engine chosen by run mode
   (:121-124): `DiagnosticTrace` → the minutes-horizon overload; ClinicDay /
   MultiDay → the calendar overload (D-110) — **all three modes now record a
   trace** through the shared sink (5c.3);
7. `catch (UnstableSystemException)` (:129-133) keeps the Core message verbatim
   (ρ ≥ 1 refusal lists λᵢ, cᵢ, μᵢ, ρᵢ per unstable stage) and
   `catch (ArgumentOutOfRangeException) when (ParamName == "exitProbability")`
   (:134-138) surfaces Core's wording too.

Helpers: `BuildFits` (:141-162, labels "Inter-arrival" + "<stage> service"),
`BuildStageSpecs` (:170-194, manual-or-fitted per stage), `FittedRateFor`
(:196-211), `Refused` (:213-214), and the internal pure functions
`ResolveExitProbability` (:220-233) and `TraceLevelFromName` (:235-242).

> **Viva box (10.3).** Q: Why build the topology inside the try after already
> checking p_exit ≥ 1? A: the fitted p_exit = 1.0 case is rejected early with a
> *data-meaning* banner; the ctor's own guard stays as belt-and-braces so no
> topology failure can ever escape as an exception (G4, `SimulationCoordinator.cs:35-44`).

### 10.3.2 FitsService.cs (47 lines)

`Fit(label, samples, familyName, alpha = 0.05)` (:22-46) wraps
`DistributionFitterFactory` + `ChiSquareTest.Run` so any failure
(empty samples :24-27, unknown family :29-32, degenerate fit :40-45) yields a
`FitReport` with null results — the results panel renders "fit unavailable"
instead of crashing. Supplying α at run start is 5d.2/D-113.

### 10.3.3 DataAnalyzer.cs (97 lines) — upload → binding

`Analyze(path)` (:22-96) mirrors the CLI's `simulate-data` stage:

1. missing file → clean error binding (:24-30);
2. parse via `DataLoaderFactory` (:35-36), parse failure → clean error (:38-45);
3. `DataValidator.ValidateReturningIssues` (:47);
4. λ₀ = `1 / mean(inter-arrival)` from the parsed `arrival_time`s, when ≥ 2 arrivals exist (`DataAnalyzer.cs:49-58`);
5. stage detection + μ per stage via `StagePairDetector`, `ClinicStageOrder.Flow`, `ServiceTimeCalculator` (:60-72), NaN where a stage has no timings (:70);
6. p_exit via `PExitCalculator.Compute`, with `DataValidationException` swallowed to null (:74-90, D-104 default fallback);
7. returns the `DataBindingResult` (:92-95).

### 10.3.4 InputAnalysisService.cs (189 lines)

Every chart derives **only** from the `ChiSquareResult`'s own arrays — "the
chart and the chi-square table can never disagree":

- `IInputChartData` (:11-21) keeps cards UI-free.
- `HistogramChartData` (:37-43) / `ChiSquareChartData` (:57-63) carry the exact
  bin edges, observed counts and expected counts the verdict used.
- `BuildHistogram` (:118-149): observed bars + fitted density
  `density(midpoint) × binWidth × N` — equal-probability bins have *different
  widths*, so each bin uses its own (:141).
- `BuildChiSquareChart` (:160-185): paired observed/expected columns,
  category labels "bin 1…".

### 10.3.5 ChartControlBuilder.cs (160 lines)

Builds LiveCharts `CartesianChart` controls **on the UI thread only** (G5,
:17-21). `ChartHeight = 220` (:33). `Build`/`BuildChiSquareChart` (:40-68,
:76-102) return null for seriesless cards so the card shows its empty state.
`BrushColor` (:149-159) tries the ChartTheme `DynamicResource` first and falls
back to hard-coded `SKColor`s that *match the theme hex* — the only hex in a
`.cs` file, and only as headless-test fallback (declared in the header comment
:23-29).

### 10.3.6 CollectionTraceSink.cs (64 lines) and WidgetPreferences.cs (83 lines)

- **CollectionTraceSink** (`Services/CollectionTraceSink.cs:11`): in-memory
  `ITraceSink` rendering via `TraceFormatter` with a **50,000-line cap**
  dropping the oldest (:18, :52-55) so long diagnostic runs never exhaust
  memory (D-104/D-105).
- **WidgetPreferences** (`Services/WidgetPreferences.cs:11`): the *only*
  per-user persisted state (AGENTS §16.11). File: Linux
  `~/.config/OpdSimulator/ui.json`, Windows `%APPDATA%\OpdSimulator\ui.json`
  (:34-38) — under ApplicationData, never next to the exe. Persists
  `VisibleWidgets` (:41) + `CollapsedSections` (:44) only; a corrupt file logs
  a warning and falls back to defaults (:53-67). Configuration, data files and
  results are deliberately **not** persisted (FR-UI-21).

## 10.4 Models — the GUI↔engine data seam

| Record | File | Purpose |
|---|---|---|
| `RunMode` | `Models/RunMode.cs:9-22` | `ClinicDay = 0`, `MultiDay = 1`, `DiagnosticTrace = 2` (D-105). |
| `SimulationParameters` | `Models/SimulationParameters.cs:28-43` | 15-field flat record of validated inputs — GUI and coordinator share it, no UI types anywhere. |
| `FitReport` | `Models/FitReport.cs:12-16` | label + samples + fitted + chi-square verdict. |
| `DataBindingResult` | `Models/DataBindingResult.cs:22-39` | dataset + issues + fitted λ₀/μs/p_exit + samples; `IsUsable => DataSet not null && ErrorMessage null && Issues.Count == 0` (:38). |

`SimulationParameters` carries `Mode` (rate-wise vs mean-wise) in case the
coordinator needs to know how the manual numbers are stated; the values have
**already** been mode-converted to rates (`ConfigPanelViewModel.cs:917-918`).

## 10.5 Views — XAML layout

### 10.5.1 MainWindow.axaml (108 lines)

Window 1200×760 (min 1100×700), maximised, centre-screen (`MainWindow.axaml:8-14`).
Layout contract comment at :21-29: the **only focusable shell content is the
TabControl** (`TabIndex="0"`, :52-54); the four tabs:

| Tab | Line | Content |
|---|---|---|
| Simulation | :58-81 | `Grid ColumnDefinitions="380,6,*"` — 380-px config column (ConfigPanel), 6-px GridSplitter, fill results column with `MinWidth="540"` (:73, 5c.1) |
| Input Analysis | :83-88 | `InputAnalysisView` |
| Token Generator | :90-96 | `PlaceholderContent` — "Phase 6" placeholder, **not implemented** |
| Help | :98-104 | `PlaceholderContent` — "Phase 6" placeholder, **not implemented** |

Honest viva note: Token Generator and Help are placeholders at the current
phase (6c.3). Milestone 6 is the advertised plan, not yet built.

### 10.5.2 ConfigPanel.axaml (321 lines) and ResultsPanel.axaml (234 lines)

ConfigPanel = **six `CollapsibleSection`s**: 1·Data (:23, Upload button + the
D-114 mismatch warning strip), 2·Model (:66, distribution dropdowns + α field),
3·Parameters (:112, λ, single μ comma list, p_exit override), 4·Stages (:164,
count + per-stage rows), 5·Horizon (:217, single/multi-day, day count, start
day, cap, minutes), 6·Advanced (:278, seed, trace level) — pinned under a
`PinnedFooterBar` with Start Calculation / Clear All (:307-314).

ResultsPanel = welcome card (`ResultsPanel.axaml:17`) → once a run starts
(`IsVisible="{Binding HasRun}"`, :28): run header + customise toggle (:31-53),
scrollable widget area (:73, 5c.1), busy progress (:81), the G3/G4 refusal
`ErrorBanner` (:89-92), then the four toggleable widgets — metrics (:94),
chi-square (:155), data preview (:191-207), event trace (:209, mono scrollable
selectable text).

### 10.5.3 WelcomeCard.axaml (62 lines)

Two 96×96 logos (:18-19), "University of Karachi", `{Binding CourseCode} — {Binding CourseName}`, professor, per-line members — all bound, no hard-coded course text (AGENTS §16.6).

## 10.6 Reusable controls (11 + `DataPreviewTableModels.cs`)

| Control | File | What it enforces |
|---|---|---|
| `SearchableDropdown` | `Controls/SearchableDropdown.axaml` (86) | Type-to-filter + ×-clear + keyboard nav (FR-UI-6) |
| `ValidatedField` | `Controls/ValidatedField.axaml` (74) | Label + placeholder + tooltip + inline error in one unit (FR-UI-17) |
| `CollapsibleSection` | `Controls/CollapsibleSection.axaml` (156) | Chevron, `IsExpanded`/`SessionKey` (FR-UI-12, persisted) |
| `PinnedFooterBar` | `Controls/PinnedFooterBar.axaml` (34) | Always-visible primary action |
| `ErrorBanner` | `Controls/ErrorBanner.axaml` (38) | Clean refusal/error surface, never a stack trace |
| `ThemedDialog` | `Controls/ThemedDialog.axaml` (52) | Confirmations/errors, never OS-native |
| `ThemedToast` | `Controls/ThemedToast.axaml` (53) | Notifications |
| `InfoIcon` | `Controls/InfoIcon.axaml` (20) | "?" contextual help (docs/CONTEXT.md) |
| `ChartCard` | `Controls/ChartCard.axaml` (37) | Input Analysis card shell |
| `DataPreviewTable` | `Controls/DataPreviewTable.axaml` (80) | FR-UI-20 read-only preview |
| `PlaceholderContent` | `Controls/PlaceholderContent.axaml` (27) | Phase-6 placeholder cards |

**DataPreviewTable** is the case study for the performance contract (FR-UI-20,
NFR-10): headers are generated from the loaded file's columns (:31-35, sorted
cycle asc→desc→original), rows render in a **virtualising** ListBox with
`VirtualizingStackPanel` (:48-52), cells are `SelectableTextBlock` for Ctrl+C
(:63-69), invalid rows get the FR-UI-17 red treatment with a reason tooltip,
and load failures swap the table for an `ErrorBanner` (:20-21).
*Known dangle:* line :66 requests `{DynamicResource FontMono}`, but Theme.axaml
defines only `FontFamilyMono` (`Theme.axaml:99`) — the key silently falls back
to the inherited font. Cosmetic; a candidate one-line fix in a future phase.

## 10.7 Assets — single-theme rule (AGENTS §16.3)

- **Theme.axaml** — **85 `x:Key` tokens**, hex only here. Contract comment at
  :19-29: fonts Default/Heading/Mono, font sizes 18/14/12, spacing 4/8/12/16/24,
  radii 4/8/12, two shadows, `BrushFocusRing` + `ThicknessFocusRing(2)` +
  `ThicknessFocusRingOffset(1)`. Brand green `#1B7A4C` (:30).
- **Motion.axaml** — 5 durations `MotionDuration{Slow,Normal,Medium,Fast,Reduced}`
  (reduced = reduced-motion accessibility).
- **ChartTheme.axaml** — 13 `BrushChart…` keys (series colours 1–4, grid, axis,
  ticks, legend, tooltip).
- **ControlStyles.axaml** — style *classes* only, zero new colours: `PrimaryButton`
  (:9-27, hover/pressed/drawn to `BrandGreenDark`), `GhostButton` (:30-40),
  `TextBlock.Caption` (:43-46), `Border.DemoCard`/`PanelCard` (:49-64).

Every control resolves `{DynamicResource …}`; ViewModels never hold hex. One
file change restyles the app.

## 10.8 Threading model (G5) — the single most visible GUI design

| Thread | Work | Marshalling |
|---|---|---|
| UI thread | config edits, blur validation, built LiveCharts controls, `Results.CompleteRun` | always `Dispatcher.UIThread.Post` from workers |
| Background `Task.Run` | `InputAnalysisViewModel.ApplyAsync` fit (:68), `MainViewModel.OnRunRequested` engine run (:106) | generation counter guards staleness (`InputAnalysisViewModel.cs:67-80`) |
| Any thread | `CrashReporter.Report` | posts the dialog on the UI thread |

The engine itself is single-threaded DES (ch. 7.5) — the *fits* (MathNet) and
the run both block, so they live on background threads and only results cross
back. LiveCharts Avalonia controls cannot be created off the UI thread; that is
why `ChartControlBuilder` is called inside the posted apply
(`InputAnalysisViewModel.cs:111-120`).

> **Chapter 10 take-away:** the App is a *thin, well-behaved shell*. Its two
> scan-worthy contracts are (1) the **`SimulationParameters` /
> `DataBindingResult` seam** — the GUI never touches Core/Data types directly
> and never lets an exception reach the UI (every failure is a banner), and
> (2) the **background-run + single-writer threading** that keeps the UI
> responsive while DES runs. Everything else is bookkeeping around that core.

# 11. The Test Projects

## 11.0 The truth about the counts

`docs/DEV_LAUNCH.md:6` records the last verified gate: **278 green**. Measured
today (2026-09-16, Release on Ubuntu, `dotnet test`):

| Project | Executed & green | Classes | Files |
|---|---|---|---|
| OpdSimulator.Core.Tests | 85 | 13 | 14 (incl. GlobalUsings + 1 fixture) |
| OpdSimulator.Data.Tests | 58 | 8 | 9 |
| OpdSimulator.Cli.Tests | 35 | 6 | 8 |
| OpdSimulator.App.Tests | 100 | 20 (11 behavioural + 9 screenshot) | 22 |
| **Total** | **278** | **47** | **53** |

**Why attribute counts lie:** counting `[Fact]`/`[Theory]` attributes gives 248
(Core 77, Data 48, Cli 23, App 100) — but xUnit executes one test **per theory
data row**, so Core/Data/Cli run higher than their attribute count
(85/58/35). App *behaves* differently: no `[Theory]` at all, and its 100 tests
are 100 `[AvaloniaFact]` attributes. The authoritative number is the executed
count, and it is 278. (Note: `DEV_LAUNCH.md:177` still says "272 tests pass" —
stale prose, flagged 2026-09-16, awaiting owner approval to correct.)

All test projects are `net8.0`, xUnit-based, with `ImplicitUsings` + nullable
enabled. There is **no `InternalsVisibleTo` in the Core/Data/Cli test projects**;
the App *does* expose one — `OpdSimulator.App.csproj:33` — so the App tests can
reach `SimulationCoordinator.ResolveExitProbability` and
`TraceLevelFromName` (both `internal`, `SimulationCoordinator.cs:220,235`).
There is no shared test-infra project; each test project is self-contained.

## 11.1 OpdSimulator.Core.Tests — 85 green

Runs against pure Core (no UI, no data layer). Per-class executed counts
(`--list-tests`, 2026-09-16):

| Class | Tests | What it pins |
|---|---|---|
| `EventTests` | 4 | the `Event` tie-break order — time → **type → patientId**, the JSON payloads, equality (D-033). |
| `FELTests` | 4 | priority-queue semantics: pop order, timestamp ties, FIFO within a tie, capacity behaviour. |
| `QueueTests` | 5 | `Queue<T>` — enqueue order, FIFO-dequeue, counts, the empty-state contract. |
| `ServerTests` | 6 | idle/busy transition guard, state flags, utilisation accounting. |
| `PatientTests` | 3 | patient identity, stage cursor, routing transitions. |
| `ClinicCalendarTests` | 15 | the **theory-heavy** day model: open days (Mon–Thu+Sat), window bounds 495/660 (08:15/11:00), day indexing, `FormatClock` — 10 `InlineData` rows (`ClinicCalendarTests.cs`). |
| `ExponentialSamplerTests` | 3 | the `double.Epsilon` clamp (never `−ln(U)/λ` with U=0 → +∞), seeded determinism. |
| `SeededRandomSourceTests` | 4 | DEFAULT_SEED=42 reproducibility, `SetSeed` reseeding, value ranges. |
| `NetworkTopologyTests` | 7 | effective λᵢ = λ₀·Π(1−p_exit) (D-007), ρᵢ, validation, single-stage factory. |
| `EngineTests` | 21 | the DES loop: FEL advance, arrival scheduling, service-end routing, calendar gating, daily cap, counter correctness. |
| `StabilityTests` | 4 | ρ ≥ 1 → `UnstableSystemException` naming each unstable stage (D-034). |
| `EventTraceTests` | 2 | `EventTrace`/sink plumbing. |
| `TraceRegressionTests` | 7 | **golden-trace guard**: seed 42 serves 29,892 patients with average wait 0.724 min; the event stream at `Fixtures/trace-5-patients.txt` must match byte-for-byte (D-054). |

The regression triple (seed 42 → 29,892 / 0.724) is the *reason* the default
seed is 42 and the RNG is a fixed `SeededRandomSource` — any engine change that
reorders RNG draws trips it.

## 11.2 OpdSimulator.Data.Tests — 58 green

| Class | Tests | What it pins |
|---|---|---|
| `LoaderTests` | 7 | `DataLoaderFactory` dispatch + CSV/XLSX parse correctness; dirty files surface as issues, not exceptions. |
| `DataValidatorTests` | 12 | the rule battery incl. the D-008/D-038 split: `departure_stage="Reception"` is an *anomaly warning*, not an exit; missing columns; out-of-range values. |
| `PreprocessTests` | 6 | column detection, stage pair detection, normalisation. |
| `TimeParserTests` | 15 | 13 `InlineData` rows over the time formats (HH:mm, HH:mm:ss, HH:mm AM/PM, Excel fraction-of-day — D-039). |
| `FittingTests` | 6 | MLE/MoM fits: Exponential λ = 1/mean, Normal/Lognormal n-denominator variance (D-043), Gamma MoM (D-041), uniform. |
| `ChiSquareTests` | 4 | equal-probability binning `k=ceil(√n)` clamped [5,20] (D-040), p-value from MathNet `ChiSquared.CDF`, the Eᵢ ≥ 1 fail-loud guard (D-044), df = k−1−p. |
| `ModeValidatorTests` | 5 | rate-wise vs mean-wise consistency (warn, never hard-block). |
| `FixtureTests` | 3 | the bundled `Fixtures/dirty_missing.xlsx` — schema/validation integrity of the fixture itself. |

## 11.3 OpdSimulator.Cli.Tests — 35 green

**End-to-end**: each test invokes `Program.Main` / the dispatcher and asserts
stdout, exit code, and file output. The two theory classes explain the big
attribute→executed jump (Cli has 23 attributes → 35 executed, via `MemberData`
rows):

| Class | Tests | What it pins |
|---|---|---|
| `CliDataCommandTests` | 5 | `data` subcommand: fit output, chi-square verdict, export. |
| `CliSimulateNetworkTests` | 12 | **theory** (MemberData-driven) network modes, param parsing, ρ refusal exit code 2. |
| `CliSimulateDataNetworkTests` | 3 | data-driven network run end-to-end. |
| `CliSimulateParamsTests` | 1 | rate/mean-wise param interpretation. |
| `CliTraceTests` | 13 | **theory** (MemberData): `trace --patients 5 --level state` output matches `Fixtures/trace-5-patients.txt`; `--level` filtering; `--patients` early stop. |
| `CliRefusalTests` | 1 | the clean single-line refusal + exit code (D-037) for ρ ≥ 1. |

## 11.4 OpdSimulator.App.Tests — 100 green

**Headless Avalonia** via `TestAppBuilder.cs:17-20`:
`AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })`.
It boots the *real* `App` on the headless platform so every test exercises the
actual startup path (XAML load, merged Theme/Motion/ChartTheme, lifetime) with
no display server — CI-friendly. Tests are marked `[AvaloniaFact]` (runs on the
headless dispatcher), the source of the 100 = 100 equivalence.

**Behavioural classes** (90 tests):

| Class | Tests | Phase |
|---|---|---|
| `Phase1SmokeTests` | 6 | window/shell renders, welcome card present. |
| `ControlsSmokeTests` | 11 | every reusable control instantiates and interacts. |
| `Phase3ShellTests` | 5 | tab shell + placeholder navigation. |
| `Phase4ConfigTests` | 13 | config fields, blur validation, μ/λ parsing, reset defaults. |
| `Phase4bConfigTests` | 5 | significance + mode toggle + ρ summary. |
| `Phase5RunFlowTests` | 8 | Start → background run → metrics populate; refusal banners. |
| `Phase5cFixesTests` | 7 | known-issue regressions from the 5c review round. |
| `Phase5dConfigTests` | 16 | stage-count edits, mismatch warning, single-μ source (D-112), preset-free startup. |
| `Phase6c1InputAnalysisTests` | 4 | empty state text verbatim, chart card population. |
| `Phase6c2HistogramTests` | 10 | histogram binning ↔ chi-square bins agree (chart can't disagree with the table). |
| `Phase6c3ChiSquareTests` | 5 | χ² caption/rows at run start (D-113). |

**Screenshot evidence tests** (10): `Phase{1,3,5,5c,5d,6c1,6c2,6c3,ControlsDemo}Screenshot`
— one test each (Phase5c has 2) that renders a frame and saves it as CI
evidence. They are *deterministic render captures*, not pixel assertions: the
assertion is "this view renders without throwing and produces an image".

> **Viva box (11.4).** Q: Why `UseHeadlessDrawing = false`? A: only the
> highest-fidelity software rasteriser reproduces the exact vectors the app
> renders on a real desktop; headless-drawing mode shortcuts parts of the
> pipeline and can hide render bugs the screenshots are meant to surface.

## 11.5 What 278 tests protect (traceability)

| Threat | Test that stops it |
|---|---|
| Engine refactor breaks a working scenario | `TraceRegressionTests` golden trace + metrics (seed 42, 29,892/0.724) |
| RNG ordering changes between releases | `SeededRandomSourceTests` + same regression |
| Unstable config runs anyway | `StabilityTests`, `CliRefusalTests`, Engine refusal path |
| Wrong p_exit/routing arithmetic | `NetworkTopologyTests` (D-007), `Phase5dConfigTests` |
| Dirty data slips through | `DataValidatorTests`, `LoaderTests`, `FixtureTests` |
| GUI refuses-but-means-something-else | `Phase5RunFlowTests` banner wording, `SimulationCoordinator` G3/G4 paths |
| Charts lie about the verdict | `Phase6c2HistogramTests` / `Phase6c3ChiSquareTests` (shared `ChiSquareResult` arrays) |
| Startup is not empty / restores secrets | `Phase5dConfigTests` (FR-UI-21, no auto-restore) |
| Mission-critical timing (08:15/11:00) breaks | `ClinicCalendarTests` 15 cases |

**Chapter 11 take-away:** the suite is layered the way the product is — Core
proves the *math* (determinism, tie-breaks, ρ, golden trace), Data proves the
*pipeline* (parse → validate → fit → verdict), Cli proves the *contracts*
(exit codes, clean refusals, identical trace bytes), App proves the *behaviour*
(headless-booted real UI, banners, chart/table agreement, render evidence).

**Chapters 10–11 verified against:** all 42 App `.cs` sources, all 22 App
`.axaml` assets (spot-read for each cited line), the App `.csproj`, and all 53
test sources; executed-test counts measured via `dotnet test -c Release`
(session run, 2026-09-16, Ubuntu: 85/58/35/100 = 278, all green). Decision IDs
cross-checked: D-104, D-105, D-107, D-110, D-112, D-113, D-114 (plus D-007,
D-008, D-018, D-033, D-034, D-037, D-038, D-039, D-040, D-041, D-043, D-044,
D-054, D-058, D-059 from earlier chapters).

---

## Incremental Update Rule

After this document exists, every future sub-phase (6c.4, 6c.5, 6c.6, Phase 6,
Phase 7, Phase 8) appends a short update section **at the end of this file**, at
the time the sub-phase commits:

```markdown
## Update — Phase 6c.4 (2026-09-16)
Changes since last update:
  - New files: …
  - Modified files: …
  - New decisions: D-…
  - Sections above affected: ch. 10, ch. 13

Detailed walkthrough: …
```

The deep-dive never drifts more than one sub-phase behind the code. If a
sub-phase lands without a doc update here, the next session's first job is to
catch this file up. The PDF (Session E) is regenerated on the final pass.