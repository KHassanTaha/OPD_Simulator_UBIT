# BLOCKERS.md — Active Blockers

Anything preventing progress, with owner and needed action. A task in
`TODO.md` marked `[?] BLOCKED` must have an entry here.

## Active

## Resolved

- **B-011:** a Gamma or Uniform stage that could not resolve a mean either built a NaN-mean spec or threw `ArgumentException` out of the coordinator. **Ruling 2026-09-26; fixed and verified.**
  - **The blocker text was wrong about the cause.** It reported the comma list as the broken source. Probing the live panel showed the comma-list rows already passed a concrete rate into `BuildSpec`, so the spec was complete and correct. The real defect was the neighbouring branch: a stage **covered by the loaded file** has `rate` deliberately set to `null` (so the coordinator keeps 7C.5's fitted-rate precedence), and that same `null` was being passed to `BuildSpec` as the spec's mean — leaving `Scale = NaN` and `Min`/`Max = NaN` for a stage whose mean the fit already knew.
  - **Fix (D-159):** `rate` and `specRate` are now separate values. `rate` is what the **coordinator** resolves against and stays `null` for a file-covered stage; `specRate` is what the **spec is built from** and carries the fitted rate. The coordinator's μ chain and the D-147 invariant are untouched.
  - **Case 2 — clean refusal:** when no source yields a **finite positive** μ, a Gamma or Uniform stage now sets an `InlineError` naming the stage, the three places to enter μ, and the family, and `TryBuildRunParameters` returns `null`. The refusal surfaces at the field instead of as a Core `ArgumentException` about a spread field the user never touched.
  - **Scope is deliberate.** The refusal fires for **Gamma and Uniform only** — the two families that store the mean a second time. Exponential, Deterministic, Normal and Lognormal need no mean at build time, so a blank μ for them still means "fit me at run time" (D-104/7C.5), which is the Path A workflow §19.1 requires. Refusing every family broke exactly that: 9 tests.
  - **The gate needed no change.** `CollectFitModeGaps` already resolves through `EffectiveMu` (fitted → comma list → row). It is now **pinned by tests** rather than edited, so it cannot drift from `TryBuildRunParameters`.
  - **"Finite positive" was load-bearing, and rate-wise mode proved it.** `double.TryParse("1e400")` returns `true` with value `+∞`. In mean-wise mode the inversion turns that into 0 and the plain `> 0` test already refuses; in **rate-wise** mode `+∞` passed straight through and built a Gamma with **mean 0, `Scale` 0 and no error at all** — a silently wrong simulation. The `double.IsFinite` guard closes it, and the test uses rate-wise mode because mean-wise would pass whether or not the guard exists.
  - **Verified:** 9 new tests — the 6 named in the ruling, plus a fitted-branch test, a Start-gate block naming the stage, a Uniform blank-μ comma-list run asserting every draw lands inside the derived bounds, and the non-finite case. Mutation-checked three ways: `specRate = null` fails the fitted-branch test; removing the refusal fails `BuildSpec_Gamma_NoMuAnywhere_RefusesCleanly`; dropping `IsFinite` fails `BuildSpec_Gamma_NonFiniteMuAnywhere_RefusesCleanly`. All reverted. 554/554 green in Release and Debug, 0 errors / 0 warnings.
  - **Linked:** `docs/DECISIONS.md` D-159; `docs/TODO.md` Phase 8K row.

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