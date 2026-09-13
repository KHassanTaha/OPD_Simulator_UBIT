# PROGRESS.md — Narrative Progress Log

> Session handoffs (AGENTS §13) and resume lines (AGENTS §14.2) are stored here newest-first at the top.

### Session Handoff — 2026-09-13 07:06
Branch: main (no commits yet — everything untracked)
Status: In-Progress (scaffold done; awaiting owner approval to commit bootstrap)

Done
Repo cleanup: removed Python-era content (per owner go-ahead)

Set up solution structure (Core / Data / App / CLI / Tests) — all six projects in OpdSimulator.sln, template cruft removed

Add NuGet packages via NuGet, pinned (D-026) — MathNet.Numerics 5.0.0; ClosedXML 0.105.1; CsvHelper 33.1.0; Avalonia 11.3.3 family; CommunityToolkit.Mvvm 8.4.2; LiveCharts 2.0.5; Serilog 4.4.0 + sinks

Create appsettings.template.json + document copy step in docs/DEV_LAUNCH §3

Create samples/.gitkeep + docs/DEV_LAUNCH B-004 note; VIVA_ANSWERS.md stub

Dead-state verification (restore → build → test → run Cli) green on Ubuntu 24.04, 0 warnings

Update docs/DEV_LAUNCH.md: Last verified 2026-09-13; §5 rewritten (CLI until M5); §6 test note; §8 layout; §12 changelog

Create BLOCKERS.md B-005 (Avalonia template not installed)

In Progress
None (all scaffold items complete; next task from TODO is M1: Implement Event, Queue, Server, Patient classes)

What is complete:
Full scaffold: solution, 6 projects, pinned packages, config template, samples/.gitkeep, viva stub, B-005 logged, docs/DEV_LAUNCH updated and verified once; dead-state re-verified twice.

What remains:
Avalonia 12.x upgrade decision (reviewed change, needs SDK ≥ 9); M1 engine; owner commits bootstrap.

Next Session Should Start With
Implement Event, Queue, Server, Patient classes (M1)
Then: FEL + generic N-stage DES engine

Blocked
B-001..B-004 (owner-pending, none blocking scaffold)
B-005 (Avalonia template not installed → App is classlib placeholder until M5)

Git State
Owner approved the root commit directly on `main` (D-028) — committed at the end of this session; details in the final summary.

Build & Test
dotnet build: PASS (0 warnings, 0 errors, Debug)
dotnet test: PASS (exit 0 — no tests yet; M1 adds them)
Warnings: 0 (CS9057 resolved by pinning Avalonia 11.3.3)

Files Touched
src/: OpdSimulator.Core/.Data/.App/.Cli + tests/.../.Tests (all added; Class1.cs/UnitTest1.cs removed)
global.json, .gitignore: unchanged (verified)
scripts/: unchanged (verified run.sh +x, run.ps1)
appsettings.template.json: added
VIVA_ANSWERS.md: added (stub)
samples/.gitkeep: added
docs/BLOCKERS.md: B-005 added
docs/DECISIONS.md: D-026 added
docs/TODO.md: scaffold items → [x], Avalonia init → [?] B-005
docs/PROGRESS.md: scaffold lines + CURRENT STATE line + this handoff
docs/DEV_LAUNCH.md: Last verified, §3, §5, §6, §8, §12 updated

Decisions Made
D-026 — NuGet package versions pinned (incl. Avalonia 11.3.3 over 12.1.2 due to CS9057 on .NET 8 SDK)
D-027 — `appsettings.template.json` ship-in-repo (`.gitignore` negation so the §3 copy step works on a fresh clone)

Assumptions Added/Changed
None new tagged in CONTEXT.md (no new assumptions; B-005 is a blocker, not an assumption)

Notes for Next Session
`dotnet test` currently prints "No test is available" and still exits 0 — expected at scaffold, real tests arrive M1.
docs/DEV_LAUNCH §5 says run Cli ("Hello, World!") until the real Avalonia app exists (M5).
Avalonia 12.x upgrade is a reviewed change (CS9057 with .NET 8 SDK 4.8 compiler).

> **CURRENT STATE (2026-09-13):** scaffold complete, verified from a dead state (Ubuntu 24.04, 0 warnings), and **pushed to `origin/main` as the root commit `73787bb`** (exact message "chore: initial project scaffold", D-028 root-commit exception). 6 projects on net8.0; Avalonia pinned 11.3.3 (CS9057 fix, D-026). No simulation logic written yet. Next: Milestone 1 engine (Event/Queue/Server/Patient + FEL + M/M/1 validation). Blocked: B-004 (sample data), B-005 (Avalonia template → App is a placeholder until M5).

Initial commit pushed: 73787bb at 2026-09-13 07:30

## 2026-09-13 — Scaffold session

- **Solution scaffolded (D-026):** `OpdSimulator.sln` with 6 projects — Core (classlib, MathNet.Numerics 5.0.0), Data (classlib, ClosedXML 0.105.1 + CsvHelper 33.1.0), App (classlib placeholder, no Avalonia template installed — BLOCKERS B-005; references Avalonia 11.3.3 family + CommunityToolkit.Mvvm 8.4.2 + LiveCharts 2.0.5 + Serilog suite), Cli (console, Serilog 4.4.0 + Sinks.Console 6.1.1), Core.Tests + Data.Tests (xUnit, template defaults). All net8.0, zero warnings. Template placeholder files (`Class1.cs`, `UnitTest1.cs`) deleted so the scaffold carries no dead code; the test projects are intentionally empty until M1.
- **Avalonia pinned to 11.3.3 (CS9057 fix):** the initially-resolved 12.1.2 bundles analyzers requiring Roslyn 4.14 vs the .NET 8 SDK's 4.8 → 2 build warnings (CS9057), violating zero-warning policy. Pinned to 11.3.3 (builds clean; satisfies LiveCharts 2.0.5's `Avalonia ≥ 11.0`), documented as a reviewed-change gate for any future 12.x upgrade.
- **Config + samples + viva stubs:** `appsettings.template.json` (Serilog console + rolling file 7-day; `Simulation` RandomSeed 42, DefaultServers Reception 1 / Screening 2 / Doctor 3) with the copy-to-`appsettings.json` step documented in docs/DEV_LAUNCH §3; `samples/.gitkeep` + docs/DEV_LAUNCH sample-file note (B-004); `VIVA_ANSWERS.md` stub created. `.gitignore` negated the template (`!appsettings.template.json`, D-027) so it ships in the bootstrap commit instead of being swallowed by `appsettings.*.json`.
- **Dead-state verification PASSED (Ubuntu 24.04, SDK 8.0.131):** `rm -rf bin/obj` → `dotnet restore` → `dotnet build -c Debug` (0 warnings, 0 errors) → `dotnet test` (exit 0; no tests yet) → `dotnet run --project src/OpdSimulator.Cli` ("Hello, World!", exit 0). docs/DEV_LAUNCH updated: Last verified 2026-09-13, §5 rewritten for CLI-until-M5, §6 test note, §8 layout, §12 changelog.
- **Root commit approved (D-028):** owner approved committing the bootstrap directly on `main` (root-commit exception to §11.2 — no upstream base exists yet). `Simulator Start Prompt.txt` moved to `docs/agent-prompts/kickoff-bootstrap.md` to archive the session-start prompt; AGENTS §11.2 exception now references D-028. NOC docs (`docs/NOC_Data_Collection_SIPMR.{md,docx}`, `docs/make_noc.py`) deleted to complete the Python-era cleanup scope.

## 2026-09-13 — Scaffold session (Start)

- **Repo cleanup executed (2026-09-13):** Python-era content removed per owner go-ahead (`src/ku_modeling_hospital/`, `tests/` Python files, `data/`, `artifacts/`, `notebooks/`, `pyproject.toml`, `uv.lock`, `.python-version`, `project-ideas.*`, `.venv/`, pytest/ruff caches). `Simulator Start Prompt.txt` kept. TODO items + Done section updated.
- **Existing scaffold verified (2026-09-13):** `global.json` (8.0.100 / latestFeature, D-013), owner-template `.gitignore` (D-020), `scripts/run.sh` (+x) and `scripts/run.ps1` all present; no re-creation needed.

## 2026-09-13 — Bootstrap & documentation set

- Superseded the deprecated Python-era project (ku-modeling-hospital ML repo) with the **OPD Clinic Queue Simulator** (C#/.NET 8, discrete-event simulation). All old content is marked for deletion (see TODO).
- **Docs created in `docs/`:** `PRD.md` (v1.0.0 → owner clarifications logged as v1.0.1 in §10 Change History), `CONTEXT.md` (with `[VERIFIED — owner clarification, 2026-09-13]` tags in §2.3, §5.4, §5.5), `DECISIONS.md` (D-001…D-011), `TODO.md`, `docs/DEV_LAUNCH.md`, `docs/USER_MANUAL.md`, `PROGRESS.md` (this file), `BLOCKERS.md`.
- **Tech stack locked in (D-001…D-005):** C# / .NET 8 LTS, Avalonia UI (MVVM), MathNet.Numerics, ClosedXML + CsvHelper, xUnit.
- **Owner-scope clarifications applied (D-006…D-011):** `departure_stage = Reception` → warn + excluded from `p_exit`; run mode = structure, horizon = span; ρ guard uses effective per-stage arrival rate (λ·(1−p_exit) at Doctor); DES engine N-stage generic from day one (validated vs M/M/1 and M/M/2); demo 2026-09-16 scoped to load → fit → chi-square → simulate via the CLI path.
- **Root files:** `global.json` (pins SDK 8.0.100, `rollForward: latestFeature` — D-013), `README.md` (link paths corrected to `docs/`), `AGENTS.md` relocated from `docs/` to repo root (D-012).
- **Decisions added to DECISIONS.md (D-012…D-014):** AGENTS.md at repo root / two launch docs in `docs/`; global.json resolution policy; wait for owner's real sample data (no synthetic file — B-004).
- **Blocker opened:** B-004 — sample patient data file (`samples/sample_patients.xlsx`) not yet provided by owner; gates M2 demo path and loader integration tests.
- **Next (per TODO):** repo cleanup of deprecated Python content, `docs/REQUIREMENTS.md`, then solution scaffold + Milestone 1 engine.
- **Per-stage ρ model (D-015, PRD v1.1.0):** FR-VAL-1 rewritten as the per-stage stability check (ρᵢ = λᵢ/(cᵢ·μᵢ), λᵢ derived from λ₀ + routing; refusal reports ALL unstable stages with λᵢ, cᵢ, μᵢ, ρᵢ); new FR-STAT-6 (per-stage ρ display); CONTEXT §2.3 extended with the per-stage ρ / routing explanation; assumption 11 reworded; §10 Change History + TODO updated.
- **Per-server utilisation model (D-016…D-018, PRD v1.2.0):** FR-DATA-10 (optional server-ID columns → historical per-server validation, never rejects files); FR-STAT-7 (engine always reports per-server utilisation + stage-level mean; imbalance flagged when max−min > 0.15 on BOTH sources; assignment random among idle). Operating time defined as first-arrival → last-service-end per day (keeps util ≤ 1 under overtime). CONTEXT §5.7 added incl. [UNVERIFIED] blank-server-cell assumption. TODO updated with 5 implementation items + Done log entry.
- **Charts feature (D-019, PRD v1.3.0):** FR-UI-4 (LiveCharts2 charts: histogram + fitted PDF, chi-square bars, per-server utilisation bars; P2 queue-over-time + waiting-time histogram), FR-STAT-8 (fit-histogram bin count = chi-square bin count), NFR-6 (< 500 ms / 10k points, downsampling, non-blocking). New CONTEXT §6 Visual Output Analysis; SPSS, Validation, Glossary, References renumbered §7–§10. Charts assigned to M5; M1 stats + M2 fitting items annotated to expose binned/PDF/series data. TODO + Done log updated.
- **Version-control protocol adopted (AGENTS §11, D-020):** trunk-based short-lived branches, Conventional Commits, `dotnet build && dotnet test` gate, review-merge to `main`, CI on ubuntu+windows once the solution exists. Local `master` renamed to `main`; `.gitignore` replaced with the owner's template (build/IDE/test/NuGet/publish/OS/data/secrets rules; `samples/*.xlsx`+`*.csv` ignored except `sample_patients.xlsx`). Bootstrap commit queued on `chore/bootstrap` (not committed — awaiting owner go-ahead).
- **Logging & error monitoring policy (AGENTS §12, D-021):** Serilog console + rolling `logs/app-YYYYMMDD.log` (7-day) + `logs/errors-YYYYMMDD.log`; committed base `appsettings.json` (`!appsettings.json` gitignore negation); global handlers (AppDomain, UnobservedTaskException, Avalonia Dispatcher) → `logs/crash-YYYYMMDD.log` + dialog; log-level map incl. crash format (viva evidence). docs/DEV_LAUNCH §9.1 added with Linux/Windows log-tail commands; troubleshooting rows for crash/no-logs added. Serilog packages + config + handlers queued in TODO.
- **Session resume protocol (AGENTS §14, docs/DEV_LAUNCH §11, D-023):** cold-start reconciliation on "RESUME SESSION" (fixed file-read order, git/log inspection, drift detection, 6-line state summary, wait for "go"; reality wins, no guessing, never discard others' uncommitted work; `## Resume — ts — reconciled: N findings` line in PROGRESS.md). docs/DEV_LAUNCH gained §11 Resuming Work (clean/abrupt end, resume prompt, "what broke" note); Changelog renumbered to §12. PROGRESS header note extended to cover resume lines.
- **Traceability discipline (AGENTS §9.1 + §9.7, D-024):** REQUIREMENTS.md redefined as a derived PRD traceability matrix (status → source file → test → decision ID) with the PRD source-of-truth rule; §9.7 sets the update discipline ([~] on start, [x]+Source+Test on completion, Decision on design impact, re-ID in-session, ≤ 1 session drift). TODO creation item reworded to the matrix framing.
- **REQUIREMENTS.md created (2026-09-13):** derived traceability matrix synced to PRD v1.3.0 — 46 requirements (UI 4, Data 10, SIM 10, STAT/VAL 13, TOKEN 3, NFR 6), all `[ ]`, 0% coverage; FR-SIM-11 placeholder dropped (no such PRD requirement); header/net-count/changelog corrected from the draft (v1.0.0 → v1.3.0, 41 → 46). TODO gained the "Requirements not DONE until Source+Test row updated" gate and a verify-traceability script task.
- **Bidirectional rule (AGENTS §9.7, D-025):** §9.7 now forbids orphan/placeholder/"to-be-decided"/speculative rows — the matrix may only contain PRD requirements. FR-SIM-11 drop formalized (already executed at creation, approved earlier); REQUIREMENTS.md audited against PRD v1.3.0 (zero orphans); Changelog row added. "Last synced" unchanged (already today, v1.3.0).
- **Config template tracked via negation (D-027):** `.gitignore` now un-ignores `appsettings.template.json` (`!appsettings.template.json` after `appsettings.*.json`); docs/DEV_LAUNCH §3 copy step moved to the top of First-Time Restore ("Before building, copy the config template") with the `# Linux` / `# Windows` commands and the Serilog/defaults explanation folded in; bottom duplicate block removed; §12 changelog row added. REQUIREMENTS.md NFR-5 Source filled (`.gitignore, DEV_LAUNCH.md §3, appsettings.template.json`) + Decision `D-027`.
- **Single canonical location for docs instructions (AGENTS §10.7, D-030):** new §10.7 — every docs/DEV_LAUNCH.md / docs/USER_MANUAL.md instruction has exactly one canonical location; duplicates consolidated into cross-references; grep the key phrase before marking any docs task `[x] DONE`. Verification: `grep -c "cp appsettings.template.json appsettings.json" docs/DEV_LAUNCH.md` → **1** (the step exists once). `grep -c "appsettings.template.json" docs/DEV_LAUNCH.md` → **5** (2 in the §3 step command pair + 1 in the §3 Configuration-file paragraph + 1 §8 layout reference + 1 §12 changelog historical row — all legitimate references, not duplicate steps).
- **Root commit exception (AGENTS §11.2, D-028):** §11.2 now states the repository's first commit goes **directly on `main`** (root commit convention); all subsequent changes use feature branches. Supersedes the earlier `chore/bootstrap` branch plan noted in D-020. TODO bootstrap item updated to direct-on-`main`.
- **Blocker B-006 opened:** Windows dead-state verification (dead-state build + run per AGENTS §10.4) blocked until a Windows machine is available; target before M5 (UI). TODO "Verify dead-state launch on Linux + Windows" → `[?] BLOCKED` (Linux half already verified 2026-09-13 on Ubuntu 24.04).