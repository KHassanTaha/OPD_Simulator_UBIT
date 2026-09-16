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
| B | 7–9 (Core, Data, Cli) | awaiting session |
| C | 10–11 (App, Tests) | awaiting session |
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