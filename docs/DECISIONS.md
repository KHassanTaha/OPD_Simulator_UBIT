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
- **Decision:** `.gitignore` negates `appsettings.template.json` (`!appsettings.template.json`) so the config template is committed, while `appsettings.*.json` environment/secret variants stay ignored (per-env files still ignored; only the base and the template are tracked).
- **Rationale:** DEV_LAUNCH §3 instructs fresh clones to `cp appsettings.template.json appsettings.json`. Under the un-negated owner template, `appsettings.*.json` matched the template too, so a fresh clone would lack it and the bootstrapping step would fail (dead-state rule, AGENTS §10.4). Deviance from the exact owner block is deliberate and minimal.
- **Implementation details:** `.gitignore` "Configuration" section: `appsettings.*.json`, then `!appsettings.json`, then `!appsettings.template.json`. Verified with `git status`: template shows as untracked/committable, per-env variants stay ignored.
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