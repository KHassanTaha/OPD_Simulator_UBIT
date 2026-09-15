# TODO.md

Last updated: 2026-09-14

> **Completion rule:** A task that adds/removes a feature is not `[x] DONE`
> until `DEV_LAUNCH.md` reflects the change (if build/launch affected) and
> `USER_MANUAL.md` reflects the change (if UI/workflow affected) — see
> `AGENTS.md` §9.2. A milestone is additionally not `[x] DONE` until
> `DEV_LAUNCH.md` is verified from a dead state (`AGENTS.md` §10.4).

> Requirements are not `[x] DONE` until their row in REQUIREMENTS.md is
> updated with Source + Test.

## Active

- [x] Capture M5 UI/UX requirements batch — 2026-09-14 (docs-only on fix/ui-requirements-capture: PRD v1.4.0 FR-UI-5..21 + NFR-7..10; AGENTS §16–17; DECISIONS D-060..D-076; TODO M5 block; VIVA_ANSWERS M5-1..15 + glossary; M5_UI_SPEC.md; REQUIREMENTS 21 new rows all `[ ]`, coverage 43.3%)
- [x] M3 sub-block C: `ClinicCalendar` (Mon–Thu + Sat, 08:15–11:00 window, DailyCap, start-day anchor) + engine calendar gating (`Run(topology, calendar, days, cap)`, continuous Poisson stream with fire-time gate, per-open-day operating time D-018, drain past 11:00 FR-SIM-6) + real-clock formatter — 2026-09-13 (D-051; ClinicCalendarTests + 7 Run_Calendar_* engine tests; 139 tests green, 0 warnings; M1 metrics intact)
- [x] M3 sub-block B: per-stage ρᵢ refusal lists ALL unstable stages (λᵢ, cᵢ, μᵢ, ρᵢ) at engine level — 2026-09-13 (Run_Network_DoctorOnlyUnstable_ListsStageRho / MultipleStagesUnstable_ListsEveryStage / AllStable_RunsAndReportsRhoPerStage; 117 tests green, 0 warnings; the CLI `--verbose` pre-run ρᵢ print — B3 — is folded into E/F where the network commands land)
- [x] M3 sub-block A: engine N-stage generalisation (StageSpec/Stage/NetworkTopology, `Run(topology, seed, horizon)`, StageMetrics, p_exit routing via existing EventType mapping) + latent server-assignment fix (random-among-idle, D-050) + M1 metric regression (served 29892 / wait 0.724 / ρ 0.75) — 2026-09-13 (D-049/D-050; 114 tests green, 0 warnings; on feat/milestone-3-multi-stage-network)
- [x] Pre-M2 fix: CLI refusal prints one clean stderr line; stack trace to file logs only — 2026-09-13 (Program.cs refactored to testable `Program.Run`; `UnstableSystemException` prefix removed; tests/OpdSimulator.Cli.Tests added; DEV_LAUNCH §5/§6/§8 + DECISIONS D-037 updated; 37 tests green; committed 5b0d4c4 and pushed to origin/fix/cli-clean-refusal, awaiting owner merge)
- [x] M2 sub-block A–F: Data layer project (loaders, DataValidator, preprocessing, 5 fitters, chi-square, parameter modes, exporter) — 2026-09-13 (52 Data tests green on feat/milestone-2-data-and-fitting; commits 99cf03b, 3490cff; FR-DATA-1..9 all tested via LoaderTests/ValidatorTests/FitterTests/ChiSquareTests/ModeValidatorTests)
- [x] M2 sub-block G: CLI subcommand dispatcher (`simulate-params`, `verify`, `fit`, `simulate-data`, `export`) — 2026-09-13 (5 CliDataCommandTests green; CliRefusalTests retargeted; commit 148d624; FR-STAT-1/3 CLI path; D-046)
- [x] M2 sub-block H: sample data + dirty fixture via `scripts/sample-data-generator` + `make-sample-data.sh`; `.gitignore` negates the sample CSV — 2026-09-13 (commit 52b5ddf; 3 FixtureTests green; D-045, D-047; B-004 Resolved)
- [x] M2 sub-block I (Ubuntu half): dead-state build + full suite + live `verify`/`fit`/`simulate-data`/`export` and M1 `simulate-params` regression — 2026-09-13 (97 tests green, 0 warnings; ρ 0.75 / wait 0.724; sweep 0.81/0.41/0.27)
- [x] M2 sub-block J: docs pass (DEV_LAUNCH §5/§6/§7/§8/§10/changelog; USER_MANUAL §7; REQUIREMENTS 50% coverage; DECISIONS D-045..D-047; VIVA_ANSWERS; PROGRESS; BLOCKERS B-004 Resolved; this TODO) — 2026-09-13 (commit 384c2be; M2 branch pushed and later merged into main via 01ea9a7/b5f6a7d)
- [x] M2.5 fix: sample data at HH:MM:SS precision (chi-square rejected a true exponential at p ≈ 0 due to minute-rounded storage) — 2026-09-13 (generator format only, RNG stream unchanged; TimeParser `H:mm:ss` test added — HH:MM untouched; samples + fixture regenerated; `fit` accepts p = 0.103 / 0.258; D-048; VIVA line; 98 tests green — on fix/sample-data-precision)
## GUI REBUILD — (feat/gui-rebuild, owner mandate 2026-09-15)

> Replaces the M5 view layer (justification: `docs/M5_FAILURES.md`). Core/Data/Cli
> and their tests are frozen. M6 chart/token work stays on
> `feat/milestone-6-charts-and-token`. Phases are gated: STOP + owner "go" at
> each phase boundary. §18 manual-verification entry + screenshot required for
> every phase gate.

- [x] **Phase 1 — Foundation** — 2026-09-15 (§18 entry in PROGRESS.md; screenshot `logs/screenshots/phase-1-window.png`; gates: Release build 0/0, full suite 185 green, real Linux launch maximized >10 s, 0 crash-log entries; old view layer deleted, M6 files untouched; Theme.axaml token contract + Motion.axaml + empty Maximized MainWindow + §12.1 3-sink Serilog + §12.3 CrashReporter in Services/; packages: App −LiveCharts2/−Serilog.Extensions.Logging, App.Tests +Avalonia.Headless(.XUnit))
- [ ] Phase 2 — Reusable controls (_gate: Release build 0/0 ✅, full suite 197 green ✅, per-control headless tests 15 ✅, screenshot `logs/screenshots/controls-demo.png` ✅ — REAL-DISPLAY keyboard walk of the 9 controls (§16.8) is **owner-required**; Wayland box cannot run a real display → headless evidence accepted per D-089, manual walk pending_)
- [x] **Phase 3 — MainWindow shell** — 2026-09-16 (header bar + TabControl [Simulation | Input Analysis | Token Generator | Help]; Simulation tab = 380px config / GridSplitter / fill results; min 1100×700; PlaceholderContent reusable card; tab-first focus cycle verified headless via arrow-key selection; §18 entry in PROGRESS.md; screenshot `logs/screenshots/phase-3-shell.png`; gates: Release build 0/0, full suite 203 green, real Linux launch 15 s alive 0 crash-log entries)
- [ ] Phase 4 — ConfigPanel (Data/Model/Parameters/Stages 1–5/Horizon/Advanced + pinned footer; p_exit [0,1) only for 2+ stages; single/multi-day via calendar path)
- [ ] Phase 5 — ResultsPanel + run flow (welcome card FR-UI-5, widget selector, refused-run ErrorBanner with exact Core message; G3/G4 re-implemented)
- [ ] Phase 6 — Help tab (Markdig) + preset system (schemaVersion JSON, shipped Demo-3stage, no auto-restore)
- [ ] Phase 7 — Acceptance: AGENTS §16.8 keyboard-only walk + FR-UI-5..21 manual pass (B-007 supersedes)
- [ ] Phase 8 — Docs: DEV_LAUNCH fully re-verified dead-state, USER_MANUAL rewrite, REQUIREMENTS re-derived, VIVA_ANSWERS, DECISIONS

## Upcoming

## M5 — GUI (see PRD §5.1, AGENTS §16–17)

### Assets & Foundation (M5-A)
- [x] Asset inventory: receive official UoK green logo + UBIT CS logo — 2026-09-14 (PNG copies from ~/Downloads landed as `Assets/uok-logo.png` 1080×1080 and `Assets/ubit-cs-logo.png` 369×293; D-077)
- [x] Create `CourseInfo.cs` constants (names, course, professor, logo paths) — 2026-09-14 (6 members; CS-577; Dr. Shaista Rais; `avares://` paths via CourseInfo, no XAML references)
- [x] Create `Theme.axaml` resource dictionary (all colours, fonts, spacing) — 2026-09-14 (single hex source; brushes/typography/spacing/radii/durations; merged into App.axaml)
- [x] Set up App project for MVVM (CommunityToolkit.Mvvm or equivalent) — 2026-09-14 (hand-built shell D-078: Program.cs, App.axaml/.cs, ViewLocator, ViewModelBase, MainViewModel, MainWindow; B-005 Resolved; WinExe + compiled bindings; Core/Data refs)
- [x] Install global exception handlers in the App project (AppDomain, UnobservedTaskException, Avalonia Dispatcher) → `logs/crash-*.log` + dialog (AGENTS §12.3, 12.5) — 2026-09-14 (Logging/CrashReporter.cs; CrashReporter.Report appends crash log + themed dialog; CrashReporterTests pending)

### Reusable Controls (build once, use everywhere)
- [x] `SearchableDropdown.axaml` (FR-UI-6) — 2026-09-14 (type-to-filter + `SearchFilter` pure ranking, clear ×, chevron toggle, arrow/Enter/Escape keys; pure-logic tests in App.Tests)
- [x] `ThemedToast.axaml` (FR-UI-10) — 2026-09-14 (single card, success/error/info classes + glyphs, `Dismissed` event; `ToastItem`/`ToastService`/`ToastLifecycle` — expiry tested)
- [x] `ThemedDialog.axaml` (FR-UI-10) — 2026-09-14 (Escape=cancel, Enter=confirm, focus restored to opener, accent/error variants)
- [x] `CollapsibleSection.axaml` (FR-UI-12) — 2026-09-14 (ContentControl + type-keyed ControlTheme in ControlStyles.axaml, chevron E70D/E70E, `IsExpanded`/`SessionKey`)
- [x] `InfoIcon.axaml` (FR-UI-8) — 2026-09-14 (hover tooltip + `HelpAnchor`/`HelpRequested` click)
- [x] `PinnedFooterBar.axaml` (FR-UI-11) — 2026-09-14 (ContentControl + ControlTheme)
- [x] `ValidatedField.axaml` (FR-UI-16 + FR-UI-17) — 2026-09-14 (persistent label + '?' + themed border + inline cause/remedy error; error clears on fix)
- [x] `DataPreviewTable.axaml` (FR-UI-20, virtualised) — 2026-09-14 (virtualizing ListBox body, file-driven headers, asc→desc→original `DataPreviewStore` sort, invalid-row badge + reason tooltip, read-only selectable cells; D-081) — 2026-09-15 Phase-2 rebuild: `ItemsRepeater` dropped (Avalonia core has none; 11.1.5 package lacks `VirtualizingStackLayout`) → virtualising `ListBox` + code-built header buttons (D-090; sort cycle + invalid-row banner covered by App.Tests)
- [x] `ErrorBanner.axaml` (FR-UI-9) — 2026-09-15 (inline dismissible error summary; icon + cause-and-remedy message + dismiss; **headless test caught a real bug**: control bound visibility never escaped → `IsVisible` now mirrors `Message`, D-093)
- [x] Phase-2 controls regression: 9 headless `AvaloniaFact`s + MainWindow showroom (`ControlsDemo`) + screenshot `logs/screenshots/controls-demo.png` — 2026-09-15 (build 0/0, suite 197 green; GUI rebuild branch `feat/gui-rebuild`)
- [?] UI acceptance of the 8 controls at M5-D screens: §16.7/§16.8 keyboard checklist, focus restore on dialogs, tooltips on hover — deferred to when the real panels land (2026-09-14 note) — **BLOCKED B-007** (owner must run the keyboard pass in the app; cannot be emulated here)

### Welcome Panel (FR-UI-5)
- [x] `WelcomeCard.axaml` view — 2026-09-14 (styled card with logo Images bound to CourseInfo avares URIs, member list, course/professor)
- [x] `WelcomeCardViewModel.cs` — 2026-09-14 (CourseInfo constants → Logo Bitmaps via AssetLoader.Open(new Uri(...)); D-077; logo regression caught + fixed this session: Bitmap(string) is file-path-only, avares needs AssetLoader stream)
- [ ] Fade-out on "Start Calculation" click (≤ 300 ms) — card swaps out instantly today (ResultsPanel ContentControl switch); animated fade deferred
- [ ] Keyboard dismissal (Tab + Enter)

### Layout (FR-UI-11, FR-UI-12, FR-UI-13)
- [x] Left config panel with scroll when overflow — 2026-09-14 (ConfigPanel axaml; Grid column 2*:5* with ScrollViewer + GridSplitter in MainWindow)
- [x] Pinned "Start Calculation" footer — 2026-09-14 (PinnedFooterBar: Start RunCommand / Reset ResetAllCommand / Guide OpenGuideCommand)
- [x] Collapsible sections with persisted state — 2026-09-14 (CollapsibleSection + SessionKey; collapsed state persisted via WidgetPreferences.CollapsedSections)
- [ ] "Clear All" with confirmation dialog + Undo toast — Reset is instant + toast; confirmation/Undo deferred

### Results Panel (FR-UI-14)
- [x] Customisable widget selector — 2026-09-14 (Customise toggle in ResultsPanel; bindings from ResultsViewModel.WidgetKeys)
- [x] Widgets: metrics table, chi-square results, trace (M4), data preview, charts (M6), token — 2026-09-14 (all six widgets rendered in ResultsPanel; charts under `Charts` ChartsPanelViewModel; token shows served/avg-wait summary)
- [x] Persist widget selection across sessions — 2026-09-14 (WidgetPreferences persisted-only VisibleWidgets + CollapsedSections; FR-UI-21 go) — App.Tests MainViewModelTests cover apply-on-construction

### Validation (FR-UI-9, FR-UI-17)
- [x] Wire ValidatedField to view-model error state — 2026-09-14 (ConfigPanel ValidatedField bindings)
- [x] Red border + icon + message on invalid fields — 2026-09-14 (ValidatedField ControlTheme)
- [x] Summary banner near "Start Calculation" — 2026-09-14 (ResultsPanel HasError banner above footer)
- [ ] Focus moves to first invalid field on submit
- [ ] Screen reader live-region announcements
- [ ] Test cases: empty required, out-of-range, malformed file, missing dropdown selection

### Accessibility (FR-UI-15, NFR-7)
- [?] Tab order audit across entire window — keyboard pass must be done in the running app by the owner (no mouse emulation here) — **BLOCKED B-007**
- [ ] Shift+Tab reverses correctly
- [ ] Focus indicator visible with ≥ 3:1 contrast
- [x] Escape closes every modal/dropdown and returns focus — 2026-09-14 (ThemedDialog Escape=cancel + focus restore; guide overlay Escape=close + focus restore via MainWindow _focusBeforeGuide; dropdown Escape)
- [ ] Reduced-motion respected

### Data Preview (FR-UI-20, NFR-10)
- [x] Column header population from loaded file — 2026-09-14 (M5-B + wired: ResultsPanel `ResultsPreviewTable.Load(vm.Preview)` on DataContextChanged)
- [x] Column sort cycle: ascending → descending → original — 2026-09-14 (DataPreviewStore.ToggleSort, tested)
- [x] Row numbering matches source file (1-based; header = row 0) — 2026-09-14
- [x] Fixed header while scrolling — 2026-09-14
- [x] Cell selection + Ctrl+C copy — 2026-09-14 (read-only TextBox cells)
- [x] Empty-cell rendering (dimmed em-dash) — 2026-09-14
- [x] Invalid-row highlighting per FR-UI-17 (red + icon + tooltip) — 2026-09-14
- [x] Error state: replace table with validation summary (FR-UI-9) — 2026-09-14
- [x] Empty state: widget not shown before a file is loaded — 2026-09-14
- [x] Performance test: 10,000-row synthetic file renders in < 1 s — 2026-09-14 (virtualizing ListBox, D-081)
- [x] Sort performance: 10,000 rows in < 200 ms — 2026-09-14
- [x] Test: extra columns render cleanly — 2026-09-14
- [x] Test: invalid row highlights with correct tooltip — 2026-09-14
- [x] Wire preview into results panel as a selectable widget (FR-UI-14) — 2026-09-14

### Startup Behaviour (FR-UI-21)
- [x] Ensure all fields start empty/default on launch — 2026-09-14 (ConfigViewModel factory defaults; welcome card visible per next row)
- [x] Presets dropdown default state = `(none)` — 2026-09-14
- [x] Welcome card visible on launch, dismissed only on "Start Calculation" — 2026-09-14
- [x] No `_lastSession.json` auto-restore — 2026-09-14 (nothing reads/writes it; FR-UI-21 honoured by construction)
- [ ] Test: fresh launch → empty fields, welcome card, no preset
- [ ] Test: launch after prior session → same as fresh
- [ ] Test: load preset → fields populate, dropdown shows name
- [ ] Test: load preset with missing data file → fields populate, data field shows inline error

### In-Program Guide (FR-UI-18, AGENTS §17.1)
- [x] Embed `docs/USER_MANUAL.md` as App resource — 2026-09-14 (EmbeddedResource LogicalName `OpdSimulator.App.Assets.UserManual.md`; no Markdig — custom parser, D-082)
- [x] `GuideView` (side panel: section list + rendered markdown) — 2026-09-14 (GuidePanel.axaml/.cs overlay in MainWindow)
- [x] Guide search + filter — 2026-09-14 (GuideViewModel search ranking; GuideTests)
- [x] F1 hotkey opens; Escape closes; focus returns — 2026-09-14 (MainWindow.KeyBindings F1; Escape; _focusBeforeGuide restore)
- [x] Contextual "?" icons on every config field with `HelpAnchor` — 2026-09-14 (InfoIcon HelpAnchor: arrival-rate, inter-arrival-distribution, service-distribution, service-rate, servers, horizon, seed, p-exit — all now exist as USER_MANUAL sections)
- [x] CI test: embedded markdown == repository markdown — 2026-09-14 (GuideTests drift guard reads embedded vs docs file; rebuilt after manual edit)

### Preset System (FR-UI-19, AGENTS §17.2)
- [x] `Preset` model + `PresetStore` (save/load/list/delete/import/export) — 2026-09-14 (D-083)
- [x] Preset JSON schema v1 + validator — 2026-09-14 (schemaVersion checked, unknown fields ignored, arrivalParameter canonical-minutes)
- [x] Presets dropdown + Save + Manage… dialog — 2026-09-14 (ConfigPanel PresetBar + PresetManagerDialog.cs code-behind Window)
- [x] File name sanitisation — 2026-09-14 (PresetNaming.Sanitize)
- [x] Path resolution under ApplicationData — 2026-09-14 (`<ApplicationData>/OpdSimulator/presets`)

### Tests
- [x] `OpdSimulator.App.Tests` project (pure-logic, no Avalonia session): SearchFilter ranking, DataPreviewStore sort cycle + invalid-row preservation, ToastService/ToastLifecycle expiry — 2026-09-14 (20 tests; full suite 196 green, 0 warnings; added to sln + DEV_LAUNCH §6/§8)
- [x] Preset round-trip: save → reload → every field matches — 2026-09-14 (PresetStoreTests.SaveAndLoad_RoundTripsEveryField)
- [x] Schema mismatch (v99) → clear error — 2026-09-14 (Load_ + Save_SchemaMismatch_ThrowsClearError)
- [x] Missing data file on preset load → inline error — 2026-09-14 (store level: preset loads with DataFile intact; inline "reselect data" error is the VM/UI layer, see Startup Behaviour rows)
- [x] Sanitisation of preset names — 2026-09-14 (PresetNaming.Sanitize tests)
- [x] Collision prompts (or deterministic error headless) — 2026-09-14 (Rename_/Duplicate_Collision_ThrowsDeterministicError)
- [x] Path resolution under ApplicationData (not CWD) — 2026-09-14 (PathResolution test)
- [x] Export → import cycle loads cleanly — 2026-09-14 (Export_ThenImportFromFreshStore_RoundTripsCleanly)
- [x] Guide: embedded markdown matches repository file — 2026-09-14 (GuideTests drift guard)
- [x] Guide: renderer produces non-empty output — 2026-09-14 (GuideTests)
- [x] Guide: search filter returns expected sections — 2026-09-14 (GuideTests ranking)

### Docs
- [ ] Add "Verifying Your Uploaded Data" section to `USER_MANUAL.md`
- [ ] Add "Starting from Empty" section to `USER_MANUAL.md`
- [ ] Add "Presets" section to `USER_MANUAL.md`
- [ ] Add "In-Program Guide" section to `USER_MANUAL.md`

- [ ] Create `scripts/verify-traceability.sh` (orphan check: `[x]` matrix rows without Source/Test; referenced by REQUIREMENTS.md Update Protocol)
- [x] Obtain `samples/sample_patients.xlsx` (owner; 1-stage demo data) — see BLOCKERS B-004 — 2026-09-13 (no real file provided → **stand-in** generated deterministically by `scripts/sample-data-generator`, seed 42: 60 rows, single Screening stage, Exp(λ=0.5)/Exp(μ=0.666…), p_exit=1.0 (CONTEXT §5.5); BLOCKERS B-004 Resolved; real clinic data remains substitutable at the same path)
- [x] Add headless CLI mode to `OpdSimulator.Cli` (DEV_LAUNCH §7; demo path 2026-09-16) — 2026-09-13 (M1 CLI: `--lambda/--mu/--servers/--horizon/--seed`, ρ table, exit 0/1; F2/F3 verified on feat/milestone-1-single-stage-engine)
- [x] Set up solution structure (Core / Data / App / CLI / Tests) — 2026-09-13 (sln + 6 projects, packages pinned per D-026; App is a placeholder classlib)
- [x] Initialize Avalonia project — 2026-09-14 (hand-built shell on feat/milestone-5-gui, D-078; B-005 Resolved via hand-build; window launches on Ubuntu; `dotnet run --project src/OpdSimulator.App` verified, logs/app created)
- [x] Add MathNet.Numerics, ClosedXML, CsvHelper via NuGet (update DEV_LAUNCH in the same change) — 2026-09-13 (extended with Avalonia/CommunityToolkit/LiveCharts/Serilog per scaffold instruction; versions + rationale in D-026; DEV_LAUNCH §3/§5/§8 updated)
- [x] Implement Event, Queue, Server, Patient classes (M1: A1–A6 on feat/milestone-1-single-stage-engine) — 2026-09-13 (all M1 Core classes written; 34 tests green; dead-state verified)
- [x] Implement FEL (priority queue) and generic N-stage DES engine — validated against M/M/1 and M/M/2 (per D-006) — 2026-09-13 (FEL + generic single-stage engine; validated vs analytical M/M/1 in EngineTests, 0.724 vs 0.75; M/M/2 + M/M/c table comparison tracked by the "Implement analytical M/M/c validation comparison" row)
- [ ] Implement Statistics collector (avg wait, queue length, utilisation, time-in-system; expose per-server busy times + waiting-time samples for charts — D-019) — M1 core metrics live in SimulationResult.cs; D-019 chart hooks pending
- [ ] Implement per-stage ρᵢ (routing-derived λᵢ) refusal — report ALL unstable stages with λᵢ, cᵢ, μᵢ, ρᵢ (FR-VAL-1) + per-stage ρ display in results panel (FR-STAT-6) + utilisation assertion 0 ≤ util ≤ 1 — engine/topology-level refusal + listing LANDED (M3 sub-blocks A/B); CLI per-stage ρᵢ display (`--verbose`) + UI panel pending (E/F/M5)

Milestone 3 (multi-stage network) sub-block plan (kickoff 2026-09-13):
- [x] M3 A: engine generalisation — StageSpec/Stage/NetworkTopology, `Run(topology, seed, horizon)`, StageMetrics[], Patient.SystemArrivalTime/AdvanceToStage, ALL-stage unstable refusal; D-050 random-among-idle server fix — 2026-09-13 (commits babed9a + docs 35fefd4; balance tests at λ=2, μ=4, c=2 green; D-049/D-050)
- [x] M3 B: engine-level per-stage refusal listing ALL unstable stages — 2026-09-13 (commit 9b17f80; 3 new EngineTests; B3 CLI display folded into E/F)
- [x] M3 C: `ClinicCalendar` (open Mon–Thu + Sat, 08:15–11:00 arrivals, service drain past 11:00, daily cap) + engine calendar integration + real-clock display (CONTEXT §5.1–5.3, §1.1; D-009) — 2026-09-13 (commit c476299; 22 new tests; D-051)
- [-] M3 D: run modes — engine-side fully absorbed by C (generatorDays + startDayOfWeek express all D-009 modes); only CLI flags remain, land in F (2026-09-13)
- [x] M3 E: `simulate-data` becomes 3-stage: per-stage fitted μᵢ, p_exit from data (PExitCalculator), per-stage metrics output — 2026-09-13 (ClinicStageOrder + validator blank-downstream relaxation + SimulateDataCommand rework + PrintNetworkMetrics; 5 new tests; 144 green, 0 warnings; D-052). NOTE: B3 `--verbose` ρᵢ pre-run print remains part of F
- [x] M3 F: `simulate-network` command — named inline flags (NOT a JSON config, D-053), `--lambda --c --mu --stages`, `--p-exit` (needs ≥ 3 stages), day-model flags `--days N`/`--start-day`/`--cap`, `--horizon` (mutually exclusive with `--days`), `--verbose` B3 ρᵢ pre-run print, refusal exit 1 listing ALL unstable stages — 2026-09-13 (12 new CLI tests; 156 green, 0 warnings; D-053; live smoke `--verbose --days 5 --cap 80` verified)
- [x] M3 G: acceptance — 156 tests (> 128), 0 warnings; M1 numerically identical live (served 29892, wait 0.724, ρ 0.75); simulate-network per-stage verified; unstable refusal via CLI blocks listing ALL unstable stages (both commands); simulate-data 3-stage (fit + p_exit + per-stage blocks) verified; day-repeatability via same-seed identical runs; M2 sweep c=2/3 waits refreshed to 0.315/0.030 in DEV_LAUNCH §7.4 (D-050 RNG change) — 2026-09-13
- [x] M3 H: docs pass — DEV_LAUNCH §7.4 stage-aware + §7.5 simulate-network + §8 sample + changelog row; USER_MANUAL §7.4–§7.6 + changelog; REQUIREMENTS (FR-SIM-1/4/6/7/8/9→[x], FR-SIM-10→[~], FR-STAT-6/7→[x], FR-VAL-1 refreshed; coverage 63.0%); DECISIONS D-052/D-053; VIVA_ANSWERS +5 M3 Q&As; TODO/PROGRESS updated; BLOCKERS no stale M3 entries — 2026-09-13 (dead-state delete bin/obj → restore/build/test: 156 green, 0 warnings)
- [x] Write unit tests for engine — 2026-09-13 (E1–E8: 34 facts across Queue/Event/FEL/RNG/Exponential/Server/Engine/Stability; all green on feat/milestone-1-single-stage-engine)
- [x] Record the 5-patient hand trace (viva walk-through) — 2026-09-14 (frozen as `tests/OpdSimulator.Core.Tests/Fixtures/trace-5-patients.txt`; hand-verified draw-by-draw against the reference `Random(42)` sequence: -ln(U)/λ for every inter-arrival and -ln(U)/μ for every service; λ=3 μ=4 c=1 seed 42)
- [x] Implement Excel/CSV loader (ClosedXML + CsvHelper) with dirty-data rejection — 2026-09-13 (src/OpdSimulator.Data/Loaders/ + Validation/DataValidator, per-row issues; LoaderTests 55-german capacity; FR-DATA-1/2/7; D-038)
- [x] Implement MLE fitting (Exponential first, then Normal/Lognormal/Gamma) — 2026-09-13 (5 fitters + DistributionFitterFactory incl. Uniform; MLE σ denominator n, D-043; gamma MoM, D-041; FR-DATA-5)
- [x] Implement chi-square test (auto bins ≈ √n, df = k−1−p, p-value, decision; expose binned data + fitted PDF points for charts — FR-STAT-8) — 2026-09-13 (BinSelector k=⌈√n⌉∈[5,20], ChiSquareTest with fail-loud guards D-040/D-044; O/E arrays exposed in ChiSquareResult; fitted-PDF-points for the P1 chart is the FR-STAT-8 M5 row)
- [x] Implement `p_exit` estimation from `departure_stage` (exclude + warn on Reception rows) — 2026-09-13 (PExitCalculator excludes Reception per CONTEXT §5.4; validator itself is strict Screening/Doctor per kickoff B1 — D-038; FR-DATA-6)
- [ ] Implement rate-wise / mean-wise toggle + manual λ override — data layer landed (src/OpdSimulator.Data/Parameters/{ParameterMode,ModeValidator}, soft warnings, FR-DATA-8 `[x]`); the **UI toggle** + manual-λ side-by-side display remain M5 GUI (reclassified from `[~]` to `[ ]` at the M4 pass so only one task is IN PROGRESS at a time, AGENTS §9.2)
- [ ] Extend engine to the 3-stage network with routing (a config change, not a rewrite) — engine architecture done (M3 sub-block A); network wiring into `simulate-data`/`simulate-network` CLI pending (M3 E/F)
- [ ] Implement clinic calendar (hours 8:15–11:00, closed Fri/Sun, daily cap, run-mode × horizon per D-009)
- [x] Implement event log and step-by-step trace (viva trace file) — 2026-09-14 (first-class `trace` feature: Trace namespace D-055; level-blind engine via ITraceSink with passive TraceRandomSource D-057; `trace` CLI D-059; golden fixture + regression tests incl. draw-by-draw RNG parity and stats cross-check D-058; CLI end-to-end trace tests; 176 green, 0 warnings; commits f6b95e7/94a2d8e/e40e1e3; DEV_LAUNCH §6+§7.6, USER_MANUAL §7.6, REQUIREMENTS FR-VAL-4 refresh + coverage recompute to 63.0%, DECISIONS D-055..D-059, VIVA_ANSWERS +5 trace Q&As. Pending: owner review/merge of feat/milestone-4-event-trace)
- [x] Engine: assign patient to random idle server (log the policy) — 2026-09-13 (IRandomServerSelectionPolicy D-050; RandomIdleSelection default, LowestIdSelection test-only; balance + negative regression tests green)
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
- [x] Install global exception handlers in the App project (AppDomain, UnobservedTaskException, Avalonia Dispatcher) → `logs/crash-*.log` + dialog (AGENTS §12.3, 12.5) — 2026-09-14 (see Assets & Foundation "Install global exception handlers" row above: CrashReporter.cs landed with M5-A)
- [ ] Apply log-level discipline (Verbose = RNG draws, Debug = event scheduling, Information = run summaries, Warning/Error/Fatal per §12.2)

## Blocked
- M5 keyboard acceptance rows above (2 rows) — owner manual run required (B-007). B-006 (Windows dead-state) remains pending; nothing blocks current M6 work.

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