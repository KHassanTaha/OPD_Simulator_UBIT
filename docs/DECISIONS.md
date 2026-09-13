# DECISIONS.md — Design & Architecture Decision Log

Every design/architecture choice is logged here the moment it is made. Never
batched. Each entry: date, decision, rationale, implementation details,
impact (positive and negative), alternatives considered.

---

## D-001 Lang/stack: C# on .NET 8 LTS

- **Date:** 2026-09-13
- **Decision:** Build the simulator in C# targeting .NET 8 LTS.
- **Rationale:** Agent instructions require a stable current LTS that is cross-platform (Linux dev, Windows deploy). .NET 8 is the current LTS; SDK 8.0.131 is already installed on the dev machine. PRD NFR-5 confirms.
- **Implementation details:** `dotnet` CLI; classic solution layout under `OpdSimulator/`. No `global.json` yet (SDK version pinned later if needed).
- **Impact (+):** Official LTS support; single `dotnet build`/`dotnet run` on Linux and Windows.
- **Impact (−):** None identified versus alternatives.
- **Alternatives considered:** .NET 9 (STS, not LTS at decision time), .NET 6 (end-of-support window), Python (rejected — project moved from Python ML to C# DES).

## D-002 GUI framework: Avalonia UI

- **Date:** 2026-09-13
- **Decision:** Use Avalonia UI (MVVM) for the desktop app.
- **Rationale:** Requirement is "modern, clean UI with advanced widgets", cross-platform with first-class Linux support. Avalonia is the only mainstream .NET XAML framework with genuinely supported Linux desktop targets, a rich widget set (DataGrid, charts), and strong MVVM.
- **Implementation details:** `OpdSimulator.App` targets Avalonia; Views/ViewModels/Controls folders; DI via `Microsoft.Extensions.DependencyInjection`. Templates installed via `dotnet new install Avalonia.Templates`.
- **Impact (+):** Single codebase for Linux + Windows; MVVM keeps Core UI-free.
- **Impact (−):** Avalonia runs on Linux via X11/Wayland — runtime libraries must be present on demo machines (offline demo pre-restore required, see DEV_LAUNCH).
- **Alternatives considered:** .NET MAUI (Linux desktop not officially supported — rejected); Uno Platform (heavier toolchain, WinUI-oriented — rejected for a small 2-panel toolbar app).

## D-003 Statistics library: MathNet.Numerics

- **Date:** 2026-09-13
- **Decision:** Use MathNet.Numerics for distributions, random variate generation, and chi-square support.
- **Rationale:** Cross-platform, pure managed, well-documented; provides Exponential/Normal/Lognormal/Gamma distributions, CDFs (needed for expected-bin computation in chi-square GoF), and the ChiSquared distribution for p-values. Exponential MLE is closed-form (λ̂ = 1/mean) — implemented by hand so every line is defensible in the viva.
- **Implementation details:** `OpdSimulator.Core` references MathNet.Numerics. Nested χ² critical values (df, α) obtained from the ChiSquared CDF rather than lookup tables.
- **Impact (+):** One trusted source for rare-function math (gamma CDF, ln-gamma); self-contained reimplementation of the core tests for the viva.
- **Impact (−):** Third-party dependency to list in the viva + DEV_LAUNCH; adds nuance that "fail to reject ≠ correct" must be explained.
- **Alternatives considered:** Meta.Numerics (smaller ecosystem — rejected); hand-rolled math only (unbounded scope for gamma/normal CDFs — rejected).

## D-004 Data I/O libraries: ClosedXML + CsvHelper

- **Date:** 2026-09-13
- **Decision:** Use ClosedXML for `.xlsx` and CsvHelper for `.csv` in `OpdSimulator.Data`.
- **Rationale:** PRD FR-DATA-1 and NFR-5 mandate `.xlsx` (primary, Google-Sheets-exports) and `.csv` (fallback). Both libs are MIT-licensed and cross-platform.
- **Implementation details:** Loader maps uploaded sheet/CSV columns (`arrival_time`, `<stage>_start/end`, `departure_stage`) onto a canonical patient record.
- **Impact (+):** Handles both file types; avoids hand-rolled XLSX parsing.
- **Impact (−):** Two extra NuGet packages to document.
- **Alternatives considered:** EPPlus (paid licence for commercial use — rejected); NPOI (heavier — rejected).

## D-005 Unit test framework: xUnit

- **Date:** 2026-09-13
- **Decision:** Use xUnit for `OpdSimulator.Core.Tests` and `OpdSimulator.Data.Tests`.
- **Rationale:** Default, lightweight, most common for modern .NET; good `dotnet test` integration on Linux and Windows CI.
- **Implementation details:** `Microsoft.NET.Test.Sdk` + `xunit` + `xunit.runner.visualstudio`.
- **Impact (+):** Simple, conventional, CI-friendly.
- **Impact (−):** None material.
- **Alternatives considered:** NUnit (attribute styles differ, no benefit here); MSTest (heavier conventions).

## D-006 DES engine is N-stage generic from day one

- **Date:** 2026-09-13
- **Decision:** The Core engine models an N-stage serial network; the 3-stage OPD layout (Reception 1 / Screening 2 / Doctor 3 + `p_exit`) is a configuration.
- **Rationale:** PRD FR-DATA-9 and the Wednesday-demo data (single Screening stage) both require that adding/removing stages is a config change, not a rewrite. Milestone 1 still validates against M/M/1 and M/M/2 analytically.
- **Implementation details:** `Stage` = queue + c servers + service distribution; `Engine` iterates over an ordered stage list; routing table decides next stage vs exit.
- **Impact (+):** Demo (Screening-only, M/M/2) and final (3-stage) both run on the same engine.
- **Impact (−):** Slightly more design work up front than a literal M/M/1-first build.
- **Alternatives considered:** Strict M/M/1 single-stage first (simplest, but would be reworked for the demo — rejected); M/M/1 core + abstract stage layer (middle ground — superseded by full generic).

## D-007 ρ guard uses effective per-stage arrival rate

- **Date:** 2026-09-13
- **Decision:** ρ for each stage = λ_eff / (c·μ) where λ_eff = λ for Reception/Screening and λ·(1 − p_exit) for Doctor. The ρ ≥ 1 refusal runs on effective rates.
- **Rationale:** Only non-exited patients reach the Doctor stage; raw λ would falsely reject stable configurations and mis-report stability.
- **Implementation details:** Engine computes per-stage λ_eff from p_exit before the stability check; refusal message names the offending stage.
- **Impact (+):** Correct stability screen, consistent with physical reality.
- **Impact (−):** None.
- **Alternatives considered:** Raw λ (conservative but incorrect — rejected); ask professor (deferred, no need).

## D-008 `departure_stage = Reception` handled as data anomaly

- **Date:** 2026-09-13
- **Decision:** Rows with `departure_stage = "Reception"` are warned about on upload and excluded from `p_exit`; never routed.
- **Rationale:** Leaving at Reception is reneging — explicitly out of scope. Including them would bias `p_exit` and imply a false third route.
- **Implementation details:** Data loader counts and reports such rows; `p_exit` denominator uses only {Screening, Doctor}.
- **Impact (+):** Model and `p_exit` stay consistent with the 3-stage flow.
- **Impact (−):** Rows are effectively dropped — report count to the user so nothing is silent.
- **Alternatives considered:** Model as valid early exit (adds reneging — rejected); CONTEXT typo theory (verified with owner: real cases must be handled).

## D-009 Run mode = structure, time horizon = span

- **Date:** 2026-09-13
- **Decision:** `Run mode` selects single-day vs multi-day structure; `time horizon` selects the simulated span (single-day: 15 min / 1 hour / full day; multi-day: 1 week / 1 month / custom days).
- **Rationale:** The two dropdowns overlap in the PRD; this separates "how many days" from "how much of that span".
- **Implementation details:** Horizon dropdown options filter by run mode; closed days and per-day cap reset apply in multi-day.
- **Impact (+):** Unambiguous user semantics; single code path for both modes.
- **Impact (−):** Slightly more UI logic (dependent dropdowns).
- **Alternatives considered:** Drop run mode entirely (PRD lists both — rejected); get professor ruling (deferred, non-blocking).

## D-010 All living docs live in `docs/`

- **Date:** 2026-09-13
- **Decision:** PRD, CONTEXT, DECISIONS, REQUIREMENTS, TODO, PROGRESS, BLOCKERS, DEV_LAUNCH, USER_MANUAL, and AGENTS.md all live in `docs/`.
- **Rationale:** Owner request (2026-09-13) moved DEV_LAUNCH.md and USER_MANUAL.md from the repo root into `docs/`; §4's canonical layout already places the rest there.
- **Implementation details:** CAPS.md names in §9.1/§10 refer to `docs/<file>`.
- **Impact (+):** One documentation home; dead-state and manual guidance colocated with specs.
- **Impact (−):** None.
- **Alternatives considered:** Root-level DEV_LAUNCH/USER_MANUAL (original Agent §10 wording — superseded by owner).

## D-011 Demo scope (2026-09-16): CLI path, not GUI

- **Date:** 2026-09-13
- **Decision:** The Wednesday demo targets Milestones 1–2 only: load sample data → fit distributions (MLE) → chi-square GoF → run a Screening-only (M/M/2) simulation and print stats + trace, via the CLI/engine path. GUI is not required for the demo.
- **Rationale:** Three days to a demo; the CLI path proves the data→fit→sim→stats spine with far less risk than a full Avalonia GUI.
- **Implementation details:** `OpdSimulator.Cli` gets commands for fit/simulate; DEV_LAUNCH `DEMO_CHECKLIST` covers offline restore for the demo machine.
- **Impact (+):** Achievable, defensible demo; engine + validation visible.
- **Impact (−):** GUI demo value is deferred; UI still targeted for final delivery.
- **Alternatives considered:** Full GUI demo in 3 days (high risk — rejected); postpone demo expectations (demo date fixed by group/class — rejected).

## D-012 AGENTS.md at repo root; DEV_LAUNCH + USER_MANUAL confirmed in `docs/`

- **Date:** 2026-09-13
- **Decision:** AGENTS.md lives at the repository root; DEV_LAUNCH.md and USER_MANUAL.md stay in `docs/`. Partially supersedes D-010 for AGENTS.md.
- **Rationale:** Owner confirmed 2026-09-13 that the Agent Instructions belong where opencode and graders look first (repo root), while the two launch/user documents remain in `docs/` with the rest of the living docs.
- **Implementation details:** Root `AGENTS.md` replaces the deprecated Python-era instructions; Agent §10.1 and README already reference `docs/DEV_LAUNCH.md` and `docs/USER_MANUAL.md`.
- **Impact (+):** Single, discoverable AGENTS.md; one docs home for the remaining living documents.
- **Impact (−):** D-010's "all living docs incl. AGENTS" is no longer literal.
- **Alternatives considered:** Keep AGENTS.md in `docs/` (repository root still held the deprecated copy — rejected); root for all three launch/user/agent docs (rejected by owner).

## D-013 global.json pins .NET SDK 8.0.100 with `rollForward: latestFeature`

- **Date:** 2026-09-13
- **Decision:** Add `global.json` at the repo root: `version: 8.0.100`, `rollForward: latestFeature`.
- **Rationale:** Locks the toolchain to the .NET 8.0.1xx feature band while tolerating patch-level drift. Dev machine runs SDK 8.0.131, which resolves under `latestFeature`.
- **Implementation details:** File placed at repo root; DEV_LAUNCH §1 documents the pin and its failure mode. Update the file (and DEV_LAUNCH) whenever the SDK policy changes.
- **Impact (+):** Reproducible, deliberate SDK resolution on Linux and Windows; build fails loudly if no 8.0.1xx SDK is installed.
- **Impact (−):** A machine with only a different feature band (e.g., 8.0.4xx) also rolls forward to the newest 8.0.x — acceptable, documented.
- **Alternatives considered:** Pin exact `8.0.100` (would require installing an older SDK on this machine — rejected); no `global.json` (non-reproducible — rejected).

## D-014 No synthetic sample data

- **Date:** 2026-09-13
- **Decision:** Do not generate a fabricated `samples/sample_patients.*`; wait for the owner's real sample file (BLOCKERS B-004).
- **Rationale:** CONTEXT §5.5 documents the real demo format (one Screening stage, `p_exit = 1.0`); a hand-made sample could mask dirty-data behaviour and differ from the real file, weakening demo validity.
- **Implementation details:** `samples/` gets a `.gitkeep` placeholder; B-004 tracks delivery; sample-dependent TODO items stay unstarted.
- **Impact (+):** Demo data matches reality; loader behaviour tested against real quirks.
- **Impact (−):** M2 sample-path work and DEV_LAUNCH §7 verification wait on the owner.
- **Alternatives considered:** Generate synthetic xlsx + csv now (rejected by owner 2026-09-13).

## D-015 Per-stage ρ via routing probabilities (refines D-007)

- **Date:** 2026-09-13
- **Decision:** ρ is computed per stage as ρᵢ = λᵢ / (cᵢ·μᵢ), where λᵢ is derived from the external arrival rate λ₀ and the routing probabilities (e.g., λ_screening = λ₀, λ_doctor = λ₀·(1 − p_exit)). The pre-run stability check reports **all** unstable stages (with λᵢ, cᵢ, μᵢ, ρᵢ), and the results panel displays ρᵢ for every stage.
- **Rationale:** In a multi-server, multi-stage network there is no single whole-system ρ — traffic intensity is defined only stage-by-stage. D-007 established effective per-stage rates; this decision makes the per-stage definition and its derivation from routing explicit and refines the refusal/output behaviour.
- **Implementation details:** PRD §5.4 — FR-VAL-1 rewritten as the per-stage stability check, new FR-STAT-6 (per-stage ρ display); PRD §8 assumption 11 reworded; CONTEXT §2.3 extended with the per-stage ρ + routing explanation; PRD version bumped to v1.1.0, §10 change history updated.
- **Impact (+):** Bottleneck stage visible at a glance; stable vs. unstable diagnosis is complete and actionable.
- **Impact (−):** Slightly more output to display; refusal message now enumerates every unstable stage.
- **Alternatives considered:** Single global ρ (meaningless in multi-stage networks — rejected); keep the vague "effective rate" wording (superseded — replaced by routing-derived λᵢ); literal FR-SIM-6 placement of the display requirement (rejected — FR-SIM-6 already means "services in progress continue to completion").

## D-016 Simulated per-server utilisation always available

- **Date:** 2026-09-13
- **Decision:** The engine tracks busy time per individual server and reports per-server utilisation for every stage, independent of input columns.
- **Rationale:** The engine assigns patients to servers, so it always knows who served whom. Per-server granularity reveals load imbalance that stage-level averages hide (e.g., 100% + 0% averages to 50%).
- **Implementation details:** Each `Server` object tracks `TotalBusyTime`; the statistics collector aggregates per-server and stage-level values. Reports the imbalance flag when max − min utilisation > 0.15, for both simulated and historical values.
- **Impact (+):** Per-server visibility; a stronger viva answer.
- **Impact (−):** Extra aggregation logic and UI columns.
- **Alternatives considered:** Report stage-level only (rejected — hides imbalance, weaker viva answer); require server-ID columns in input (rejected — blocks real data).

## D-017 Server assignment policy: random among idle

- **Date:** 2026-09-13
- **Decision:** When multiple servers at a stage are idle, the incoming patient is assigned to one chosen uniformly at random.
- **Rationale:** Matches the real clinic ("patient can go to either table"). Avoids artificial load imbalance from lowest-ID preference.
- **Implementation details:** Engine uses the seeded RNG for the assignment, so runs remain reproducible (FR-VAL-3).
- **Impact (+):** Realistic, no starvation of any server.
- **Impact (−):** Per-server busy times vary run-to-run (controlled by seed).
- **Alternatives considered:** Round-robin (deterministic, less realistic — rejected); shortest queue (realistic but couples stages — rejected); lowest-ID (unrealistic, creates starvation — rejected).

## D-018 Operating time = first arrival → last service end

- **Date:** 2026-09-13
- **Decision:** The utilisation denominator ("operating time") per day = time from the day's first arrival to its last service end, identical for historical and simulated values.
- **Rationale:** The system is only genuinely active between those two moments. A fixed 8:15–11:00 window would let close-time overtime push utilisation toward (and past) 1, violating FR-VAL-2's 0 ≤ util ≤ 1 assertion.
- **Implementation details:** Denominator computed per day from the actual service timeline; used for both stage-level and per-server utilisation.
- **Impact (+):** util ∈ [0, 1] guaranteed; one consistent denominator across both sources.
- **Impact (−):** Denominator is load-dependent (differs day to day); must be displayed alongside utilisation for transparency.
- **Alternatives considered:** Fixed 8:15–11:00 window (rejected — overtime breaks util ≤ 1); run horizon (rejected — denominators differ between sources).

## D-019 Charts via LiveCharts2

- **Date:** 2026-09-13
- **Decision:** Use LiveCharts2 for all in-app charts. Include P1 charts (histogram + fitted PDF, chi-square bars, utilisation bars) in v1. P2 charts (queue length over time, waiting time histogram) as time permits.
- **Rationale:**
  (a) Simulation output analysis is standard practice; tables alone are insufficient for a strong viva.
  (b) LiveCharts2 is MVVM-native, cross-platform (Linux + Windows), and produces presentation-quality visuals.
  (c) Histogram + fitted PDF is the canonical visual proof of a distribution fit.
- **Implementation details:** Charts live in the GUI (M5), driven by data the M1 statistics collector (per-server busy time, waiting-time samples) and M2 fitting module (binned data + fitted PDF curve points) already expose. Fit histogram bin count = chi-square bin count (FR-STAT-3 / FR-STAT-8). New "Input Analysis" tab hosts the input charts; results-panel charts refresh after each run (NFR-6 rendering budget: < 500 ms / 10k points, non-blocking).
- **Impact (+):** Stronger viva; bottleneck stage visible at a glance; fits visually verifiable before reading p-values.
- **Impact (−):** New dependency; more UI surface; rendering budget must be honored via downsampling when needed.
- **Alternatives considered:** OxyPlot (mature but visually dated — rejected on presentation quality); ScottPlot (excellent for science, overkill for our scope — rejected); custom SkiaSharp (too much work for marginal benefit — rejected); no charts (rejected — weakens the viva, ignores standard simulation practice).

## D-020 Git/GitHub workflow: trunk-based, conventional commits, CI

- **Date:** 2026-09-13
- **Decision:** Adopt AGENTS.md §11 as the version-control protocol: default branch `main` (local `master` renamed to `main`), trunk-based development with short-lived feature branches (`feat/`, `fix/`, `docs/`, `chore/`), Conventional Commits, `dotnet build && dotnet test` gate before every commit, review-merge to `main` (no direct pushes, no auto-PRs, no force-push). CI: `.github/workflows/ci.yml` on `ubuntu-latest` + `windows-latest` (restore/build/test) once the solution builds locally; README badge added once green.
- **Rationale:** A clean, always-green `main` is required by the milestone completion protocol, and the viva demands attributable, reviewable history. Raw patient data is not committable (AGENTS §11.3), so the demo sample stays anonymised; raw files are gitignored.
- **Implementation details:** `git branch -M main` executed 2026-09-13 (local only; remote untouched until the first push). `.gitignore` replaced 2026-09-13 with the owner's template (build artifacts `[Bb]in/`/`[Oo]bj/`/logs, IDE `.vs/`/`.idea/`, test results, NuGet packages, publish output, OS junk, `samples/*.xlsx`+`*.csv` ignored except `sample_patients.xlsx`, secrets `*.env`/`secrets.json`/`appsettings.*.json`). First bootstrap commit goes on short-lived `chore/bootstrap`.
- **Impact (+):** Reproducible reviewable history; CI catches cross-platform breakage early.
- **Impact (−):** Merge discipline adds ceremony; CI workflow exists only after the solution scaffolds.
- **Alternatives considered:** Long-lived dev branch (rejected — extra merge complexity); keep local `master` (rejected — mismatch with §11.1 and GitHub default); commit real patient data (rejected — privacy).

## D-021 Logging & error monitoring: Serilog + global exception handlers

- **Date:** 2026-09-13
- **Decision:** Adopt AGENTS.md §12: Serilog with Console, rolling file `logs/app-YYYYMMDD.log` (7-day retention) and error-only `logs/errors-YYYYMMDD.log` sinks; configuration in committed base `appsettings.json`; log-level discipline (Verbose RNG draws → Fatal exit); App project installs global handlers for `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` and Avalonia `Dispatcher.UnhandledException`, each writing `logs/crash-YYYYMMDD.log` (ISO timestamp, version, args, sim state, full exception, environment) and showing a user dialog.
- **Rationale:** Supersedes the provisional "e.g. Microsoft.Extensions.Logging" note in AGENTS §3 with a concrete, cross-platform, structured choice. Crash logs are the primary debugging evidence for the viva (§12.6), and the empty-catch ban (AGENTS §12.4) keeps failures loud rather than silent.
- **Implementation details:** `!appsettings.json` negation added to `.gitignore` so the base config is committable while `appsettings.*.json` environment/secret variants stay ignored. DEV_LAUNCH §9.1 documents where to find logs plus Linux/Windows tail commands.
- **Impact (+):** Structured, searchable logs; reproducible debugging story for the viva; loud failures.
- **Impact (−):** Serialized-to-disk I/O per event; extra packages and config surface.
- **Alternatives considered:** Microsoft.Extensions.Logging (no Serilog-level structured console/file sinks — replaced); plain file writes (no envelope, no rolling — rejected); no logging (rejected — impossible to debug or defend in the viva).

> **Note:** Superseded by D-027: only `appsettings.template.json` is committed; `appsettings.json` is gitignored.

## D-022 Session wrap-up format standardized

- **Date:** 2026-09-13
- **Decision:** Adopt AGENTS.md §13: every session end (or "wrap up") produces the exact Session Handoff block — ISO 8601 timestamp, branch & status, Done (only TODO `[x]` items), In Progress, What is complete/remains, Next Session Should Start With, Blocked (BLOCKERS.md link), Git State, Build & Test (never blank), Files Touched, Decisions (DECISIONS.md anchors), Assumptions (CONTEXT.md tags), Notes. Saved to the top of `docs/PROGRESS.md` (newest first) and pasted into the chat.
- **Rationale:** §9.4's loose "short handoff" produced inconsistent handoffs; a fixed template forces cross-checked, non-blank state handoffs and gives the owner an auditable end-of-session contract.
- **Implementation details:** §9.4 end-protocol bullet now points to the §13 format; PROGRESS.md carries a header note that handoffs are stored newest-first at the top.
- **Impact (+):** Predictable, complete session summaries; stale-state drift caught at every wrap.
- **Impact (−):** Requires running `dotnet build`/`dotnet test` at each session end before wrapping.
- **Alternatives considered:** Keep loose paragraph handoff (rejected — inconsistent); handoff-only-on-request variable structure (rejected — template must be fixed); store handoffs at bottom of PROGRESS.md (rejected — newest-first at top is the convention).

## D-023 Session resume protocol (cold-start reconciliation)

- **Date:** 2026-09-13
- **Decision:** Adopt AGENTS.md §14: on "RESUME SESSION", run a mandatory cold-start reconciliation — read persistent files in a fixed order (AGENTS → TODO → PROGRESS top 3 → BLOCKERS → DECISIONS last 5 → DEV_LAUNCH Last-verified), inspect reality (git status/log/branch, newest crash log), detect drift ([~] tasks without matching [x], uncommitted changes, crash logs newer than PROGRESS, stale DEV_LAUNCH verification, doc disagreements), then produce the exact 6-line state summary and wait for "go". Reality wins over recorded state; "cannot determine" beats guessing; others' uncommitted changes are never discarded. Result is a single `## Resume — <ts> — reconciled: N findings` line in PROGRESS.md.
- **Rationale:** Agent sessions end abruptly (crash, timeout, exhausted context); a cheap, scripted reconciliation catches state drift before any work starts. DEV_LAUNCH §11 gives the owner the human-side resume flow (clean vs abrupt end, the resume prompt, and the "tell the new session what broke" line).
- **Implementation details:** DEV_LAUNCH renumbered so the new "Resuming Work" section is §11 and the Changelog moved to §12.
- **Impact (+):** Reliable hand-offs, drift detected in ~1 minute, no lost work.
- **Impact (−):** Slight ceremony at resume time; REQUIREMENTS.md is absent until its creation task lands, so reconciliation will list it as missing meanwhile.
- **Alternatives considered:** Trust PROGRESS.md alone on resume (rejected — stale state risk); full manual re-read without git cross-check (rejected — reality may differ); skip reconciliation and continue cold (rejected — mid-task ambiguity).

## D-024 REQUIREMENTS.md as PRD traceability matrix

- **Date:** 2026-09-13
- **Decision:** Redefine `REQUIREMENTS.md` from "consolidated superset of PRD" to a **derived traceability matrix**: every PRD requirement maps to status → source file → test → decision ID. PRD.md is the source of truth; if they disagree, PRD wins and the matrix must be reconciled immediately (AGENTS §9.1 source-of-truth rule).
- **Rationale:** A superset duplicate of PRD invites silent drift; a per-requirement matrix makes the viva trace (requirement → implementation file → unit test → design decision) auditable and is cheap to maintain alongside §9.7 discipline.
- **Implementation details:** AGENTS §9.1 row updated (traceability-matrix purpose; update triggers = status/source/test changes); §9.7 Traceability Discipline added ([~] on start, [x] + Source + Test on completion, Decision filled on design impact, re-ID rows in-session, ≤ 1 session of drift). TODO creation item reworded to the traceability-matrix framing.
- **Impact (+):** Source-of-truth clarity; viva-ready requirement/test/decision chain; drift caught quickly.
- **Impact (−):** Matrix must be maintained on every requirement change; one more file to keep current from the first milestone.
- **Alternatives considered:** Keep REQUIREMENTS.md as a superset snapshot (rejected — duplicates PRD, encourages drift); make REQUIREMENTS.md the master (rejected — PRD was authored and versioned as product spec); no matrix, rely on PRD alone (rejected — loses status/source/test/decision linkage).

## D-025 Drop orphan FR-SIM-11 from traceability matrix; add bidirectional rule

- **Date:** 2026-09-13
- **Decision:** Formalize the removal of the orphan FR-SIM-11 row (dropped during initial REQUIREMENTS.md creation, per owner approval) and add a **bidirectional rule** to AGENTS §9.7: REQUIREMENTS.md must not contain any row absent from PRD.md; orphan rows are deleted unless the owner explicitly promotes them into PRD.md; placeholder/"to be decided"/speculative rows are forbidden.
- **Rationale:** The matrix is a derived view of PRD (D-024); invented or speculative rows reintroduce the drift the traceability discipline exists to prevent, and mislead the viva trace.
- **Implementation details:** AGENTS §9.7 now ends with the bidirectional rule. REQUIREMENTS.md audited against PRD v1.3.0 — zero orphan rows; Changelog row added; "Last synced" already reads 2026-09-13 (v1.3.0), unchanged.
- **Impact (+):** One-way-only PRD → matrix mapping; orphan/placeholder rows eliminated by construction.
- **Impact (−):** Any new requirement must first be added to PRD.md (an extra step for ad-hoc ideas).
- **Alternatives considered:** Keep FR-SIM-11 as a placeholder pending PRD (rejected by owner — projector of invented requirements); allow speculative rows to guide future work (rejected — violates source-of-truth and clamp, and PRD.md is the change channel for new ideas).

## D-026 NuGet package versions (pinned at scaffold)

- **Date:** 2026-09-13
- **Decision:** Pin the following NuGet package versions, resolved as latest-stable on the scaffold date, explicitly listed per project:
  - `OpdSimulator.Core` — MathNet.Numerics **5.0.0**
  - `OpdSimulator.Data` — ClosedXML **0.105.1**, CsvHelper **33.1.0**
  - `OpdSimulator.App` — Avalonia **11.3.3**, Avalonia.Desktop **11.3.3**, Avalonia.Themes.Fluent **11.3.3**, CommunityToolkit.Mvvm **8.4.2**, LiveChartsCore.SkiaSharpView.Avalonia **2.0.5**, Serilog **4.4.0**, Serilog.Extensions.Logging **10.0.0**, Serilog.Sinks.Console **6.1.1**, Serilog.Sinks.File **7.0.0**
  - `OpdSimulator.Cli` — Serilog **4.4.0**, Serilog.Sinks.Console **6.1.1**
  - Test projects — xUnit + runner from the SDK template (pinned by the template itself)
- **Rationale:** Latest stable pins avoid silent upgrades breaking the validated build; the exact list is required for DEV_LAUNCH and the viva's "external libraries" disclosure (AGENTS §8). Avalonia is pinned to the **11.3.3** line, **not** the newer 12.1.2: 12.1.2's bundled analyzers reference Roslyn 4.14, newer than the .NET 8 SDK's compiler 4.8, which produced `CS9057` warnings (2) — violating the project's zero-warning policy. 11.3.3 builds warning-free on the .NET 8 SDK and satisfies LiveCharts 2.0.5's `Avalonia ≥ 11.0.0` dependency (verified from its nuspec). A later upgrade to Avalonia 12.x is a reviewed change (needs SDK ≥ 9).
- **Implementation details:** Versions written directly into each `.csproj` by `dotnet add package` (no version ranges). `OpdSimulator.App` references the UI packages but is a classlib placeholder this session (no Avalonia template installed — B-005), so the packages are not exercised until M5.
- **Impact (+):** Reproducible restore; visible dependency list for documentation.
- **Impact (−):** Manual version bumps required later; a future Breaking Change in any major (e.g., Avalonia 13) needs a reviewed upgrade.
- **Alternatives considered:** Version ranges (`8.*`) — rejected (non-reproducible); add packages later per milestone (rejected — owner specified them at scaffold time).

## D-027 `appsettings.template.json` must ship in the repo

- **Date:** 2026-09-13
- **Decision:** `.gitignore` negates `appsettings.template.json` (`!appsettings.template.json`) so the config template is committed, while `appsettings.*.json` (including the local `appsettings.json` created by copying the template) stays ignored.
- **Rationale:** DEV_LAUNCH §3 instructs fresh clones to `cp appsettings.template.json appsettings.json`. Under the un-negated owner template, `appsettings.*.json` matched the template too, so a fresh clone would lack it and the bootstrapping step would fail (dead-state rule, AGENTS §10.4). Deviance from the exact owner block is deliberate and minimal.
- **Implementation details:** `.gitignore` "Configuration" section: `appsettings*.json`, then `!appsettings.template.json`. Verified with `git status`: template shows as untracked/committable, `appsettings.json` and per-env variants stay ignored.
- **Impact (+):** Template is present in the bootstrap commit and on any fresh clone; §3 copy step works from a dead state.
- **Impact (−):** Minor deviation from the literal owner-provided `.gitignore` block (order/negation preserved otherwise).
- **Alternatives considered:** Keep template local-only and have first run create `appsettings.json` (rejected — breaks dead-state §3); rename to `appsettings.template.json.example` (rejected — owner preferred the negation).

## D-028 Root commit on `main`; §11.2 applies from second commit onward

- **Date:** 2026-09-13
- **Decision:** The repository's very first commit (the bootstrap: AGENTS, docs/, global.json, scripts/, scaffold, config templates) goes **directly on `main`**. From the second commit onward, AGENTS §11.2 feature branches apply in full.
- **Rationale:** There is no upstream base branch yet for a pull request — a `feat/` branch would have nothing to merge into but the same commit. Standard practice is a root commit on the trunk; this matches the AGENTS §11.2 exception already present and is a one-time-only carve-out for the root commit.
- **Implementation details:** `git add -A && git commit` on `main` (conventional `chore:` message). AGENTS §11.2 "Exception — root commit" now references D-028. `Simulator Start Prompt.txt` moved to `docs/agent-prompts/kickoff-bootstrap.md` so the starting prompt is archived in the repo rather than littering the root.
- **Impact (+):** Clean single-branch start; history begins on the trunk as PR tooling expects.
- **Impact (−):** None material — the root commit is the only exception; all later branches follow §11.2.
- **Alternatives considered:** Create `chore/bootstrap` and PR-merge the root (no base exists — rejected); keep the file at root (rejected by owner — archive it in `docs/agent-prompts/`).

## D-029 appsettings.template.json Committed via Negation

- **Date:** 2026-09-13
- **Decision:** Keep `appsettings.template.json` committed by adding `!appsettings.template.json` after the `appsettings.*.json` ignore pattern in .gitignore.
- **Rationale:**
  (a) Standard .NET idiom — recognisable to any reviewer.
  (b) The filename is self-documenting.
  (c) Preserves the fresh-clone flow in DEV_LAUNCH.md §3.
- **Alternatives:**
  (a) Rename to `appsettings.json.example` — works, but non-standard and the negation pattern is cleaner.
  (b) Leave template ignored — rejected: forces manual config creation on every fresh clone, breaks the dead-state promise.
- **Impact:** .gitignore config section grows by one line. DEV_LAUNCH.md §3 gains a single copy step. No code impact.

## D-030 Single Canonical Location for Docs Instructions

- **Date:** 2026-09-13
- **Decision:** Each instruction in DEV_LAUNCH.md and USER_MANUAL.md has one canonical location. Duplicates are consolidated and replaced with cross-references.
- **Rationale:** Duplicated instructions drift apart over time; readers cannot trust either version. One location = one source of truth.
- **Alternatives:** (a) Keep both — rejected: guaranteed drift. (b) Delete both, put in a separate file — rejected: over-splitting.
- **Impact:** DEV_LAUNCH §3 consolidated. New rule added to AGENTS.md §10.7. Agents must grep before marking docs tasks done.

## D-031 Root commit goes directly on `main`

- **Date:** 2026-09-13
- **Decision:** The repository's very first commit (bootstrap: AGENTS.md, docs/, global.json, scripts/, README, solution scaffold, appsettings.template.json) goes directly on `main` as the root commit. All subsequent changes use short-lived feature branches per AGENTS §11.2. Supersedes the `chore/bootstrap` branch plan recorded in D-020's implementation details.
- **Rationale:** A branch can only exist relative to an existing commit; splitting the bootstrap onto a branch that merges into an empty `main` adds ceremony with no review value. The Conventional Commit + build/test gate still applies to the root commit, and the owner reviews it before it is created.
- **Implementation details:** AGENTS §11.2 gains the "Exception — root commit" note; TODO bootstrap item updated from `chore/bootstrap` to direct-on-`main`; D-020's `chore/bootstrap` wording is superseded by this entry.
- **Impact (+):** Simplified bootstrap; `main` starts with the full documented state; no throwaway branch.
- **Impact (−):** The one commit that skips the feature-branch flow is the largest (the whole scaffold) — mitigated because it is the root, reviewed by the owner before creation.
- **Alternatives considered:** Keep the `chore/bootstrap` branch (recorded plan, but adds no review value over a reviewed root commit — superseded); split the scaffold across multiple bootstrap commits (rejected — history would pretend the scaffold grew incrementally when it did not).

## D-032 Reconciliation fixes gated before M1

- **Date:** 2026-09-13
- **Decision:** The documentation-hygiene drifts found during the AGENTS §14 reconciliation were fixed on a dedicated `docs/` branch before any Milestone 1 code: duplicate decision IDs renumbered, appsettings-gitignore doc claims realigned with the actual rule, PROGRESS.md history gap filled.
- **Rationale:** Decision-ID collisions corrupt traceability — a REQUIREMENTS.md row or viva citation can point at two different decisions under one ID, or at none. Stale claims about a `!appsettings.json` negation would mislead anyone debugging the config flow.
- **Implementation details:** The three tail entries of DECISIONS.md collided with earlier IDs; renumbered to D-029 (template-negation draft), D-030 (single canonical location), D-031 (root-commit draft). The canonical root-commit entry stays D-028. Cross-references corrected in PROGRESS.md, DEV_LAUNCH.md, TODO.md (AGENTS.md already pointed at the canonical D-028 and needed no change; verified by re-grep). DEV_LAUNCH §3 and D-021/D-027 corrected to the actual rule (`appsettings*.json` ignored, only `!appsettings.template.json`). PROGRESS.md gained the missing `9537d46` entry.
- **Impact (+):** M1 starts from a clean, unambiguous, traceable doc state.
- **Impact (−):** None — doc-only changes, no application code.
- **Alternatives considered:** Fixing the drifts silently inside M1 commits (rejected — conflates doc hygiene with feature work); deferring until the viva (rejected — traceability is a live artifact that must stay current).

## D-033 FEL ordering: time → event type → patient id

- **Date:** 2026-09-13
- **Decision:** The Future Event List (`Event` class implementing `IComparable<Event>`) orders events by simulation time, then by `EventType` enum value (`Arrival=0 < ReceptionEnd=1 < ScreeningEnd=2 < DoctorEnd=3`), then by patient id. Ties at equal time are broken by type; equal time **and** type break by patient id (smaller first).
- **Rationale:** DES correctness is order-sensitive: at an identical clock time the events a model generates are not physically simultaneous (or must be given an agreed order), and the choice changes waits/queue lengths. A **total, documented order** makes the FEL deterministic for the same seed — required by NFR-4 and the REPRODUCIBLE enforcement rule. The type ordering (Arrival before Service End at the same instant) is a stated convention so the viva can be answered precisely: a patient arriving at the exact instant a server frees up is handed to that server.
- **Implementation details:** `Event.CompareTo` compares `Time`, then `(int)Type`, then `PatientId`. `FEL` wraps `System.Collections.Generic.PriorityQueue<Event, Event>` whose default priority comparer uses `Comparer<Event>.Default` — i.e. our `CompareTo`. ARM-verdict-free, no custom comparer needed.
- **Impact (+):** Deterministic, explainable tie-breaking; no ambiguity in the trace; PriorityQueue supplied by the BCL (nothing to hand-roll).
- **Impact (−):** The enum-value ordering couples event semantics to priority; adding a new event type in M3 must place it in the numeric order of stage execution.
- **Alternatives considered:** FIFO insertion order for ties (rejected — insertion order is implementation-defined and not reproducible across collection resizes); random tie-breaking (rejected — never reproducible for the same seed); a custom `IComparer<Event>` (rejected — redundant, `IComparable<Event>` is the natural home for a total order).

## D-034 UnstableSystemException replaces silent endless growth (FR-VAL-1)

- **Date:** 2026-09-13
- **Decision:** `EngineConfig.Validate()` refuses to run when ρ = λ/(c·μ) ≥ 1 for any stage, throwing `UnstableSystemException` carrying the stage name, λ, c, μ and the exact ρ. The CLI prints the message and exits with code 1.
- **Rationale:** A queue with ρ ≥ 1 never reaches steady state; the analytical M/M/c formulas break down and a simulation's queue lengths would grow without bound, producing misleading metrics. FR-VAL-1 mandates refusal. Carrying the numeric ρ in the exception gives the user (and the viva) an immediate, quantified reason.
- **Implementation details:** `EngineConfig.Rho` = `ArrivalRate / (ServerCount * ServiceRate)`; `Validate()` uses `Rho >= 1.0 - 1e-9` to avoid double-representation flakiness at exactly ρ = 1. `UnstableSystemException` is a subclass of `Exception` with a formatted message. `StabilityTests` cover ρ > 1 and ρ = 1 throwing, and ρ < 1 running.
- **Impact (+):** Fails loud and early (AGENTS §5.0); the message is directly viva-presentable; prevents garbage "results" from unstable runs.
- **Impact (−):** A threshold float-precision guard (`- 1e-9`) must be explained in the viva; users with genuinely unstable data must change inputs (λ down, c up, μ up).
- **Alternatives considered:** Run anyway and let queue length grow (rejected — misleading); seed/cap the queue (rejected — hides instability); warn but run (rejected — FR-VAL-1 explicitly refuses).

## D-035 Deterministic RNG: System.Random wrapper + inverse-CDF exponential

- **Date:** 2026-09-13
- **Decision:** M1 randomness comes from `IRandomSource` (SetSeed/NextDouble) backed by `SeededRandomSource`, a thin wrapper over `System.Random` with default seed 42. `ExponentialSampler` transforms ~U(0,1) to Exp(λ) via the inverse CDF `X = -ln(U)/λ`, clamping U to `double.Epsilon` when the draw is 0.
- **Rationale:** NFR-4 (deterministic given seed) requires the engine to re-seed at run start so the same seed always reproduces the identical event sequence. The inverse-CDF transform is simple enough to defend line-by-line in the viva and needs no third-party dependency for the exponential case. MathNet.Numerics remains the library for the rarer distributions (Normal/Lognormal/Gamma) later (D-003).
- **Implementation details:** `Engine.Run()` calls `_random.SetSeed(_config.Seed)` before the loop. `Sampled` draws log to Debug (FR-VAL-4). The `ln(0)` edge case is clamped to `double.Epsilon` so it can never produce `+∞`. `System.Random` is not thread-safe, but the engine is single-threaded by design (the GUI will run the whole run on a background thread, M5).
- **Impact (+):** Zero-dependency exponential sampling; fully reproducible runs; deterministic unit tests.
- **Impact (−):** `System.Random` is implementation-defined across .NET versions — acceptable because both CI platforms run the same .NET 8 and the seed contract only promises run-to-run equality on a given runtime, not across runtimes.
- **Alternatives considered:** MathNet's `Exponential.Sample` (deferred to the distribution-parameterized stage, D-003/D-026); `System.Random.Shared` (rejected — not seedable per-run); crypto-grade RNG (rejected — overkill, no seed contract).

## D-036 Engine event trace at Serilog Debug level

- **Date:** 2026-09-13
- **Decision:** Every event the engine processes is logged at `Debug` level with clock time, event type, patient id, queue length, server states, and the RNG draws consumed (inter-arrival and service time). The CLI configures three sinks per AGENTS §12.1: console at Information (the metrics table stays readable), rolling file `logs/app-YYYYMMDD.log` at Debug (the full trace, 7-day retention), and `logs/errors-YYYYMMDD.log` at Warning+.
- **Rationale:** FR-VAL-4 requires an event log capturing all state and RNG draws — this is the viva's step-by-step evidence ("here is patient 12's arrival, this draw produced service time X") and the basis for the M4 human-readable trace file. Keeping the console at Information avoids flooding while the file retains the Debug trace.
- **Implementation details:** `Engine` receives a Serilog `ILogger` via constructor injection; `HandleArrival`, `StartService`, `HandleServiceEnd` each emit Debug lines including the draw values. The CLI builds the logger programmatically for M1; the appsettings.json-driven configuration (todo item) remains.
- **Impact (+):** Complete, citable event trace for the viva; log-driven debugging (AGENTS §12.6); no behavior change to the engine when logging is disabled.
- **Impact (−):** Verbose files for long horizons (mitigated by 7-day retention and Debug-only in files); console stays sparser by design.

## D-037 CLI refusal prints a clean line; stack trace stays in file logs

- **Date:** 2026-09-13
- **Decision:** When the CLI refuses to run an unstable configuration (ρ ≥ 1), stderr receives exactly one line beginning `Refusing to run: {message}`. The exception and its stack trace are no longer printed to the console; they are logged `Error` to the file logs (`logs/errors-YYYYMMDD.log` and `logs/app-YYYYMMDD.log`) via a file-only Serilog logger. The `Program` class was converted from top-level statements to a class-based `Program` exposing public `static int Run(args, stdout, stderr, fileLogger)` so `OpdSimulator.Cli.Tests` can assert exit code and console output in-process. Both file writers use `shared: true` to avoid concurrent-writer handle conflicts between the two loggers.
- **Rationale:** A stack trace on stderr made a *deliberate, documented refusal* (FR-VAL-1) look like a crash — misleading in a demo and in the viva. The detail must remain available for debugging (AGENTS §12.5/§12.6) but belongs in the logs, not the terminal. The user-facing template stays `Refusing to run: {ex.Message}`; to give that template ownership of the "Refusing to run:" phrasing, `UnstableSystemException.Message` no longer repeats the prefix (it now begins with `stage '…' is unstable — …`), otherwise the line would read "Refusing to run: Refusing to run: stage …".
- **Implementation details:** `Program.Main` creates the console+file logger and a separate file-only logger, then delegates to `Program.Run`. `Run` catches `UnstableSystemException`, writes the single line to `stderr`, calls `fileLogger.Error(ex, …)`, returns 1. New project `tests/OpdSimulator.Cli.Tests` (xunit) with `CliRefusalTests.UnstableConfig_ExitsOne_WithCleanStderr_AndFileLogKeepsStackTrace`, which in-process runs `--lambda 5 --mu 4 --servers 1 --horizon 1000` and asserts: exit 1; stderr starts `Refusing to run:` and contains neither `at OpdSimulator` nor the exception type name; no metrics on stdout; the detail logger captured exactly one event carrying the `UnstableSystemException` with `at OpdSimulator` in its trace; the console-level logger captured no exception.
- **Impact (+):** The refusal finally looks intentional; the console never gets a stack trace; in-process test coverage of the CLI path; exit 1 and F2 behaviour unchanged.
- **Impact (−):** `Program.Run`'s additional parameters slightly widen the CLI's public surface (accepted for testability); message wording for `UnstableSystemException` changed in a way that only caller is the CLI refusal line, so no other formatting is affected.
- **Alternatives considered:** Catching the exception at the `Main` level and wrapping Serilog output — rejected: `Main` is not cleanly testable in-process. Launching the CLI as a subprocess in tests — rejected: slower and OS-dependent, and it cannot observe the file-detail logger.
- **Verification:** `dotnet run --project src/OpdSimulator.Cli -- --lambda 5 --mu 4 --servers 1 --horizon 1000` → stderr single line, exit 1; `grep -c 'at OpdSimulator.Core.Engine' logs/errors-*.log` ≥ 1 confirms the stack trace survives in the file log. Full solution: 37 tests green, 0 warnings.
## D-038 M2 validation stricter than CONTEXT §5.4 for `departure_stage`

- **Date:** 2026-09-13
- **Decision:** The M2 `verify`/`fit`/`simulate-data` path validates `departure_stage` ∈ {Screening, Doctor} (case-insensitive) strictly, per kickoff rule B1 ("bad departure_stage cell value → reject"). Any other value — including `Reception` — is a per-row rejection issue.
- **Rationale:** Kickoff B1 explicitly enumerates this rule and B3 demands rejection tests; it is the newest owner instruction and trivially testable. CONTEXT §5.4 (verified 2026-09-13) instead said Reception rows warn-and-are-excluded from p_exit. This is a real conflict between two owner instructions; per AGENTS §7 we do not average — we pick the newer, concrete rule for the CLI path and surface the conflict.
- **Implementation details:** `DataValidator.IsValidDepartureStage` accepts exactly Screening/Doctor (OrdinalIgnoreCase). `PExitCalculator` still implements the FR-DATA-6 exclusion logic (Reception counted but excluded from numerator/denominator) so the computation stays correct for any future non-validation-gated path; its unit test exercises the Reception branch directly. In the CLI path Reception rows never reach it (validator rejects the file).
- **Impact (+):** dirty-data handling is unambiguous; matches kickoff tests.
- **Impact (−):** a real clinic file containing Reception rows from the demo data (all exit Screening) is unaffected; if the owner wants warn-and-exclude at the CLI, this decision flips to a check in `ModeValidator`-style soft warning instead.
- **Alternatives considered:** Warn+exclude Reception at the CLI (CONTEXT §5.4) — rejected for now because B1 explicitly says reject; ambiguity flagged to owner.
- **Verification:** `DataValidatorTests.InvalidDepartureStage_IsRejected`; `AllIssuesCollected_NotStoppedAtFirst` (bad stage among 3 issues).

## D-039 TimeParser accepted formats and the bare-number heuristic

- **Date:** 2026-09-13
- **Decision:** `TimeParser.TryParse` accepts 24-hour (`H:mm`, `HH:mm`, with optional `:ss`), 12-hour `h:mm AM/PM` (case-insensitive; lowercase meridiem normalised), and bare numbers. A bare number `< 1` is an Excel fraction of a day (×1440); a number `≥ 1` is already minutes since midnight.
- **Rationale:** Excel (ClosedXML) serialises time cells as day-fractions (0.34375 = 08:15) — confirmed in the loader test — and clinic data may arrive typed as "8:15", "08:15:30" or "8:15 AM". The three formats cover what the demo CSV/Excel realistically contains; the bifurcation `<1` vs `≥1` is unambiguous because a day-fraction is always < 1 and minute-counts are always ≥ 0.
- **Implementation details:** Uses `DateTime.TryParseExact` (invariant culture) rather than `TimeSpan.TryParseExact`, because TimeSpan custom formats have no meridiem specifier and no uppercase hour specifier. Meridiem text is normalised (`prefix + " AM/PM"`) so lowercase "am/pm" parses.
- **Impact (+):** one parser for every file type; round-trips Excel fractions and human text alike.
- **Impact (−):** a bare integer like `60` means 01:00, not 60 seconds — acceptable because column semantics are minutes (documented in USER_MANUAL).
- **Verification:** `TimeParserTests` (theories for 24h, 12h, fraction, bare number, invalid).

## D-040 Chi-square: equal-probability bins, square-root count, df = k − 1 − p

- **Date:** 2026-09-13
- **Decision:** Bins are equal-probability (interior edges at the fitted distribution's quantiles i/k; outer edges at sample min/max). Bin count k = ceil(√n) clamped to [5,20]. Degrees of freedom df = k − 1 − p with p = number of fitted parameters.
- **Rationale:** Equal-probability bins are the textbook approach for continuous GOF; every interior bin is expected to hold n/k observations, which keeps the test unbiased regardless of the fitted shape. k≈√n is the standard rule of thumb (CONTEXT §3.5 predates this choice); clamping keeps tiny samples testable and huge samples printable.
- **Implementation details:** `BinSelector.BinCount`, `BinomialSelector.EqualProbabilityEdges` build edges from the fitted CDF captured as a delegate (see D-042); `ChiSquareTest` computes Σ(O−E)²/E and p = 1 − CDF_χ²(statistic). MathNet `ChiSquared` supplies only the CDF; the test itself is implemented here (D-003 note about nested critical values applies).
- **Impact (+):** deterministic, defensible, matches the viva formula.
- **Impact (−):** outer-edge approximation makes the first/last bin expected counts slightly less than n/k — immaterial in practice (guarded, see D-044).
- **Verification:** `ChiSquareTests.BinCount_FollowsSquareRootRule_ClampedToFiveAndTwenty`.

## D-041 Gamma fitter uses method-of-moments

- **Date:** 2026-09-13
- **Decision:** `GammaFitter` estimates shape = x̄²/Var, rate = x̄/Var (moment matching).
- **Rationale:** Gamma MLE has no closed form (numeric optimisation). The viva can derive MoM in one line from E[X]=shape/rate and Var[X]=shape/rate²; the uniform and normal fitters are closed-form MLE, so MoM is the only approximate family and is clearly documented, per kickoff D ("Gamma MoM").
- **Implementation details:** MathNet `Gamma(shape, rate)` is shape–rate parameterised; parameters stored as `shape`/`rate`.
- **Verification:** `FittingTests.Gamma_MomentEstimatesMatchSampleMoments` (shape/rate ≈ mean, shape/rate² ≈ variance within 5%).

## D-042 FittedDistribution carries CDF/InverseCDF delegates

- **Date:** 2026-09-13
- **Decision:** `FittedDistribution` wraps the MathNet distribution instance plus `Func<double,double>` `Cdf`/`InverseCdf` delegates captured at construction from the concrete distribution type.
- **Rationale:** MathNet's public `IContinuousDistribution` interface exposes densities and sampling but NOT the CDF/quantile function (the class carrying them is internal — confirmed by reflection). The equal-probability binning needs exactly those two functions; capturing them where the concrete type is known keeps `FittedDistribution` dependency-light and its consumers type-agnostic.
- **Implementation details:** Each fitter builds `new Exponential(rate)` etc. and passes `dist.CumulativeDistribution` / `dist.InverseCumulativeDistribution` as method groups. BinSelector/ChiSquareTest consume the delegates, not the MathNet type.
- **Impact (+):** no reliance on an internal MathNet type name; binning logic testable with any fit.
- **Impact (−):** three extra properties in the record — worth it to keep the API honest.
- **Verification:** full FittingTests + ChiSquareTests green.

## D-043 Normal/Lognormal σ use MLE denominator n

- **Date:** 2026-09-13
- **Decision:** `NormalFitter` and `LognormalFitter` divide the squared deviations by n (not n−1).
- **Rationale:** We report MLE parameter estimates; the MLE of σ² divides by n. The chi-square test treats parameters as fixed, so the bias correction is irrelevant to the GOF and mixing estimators would only confuse the viva narrative.
- **Verification:** `FittingTests.Normal_RecoversTrueMoments` / `Lognormal_RecoversMuSigma`.

## D-044 Chi-square guards fail loud (E ≥ 1, df ≥ 1)

- **Date:** 2026-09-13
- **Decision:** `ChiSquareTest.Run` throws `InvalidOperationException` when any expected bin count is < 1 or when df = k−1−p drops below 1.
- **Rationale:** The χ² approximation is unreliable below these thresholds; printing a p-value anyway would silently endorse a garbage test. Per AGENTS §12, fail loud rather than fabricate a number.
- **Implementation details:** k is clamped ≥ 5 and df≥1 normally holds; the guard protects degenerate fitters (e.g. uniform with tiny n) and 2-parameter families at the lowest bin count.
- **Verification:** `ChiSquareTests` green; degenerate paths raise clean exceptions (fit command prints them as errors, not stack traces).

## D-045 Sample CSV tracked alongside the XLSX via .gitignore negation

- **Date:** 2026-09-13
- **Decision:** `.gitignore` now also negates `!samples/sample_patients.csv`, so both the XLSX and its CSV twin are committed.
- **Rationale:** The demo (2026-09-16) may run entirely from a terminal; the CSV is the zero-dependency twin of the XLSX (no ClosedXML needed to inspect the data).
- **Implementation details:** Data files remain otherwise ignored — real patient data must never be committed.
- **Impact:** (+) Easy terminal-side verification and diffing of the sample. (−) An extra committed binary-adjacent file to keep in sync.
- **Alternatives considered:** Keep CSV in `.gitignore` and regenerate on demand — rejected, because the demo machine may be offline (Agents §10.4).
- **Verification:** `git status` shows the CSV tracked after regeneration; `verify --file samples/sample_patients.csv` exits 0.

## D-046 CLI reworked as a subcommand dispatcher in Milestone 2

- **Date:** 2026-09-13
- **Decision:** `Program.Run(args, stdout, stderr, fileLogger)` dispatches on `args[0]`. Commands: `simulate-params` (the renamed M1 command — remains the only rate-driven path), `verify`, `fit`, `simulate-data`, `export`, each in `src/OpdSimulator.Cli/Commands/`.
- **Rationale:** Milestone-1 bare flags would have collided with data-driven entry points. A first argument dispatcher keeps each command's parser tiny and lets the test runner hand a StringWriter/StringReader into every path (public `Run`), which is how the CLI tests drive the whole app headlessly.
- **Implementation details:** `simulate-data` accepts `--servers 1,2,3` and runs one full M1 engine per count; each count is refused independently when ρ ≥ 1 (no stack trace), and the command exits 0 if at least one run completed. Only `exponential` is accepted by `simulate-data` in M2 (clean refusal, exit 2, for other families). `fit` writes a JSON report (`logs/fit-YYYYMMDD-HHMMSS.json`, System.Text.Json) for independent SPSS cross-checking.
- **Impact:** (+) One logical entry point; testable end to end; each command self-documenting with its own `--help`-style usage. (−) A reworked entry surface — the old bare-flag form is gone (documented in USER_MANUAL §3 and DEV_LAUNCH §5).
- **Alternatives considered:** Separate executable per command, and positional `--file`-only forms — both rejected as heavier than a dispatcher with subcommands.
- **Verification:** `CliRefusalTests` updated with the `simulate-params` prefix; `CliDataCommandTests` (5 tests) cover verify exit codes, issue listing, unknown-command exit 2, the server sweep, and the non-exponential refusal. Live runs verified on Ubuntu 24.04.

## D-047 Sample data + dirty fixture are generated by a standalone tool, not hand-written

- **Date:** 2026-09-13
- **Decision:** `scripts/sample-data-generator` (a standalone console app NOT in the sln) writes `samples/sample_patients.{xlsx,csv}` and the dirty fixture `tests/OpdSimulator.Data.Tests/Fixtures/dirty_missing.xlsx`. Driven by `scripts/make-sample-data.sh`. Seeded `Random(42)`; inverse-CDF exponential draws.
- **Rationale:** The sample must be (1) reproducible for the viva ("why is the mean inter-arrival 1.78, not 2.0?" — sampling variation of a documented seed), (2) tied to the generator's intended ρ = 0.75 (inter-arrival Exp(λ=0.5)/min, service Exp(μ=0.666…)/min, c=1), and (3) able to produce a dirty file with exactly one defect per row for the validator tests.
- **Implementation details:** Arrivals start 08:15; `screening_start = arrival + small pre-service delay`; all rows exit at Screening ⇒ p_exit = 1.0. The dirty fixture triggers: missing arrival, bad stage value, `end < start`, empty row, out-of-order arrival — each on its own row.
- **Impact:** (+) Deterministic, self-healing fixtures; the CSV sample is diffable. (−) A third generator to keep in sync with the loader schema (columns) — guarded by FixtureTests, which fail loudly if the fixtures no longer pass validation.
- **Alternatives considered:** Hand-writing the Excel once by scripted commands — rejected: not reproducible for the viva.
- **Verification:** `FixtureTests` (3 tests) green: both samples validate clean, the dirty fixture is rejected with exact row numbers; live `verify`/`fit`/`simulate-data` runs match the committed files.

## D-048 Sample data stores wall-clock times at second precision (HH:MM:SS)

- **Date:** 2026-09-13
- **Decision:** `scripts/sample-data-generator` writes times as `H:mm:ss` (e.g. `8:17:12`), quantised to whole seconds, instead of `H:mm` whole minutes. Chosen over decimal minutes (e.g. `8.25`).
- **Rationale:** The M2 smoke test showed chi-square rejecting a genuinely exponential sample at p ≈ 0 for BOTH inter-arrival and service times. The test is calibrated correctly — the minute-rounded storage collapsed the continuous exponential to ties, so the empirical distribution looked discrete. Quantising at 1/60 min keeps the distortion (~0.008 min) far below the chi-square bin width (≈13 min at k = 8), which the divergence from p ≈ 0 to p ≈ 0.10/0.26 confirms. HH:MM:SS wins over decimal minutes because the wall-clock strings remain human-readable in the viva and match a plausible real clinic log format; decimal minutes are opaque to field staff.
- **Implementation details:** The RNG stream is untouched — seed 42 draws the same exponential gaps/ service times as before; only the string format changes. TimeParser already accepted `H:mm:ss`, so no parser change was needed; a new `TimeParserTests` case (`8:17:30` → 497.5) pins the `H:mm:ss` path alongside the existing `HH:mm` cases (no HH:MM regression).
- **Impact:** (+) chi-square now honestly accepts the fitted exponential (p 0.103 / 0.258); (+) demo output is sample-consistent with the fitted means. (−) Simulated ρ at c=1 nudges 0.81 → 0.82 because μ̂ now uses untruncated service times (1.45 → 1.464 min).
- **Alternatives considered:** (a) HH:MM:SS — chosen, see rationale. (b) Decimal minutes — rejected: unreadable and unlike clinical logs.
- **Verification:** `fit --file samples/sample_patients.xlsx --distribution exponential` → p = 0.103 (inter-arrival) and p = 0.258 (service), both Fail-to-reject at α = 0.05. Full suite 98 tests green.

## D-049 Engine generalised to run a NetworkTopology; the single-stage path delegates

- **Date:** 2026-09-13
- **Decision:** `Engine` now runs any ordered `NetworkTopology` via `Run(NetworkTopology, seed, horizon)`. The classic single-stage `Run()` simply builds a one-stage topology (`NetworkTopology.CreateSingleStage`) and delegates. New Core types: `StageSpec` (immutable config), `Stage` (runtime: queue + servers + derived λᵢ), `NetworkTopology` (λ₀, ordered stages, one probabilistic exit stage, product-rule effective rates, all-stage ρ validation), and `StageMetrics` (per-stage ρᵢ, utilisation, wait, queue, throughput) attached to `SimulationResult`.
- **Rationale:** Realises D-006 (N-stage generic) exactly as the kickoff demanded — "extend, don't rewrite": `Run()` now returns the identical byte-for-byte M1 numbers through the same loop code, with the per-stage state generalised from a lone `Server[]`+`Queue` to a `Stage[]`. A separate `NetworkEngine` class was rejected because the two loops would drift apart and the M1 regression would silently rot.
- **Implementation details:** Routing uses the existing `EventType` mapping (stage `i` completes via event type `i+1`), so no new event types were added. Completion routes downstream except at the exit stage, where a uniform draw against `p_exit` decides continuation (FR-SIM-3). Effective rates follow the product rule λᵢ = λ₀·Π_{j<i}(1−exitProb_j) (D-007). `Patient` gained `SystemArrivalTime` (whole-journey clock) and `AdvanceToStage` (per-visit re-anchor that resets stage service milestones). Each run materialises fresh `Stage[]` runtime so queue/server state never leaks between seeds.
- **Impact:** (+) Multi-stage clinic network is now a config change; (+) per-stage metrics and ρᵢ exist for FR-STAT-6/FR-VAL-1. (−) `SimulationResult` aggregate `AverageQueueLength`/`StageUtilisation` are means over stages — whole-network values deliberately stay stage-explicit in `StageMetrics`.
- **Alternatives considered:** (a) Generic network engine from the start (rejected — rewrites M1). (b) Bite the D-017 idle-pick first (see D-050).
- **Verification:** `EngineTests.Run_SingleStage_ByteForByteRegression` + new CLI test lock served = 29892 / wait = 0.724 / ρ = 0.75; 3-stage `Run_ClinicNetwork_*` tests assert derivation λ_doctor = λ₀·(1−p_exit) and routing fractions; 114 tests green, 0 warnings.

## D-050 Server assignment: Milestone-1 lowest-ID bias fixed to random-among-idle

- **Date:** 2026-09-13
- **Decision:** Idle-server selection is now an injectable `IServerSelectionPolicy`. Production default `RandomIdleSelection` picks uniformly among idle servers using one `NextDouble` when ≥2 are idle (no draw when exactly one is idle, keeping the single-server M1 stream byte-for-byte); `LowestIdSelection` reproduces the old M1 behaviour for one negative regression test.
- **Rationale:** D-017 documented random-among-idle, but the Milestone-1 engine actually used `FirstOrDefault(!IsBusy)` — the lowest-ID server. That is a latent bug: the lowest-index server is always the first choice, so in low-load regimes (both servers idle) it grabs nearly all arrivals. The FR-STAT-7 imbalance flag (max−min utilisation > 0.15) is only meaningful once the assignment policy is fair.
- **Implementation details:** `Stage.Queue` + `Server[]` per stage; the engine calls the policy only when `HasIdleServer` (always true at the call sites). The negative test uses a low-load symmetric 2-server stage (λ = 2, μ = 4, c = 2, ρ = 0.25) where the bias is strongest: lowest-ID yields |util₀−util₁| ≥ 0.10 while random stays below.
- **Impact:** (+) Fair per-server metrics; (−) Multi-server M/M/c runs now consume extra RNG draws, so the M2 sample sweep numbers for c=2,3 (ρ 0.41/0.27 waits) will be refreshed during M3 verification (G). Single-server numbers are unchanged.
- **Alternatives considered:** (a) Round-robin — rejected: periodic assignment correlates arrivals to servers and is less defensible orally than "random among idle". (b) Lowest-ID "as-is" — rejected: breaks FR-STAT-7 intent.
- **Verification:** `EngineTests.Run_NetworkSymmetricServers2_RandomSelection_BalancesUtilisation` (diff < 0.10) and `..._LowestIdPolicy_RecreatesImbalance` (diff ≥ 0.10) both green on seed 42.

## D-051 Clinic calendar model: block-relative days, gated Poisson stream, per-open-day operating time

- **Date:** 2026-09-13
- **Decision:** `ClinicCalendar` (Core/Calendar) models the schedule as: t = 0 anchored at the day-0 arrival window; a "day block" is 24 h from that anchor, whose weekday is `(startDay + blockIndex) mod 7`; admissions are allowed only in the first `OpenDurationMinutes` (default 165 = 08:15→11:00) of an open day's block (open Mon–Thu + Sat per CONTEXT §1.1). Arrivals are one continuous Poisson stream for `generatorDays` blocks; the gate admits only arrivals landing inside an open-day window and under the optional daily cap (resets per block). Operating time (D-018) is summed per open-day block as first admitted arrival → last service end, so overnight gaps never dilute utilisation.
- **Rationale:** (1) Anchoring blocks at the window start keeps every simulation time ≥ 0 and avoids the pre-8:15 negative-time problem of a midnight anchor. (2) A single Poisson stream with fire-time gating is the simplest viva-defensible model: "the clinic's potential demand is a Poisson process; the calendar is the admission gate" — no per-day draw resets, no special-casing the first arrival after a weekend, and closed days are just blocks where the gate always refuses. (3) D-018's per-day denominator must be summed per open day in a multi-day run, or weekend/night gaps would falsely halve utilisation.
- **Implementation details:** New `Engine.Run(NetworkTopology, ClinicCalendar, generatorDays, seed, dailyCap)` + a private nested `CalendarGate` holding admittedPerDay, per-day first-arrival/last-service-end, the daily cap counter and StopTime = `(generatorDays−1)·1440 + openDuration`. The existing horizon `Run` delegates to the same `RunCore` with `calendar == null`, so the byte-for-byte M1 path (served 29892, wait 0.724, ρ 0.75) executes identical statements and stays guarded by its two regression tests. `SimulationResult` gained `AdmittedPerDay`, `GeneratorDays`, `DailyCap`. `ClinicCalendar.FormatClock` renders wall-clock strings for the viva/UI (C4). Closed blocks simply admit nothing; services already in progress drain past 11:00 (FR-SIM-6), which the drain test forces with ρ = 0.5, μ = 0.1.
- **Impact:** (+) Multi-day, calendar-accurate runs; (−) Calendar runs are a separate engine entry point from the M1 path (intentional — isolation keeps the byte-for-byte guard meaningful).
- **Alternatives considered:** (a) Per-day Poisson reset (fresh first arrival each morning) — rejected: less defensible phrasing and more edge cases. (b) Midnight-anchored day boundaries with a pre-window dead zone — rejected: negative times for t < 08:15 complicate gating. (c) Synthetic WindowOpen FEL events to skip idle nights — rejected: unnecessary, the continuous stream already moves the clock through them.
- **Verification:** `ClinicCalendarTests` (defaults, window-boundary theory, Friday/Sunday closure, `--start-day` weekday shift, FormatClock, ctor guards) and 7 `EngineTests.Run_Calendar_*` facts (single-day admit+drain, 7-day Fri/Sun zero admissions, start-day Friday first block closed, cap binds 4/day × 5 open days, same-seed reproducibility, drain past 165, arg guards). 139 tests green, 0 warnings; byte-for-byte regressions unchanged.

## D-052 Stage-aware `simulate-data`: clinic-flow ordering, blank-downstream-stage rule, per-stage server-count contract

- **Date:** 2026-09-13
- **Decision:** `simulate-data` fits **each detected stage** with its own μᵢ and orders the stages by the canonical clinic flow via `ClinicStageOrder` (Reception → Screening → Doctor, Data/Preprocess), regardless of CSV column order. Single-stage files keep the M2 `--servers 1,2,3` sweep semantics **byte-for-byte** (same fitted line, same `PrintMetrics`). Stage-aware files require exactly **one `--servers` count per detected stage in flow order** (count mismatch = usage error exit 2) and run a single network through `Engine.Run(topology, …)`. `DataValidator` acquires one relaxation: a stage cell may be blank **only when that stage comes strictly after the row's `departure_stage`** in the clinic flow (a Screening exit has no doctor times). p_exit is estimated and bound to Screening **only when Doctor exists**; otherwise the exit stage index is −1 and everyone leaves at the last stage.
- **Rationale:** (1) Clinic knowledge belongs in the Data layer, never the engine (AGENTS §4); the engine consumes an already-ordered `NetworkTopology`, so simulated order always matches the real flow and `simulate-data` vs `simulate-network` stay consistent. (2) Single-stage byte-for-byte preservation protects the M2 demo path and the M/M/c sweep's validation value (one command = M/M/c table). (3) Blank downstream cells are real data, not dirt: forcing fabricated doctor times would corrupt μ_doctor. (4) p_exit only matters when a stage follows Screening, mirroring the routing law λ_doctor = λ₀·(1 − p_exit) of D-015; with no Doctor, "exit probability" is meaningless. (5) The relaxation is directional and strict in both directions: earlier-stage blanks and arrival blanks are still rejected, so D-038's strictness survives where it matters.
- **Implementation details:** `ClinicStageOrder.Flow` is the single source of the clinic flow; `ClinicStageOrder.FlowIndex(stage)` returns the flow position (unknown stage → int.MaxValue). `DataValidator.ValidateStageCell` checks `StageMayBeBlankFor(column, row)`: the column's stage name (split off `_start`/`_end`) must have flow index strictly greater than the row's `departure_stage`. `SimulateDataCommand` builds `StageSpec[]` from flow-ordered μᵢ and server counts, computes p_exit via `PExitCalculator` when Doctor is present, and prints results with the new `Program.PrintNetworkMetrics` (per-stage block: served, avg wait, avg queue, stage util, per-server util, throughput, ρᵢ = λᵢ/(c·μ); network totals block). Unstable stage-aware runs refuse with exit 1 and the single message listing ALL unstable stages.
- **Impact:** (+) Realistic M3 data validates and simulates without fabrication; (−) `--servers` now surprises M2 users who pass 3 counts on a 3-stage file — mitigated by the explicit flow-order usage wording and the count-mismatch exit-2 error naming the detected stages.
- **Alternatives considered:** (a) Keep the M2 alphabetical pair order — rejected: μ would be burned onto the wrong stage whenever columns aren't written in flow order. (b) Allow any blank cell — rejected: silently weakens D-038 everywhere, including columns before `departure_stage`. (c) Warn-and-exclude rows with blank downstream cells — rejected: excludes statistically valid rows and changes p_exit denominators.
- **Verification:** 2 new `DataValidatorTests` facts (blank doctor cells accepted for Screening exit; blank reception cell for Doctor exit still rejected) + 3 new `CliSimulateDataNetworkTests` facts (3-stage file → exit 0, `p_exit = 0.7`, 3 per-stage metric blocks + network totals, and `Fitted from data: λ = 0.2`; unstable 3-stage file → exit 1 listing Reception ρ = 6.00 and Screening ρ = 4.00; `--servers 1,2` on a 3-stage file → exit 2 usage error). Full suite 144 tests green (76 Core + 58 Data + 10 Cli), 0 warnings; live CLI smoke: 3-stage run serving 109 patients with per-stage blocks and network totals.
