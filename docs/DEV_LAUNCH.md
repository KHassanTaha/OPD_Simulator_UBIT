# DEV_LAUNCH.md — Developer Launch Guide

**Purpose:** Launch this project from a dead state (fresh clone, no build artifacts)
with zero errors. Follow this file literally.

**Last verified:** 2026-09-18 — restore/build/test/CLI-run all pass from a clean state on **Ubuntu 24.04** (.NET SDK 8.0.131), Phase 8B (App-only: the Results panel gains a seventh widget — simulation-output chi-square + per-series histogram over the samples 8A retains, D-136; on top of Phase 8A Core-only `SimulationResult` sample retention, D-135) on top of Phase 7D (merged Input tab — upload + preview + fit analysis on one tab; Simulation | Input | Token Generator | Help): Release build 0 warnings/0 errors, full suite **402 green** (Core 90, Data 58, Cli 35, App 219) including the 9 new `Phase8BVerificationTests`, the 5 `GeneratedSamplesTests` and the 36 new `Phase7DTests` (preview projection + invalid-row mapping + severity + truncation, InputTab VM intent events, rendered empty/loaded/validation/mismatch states, config status strip, Results panel loses the preview + legacy `dataPreview` key still parses, four-tab order, MainViewModel routing) and the 4 new `Phase7DScreenshots` frames, real Linux launch alive with "Main window created." and crash logs unchanged; headless evidence `logs/screenshots/phase-8b-verification.png`, `phase-7d-input-empty.png`, `phase-7d-input-loaded.png`, `phase-7d-input-mismatch.png`, `phase-7d-config-strip.png`, plus the earlier 7C/7B/7A/6c PNGs (`phase-7c-manual-mode.png`, `phase-7b-stage-models.png`, `phase-7a-units.png`, `phase-6c-input-analysis.png`, `phase-6c-results-all.png`, `phase-6c-widget-toggled.png`, `phase-6c1-empty.png`, `phase-6c2-histograms.png`, `phase-6c3-chi-square.png`, `phase-6c4-utilisation.png`, `phase-6c5-charts.png`). M1 headless CLI verified: stable run (ρ 0.75) and clean unstable refusal (single-line stderr, no stack trace, exit 1 — ρ 1.25). M2 data CLI verified: `verify` (clean file → exit 0; dirty fixture → exit 1 listing all 5 issues), `fit` (prints params + chi-square, writes `logs/fit-*.json`), `simulate-data --servers 1,2,3` (three runs, exit 0), `export`. M3 `simulate-network` verified (incl. `--days 5 --cap 80 --verbose`). M4 `trace` verified against the frozen golden fixture (state/rng/events; `--output`; unstable refusal). **M5/Rebuild GUI verified: ava headless renders of MainWindow (phase-1 + controls-demo PNGs in `logs/screenshots/`); real-display launch/keyboard walk is owner-required on a machine with a display (this host is Wayland).** [Windows: TBD]
**Maintainer:** Coding agent (auto-updated)
**Audience:** Taha, graders, any developer

> This file lives in `docs/` (owner decision 2026-09-13).

---

## 1. Prerequisites

Install exactly these once. Do not install anything not listed here without
updating this file.

| Tool | Version | Why | Install (Linux) | Install (Windows) |
|------|---------|-----|-----------------|-------------------|
| .NET SDK | 8.0.x (see `global.json`) | Build + run | `sudo apt install dotnet-sdk-8.0` or via Microsoft feed | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Git | any recent | Clone | `sudo apt install git` | https://git-scm.com/ |
| System libs (Linux only) | libx11-6, libice6, libsm6, libfontconfig1 | Avalonia X11 rendering + font discovery | `sudo apt install libx11-6 libice6 libsm6 libfontconfig1` | — (bundled with Windows) |
| (optional) JetBrains Rider / VS Code + C# Dev Kit | latest | IDE | — | — |

**Verify:**
```bash
dotnet --version     # must print 8.0.x
dotnet --list-sdks   # must include 8.0.x
git --version
```

If `dotnet --version` prints anything other than `8.0.x`, `global.json` will
refuse the build. That is intentional — install the pinned SDK.

---

## 2. Get the Code (Dead State)

```bash
git clone <repo-url> opd-simulator
cd opd-simulator
```

You should see `OpdSimulator.sln`, `global.json`, `src/`, `tests/`, `docs/`.
There must be **no** `bin/` or `obj/` folders yet. If there are, delete them:

```bash
find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +
```

---

## 3. First-Time Restore

Before building, copy the config template:

```bash
cp appsettings.template.json appsettings.json   # Linux
Copy-Item appsettings.template.json appsettings.json   # Windows
```

**Configuration file:** the repo ships `appsettings.template.json` (Serilog sinks + `Simulation` defaults: `RandomSeed: 42`, servers Reception=1 / Screening=2 / Doctor=3). Create your local, git-ignored config **once** using the step above. `appsettings.template.json` is the only committed config: `.gitignore` ignores `appsettings*.json` and negates just the template, so the `appsettings.json` you copied above — and any per-environment overrides such as `appsettings.Development.json` — stay git-ignored.

Run **once** (needs internet):

```bash
dotnet restore OpdSimulator.sln
```

This pulls all NuGet packages (MathNet.Numerics, Avalonia, ClosedXML, CsvHelper, Serilog, etc.).
Expected output ends with `Restored ...`.

> M1 dependency changes (2026-09-13): `OpdSimulator.Core` now references
> **Serilog 4.4.0** (event trace, D-036); `OpdSimulator.Cli` references
> **Serilog.Sinks.File 7.0.0** (rolling + error sinks) and
> `<ProjectReference>` to Core; `OpdSimulator.Core.Tests` references Core.
>
> Phase 6c.1 (2026-09-16): `OpdSimulator.App` re-adds
> **LiveChartsCore.SkiaSharpView.Avalonia 2.0.5** (charts, D-116). The SkiaSharp
> native deps (libfontconfig.so.1, libfreetype.so.6) are already present on
> Ubuntu 24.04 — no extra `apt` step.

**Troubleshooting:**
- `NU1101` — package not found → check `NuGet.config`, check spelling.
- Slow restore on first run — normal; subsequent restores are cached.

---

## 4. Build

```bash
dotnet build OpdSimulator.sln -c Debug
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

If warnings appear, treat them as errors — this project enforces zero-warning policy.

---

## 5. Run the Simulator (Single Command)

> **Status as of 2026-09-15:** the view layer is being **rebuilt from scratch**
> on `feat/gui-rebuild` (feat/gui-rebuild Phases 1–8; the M5 GUI was disposed —
> see `docs/M5_FAILURES.md`). Phase 1 shipped: empty maximized window
> "OPD Clinic Queue Simulator" with the new Token Theme/Motion dictionaries,
> Serilog 3 sinks (§12.1) and the 3 crash handlers (§12.3). The config/results
> panels land in Phases 2–5. Until the GUI is functional again the CLI remains
> the primary path for verified command-line runs.

**Linux / macOS:**
```bash
dotnet run --project src/OpdSimulator.Cli -- simulate-params --lambda 3 --mu 4 --servers 1 --horizon 10000 --seed 42
```

**Windows (PowerShell):**
```powershell
dotnet run --project src\OpdSimulator.Cli -- simulate-params --lambda 3 --mu 4 --servers 1 --horizon 10000 --seed 42
```

Since Milestone 2 the CLI is a subcommand dispatcher: `simulate-params`,
`verify`, `fit`, `simulate-data`, `export` (§7). `simulate-params` is the
Milestone-1 command (bare flags after it).

Expected output (Milestone 1, verified 2026-09-13): a metrics table ending with
`ρ = λ/(c·μ)              :     0.75` and exit code 0 (average wait ≈ 0.72 min
against the analytical M/M/1 value 0.75).

Arguments: `--lambda` arrival rate λ (required), `--mu` service rate per server μ
(required), `--servers` parallel servers (default 1), `--horizon` arrival-generation
window in minutes (default 10000), `--seed` (default 42).

Exit codes: `0` run completed · `1` refused to run (unstable ρ ≥ 1) · `2` bad arguments.

Unstable example (refuses, exit 1). The refusal is one clean line on stderr —
no stack trace (D-037); the full exception is written to `logs/errors-YYYYMMDD.log`:
```bash
dotnet run --project src/OpdSimulator.Cli -- simulate-params --lambda 5 --mu 4 --servers 1 --horizon 1000
# exit code 1; stderr shows exactly:
#   Refusing to run: stage 'Stage 0 (single-stage)' is unstable — ρ = 1.25 (≥ 1) with λ = 5.000, c = 1, μ = 4.000. The queue would grow without bound; lower the arrival rate or add servers.
```

Event trace location: `logs/app-YYYYMMDD.log` (Debug level, one line per event) and
`logs/errors-YYYYMMDD.log` (warnings+) are written on every CLI run.

**GUI (from Milestone 5) — Linux:**
```bash
dotnet run --project src/OpdSimulator.App
```

**GUI (from Milestone 5) — Windows (PowerShell):**
```powershell
dotnet run --project src\OpdSimulator.App
```

**Convenience scripts** (present since 2026-09-13):
- `scripts/run.sh` — Linux launcher (`chmod +x run.sh` once)
- `scripts/run.ps1` — Windows launcher

> Both scripts invoke `src/OpdSimulator.App`. The full M5 GUI landed 2026-09-14:
> left config panel (parameter mode, distributions, servers, horizon, seed,
> presets), right results panel (metrics, chi-square, charts, trace, token),
> a four-tab shell (Simulation | Input | Token Generator | Help — the data
> upload, preview and distribution-fit analysis live on the Input tab since
> Phase 7D), in-program guide (F1), toasts and themed dialogs.
> The CLI remains the headless/scripting path (§7).

The window should open within ~5 seconds. If it does not, see **Troubleshooting** below.

---

## 6. Run Tests

```bash
dotnet test OpdSimulator.sln
```

Expected: `Passed! - Failed: 0`. As of 2026-09-18 **393 tests pass**:
- `OpdSimulator.Core.Tests` (90) — queue, event/FEL ordering, RNG determinism, exponential
  sampling, server utilisation, engine M/M/1 analytical bound, stability refusal, event trace,
  **M4 trace regression (golden fixture, draw-by-draw RNG parity, stats cross-check, sink passivity)**,
  **Phase 8A generated-sample retention (`GeneratedSamplesTests`: horizon inter-arrival count,
  per-stage service samples, calendar retention, same-seed identity, different-seed divergence)**.
- `OpdSimulator.Data.Tests` (58) — Excel/CSV loaders, TimeParser, validator (per-row issues),
  preprocessing (inter-arrival/service/p_exit), all 5 fitters, chi-square (accept/reject/k
  bounds), parameter-mode warnings, export, committed fixtures (samples + dirty file).
- `OpdSimulator.Cli.Tests` (35) — `simulate-params` refusal (clean stderr, no stack, exit 1,
  D-037); `verify` exit 0/1 + issue listing; unknown command → global usage, exit 2;
  `simulate-data` multi-server sweep; non-exponential refusal, exit 2; **M4 `trace` end-to-end
  (golden stdout, levels, refusal exit 1, `--output` mode, usage exit 2)**.
- `OpdSimulator.App.Tests` (219, headless Avalonia, GUI rebuild Phase 1–4, 4b, 4c, 5, 5c, 5c.4, 5d, 6c.1, 6c.2, 6c.3, 6c.4, 6c.5, 6c.6, 7A, 7B, 7C, 7D, 8B) — Avalonia.Headless
  session via `TestAppBuilder`; Phase-1 smoke/render: window title + Maximized state, theme +
  motion token resolution, screenshot capture; Phase-2 per-control tests: ValidatedField (error
  cause+remedy, clear-on-fix, blur validation), SearchableDropdown (type-to-filter + Enter
  commit), ThemedDialog (Escape→Cancel, primary/secondary), ThemedToast (severity render,
  close-with-item), CollapsibleSection (toggle), InfoIcon (tooltip + automation name),
  PinnedFooterBar (real pointer click drives command), DataPreviewTable (asc→desc→original
  sort cycle, invalid-row banner), ErrorBanner (visible on message, hidden on dismiss),
  ControlsDemo screenshot render; Phase-3 shell tests: four tab headers in order, Simulation
  default-selected, 380px config / fill-results split (searched from the window — tab content
  lives in the TabControl content presenter, D-095), min size 1100×700, arrow-key tab
  navigation; shell screenshot render. Phase-4 ConfigPanel tests (10): six CollapsibleSections +
  pinned footer, stage-count live resize (3→5→1 with default names), p_exit visible ⇔ stages ≥ 2,
  p_exit = 1 → inline error + Start blocked (Core boundary [0,1), D-098), p_exit = 0.4 accepted,
  stage-name/Servers two-way binding, Clear All factory reset (FR-UI-21), Clear All confirmation
  gate, Upload request event, config screenshot render. Phase-4b ConfigPanel
  corrections (5): Parameters-section fields effectively disabled + dimmed at
  50% with an FR-UI-7 "Enable '…' above to edit this field." tooltip while the
  optional toggle is OFF, re-enabled + full opacity when toggled ON, Clear All
  resets both optional toggles to OFF, Parameters-off ⇒ no manual overrides +
  ρ "—", Advanced-off ⇒ seed 42 / trace State. **Phase-5c (3 more tests):**
  results column scrolls with the customise toggle pinned and all widgets inside
  the ScrollViewer (`ResultsPanel_ScrollViewer_ContainsAllWidgets`), the known
  Wayland `AppMenu.Registrar` DBus quirk is ignored instead of crash-reported
  (`CrashReporter_IgnoresAppMenuRegistrarDBusError`), and the Phase-5c results
  screenshot renders with a populated trace (overflow asserted in-test).
  **Phase-5d config refinements (17):** 5d.1 stage rows are topology only —
  no editable per-row μ, each row's read-only label shows "(no source)" at
  factory defaults, "(from data)" for stages the loaded file covers, "(manual)"
  only while Parameters is on, and a run whose stage has no μ is refused with
  a banner naming that stage; 5d.2 significance-level α — default 0.05,
  zero/one/non-numeric rejected (blocking Start), α flows into every chi-square
  verdict and into the dynamic "Chi-square goodness-of-fit (α = …)" caption
  (restored by Clear All); 5d.3 stage-count mismatch — loading data with fewer
  or more stages than configured raises the amber warning with Sync / Keep
  actions, Sync resizes + renames rows + clears the warning + refreshes the μ
  labels, Keep dismisses non-destructively, and the Sync command requests
  confirmation before resizing; 5d evidence screenshots
  `logs/screenshots/phase-5d-config.png` (amber mismatch banner + α field) and
  `phase-5d-cleared.png` (default no-source labels after Clear All).
  **Phase-6c.1 chart scaffold (5):** `ChartCard` renders title + caption, shows
  the empty state by default (`ChartCard_EmptyStateVisibleWhenNoData`), hides
  it when `ShowEmptyState=false` (asserted on the named container parts —
  a hidden ContentPresenter prunes its content, local `IsVisible` is not
  effective, D-117), `InputAnalysisView` shows "Load a data file to see fit
  analysis." with no file loaded, and the Phase-6c.1 empty-state screenshot
  (`logs/screenshots/phase-6c1-empty.png`).
  **Phase-6c.2 input histograms (11):** `InputAnalysisService` mirrors the
  run's fits (`"Inter-arrival"` + `"<stage> service"`) and returns nothing for
  an unusable binding; `BuildHistogram` reuses the chi-square result's bins
  verbatim ("never recomputed" — the histogram and the verdict share their
  arrays), scales the fitted PDF as density × bin-width × N (equal-probability
  bins have varying widths), labels bins `[low, high)` and captions each card
  with family + χ² + verdict, and yields a seriesless empty card when a fit
  failed; the ViewModel builds real `CartesianChart` controls
  (observed columns + fitted line overlay) and clears to the empty state on a
  null binding; `ConfigPanelViewModel.DataBindingChanged` fires on load and
  reset; and the MainViewModel wiring keeps the tab in sync across upload,
  Clear All, and a distribution switch. Evidence
  `logs/screenshots/phase-6c2-histograms.png` (Inter-arrival + Screening cards).
  **Phase-6c.3 chi-square bars (6):** every fitted series gains a paired
  observed-vs-expected card below its histogram — observed and expected from
  the verdict's own arrays (same bin count, observed sum = n, categories are
  the bin indices "bin 1"… derived from `BinEdges`, no recomputation), the
  caption restates the ResultsPanel chi-square row verbatim (χ² / df / p /
  Decision), and a fit with no chi-square produces no second card; the shared
  `IInputChartData` interface lets one `ChartCard` slot host either chart kind.
  Evidence `logs/screenshots/phase-6c3-chi-square.png` (histogram + chi-square
  pair per fit, grouped side-by-side columns via the LiveCharts default).
  **Phase-6c.4 per-server utilisation (6):** a **Results** widget (FR-UI-14,
  run-driven per D-121): `UtilisationChartService` maps each `StageMetrics`
  stage's `PerServerUtilisation` to one `UtilisationBarRow`, flagging a server
  when |util − stage mean| > 0.15 (stage mean = `StageUtilisation`, the engine's
  mean of per-server utilisations); `ChartControlBuilder.BuildUtilisationChart`
  renders one `ColumnSeries<double?>` per server (green, amber when flagged,
  per-series `YToolTipLabelFormatter` with the gap), a thin stage-average
  `LineSeries` per stage, hidden legend, % Y labels; the card shows "Run a
  simulation to see utilisation." until the first run completes. Pre-6c.4
  `ui.json` files gain the "utilisation" key once (default-on migration).
  Evidence `logs/screenshots/phase-6c4-utilisation.png` (real 3-stage run,
  servers 1/2/3 → 6 bars + 3 reference lines).
  **Phase-6c.5 queue + waiting-time charts (7):** two more **Results**
  widgets (FR-UI-14, D-122/D-123) — the queue-length-over-time card draws one
  `LineSeries` per stage from `StageMetrics.QueueLengthSeries`, downsampled to
  ≤ 2000 points by min-max bucket decimation (first/last + global min/max
  preserved, O(n)); the waiting-time-distribution card bins
  `StageMetrics.WaitingTimeSamples` into 16 equal-width bins **one stage at a
  time**, with an optional base-10 log-scale Y axis that drops zero-count bins
  as gaps (log 0 is undefined); the stage selector resets to the first stage on
  every new run; both cards show their empty state until a run exists. Chart
  behaviour is asserted on the pure service seams in plain Facts (decimation,
  first/last/min-max, one line per stage, selector reset/change, empty states);
  the log-axis swap is proven in a window-based test driven through the real VM
  `IsWaitLogScale` path. Evidence `logs/screenshots/phase-6c5-charts.png` (real
  3-stage run, both widgets populated).
  **Phase 6c.6 completion gate (9 tests):** widget selector lists exactly the 6
  Results widgets (metrics, chi-square, trace, utilisation, queue-over-time,
  wait histogram — the data preview was removed from the Results panel in
  Phase 7D). All Results charts have empty-state
  messages. NFR-6 background preparation measured and asserted. REQUIREMENTS
  FR-UI-4 and NFR-6 flipped to [x] with source files and test names. Evidence:
  `logs/screenshots/phase-6c-input-analysis.png`,
  `logs/screenshots/phase-6c-results-all.png`,
  `logs/screenshots/phase-6c-widget-toggled.png`.
  **Phase 7A model-driven inputs (15 tests):** `Phase7ATests` covers the new
  time-unit selector (`Models/TimeUnit.cs` + reusable
  `Controls/TimeUnitSelector`, default Minutes), the `ToPerMinute` conversion
  boundary reached through the real `TryBuildRunParameters` seam (Minutes
  passthrough, Seconds ×60, Hours ÷60; Mean-wise inverts 1/mean before the unit
  step), rate-wise/mean-wise manual λ handling, the `TimeSpanPreset` resolution
  (15 min → horizon 15; 1 hour → horizon 60; 1 day → 1 generator day; 1 week →
  6; 1 month → 26; custom days → parsed field), and the `TimeUnitSelector`
  binding. The parameter-mode radio now lives at the top of the optional
  Parameters section above the unit selector and the manual λ/μ fields; the
  Horizon section gains a "Time span" dropdown above the run-mode radio. The
  engine is untouched and still per-minute (D-125). Evidence
  `logs/screenshots/phase-7a-units.png`.
  **Phase 7C two-path configuration (16 tests):** `Phase7CTests` covers the
  "Data source" mode selector (`Models/DataSourceMode.cs`; default FitFromData,
  string-backed dropdown bridge), the always-on data status strip (the "1 · Data"
  section became a strip in Phase 7D — the mode test now asserts the strip stays
  visible and the comma-list μ field hides in EnterManually / returns in
  FitFromData), the `0.4` p_exit seed on switching
  to manual, the EnterManually gate (empty λ, missing/invalid per-stage μ,
  p_exit = 1 all block Start with a named `StartBlockedMessage`), the
  FitFromData gate (no file blocked / usable sample file enabled), the per-stage
  μ → `ManualServiceRates` flow, and both modes producing valid
  `SimulationParameters`. Start is now a completeness gate (D-128, supersedes
  5d.1): the 5d.1-era Start-enabled assertions were updated in place and the
  coordinator's runtime μ refusal is retained as defence in depth (its unit/CLI
  tests carry that load). Evidence `logs/screenshots/phase-7c-manual-mode.png`.
  **Phase 7D merged Input tab (36 tests + 4 screenshots):** the Data section and
  the Input Analysis tab became one **Input** tab. `Phase7DTests` covers
  `InputPreviewViewModel` (column/row projection, 1-based→0-based invalid-row
  mapping, load-failure vs row-warning severity, the five-issue truncation
  summary, clear), the `InputTabViewModel` intent events (upload / clear /
  use-for-simulation / sync-stages / keep-stages) and `SetLoadedFile` /
  `ApplyStageMismatch` / `Clear`, the rendered tab (empty state, "Clear File"
  hidden until a file loads, preview + fit-analysis both visible after a load,
  the FR-UI-9 banner, the D-114 warning), the config status strip (no `1 · Data`
  section; "Manage input →"; the one-line mismatch indicator), the Results panel
  losing the preview and the legacy `dataPreview` preference key still parsing,
  the four-tab order (Simulation | Input | Token Generator | Help), and the
  `MainViewModel` routing (use-for-simulation → FitFromData + tab 0; sync-stages
  → `SyncStagesToData`; clear → config unload; both Upload buttons reach the one
  picker).   `Phase7DScreenshots` renders `phase-7d-input-empty.png`,
  `phase-7d-input-loaded.png`, `phase-7d-input-mismatch.png` and
  `phase-7d-config-strip.png`. `Phase3ShellTests` header renamed "Input Analysis"
  → "Input"; `Phase4ConfigTests` 6→5 sections; `Phase7CTests` mode test updated
  for the strip. D-130..D-134.
  **Phase 8B simulation-verification widget (9 tests + 1 screenshot):**
  `Phase8BVerificationTests` covers the pure `SimulationVerificationService`
  (`VerifyAll` genuine-Exponential output passes chi-square; Deterministic skips
  with a note; General is flattened to Exponential with a note; one report per
  series in stage order — inter-arrival + 3 stages; fewer than two samples yields
  a note, not a verdict) and the widget lifecycle (empty before a run; populated
  by the real Start-button → background-run → posted-completion path with the
  gate config λ=0.5, μ 0.8/0.6/0.4, servers 1/2/3, p_exit 0.4, seed 42, all four
  cards drawable and captioned; cleared by Clear All; the rendered picker lists
  exactly seven widgets including "Simulation verification"). `Phase6c6WidgetSelectorTests`
  and `Phase6c6Screenshots` were updated in place 6→7 (helper/doc "six" renames;
  the results-in-one-frame window grew to 5600 px). The gate frame is saved as
  `logs/screenshots/phase-8b-verification.png`. D-136.

Default seed 42 is used for reproducibility in every test and demo command.

---

## 7. Headless Mode (data-driven commands, since Milestone 2)

The CLI is a subcommand dispatcher. Run `dotnet run --project src/OpdSimulator.Cli -- <command>`.
Each command prints to **stdout** and also logs to `logs/app-YYYYMMDD.log` / `logs/errors-YYYYMMDD.log`.

### 7.1 `simulate-params` — rate-driven simulation (Milestone-1 path)
```bash
dotnet run --project src/OpdSimulator.Cli -- simulate-params --lambda 3 --mu 4 --servers 1 --horizon 10000 --seed 42
```
The M1 metrics table; see §5. Refuses unstable runs (ρ ≥ 1) with a clean line on stderr, exit 1.

### 7.2 `verify —file <path>` — validate an uploaded file
```bash
dotnet run --project src/OpdSimulator.Cli -- verify --file samples/sample_patients.csv
# File is valid: 60 row(s), 1 service stage pair(s).   → exit 0

dotnet run --project src/OpdSimulator.Cli -- verify --file tests/OpdSimulator.Data.Tests/Fixtures/dirty_missing.xlsx
# one line per issue, then "Validation failed: 5 issue(s)…"   → exit 1
```
Validates against FR-DATA-1..9: required columns, valid times, monotonic arrivals,
per-stage `start ≤ end`, `departure_stage ∈ {Screening, Doctor}` (case-insensitive),
blank rows. Every issue names its row; exit 0 = clean, 1 = dirty.

### 7.3 `fit --file <path> [--family exponential|normal|lognormal|gamma|uniform] [--stage all|screening|doctor]` — fit + goodness-of-fit (default family exponential, all stages)
```bash
dotnet run --project src/OpdSimulator.Cli -- fit --file samples/sample_patients.csv --stage all
```
Fits the chosen distribution (MLE/MoM — D-041/D-043) to inter-arrival times and
to service times of each detected stage; prints fitted params, log-likelihood,
AIC, Pearson chi-square (equal-probability bins, D-040) with df and p, and a
Reject/Accept verdict at α = 0.05; then `p_exit` (FR-DATA-6). Writes a JSON
report (for SPSS-style recheck) to `logs/fit-YYYYMMDD-HHMMSS.json`.

### 7.4 `simulate-data --file <path> [--servers 1,2,3] [--seed 42] [--horizon 10000]` — fit then simulate, sweeping servers
```bash
dotnet run --project src/OpdSimulator.Cli -- simulate-data --file samples/sample_patients.csv --servers 1,2,3 --seed 42 --horizon 10000
```
`λ = 1/mean(inter-arrival)`, `μ = 1/mean(service)` on the first detected stage
(details printed). Runs one full M1 engine simulation per server count and prints
a metrics block each (patients served, average wait, ρ). Unstable counts are
refused per-count with no stack trace; exit 0 if at least one run completed.
Only `exponential` is accepted in M2 (other families: clean refusal, exit 2).

**Stage-aware data (M3):** a file with more than one service stage is ordered by
the clinic flow (Reception → Screening → Doctor, `ClinicStageOrder`), fitted with
per-stage μᵢ, and `--servers` then takes exactly one count per detected stage in
flow order (mismatch → exit 2). `p_exit` is estimated from `departure_stage`
when Doctor is present (blank doctor cells for Screening exits are valid — see
D-052), and one network run prints a per-stage block plus network totals. An
unstable network refuses with exit 1 listing every unstable stage:
```bash
dotnet run --project src/OpdSimulator.Cli -- simulate-data --file samples/sample_3stage_clinic.csv --servers 1,2,3 --seed 42 --horizon 500
# p_exit = 0.7; three per-stage blocks + network totals; exit 0
```

**Verified 2026-09-13 on `samples/sample_patients.csv` (seed 42, HH:MM:SS times — D-048):** λ = 0.562/min
(mean inter-arrival 1.78 min), μ = 0.683/min (mean service 1.464 min, stage
`screening`); ρ/avg-wait = 0.82/6.058 min (c=1), 0.41/0.315 min (c=2), 0.27/0.030 min
(c=3). The c=2/c=3 waits were refreshed on 2026-09-13 after D-050 changed server assignment
to random-among-idle (Milestone-1's lowest-ID bias only affected c>1 runs; the c=1 path is
metric-identical, 6.058 min unchanged). `fit` accepts the exponential for both quantities
(p = 0.103 inter-arrival, p = 0.258 service — the second-precision storage lets the
chi-square no longer see the minute-rounded data as discrete). The mean inter-arrival is
~1.78 min vs the generator's 2.0 — sampling variation of the deterministic seed 42 (SE ≈ 0.26).
The stage-aware form was verified on `samples/sample_3stage_clinic.csv` (λ0 = 0.2/min,
p_exit = 0.7, μ 0.5/0.25/0.2, 3 metric blocks + network totals).

### 7.5 `simulate-network --lambda λ₀ --c c₁,c₂,c₃ --mu μ₁,μ₂,μ₃ [--stages …] [--p-exit p] [--days N] [--start-day D] [--cap N] [--horizon m] [--seed s] [--verbose]` — parameter-driven multi-stage run (M3)
```bash
dotnet run --project src/OpdSimulator.Cli -- simulate-network --lambda 0.2 --c 1,2,3 --mu 0.5,0.25,0.2 --p-exit 0.7 --days 5 --cap 80 --verbose
```
Builds the network from named parameters (no data file): `--c`/`--mu` provide one
value per stage (names default to the clinic flow; `--stages` overrides). `--p-exit`
is bound to the Screening stage and needs ≥ 3 stages; λ_doctor = λ₀·(1 − p_exit)
(then ρ_doctor = 0.1 in the example). `--days N` runs the clinic calendar
(open Mon–Thu + Sat, window 08:15–11:00, D-051) with optional `--start-day` and
per-day `--cap`; `--horizon` is the alternative run mode and the two are mutually
exclusive. `--verbose` prints the pre-run routing-derived ρᵢ per stage (the B3
trace aid). Exit 1 with a single stderr line listing EVERY unstable stage.

**Verified 2026-09-13 (Ubuntu 24.04):** the example command printed the day-model
header, pre-run ρᵢ (0.4/0.4/0.1), three per-stage blocks and network totals
(124 served); `--days 5 --cap 80 --seed 42` reproduced identical stdout on a
second run (FR-VAL-3).

### 7.6 `trace --lambda λ --mu μ --servers c [--stages …] [--p-exit p] --patients n [--seed s] [--level events|state|rng] [--output f] [--real-start HH:mm[:ss]]` — deterministic event trace (M4)

```bash
dotnet run --project src/OpdSimulator.Cli -- trace --lambda 3 --mu 4 --servers 1 --patients 5 --seed 42
```

Reruns the configured network and prints one line per state-changing point —
ARRIVAL / START_SVC / END_SVC / ROUTE / EXIT — stopping after `--patients` have
fully left the system, so a trace stays short and reviewable. Level control:
`events` (core columns), `state` (default; adds the server id and the
`→ exit`/`→ next stage` destination), `rng` (adds one RNG row per draw —
`seed=42`, `draw#k U=0.6681 → service time 0.101 min …`). The wall-clock column is
hours into the real anchor (default 08:15:00 via `--real-start`). Every number is
invariant-culture and the RNG stream is untouched (D-057), so the same seed
reproduces the same bytes and every row can be hand-checked against `−ln(U)/λ`.
Engine narration goes to the file logs only — stdout carries trace lines alone
(D-059). Exit 1 with a single stderr line for an unstable configuration
(ρ ≥ 1 at any stage); exit 2 for usage or file-write errors.

**Verified 2026-09-14 (Ubuntu 24.04):** the example command printed and matched
the frozen golden fixture `tests/OpdSimulator.Core.Tests/Fixtures/trace-5-patients.txt`
(draw-by-draw hand-verified against the reference `Random(42)` sequence);
`--level rng` showed `draw#1 U=0.6681` … and `draw#10 U=0.7613`; `--level events`
dropped the state columns and RNG rows; `--output /tmp/t.txt` wrote the trace and
printed the confirmation line; an unstable config (`--lambda 5 --mu 1`) refused
with exit 1.

---

## 8. Project Layout

```
opd-simulator/
├── OpdSimulator.sln
├── global.json                 # pins .NET SDK version
├── AGENTS.md
├── README.md
├── VIVA_ANSWERS.md             # viva Q&A grows here
├── appsettings.template.json   # copy to appsettings.json (§3)
├── appsettings.json            # local config (created by the §3 copy step)
├── docs/                       # PRD, CONTEXT, DECISIONS, TODO, PROGRESS, BLOCKERS,
│                               # DEV_LAUNCH (this file), USER_MANUAL, REQUIREMENTS
├── scripts/
│   ├── run.sh, run.ps1             # invoke the App at M5
│   ├── make-sample-data.sh          # regenerate the sample + fixture files
│   └── sample-data-generator/       # standalone console app (NOT in the sln)
├── samples/
│   ├── sample_patients.xlsx         # committed demo data (generator, seed 42)
│   ├── sample_patients.csv          # committed twin for terminal workflows
│   └── sample_3stage_clinic.csv     # committed 3-stage fixture (Reception→Screening→Doctor, p_exit 0.7)
├── src/
│   ├── OpdSimulator.Core/      # simulation engine (no UI); Trace/ = M4 event trace (D-055)
│   ├── OpdSimulator.Data/      # Excel/CSV loader, fitting
│   ├── OpdSimulator.App/       # Avalonia UI — hand-built shell landed M5-A (D-078)
│   └── OpdSimulator.Cli/       # headless runner
└── tests/
    ├── OpdSimulator.Core.Tests/
    ├── OpdSimulator.Cli.Tests/
    ├── OpdSimulator.Data.Tests/
    └── OpdSimulator.App.Tests/   # M5-B: pure-logic tests (SearchFilter, DataPreviewStore,
        #                           ToastService) — no Avalonia session required
```

> `samples/sample_patients.xlsx` and `.csv` are committed and regenerable via
> `scripts/make-sample-data.sh` (deterministic, seed 42). Dummy data only —
> never commit real patient data (§ .gitignore).

---

## 9. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `A compatible .NET SDK was not found` | Wrong SDK installed | Install 8.0.x per `global.json` |
| `dotnet: command not found` | SDK not on PATH | Reopen terminal; check `~/.bashrc` |
| App starts but window is blank on Linux | Missing X11 or fontconfig libs | Install the Linux system libs in §1 (a one-time `sudo apt install libx11-6 libice6 libsm6 libfontconfig1`) |
| `NU1301` / restore fails | Offline or NuGet blocked | Connect to internet; run `dotnet restore` again |
| App crashes on start | Missing `samples/` file or config | Check console output; file a bug in `BLOCKERS.md` |
| App crashes at runtime | Unhandled exception | Open `logs/crash-*.log` FIRST (full stack trace); see §9.1 |
| No log files appear | App not run yet, or logging misconfigured | Run once; if still none, check `appsettings.json` Serilog sinks |
| All results widgets hidden on launch | Stale per-user preferences file left by an older build | One-time reset: delete the preferences file below; the app re-seeds all widgets on |

### 9.1 Where to Find Logs

| File | Purpose |
|------|---------|
| `logs/app-YYYYMMDD.log` | Full run log — every event, message, warning |
| `logs/errors-YYYYMMDD.log` | Errors and warnings only |
| `logs/crash-YYYYMMDD.log` | Unhandled exceptions with stack traces |

**On Linux:** `tail -f logs/app-$(date +%Y%m%d).log`
**On Windows:** `Get-Content logs\app-$(Get-Date -Format yyyyMMdd).log -Wait`

If the app crashes and the dialog points to a crash log, open that file
first — it contains the full stack trace.

**Per-user preference file (FR-UI-14 widget visibility + collapsed sections; the ONLY persisted UI state — AGENTS §16.11):**
- Linux: `~/.config/OpdSimulator/ui.json`
- Windows: `%APPDATA%\OpdSimulator\ui.json`

If widgets fail to appear on launch, delete this file as a one-time reset — the
app re-seeds all widgets on (D-108). On Linux the folder also holds
`presets/` — never delete presets; `ui.json` alone resets the view state.

*(Add new rows here whenever a new failure mode is discovered and fixed.)*

---

## 10. Demo-Day Checklist

Use this before any live demo. The goal: **launch from dead state, no internet,
no surprises.**

### 10.1 The Night Before
- [ ] `git pull` on the demo machine.
- [ ] `dotnet restore OpdSimulator.sln` (while internet is available).
- [ ] `dotnet build -c Release` (verify zero warnings).
- [ ] `dotnet test` (verify all pass).
- [ ] Confirm `samples/sample_patients.csv` and `.xlsx` exist and load:
      `dotnet run --project src/OpdSimulator.Cli -- verify --file samples/sample_patients.csv` (exit 0).
- [ ] Delete `bin/`/`obj/` once more and re-run `DEV_LAUNCH.md` §3–§5 to prove the dead-state path.
- [ ] Record a full session covering `verify → fit → simulate-data --servers 1,2,3` as fallback.
- [ ] Copy the repo (as a `.zip`, including `obj/` with restored packages) to a USB stick as a second fallback.

### 10.2 On Demo Day (Offline Mode)
- [ ] Open terminal in repo folder.
- [ ] `dotnet build --no-restore -c Release`
- [ ] `dotnet run --project src/OpdSimulator.App --no-build -c Release`
- [ ] Load `samples/sample_patients.xlsx` via the Upload button.
- [ ] Set: servers = (1, 2, 3), distribution = Exponential, horizon = 1 day, seed = 42.
- [ ] Click **Run Simulation**.
- [ ] Walk through: metrics table → chi-square panel → event log → token tab.

### 10.3 If Something Breaks
1. Stay calm. Do not debug live.
2. If the app launches but misbehaves: switch to the **CLI headless run** (§7) — its output alone can carry the demo.
3. If the app does not launch: play the screen recording.
4. If the recording fails: open the repo on the USB copy and repeat §10.2.

### 10.4 Pre-Demo Sanity Commands

```bash
dotnet --version                       # 8.0.x
dotnet build -c Release --no-restore   # succeeds
dotnet test --no-build -c Release      # passes
```

If any of these fail, fix before presenting.

---

## 11. Resuming Work After a Session Ends

### 11.1 Clean End
If the previous session ran a wrap-up, `docs/PROGRESS.md` top entry
is a "Session Handoff" block and `git status` is clean. Open a new
agent session and paste the RESUME SESSION prompt (see §11.3).

### 11.2 Abrupt End
If the previous session ended unexpectedly:
- Check `git status` — commit or stash any WIP before resuming.
- Check `logs/crash-*.log` — the newest file holds the last error.
- Check `docs/TODO.md` for orphaned `[~] IN PROGRESS` tasks.
- Then paste the RESUME SESSION prompt.

### 11.3 The Resume Prompt
RESUME SESSION
Perform the cold-start reconciliation in AGENTS.md §14.
Summarise state in the 6-line format.
Wait for my confirmation.

### 11.4 What to Tell the New Session If You Know What Broke
If you ended the last session because of a specific problem, prepend
one line to the resume prompt, e.g.:
    "Note: the previous session crashed while implementing the FEL
    priority queue — see logs/crash-20260914.log."
That saves the agent the time of discovering it.

---

## 12. Changelog

| Date | Change | Verified on |
|------|--------|-------------|
| 2026-09-18 | **Phase 8B — simulation-output chi-square verification widget** (`feat/milestone-7-model-driven`, App-only): new pure `Services/SimulationVerificationService.cs` (`VerifyAll` → one `VerificationReport` per series; per-series `FitsService.Fit` + shared `InputAnalysisService.BuildHistogram`; Deterministic/General/insufficient-samples handled with an explanatory note, D-136) and new `ViewModels/SimulationVerificationViewModel.cs` (+ `VerificationChartViewModel`; background prep, `Dispatcher.UIThread.Post` chart build, generation-guarded stale-apply). The widget is the seventh Results key `simulationVerification`: `ResultsPanelViewModel` (show/toggle/visible/migration), `WidgetPreferences` seed, `MainViewModel` shared instance + `ResetAll` clear + `ApplyVerification` on completion, `ResultsPanel.axaml` card + picker checkbox. New `Phase8BVerificationTests.cs` (9 tests); `Phase6c6WidgetSelectorTests`/`Phase6c6Screenshots` updated in place 6→7. Baseline 393 → **402** (Core 90 / Data 58 / Cli 35 / App **210 → 219**). Evidence `logs/screenshots/phase-8b-verification.png` (gate config: EnterManually λ=0.5, μ 0.8/0.6/0.4, servers 1/2/3, p_exit 0.4, seed 42; 4/4 series histogram + chi-square, p > 0.05). No Core/Data/Cli change. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release build 0/0; full suite 402 green; headless gate frame rendered (D-089) |
| 2026-09-18 | **Phase 8A — retain RNG-generated samples in `SimulationResult`** (`feat/milestone-7-model-driven`, Core-only): `SimulationResult` gains `GeneratedInterArrivalSamples` (`IReadOnlyList<double>`) and `GeneratedServiceSamplesByStage` (`IReadOnlyList<IReadOnlyList<double>>`), both defaulting to `Array.Empty<…>`; `Engine` gains `_generatedInterArrivals` / `_generatedServiceSamples` per-run buffers with the chart-buffer lifecycle — allocated in `RunCore`, appended in `HandleArrival` (only when the next arrival is scheduled) and `StartService` (indexed by `patient.StageIndex`), projected into the result, then released; no existing property/method/constructor signature changed and both `Run` overloads converge on the same path. New `GeneratedSamplesTests.cs` (5 tests). Baseline 388 → **393** (Core **85 → 90** / Data 58 / Cli 35 / App 210). D-135. No UI/launch path touched, so no launch smoke required. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release build 0/0; full suite 393 green |
| 2026-09-18 | **Phase 7D — merged Input tab (upload + preview + fit analysis)** (`feat/milestone-7-model-driven`): New `ViewModels/InputPreviewViewModel.cs` (preview projection + invalid-row map + severity + 5-issue truncation summary, RULING 1/D-130) and `ViewModels/InputTabViewModel.cs` (upload/clear/use-for-simulation/sync-stages/keep-stages intent events, `SetLoadedFile`, `ApplyStageMismatch`, `Clear`); new `Views/InputTab.axaml`(+`.cs`) hosting the reused `InputAnalysisView` behind an `IsVisible="{Binding HasFile}"` border; `MainWindow` gains `MainTabs` + `TabSelectionChanged` + `PickDataFileAsync`, `MainViewModel` routes tab selection and the single picker (`SetSelectedTabIndex`, D-131); the Data section is replaced by a `PanelCard` status strip (`ConfigSourceText`, "Manage input →", one-line D-114 mismatch indicator; `ConfigPanelViewModel` gains `ConfigSourceStatus`/`HasStageMismatch`/`NavigateToInputTab`/`ClearLoadedFile`, dead Upload/Sync/Keep handlers removed); the data-preview widget + `ShowDataPreview` are removed from `ResultsPanelViewModel`/`ResultsPanel.axaml` (the `dataPreview` preference key still parses); one picker / one load path (RULING 3, D-132); the D-114 Sync/Keep warning moves to the Input tab with Sync routed straight to `SyncStagesToData` (RULING 4, D-133); tab 2 renamed "Input" in place (D-134). 36 new `Phase7DTests` + 4 `Phase7DScreenshots`; stale Data-section tests updated. Baseline 348 → **388** (Core 85 / Data 58 / Cli 35 / App 210). Evidence `phase-7d-input-empty.png`, `phase-7d-input-loaded.png`, `phase-7d-input-mismatch.png`, `phase-7d-config-strip.png`; duplication check passed (ConfigPanel −18, ResultsPanel −17, ResultsPanelViewModel −57; `InputTab.axaml` new). | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release build 0/0; full suite 388 green; headless Linux launch alive "Main window created." + exit 0; crash logs unchanged since 2026-09-16 |
| 2026-09-18 | **Phase 7C — two-path configuration (fit-from-data / enter-manually)** (`feat/milestone-7-model-driven`): new `Models/DataSourceMode.cs` enum; `ConfigPanelViewModel` gains `SourceMode` (default FitFromData) + string-backed dropdown bridge, `IsDataSectionVisible`/`IsManualMuEditable`/`IsCommaListMuVisible`, `0.4` p_exit seed on switching to manual, Parameters-toggle lock; `ConfigPanel.axaml` adds a "Data source" dropdown above `1 · Data`, gates the Data section, hides the comma-list `ManualMuField` in manual mode, adds a per-stage "Service rate μ" `ValidatedField` (manual mode always; fit mode only for stages with no fitted rate), and binds a new `ErrorBanner` to `StartBlockedMessage`; `StageRow` gains `MuValue`/`MuHasError`/`MuError`/`IsMuVisible` + blur `ValidateMu()`; fit-mode `ManualServiceRates` now prefers fitted → comma list → per-stage (fitted wins, superseding 5d.1 manual-over-fitted); `RecomputeBlockingState()` is a per-mode completeness gate (D-128) also invoked from `ApplyLoadedFile`/`SyncStagesToData` (stale-name bug fixed in-gate). 16 new `Phase7CTests`; 5d.1 Start-gate tests updated in place. Baseline 348 (Core 85 / Data 58 / Cli 35 / App 170). Evidence `logs/screenshots/phase-7c-manual-mode.png`. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release build 0/0; full suite 348 green; real Linux launch 18 s alive "Main window created." + exit 0; crash logs unchanged since 2026-09-16 |
| 2026-09-18 | **Phase 7B — per-stage model notation** (`feat/milestone-7-model-driven`): new pure `Services/ModelNotationParser` (Kendall `A/S/c`; `M`→Exponential, `D`→Deterministic, `G`→General, c 1–5; throws with an actionable message otherwise; `StandardModels` = 11 entries M/M/1..5, M/D/1..3, D/M/1..2, G/G/1); `StageRow` gains `SelectedModel`/`UseAdvancedSetup`/`ArrivalFamily`/`ServiceFamily` + `OnSelectedModelChanged` (sets families and `Servers.Value`, skipped while Advanced is on); `ConfigPanel.axaml` stage row adds a Model dropdown, an Advanced toggle, and two revealed family dropdowns; `TryBuildRunParameters` sources the arrival/service family from the FIRST stage (per-stage service deferred — single family in the record, D-126); `Deterministic`/`General` are display-only placeholders (engine still exponential, null fit report — D-127). 17 new `Phase7BTests`; baseline 332 (Core 85 / Data 58 / Cli 35 / App 154). Evidence `logs/screenshots/phase-7b-stage-models.png`. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release build 0/0; full suite 332 green; real Linux launch 18 s alive "Main window created." + exit 0; crash logs unchanged since 2026-09-16 |
| 2026-09-18 | **Phase 7A — model-driven inputs** (`feat/milestone-7-model-driven`): new `Models/TimeUnit` (Minutes/Seconds/Hours) + reusable `Controls/TimeUnitSelector`; parameter-mode radio moved to the top of the Parameters section alongside the unit selector; Horizon gains a `TimeSpanPreset` dropdown (15 min / 1 h / 1 d / 1 wk / 1 mo / custom days); `TryBuildRunParameters` converts manual λ/μ to per-minute (engine stays minutes-only, D-125) and resolves short spans to a bounded horizon / day+ spans to generator days (week→6, month→26). 15 new `Phase7ATests`; baseline 315 (Core 85 / Data 58 / Cli 35 / App 137). Evidence `logs/screenshots/phase-7a-units.png`. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release build 0/0; full suite 315 green; real Linux launch 18 s alive "Main window created." + exit 0; crash logs unchanged since 2026-09-16 |
| 2026-09-17 | **Phase 6c.6 — final 6C gate** (`feat/milestone-7-model-driven`): widget selector lists exactly 7 Results widgets (metrics, chi-square, trace, data preview, utilisation, queue-over-time, wait histogram); all Results charts have empty-state messages; NFR-6 background preparation measured and asserted; REQUIREMENTS FR-UI-4 and NFR-6 → [x] with source + test; consolidated screenshots `phase-6c-input-analysis.png`, `phase-6c-results-all.png`, `phase-6c-widget-toggled.png`. Test baseline 300 (Core 85 / Data 58 / Cli 35 / App 122). | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release build 0/0; full suite 300 green; real launch smoke clean; 0 new crash entries |
| 2026-09-17 | **Phase 6c.5 — queue-length-over-time + waiting-time histogram widgets** (`feat/milestone-6c-input-analysis-charts`): new `Services/QueueLengthChartService` (pure — one `QueueStageSeries` per stage from `StageMetrics.QueueLengthSeries`, **min-max bucket decimation** to ≤ 2000 pts D-122: first/last verbatim, global min/max preserved, O(n), caption notes "downsampled from {N} samples") + `Services/WaitHistogramService` (pure — 16 equal-width bins per stage, invariant-culture `[low, high)` labels, degenerate single bin for a zero-width range, `EmptyStateText`); `ChartControlBuilder` gains `BuildQueueChart` (per-stage `LineSeries<ObservablePoint>`, flat lines, minute X labels, legend, theme palette cycled) and `BuildWaitHistogramChart` (per-stage `ColumnSeries<double?>`, optional `LogarithmicAxis(10)` through a new `yAxisOverride` on the shared `CreateChart`, zero bins → null gaps only on the log axis) plus `SeriesPalette` (green/teal/violet `#6B2FBA`/vermillion `#E54600`, hex never inline, D-117); `ResultsPanelViewModel` `SetQueueLength`/`SetWaitHistogram`/`RebuildWaitHistogram`/`IsWaitLogScale` + `WaitStageNames`/`SelectedWaitStage` (selector resets to first stage each run, D-123) wired into `CompleteRun`/`Reset`, two more picker checkboxes, `WidgetPreferences` seed + one-time migration gain "queueLength"/"waitHistogram"; empty states "Run a simulation to see queue length over time." / "…waiting-time distribution."; widget order metrics → utilisation → queue length → wait histogram → chi-square → preview → trace. Test lessons D-123: chart behaviour asserted on pure service seams in plain Facts; axis/rendering proof only in window-based AvaloniaFacts (bare chart construction flaked the headless dispatcher under suite load — 6 plain + 1 screenshot test, incl. 3 assertions of the decimator and a VM-driven log-axis swap). §6 refreshed to 291 tests (App 113); §1 Last verified updated; D-122, D-123 | **Ubuntu 24.04** (dotnet SDK 8.0.131) — Release build 0 errors/0 warnings; full suite 291 green (Core 85 / Data 58 / Cli 35 / App 113); real Linux launch 18 s alive with "Main window created." and crash logs unchanged; headless evidence `logs/screenshots/phase-6c5-charts.png` (real 3-stage run, both widgets populated) |
| 2026-09-17 | **Phase 6c.4 — per-server utilisation widget in Results** (`feat/milestone-6c-input-analysis-charts`): new `Services/UtilisationChartService` (pure `UtilisationChartData` — one bar per server in stage order, reference line per stage at `StageUtilisation` = engine's mean of per-server utilisations, flag when \|util − stage mean\| > 0.15, threshold + caption constants); `ChartControlBuilder.BuildUtilisationChart` (per-server `ColumnSeries<double?>` green / amber `BrushWarning` for flagged servers, per-series `YToolTipLabelFormatter` with the gap, thin stage-average `LineSeries`, hidden legend, % Y labels; the shared `CreateChart` shell gained `yLabeler` + `showLegend`); `ResultsPanelViewModel` `ShowUtilisation` (FR-UI-14 default-on) + `UtilisationChart` content + empty state "Run a simulation to see utilisation.", wired in `CompleteRun`/`Reset`; `ResultsPanel.axaml` widget between metrics and chi-square + 5th picker checkbox; `WidgetPreferences` seed gained "utilisation" with a one-time migration for pre-6c.4 `ui.json`; AGENTS §16.12 Tab Semantics added; `BrushColor` lookup hardened (lazy theme-dictionary throw → theme-hex fallback) and `SetUtilisation` degrades to the empty card when no full Avalonia app exists (plain unit tests — real path covered by the AvaloniaFact). 5 content tests + screenshot-evidence test. §6 refreshed to 284 tests (App 106); §1 Last verified updated; D-121 | **Ubuntu 24.04** (dotnet SDK 8.0.131) — Release build 0 errors/0 warnings; full suite 284 green (Core 85 / Data 58 / Cli 35 / App 106); real Linux launch 18 s alive with "Main window created." and crash logs unchanged; headless evidence `logs/screenshots/phase-6c4-utilisation.png` (real 3-stage run, servers 1/2/3 → 6 bars + 3 stage-average reference lines) |
| 2026-09-16 | **Phase 6c.3 — chi-square observed-vs-expected bars** (`feat/milestone-6c-input-analysis-charts`): `InputAnalysisService.BuildChiSquareChart(FitReport)` returns `ChiSquareChartData` — categories are the bin indices ("bin 1"…) derived from `ChiSquareResult.BinEdges`, observed/expected are the verdict's own arrays verbatim (never recomputed, D-118 preserved), caption restates the ResultsPanel row verbatim (`χ² = stat, df = df, p = p — Decision`); `ChartControlBuilder` gains `BuildChiSquareChart` (two `ColumnSeries` — Observed `BrushChartSeries1`, Expected `BrushChartSeries2` — drawn grouped side-by-side by the LiveCharts default, never stacked) and a shared `CreateChart` shell re-factored out of the histogram builder; a common `IInputChartData` interface lets `ApplyPrepared` dispatch histogram vs chi-square data into the same `ChartCard` slot; `Prepare` emits per fit: histogram card then chi-square card (a fit with no chi-square emits only its histogram empty state, no fabricated second card). 5 content tests + screenshot-evidence test. §6 refreshed to 278 tests (App 100); §1 Last verified updated; D-120 | **Ubuntu 24.04** (dotnet SDK 8.0.131) — Release build 0 errors/0 warnings; full suite 278 green (Core 85 / Data 58 / Cli 35 / App 100); real Linux launch 18 s alive with "Main window created." and crash logs unchanged; headless evidence `logs/screenshots/phase-6c3-chi-square.png` (histogram + chi-square pair per fit) |
| 2026-09-16 | **Phase 6c.2 — Input Analysis histograms + fitted PDF overlay** (`feat/milestone-6c-input-analysis-charts`): new `Services/InputAnalysisService` (pure — `FitAll` mirrors the run's fits, `BuildHistogram` reuses `ChiSquareResult.BinEdges`/`Observed` verbatim "never recomputed" and scales the PDF as density × bin-width × N, D-118); `Services/ChartControlBuilder` builds the `CartesianChart` (observed columns `BrushChartSeries1`, fitted line `BrushChartSeries2`, axis/grid/legend/tooltip paints from ChartTheme brushes with hex-free fallbacks); `ViewModels/InputAnalysisChartViewModel` (Title/Caption/HasSeries/ChartContent); `InputAnalysisViewModel` grows `Charts`, `Apply` (sync) / `ApplyAsync` (Task.Run + generation guard, G5, D-119) / `ApplyPrepared`; `ConfigPanelViewModel.DataBindingChanged` fires on load/reset; `MainViewModel` syncs the tab on upload, Clear All, distribution, and α changes; `InputAnalysisView` renders one `ChartCard` per fit. §6 refreshed to 272 tests (App 94); §1 Last verified updated | **Ubuntu 24.04** (dotnet SDK 8.0.131) — Release build 0 errors/0 warnings; full suite 272 green (Core 85 / Data 58 / Cli 35 / App 94); real Linux launch 18 s alive with "Main window created." and crash logs unchanged; headless evidence `logs/screenshots/phase-6c2-histograms.png` (Inter-arrival + Screening histogram cards) |
| 2026-09-16 | **Phase 6c.1 — chart infrastructure + Input Analysis scaffold** (`feat/milestone-6c-input-analysis-charts`): re-pinned **LiveChartsCore.SkiaSharpView.Avalonia 2.0.5** (D-116, matches the parked M6 branch pair with Avalonia 11.3.3); Theme.axaml adds colour tokens `ColorChartSeries3` #6B2FBA (violet, ≈7.7:1) and `ColorChartSeries4` #E54600 (vermillion, ≈4.0:1), both ≥ 3:1 WCAG AA vs the white panel; new brush-only `Assets/ChartTheme.axaml` (referenced by charts, hex never inline — D-117); new reusable `Controls/ChartCard` (Title/Caption/EmptyStateText/ShowEmptyState/ChartContent); new `InputAnalysisViewModel` + `Views/InputAnalysisView` (empty state "Load a data file to see fit analysis.") wired into the Input Analysis tab, replacing the placeholder; §6 refreshed to 261 tests (App 83) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — Release build 0 errors/0 warnings; full suite 261 green (Core 85 / Data 58 / Cli 35 / App 83); real Linux launch 18 s alive with "Main window created." and crash logs unchanged; headless evidence `logs/screenshots/phase-6c1-empty.png` (Input Analysis tab empty state) |
| 2026-09-16 | **GUI rebuild Phase 5d — config panel refinements** (`feat/gui-rebuild`): 5d.1 μ moves out of Stages — rows are topology only (name + servers) with a read-only μ-source label; the single manual entry point is the Parameters comma list (rate/mean-wise per toggle, blank = fitted); the run refuses with a banner naming the stage (`Stage '<name>' has no service rate…`) when no source exists (D-112). 5d.2 significance level α in Model — default 0.05, strictly (0,1) validation blocking Start, threads through FitsService into every chi-square verdict and into the dynamic results caption (`SetChiSquareAlpha`, D-113). 5d.3 stage-count mismatch — amber warning (reuses theme warning tokens, D-115) with Sync-stages-from-data / Keep-current-stages actions when the loaded data's stage count differs from the configured list (D-114; the 5d.1-vs-5d.3 message conflict resolved to the 5d.3 "NEW" wording). 5d.4 ErrorBanner gained a `BannerSeverity` (Error/Warning/Info) with theme-resource colours. §6 refreshed to **256 tests** (App 78) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — Release build 0 errors/0 warnings; full suite 256 green (Core 85 / Data 58 / Cli 35 / App 78); real Linux launch 15 s alive with "Main window created." and crash log unchanged; headless evidence `logs/screenshots/phase-5d-config.png` (amber mismatch banner + α field) + `logs/screenshots/phase-5d-cleared.png` (default no-source μ labels after Clear All) |
| 2026-09-16 | **GUI rebuild Phase 5c.4 — Clear All full reset** (`feat/gui-rebuild`): confirming the Clear All themed dialog now runs `MainViewModel.ResetAll()` — `Config.ResetToDefaults()` (unloads the uploaded file, drops fitted parameters) + new `ResultsPanelViewModel.Reset()` (welcome card back, `HasRun=false`, `RunError=null`, `TraceText=""`, metrics/chi-square/preview content cleared; persisted widget VISIBILITY preferences survive per FR-UI-21); ConfigPanel falls back to config-only reset when no MainWindow hosts it; tooltip updated; D-111; §6 refreshed to 239 tests (App 61) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — Release build 0 errors/0 warnings; full suite 239 green (Core 85 / Data 58 / Cli 35 / App 61); real Linux launch 15 s alive with "Main window created." and crash log unchanged |
| 2026-09-16 | **GUI rebuild Phase 5c parts 2 + 3** (`feat/gui-rebuild`): the calendar `Engine.Run` overload now forwards an optional `ITraceSink` so ClinicDay/MultiDay runs populate the event trace in every mode (owner-approved minimal Core change, D-110 — Core suite 85 green before AND after; `SimulationCoordinator` passes its sink in every run mode; the "(D-105) Diagnostic-only" placeholder removed; new `TraceViewer_PopulatesAfterClinicDayRun`; two old `Assert.Empty(TraceLines)` calendar assertions updated); welcome card now actually renders on fresh launch — the ResultsPanel `ContentControl` was bound only to `IsVisible` and never to `Content="{Binding Welcome}"`, so the template never instantiated (D-109 value — part 3 test `WelcomeCard_VisibleOnFreshLaunch_AndHiddenAfterRun`); test-data fix — `TraceViewer_PopulatesAfterClinicDayRun` needed three manual μs (one per default stage), not one; §6 refreshed to 236 tests (App 58); §9.1 ui.json paths unchanged (part 1) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — Release build 0 errors/0 warnings; full suite 236 green (Core 85 / Data 58 / Cli 35 / App 58); real Linux launch 15 s alive with "Main window created." and crash log unchanged (0 new entries); headless evidence `logs/screenshots/phase-5c-results.png` (60.9 KB, populated run) + `logs/screenshots/phase-5c-welcome.png` (48.5 KB, fresh-launch welcome card with logos + "Group members" card, asserted in-test) |
| 2026-09-16 | **GUI rebuild Phase 5c part 1** (`feat/gui-rebuild`): results column scrolls — widget container in a ScrollViewer (Vertical=Auto, Horizontal=Disabled) with the "Customise results" toggle + widget picker pinned above (ResultsPanel `RowDefinitions="Auto,*"`), results column `380,6,*` + `MinWidth=540` on the Border (compares to the rejected `MinMax(540,*)` — AVLN2005); the known Wayland `AppMenu.Registrar` DBus quirk is ignored by the TaskScheduler handler instead of crash-reported (D-107); FR-UI-14 persistence actually restored — `MainViewModel` now `WidgetPreferences.Load()`s and the default seed is all-on instead of all-off (D-108); repo's first `InternalsVisibleTo` so App log/banner machinery is testable; §6 refreshed to 233 tests (App 55); §9.1 documents the per-user `ui.json` paths + one-time reset; stale `ui.json` deleted as the approved one-time reset | **Ubuntu 24.04** (dotnet SDK 8.0.131) — Release build 0 errors/0 warnings; full suite 233 green (Core 85 / Data 58 / Cli 35 / App 55); real Linux launch 15 s alive with "Main window created." and crash log unchanged; headless evidence `logs/screenshots/phase-5c-results.png` (65 KB, metrics + chi-square + populated State trace, overflow asserted in-test) |
| 2026-09-16 | **GUI rebuild Phase 4c — owner corrections to Phase 4b** (`feat/gui-rebuild`): optional-OFF sections no longer gate Start — `ClearError()` on OFF, `RecomputeBlockingState` skips their fields, re-validate on next blur (D-103 supersedes the D-102 deviation note; Phase-4 `PExit_ValueOne` test now enables Parameters first; +3 App tests); white/light header switch via custom `HeaderToggleSwitch` ControlTheme (`PART_MovingKnobs` Panel contract + `x:SetterTargetType`, build lessons in D-103); single full-width brand-blue section bar (top corners `8,8,0,0`, content on white beneath); §6 refreshed to 221 tests (App 43) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — dead-state Release build 0 errors/0 warnings; full suite 221 green (Core 85 / Data 58 / Cli 35 / App 43); real Linux launch 15 s alive with "Main window created." and 0 new crash-log entries; headless evidence `logs/screenshots/phase-4-config.png` regenerated (45 KB) |
| 2026-09-16 | **GUI rebuild Phase 4 — ConfigPanel** (`feat/gui-rebuild`): real config panel replaces the Simulation-tab placeholder — six CollapsibleSections (Data upload / Model / Parameters / Stages 1–5 / Horizon / Advanced) in one ScrollViewer + pinned PinnedFooterBar (Start Calculation, Clear All with ThemedDialog confirm); p_exit override shown only for 2+ stages with Core [0,1) boundary blocking Start; stage rows resize live; `MainWindow` DataContext moved to a new `MainViewModel`; new `ConfigFieldViewModel` + `StageRow` VMs; blur-validation routed via `ConfigPanelValidation.ValidationKey` attached property; §6 refreshed to 213 tests; D-096..D-100 | **Ubuntu 24.04** (dotnet SDK 8.0.131) — dead-state Release build 0 errors/0 warnings; full suite 213 green (Core 85 / Data 58 / Cli 35 / App 35); real Linux launch 15 s alive with "Main window created." and 0 new crash-log entries; headless evidence `logs/screenshots/phase-4-config.png` |
| 2026-09-16 | **GUI rebuild Phase 3 — MainWindow shell** (`feat/gui-rebuild`): header bar + TabControl [Simulation | Input Analysis | Token Generator | Help]; Simulation tab = 380px config / GridSplitter / fill results; new reusable `Controls/PlaceholderContent` + `Border.PanelCard`; ControlsDemo no longer hosted in MainWindow (screenshot test hosts it in its own window); §6 refreshed to 203 tests; §5 status updated (shell tabs, real panels pending P4–P6) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — dead-state Release build 0 errors/0 warnings; full suite 203 green (Core 85 / Data 58 / Cli 35 / App 25); real Linux launch 15 s alive with "Main window created." and 0 crash-log entries; headless evidence `logs/screenshots/phase-3-shell.png` |
| 2026-09-15 | **GUI rebuild Phase 2 — reusable controls** (`feat/gui-rebuild`): 9 controls in `src/OpdSimulator.App/Controls/` (ValidatedField, SearchableDropdown, ThemedDialog, ThemedToast, CollapsibleSection, InfoIcon, PinnedFooterBar, DataPreviewTable, ErrorBanner) + `Views/ControlsDemo` showroom inside MainWindow; template wiring moved to `OnApplyTemplate`+`INameScope.Find` (D-091); DataPreviewTable virtualises via ListBox, `Avalonia.Controls.ItemsRepeater` package dropped (D-090); PinnedFooterBar is a ContentControl template (fixes self-recursive content, D-092); ErrorBanner `IsVisible` mirrors Message (D-093); §6 refreshed to 197 tests | **Ubuntu 24.04** (dotnet SDK 8.0.131) — dead-state Release build 0 errors/0 warnings; full suite 197 green (Core 85 / Data 58 / Cli 35 / App 19); per-control headless tests + `logs/screenshots/controls-demo.png` render; two real bugs caught by the tests (ErrorBanner dead-control, PinnedFooterBar recursion) |
| 2026-09-15 | **GUI rebuild Phase 1** (`feat/gui-rebuild`): M5 view layer deleted (M6 chart files preserved on `feat/milestone-6-charts-and-token`); new App skeleton — `Assets/Theme.axaml` token contract (3 fonts, sizes 18/14/12, spacing 4/8/12/16/24, radii 4/8/12, 2 shadows, focus ring), `Assets/Motion.axaml` (150/200/250/600 ms + reduced→0), empty maximized `MainWindow`, CrashReporter relocated to `Services/`; packages changed — App drops `LiveCharts2` + `Serilog.Extensions.Logging`, App.Tests adds `Avalonia.Headless` + `Avalonia.Headless.XUnit` for headless smoke/render tests; §5 status updated (CLI still primary until GUI functional) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — Release build 0 errors/0 warnings; full suite 185 green (Core 85 / Data 58 / Cli 35 / App 7); real app launched for >10 s with 0 crash-log entries; |
| 2026-09-14 | M5-B: 8 reusable controls landed in `src/OpdSimulator.App/Controls/` (D-080); new `tests/OpdSimulator.App.Tests` (20 pure-logic tests, no Avalonia session) added to the sln; §6 refreshed to 196 tests; §8 layout updated | **Ubuntu 24.04** (dotnet SDK 8.0.131) — full suite 196 green, 0 warnings; App project builds standalone |
| 2026-09-14 | M5-A: §1 prerequisites add Linux system libs row (libx11-6 libice6 libsm6 libfontconfig1, D-079 — installed by owner, not the agent); §9 blank-window row replaced with cross-ref to §1 (one canonical apt command, AGENTS §10.7); §5/§8 status updated: App is now a real Avalonia shell (D-078), placeholder panels until M5-D | docs-only (owner installed libs; App launch smoke-tested 2026-09-14) |
| 2026-09-14 | M4: deterministic event trace — new `trace` command (§7.6) emitting ARRIVAL/START_SVC/END_SVC/ROUTE/EXIT (+ RNG draw rows at `--level rng`); golden fixture + regression tests (draw-by-draw RNG parity, stats cross-check, sink passivity, D-055..D-059); "CLI run requested" demoted to Debug so trace stdout is pure lines; §6 refreshed to 176 tests; §8 layout note for `src/OpdSimulator.Core/Trace/` | **Ubuntu 24.04** (dotnet SDK 8.0.131) — full suite 176 green, 0 warnings; `trace` (state/rng/events, `--output`, unstable refusal) live-run verified against the frozen fixture |
| 2026-09-13 | M3: `simulate-data` is stage-aware (per-stage μᵢ, p_exit, per-stage blocks, D-052), new `simulate-network` command with `--days`/`--start-day`/`--cap`/`--verbose` (D-053) and `--p-exit` routing; `samples/sample_3stage_clinic.csv` tracked; §7.4/§7.5 + §8 updated; M2 sweep c=2/3 waits refreshed (§7.4); 156 tests green (§6) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — full suite 156 green, 0 warnings; `simulate-network` (incl. `--days 5 --cap 80 --verbose`) and stage-aware `simulate-data` live-run verified; M1 regression live (29892/0.724/0.75); §7.4 c=2/3 refresh command run |
| 2026-09-13 | M2: CLI is a subcommand dispatcher — `simulate-params` (renamed M1 form), `verify`, `fit`, `simulate-data` (server sweep), `export` (§5/§7); Data layer lands (loaders, validator, preprocessing, fitters, chi-square); sample files committed + regenerable via `scripts/make-sample-data.sh` (§8); 97 tests green (§6) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — dead-state restore/build/test pass, 0 warnings; verify/fit/simulate-data/export live-run verified, exit codes observed |
| 2026-09-13 | M1: CLI takes `--lambda/--mu/--servers/--horizon/--seed`, prints metrics + ρ, exits 0/1/2 (§5); tests exist (34 green, §6); headless mode updated to the rate-driven form, M2 data form noted (§7); Serilog.Sinks.File 7.0.0 + Core Serilog 4.4.0 + ProjectReferences noted (§3) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — dead-state restore/build/test + M1 CLI F2/F3 all pass, 0 warnings |
| 2026-09-13 | File created (starter template); OS corrected to Ubuntu 24.04 | Not yet verified |
| 2026-09-13 | Added §11 Resuming Work After a Session Ends (renumbered Changelog → §12) | Not yet verified |
| 2026-09-13 | Scaffold: solution + 6 projects created, NuGet pinned (D-026), appsettings.template.json + copy step in §3, §5 rewritten for CLI-until-M5, §8 layout updated, test-projects-empty note added to §6 | **Ubuntu 24.04** (dotnet SDK 8.0.131) — restore/build/test/CLI all pass, 0 warnings |
| 2026-09-13 | §3: config-copy step moved to top of First-Time Restore; .gitignore keeps template tracked via negation (D-027, D-030) | N/A (docs-only) |