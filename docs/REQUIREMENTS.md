# REQUIREMENTS.md — Traceability Matrix

**Purpose:** Derived view of `PRD.md`. Every requirement is tracked from
spec → code → test → decision.

**Source of truth:** `PRD.md` (master). If this file disagrees with PRD.md,
PRD.md wins.

**Last synced with PRD.md:** 2026-09-13 (v1.3.0)

**Status vocabulary:** `[ ]` TODO · `[~]` IN PROGRESS · `[x]` DONE ·
`[?]` BLOCKED · `[-]` CANCELLED

---

## Functional Requirements — UI

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| FR-UI-1 | Left config panel + right results panel | [ ] | — | — | — |
| FR-UI-2 | Input validation (numeric, file type, mode mismatch) | [ ] | — | — | — |
| FR-UI-3 | Background thread + progress indicator | [ ] | — | — | — |
| FR-UI-4 | Charts via LiveCharts2 (P1 + P2) | [ ] | — | — | — |

## Functional Requirements — Data

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| FR-DATA-1 | Accept .xlsx and .csv | [x] | src/OpdSimulator.Data/Loaders/{ExcelLoader,CsvLoader,DataLoaderFactory}.cs | LoaderTests (incl. real temp-file round trip, factory extension check) | D-038 |
| FR-DATA-2 | Require columns: arrival_time, *_start, *_end, departure_stage | [x] | src/OpdSimulator.Data/Loaders/DataSet.cs, Validation/DataValidator.cs | ValidatorTests (missing-column issues; stage-pair detection) | D-038 |
| FR-DATA-3 | Compute inter-arrival times | [x] | src/OpdSimulator.Data/Preprocess/InterArrivalCalculator.cs | PreprocessTests | — |
| FR-DATA-4 | Compute per-stage waiting + service times | [x] | src/OpdSimulator.Data/Preprocess/ServiceTimeCalculator.cs | PreprocessTests | — |
| FR-DATA-5 | Fit selected distribution via MLE | [x] | src/OpdSimulator.Data/Fitting/{IDistributionFitter,ExponentialFitter,...DistributionFitterFactory}.cs | FitterTests (MLE means recovered, seed-stable) | D-041, D-043 |
| FR-DATA-6 | Compute p_exit from departure_stage | [x] | src/OpdSimulator.Data/Preprocess/PExitCalculator.cs | PreprocessTests (Reception excluded per CONTEXT §5.4) | — |
| FR-DATA-7 | Reject dirty data with specific errors | [x] | src/OpdSimulator.Data/Validation/DataValidator.cs | ValidatorTests, DataValidationExceptionTests, FixtureTests (dirty file: per-row issues) | D-038 |
| FR-DATA-8 | Validate rate-wise vs mean-wise consistency | [x] | src/OpdSimulator.Data/Parameters/{ParameterMode,ModeValidator}.cs | ModeValidatorTests (soft warnings, never blocks) | — |
| FR-DATA-9 | N-stage generic loader (refactor plan) | [x] | src/OpdSimulator.Data/Loaders/DataSet.cs, Preprocess/StagePairDetector.cs (any-stage generic); M2 `simulate-data` runs the first detected stage — M3 wires the multi-stage engine | StagePairDetectorTests, FixtureTests (single Screening pair) | D-006 |
| FR-DATA-10 | Optional per-server columns (graceful degradation) | [ ] | — | — | — |

## Functional Requirements — Simulation

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| FR-SIM-1 | 3-stage serial network (1/2/3 servers) | [x] | src/OpdSimulator.Core/Stages/NetworkTopology.cs + Engine/Engine.cs; Cli/Commands/{SimulateDataCommand,SimulateNetworkCommand}.cs | EngineTests.Run_Network_*, NetworkTopologyTests, CliSimulateDataNetworkTests, CliSimulateNetworkTests | D-006, D-049, D-052, D-053 |
| FR-SIM-2 | DES with FEL; 4 event types | [x] | src/OpdSimulator.Core/Events/{Event,EventType,FEL}.cs, Engine/Engine.cs | FELTests, EventTests, EngineTests | D-033 |
| FR-SIM-3 | RNG per selected distributions | [x] | src/OpdSimulator.Core/Distributions/{IRandomSource,SeededRandomSource,ExponentialSampler}.cs | SeededRandomSourceTests, ExponentialSamplerTests | D-035 |
| FR-SIM-4 | p_exit routing after Screening | [x] | src/OpdSimulator.Core/Stages/NetworkTopology.cs (routing λᵢ), Data/Preprocess/PExitCalculator.cs, Cli/Commands/SimulateDataCommand.cs (p_exit when Doctor present) | NetworkTopologyTests, CliSimulateDataNetworkTests, CliSimulateNetworkTests | D-007, D-015, D-052, D-053 |
| FR-SIM-5 | Arrival window 8:15 → cap/11:00 | [x] | src/OpdSimulator.Core/Engine/Engine.cs (HandleArrival horizon gate — M1; clinic calendar hours are M3/M4) | EngineTests | — |
| FR-SIM-6 | Services continue past close | [x] | src/OpdSimulator.Core/Engine/Engine.cs (FEL drains past horizon), Calendar/ClinicCalendar.cs (drain in day model) | EngineTests.Run_Calendar_DrainsPastWindow | — |
| FR-SIM-7 | Day ends when cap served | [x] | src/OpdSimulator.Core/Engine/Engine.cs (CalendarGate daily cap, D-051), Cli/Commands/SimulateNetworkCommand.cs (--cap) | EngineTests.Run_Calendar_CapBindsPerDay, CliSimulateNetworkTests.DaysTwo_SameSeed_RunsAreIdentical | D-009, D-051 |
| FR-SIM-8 | Skip Fri/Sun | [x] | src/OpdSimulator.Core/Calendar/ClinicCalendar.cs (open days + closure gates) | ClinicCalendarTests, EngineTests.Run_Calendar_FridaySundayZeroAdmissions | D-051 |
| FR-SIM-9 | Single-day / multi-day modes | [x] | src/OpdSimulator.Core/Engine/Engine.cs (calendar Run overload), Cli/Commands/SimulateNetworkCommand.cs (--days / --horizon mutually exclusive) | EngineTests.Run_Calendar_*, CliSimulateNetworkTests | D-009, D-051, D-053 |
| FR-SIM-10 | Internal minutes; UI shows clock time | [~] | src/OpdSimulator.Core/Calendar/ClinicCalendar.cs (FormatClock), Patient.cs internal minutes | ClinicCalendarTests.FormatClock | — |

## Functional Requirements — Statistics & Validation

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| FR-STAT-1 | Chi-square on inter-arrival + service | [x] | src/OpdSimulator.Data/Fitting/ChiSquareTest.cs; Cli/Commands/FitCommand.cs runs test on inter-arrival + every detected stage | ChiSquareTests (exp-fit accepts; uniform-vs-exp rejects; n-adaptive bins), CliDataCommandTests | D-040, D-044 |
| FR-STAT-2 | α default 0.05, user-selectable | [~] | Cli/Commands/FitCommand.cs hardcodes α = 0.05 (default met, D-044); user selection is the M5 GUI control | ChiSquareTests | — |
| FR-STAT-3 | Auto bin count | [x] | src/OpdSimulator.Data/Fitting/BinSelector.cs (k = ⌈√n⌉ clamped [5,20]) | ChiSquareTests (k = 5/8/20 bounds) | D-040 |
| FR-STAT-4 | Display O, E, χ², df, p, decision | [~] | ChiSquareResult carries Observed/Expected arrays (tested); `fit` CLI prints χ², df, p, decision; the O/E table is the M5 GUI | ChiSquareTests | — |
| FR-STAT-5 | Auto compare vs analytical M/M/c | [ ] | — | — | — |
| FR-STAT-6 | Display per-stage ρ (bottleneck) | [x] | src/OpdSimulator.Core/Stages/NetworkTopology.cs (RhoFor, routing λᵢ) + cli/Program.cs PrintNetworkMetrics (`ρ = λᵢ/(c·μ)`) + SimulateNetworkCommand --verbose | CliSimulateNetworkTests.Verbose_PrintsPreRunRho, CliSimulateDataNetworkTests | D-015, D-052, D-053 |
| FR-STAT-7 | Per-server util + imbalance flag (>0.15) | [x] | src/OpdSimulator.Core/Servers/{IServerSelectionPolicy,RandomIdleSelection}.cs + cli/Program.cs PrintNetworkMetrics per-server lines | EngineTests (balance/utilisation), CliSimulateNetworkTests | D-016, D-050 |
| FR-STAT-8 | Histogram + fitted PDF overlay | [ ] | — | — | — |
| FR-VAL-1 | Refuse run if any ρᵢ ≥ 1 | [x] | src/OpdSimulator.Core/Stages/NetworkTopology.cs (Validate — lists ALL unstable stages with λᵢ, cᵢ, μᵢ, ρᵢ), Engine/{EngineConfig,UnstableSystemException}.cs, Cli/Program.cs (clean stderr, D-037) | StabilityTests, EngineTests.Run_Network_*, CliSimulateDataNetworkTests, CliSimulateNetworkTests.UnstableStage_RefusedListingAllUnstable | D-034, D-015, D-049 |
| FR-VAL-2 | Assert 0 ≤ utilisation ≤ 1 | [x] | src/OpdSimulator.Core/Servers/Server.cs, Engine/Engine.cs | ServerTests (Utilisation_StaysWithinUnitInterval), EngineTests | — |
| FR-VAL-3 | Random seed (default 42, logged) | [x] | src/OpdSimulator.Core/Distributions/SeededRandomSource.cs, Engine/Engine.cs | SeededRandomSourceTests, EngineTests.Run_SameSeed | D-035 |
| FR-VAL-4 | Event log with all state + RNG draws | [x] | src/OpdSimulator.Core/Engine/Engine.cs (ITraceSink emission) + Trace/ namespace (TraceEvent/TraceFormatter/TraceRandomSource) + Cli/Commands/TraceCommand.cs (`trace`, `--level rng`). The M4 first-class trace supersedes the Serilog Debug channel as the stronger implementation | TraceRegressionTests (golden fixture, draw-by-draw RNG parity), CliTraceTests (end-to-end stdout lock, levels), EventTraceTests (Serilog channel) | D-036, D-055 |
| FR-VAL-5 | (Stretch) N replications + CI | [ ] | — | — | — |

## Functional Requirements — Token Generator

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| FR-TOKEN-1 | Designed for from day 1, built last | [ ] | — | — | — |
| FR-TOKEN-2 | Sequential token + estimated wait | [ ] | — | — | — |
| FR-TOKEN-3 | Separate tab with UI flourish | [ ] | — | — | — |

## Non-Functional Requirements

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| NFR-1 | Modular projects (Core/Data/App/Cli/Tests) | [x] | OpdSimulator.sln — Core, Data, App (placeholder until M5), Cli, 3 test projects, all with ProjectReferences only downward (UI→Core, never reverse) | build-green across `dotnet build` | D-025 |
| NFR-2 | XML doc comments on public APIs | [x] | All public Core + Data types/members carry `///` docs (Data layer written 100% documented; verifiable by reading src/OpdSimulator.Data/…) | — | — |
| NFR-3 | 4,000 patients / 30 days in <3s | [ ] | — | — | — |
| NFR-4 | Deterministic given seed | [x] | src/OpdSimulator.Core/Engine/Engine.cs, Distributions/SeededRandomSource.cs | EngineTests.Run_SameSeed_TwoRuns_ProduceIdenticalResults, SeededRandomSourceTests | D-035 |
| NFR-5 | C# .NET 8, Avalonia, MathNet, ClosedXML, CsvHelper | [ ] | .gitignore, DEV_LAUNCH.md §3, appsettings.template.json | — | D-027 |
| NFR-6 | Charts <500ms, non-blocking UI | [ ] | — | — | — |

---

## Coverage Summary

- Total requirements: 46
- `[x]` DONE: 29
- `[~]` IN PROGRESS: 3
- `[ ]` TODO: 14
- `[?]` BLOCKED: 0
- `[-]` CANCELLED: 0

**Coverage:** 63.0% (29/46)

> M1 (single-stage M/M/1 engine) marked FR-SIM-1/2/3/5/6, FR-VAL-1/2/3/4 and
> NFR-4 DONE. FR-VAL-4 was briefly `[~]` because it had no automated test; it is
> now `[x]` backed by EventTraceTests (in-memory Serilog sink asserting event-line
> state + RNG draws). M4 (D-055) lands a first-class deterministic trace
> (`OpdSimulator.Core.Trace` + the `trace` CLI) that supersedes the Serilog Debug
> channel as the stronger FR-VAL-4 implementation — the line-by-line story now
> has a golden regression (draw-by-draw RNG parity, stats cross-check) and the
> hand-verified 5-patient story is frozen at
> `tests/OpdSimulator.Core.Tests/Fixtures/trace-5-patients.txt`.
> FR-SIM-5's clinic-calendar hours (8:15→11:00, closed Fri/Sun) are M3/M4; M1
> implements the arrival-generation gate.
>
> M2 (data + goodness-of-fit, merged 2026-09-13) marked FR-DATA-1..9,
> FR-STAT-1/3, NFR-1/2 DONE. FR-STAT-2 (user-selectable α) and FR-STAT-4
> (O/E table display) are `[~]`: the CLI satisfies the default α = 0.05 and
> prints χ²/df/p/decision, but the selection control and full O/E bins table
> are M5 GUI work. FR-DATA-9 is loader-side-generic now; wiring the N-stage
> engine to the loader is M3.

---

## Update Protocol

Update this matrix in the SAME session as:
- Any PRD.md change (add/remove/modify requirement ID).
- Any requirement status change.
- Any source file implementing a requirement.
- Any test covering a requirement.
- Any decision logged in DECISIONS.md that shaped a requirement.

Run `scripts/verify-traceability.sh` (to be created) before each
milestone close to catch orphans — requirements with status `[x]`
but no source or test.

---

## Changelog

| Date | Change |
|------|--------|
| 2026-09-14 | M4: FR-VAL-4 Source/Test/Decision refreshed — the first-class deterministic trace (Trace/ namespace + `trace` CLI, D-055) supersedes the Serilog Debug channel as the implementation; tests now TraceRegressionTests (golden fixture, draw-by-draw RNG parity, stats cross-check) + CliTraceTests + EventTraceTests; coverage summary block recomputed from 50.0% to the actual 63.0% (constituting stale from M3) |
| 2026-09-13 | M3: FR-SIM-1/4/7/8/9, FR-STAT-6/7 → `[x]` with network source/tests/decisions (D-049→D-053); FR-SIM-10 → `[~]` (FormatClock lands the real-clock piece, UI binding M5); FR-VAL-1 + FR-SIM-1 source refreshed for `NetworkTopology.Validate`/CLI; coverage now 63.0% (29/46 `[x]`, +3 `[~]`) |
| 2026-09-13 | Initial matrix created from PRD v1.3.0 |
| 2026-09-13 | Bidirectional rule (AGENTS §9.7) applied — matrix audited against PRD v1.3.0; zero orphan rows; D-025 logged |
| 2026-09-13 | M1: FR-SIM-1/2/3/5/6, FR-VAL-1/2/3, NFR-4 marked `[x]` with Source + Test + Decision |
| 2026-09-13 | Review fix: FR-VAL-4 promoted `[~]`→`[x]` with EventTraceTests (review note "mark it [x] with source + test filled"); coverage now 21.7% |
| 2026-09-13 | M2: FR-DATA-1..9, FR-STAT-1/3, NFR-1/2 → `[x]`; FR-STAT-2/4 → `[~]` (CLI default met, GUI remainder M5); coverage now 50.0% (23/46) |