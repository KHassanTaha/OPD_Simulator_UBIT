# BLOCKERS.md — Active Blockers

Anything preventing progress, with owner and needed action. A task in
`TODO.md` marked `[?] BLOCKED` must have an entry here.

## Active

- **B-011:** a Gamma or Uniform stage whose row μ is BLANK but whose μ is supplied by the comma list still **throws** `ArgumentException` out of the coordinator instead of returning a clean refusal. Found 2026-09-26 while verifying B-010. **Owner ruling needed — NOT fixed.**
  - **Not a regression.** Probed against the pre-fix coordinator: that path threw for **all three** families (`StdDev > 0`, `Shape > 0`, `Min < Max`). The B-010 fix narrowed it to Gamma and Uniform only, so it strictly improved the blast radius. It was already broken and stays broken for these two.
  - **Mechanism:** `with { Mean = ... }` replaces only `Mean`. But Gamma's `Scale` and Uniform's `Min`/`Max` are **mean-derived stored fields** — `BuildSpec` computes `Scale = mean / shape` and `Min/Max = mean ∓ w` — so substituting the mean leaves them stale. `StdDev` is mean-*independent*, which is why Normal is fine.
  - **Two distinct sub-cases, and only one is fixable by re-deriving:**
    1. *Old mean valid, new mean differs.* Re-deriving would work: `Scale = newMean / Shape` (Shape is stored), and for Uniform recover the half-width from the old bounds (`w = (Max − Min) / 2`, then `Min/Max = newMean ∓ w`). This is the same rule `BuildSpec` already applies, so it is not a new design — it is what D-147's claim that "the sampler's mean agrees (Gamma draws from k·Scale = k·(Mean/k) = Mean)" already requires.
    2. *Old mean is NaN* (row μ blank). Here `w` and the spread are **undefined**, because both are only meaningful relative to a mean. No re-derivation can help: `w` is NaN. This must become a clean **refusal** on the row, exactly as `BuildSpec` already refuses a missing σ/k/w input — not a Core exception.
  - **Why it needs a ruling:** both sub-cases change what the engine samples or what the user is told, and sub-case 2 additionally touches `BuildSpec`/`StageRow`, which were out of scope for B-010.
  - **Recommended:** fix (1) as a one-line extension of the B-010 change, and treat (2) as a `BuildSpec` refusal ("Gamma needs a mean service time before its half-width/shape can be applied") plus closing the comma-list-vs-blank-row-μ hole in the D-128 Start gate.
  - **Impact if left:** a user who leaves a Gamma or Uniform row's μ blank while filling the comma list gets an unhandled `ArgumentException` instead of a field-level message. Normal, Lognormal, Exponential and Deterministic are unaffected.

## Resolved

- **B-010:** `SimulationCoordinator` dropped every stage's configured spread, so **no non-Exponential stage could actually run**.
  - **Ruling 2026-09-26:** owner approved the one-line change. **Fixed and verified.**
  - **Fix:** `SimulationCoordinator.BuildStageSpecs` now carries the configured spec through whole — `parameters.ServiceFamilies[i] with { Mean = 1.0 / mu }` (`SimulationCoordinator.cs:255`) — instead of rebuilding it from `.Family` alone. The existing μ resolution chain is UNCHANGED (`ServiceRates[i] ?? ManualServiceRates[i] ?? FittedRateFor`), so manual-mode and FitFromData precedence and the D-147 invariant (`ServiceRate == 1/Mean == μ`, same source) both still hold.
  - **Verified:** four new family tests observe the **sampler's output** (sample sd vs σ, bounds vs Min/Max, variance vs mean²/k, positivity + centring) rather than the spec object, because the spec is precisely what the bug discarded. Plus the end-to-end M/M/1 + M/D/2 + M/G/3 run.
  - **Mutation:** reverting the fix fails 5 tests — the four family tests and `Render_MixedFamilyVerification`. Reverted, `git diff` clean, 544/544 green.
  - **Mixed frame regenerated for real:** `phase-8k-verification-mixed.png` now comes from a run with three genuinely different families (Exponential, Deterministic, Gamma), evidencing both card treatments — a real chi-square curve for M/M/1 and M/G/3, and the "not applicable" note for M/D/2. The frame's limitations paragraph is gone because the limitation is.
  - **Linked:** `docs/DECISIONS.md` D-158; `docs/TODO.md` Phase 8K row.

- **B-009:** Phase 7C docs — `AGENTS.md` §19 "Two-Path Configuration Contract" required the owner's exact text.
  - **Resolved 2026-09-18:** §18 restored (never previously present; UI completion discipline now codified). §19 added with text reconciled to code. §15 and the 14→16 / 17→19 numbering gaps documented at end of `AGENTS.md`.
  - Linked: `docs/TODO.md` Phase 7C row; `docs/DECISIONS.md` D-128.

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