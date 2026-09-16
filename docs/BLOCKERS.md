# BLOCKERS.md — Active Blockers

Anything preventing progress, with owner and needed action. A task in
`TODO.md` marked `[?] BLOCKED` must have an entry here.

## Active

*(none — current resolutions on file)*

## Resolved

- **B-008:** Phase 5c.3 (event trace in ClinicDay/MultiDay run modes) conflicts with the frozen-Core rule.
  - Resolution: owner chose **(a)** — allow the minimal Core change. 2026-09-16.
  - Outcome: optional `ITraceSink? traceSink = null` added to the calendar `Engine.Run` overload and forwarded to `RunCore` (D-110). Byte-compatibility proven: Core suite 85 green before AND after. `SimulationCoordinator` now collects a trace in every run mode; `TraceViewer_PopulatesAfterClinicDayRun` green.
  - Linked: `docs/TODO.md` Phase 5c row (5c.3 DONE); DECISIONS.md D-110.

- **B-001:** Confirm daily patient cap with clinic management.
  - Owner: Taha
  - Impact: Affects default config in UI (maps to PRD OQ-1)
  - Status: Pending

- **B-002:** Confirm 8:15 AM arrival start with real data.
  - Owner: Taha
  - Impact: t=0 anchor (maps to PRD OQ-2)
  - Status: Pending

- **B-003:** Confirm "first distribution" = inter-arrival interpretation with professor.
  - Owner: Taha
  - Impact: UI labeling (maps to PRD OQ-3)
  - Status: Pending

- **B-005:** Avalonia MVVM template not installed — **RESOLVED** 2026-09-14 (hand-build, D-078). [Kept out of Active; see Resolved]

- **B-007:** M5 GUI requires a keyboard-only acceptance run to find bugs and defects.
  - Owner: Taha
  - Impact: The M6 kickoff defers the M5 keyboard pass ("owner will run manually"). AGENTS §16.8's pre-commit UI checklist (Tab through every control, Shift+Tab reversal, Exit/Escape/Enter contracts, focus restore, disabled-field reasons, invalid-submit flow) can only be exercised on the **running app with the mouse unplugged** — it cannot be emulated headlessly. Until this pass is done, the M5 UI quality claim and the related TODO rows stay open/blocked.
  - Needed action: Run the AGENTS §16.8 keyboard checklist in the running app (launch per DEV_LAUNCH §5) and the §16.7 welcome-card keyboard dismissal; report defects in `BLOCKERS.md`/`TODO.md` so they can be fixed.
  - Linked tasks: TODO rows "UI acceptance of the 8 controls at M5-D screens" and "Tab order audit across entire window" (both marked `[?] BLOCKED`).
  - Target: Before the final demo (planned 2026-09-16).
  - Status: Blocked on owner running the manual pass

- **B-006:** Verify dead-state build on Windows.
  - Owner: Taha
  - Impact: Cross-platform claim (Linux dev / Windows deploy, PRD NFR-5) cannot be certified until a real Windows dead-state build + run is confirmed (AGENTS §10.4). Gates Milestone 5 (UI).
  - Needed action: Make a Windows machine available, or rule that CI `windows-latest` green suffices as the Windows verification.
  - Target: Before Milestone 5 (UI)
  - Status: Blocked until a Windows machine is available

## Resolved

- **B-005:** Avalonia MVVM template not installed (`dotnet new install Avalonia.Templates` not run).
  - **Resolution:** 2026-09-14 — owner approved hand-building the Avalonia project (M5 kickoff adjustment 3). The App shell (Program.cs, App.axaml/.cs, ViewLocator, ViewModels, Views, Logging/CrashReporter, app.manifest) was authored directly (D-078) with no template installation or network dependency; `dotnet build` 0 warnings and the window launches on Ubuntu.
  - Status: **Resolved via hand-build**

- **B-004:** Sample patient data file (`samples/sample_patients.xlsx`) not yet received.
  - **Resolution:** 2026-09-13 — since no real file was provided, a **stand-in** sample was generated deterministically (seed 42) by `scripts/sample-data-generator` (D-047): 60 rows, single Screening stage, inter-arrival Exp(λ=0.5), service Exp(μ=0.666…), all `departure_stage = "Screening"` ⇒ p_exit = 1.0 (matches CONTEXT §5.5). FixtureTests now lock these files to the loader/validator, so substituting the real spreadsheet later is a drop-in replacement that the tests will guard.
  - Status: **Resolved** (real clinic data is still welcome and remains substitutable via the same file path).