# Definition of Done — OPD Clinic Queue Simulator

This is the viva checklist. Every item is verified with a
specific test, screenshot, or manual step. As of the end of
Phase 8D, tick off each item truthfully. Unchecked items are
known limitations, not hidden gaps.

## Solution & build
- [ ] Solution builds with `dotnet build -c Release` (0/0)
- [ ] Full test suite passes
- [ ] Core has no UI dependencies (verified by `dotnet list
       package` on Core only)

## Data pipeline
- [ ] Excel import works (test + manual)
- [ ] CSV fallback works
- [ ] Validation reports row-level issues
- [ ] Preview handles 10k rows
- [ ] Inter-arrival times computed correctly
- [ ] Service/waiting times computed correctly

## Statistical pipeline
- [ ] Distribution fitting (MLE) works
- [ ] Chi-square on INPUT data works
- [ ] Histogram bins match chi-square bins
- [ ] Manual λ override works
- [ ] Fitted λ stays available for comparison

## Simulation engine
- [ ] N-stage engine works
- [ ] Per-stage server count configurable
- [ ] Random idle-server assignment works
- [ ] p_exit routing works
- [ ] Calendar: arrival window 08:15–11:00
- [ ] Calendar: Fri/Sun skipped
- [ ] Daily cap works
- [ ] Services continue after 11:00 until drained
- [ ] Stability guard refuses ρ ≥ 1
- [ ] Per-server utilisation reported
- [ ] Utilisation asserted in [0, 1]
- [ ] Imbalance flag (max−min > 0.15) works
- [ ] Event trace works
- [ ] Same seed → identical results

## Configuration paths
- [ ] Fit-from-data path works end-to-end
- [ ] Enter-manually path works end-to-end
- [ ] Both produce identical SimulationParameters given
      equivalent values
- [ ] Time unit selector (min / sec / hour)
- [ ] Rate-wise / Mean-wise toggle
- [ ] Per-stage model notation (M/M/1, M/M/2, …)
- [ ] Per-stage distribution dropdowns (advanced mode)
- [ ] Time-span presets (15min / 1hr / 1day / 1week / 1month /
      custom)

## Verification & validation
- [ ] Chi-square on SIMULATION OUTPUT works
- [ ] Histogram of simulated samples works
- [ ] Analytical M/M/c comparison appears when applicable
- [ ] Analytical comparison is guarded to steady-state runs
      (horizon ≥ 100,000 minutes)
- [ ] Simulated vs analytical within 5% on stable
      exponential configs
- [ ] Chi-square on INPUT data (for fit-from-data path)
- [ ] INPUT-side and OUTPUT-side chi-square are visually
      distinct

## UI
- [ ] Results panel is scrollable
- [ ] Results panel is grouped (Overview / Stage / Server /
      Statistical / Simulation Verification / Analytical /
      Charts / Trace)
- [ ] Config panel is scrollable
- [ ] Config panel has Collapse All / Expand All
- [ ] Welcome card shows on first launch
- [ ] Welcome card hides on Start Calculation
- [ ] Clear All resets to fresh-launch state
- [ ] Widget selector (Customise results) works — 8 widgets
- [ ] Input tab merges upload + preview + fit analysis
- [ ] Preset system works (save/load/import/export)
- [ ] Help tab works (F1 opens, search filters)

## Accessibility
- [ ] Keyboard-only walkthrough passes (§16.8)
- [ ] Every input has a persistent label + placeholder
- [ ] Every error has red + icon + text (never colour alone)
- [ ] Contrast ratios ≥ 4.5:1 (WCAG AA)

## Cross-platform
- [ ] Windows build works
- [ ] Linux build works
- [ ] Presets stored under ApplicationData on both
- [ ] Dead-state instructions in DEV_LAUNCH.md verified

## Documentation
- [ ] README points to DEV_LAUNCH, USER_MANUAL, DEEP_DIVE
- [ ] DEEP_DIVE covers every source file
- [ ] DECISIONS log is complete
- [ ] VIVA_ANSWERS covers all chart types
