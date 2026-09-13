# TODO.md

Last updated: 2026-09-13

> **Completion rule:** A task that adds/removes a feature is not `[x] DONE`
> until `DEV_LAUNCH.md` reflects the change (if build/launch affected) and
> `USER_MANUAL.md` reflects the change (if UI/workflow affected) — see
> `AGENTS.md` §9.2. A milestone is additionally not `[x] DONE` until
> `DEV_LAUNCH.md` is verified from a dead state (`AGENTS.md` §10.4).

> Requirements are not `[x] DONE` until their row in REQUIREMENTS.md is
> updated with Source + Test.

## Active
- [x] Pre-M2 fix: CLI refusal prints one clean stderr line; stack trace to file logs only — 2026-09-13 (Program.cs refactored to testable `Program.Run`; `UnstableSystemException` prefix removed; tests/OpdSimulator.Cli.Tests added; DEV_LAUNCH §5/§6/§8 + DECISIONS D-037 updated; 37 tests green; committed 5b0d4c4 and pushed to origin/fix/cli-clean-refusal, awaiting owner merge)
- [x] M2 sub-block A–F: Data layer project (loaders, DataValidator, preprocessing, 5 fitters, chi-square, parameter modes, exporter) — 2026-09-13 (52 Data tests green on feat/milestone-2-data-and-fitting; commits 99cf03b, 3490cff; FR-DATA-1..9 all tested via LoaderTests/ValidatorTests/FitterTests/ChiSquareTests/ModeValidatorTests)
- [x] M2 sub-block G: CLI subcommand dispatcher (`simulate-params`, `verify`, `fit`, `simulate-data`, `export`) — 2026-09-13 (5 CliDataCommandTests green; CliRefusalTests retargeted; commit 148d624; FR-STAT-1/3 CLI path; D-046)
- [x] M2 sub-block H: sample data + dirty fixture via `scripts/sample-data-generator` + `make-sample-data.sh`; `.gitignore` negates the sample CSV — 2026-09-13 (commit 52b5ddf; 3 FixtureTests green; D-045, D-047; B-004 Resolved)
- [x] M2 sub-block I (Ubuntu half): dead-state build + full suite + live `verify`/`fit`/`simulate-data`/`export` and M1 `simulate-params` regression — 2026-09-13 (97 tests green, 0 warnings; ρ 0.75 / wait 0.724; sweep 0.81/0.41/0.27)
- [~] M2 sub-block J: docs pass (DEV_LAUNCH §5/§6/§7/§8/§10/changelog; USER_MANUAL §7; REQUIREMENTS 50% coverage; DECISIONS D-045..D-047; VIVA_ANSWERS; PROGRESS; BLOCKERS B-004 Resolved; this TODO) — 2026-09-13 (commit 384c2be; M2 branch pushed, awaiting owner review)
- [x] M2.5 fix: sample data at HH:MM:SS precision (chi-square rejected a true exponential at p ≈ 0 due to minute-rounded storage) — 2026-09-13 (generator format only, RNG stream unchanged; TimeParser `H:mm:ss` test added — HH:MM untouched; samples + fixture regenerated; `fit` accepts p = 0.103 / 0.258; D-048; VIVA line; 98 tests green — on fix/sample-data-precision)

## Upcoming
- [ ] Create `scripts/verify-traceability.sh` (orphan check: `[x]` matrix rows without Source/Test; referenced by REQUIREMENTS.md Update Protocol)
- [x] Obtain `samples/sample_patients.xlsx` (owner; 1-stage demo data) — see BLOCKERS B-004 — 2026-09-13 (no real file provided → **stand-in** generated deterministically by `scripts/sample-data-generator`, seed 42: 60 rows, single Screening stage, Exp(λ=0.5)/Exp(μ=0.666…), p_exit=1.0 (CONTEXT §5.5); BLOCKERS B-004 Resolved; real clinic data remains substitutable at the same path)
- [x] Add headless CLI mode to `OpdSimulator.Cli` (DEV_LAUNCH §7; demo path 2026-09-16) — 2026-09-13 (M1 CLI: `--lambda/--mu/--servers/--horizon/--seed`, ρ table, exit 0/1; F2/F3 verified on feat/milestone-1-single-stage-engine)
- [x] Set up solution structure (Core / Data / App / CLI / Tests) — 2026-09-13 (sln + 6 projects, packages pinned per D-026; App is a placeholder classlib)
- [?] Initialize Avalonia project — BLOCKED on B-005 (no Avalonia template installed; App is a classlib placeholder until M5)
- [x] Add MathNet.Numerics, ClosedXML, CsvHelper via NuGet (update DEV_LAUNCH in the same change) — 2026-09-13 (extended with Avalonia/CommunityToolkit/LiveCharts/Serilog per scaffold instruction; versions + rationale in D-026; DEV_LAUNCH §3/§5/§8 updated)
- [x] Implement Event, Queue, Server, Patient classes (M1: A1–A6 on feat/milestone-1-single-stage-engine) — 2026-09-13 (all M1 Core classes written; 34 tests green; dead-state verified)
- [x] Implement FEL (priority queue) and generic N-stage DES engine — validated against M/M/1 and M/M/2 (per D-006) — 2026-09-13 (FEL + generic single-stage engine; validated vs analytical M/M/1 in EngineTests, 0.724 vs 0.75; M/M/2 + M/M/c table comparison tracked by the "Implement analytical M/M/c validation comparison" row)
- [ ] Implement Statistics collector (avg wait, queue length, utilisation, time-in-system; expose per-server busy times + waiting-time samples for charts — D-019) — M1 core metrics live in SimulationResult.cs; D-019 chart hooks pending
- [ ] Implement per-stage ρᵢ (routing-derived λᵢ) refusal — report ALL unstable stages with λᵢ, cᵢ, μᵢ, ρᵢ (FR-VAL-1) + per-stage ρ display in results panel (FR-STAT-6) + utilisation assertion 0 ≤ util ≤ 1
- [x] Write unit tests for engine — 2026-09-13 (E1–E8: 34 facts across Queue/Event/FEL/RNG/Exponential/Server/Engine/Stability; all green on feat/milestone-1-single-stage-engine)
- [ ] Record the 5-patient hand trace (viva walk-through)
- [x] Implement Excel/CSV loader (ClosedXML + CsvHelper) with dirty-data rejection — 2026-09-13 (src/OpdSimulator.Data/Loaders/ + Validation/DataValidator, per-row issues; LoaderTests 55-german capacity; FR-DATA-1/2/7; D-038)
- [x] Implement MLE fitting (Exponential first, then Normal/Lognormal/Gamma) — 2026-09-13 (5 fitters + DistributionFitterFactory incl. Uniform; MLE σ denominator n, D-043; gamma MoM, D-041; FR-DATA-5)
- [x] Implement chi-square test (auto bins ≈ √n, df = k−1−p, p-value, decision; expose binned data + fitted PDF points for charts — FR-STAT-8) — 2026-09-13 (BinSelector k=⌈√n⌉∈[5,20], ChiSquareTest with fail-loud guards D-040/D-044; O/E arrays exposed in ChiSquareResult; fitted-PDF-points for the P1 chart is the FR-STAT-8 M5 row)
- [x] Implement `p_exit` estimation from `departure_stage` (exclude + warn on Reception rows) — 2026-09-13 (PExitCalculator excludes Reception per CONTEXT §5.4; validator itself is strict Screening/Doctor per kickoff B1 — D-038; FR-DATA-6)
- [~] Implement rate-wise / mean-wise toggle + manual λ override — 2026-09-13 (data layer landed: src/OpdSimulator.Data/Parameters/{ParameterMode,ModeValidator} with soft warnings, FR-DATA-8; the **UI toggle** + manual-λ side-by-side display remain M5 GUI)
- [ ] Extend engine to the 3-stage network with routing (a config change, not a rewrite)
- [ ] Implement clinic calendar (hours 8:15–11:00, closed Fri/Sun, daily cap, run-mode × horizon per D-009)
- [~] Implement event log and step-by-step trace (viva trace file) — 2026-09-13 (engine Debug event trace live per D-036 — FR-VAL-4 is `[x]` in REQUIREMENTS.md; the human-readable trace file + 5-patient hand trace remain from this row)
- [ ] Engine: assign patient to random idle server (log the policy)
- [ ] Statistics: track per-server busy time, compute per-server + stage-level utilisation
- [ ] Statistics: flag imbalance when max−min utilisation > 0.15 (both simulated + historical)
- [ ] Loader: parse optional server-ID columns for historical validation only
- [ ] UI: metrics table shows per-server AND stage-level utilisation columns
- [ ] Build Avalonia UI (left config panel + right results panel; background-thread run + progress)
- [ ] Implement event log rendering
- [ ] (M5) Add LiveCharts2 NuGet package (update DEV_LAUNCH in the same change)
- [ ] (M5) Implement P1 chart: histogram + fitted PDF (input analysis)
- [ ] (M5) Implement P1 chart: chi-square observed vs expected bars
- [ ] (M5) Implement P1 chart: per-server utilisation bar chart
- [ ] (M5) Implement P2 chart: queue length over time
- [ ] (M5) Implement P2 chart: waiting time distribution histogram
- [ ] (M5) Add Input Analysis tab to UI (hosts P1 input charts)
- [ ] (M5) Wire chart updates to simulation-run event
- [ ] Implement analytical M/M/c validation comparison → `validation_report.txt`
- [ ] Implement token generator tab (LAST)
- [ ] Cross-platform CI on GitHub Actions (ubuntu-latest + windows-latest)
- [?] Verify dead-state launch on Linux + Windows (delete `bin/`/`obj/`, follow DEV_LAUNCH alone) — Linux half verified 2026-09-13 (Ubuntu 24.04); Windows half blocked by B-006
- [ ] Produce `DEMO_CHECKLIST` in DEV_LAUNCH (offline pre-restore, single launch command, sample file, click path, fallback)
- [ ] Final README + USER_MANUAL polish
- [ ] Create `.github/workflows/ci.yml` (ubuntu-latest + windows-latest: restore → build --no-restore -c Release → test --no-build -c Release) and add README badge once green (AGENTS §11.8)
- [x] Add Serilog packages (Serilog, Serilog.Sinks.Console, Serilog.Sinks.File) via NuGet (update DEV_LAUNCH in the same change) — 2026-09-13 (Serilog 4.4.0 + Sinks.Console 6.1.1 in Cli; App additionally Serilog.Extensions.Logging 10.0.0 + Sinks.File 7.0.0; see D-026)
- [ ] Configure logging in `appsettings.json`: console + rolling file (7-day retention) + error file sinks (AGENTS §12.1) — template has console + rolling file; error-only sink pending
- [ ] Install global exception handlers in the App project (AppDomain, UnobservedTaskException, Avalonia Dispatcher) → `logs/crash-*.log` + dialog (AGENTS §12.3, 12.5)
- [ ] Apply log-level discipline (Verbose = RNG draws, Debug = event scheduling, Information = run summaries, Warning/Error/Fatal per §12.2)

## Blocked
- (none — B-005 (Avalonia template) and B-006 (Windows dead-state) are pending, nothing blocks current work)

## Done
- [x] FIX (feat/milestone-1-single-stage-engine): average-wait/system-time bug — `totalWaitMinutes`/`totalSystemMinutes` were `long`, truncating every sub-minute wait to 0 (mean wait read 0.41 vs analytical 0.75). Switched to `double` accumulators; EngineTests.Run_MatchesAnalyticalMM1 now green (0.724 within 15% of 0.75) — 2026-09-13
- [x] FIX (docs/reconcile-decisions-and-paths): renumber duplicate decision IDs (D-027b→D-029, D-028b→D-030, D-029→D-031) + update all cross-references — 2026-09-13 (commit 27a1a34)
- [x] FIX (docs/reconcile-decisions-and-paths): align DEV_LAUNCH §3 + D-021/D-027 with template-only rule; log D-032 — 2026-09-13 (commit dee90c2)
- [x] FIX (docs/reconcile-decisions-and-paths): fill PROGRESS.md gap for commit 9537d46 + resume line — 2026-09-13 (commit 2fecab9)
- [x] Initial commit of bootstrap (AGENTS.md, docs/, global.json, scripts/, README, solution scaffold, appsettings.template.json) directly on `main` as the root commit (AGENTS §11.2 exception, D-028) — 2026-09-13 (commit 73787bb, pushed to origin/main)
- [x] Repo cleanup: removed Python-era content (`src/ku_modeling_hospital/`, `tests/`, `data/`, `artifacts/`, `notebooks/`, `pyproject.toml`, `uv.lock`, `.python-version`, `project-ideas.*`, `.venv/`, caches, NOC docs incl. `docs/make_noc.py`); `Simulator Start Prompt.txt` moved to `docs/agent-prompts/kickoff-bootstrap.md` — 2026-09-13
- [x] Create `scripts/run.sh` and `scripts/run.ps1` (already present since 2026-09-13; verified this session, still `chmod +x`) — 2026-09-13
- [x] Choose C# + Avalonia + MathNet.Numerics (+ ClosedXML, CsvHelper, xUnit) — 2026-09-13
- [x] Create initial PRD, CONTEXT, AGENTS documents — 2026-09-13
- [x] Record owner clarifications (effective ρ, Reception exclusion, run-mode × horizon, N-stage generic, demo scope) in PRD §10 (v1.0.1) + CONTEXT [VERIFIED] tags — 2026-09-13
- [x] Log decisions D-001..D-011 in DECISIONS.md — 2026-09-13
- [x] Create `global.json` pinning .NET 8 SDK (`8.0.100`, `rollForward: latestFeature`) — 2026-09-13
- [x] Move AGENTS.md to repo root; confirm DEV_LAUNCH.md + USER_MANUAL.md in `docs/` (D-012) — 2026-09-13
- [x] Create DEV_LAUNCH.md, USER_MANUAL.md, BLOCKERS.md, PROGRESS.md, README.md — 2026-09-13
- [x] Per-stage ρ model: rewrite FR-VAL-1, add FR-STAT-6, bump PRD to v1.1.0 (D-015) — 2026-09-13
- [x] Per-server utilisation model: FR-DATA-10 + FR-STAT-7, operating-time definition, D-016..D-018, PRD v1.2.0 — 2026-09-13
- [x] Charts feature spec: FR-UI-4, FR-STAT-8, NFR-6, D-019, PRD v1.3.0 (implement in M5) — 2026-09-13
- [x] Append AGENTS §11 Version Control protocol (D-020) — 2026-09-13
- [x] Rename local default branch `master` → `main` (`git branch -M main`) — 2026-09-13
- [x] Replace `.gitignore` with owner template (build/IDE/test/NuGet/publish/OS/data/secrets rules) — 2026-09-13
- [x] Append AGENTS §12 Error Monitoring & Debugging (Serilog, log levels, global handlers, crash-log format); DEV_LAUNCH §9.1 Where to Find Logs; gitignore `!appsettings.json`; D-021 — 2026-09-13
- [x] Append AGENTS §13 Session Wrap-Up Format (D-022); §9.4 points to §13; PROGRESS.md handoff header note — 2026-09-13
- [x] Append AGENTS §14 Session Resume Protocol (D-023); DEV_LAUNCH §11 Resuming Work (Changelog → §12); PROGRESS header note extended — 2026-09-13
- [x] AGENTS §9.1: REQUIREMENTS.md row → traceability matrix + source-of-truth rule; new §9.7 Traceability Discipline (D-024) — 2026-09-13
- [x] Create `docs/REQUIREMENTS.md` (PRD v1.3.0 traceability matrix, 46 reqs, all `[ ]`; FR-SIM-11 placeholder dropped; traceability gate line added under TODO completion rule) — 2026-09-13
- [x] AGENTS §9.7: add Bidirectional rule (no orphan/placeholder/speculative rows); D-025; REQUIREMENTS.md audit + Changelog row — 2026-09-13