# PROGRESS.md — Narrative Progress Log

> Session handoffs (AGENTS §13) and resume lines (AGENTS §14.2) are stored here newest-first at the top.

### Session Handoff — 2026-09-16 09:05
Branch: `docs/deep-dive-ch1-6`
Status: Clean (committed, pushed, uncommitted = none)
Done
Project deep-dive Session B — chapters 7–9 of `docs/PROJECT_DEEP_DIVE.md` (TODO deep-dive row updated: Session B component done): ch. 7 Core deep walk (30 files, all sections with file:line citations — Engine DES loop, EventType stage-mapping, FEL tie-break D-033, NetworkTopology ρ/effective-rate D-007/D-015, ClinicCalendar day model D-051, trace sink feature D-055..D-058), ch. 8 Data deep walk (30 files — validator rules incl. the D-008/D-038 Reception-anomaly split, TimeParser formats D-039, fitting pipeline D-040..D-044 incl. MoM Gamma D-041 and n-denominator Normal/Lognormal D-043), ch. 9 CLI deep walk (9 files — subcommand dispatcher, exit codes 0/1/2, D-037 clean refusal, trace command as M4 viva artifact). Verified against all 30 Core + 30 Data + 9 Cli source files (glob-confirmed; earlier "31/31" counts corrected to 30/30), decision IDs cross-checked against DECISIONS.md entries.

In Progress
Project deep-dive — Sessions C–E (ch. 10–18 + PDF) await owner "go".

What is complete: chapters 7–9 written with file:line citations; Document Status row B updated to "written 2026-09-16"; TODO deep-dive row updated; all claims verified against read source files in this session. NOTE: earlier summary claimed Core=31/Data=31 files; glob on 2026-09-16 Session B confirmed Core=30, Data=30, Cli=9 — corrected in chapters and this handoff (reality wins per AGENTS §14.2).

What remains: Sessions C–E (ch. 10–11 App/Tests, ch. 12–15, ch. 16–18 + PDF), each gated by owner "go"; PDF renderer = wkhtmltopdf (pandoc/xelatex/weasyprint all absent; no sudo-permitted installs). DEV_LAUNCH.md:177 "272 tests pass" vs §6 "278" reconciliation still deferred to a docs pass (do NOT edit without owner OK — DEV_LAUNCH has a 6c.3 row already).

Next Session Should Start With
Session C — chapters 10–11 (App, Tests) per the task brief; commit "docs: deep dive, chapters 10-11", push, wait for "go".

Blocked
None.

Git State
Commits made this session: (pending commit of docs/PROJECT_DEEP_DIVE.md + TODO + PROGRESS, listed in report)
Pushed to origin: pending owner "go" check — feature branch `docs/deep-dive-ch1-6` will be pushed by the agent at owner go-gate.

Uncommitted changes: docs/PROJECT_DEEP_DIVE.md (modified), docs/TODO.md (modified), docs/PROGRESS.md (modified)

Build & Test
dotnet build: PASS (0 warnings/errors — verified in 6c.3 session gate, unchanged this docs-only session)
dotnet test: PASS — 278 green (Core 85 / Data 58 / Cli 35 / App 100) per 6c.3 gate result (DEV_LAUNCH:6)

Warnings: 0

Files Touched
docs/PROJECT_DEEP_DIVE.md: modified (ch. 7–9 added before Incremental Update Rule; Document Status row B)
docs/TODO.md: modified (deep-dive row → Session B done)
docs/PROGRESS.md: modified (this handoff)

Decisions Made
None (docs-only session; no new decision required).

Assumptions Added/Changed
None.

Notes for Next Session
Session C reads all App + Tests source files (30 folders) before writing ch. 10–11. DEV_LAUNCH.md:177 "272" reconciliation still pending owner review.

### Session Handoff — 2026-09-16 08:20
Branch: `docs/deep-dive-ch1-6`
Status: Clean (committed, pushed, uncommitted = none)
Done
Project deep-dive Session A — chapters 1–6 of `docs/PROJECT_DEEP_DIVE.md` (TODO deep-dive row → IN PROGRESS, session A component done): cover page (phases 1–6c.3), executive summary, system architecture (dependency graph, why Core is UI-free), repository map, package inventory (all pinned versions + "why this one" with alternatives), build/run/test. Verified against csproj projections, `OpdSimulator.sln`, `global.json`, DEV_LAUNCH.last-verified, D-001..D-005/D-013/D-019/D-021/D-026/D-082/D-086/D-116.

In Progress
Project deep-dive — Sessions B–E (ch. 7–18 + PDF) await owner "go".

What is complete: chapters 1–6 written with file:line citations throughout; NOTE — `docs/DEV_LAUNCH.md:177` still says "272 tests pass" in one heading place but the §6 catalogue and §1 Last-verified say 278; the chapter-6 table cites 278 per the Last-verified line (DEV_LAUNCH:6) and TODO 6c.3 gate. Flagging rather than silently editing DEV_LAUNCH (docs-only session; owner merge of 6c branch pending).

What remains: Sessions B–E (ch. 7 Core/Data/Cli, ch. 10–11 App/Tests, ch. 12–15, ch. 16–18 + PDF), each gated by owner "go"; PDF renderer = wkhtmltopdf (pandoc/xelatex/weasyprint all absent; no sudo-permitted installs).

Next Session Should Start With
Session B — chapters 7–9 (Core, Data, Cli) per the task brief; commit "docs: deep dive, chapters 7–9", push, wait for "go".

Blocked
None.

Git State
Commits made this session: (pending commit of docs/PROJECT_DEEP_DIVE.md + TODO + PROGRESS, listed in report)
Pushed to origin: pending owner "go" check — feature branch `docs/deep-dive-ch1-6` will be pushed by the agent at owner go-gate.

Uncommitted changes: docs/PROJECT_DEEP_DIVE.md (new), docs/TODO.md, docs/PROGRESS.md

Build & Test
dotnet build: PASS (0 warnings/errors, verified this session before docs work — 6c.3 gate)
dotnet test: PASS — 278 green (Core 85 / Data 58 / Cli 35 / App 100) per 6c.3 gate result (DEV_LAUNCH:6)

Warnings: 0

Files Touched
docs/PROJECT_DEEP_DIVE.md: added (ch. 1–6)
docs/TODO.md: modified (deep-dive row → IN PROGRESS)
docs/PROGRESS.md: modified (this handoff)

Decisions Made
None (docs-only session; no decision recorded).

Assumptions Added/Changed
None.

Notes for Next Session
DEV_LAUNCH.md line 177 says "272 tests pass" but §6 catalogue + §1 say 278 — reconcile on a docs pass, do NOT edit in Session B without owner OK (DEV_LAUNCH has a 6c.3 row already).

### Session Handoff — 2026-09-16 07:35
Branch: `feat/milestone-6c-input-analysis-charts`
Status: Clean (committed, uncommitted = none)
Done
6c.3 — Chi-square observed vs expected bars (TODO 6c.3 → [x]; commit + push this session, hash in Git State)

In Progress
None

What is complete: 6c.3 fully built, verified, committed, and pushed. Grouped
side-by-side columns use the LiveCharts default (two plain `ColumnSeries`
sharing coords; `StackedColumnSeries` deliberately avoided) — confirmed against
the official 2.0.5 docs. `IInputChartData` + dispatch lets the same ChartCard
slot host either chart kind. Full suite 278 green (App 100, +6:
`Phase6c3ChiSquareTests` 5 content facts + `Phase6c3Screenshot`). Real launch
18 s alive, crash logs unchanged. Docs synced (D-120, TODO, DEV_LAUNCH
§1/§6/§12, USER_MANUAL §4/§12, REQUIREMENTS FR-UI-4).

What remains: owner review of the 6c.3 branch, then "go" for 6c.4 (per-server
utilisation bars + imbalance flag).

Blocked
None

Git State
Commits made this session: `207187d` (6c.2, prior gate) + the 6c.3 commit (hash reported in chat at wrap time)
Pushed to origin: Yes
Uncommitted changes: None

Build & Test
dotnet build: PASS (0 warnings / 0 errors)
dotnet test: PASS — 278 passed, 0 failed
Warnings: 0

Files Touched
src/OpdSimulator.App/Services/InputAnalysisService.cs: added `IInputChartData` + `ChiSquareChartData` + `BuildChiSquareChart`
src/OpdSimulator.App/Services/ChartControlBuilder.cs: extracted shared `CreateChart`, added `BuildChiSquareChart`
src/OpdSimulator.App/ViewModels/InputAnalysisViewModel.cs: `ApplyPrepared` dispatches on `IInputChartData`; `Prepare` emits histogram + chi-square per fit
tests/OpdSimulator.App.Tests/Phase6c2HistogramTests.cs: expected cards 2 → 4 (helper + wiring + layout asserts)
tests/OpdSimulator.App.Tests/Phase6c2Screenshot.cs: expected 4 cards
tests/OpdSimulator.App.Tests/Phase6c3ChiSquareTests.cs: added (5 facts)
tests/OpdSimulator.App.Tests/Phase6c3Screenshot.cs: added (evidence PNG)
docs/DECISIONS.md (D-120), docs/TODO.md (+6c.3), docs/PROGRESS.md, docs/DEV_LAUNCH.md, docs/USER_MANUAL.md, docs/REQUIREMENTS.md

Decisions Made
D-120 — chi-square cards reuse the verdict's own bins + two side-by-side `ColumnSeries` (DECISIONS.md)

Assumptions Added/Changed
None (the owner's gate note "272 + 5 = 277" miscounted: the per-phase convention
counts the screenshot-evidence test, so 6 content tests landed → 278. Flagged
in TODO 6c.3 + this handoff; not a blocker.)

Notes for Next Session
6c.4 spec is in InputAnalysisService/BuildChiSquareChart's sibling files region
of TODO.md (per-server utilisation bar chart, imbalance > 0.15 amber `BrushWarning`,
tooltip "Above average by {delta:.1%}", screenshot phase-6c4-utilisation.png).
Await owner "go".

### Resume — 2026-09-16 07:20 — reconciled: 0 findings (clean 6c.2 working tree on `feat/milestone-6c-input-analysis-charts`, HEAD `ec83231`, all 6c.2 src/tests/docs present uncommitted)

### Phase 6c.3 — 2026-09-16 07:25 — chi-square observed-vs-expected bars (`feat/milestone-6c-input-analysis-charts`)

Owner said "go" after the 6c.2 push (`207187d`). Implementation:

1. **Docs-first API confirm (AGENTS rule: don't invent):** the LiveCharts2
   2.0.5 docs confirm grouping is *default* — multiple `ColumnSeries` sharing a
   coordinate render side-by-side; stacking needs `StackedColumnSeries`; the
   `Padding` property tunes in-series spacing and `IgnoresBarPosition` is only
   for background columns. So observed-vs-expected is literally two
   `ColumnSeries` — no undocumented API.
2. **Service:** `InputAnalysisService.BuildChiSquareChart(FitReport)` returns
   `ChiSquareChartData` (Title "Chi-square: {label}", Caption, HasSeries,
   Categories, Observed, Expected). Categories = "bin 1"… "bin k" derived from
   `ChiSquareResult.BinEdges.Count − 1`; Observed/Expected ARE the verdict's
   arrays (cast only), never recomputed (D-118 principle preserved); Caption =
   `"χ² = {stat:0.###}, df = {df}, p = {p:0.###} — {Decision}"` invariant —
   identical wording/format to the ResultsPanel row. Null chi-square → empty
   card (HasSeries false).
3. **Builder:** `ChartControlBuilder` — extracted a shared private `CreateChart`
   shell (categorical X labels / frequency Y axis / theme-resolved paints /
   legend-top / tooltips) so histogram and chi-square builders differ only in
   their series lists. `BuildChiSquareChart` = two `ColumnSeries` (Observed =
   BrushChartSeries1 green, Expected = BrushChartSeries2 teal, MaxBarWidth 22
   each) grouped side-by-side by the default.
4. **One slot, two shapes:** both records implement the tiny `IInputChartData`
   interface (Title/Caption/HasSeries); `InputAnalysisViewModel.ApplyPrepared`
   dispatches histogram vs chi-square to the matching builder and both fill the
   same `ChartCard` slot. `Prepare` emits per fit: histogram card then
   chi-square card (only when the fit produced a chi-square).
5. **Tests (6 new, broadened existing):** `Phase6c3ChiSquareTests` — same bin
   count for observed/expected/categories; observed sum == sample size; caption
   format matches the results-table wording (contains χ²=stat, df =, p =, and
   the Decision string); categories equal bin index labels from BinEdges;
   null-fit → empty card. `Phase6c3Screenshot` → `phase-6c3-chi-square.png`.
   6c.2 tests updated: 2 → 4 cards everywhere (helper, layout assert, wiring
   await, screenshot count).
6. **Gate:** build 0/0; **278 green** (Core 85 / Data 58 / Cli 35 / App 100);
   real launch 18 s alive "Main window created.", crash logs unchanged (2);
   evidence PNG 38.9 KB (byte-identical to the *regenerated* 6c.2 PNG because
   both screenshot tests now render the exact same 4-card window — both show
   the chi-square cards; acceptable, noted in DEV_LAUNCH §6). Count: the owner
   gate line said "272 + 5 = 277" but 6 tests landed (5 content + 1 screenshot),
   matching the per-phase convention — flagged.
7. **Docs:** D-120; TODO 6c.3 → [x]; DEV_LAUNCH §1 (278), §6 (App 100 + 6c.3
   bullet), §12 changelog row; USER_MANUAL §4 (two cards per series) + §12;
   REQUIREMENTS FR-UI-4 description updated (D-120); PROGRESS this entry.

Lesson: the two 6c.2/6c.3 screenshots are byte-identical by construction now —
do not chase "newer PNG looks same" as a defect; the window content is
identical because 6c.2's evidence test also renders the chi-square cards (both
tests share the 4-card MainWindow). Visual pixel-verification is unavailable in
headless; rely on the assertions (4 chart controls, all non-null content) +
LiveCharts' own rendering path.

### Phase 6c.2 — 2026-09-16 07:05 — Input Analysis histograms + fitted PDF overlay (`feat/milestone-6c-input-analysis-charts`)

Exploration confirmed the contracts 6c.2 builds on: `FitsService.Fit` returns
`FitReport(Label, Samples, Fitted, ChiSquare)`; `ChiSquareResult` carries
`Statistic/df/PValue/Alpha/RejectFit/Observed(/* int */)/Expected/BinEdges`
(equal-probability bins, Eᵢ ≥ 1 guard — widths vary per bin); `FittedDistribution`
exposes `Name/Parameters/ParametersText()/Distribution(Density)/SampleSize`;
`SimulationCoordinator.BuildFits` labels "Inter-arrival" + "<stage> service"
with the distribution label passed straight to `FitsService` (no label→family
mapping exists); sample CSV is a single Screening stage; M6 branch precedent
(`feat/milestone-6-charts-and-token`) proved the `ColumnSeries`/`LineSeries`/
`SolidColorPaint` construction; a LiveCharts probe showed the `LegendPosition`/
`TooltipPosition` enums live in `LiveChartsCore.Measure`.

Implemented:
- `Services/InputAnalysisService.cs` — pure numbers: `FitAll` mirrors BuildFits;
  `BuildHistogram` copies `chi.BinEdges`/`chi.Observed` verbatim (6c.2 owner
  spec "bins from the chi-square result, never recomputed"), PDF per bin =
  `Density(midpoint) × width × N`, categories `[low, high)`, caption
  `Family (params) — χ²(df) = stat, p = p — verdict`; failed fit → seriesless
  empty card (title kept, EmptyState "Fit unavailable for this series.").
- `Services/ChartControlBuilder.cs` — builds the `CartesianChart` on the UI
  thread (G5): observed `ColumnSeries` (BrushChartSeries1), fitted
  `LineSeries` (BrushChartSeries2), axes/grid/legend/tooltip paints resolved
  from ChartTheme brushes with hex fallbacks matching those theme colours
  (headless).
- `ViewModels/InputAnalysisChartViewModel.cs` — Title / Caption / HasSeries /
  ShowEmptyState / ChartContent (bound to `ChartCard`).
- `InputAnalysisViewModel` — `Charts` ObservableCollection,
  `ApplyAsync` (Task.Run prep + Dispatcher.UIThread.Post + generation guard,
  stale applies dropped with a Serilog warning) / `Apply` (sync, deterministic
  tests) / `ApplyPrepared`.
- `ConfigPanelViewModel` — new `DataBindingChanged` event raised at the end of
  `ApplyLoadedFile` and `ResetToDefaults` (internal `RaiseDataBindingChanged`).
- `MainViewModel` — subscribes to the event + `Config.PropertyChanged`
  (distribution dropdowns) + `Config.SignificanceLevel.PropertyChanged` (α
  feeds the captions) → `InputAnalysis.ApplyAsync(binding, iad, svd, α)`.
- `Views/InputAnalysisView.axaml` — ItemsControl of `ChartCard`s bound to
  `Charts` (show-empty via `!HasSeries`), card margin 0,0,0,16.

Gate evidence: Release build 0 warnings/0 errors; **suite 272 green** (Core 85 /
Data 58 / Cli 35 / App 94 — 261 baseline + 11 new: 6 pure-service, 2 Avalonia
VM, 1 config-event, 1 async MainViewModel wiring, 1 screenshot); real Linux
launch 18 s alive "Main window created." with crash logs unchanged (0 new);
evidence `logs/screenshots/phase-6c2-histograms.png` (57 KB — Inter-arrival +
Screening cards). Lesson: cannot visually confirm the PNG via the model (no
image input) — size vs the 27 KB empty-tab frame plus the in-test `Series`
assertions back the render. Docs: DECISIONS **D-118** (bin reuse + PDF scale)
+ **D-119** (refresh wiring + generation guard); TODO 6c.2 → [x];
DEV_LAUNCH §1/§6/§12 + USER_MANUAL §4/§12 + changelogs updated. 6c.3 await:
chi-square O/E bars (verdict text mirrors ResultsPanel rows).

### Phase 6c.1 — 2026-09-16 06:45 — chart infrastructure + Input Analysis scaffold (`feat/milestone-6c-input-analysis-charts`)

Pre-flight (owner-approved) established: pin **LiveChartsCore.SkiaSharpView.Avalonia
2.0.5** (latest stable, deps Avalonia ≥ 11.0 ≥ 11.0.0 — matches the parked M6
branch pair with 11.3.3; D-116); SkiaSharp native deps already present, no apt
change; charts consume `DataBindingResult.InterArrivalMinutes` +
`ServiceMinutesByStage` through the public `FitsService.Fit` (chi-square bins
never recomputed — used straight from `ChiSquareResult`); per-stage
`StageMetrics.WaitingTimeSamples` + `QueueLengthSeries` already exist in frozen
Core, so the proposed "minimal Core addition" is **permanently dropped** per
owner instruction — any genuine 6c.5 gap gets reported, not patched
speculatively. Owner added two accessible colour tokens (≥ 3:1 vs white panel,
logged with rationale).

Shipped in 6c.1:
- `Theme.axaml`: `ColorChartSeries3` #6B2FBA (violet ≈ 7.7:1) +
  `ColorChartSeries4` #E54600 (vermillion ≈ 4.0:1) — hex lives only here.
- `Assets/ChartTheme.axaml` (new): brush-only chart dictionary
  (BrushChartSeries1..4, grid, axis text/ticks, legend, tooltip brushes);
  merged into App.axaml after Theme/Motion (D-117).
- `Controls/ChartCard.axaml(.cs)` (new): reusable card — Title / Caption /
  EmptyStateText / ShowEmptyState(default true) / ChartContent; empty-state
  Border vs ChartHost ContentPresenter toggled by ApplyEmptyState.
- `ViewModels/InputAnalysisViewModel.cs` (new): `IsEmpty` → "Load a data file
  to see fit analysis.", `HasData` inverse raised via OnIsEmptyChanged.
- `Views/InputAnalysisView.axaml(.cs)` (new): empty-state + HasData
  ScrollViewer reserved for 6c.2+ cards. MainWindow Input Analysis tab now
  hosts it (placeholder removed); `MainViewModel.InputAnalysis` property added.
- Tests (`Phase6c1InputAnalysisTests`, 4) + `Phase6c1Screenshot` (evidence):
  ChartCard title+caption, empty-state visible by default, hidden when
  ShowEmptyState=false, InputAnalysisView empty message, empty-state PNG.
- Build/test lesson (D-117): `Control.IsVisible` is **local**, not effective —
  a hidden ContentPresenter prunes its content; assertions target the named
  containers (`EmptyStateBorder`/`ChartHost`), never child TextBlocks. Stale
  App.Tests dll caused one false failure — rebuild the solution, not just the
  App project. Test scratch probe (`ScratchProbe.cs`) deleted.

GATE (all passed): Release build 0 warnings/0 errors; full suite **261 green**
(Core 85 / Data 58 / Cli 35 / App 83 — 256 baseline + 4 spec'd tests + 1
screenshot-evidence test, matching the per-phase convention that counts the
screenshot test); real Linux launch 18 s alive with "Main window created."
and crash logs unchanged (0 new entries); evidence
`logs/screenshots/phase-6c1-empty.png` (27 KB). Docs: DECISIONS D-116 (package
pin) + D-117 (palette/theme contract); DEV_LAUNCH §1 Last-verified, §3 package
note, §6 counts + 6c.1 bullet, §12 changelog row; USER_MANUAL §4 tab list +
hint text, §12 changelog row; TODO 6c.1 → [x], 6c.2..6c.6 rows retained.

### Phase 5d — 2026-09-16 06:10 — config panel refinements (μ relocation, α selector, stage-count mismatch warning)

- **5d.1 (D-112):** Stages rows are now **topology only** — the editable per-row
  "Service rate μ" field is removed; each row shows a read-only source label
  (`μ = 0.50 (from data)` / `μ = 0.50 (manual)` / `μ = — (no source)`) maintained
  by `RefreshStageSourceLabels()` across load, Parameter-list change, mode
  toggle, Parameters toggle and stage resize. The single manual entry point is
  the Parameters comma list. `SimulationCoordinator.BuildStageSpecs` returns a
  `(Specs, MissingStageName)` tuple and the run is refused with
  `String.Format(MissingServiceRateMessage, …)` naming the stage. `RecomputeRho`
  now uses the identical λ/μ sources so the ρ preview and the run cannot
  disagree. 5d.1's old message snippet is superseded by the canonical 5d.3
  wording (logged in D-114).
- **5d.2 (D-113):** Model gains "Significance level (α)" (default 0.05), strict
  (0,1) blur validation with exact error strings and Start blocking;
  `SignificanceLevelForRun` threads through `SimulationCoordinator.Run` →
  `BuildFits` → `FitsService.Fit(…, alpha)` → `ChiSquareTest.Run` (Data API
  unchanged — it already took α). Results caption now dynamic via
  `ResultsPanelViewModel.SetChiSquareAlpha`, restored on Clear All.
- **5d.3 (D-114):** loading data whose detected stage count differs from the
  configured list raises an **amber warning** (theme `BrushWarning*` tokens,
  D-115) in the Data section with exact counts + names and two actions:
  **Sync stages from data** (themed confirm → `SyncStagesToData()` resizes,
  adopts names, refreshes labels, clears warning) and **Keep current stages**
  (non-destructive dismiss). 5d.1-vs-5d.3 message conflict resolved to the
  "NEW" wording as the single canonical banner.
- **5d.4 (D-115):** ErrorBanner gained `BannerSeverity` (Error/Warning/Info)
  with theme-resource colours and a `BrushOf` fallback keeping the build at 0
  warnings; automation name `"{Severity}: {Message}"`.
- **Tests (17 new, Phase5dConfigTests + Phase5dScreenshot):** default/nosource
  labels + Start enabled; fitted label from data; manual label only while
  Parameters on; missing-μ refusal names the stage ("Stage 'Doctor' has no
  service rate…" via a temp 2-stage CSV whose p_exit = 0.5); SignificanceLevel
  default/zero/one/non-numeric; α flows into chi-square verdicts (120-sample
  guaranteed-degenerate-free series asserts a real verdict at 0.01 and
  0.05); coordinator threads α through the data batch; caption reflects α and
  resets; mismatch N<M, N>M (temp 2-stage CSV vs StageCount 1); Sync resizes +
  names + clears + refreshes labels; Sync command asks for confirmation
  without resizing; Keep dismisses. Screenshots host the real ConfigPanel:
  `logs/screenshots/phase-5d-config.png` (sample loaded vs 3 stages → amber
  warning + α field) and `phase-5d-cleared.png` (after Clear All → default
  no-source labels, warning gone).
- **Gate:** Release build 0 errors/0 warnings; **256 green** (Core 85 / Data
  58 / Cli 35 / App 78); real launch 15 s alive `timeout 15 dotnet run
  --project src/OpdSimulator.App --no-build -c Release` → exit 124, "Application
  started. Main window created." at 05:53:05 in `logs/app-20260916.log`, crash
  log mtime unchanged (02:49). Docs synced: DECISIONS D-112..D-115, DEV_LAUNCH
  §6 (256) + changelog row, USER_MANUAL (Service rate / Significance level /
  new α + μ steps 4–8 / refuse paragraph), CONTEXT D-100 assumption updated to
  VERIFIED/superseded by D-112, TODO phase-5d [x], D-106 polish row marked
  fixed.

### Phase 5c.4 — 2026-09-16 05:36 — Clear All performs a full reset to the fresh-launch state

- Owner-reported bug: Clear All reset the fields but left the previous run's
  metrics, chi-square, trace and banner on the results panel with the welcome
  card hidden — the opposite of "return to the clear screen".
- `MainViewModel.ResetAll()` (invoked by ConfigPanel.axaml.cs after the
  themed confirmation; fallback to config-only `ResetToDefaults()` when no
  MainWindow hosts the panel) = `Config.ResetToDefaults()` (already unloads
  the file: `Binding=null`, `LoadedFileName=null`, "No file loaded") + the new
  `ResultsPanelViewModel.Reset()` (welcome card back, `HasRun=false`,
  `RunError=null`, `TraceText=""`, SystemMetrics/StageRows/ChiSquareRows
  cleared, preview rows dropped; then `ApplyPreferences()` re-applies the
  persisted widget-visibility set — CONTENT resets, VISIBILITY preferences
  survive by design, FR-UI-21). Information log line. Tooltip updated (D-111).
- Three tests added (App +3 = 61): `ClearAll_ResetsResultsPanel_ToWelcomeState`,
  `ClearAll_UnloadsUploadedFile` (loads samples/sample_patients.csv, asserts
  "No file loaded" + `Binding` null), `ClearAll_KeepsPinnedFooterVisible`.
- Hermeticity note: a stale `~/.config/OpdSimulator/ui.json` containing
  "dataPreview" (a one-off artifact from the first test run) made the reset
  test read a non-default visibility set. Root cause: visibility is a
  persisted preference, so the assertion was corrected to the reset CONTRACT —
  widget content empty, not the visibility flag. With a clean store the suite
  is hermetic (verified twice: no ui.json written, store unchanged when
  seeded with defaults).
- Gate: Release build 0/0; **full suite 239 green** (Core 85 / Data 58 / Cli
  35 / App 61); real launch 15 s alive "Main window created." + crash log
  unchanged; DEV_LAUNCH §6 refreshed.

### Phase 5c, part 2 — 2026-09-16 05:10 — traceSink forwards through the calendar Engine.Run (5c.3)

- Owner approved the minimal Core change (byte-compatible additive seam).
  `Engine.cs` calendar `Run` gained `ITraceSink? traceSink = null`, forwarded
  to `RunCore`; the minutes-horizon overload already had one (D-110).
- **Core byte-compatibility proven: 85 green BEFORE the edit AND 85 green AFTER.**
- App side: `SimulationCoordinator` now passes its `CollectionTraceSink` in
  EVERY run mode; the "Only the Diagnostic trace mode records events (D-105)"
  placeholder was removed from ResultsPanel.axaml.
- Test fallout handled: the failing new test (`TraceViewer_PopulatesAfterClinicDayRun`)
  was a TEST-DATA bug — `ManualMuPerStage` set one μ for the default 3-stage
  config, so `BuildStageSpecs` refused with "Stage has no service rate…".
  Fixed to `"0.8, 0.5, 0.4"` (one μ per stage), matching the screenshot
  fixture. Two EXISTING Phase5RunFlowTests assertions
  (`Assert.Empty(outcome.TraceLines)` on ClinicDay/MultiDay) were updated to
  reflect the new reality — calendar runs now populate the trace.
- Fresh-launch FR-UI-5 evidence: `Phase5cScreenshot.Render_FreshLaunchWelcome_
  SavesWelcomeSnapshot` captures `logs/screenshots/phase-5c-welcome.png`
  (welcome card with logos + "Group members" card, asserted in-test).
- Gate: Release build 0/0; **full suite 236 green** (Core 85 / Data 58 / Cli 35
  / App 58 — one more than the owner's 235 estimate because of the welcome
  screenshot test); real launch smoke "Main window created." with crash log
  unchanged (0 new entries); `phase-5c-results.png` regenerated (60.9 KB) +
  `phase-5c-welcome.png` new (48.5 KB).

### Phase 5c — 2026-09-16 03:20 — post-review fixes (5c.1 + 5c.2 done, 5c.3 blocked, checkpoint reached)

Implemented and (mostly) gate-evidenced the first two Phase-5c fixes plus a
discovered FR-UI-14 defect, then STOPPED at the owner's mandatory checkpoint
because 5c.3 needs a Core decision (B-008).

- **5c.1 — results column scrolls.** `Views/ResultsPanel.axaml` restructured:
  the `HasRun` grid is now `RowDefinitions="Auto,*"` — the header row + the
  "Customise results" toggle + widget picker are PINNED at the top; the
  metrics / chi-square / data-preview / event-trace widgets sit in a
  `ScrollViewer` (Vertical=Auto, Horizontal=Disabled) so the event trace no
  longer has to fit vertically. The results column stays readable via
  `MinWidth="540"` on the results `Border` in `MainWindow.axaml` — Avalonia's
  compact grid parser rejects `MinMax(540,*)` with AVLN2005, so the minimum
  moves to the border. Verified by `ResultsPanel_ScrollViewer_ContainsAllWidgets`.
- **5c.2 — Wayland DBus quirk no longer crashes.** `App.axaml.cs` filter +
  `CrashReporter.IsIgnorableWaylandQuirk` (internal, exposed to tests via the
  repo's first `InternalsVisibleTo`). The 02:49 crash log was the historical
  reproduction inside Tmds.DBus; message-level matching was chosen over type
  matching because the class is `DBusException`, not `ServiceUnknown`
  (D-107). Verified by `CrashReporter_IgnoresAppMenuRegistrarDBusError`.
- **D-108 bug found + fixed:** `MainViewModel` passed `new WidgetPreferences()`
  — never `Load()` — so persisted widget visibility was never restored, and
  the default seed was all-off despite the documented all-on. Now loads + seeds
  all-on. The dev machine's stale `ui.json` (`VisibleWidgets: []`) is flagged,
  not deleted. This made all window-based UI tests run with hidden widgets;
  the screenshot test now hosts its own `ResultsPanelViewModel` with explicit
  temp-file preferences to stay hermetic.
- **5c.3 — RESOLVED (B-008 → option (a)):** see the "Phase 5c, part 2" entry above — calendar `Engine.Run` now forwards an optional `ITraceSink` (D-110, Core 85 green before AND after); `SimulationCoordinator` collects a trace in every run mode; `TraceViewer_PopulatesAfterClinicDayRun` green. (Historical record below captured the blocked state for the viva's bug-fix narrative.)
- **Gate evidence collected:** Release build 0/0; full suite **233 green**
  (App +3 = 55); real launch smoke (3:18) 15 s alive "Main window created." +
  crash-*.log unchanged; `logs/screenshots/phase-5c-results.png` (65 KB,
  metrics + chi-square + populated State trace, in-test overflow assertion).
- **Next:** owner decision on 5c.3 → commit 5c.1/5c.2 (+ D-107/D-108; the
  owner-specified commit message mentions 5c.3, so it must be confirmed) →
  push → Phase 5d.

### Session Handoff — 2026-09-16 02:32
Branch: feat/gui-rebuild (Phase 5 — STOPPED at the 5-H gate for owner review)
Status: Clean (all changes below are committed/queued; Phase-5 code complete and gate-verified, awaiting owner review + merge per AGENTS §11.5)

Done
- Phase 5 (5-A..5-H) — ResultsPanel + run flow on feat/gui-rebuild: coordinator seam (D-104), three-valued RunMode with DiagnosticTrace (D-105), welcome card, widget selector, refusal banners, 8 run-flow tests + gate screenshot.

In Progress
- None (STOPPED at the Phase 5 gate; owner must review + merge into main before Phase 6 — TODO row for Phase 5 flips `[x]` only after merge)

What is complete:
- 5-B — `Models/{RunMode,SimulationParameters,DataBindingResult,FitReport}.cs`, `Services/{DataAnalyzer,FitsService,CollectionTraceSink,WidgetPreferences,SimulationCoordinator}.cs`; DECISIONS D-104 + D-105. Commit `3901016`.
- 5-Config — `ConfigPanelViewModel` run modes + per-mode gating + `TryBuildRunParameters` + `Binding` analysis on load; ConfigPanel 3rd radio + Horizon(minutes) field. Commit `c3d4bcd`.
- 5-C/5-D/5-A — `CourseInfo.cs` (App), `WelcomeCardViewModel`, `WelcomeCard.axaml(.cs)`, `ResultsPanelViewModel`, `ResultsPanel.axaml(.cs)` (metrics / chi-square / trace / preview widgets, customise toggle, ErrorBanner), MainViewModel wiring (Task.Run + Dispatcher.UIThread.Post), MainWindow col-2 → ResultsPanel. Commit `61755ad`.
- 5-G — `Phase5RunFlowTests.cs` (8 tests: trace detail by level, diagnostic run records arrivals, ClinicDay = single-session no-trace, MultiDay exact day count, missing-λ and p_exit=1.0 and unstable-refusal banners, welcome-card swap). Fixed `TryBuildRunParameters` seed gating (Advanced seed must not gate Start — D-106) and comma-list μ backfill. Commit `e59729b`.
- 5-H gate — Release build **0 errors / 0 warnings**; full suite **230 green** (Core 85 / Data 58 / Cli 35 / App 52), 0 failed; gate screenshot `logs/screenshots/phase-5-results.png` rendered from a completed diagnostic run (λ 0.1; μ 0.8/0.5/0.4; TraceLevel State; horizon 1500 min) via the new `Phase5Screenshot.cs` test. Docs updated for the gate: DECISIONS D-106, REQUIREMENTS rows FR-UI-3/5/9/14/20/21 re-pointed at rebuilt sources + Phase-5 tests, USER_MANUAL §5/§6/§7/§9/§12, TODO Phase 5 row + 2 polish items, PROGRESS (this entry), and new `docs/VIVA_ANSWERS.md`.

What remains:
- Owner review + merge of feat/gui-rebuild into main; then Phase 6 (Help tab + preset system).

Next Session Should Start With
- Owner review/merge gate for feat/gui-rebuild, then Phase 6 — Help tab (Markdig) + preset system (TODO line ≈43)
- Post-Phase-5 polish items in TODO §5-A1 (manual-μ consolidation; trace-widget scroll performance)

Blocked
- None (B-007 keyboard-acceptance rows remain owner-blocked for a later manual pass per D-089)

Git State
- Commits made this session: b861ae8 (4c, prior entry) → 3901016 (5-B), c3d4bcd (5-Config), 61755ad (5-C/5-D/5-A), e59729b (5-G tests); Phase5Screenshot.cs + docs queued for the final gate commit
- Pushed to origin: yes, history pushed with the Phase-5 row (feat/gui-rebuild); final gate commit pushes with this entry

Build & Test
- dotnet build: PASS (Release) — 0 errors, 0 warnings
- dotnet test: PASS — 230 green (Core 85 / Data 58 / Cli 35 / App 52), 0 failed

Decisions Made
- D-104 — Coordinator seam for refused runs (topology built inside the try; G3/G4)
- D-105 — Three-valued RunMode ClinicDay | MultiDay | DiagnosticTrace
- D-106 — Manual-μ dual-source resolution (stage row wins, list backfills) + seed never gates Start
- (full entries in DECISIONS.md, appended in real time)

Assumptions Added/Changed
- D-100 [UNVERIFIED] from 2026-09-16 carried forward unchanged (blank per-stage μ → use fitted value)

Notes for Next Session
- The guest/Owner-review gates are STOP points: nothing may begin without an explicit "go".
- Phase 5 flips `[x]` in TODO.md only after the owner merges the branch into main.

### Session Handoff — 2026-09-16 02:05
Branch: feat/gui-rebuild
Status: Clean (Phase 4c shipped, awaiting owner eye-ball for Phase 5)

Done
- Phase 4c — owner corrections to Phase 4b (feat/gui-rebuild): optional-OFF sections no longer gate Start (D-103 supersedes the D-102 deviation note); white/light header switch; single full-width blue section bar.

In Progress
- None (STOPPED at the Phase 4c gate; Phase 5 not started)

What is complete:
- 4c.1 — Toggled-OFF sections: `OnParametersIsOptionalEnabledChanged(false)` / `OnAdvancedIsOptionalEnabledChanged(false)` call `ClearError()` on their fields; `RecomputeBlockingState()` filters those fields out; toggling back ON re-evaluates Start without pre-flagging (re-validate on next blur). Phase-4 test `PExit_ValueOne_SetsInlineError_AndBlocksStart` now enables Parameters first; 3 new tests: `OptionalSection_ToggledOff_ClearsFieldErrors`, `OptionalSection_ToggledOff_DoesNotBlockStart`, `OptionalSection_ToggledOn_RevalidatesOnNextBlur` (note: CommunityToolkit setters skip the callback on an unchanged value, so ground-false is a no-op — tests use real true→false transitions).
- 4c.2 — Custom `HeaderToggleSwitch` ControlTheme: white/light track+knob readable on the blue bar (dark knob on white track OFF; white knob on dark track ON). ToggleSwitch requires `PART_MovingKnobs` to be a `Panel` (AVLN2207); inline part Styles inside a ControlTheme need `x:SetterTargetType` (AVLN2200) — build lessons logged in D-103.
- 4c.3 — Header is now ONE brand-green bar spanning the full card width (top corners `8,8,0,0`), padding `ThicknessSectionHeader`, title white, InfoIcon far-right, switch between; expanded content sits on the panel-white background below the bar (no second coloured frame).
- A temp `Exclude="Assets\**\*.axaml"` csproj experiment was reverted (it stopped App.axaml compiling — explicit items suppress SDK defaults); the transient duplicated-`AvaloniaResources` manifest that crashed headless `StandardAssetLoader` was a stale partial compile and cleared on clean rebuild.

What remains:
- Phase 5 — ResultsPanel + run flow, awaiting the owner's second "go".

Next Session Should Start With
- Phase 5 — ResultsPanel + run flow (TODO line 41)

Blocked
- None

Git State
- Commits made this session: f7641b1 (Phase 4b, prior session block) → (Phase 4c commit lands with this entry)
- Pushed to origin: pushed with this entry (feat/gui-rebuild)

Build & Test
- dotnet build: PASS — 0 errors, 0 warnings
- dotnet test: PASS — 221 green (Core 85 / Data 58 / Cli 35 / App 43), 0 failed

Decisions Made
- D-103 — Optional section OFF means its fields do not participate in Start gating (supersedes the D-102 deviation note; also logs the HeaderToggleSwitch build lessons)

Assumptions Added/Changed
- None new (D-100 [UNVERIFIED] from 2026-09-16 carried forward unchanged)

### Session Handoff — 2026-09-16 01:35
Branch: feat/gui-rebuild
Status: Clean (Phase 4b shipped, awaiting owner "go" for Phase 5)

Done
- Phase 4b — ConfigPanel UI corrections (feat/gui-rebuild): white section headers on brand-green bars with 12,10 padding (`ThicknessSectionHeader`), InfoIcon right-anchored in a new `Auto,*,Auto,Auto` header grid, and optional-section enable toggles (Parameters + Advanced) that disable/dim descendant fields with FR-UI-7 tooltips while OFF and persist in the VM (D-101/D-102).

In Progress
- None (STOPPED at the Phase 4b gate; Phase 5 not started)

What is complete:
- Phase 4b gate: Release build 0/0; 218 green (Core 85/Data 58/Cli 35/App 40, +5 tests); real launch 15s alive "Main window created." 0 new crash logs; screenshot `logs/screenshots/phase-4-config.png` regenerated (43 KB); DECISIONS D-101/D-102, TODO 4b [x], DEV_LAUNCH §6 + Changelog row.

What remains:
- Phase 5 ResultsPanel + run flow — awaiting owner "go".

Next Session Should Start With
- Phase 5 — ResultsPanel + run flow (TODO line 40)

Blocked
- None

Git State
- Commits made this session: (Phase 4b commit lands with this entry)
- Pushed to origin: No — pushed after this block (feat/gui-rebuild)

Build & Test
- dotnet build: PASS — 0 errors, 0 warnings
- dotnet test: PASS — 218 green (Core 85 / Data 58 / Cli 35 / App 40), 0 failed

Decisions Made
- D-101 — CollapsibleSection header redesign (see DECISIONS.md)
- D-102 — Optional-section enable-toggle pattern (see DECISIONS.md)

Assumptions Added/Changed
- Start-blocking stays independent of the optional toggles (any field error blocks Start) — required to keep the Phase-4 baseline (p_exit = 1 blocks at factory ground) green; the "off = not supplied" rule governs run parameters + ρ preview. Flagged in DECISIONS D-102 for owner awareness.

### Session Handoff — 2026-09-16 01:10
Branch: feat/gui-rebuild
Status: Clean (Phase 4 shipped, awaiting review)

Done
- Phase 4 — ConfigPanel (feat/gui-rebuild): Data / Model / Parameters / Stages 1–5 / Horizon / Advanced + PinnedFooterBar Start & Clear All. Files: `Views/ConfigPanel.axaml(.cs)`, `ViewModels/ConfigPanelViewModel.cs`, `ViewModels/ConfigFieldViewModel.cs`, `ViewModels/MainViewModel.cs`, `tests/Phase4ConfigTests.cs`; `MainWindow.axaml` now hosts `<views:ConfigPanel DataContext="{Binding Config}"/>`.

What is complete:
- Six CollapsibleSection groups in one ScrollViewer + pinned footer (D-096); p_exit visible only for 2+ stages (D-097) and boundary [0,1) enforced inline, blocking Start (D-098); stages resize live 1–5 with default names Reception/Screening/Doctor/Stage 4/Stage 5 (D-099, D-100); blur-based validation routed via the `ConfigPanelValidation.ValidationKey` attached property; Upload → DataLoaderFactory row count; Clear All → ThemedDialog confirm then reset.

What remains:
- Phase 5 ResultsPanel + run wiring (awaiting owner "go" — STOP at phase boundary per gate).

Next Session Should Start With
- Phase 5 — ResultsPanel + run flow (TODO line 39)

Blocked
- None

Git State
- Commits made this session: <pending — one Phase 4 commit after this entry>
- Pushed to origin: Yes (after commit)
- Uncommitted changes: none (commit lands with the Phase 4 commit)

Build & Test
- dotnet build: PASS — 0 errors, 0 warnings
- dotnet test: PASS — 213 green (Core 85 / Data 58 / Cli 35 / App 35), 0 failed

Files Touched
- src/OpdSimulator.App/Views/ConfigPanel.axaml, ConfigPanel.axaml.cs: added
- src/OpdSimulator.App/ViewModels/ConfigFieldViewModel.cs, ConfigPanelViewModel.cs, MainViewModel.cs: added
- src/OpdSimulator.App/Views/MainWindow.axaml: modified (DataContext → MainViewModel; column-0 Content → ConfigPanel)
- tests/OpdSimulator.App.Tests/Phase4ConfigTests.cs: added (10 tests incl. screenshot)
- docs/: TODO.md, PROGRESS.md, DECISIONS.md, DEV_LAUNCH.md, CONTEXT.md

Decisions Made
- D-096 ConfigPanel layout (ScrollViewer + pinned footer)
- D-097 p_exit hidden for 1-stage configs
- D-098 p_exit boundary [0,1) matches Core
- D-099 Stage row is a dedicated view-model class (not a tuple)
- D-100 Blank service-rate = fitted fallback; Start enabled by default

Assumptions Added/Changed
- D-100 tagged [UNVERIFIED] in CONTEXT.md — blank per-stage rate means "use fitted value" (needs owner sign-off vs PRD "double > 0")

Notes for Next Session
- Phase 4 verified live on 2026-09-16: real launch 15 s "Application started. Main window created.", 0 new crash entries; screenshot `logs/screenshots/phase-4-config.png`.

## GUI REBUILD — Phase 5 — ResultsPanel + Run Flow — 2026-09-16 (feat/gui-rebuild)

**Phase gate: STOPPED — Phase 5 code complete and gate-verified; awaiting owner
review + merge of feat/gui-rebuild into main (AGENTS §11.5). Phase 6 not started.**

What shipped (commits `3901016`, `c3d4bcd`, `61755ad`, `e59729b`):
- **D-104 — Coordinator seam.** `Services/SimulationCoordinator.Run` owns the
  whole run: the topology is built **inside** the try (a ρ ≥ 1 topology throws
  `UnstableSystemException`; an out-of-range `exitProbability` throws
  `ArgumentOutOfRangeException`), so the VM can never crash the UI thread and
  the refusal banner shows the exact Core message. Returns a structured
  `RunOutcome(Result?, Fits, TraceLines, EffectiveExitProbability, Error?)`.
  Two dedicated banners: `MissingArrivalRateMessage` (no manual λ **and** no
  valid loaded data) and `FittedPExitEqualsOneMessage` (every data row exits
  at Screening so the Doctor stage is unreachable — recompute with an override,
  G3/G4). `DataAnalyzer` is the single data-source path; fitted λ is null
  only for < 2 arrivals.
- **D-105 — Three-valued RunMode ClinicDay | MultiDay | DiagnosticTrace.**
  Only DiagnosticTrace reveals Horizon (minutes) (int ≥ 1, default 10000) and
  the Trace-level dropdown, and hides Days/Start day/Daily cap; the other modes
  hide Horizon + Trace level. Start gating follows 4-c.1 (visible fields only);
  leaving a mode clears its hidden field's stale error. Dispatching (frozen
  Core untouched): DiagnosticTrace → `Engine.Run(topology, seed, horizonMinutes,
  sink)`; ClinicDay/MultiDay → `Engine.Run(topology, ClinicCalendar(startDay),
  generatorDays, seed, dailyCap)` (calendar overload hardcodes `traceSink: null`,
  so only diagnostic runs produce a trace).
- **D-106 — manual-μ resolution.** Per-stage `ServiceRate` row wins; the
  Parameters `ManualMuPerStage` list backfills blanks, both after rate/mean
  conversion. Raw `Seed.Value` never gates Start (Advanced is optional);
  `EffectiveSeed` falls back to 42 — a bug found during 5-G where the factory-
  default config was un-startable.
- **5-C/5-D/5-A — welcome card + results panel.** `CourseInfo.cs` constants;
  `WelcomeCardViewModel`/`WelcomeCard.axaml` (logo, members, CS-577, professor
  — themed with rebuilt tokens); `ResultsPanelViewModel`/`ResultsPanel.axaml`
  with the metrics table, per-stage ρ block, chi-square, trace, and data
  preview widgets, a "Customise results" widget toggle persisted via
  `WidgetPreferences`, and the ErrorBanner. `MainViewModel` subscribes to
  `RunRequested`, runs the coordinator on a `Task.Run` worker and posts status
  back with `Dispatcher.UIThread.Post`; `MainWindow` column-2 placeholder →
  ResultsPanel.
- **5-G — 8 run-flow tests** (`Phase5RunFlowTests.cs`): trace detail by level,
  diagnostic run records arrivals, ClinicDay = one-session run with no trace,
  MultiDay generates exactly the requested days, and the three refusal banners;
  welcome card visible initially and replaced by the first run attempt.

**§18 verification — Phase 5 verified on 2026-09-16 by agent (feat/gui-rebuild):**
- Headless run-flow tests: 8 in `Phase5RunFlowTests.cs` (see above) — all pass.
- Screenshot `logs/screenshots/phase-5-results.png` (62 KB) rendered by the new
  `Phase5Screenshot.cs` headless test: a full `MainWindow` after a *completed*
  diagnostic run — λ 0.1, μ 0.8/0.5/0.4, TraceLevel State, horizon 1500 min —
  showing metrics + chi-square + trace populated. (Agent cannot visually inspect
  the image — owner to eyeball.)
- This gate's launch smoke is the screenshot test itself (it constructs the real
  `MainWindow` and dispatches a real run end-to-end). Real-display launch and
  §16.8 keyboard walk remain owner-required (per D-089, captured in B-007).

Gate evidence:
- `dotnet build OpdSimulator.sln -c Release` → 0 errors, 0 warnings (the
  README/DEV_LAUNCH release command in §4).
- `dotnet test OpdSimulator.sln -c Release --no-build` → **230 green** (Core 85
  / Data 58 / Cli 35 / App 52), 0 failed.
- Docs updated at the gate: DECISIONS D-106 (D-104/D-105 were logged when the
  decision was made, §9.3); REQUIREMENTS rows FR-UI-3/5/9/14/20/21 re-pointed at
  rebuilt sources + Phase-5 tests; USER_MANUAL §5/§6/§7/§9/§12; TODO Phase 5 row
  + §5-A1 polish items; new `docs/VIVA_ANSWERS.md` (`Phase 5` section). DEV_LAUNCH
  unchanged — the build/launch commands did not change this phase (§10.2).
- Dev-notes: `ValidateHorizonMinutes` is registered under the `horizon-minutes`
  validation key in `ConfigPanel.axaml.cs`; the chi-square row maps validation
  rows `RowNumber` (1-based) to preview index −1; the 5-G seed-gating bug above
  was caught because the tests exercised a factory-default config.

## GUI REBUILD — Phase 4c — Owner Corrections to Phase 4b — 2026-09-16 (feat/gui-rebuild)

**Phase gate: STOPPED — awaiting owner eye-ball of `logs/screenshots/phase-4-config.png` before Phase 5.**

Three owner-reported defects in Phase 4b, fixed:

- **4c.1 — Toggled-OFF fields must not block Start (D-103).**
  Prior behaviour (D-102 deviation note): with the Parameters toggle OFF an
  error in `p_exit` etc. still disabled the Start button. Now an OFF optional
  section contributes nothing: `OnParametersIsOptionalEnabledChanged(false)` /
  `OnAdvancedIsOptionalEnabledChanged(false)` call `ClearError()` on all of
  the section's fields (so stale red borders disappear too), and
  `RecomputeBlockingState()` only includes `ManualLambda`/`ManualMuPerStage`/
  `PExit` when `ParametersIsOptionalEnabled` and `Seed` when
  `AdvancedIsOptionalEnabled`. Flipping the toggle back ON re-runs
  `RecomputeBlockingState()` but pre-flags nothing — each field re-validates
  on its next blur. (Start gating therefore depends only on what the user is
  actually using.) Tests in `Phase4ConfigTests.cs`: `OptionalSection_ToggledOff_
  ClearsFieldErrors`, `OptionalSection_ToggledOff_DoesNotBlockStart`,
  `OptionalSection_ToggledOn_RevalidatesOnNextBlur`; the Phase-4 baseline
  `PExit_ValueOne_SetsInlineError_AndBlocksStart` now enables the Parameters
  toggle first so its assertion still exercises blocking.
- **4c.2 — White/light toggle on the blue header.** The Fluent `ToggleSwitch`
  exposes no knob colour properties (only `KnobTransitions`/`OnContent`/
  `OffContent`), so its dark knob vanished against the brand bar. A local
  `HeaderToggleSwitch` ControlTheme draws an explicit track + knob: white
  pill with a dark-green knob when OFF, dark-green pill with a white knob
  when ON, 2 px focus ring on `:focus-visible`. Two compiled-XAML contracts
  surfaced as build errors and are logged in D-103: the knob part must be
  named `PART_MovingKnobs` and typed `Panel` (ToggleSwitch's knob-animation
  contract, AVLN2207), and inline part Styles inside a `ControlTheme` need
  `x:SetterTargetType="Border|Panel"` so compiled setters know each part's
  CLR type (AVLN2200).
- **4c.3 — Single blue bar, no blue-on-green.** The header bar previously
  rendered as an inner blue box inset within the outer card Border (its
  corner radius + padding showed the panel background around it). The whole
  outer Border is now a plain rounded card; the brand-green bar spans the
  full width (top corners `8,8,0,0` to match the card), carries the white
  title, the right-anchored switch, and the InfoIcon, and the expanded body
  sits on the panel-white background beneath with `ThicknessSpaceM` padding.
  No background behind the bar; no ControlStyles/theme-token changes.

Build & test notes (logged for the viva): a mid-fix partial compile left a
stale `AvaloniaResources` manifest with `/Assets/ControlStyles.axaml` twice
(crashed headless `StandardAssetLoader` with a duplicate-key ArgumentException
— masked on incremental builds); a clean rebuild after the axaml errors were
fixed resolved it. A tentative `Exclude="Assets\**\*.axaml"` on the
`AvaloniaResource` glob was reverted: explicit AvaloniaResource items suppress
the SDK's default axaml items, so excluding axaml silently dropped `App.axaml`
("No precompiled XAML found").

Verified live on 2026-09-16: Release build 0/0; full suite **221 green**
(Core 85 / Data 58 / Cli 35 / App 43); 15 s real launch "Application started.
Main window created."; 0 new crash logs (only stale `crash-20260915.log`);
screenshot `logs/screenshots/phase-4-config.png` regenerated (45 KB, 01:56).

## GUI REBUILD — Phase 4b — ConfigPanel UI Corrections — 2026-09-16 (feat/gui-rebuild)

**Phase gate: STOPPED — awaiting owner "go" before Phase 5.**

What shipped:
- **4b.1** `CollapsibleSection` header title now renders **white** on a
  **brand-green bar** (`Foreground BrushTextOnBrand`, background
  `BrushBrandGreen`); chevron stroke matches (D-101).
- **4b.2** New theme resource **`ThicknessSectionHeader = "12,10"`** — the blue
  bar's vertical padding grows to 10 px top/bottom, left/right stay 12 px.
- **4b.3** Header layout re-built as `Grid ColumnDefinitions="Auto,*,Auto,Auto"`:
  collapse ToggleButton (chevron + title) spans the first two columns so a
  click still collapses; the **InfoIcon is anchored to the far right** and the
  unused header ContentPresenter is removed (D-101).
- **4b.4** **Optional-section toggle pattern** (D-102): `CollapsibleSection`
  gains `IsOptional` + two-way `IsEnabledToggle` and a header `ToggleSwitch`.
  **Parameters** and **Advanced** are flagged optional and default **OFF**;
  while OFF the section content is `IsEnabled=false` + `Opacity 0.5` and every
  descendant field gets the injected FR-UI-7 tooltip "Enable '\<Section\>'
  above to edit this field." (hand-authored tooltips win). State lives in the
  VM — Clear All resets both toggles to OFF.
- Semantics when OFF: Parameters → run sees no manual λ/μ/p_exit
  (`ParametersSupplied == false`), ρ preview reads "—" (`RecomputeRho` needs
  the toggle on); Advanced → `EffectiveSeed` **42**, `EffectiveTraceLevel`
  **State**. Start-blocking equation deliberately unchanged from Phase 4
  (field errors block regardless of toggle — required by the 213-test
  baseline, D-102).

**§18 verification — Phase 4b verified on 2026-09-16 by agent (feat/gui-rebuild):**
- Headless tests (5 new, `tests/OpdSimulator.App.Tests/Phase4bConfigTests.cs`):
  `OptionalSection_ToggleOff_DisablesFields` (3 fields present, effectively
  disabled, dimmed body, FR-UI-7 tooltip text), `OptionalSection_ToggleOn_
  EnablesFields` (switch → section → VM chain, fields re-enabled, opacity
  restored), `ClearAll_ResetsOptionalTogglesToOff`, plus two VM-behaviour tests
  (Parameters off ⇒ `ParametersSupplied == false` + ρ "—"; Advanced off ⇒
  seed 42 / trace State).
- Real launch (Wayland box, capture blocked per D-089): `timeout 15 dotnet run
  --project src/OpdSimulator.App -c Release --no-build` → **"Application
  started. Main window created."**, killed by timeout, **0 new crash log
  entries** (only stale `crash-20260915.log`).
- Screenshot `logs/screenshots/phase-4-config.png` **regenerated** (43 KB) by
  the existing Phase-4 screenshot test — owner to eyeball the new headers.

Gate evidence:
- `dotnet build -c Release` → 0 errors, 0 warnings.
- `dotnet test -c Release` → **218 green** (Core 85 / Data 58 / Cli 35 /
  App 40), 0 failed.
- Real launch 15 s alive, "Main window created." logged, 0 new crash logs.
- Docs: DECISIONS D-101 + D-102; TODO Phase 4b [x]; DEV_LAUNCH §6 refreshed
  (218 tests) + Changelog row.
- Dev-notes: (`IsEnabled` reports the local value; effective inheritance is
  `IsEffectivelyEnabled` — the disable assertions use the effective value.)

## GUI REBUILD — Phase 4 — ConfigPanel — 2026-09-16 (feat/gui-rebuild)

**Phase gate: passed — Phase 5 NOT started (owner "go" required).**

What shipped:
- `ConfigPanel` replaces the Simulation-tab column-0 placeholder: one
  ScrollViewer with six CollapsibleSections and a pinned footer
  (Start Calculation + Clear All) outside the scroll area (D-096).
  1 · Data (Upload → DataLoaderFactory, "No file loaded" / "Loaded N rows from …"),
  2 · Model (two SearchableDropdowns + Rate-wise/Mean-wise radio, CONTEXT §5.6),
  3 · Parameters (manual λ, manual μ comma-list, live ρ-per-stage summary, p_exit
  override shown only for 2+ stages — D-097, boundary [0,1) — D-098),
  4 · Stages (count 1–5 resizes rows live; default names Reception/Screening/Doctor/
  Stage N; per-row Servers + Service rate) — D-099,
  5 · Horizon (Single day default / Multi-day + Days + Start day, optional Daily cap),
  6 · Advanced (Random seed default 42, Trace level default State).
- `ConfigFieldViewModel` wraps value + HasError + ErrorMessage with blur-clear
  behaviour (FR-UI-17); `MainViewModel` exposes the Config panel; MainWindow
  DataContext is now a MainViewModel (FR-UI-21 clean start, no auto-restore).
- Blur-time validation is routed from the view to the VM through the
  `ConfigPanelValidation.ValidationKey` attached property; Clear All confirms via
  ThemedDialog then resets every field to factory defaults (D-100).

**§18 verification — Phase 4 verified on 2026-09-16 by agent (feat/gui-rebuild):**
- Headless tests (10 new, `test/OpdSimulator.App.Tests/Phase4ConfigTests.cs`): six
  sections + pinned footer present; stage-count resize (3→5→1 with default names);
  p_exit visible ⇔ stages ≥ 2; p_exit = 1 → exact inline error + `StartCalculationCommand`
  disabled; p_exit = 0.4 accepted and Start enabled; stage-name/servers two-way
  binding; Clear All reset (all fields verified); Clear All confirmation gate;
  Upload request event; screenshot capture.
- Real launch (Wayland box, capture blocked per D-089): `timeout 15 dotnet run
  --project src/OpdSimulator.App -c Release --no-build` → **"Application started.
  Main window created."** in `logs/app-20260916.log`, killed by timeout, **0 new
  crash log entries** (`crash-20260915.log` untouched).
- Screenshot `logs/screenshots/phase-4-config.png` (47 KB, headless; owner to
  eyeball).

Gate evidence:
- `dotnet build -c Release` → 0 errors, 0 warnings.
- `dotnet test -c Release` → **213 green** (Core 85 / Data 58 / Cli 35 / App 35), 0 failed.
- Real launch 15 s alive, "Main window created." logged, 0 new crash logs.
- Docs: DECISIONS D-096..D-100; CONTEXT assumption tagged; DEV_LAUNCH §6 refreshed
  (213 tests) + Changelog row; TODO Phase 4 [x].

## GUI REBUILD — Phase 3 MainWindow Shell — 2026-09-16 (feat/gui-rebuild)

**Phase gate: STOPPED — awaiting owner "go" before Phase 4.**

What shipped:
- `MainWindow` is now the shell: header bar + TabControl with four tabs —
  **Simulation | Input Analysis | Token Generator | Help**.
- Simulation tab: **380px config column / GridSplitter / fill results column**
  (mirrors M5's 400,6,* split). Config/Results placeholders carry themed
  "Phase 4/5" hints.
- New reusable `Controls/PlaceholderContent` (Title + Hint) fills all four
  tabs and will back the pre-build empty states until each real panel lands —
  built once, reused in all tabs (§16.5). New `Border.PanelCard` style in
  ControlStyles.axaml (theme tokens only).
- ControlsDemo (Phase-2 showroom) is no longer hosted in MainWindow; its
  screenshot test now hosts the demo in its own test window so the
  `controls-demo.png` evidence path is preserved.

**Keyboard contract (§16.7):** tab headers are the first focusable element;
arrow-key selection between tabs is implemented natively by the TabControl and
exercised headlessly (Right×2 → Input Analysis, Token Generator; Left → back).

**§18 verification — Phase 3 verified on 2026-09-16 by agent (feat/gui-rebuild):**
- Headless tests: 4 tab headers in order; Simulation default-selected; 380px
  pixel column + star results column (found via TabControl content presenter —
  the data grid is NOT a descendant of the TabItem, an Avalonia realisation
  quirk); MinWidth/MinHeight 1100×700 + Maximized; arrow-key tab navigation.
- Real launch (Wayland box, capture blocked per D-089): `dotnet run
  --project src/OpdSimulator.App -c Release --no-build` → "Application started.
  Main window created." in `logs/app-20260916.log`, killed by 15 s timeout,
  **0 new crash-log entries** (`crash-20260915.log` untouched).
- Screenshot `logs/screenshots/phase-3-shell.png` (headless; owner to eyeball).

Gate evidence:
- `dotnet build -c Release` → 0 errors, 0 warnings.
- `dotnet test -c Release` → **203 green** (Core 85 / Data 58 / Cli 35 / App 25), 0 failed.

## GUI REBUILD — Phase 2 Reusable Controls — 2026-09-15 (feat/gui-rebuild)

**Phase gate: STOPPED — awaiting owner "go" before Phase 3.**
(Owner also owes the real-display §16.8 keyboard walk of the 9 controls —
this box is Wayland, so headless evidence was used per D-089.)

What shipped:
- All nine reusable controls rebuilt/tested: ValidatedField, SearchableDropdown,
  ThemedDialog, ThemedToast, CollapsibleSection, InfoIcon, PinnedFooterBar,
  DataPreviewTable, ErrorBanner.
- `Views/ControlsDemo.axaml(.cs)` + `ViewModels/ControlsDemoViewModel.cs` —
  showroom hosting every control live inside MainWindow for visual/keyboard review.
- `tests/OpdSimulator.App.Tests/ControlsSmokeTests.cs` — 9 per-control `[AvaloniaFact]`
  headless tests (tooltips/a11y names, error cause+remedy + clear-on-fix, type-to-filter
  + Enter commit, toggle/collapse, dismiss, toast close-with-item, footer command via
  real pointer click, dialog Escape→Cancel / primary→Primary via `KeyPressQwerty`,
  sort cycle asc→desc→original + invalid-row banner).
- `tests/OpdSimulator.App.Tests/ControlsDemoScreenshot.cs` — renders MainWindow →
  `logs/screenshots/controls-demo.png` (69 KB frame; both png files identical render
  because MainWindow is now the showroom).

**Avalonia 11.3 API corrections surfaced during Phase 2 (see DECISIONS D-090..D-093):**
- No WPF-style `GetTemplateChild`: `OnApplyTemplate(TemplateAppliedEventArgs)` +
  `e.NameScope.Find("PART_…")`. XAML-name generator does not emit fields for
  elements inside `ControlTemplate`.
- `TemplatedControl` has no `Content`; `Shape` uses `StrokeJoin`; `StackPanel` has
  no `Padding`; `Popup` uses `IsLightDismissEnabled`.
- `ItemsRepeater` is NOT in Avalonia core; `Avalonia.Controls.ItemsRepeater` 11.x
  stops at 11.1.5 with no `VirtualizingStackLayout` → replaced with virtualising
  `ListBox` + code-built header buttons (D-090).
- Compiled bindings reject `$parent[UserControl]` (resolves base) → must use
  `$parent[controls:ConcreteType]`.

**Real bugs caught by the tests (Rule 9 — tests encode intent):**
- ErrorBanner: binding to inner `Root.IsVisible` meant the control could never
  escape its hidden state → `IsVisible` now mirrors `Message` (D-093).
- PinnedFooterBar: inner `ContentPresenter` bound to the UserControl's own `Content`
  was self-recursive under Measure (`Border already has a visual parent`) →
  control is now a `ContentControl` with a ControlTemplate (the chrome lives in the
  template; caller content presents exactly once).

**§18 verification — Phase 2 verified on 2026-09-15 by agent (feat/gui-rebuild):**
| Control | Headless test | Evidence |
|---|---|---|
| ValidatedField | error shows cause+remedy, clears on fix, FieldLostFocus on blur | Pass |
| SearchableDropdown | filter reduces 4→1, Enter commits SelectedItem | Pass |
| ThemedDialog | Escape→Cancel, primary→Primary, buttons = 2 | Pass |
| ThemedToast | severity text renders, Close runs with item | Pass |
| CollapsibleSection | toggle flips IsExpanded | Pass |
| InfoIcon | tooltip + automation name, help event | Pass |
| PinnedFooterBar | enabled/disabled reflects PrimaryIsEnabled; command via real click | Pass |
| DataPreviewTable | asc→desc→original cycle, invalid-row banner | Pass |
| ErrorBanner | message visible on set, hidden on dismiss | Pass |
- Screenshot `logs/screenshots/controls-demo.png` (headless, D-089; owner to eyeball).
- **Not performed on this box (Wayland):** real-display keyboard walk (§16.8). Owner task.

Gate evidence:
- `dotnet build -c Release` → 0 errors, 0 warnings (clean `bin`/`obj`).
- `dotnet test -c Release` → **197 green** (Core 85 / Data 58 / Cli 35 / App 19), 0 failed.

## GUI REBUILD — Phase 1 Foundation — 2026-09-15 (feat/gui-rebuild)

**Phase gate: STOPPED — awaiting owner "go" before Phase 2.** (Phase 2 work proceeded per owner instruction 2026-09-15.)

What shipped:
- Old M5 view layer deleted on this branch (Views/Controls/ViewModels/Services/
  Logging/Models/ViewLocator/app.manifest; LiveCharts2 + Serilog.Extensions.Logging
  dropped from the csproj). M6 chart files untouched on
  `feat/milestone-6-charts-and-token`.
- New `Assets/Theme.axaml` token contract (palette+brushes, 3 font families,
  sizes 18/14/12, spacing 4/8/12/16/24, radii 4/8/12, ShadowCard/ShadowOverlay,
  focus ring 2/1 px) + `Assets/Motion.axaml` (150/200/250/600 ms,
  MotionDurationReduced = 0) merged in App.axaml.
- `Views/MainWindow` — "OPD Clinic Queue Simulator", Maximized, min 1100×700,
  theme background; `Services/CrashReporter.cs` relocated (namespace → Services);
  §12.1 3-sink Serilog + §12.3 handlers carry over (D-088).
- Test project rebuilt around Avalonia.Headless(.XUnit): 6 smoke tests
  (title/state, theme/motion token resolution) + 1 render-capture test.

**§18 verification — Phase 1 verified on 2026-09-15 by agent (feat/gui-rebuild):**
launched `dotnet run --project src/OpdSimulator.App -c Release` on Linux
(Ubuntu 24.04, dotnet SDK 8.0.131); observed the maximized window
("Application started. Main window created." in `logs/app-20260915.log`),
WindowState=Maximized + Title asserted headless, app stayed alive >10 s,
**0 new crash-log entries** (`logs/crash-20260915.log` unchanged, 12 lines /
mtime 05:01). Screenshot `logs/screenshots/phase-1-window.png` — rendered via
Avalonia.Headless Skia frame capture because the Wayland compositor blocks X
frame capture here (D-089); owner to eyeball the PNG.

Gate evidence:
- `dotnet build -c Release` → 0 errors, 0 warnings.
- `dotnet test -c Release` → **185 green** (Core 85 / Data 58 / Cli 35 / App 7), 0 failed.
- Real launch → maximized, alive, no crash.

## Resume — 2026-09-14 — reconciled: 6 findings

Findings (all non-blocking for M6) + B-007 blocker recorded for the M5 keyboard pass:
1. **Git clean on `main`** @ `71a816a` (merge: milestone 5 — GUI view layer), up-to-date with origin, working tree clean. DEV_LAUNCH "Last verified" claims 243 green (Core 85 / Data 58 / Cli 35 / App 65) — to be re-verified at M6-G, not trusted blindly.
2. **A1 is already done as a package add:** `LiveChartsCore.SkiaSharpView.Avalonia` **2.0.5** + Avalonia **11.3.3** + SkiaSharp are already pinned in `OpdSimulator.App.csproj`. M6-A1 becomes a version-compatibility confirmation + D-086 decision, not a NuGet install. SkiaSharp native libs: none required on Linux beyond the existing Avalonia X11 libs (verify at G1).
3. **M5 already landed charts infrastructure:** `Services/ChartsBuilder.cs`, `ViewModels/ChartViewModels.cs` (ChartViewModel + ChartsPanelViewModel), `Views/ChartsPanel.axaml` with Input analysis / Simulation run tabs, LiveCharts2 2.0.5, `ChartsPanelViewModel.ForResources()` theme-colour resolution (D-084). FR-UI-4 is already `[x]` in REQUIREMENTS.md. M6 gaps versus the M5 chart shell: `ChartTheme.axaml` (A3), `ChartWidgetBase` abstraction (A4), per-server utilisation **imbalance flag + red outline** (B3), reduced-motion handling, empty/error states (D4/D5), and **token generator is entirely absent** (E — ResultsPanel shows only a placeholder readout; FR-TOKEN-1..3 all `[ ]`).
4. **DEV_LAUNCH §5 stale text:** lines 99–103 still say "the window opens but shows placeholder config/results panels … until then the CLI stays the primary path", contradicted by the same §5's later bullet ("The full M5 GUI landed 2026-09-14: left config panel …"). Fix during M6-H1.
5. **`ChartsBuilder` uses `HistogramBinCount = 16`** (hardcoded), independent of the chi-square `BinSelector` k — M6 F1 requires histogram bin count == chi-square bin count for strict FR-STAT-8 consistency; also a **dead `fit.Label.Contains("P2")` branch** (~lines 48–51) that does nothing (pre-existing — mention, do not delete). Downsample cap `MaxQueuePoints = 2_000` already exists (NFR-6 half-ready).
6. **Token widget is a placeholder** (ResultsPanel.axaml lines 220–244 show issued-count + avg-wait summary only). No `TokenGeneratorViewModel`, no Little's-Law estimate, no ticket card. FR-TOKEN-1/2/3 rows all `[ ]`.
Also: **B-007 added** — M5 keyboard-only acceptance run on the running GUI (AGENTS §16.8/§16.7) is an owner manual task; the "8 controls UI acceptance" and "Tab order audit" TODO rows are marked `[?] BLOCKED` and linked to it.

## M5-C/D/E/F/L VIEW LAYER + TESTS — 2026-09-14 (feat/milestone-5-gui)

View/view-model layer for the whole M5 GUI landed and is verified. Build:
`dotnet build -c Release` → 0 warnings / 0 errors. Tests: full suite **243 green**
(Core 85 / Data 58 / Cli 35 / App 65), 0 failures.

**Shipped (sub-blocks C, D, E, F, L):**
1. `MainWindow` — `400,6,*` layout with GridSplitter; `ConfigPanel` | `ResultsPanel`;
   `PinnedFooterBar` (Start=`RunCommand`, Reset, Guide); guide overlay (F1 open,
   Esc close, focus save/restore via `_focusBeforeGuide`); ToastHost ItemsControl +
   600 ms DispatcherTimer purge; `OnClosed` override.
2. `ResultsPanel` — welcome-card switch via `ContentControl.ContentTemplate`
   (NOT `DataTemplate`), `HasError` banner, running progress, Customise widget
   picker (per-widget `ToggleWidgetCommand`), all six widgets: Metrics (+stages),
   ChiSquare, Charts (`Charts`/`ChartsPanelViewModel`), Trace (monospace
   ItemsControl), DataPreview (`ResultsPreviewTable.Load(vm.Preview)`), Token.
3. `ChartsPanel` — Input-analysis / Simulation-run tabs over `InputCharts`/
   `RunCharts` `ChartViewModel`s; LiveCharts `CartesianChart`; `ShowEmpty` +
   `HasCharts` states; OneTime bindings for stable props.
4. `GuidePanel` + `GuideViewModel` — search + section list (`SelectedTitle`
   string binding), code-behind block renderer (Heading/Paragraph/Bullet/
   Numbered/Code/Separator, inline **bold** + `code` runs), `FocusSearch()`.
5. `WelcomeCard` + `WelcomeCardViewModel` — CourseInfo values; logos via
   `AssetLoader.Open(new Uri(...))`. **Bug fixed this session:** `Bitmap(string)`
   treats its argument as a file path, so `avares://` URIs failed the smoke run;
   the AssetLoader-stream constructor is the correct avares path (crash-log
   evidence: `DirectoryNotFoundException .../avares:/OpdSimulator.App/...`).
6. `PresetManagerDialog.cs` — code-behind-only themed Window over `ThemedDialog`
   helpers; Load/Rename/Duplicate/Delete/Export/Import; `AppBrush` theme-resource
   helper (the `TryFindResource` extension does not resolve on a Window here).
7. Services: `SimulationCoordinator` (3 stages, exit index 1, p_exit 0.4 default,
   `UnstableSystemException` → `RunOutcome.Error`, capped trace sink),
   `ChartsBuilder`, `FitsService`, `DataAnalyzer`, `WidgetPreferences`,
   `GuideMarkdown` (no Markdig — **D-082**), `PresetStore`/`Preset`/`PresetNaming`
   (**D-083**).
8. Tests: `ResultsViewModelTests`, `MainViewModelTests`, `ChartViewModelTests`,
   `GuideTests`, `SimOutcomeFactory`, `PresetStoreTests`, `WelcomeCardViewModelTests`.

**Facts verified by running (Linux, this session):**
- `dotnet run --project src/OpdSimulator.App` opens the window and stays alive
  (10 s smoke run, exit 124 = still running at `timeout`), no crash log.
- After the AssetLoader fix, the app log shows no logo warning during startup.
- Pre-existing **gap found + closed**: the preset system had *no tests* despite
  an earlier handwritten claim; AGENTS §17.3 minimum suite written
  (round-trip, v99 schema refusal, missing-dataFile, sanitisation, collisions,
  ApplicationData path, export→import, case-insensitivity). The
  case-insensitive-name contract was enforced everywhere by adding
  `PresetStore.ResolveFile` (a directory scan fallback for Linux's
  case-sensitive FS).

**Decisions:** D-082 (guide uses a purpose-built parser, not Markdig), D-083
(preset schema v1 + `arrivalParameter` canonical minutes), D-084 (chart colour
fallback factory), D-085 (computed VM booleans over converters).

**Remaining, honest gaps (not done):** FR-UI-2 edge-case validation tests;
FR-UI-7 disabled-field reason tooltips; FR-UI-13 Clear All confirm+undo;
FR-UI-15/17 live-region + full keyboard-acceptance pass (§16.8 needs a real
mouse-less session on the running GUI — cannot be emulated here); welcome-card
fade-out; reduced-motion; preset UI `_lastSession`-less startup edge tests
(covered by design + one MainViewModel test). Windows dead-state verification
still pending (BLOCKERS B-006).

## M5-B Reusable Controls — 2026-09-14 (feat/milestone-5-gui)

Implemented all eight M5-B reusable controls (§16.5) on `feat/milestone-5-gui`
plus their pure-logic tests. Commits: `b097c49` (B1+B2), `6a32951` (B3–B6),
`93ec0c2` (tests + docs).

1. **Architecture split (D-080)** — chrome-only controls (`CollapsibleSection`,
   `PinnedFooterBar`) are `ContentControl` subclass + type-keyed `ControlTheme`
   in `Controls/ControlStyles.axaml` (merged into App.axaml); functional
   composites (`InfoIcon`, `ThemedToast`, `SearchableDropdown`, `ValidatedField`,
   `DataPreviewTable`, `ThemedDialog`) are `UserControl`s wired in code-behind.
   All visuals pull `DynamicResource` from Theme.axaml (zero hex elsewhere).
2. **B1** — `InfoIcon` ('?' + hover tooltip + clickable `HelpAnchor`);
   `ThemedToast` card + `ToastItem` VM + `ToastService` + pure `ToastLifecycle` expiry.
3. **B2** — `CollapsibleSection` + `PinnedFooterBar` via ControlTheme;
   shared `ToggleButton.collapsibleHeader` styles moved to `App.Styles`.
4. **B3** — `ThemedDialog`: Escape=Cancel / Enter=Confirm, focus restores to
   opener, error/info accent variants; parameterless ctor added for the XAML
   loader (AVLN3001).
5. **B4** — `SearchableDropdown` (type-to-filter, clear ×, chevron, arrow/Enter/
   Escape keys) + pure `SearchFilter` (prefix > substring ranking).
6. **B5** — `ValidatedField` (persistent label, optional '?', themed border,
   inline cause+remedy error that clears on fix).
7. **B6** — `DataPreviewTable` + pure `DataPreviewStore` (asc→desc→original sort
   cycle). **D-081:** stock Avalonia 11.3.3 has no `ItemsRepeater`, so the body is
   a virtualizing `ListBox` with read-only `TextBox` cells (selectable, Ctrl+C),
   invalid-row error tint + warning badge + reason tooltip.
8. **Tests** — new `tests/OpdSimulator.App.Tests` (xunit): SearchFilter (6),
   DataPreviewStore (8, incl. invalid-row preservation), ToastService (6);
   `ToastItem` gained an injectable `createdUtc` and `ToastLifecycle` is public
   for deterministic expiry. Added to sln; DEV_LAUNCH §6 refresh to 196 + §8 layout.

Fix-log (fail-loud): `ContentPresenter`→`Avalonia.Controls.Presenters`,
`TemplateAppliedEventArgs`→`Avalonia.Controls.Primitives` (both Avalonia 11.3);
`Classes.Reset()`→remove-then-add; `Panel.ZIndex` removed (unresolved attached
setter); `TextBox.Text` is nullable; `IsAttachedToVisualTree` is an extension in
`Avalonia.VisualTree`; `SelectionMode.None` doesn't exist.

Verification: `dotnet build OpdSimulator.sln -c Debug/Release` 0 warnings,
0 errors; `dotnet test` **196 green** (83/58/35/20), 0 failed.

## M5-A Assets & Foundation — 2026-09-14 (feat/milestone-5-gui)

Completed the foundation sub-block of Milestone 5 after owner's GO + kickoff adjustments:
1. **PRD header synced** to v1.4.0 / 2026-09-14 (docs-only commit a3d372e).
2. **A1 Avalonia shell hand-built** (D-078) — Program.cs (Serilog bootstrap: console + `logs/app-*.log` rolling 7-day + error-only sink), App.axaml/.cs (ViewLocator + the three §12.3 global exception handlers), ViewModels (ViewModelBase, MainViewModel), Views/MainWindow scaffold (placeholder config/results panels, replaced in M5-D), Logging/CrashReporter (appends `logs/crash-YYYMMDD.log` + user dialog, sim-state param ready for M5-E), app.manifest (PerMonitorV2, Windows-only). csproj → WinExe, compiled bindings on, references Core+Data, embeds `Assets\**`. Committed cc1bce3.
3. **A2 CourseInfo.cs** — CourseName "Simulation & Modelling", CourseCode "CS-577", Professor "Dr. Shaista Rais", 6 members, logo URIs (D-077). Single source; XAML keeps zero references.
4. **A3 Theme.axaml** — single palette source (D-077-era): 40 colours → brushes, typography (Inter/Segoe/system sans), spacing, radii, durations; `sys:TimeSpan` syntax fix; merged into App.axaml. No hex anywhere else.
5. **A4 logos** — `Assets/uok-logo.png` (1080×1080) + `Assets/ubit-cs-logo.png` (369×293) copied from ~/Downloads (D-077; quality flag on the 369 px UBIT logo reported, not blocking).
6. **DEV_LAUNCH** — §1 prerequisites row for Linux system libs (libx11-6 libice6 libsm6 libfontconfig1, owner-installed, D-079); §9 blank-window row consolidated to a cross-ref (§10.7, single canonical apt command); §5/§8 App status refreshed. Committed 53f8bba.
7. **BLOCKERS B-005 Resolved** via hand-build; D-077..D-079 logged; TODO 5 foundation rows `[x]`.

Verification: `dotnet build OpdSimulator.sln` 0 warnings, 0 errors; **176 tests green** (83/58/35 — unchanged); app launch smoke-tested on Ubuntu (`Application started. Main window created.`; logs/app-20260914.log written). 0 warnings everywhere.

## M5 UI/UX requirements capture — 2026-09-14 (fix/ui-requirements-capture)

Applied the six-file M5 documentation batch as a single docs-only change:
- PRD.md → v1.4.0: FR-UI-5..21 in §5.1 (after FR-UI-4), NFR-7..10 in §6 (after NFR-6), changelog row.
- AGENTS.md → Sections 16 (UI/UX standards, incl. §16.8 pre-commit checklist, §16.9 ValidatedField, §16.10 DataPreviewTable, §16.11 startup) and 17 (in-program guide via Markdig + presets). Note: file previously ended at §14.3 — no §15 existed; §16/17 appended as authored, preserving the cross-file numbering the whole batch references.
- DECISIONS.md → D-060..D-076 (17 M5 entries; IDs assigned by scanning for the next free number, D-059 was the last).
- TODO.md → "M5 — GUI (see PRD §5.1, AGENTS §16–17)" planned block under Upcoming, + the capture task as the only `[x]` in Active.
- VIVA_ANSWERS.md → M5-1..M5-15 + glossary additions.
- docs/M5_UI_SPEC.md → new quick-reference spec (feature map, assets, controls, keyboard map, error contract, preset paths, startup sequence, preview contract, accessibility acceptance).
- REQUIREMENTS.md → 21 new rows (FR-UI-5..21, NFR-7..10) all `[ ]` (no source/test yet); coverage summary recomputed 29/67 = 43.3% (35 `[ ]`); "Last synced" → v1.4.0; changelog row.

Verification: no `D-XXX` placeholders remain; D-060..D-076 present; FR-UI count 21 in both PRD and matrix; NFR-7..10 present; AGENTS §16/§17 present. Docs-only — no UI code was written.

### Session Handoff — 2026-09-14 05:55
Branch: feat/milestone-4-event-trace
Status: Clean (7 commits pushed; awaiting owner review/merge of the branch)

Done
M4 sub-tasks A–D — trace abstraction, engine instrumentation, `trace` CLI, golden regression (f6b95e7, 94a2d8e)
M4 docs pass — DEV_LAUNCH §6+§7.6, USER_MANUAL §7.6, REQUIREMENTS FR-VAL-4 + coverage 63.0%, VIVA_ANSWERS +5, DECISIONS D-055..D-059, TODO, PROGRESS (2b6c730)
CLI trace tests end-to-end — golden stdout, levels, refusal, --output, usage (e40e1e3)
stdout purity — "CLI run requested" → Debug; trace stdout carries only trace lines
Golden fixture byte-identical to CLI stdout (trailing-newline commit a94ec7c)
Dead-state verification — full bin/obj wipe → restore → Release build 0 errors → 176 green → documented commands re-run

In Progress
None

What is complete: Milestone 4 (event trace) is implemented, tested and documented:
`trace` CLI emits a deterministic ARRIVAL/START_SVC/END_SVC/ROUTE/EXIT story (+ draw rows
at --level rng) whose 5-patient golden trace was hand-verified draw-by-draw against the
reference Random(42) sequence; regression tests lock byte identity, RNG parity, level
filtering, stats agreement and sink passivity; engine stays level-blind and metric-neutral.
176 tests (83+58+35), 0 warnings, dead-state verified on Ubuntu 24.04. M1 golden values
re-verified live (served 29892, wait 0.724, ρ 0.75).

What remains: owner review + merge of the branch into main (AGENTS §11.5 item 6). No
open M4 work items.

Next Session Should Start With
Owner: review/merge feat/milestone-4-event-trace into main.
Then M5 GUI (Avalonia; B-005 remains an open owner/OS blocker; M5_UI_SPEC.md to be
written at kickoff). M2 sweep / FR-STAT-5 analytical validation remains a future M5/M7 item.

Blocked
None new (B-001..003, B-005, B-006 remain open owner/OS blockers, unrelated to M4).

Git State
Commits made this session: 3705830 (docs resume), f6b95e7 (trace abstraction + engine),
94a2d8e (trace CLI + golden tests), e84d50d (docs D-055..D-059), e40e1e3 (CLI trace tests),
2b6c730 (docs pass), a94ec7c (fixture newline).
Pushed to origin: Yes (feat/milestone-4-event-trace).

Uncommitted changes: None.

Build & Test
dotnet build: PASS (0 warnings)
dotnet test: PASS — 176 passed, 0 failed (83 Core + 58 Data + 35 Cli), from dead state

Warnings: 0

Files Touched
src/OpdSimulator.Core/Trace/: added 9 files (TraceLevel, TraceEventType, TraceEvent,
ITraceSink, TraceFormatter, TraceClock, TraceRandomSource, TextWriterTraceSink, NullTraceSink)
src/OpdSimulator.Core/Engine/: Engine.cs, EngineConfig.cs modified (trace emission, maxCompletedPatients)
src/OpdSimulator.Cli/Commands/TraceCommand.cs: added
src/OpdSimulator.Cli/Program.cs: modified (trace dispatch + usage; CLI-run line at Debug)
tests/OpdSimulator.Core.Tests/: TraceRegressionTests.cs added; Fixtures/trace-5-patients.txt added;
csproj fixture-copy rule
tests/OpdSimulator.Cli.Tests/: CliTraceTests.cs added; csproj links the golden fixture
docs/: DEV_LAUNCH, USER_MANUAL, REQUIREMENTS, DECISIONS (D-055..D-059), TODO, PROGRESS; VIVA_ANSWERS.md (+5)

Decisions Made
D-055 — trace as a first-class sink feature (DECISIONS.md)
D-056 — TraceEvent schema + fixed q= semantics
D-057 — passive TraceRandomSource wrapper
D-058 — trace/stats cross-check + golden lock + maxCompletedPatients early break
D-059 — file-only logger for pure trace stdout

Assumptions Added/Changed
None new this session.

Notes for Next Session
Branch is ready for owner review/PR to main; do NOT merge yourself.
The golden fixture is hand-verified and byte-locked; regenerate + hand-verify again
(e.g. after any RNG change, D-050-style) — do not auto-update it.
DEV_LAUNCH §7.6 documents the verified trace command; §6 now quotes 176 tests.
The `dotnet test --project` MSBuild response-file quirk (MSB1001) appears on this box;
run `dotnet test` from the project dir instead.

## M4 sub-task H: docs pass — DEV_LAUNCH/USER_MANUAL/REQUIREMENTS/VIVA_ANSWERS + TODO cleanup — 2026-09-14 (feat/milestone-4-event-trace)

- **DEV_LAUNCH** §6 test count 97→176 with the M4 test breakdown; new §7.6
  `trace` command section (flags, level contract, verified command + output);
  changelog row (M4; M3 row preserved beneath it); "Last verified" refreshed to
  2026-09-14 incl. the trace live-run; §8 layout note for `Trace/`.
- **USER_MANUAL** new §7.6 `trace` section (pointer to DEV_LAUNCH
  for the full flag list — no instruction duplication, §10.7); regenerating
  section renumbered to §7.7; changelog row added.
- **REQUIREMENTS** FR-VAL-4 Source/Test/Decision refreshed — first-class trace
  (Trace/ namespace, `trace` CLI) supersedes the Serilog Debug channel as the
  FR-VAL-4 implementation; tests now TraceRegressionTests + CliTraceTests +
  EventTraceTests; D-036 + D-055. Coverage summary block recomputed from stale
  50.0%/23 to actual 63.0%/29 (recon finding #3 resolved). Changelog row + note.
- **VIVA_ANSWERS** +5 Milestone-4 Q&As (two channels D-055; defending trace
  numbers via stats/RNG checks D-057/D-058; byte-stability; `--level rng` draw
  order; `--patients` early break).
- **TODO.md** M4 trace row → `[x]` (pending owner merge); reconcile finding #4:
  the rate-wise/mean-wise toggle row reclassified `[~]` → `[ ]` so exactly one
  task is IN PROGRESS at once.
- Full suite 176 green (83+58+35), 0 warnings. Branch has 5 commits pushed
  (3705830, f6b95e7, 94a2d8e, e84d50d, e40e1e3 + docs pass commit pending).

## M4 sub-tasks A–D: trace feature, golden regression, stdout purity — 2026-09-14 (feat/milestone-4-event-trace)

M4 kicked off from `main` @51d6d92 (156 green). Branch pushed; then:

- **A — Trace abstraction** (`src/OpdSimulator.Core/Trace/`, 9 files): `TraceLevel`,
  `TraceEventType`, `TraceEvent`, `ITraceSink`, `TraceFormatter`, `TraceClock`,
  `TraceRandomSource`, `TextWriterTraceSink`, `NullTraceSink`. Level filtering lives
  only in `TraceFormatter` (engine is level-blind and emits the identical event
  stream at every level); invariant culture + floor-truncated seconds keep output
  byte-stable. (D-055, D-056)
- **B — Engine instrumentation**: `_random` is now `TraceRandomSource` always
  (D-057); per-run `_traceSink`; emission at arrival/start/end/route/exit plus RNG
  rows (seed, inter-arrival, service, routing, server-pick-if-drawn); optional
  `maxCompletedPatients` early break; `EngineConfig.TraceSink`. Committed `f6b95e7`;
  **M1 regression green, full suite 156 green** — the sink is provably passive.
- **C — `trace` CLI command** (`TraceCommand.cs` + `Program.cs` registration):
  `--lambda --mu --servers --stages --p-exit --patients --seed --level --output
  --real-start`; exit 0/1/2. Live smoke verified: 5 ARRIVE / 5 START / 5 END / 5
  EXIT at state level; RNG level shows `draw#k U=…` rows; events level drops
  state columns and RNG rows. **stdout-purity fix**: "CLI run requested" demoted
  `Log.Information` → `Log.Debug`, engine is given the file-only logger — trace
  stdout now carries only trace lines (D-059).
- **D — Golden regression**: fixture `tests/OpdSimulator.Core.Tests/Fixtures/
  trace-5-patients.txt` frozen from λ=3 μ=4 c=1 seed 42 and **hand-verified
  draw-by-draw** against the reference `System.Random(42)` sequence (U values
  0.6681→0.101, 0.1409→0.653, 0.1255→0.519, 0.5228→0.216, 0.1684→0.594,
  0.2626→0.334, 0.7244→0.108, 0.5129→0.167, 0.1737→0.584, 0.7613→0.068).
  `TraceRegressionTests` (6 facts): golden byte-lock (CRLF-normalised), tamper
  detection, 5×5 event census, level-filter contract, draw-by-draw RNG parity,
  exit-count = served, trace-wait = metric (9 dp), and sink-passivity. Committed
  `94a2d8e`, pushed.
- **Full suite: 163 green (83 Core + 58 Data + 22 Cli), 0 warnings.**
- Docs: TODO rows updated (5-patient hand trace → [x], M4 trace row → [~] with
  remaining list); DECISIONS D-055..D-059 logged.
- Remaining for M4: docs pass (DEV_LAUNCH §6/§7 trace + changelog, USER_MANUAL
  trace section, REQUIREMENTS FR-VAL-4 refresh + coverage recompute, VIVA_ANSWERS
  trace Q&As), a CLI trace test, and final M4 acceptance (≥171-target noted in
  kickoff; now 163 — the M4 CLI trace tests close the gap).

## Resume — 2026-09-14 05:20 — reconciled: 6 findings

Findings (all non-blocking, fixed during M4):
1. DEAD-STATE BASELINE VERIFIED: `git status` clean on `main`, up-to-date with
   origin, HEAD = merge 51d6d92 (metric-identical wording). `dotnet build` 0
   warnings; `dotnet test` 156 green (76 Core + 58 Data + 22 Cli) — matches the
   handoff claim. M4 branch can start from this green baseline.
2. DEV_LAUNCH §6 test count is stale: still reads "As of 2026-09-13 97 tests
   pass" (M2 count); reality is 156. §6 must be refreshed during M4's docs pass.
3. REQUIREMENTS.md Coverage Summary block is stale: top block reads 23 `[x]` /
   2 `[~]` / 50.0%, but the M3 changelog row and the rows themselves say 29 `[x]`
   / 3 `[~]` / 63.0%. The summary block must be recomputed during M4.
4. TODO.md carries two `[~]` IN PROGRESS rows simultaneously — the rate-wise /
   mean-wise toggle row (its data layer is done; the M5 GUI toggle remains) and
   the event-log/trace row (M4's natural home). Reclassify the toggle row to `[ ]`
   (or leave `[~]` with M5 note) at the M4 TODO pass so one-at-a-time holds.
5. M5_UI_SPEC.md is absent — the kickoff said "if present"; nothing to reconcile.
6. FR-VAL-4 is already `[x]` in REQUIREMENTS.md backed by EventTraceTests
   (Serilog Debug events). M4's first-class trace supersedes it as the stronger
   implementation; the row's Source/Test will be refreshed rather than promoted.

## Resume — 2026-09-14 05:11 — reconciled: 6 findings

### fix/metric-identical-wording — M3 post-merge wording pass — 2026-09-14
Merge verification (against pre-merge tip `b5f6a7d`) proved the single-stage
output is **metric**-identical, not byte-for-byte: stage-name casing is
normalised to canonical form and the INF start line uses the network form
(D-054). All "byte-for-byte"/"byte-by-byte" claims tightened to
"metric-identical"/"numerically identical" across DEV_LAUNCH, DECISIONS
(D-049/050/051/052), PROGRESS, TODO, VIVA_ANSWERS and the src XML/code
comments; regression tests renamed to assert numbers, not strings:
`Run_SingleStage_ByteForByteRegression` →
`Run_M1Regression_SingleStage_GoldenValues`,
`SimulateParams_Regression_ByteForByteKnownOutput` →
`SimulateParams_Regression_M1GoldenValues`. D-054 logged. Output text is no
longer claimed as a contract.

### Session Handoff — 2026-09-13 16:30
Branch: feat/milestone-3-multi-stage-network
Status: Clean

Done
M3 sub-block A — engine generalisation (StageSpec/Stage/NetworkTopology, Run(topology,…), StageMetrics[], D-050)
M3 sub-block B — engine-level per-stage refusal listing ALL unstable stages (B3 CLI surface folded into E/F)
M3 sub-block C — ClinicCalendar + engine arrival gating (D-051)
M3 sub-block E — stage-aware simulate-data (D-052)
M3 sub-block F — simulate-network command + day-model flags + --verbose ρᵢ (D-053)
M3 sub-block G — acceptance: 156 tests, 0 warnings; M1 numerically identical; refusals; day-repeatability; c=2/3 sweep refreshed
M3 sub-block H — docs pass: DEV_LAUNCH, USER_MANUAL, REQUIREMENTS (63.0%), VIVA_ANSWERS +5 Q&As, dead-state verified

In Progress
None

What is complete: M1–M3 end-to-end. Single-stage M1 metrics intact (served 29892,
wait 0.724, ρ 0.75). M2 data layer intact. M3 network: stage-aware simulate-data,
parameter-driven simulate-network with clinic calendar (--days/--start-day/--cap),
per-stage ρᵢ + refusal listing every unstable stage, p_exit routing. 156 tests (76 Core +
58 Data + 22 Cli), 0 warnings, dead-state restore/build/test pass.

What remains: owner review + merge of feat/milestone-3-multi-stage-network into main
(AGENTS §11.5 item 6); M4 (event logging + step-by-step trace) and M5 (GUI) and M6
(token generator) not yet started.

Next Session Should Start With
Owner: review/P R the branch into main (per AGENTS §11.5); then pick the next milestone
M4 event logging & step-by-step trace (the deviation-kickoff plan had D-block run modes
absorbed into C/F, B3 into E/F — both landed).
M5 GUI (Avalonia; B-005 remains a pending owner decision).

Blocked
None (B-001..003, B-005, B-006 remain open owner/OS blockers, unrelated to M3).

Git State
Commits made this session: c5efd5c (stage-aware simulate-data), 9751986 (docs D-052),
46e2a00 (simulate-network), ae13a73 (docs D-053), ff4f565 (docs acceptance + sweep refresh),
7e0843a (sample fixture), 8dd93ea (docs H + dead-state). Prior session: 8d26fa6, babed9a,
35fefd4, 9b17f80, c476299.
Pushed to origin: Yes (feat/milestone-3-multi-stage-network).

Uncommitted changes: None.

Build & Test
dotnet build: PASS (0 warnings)
dotnet test: PASS — 156 passed, 0 failed (76 Core + 58 Data + 22 Cli) from dead-state

Warnings: 0

Files Touched
src/OpdSimulator.Core: Stages/NetworkTopology.cs (M3-A), Calendar/ClinicCalendar.cs (C), Engine/Engine.cs, SimulationResult.cs, StageMetrics.cs, Servers/* (D-050) — from prior commits this session set
src/OpdSimulator.Data: Preprocess/ClinicStageOrder.cs (new), Validation/DataValidator.cs (blank-downstream rule)
src/OpdSimulator.Cli: Commands/SimulateDataCommand.cs (stage-aware), Commands/SimulateNetworkCommand.cs (new), Commands/CliShared.cs (+TryParsePositive), Program.cs (dispatch, PrintNetworkMetrics)
tests/: DataValidatorTests (+2), CliSimulateDataNetworkTests (new), CliSimulateNetworkTests (new)
docs/: DECISIONS (D-052, D-053), PROGRESS, TODO, DEV_LAUNCH, USER_MANUAL, REQUIREMENTS; VIVA_ANSWERS.md, .gitignore, samples/sample_3stage_clinic.csv (new)

Decisions Made
D-052 — stage-aware simulate-data flows (DECISIONS.md)
D-053 — simulate-network inline flags + B3 pre-run ρᵢ (DECISIONS.md)

Assumptions Added/Changed
None new besides D-052's documented blank-downstream-cell rule (logical consequence of CONTEXT §1.2, not a new assumption).

Notes for Next Session
Branch is ready for owner review/merge to main; DO NOT merge to main yourself.
DEV_LAUNCH §7.4 c=2/c=3 numbers (0.315/0.030) reflect D-050 and are re-derivable via the documented command.
The kickoff "M3 D run modes" (single-day default, --days N, --start-day) was delivered engine-side in C and CLI-side in F (both DONE, D-051/D-053) — no work item remains.
Next natural task from TODO.md "Upcoming": M4 event trace milestone — see docs/TODO.md.

### M3 sub-block H: docs pass + dead-state check — 2026-09-13 (feat/milestone-3-multi-stage-network)
Every living doc brought current and the dead-state rule re-run:
**DEV_LAUNCH** §7.4 documents the stage-aware `simulate-data` form with the
committed `samples/sample_3stage_clinic.csv` (verified command + output), new
§7.5 for `simulate-network` (verified example, exit-code + refusal semantics),
§8 layout lists the new sample, §12 changelog row with the Ubuntu-24.04
verification stamp. **USER_MANUAL** §7.4 gains the multi-stage paragraph,
new §7.5 (simulate-network, non-technical), §7.6 regenerating samples; §12
changelog row. **REQUIREMENTS.md** M3 rows: FR-SIM-1/4/6/7/8/9 → `[x]` with
network sources + tests + D-049→D-053, FR-SIM-10 → `[~]` (real-clock binding
is M5), FR-STAT-6/7 → `[x]`, FR-VAL-1 source refreshed to
`NetworkTopology.Validate`; coverage 63.0% (29/46 `[x]` + 3 `[~]`); changelog
row added. **VIVA_ANSWERS.md** +5 M3 Q&As (per-stage ρ routing, blank-doctor
days rule, --servers contract, calendar-as-one-stream, D-050 c=1 unchanged).
**BLOCKERS** — no stale M3 entries (B-001..003/005/006 unaffected). Dead-state
check (AGENTS §10.4): `bin`/`obj` deleted everywhere, `dotnet restore` →
`dotnet build` (0 warnings) → `dotnet test` 156 green. **M3 sub-blocks A–H all
DONE.**

### M3 sub-block G: acceptance pass — 2026-09-13 (feat/milestone-3-multi-stage-network)
Every kickoff acceptance item verified, live where a live check exists:
**≥128 tests / 0 warnings** → 156 tests (76 Core + 58 Data + 22 Cli), 0 warnings.
**M1 metrics numerically identical** → live `simulate-params --lambda 3 --mu 4 --servers 1 --horizon
10000 --seed 42` prints served 29892, wait 0.724, ρ 0.75 (both EngineTests +
CliSimulateParamsTests guard it). **simulate-network per-stage** → 3 metric blocks
+ network totals; `--verbose` pre-run ρᵢ (0.4/0.4/0.1). **Unstable refusal via
CLI** → both commands exit 1 with one stderr line listing every unstable stage
(live: Reception ρ = 6.06 + Screening ρ = 4.00). **simulate-data 3-stage** →
fit λ0 + per-stage μᵢ + p_exit, 3 blocks, network totals. **Day-repeatability** →
same-seed `--days` runs reproduce identical stdout. **M2 sweep c=2/3 refreshed**
→ D-050 changed assignment to random-among-idle; c=1 path unchanged
(6.058 min); `simulate-data samples/sample_patients.csv --servers 1,2,3 --seed
42` now reports 0.82/6.058, 0.41/0.315, 0.27/0.030 — DEV_LAUNCH §7.4 updated
with the exact command used (wording marks the refresh date and cause).

### M3 sub-block F: `simulate-network` command + day-model flags + B3 pre-run ρᵢ — 2026-09-13 (feat/milestone-3-multi-stage-network)
New `Cli/Commands/SimulateNetworkCommand.cs`: the parameter-driven twin of the
fitted path. `--lambda λ₀ --c 1,2,3 --mu μ₁,μ₂,μ₃` build `StageSpec[]` (names
default to the clinic flow unless `--stages` overrides), `--p-exit` binds to
the second-to-last stage and requires ≥ 3 stages, and the D-009 day model lands
as CLI flags: `--days N` → `Engine.Run(topology, ClinicCalendar(), N, seed,
--cap)` with `--start-day` naming day 0 (D-051 block-relative weekdays);
`--horizon` stays the no-`--days` run mode and the two are mutually exclusive.
`--verbose` prints the B3 pre-run routing-derived ρᵢ per stage
(`NetworkTopology.EffectiveArrivalRate/RhoFor`, pure reads); calendar runs add
a `── Clinic day model ──` header before `PrintNetworkMetrics`. `CliShared`
gains `TryParsePositive`. Unstable refusals exit 1 with the single stderr line
listing EVERY unstable stage. Tests: 12 new facts in
`CliSimulateNetworkTests` (3-stage run, p-exit run, --verbose ρᵢ block,
5-day same-seed reproducibility, all-stages unstable refusal, 7-case
usage-error theory) — full suite **156 green** (76 Core + 58 Data + 22 Cli),
0 warnings. Live smoke: `--verbose --days 5 --cap 80` serves 124 patients with
pre-run ρᵢ (0.4/0.4/0.1), day-model header, and 3 per-stage blocks. D-053
(logging the inline-flags-vs-JSON decision). The JSON-scenario deviation from
the kickoff TODO wording is intentional and owner-visible via D-053.

### M3 sub-block E: stage-aware `simulate-data` — 2026-09-13 (feat/milestone-3-multi-stage-network)
`simulate-data` now runs the fitted 3-stage network. New `ClinicStageOrder`
(Data/Preprocess): canonical clinic flow Reception → Screening → Doctor, used
to (1) order detected stage pairs regardless of CSV column order and (2) power
the only validator relaxation that M3 data needs — a stage column may be blank
only when that stage comes strictly after the row's `departure_stage` in the
flow (a Screening exit legitimately has no doctor times). `DataValidator`
recognises this via `StageMayBeBlankFor`; all prior strictness (blank arrival,
blank earlier-stage cells, unparseable times, inverted pairs, out-of-order
arrivals) is unchanged. `SimulateDataCommand` was rewritten: λ₀ = 1/mean
inter-arrival; per-stage μᵢ = 1/mean(service) over the stage's usable rows
(refusal if a stage has none); p_exit via `PExitCalculator` with the exit stage
bound to Screening **only when Doctor exists** (else all exit at the last stage
and no p_exit — routing λ_doctor = λ₀·(1−p_exit) only applies with Doctor).
Single-stage files keep the M2 `--servers 1,2,3` sweep numerically identical (the
`Fitted from data` line and PrintMetrics metrics — output text is normalised to
canonical stage names and the network log form, D-054);
stage-aware files require exactly one `--servers` count per detected stage in
flow order (mismatch = usage exit 2) and run one network. New
`Program.PrintNetworkMetrics` emits a per-stage block (patients served, avg
wait, avg queue, stage util, per-server util, throughput, ρᵢ) plus network
totals. Unstable networks refuse with exit 1 and the message listing EVERY
unstable stage (λᵢ, cᵢ, μᵢ, ρᵢ). Tests: 2 DataValidator facts (blank doctor
allowed for Screening exit; blank reception for Doctor exit still rejected) +
3 CLI facts (`CliSimulateDataNetworkTests`: 3-stage fit+p_exit=0.7+3 metric
blocks, all-stages unstable refusal, server-count mismatch exit 2). 144 tests
green (76 Core + 58 Data + 10 Cli), 0 warnings; live smoke run verified a full
3-stage run (109 served, per-stage blocks, network totals). D-052.

### M3 sub-block C: clinic calendar + engine arrival gating — 2026-09-13 (feat/milestone-3-multi-stage-network)
`ClinicCalendar` (Core/Calendar) encodes the OPD schedule: open Mon–Thu + Sat,
window 08:15–11:00 (CONTEXT §1.1), optional daily cap, and a start-day anchor
(future `--start-day`). The time model (D-051) anchors t = 0 at the day-0 window
so all times are ≥ 0: a day block is 24 h from the anchor, weekday =
`(startDay + block) mod 7`, admission window = first 165 minutes of an open
block. Arrivals are ONE continuous Poisson stream that the engine gates at fire
time — the demand exists, the calendar is the admission gate — so no per-day
draw resets and no first-arrival-after-the-weekend special case. New `Engine.Run
(topology, calendar, generatorDays, seed, dailyCap)` (+`CalendarGate` nested
class) shares `RunCore` with the horizon path; the M1 loop is
untouched (calendar == null branch). Operating time per open day = first admitted
arrival → last service end, summed (D-018, not diluted by nights/weekends);
services drain past 11:00 (FR-SIM-6). 22 new tests (11 ClinicCalendar + 7 engine
facts + theory rows): defaults, window boundary (165 exclusive), Fri/Sun admit
zero, start-day-Friday shift, cap binds 4/day × 5 open days and resets daily,
same-seed reproducibility incl. AdmittedPerDay, drain-into-the-evening with
μ = 0.1/ρ = 0.5, arg guards. 139 tests green (76 Core + 56 Data + 7 Cli),
0 warnings. D-051.

### M3 sub-block B: per-stage ρ refusal at engine level — 2026-09-13 (feat/milestone-3-multi-stage-network)
B1/B2 landed inside sub-block A (NetworkTopology uses the D-007 product rule for
λᵢ = λ₀·Π(1−p_exit), and Validate() throws UnstableSystemException listing every
offender with λᵢ, cᵢ, μᵢ, ρᵢ). This letter proved the full engine path (FR-VAL-1)
with three new tests: doctor-only-unstable (ρ = 1.20, refusal carries the
routing-derived λ = 1.800 and does NOT mention the stable stages), two-stages-
unstable (both Reception 3.00 and Screening 1.50 listed with their cᵢ), and an
all-stable network whose StageMetrics report ρ = 0.30 / 0.375 / 0.375 per stage
(FR-STAT-6 engine groundwork). B3 (CLI `--verbose` per-stage ρᵢ before the run)
is genuinely a network-command feature, so it is folded into E/F rather than
half-built against the still-single-stage CLI. 117 tests green (54 Core + 7 Cli
+ 56 Data), 0 warnings. Milestone-3 sub-block plan persisted into TODO.md so the
kickoff survives sessions.

### M3 sub-block A: engine N-stage generalisation + server-assignment latent-bug fix — 2026-09-13 (feat/milestone-3-multi-stage-network)
First commit on the branch cleared the stale M2 `[~]` TODO row (`8d26fa6`,
message per kickoff). Then the engine became network-capable without a rewrite:
new `Core/Stages/{StageSpec,Stage,NetworkTopology}` and `Engine.Run(NetworkTopology,
seed, horizon)`; `SimulationResult.StageMetrics[]`; `Patient.SystemArrivalTime` +
`AdvanceToStage`; `UnstableSystemException` now lists ALL unstable stages
(`UnstableStage` payload). Routing uses the existing EventType mapping (stage i →
completion event i+1, D-006), and draws a uniform against p_exit only at the exit
stage (FR-SIM-3). Latent M1 bug fixed (D-017 vs actual FirstOrDefault code):
idle assignment is now `IServerSelectionPolicy` — `RandomIdleSelection` (default;
no draw when a single server is idle → single-server stream untouched) and test-only
`LowestIdSelection`. M1 golden values guarded two ways: Core test
(`Run_M1Regression_SingleStage_GoldenValues`) and a CLI test asserting the exact
simulate-params output lines (served 29892, wait 0.724, ρ 0.75). Balance regression
uses a low-load 2-server stage (λ=2, μ=4, c=2): random-diff < 0.10, lowest-ID-diff
≥ 0.10. Test infra fix: CLI tests share the static Serilog global, so the assembly
is now `CollectionBehavior(DisableTestParallelization)` (was a latent race — new
second CLI class tripped it). 114 tests green (51 Core + 7 Cli + 56 Data),
0 warnings. Decisions D-049 (NetworkTopology generalisation), D-050 (server fix).

### M2.5 Sample data precision fix — 2026-09-13 (fix/sample-data-precision)
Owner's smoke test found chi-square rejecting the sample's exponential fit at p ≈ 0
for inter-arrival AND service. Root cause: the sample stored whole minutes
(`H:mm`), collapsing continuous exponential draws to ties — the empirical
distribution looked discrete. Fix (D-048): generator now writes `H:mm:ss`
(quantised to 1/60 min); RNG stream untouched (same seed-42 draws), so the
underlying samples are identical. TimeParser already accepted `H:mm:ss`; a new
`TimeParserTests` case (`8:17:30` → 497.5) pins it — HH:MM cases unchanged.
Samples + dirty fixture regenerated. `fit` now reports p = 0.103 / 0.258, both
Fail-to-reject; sweep numbers refreshed (ρ 0.82/0.41/0.27; μ̂ 0.683 from the now
untruncated service mean 1.464). 98 tests green. VIVA line on rounding vs
chi-square power added; DEV_LAUNCH §7.4 verified block updated.

### Session Handoff — 2026-09-13 10:12
Branch: feat/milestone-2-data-and-fitting
Status: Clean (3 commits, not yet pushed; awaiting owner review — do not merge)

Done
- [x] M2 sub-blocks A–F: Data layer project (loaders, DataValidator, preprocessing, 5 fitters, chi-square, parameter modes, exporter; 52 Data tests green)
- [x] M2 sub-block G: CLI subcommand dispatcher (`simulate-params`, `verify`, `fit`, `simulate-data`, `export`; 5 new CLI tests green)
- [x] M2 sub-block H: `scripts/sample-data-generator` + `make-sample-data.sh`; committed `samples/sample_patients.xlsx`/`.csv` + dirty fixture; `.gitignore` negation
- [x] M2 sub-block I (Ubuntu half): dead-state Release build (0 warnings) + full suite (97) + live command verification (verify exit 0/1, fit + fit JSON, simulate-data sweep 0.81/0.41/0.27, export) + M1 regression (ρ 0.75, wait 0.724)
- [x] M2 sub-block J (docs): DEV_LAUNCH §5/§6/§7/§8/§10/changelog; USER_MANUAL §7 (Loading Real Data) + renumber; REQUIREMENTS 50.0% (23/46); DECISIONS D-045..D-047; VIVA_ANSWERS 6 entries; BLOCKERS B-004 → Resolved; TODO rows

In Progress
- (none — final `dotnet build`/`dotnet test` re-check before the docs commit is the last step of J)

What is complete:
Milestone-2 data path end-to-end on `feat/milestone-2-data-and-fitting`. Data: `DataLoaderFactory`/`ExcelLoader` (ClosedXML)/`CsvLoader` (CsvHelper)/`DataSet`; `TimeParser`; `StagePairDetector` (N-stage generic); `InterArrivalCalculator`; `ServiceTimeCalculator`; `PExitCalculator` (Reception excluded); `DataValidator` (per-row issues, strict Screening/Doctor — D-038); 5 fitters + factory (MLE; gamma MoM D-041; σ denom n D-043); `BinSelector` k=⌈√n⌉∈[5,20]; `ChiSquareTest` (equal-probability bins D-040, fail-loud guards D-044, CDF delegates D-042); `ModeValidator` soft warnings; `DataExporter`. CLI: dispatcher on args[0] (D-046); `fit` writes `logs/fit-*.json`; `simulate-data` sweeps servers, per-count refusal, exit 0 if any ran. Samples: deterministic seed-42 generator (D-047) → 60-row sample (Exp λ=0.5 / Exp μ=0.666…; all exit Screening ⇒ p_exit 1.0) + 1-defect-per-row dirty fixture; B-004 Resolved. Tests: 97 green (36 Core + 6 CLI + 55 Data), 0 warnings. Commits: 99cf03b (data layer), 3490cff (decisions D-038..D-044), 148d624 (CLI rework), 52b5ddf (generator + samples + fixtures). Push pending owner review.

What remains:
Owner review + merge of this branch into `main`. Milestone-3 (multi-stage network with p_exit routing, clinic calendar). Linux half of sub-block I is verified; Windows dead-state (B-006) remains gated. `scripts/verify-traceability.sh` still to be created.

Next Session Should Start With
- Await owner merge; then M3 (engine multi-stage wiring + routing from PExit, per-stage ρ refusal FR-VAL-1/FR-STAT-6, clinic hours)
- `scripts/verify-traceability.sh` orphan-check script
Blocked
- None active (B-005 Avalonia template, B-006 Windows dead-state are pending, not blocking)

Git State
Commits made this session: 99cf03b (feat: data project), 3490cff (docs: M2 decisions D-038..D-044), 148d624 (feat: CLI subcommands), 52b5ddf (feat: deterministic sample data + dirty fixture). Docs-pass commit follows this handoff.
Pushed to origin: No (branch created from main e722f04; push blocked until owner review — per §13 rules, awaiting approval)
Uncommitted changes: this PROGRESS handoff + the rest of the docs pass (DEV_LAUNCH/USER_MANUAL/REQUIREMENTS/DECISIONS/VIVA_ANSWERS/BLOCKERS/TODO edits) — to be committed as one `docs:` commit

Build & Test
dotnet build: PASS (0 warnings, Release)
dotnet test: PASS — 97 passed, 0 failed (36 Core + 6 Cli + 55 Data)
Warnings: 0

Files Touched
src/OpdSimulator.Data/...: added (Loaders, Preprocess, Validation, Fitting, Parameters, Export, csproj + MathNet 5.0.0)
src/OpdSimulator.Cli/...: added Commands/*; modified Program.cs, csproj (Data ref), Loggers
tests/OpdSimulator.Data.Tests/...: 7 test files + FixtureTests + Fixtures/dirty_missing.xlsx (55 tests)
tests/OpdSimulator.Cli.Tests/...: CliDataCommandTests (5); CliRefusalTests updated
samples/: sample_patients.xlsx + .csv (committed)
scripts/: sample-data-generator/ + make-sample-data.sh
docs/...: modified DEV_LAUNCH, USER_MANUAL, REQUIREMENTS, DECISIONS, VIVA_ANSWERS, BLOCKERS, TODO, PROGRESS
.gitignore: negates the sample CSV

Decisions Made
- D-045 Sample CSV committed via .gitignore negation (samples/sample_patients.xlsx|.csv)
- D-046 CLI subcommand dispatcher (simulate-params/verify/fit/simulate-data/export; server sweep)
- D-047 Sample data + dirty fixture generated deterministically (seed 42), not hand-written
Assumptions Added/Changed
- B-004 (waiting for real sample) → Resolved: project proceeds with the generated stand-in; real clinic data remains a drop-in at the same path (CONTEXT §5.5 format unchanged).
Notes for Next Session
- The unfitched docs commit must include everything in the handoff. Do not force-push; do not merge; report for owner review.
- `samples/sample_patients.csv` verified λ-hat 0.562 (mean 1.78 min) — sampling variation of seed 42, explained in VIVA_ANSWERS.

## Resume — 2026-09-13 09:37 — reconciled: 3 findings

1. **fix/cli-clean-refusal merged into `main`** (`e722f04 merge: clean CLI refusal output`): the PROGRESS top entry still says "awaiting owner review; do not merge". Reality wins — the clean-refusal fix (D-037) is on main. Next M2 branch must start from this main.
2. **B-004 (real sample file) resolved by owner instruction:** the M2 kickoff orders programmatic sample generation (samples/sample_patients.xlsx + .csv via scripts/make-sample-data, plus a dirty fixture), superseding D-014 "wait for the owner's real sample". B-004 to be moved to Resolved when M2 starts.
3. **DEV_LAUNCH §7 "Planned (M2): --input/--days" is stale:** the M2 kickoff specifies subcommands (simulate-params, verify, fit, simulate-data, export), not `--input/--days`. §7 must be rewritten during M2 (J1).
4. **Test-count drift:** kickoff I1 expects "34 + ≥20 = ≥54"; current main has **37** tests (M1 + Core.Tests 36 + Cli.Tests 1 after the clean-refusal merge), so the M2 target should read **37 + ≥20 = ≥57**.

### Pre-M2 Micro-Fix — 2026-09-13 09:30 (fix/cli-clean-refusal)
Owner requested a clean CLI refusal for unstable configurations (pre-M2 fix) after M1 was merged to
main as `5720772`. Completed: `Program.cs` converted from top-level statements to a class-based
`Program` exposing public `static int Run(args, stdout, stderr, fileLogger)` for in-process testing;
the `UnstableSystemException` catch now writes exactly one line `Refusing to run: {ex.Message}` to
stderr with **no stack trace** and logs the full exception to the file logs only via a file-only
Serilog logger (`shared: true` writers — D-037). `UnstableSystemException.Message` no longer repeats
the "Refusing to run: " prefix (removed to avoid "Refusing to run: Refusing to run: …"). New project
`tests/OpdSimulator.Cli.Tests` (added to sln) with `CliRefusalTests` asserting exit 1, clean stderr
(no `at OpdSimulator`, no exception type), empty stdout, and that the detail logger captured the
exception while the console-level logger captured none. Docs: DEV_LAUNCH §5 F3 example + §6 (37 tests)
+ §8 layout + changelog; DECISIONS D-037; TODO row. Verified: `dotnet build` 0 warnings; 37/37 tests
green; F3 runs show a single clean stderr line with exit 1 while `logs/errors-*.log` retains the full
stack trace; F2 (ρ=0.75) unchanged. Committed as `5b0d4c4` and pushed to `origin/fix/cli-clean-refusal`
— awaiting owner review; do not merge.

### Session Handoff — 2026-09-13 09:15
Branch: feat/milestone-1-single-stage-engine
Status: Clean (local branch, 4 commits, not pushed — awaiting owner approval per §13/§14 wrap-up rules)

Done
- [x] Implement Event, Queue, Server, Patient classes (M1: A1–A6)
- [x] Implement FEL (priority queue) and generic N-stage DES engine — validated against analytical M/M/1
- [x] Write unit tests for engine (E1–E8, 34 facts)
- [x] Add headless CLI mode to `OpdSimulator.Cli` (`--lambda/--mu/--servers/--horizon/--seed`)
- [x] FIX: wait/system accumulation `long`→`double` (E7 exposed 0.41 vs analytical 0.75; now 0.724)

In Progress
- Implement event log and step-by-step trace (viva trace file) — the engine's Debug event trace is
  live and FR-VAL-4 is `[x]` (review fix, EventTraceTests); the human-readable trace file +
  5-patient hand trace remain from this row.

What is complete:
Milestone 1 end-to-end on `feat/milestone-1-single-stage-engine` (commits 0a87fa0, fef6d01, 30b7771,
this docs commit). Core: `Patient`, `EventType`/`Event`/`FEL` (time→type→id total order), FIFO
`Queue`, `Server`, `IRandomSource`/`SeededRandomSource` (default seed 42), `ExponentialSampler`
(inverse CDF), `EngineConfig` (ρ = λ/(c·μ), refuses ρ ≥ 1), `UnstableSystemException`,
`SimulationResult`, and the generic single-stage `Engine` (arrival gating at horizon, services
draining past it, Debug event trace). CLI: full arg parsing + Serilog three-sink logging, metrics
table + ρ, exit codes 0/1/2. Tests: 34/34 green including same-seed determinism, M/M/1
average-wait 15% bound, and stability refusal; dead-state restore/build/test re-verified in
Release with 0 warnings. F2 (λ=3, μ=4): ρ=0.75, wait 0.724. F3 (λ=5, μ=4): refused, ρ=1.25,
exit 1. Docs updated: TODO, DECISIONS (D-033..D-036), REQUIREMENTS (10 rows `[x]` incl. FR-VAL-4,
coverage 21.7%), DEV_LAUNCH (§3/§5/§6/§7 + changelog), USER_MANUAL (§3 CLI path),
VIVA_ANSWERS (M1 entries). Decisions D-033..D-036 logged. Review fix: EventTraceTests added and
FR-VAL-4 promoted to `[x]` (36/36 tests green).

What remains:
Owner review + merge of this branch into `main`; then M2 (data loading & distribution fitting) —
loader, MLE Exponential fit, chi-square, sample `.xlsx`, per-stage ρ refusal extension, and the
data-driven CLI `--input/--days`.

Next Session Should Start With
Milestone 2 kickoff (reconcile → implement Data loader + fitting + chi-square + validation_report).
Second item: record the 5-patient hand trace (viva) using the engine's Debug event output.
Blocked
None new — BLOCKERS.md unchanged (B-001..B-006 all active, none touch this branch).

Git State
Commits made this session:
- 0a87fa0 feat: add Milestone 1 core engine (Patient/Event/FEL/Queue/Server/RNG/Engine)
- fef6d01 feat: add Milestone 1 headless CLI (--lambda/--mu/--servers/--horizon/--seed)
- 30b7771 test: add E1-E8 unit tests for queue, FEL, RNG, server, engine, stability
- 581c6b1 docs: update TODO/DECISIONS/REQUIREMENTS/DEV_LAUNCH/USER_MANUAL/VIVA_ANSWERS/PROGRESS for M1
Pushed to origin: No — per AGENTS §11.4/§13, the branch waits for owner review before push.

Uncommitted changes: None (checked after this commit)

Build & Test
dotnet build: PASS (Release, dead-state, 0 warnings)
dotnet test: PASS — 34 passed, 0 failed (OpdSimulator.Core.Tests; Data.Tests is an empty placeholder)
Warnings: 0

Files Touched
src/OpdSimulator.Core/Patients, Events, Queues, Servers, Distributions, Engine: added (14 files)
src/OpdSimulator.Core/OpdSimulator.Core.csproj: modified (Serilog 4.4.0)
src/OpdSimulator.Cli/Program.cs + .csproj: modified (CLI, Serilog.Sinks.File 7.0.0, ref → Core)
tests/OpdSimulator.Core.Tests/: added 8 test files + ProjectReference to Core
docs/: TODO, DECISIONS, REQUIREMENTS, DEV_LAUNCH, USER_MANUAL, PROGRESS modified; VIVA_ANSWERS at repo root modified

Decisions Made
D-033 — FEL ordering: time → event type → patient id (DECISIONS.md)
D-034 — UnstableSystemException refuses ρ ≥ 1 (FR-VAL-1)
D-035 — Deterministic RNG: System.Random wrapper + inverse-CDF exponential
D-036 — Engine event trace at Serilog Debug; console Info, file Debug, error file Warning+

Assumptions Added/Changed
None new — existing CONTEXT assumptions unchanged; no `[UNVERIFIED]` created this session.

Notes for Next Session
- Branch pushed to origin on owner approval; owner review in progress — do NOT start M2 until merged.
- FR-VAL-4 promoted to `[x]` on review (EventTraceTests added); the human-readable trace file + 5-patient
  hand trace remain tracked in TODO (not FR-VAL-4's PRD scope).
- FR-SIM-1 marked `[x]` with the interpretation noted in its REQUIREMENTS row (generic engine
  in place; 3-stage config is M3) — flagged pre-go in the resume line, owner approved.
- FR-SIM-5 `[x]` covers the M1 arrival-generation gate; the true clinic calendar
  (8:15–11:00, closed Fri/Sun) is M3/M4 work.
- `OpdSimulator.Data.Tests` is an empty placeholder — restore that project's content in M2.
- DEV_LAUNCH "Last verified" refreshed; Windows half still TBD (B-006).

## Resume — 2026-09-13 08:40 — reconciled: 3 findings

Findings (all non-blocking for M1):
1. **PROGRESS merge-status lag:** the top handoff still reads "awaiting review — do NOT merge without approval", but the docs branch was merged into `main` (d02da96). Reality wins; will refresh the CURRENT STATE line during M1 wrap-up.
2. **AGENTS §9.8 cited but absent:** the M1 kickoff references "Decision ID hygiene: AGENTS.md §9.8", but the file's §9 ends at §9.7. The rule ("scan for next free ID, never assume the last visible number") will be followed; codifying it in AGENTS.md is a proposed follow-up for owner approval.
3. **FR-SIM-1 marking needs an explicit reading:** kickoff G2 says mark FR-SIM-1 `[x]`, but FR-SIM-1's literal text is "3-stage serial network (defaults 1/2/3)" — the 3-stage *configuration* is not built until M3. PRD v1.3.0 FR-SIM-1 itself carries the clause "engine is N-stage generic from day one — the 3-stage network is a configuration, not a hard-coded structure", and D-006 commits to generic-first. Plan: build the engine as an N-stage-generic serial pipeline exercised as M/M/1 in M1, mark FR-SIM-1 `[x]` with a row note "generic engine in place; 3-stage config in M3", and log the interpretation as a decision. Flagged here so the owner can override at the "go".

### Session Handoff — 2026-09-13 08:30
Branch: docs/reconcile-decisions-and-paths
Status: Clean (pushed to origin, awaiting review — do NOT merge without approval)

Done
- [x] FIX: renumber duplicate decision IDs (D-027b→D-029, D-028b→D-030, D-029→D-031) + update references
- [x] FIX: align DEV_LAUNCH §3 + D-021/D-027 with template-only rule
- [x] FIX: fill PROGRESS.md gap for commit 9537d46
- [x] Initial bootstrap root commit task marked DONE (was stale `[ ]` in TODO despite 73787bb pushed)

In Progress
None

What is complete:
All three reconciliation drifts fixed and committed: duplicate decision IDs renumbered with every cross-reference updated and re-grepped to zero stale IDs; DEV_LAUNCH §3 + D-021/D-027 corrected to the actual template-only gitignore rule (verified via git check-ignore); PROGRESS.md gap for commit 9537d46 filled plus the §14 resume line and D-032 process-fix decision logged.

What remains:
Owner review and merge of the branch into main; then Milestone 1 (Event/Queue/Server/Patient + FEL + M/M/1 engine) on feat/milestone-1-single-stage-engine.

Next Session Should Start With
Milestone 1 kickoff (reconcile → branch feat/milestone-1-single-stage-engine → implement A1..A8 per kickoff prompt) — drifts now cleared.
Second item: none until M1 done.

Blocked
None new — BLOCKERS.md unchanged (B-001..B-006 all active, none touch this branch).

Git State
Commits made this session:
- 27a1a34 docs: renumber duplicate decision IDs and update references
- dee90c2 docs: align DEV_LAUNCH §3 and D-021/D-027 with template-only rule
- 2fecab9 docs: fill PROGRESS.md gap for commit 9537d46

Pushed to origin: Yes (docs/reconcile-decisions-and-paths → origin; PR creation intentionally skipped — owner opens it)

Uncommitted changes: none at wrap time (TODO.md Done-row additions committed below as 2fecab9)

Build & Test
dotnet build: PASS (0 warnings, 0 errors, Debug)
dotnet test: PASS (exit 0 — no tests yet; expected at scaffold, M1 adds them)

Warnings: 0

Files Touched
docs/DECISIONS.md: modified (renumber + D-021 note + D-032 entry)
docs/DEV_LAUNCH.md: modified (§3 wording, changelog D-030)
docs/PROGRESS.md: modified (resume line, gap entry, reference fixes, this handoff)
docs/TODO.md: modified (root-commit item → Done with D-028, three FIX items → Done)
AGENTS.md: read-only (its D-028 reference needed no change — verified by grep)

Decisions Made
D-032 — Reconciliation fixes gated before M1 (decision IDs renumbered, appsettings doc drift corrected, PROGRESS gap filled; rationale: ID collisions corrupt traceability)

ID renumber mapping (D-027b→D-029, D-028b→D-030, D-029→D-031); canonical D-028 = root commit unchanged.

Assumptions Added/Changed
None — no new assumptions; CONTEXT.md untouched.

Notes for Next Session
- Reference sweep verified: every D-ID cited resolves to exactly one DECISIONS.md entry (D-001..D-032 sequential, zero stale refs).
- git check-ignore verified: appsettings.json → `.gitignore:44 appsettings*.json`; appsettings.template.json → nothing (exit 1).
- DEV_LAUNCH §3 "Last verified" still reads 2026-09-13 (Ubuntu 24.04) — unchanged by this docs-only branch; restore+build re-run green.

## Resume — 2026-09-13 08:22 — reconciled: 3 findings

Findings (all pre-existing, none blocking M1):
1. **Git history vs PROGRESS lag:** `git log` shows 2 commits on main — `73787bb` (root scaffold) and `9537d46` ("docs: point DEV_LAUNCH/USER_MANUAL references at docs/; add B-006 blocker", 2026-09-13 08:16). PROGRESS.md CURRENT STATE + last handoff reference only `73787bb`. No orphaned `[~]` tasks; `9537d46` is consistent with D-028/D-031/B-006 already logged.
2. **Gitignore/doc drift:** `.gitignore` line 44–45 ignores `appsettings*.json` then negates `!appsettings.template.json` only — the `!appsettings.json` base-config negation described in DEV_LAUNCH §3 and D-021 is NOT present (reality matches D-027, which is the later decision). DEV_LAUNCH §3's "appsettings.json is committed" parenthetical is inaccurate.
3. **DECISIONS.md duplicate IDs:** D-027 appears twice (template negation), D-028 twice (second entry is actually "Single Canonical Location for Docs Instructions"), and "D-029 Root commit goes directly on main" duplicates the first D-028's content. Needs renumber/cleanup.

State summary follows below; awaiting owner "go".

## 2026-09-13 — Docs Path Fix + Reconciliation
- Commit 9537d46: DEV_LAUNCH/USER_MANUAL references repointed at docs/; B-006 blocker added.
- Reconciliation (§14) run: 3 pre-existing drifts found.
- Fixed under this branch: duplicate decision IDs, appsettings doc drift, PROGRESS.md gap.

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
## M2 Data layer — 2026-09-13 (feat/milestone-2-data-and-fitting)
Completed sub-blocks A–F on the Data project (no engine changes). New files:
Loaders/ (DataSet, IDataSource, ExcelLoader, CsvLoader, DataLoaderFactory),
Preprocess/ (TimeParser, StagePairDetector, StagePair, InterArrivalCalculator,
ServiceTimeCalculator, PExitCalculator, PExitResult), Validation/ (DataValidator,
DataValidationException, ValidationIssue), Fitting/ (IDistributionFitter +
5 fitter classes + factory, FittedDistribution, BinSelector, ChiSquareTest,
ChiSquareResult), Parameters/ (ParameterMode, ModeValidator), Export/DataExporter.
Added MathNet.Numerics 5.0.0 to Data; ProjectReference from Data.Tests.
52 new tests (loader, time-parser, validator, preprocess/export, fitting ×5 families,
chi-square, mode-validator) — all green; core 37 tests still green.
Fit `Distribution` holds CDF/InverseCDF delegates (MathNet's IContinuousDistribution
exposes no CDF — captured from the concrete type at construction).
Surfaced conflict: validator strictly rejects departure_stage ∉ {Screening, Doctor}
(incl. Reception) per kickoff B1 — stricter than CONTEXT §5.4 warn-and-exclude.
Logged as D-038 in DECISIONS.md (which behaviour the CLI must have is settled:
strict).
