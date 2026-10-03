**Last verified:** 2026-10-03 — Phase 8R.1 (per-stage, per-session backlog and drain series — D-193): Release **and** Debug builds 0 errors / 0 warnings (both `--no-incremental`), full suite **833 green in each** (Core 134 / Data 134 / Cli 35 / App 530). **No new package, project, SDK, env var, launch command or screenshot** — the three 8R frames are unchanged and still pending owner review (B-012). What a reader must know: **(1)** on a clinic week each row of the Backlog and drain table is now **that stage's own average** across sessions; before 8R.1 every row repeated one system-wide average, so two stages with different backlogs looked identical — the one case where the figure matters. **(2)** `StageMetrics.BacklogAtClose` and `DrainMinutes` still mean **the final session** and were not redefined; the new `BacklogAtCloseBySession` / `DrainMinutesBySession` carry one entry per operating session. **(3)** The calculations dialog therefore names both on a multi-day run — `final session` and `mean across N sessions` with a range, e.g. `(range 2-11)` — and prints the single figure on a one-session run. **(4)** The total-drain summary is the slowest stage's **final-session** drain, still `max()`, never a sum. To check by hand: `dotnet test tests/OpdSimulator.App.Tests --filter Phase8R`.
**Last verified:** 2026-10-03 — Phase 8Q.4 (results bottom buffer + always-visible event trace — D-184, and a preferences bug fixed on the way — D-185): Release **and** Debug builds 0 errors / 0 warnings (both `--no-incremental`), full suite **779 green in each** (Core 125 / Data 134 / Cli 35 / App 485), headless evidence `logs/screenshots/phase-8q-results-buffer-trace.png` (111,734 B, the app's declared 1200×760 — no size supplied by the test, D-166). **No new package, no new project, no new prerequisite, no launch-step change** — this is a layout change inside the results panel plus two theme tokens, so §§1–§3 are unchanged. What a reader must know: the **event trace is now pinned below the results widgets and is always shown**, and it has been **removed from the "Customise results" list**, which now lists **seven** widgets instead of eight. A saved `ui.json` from an older build that still lists the trace keeps working — the stale entry is ignored and cleaned up on the next widget change. **`WidgetPreferences.Load` was fixed (D-185):** it used to return an object bound to the *default* path (`~/.config/OpdSimulator/ui.json`) even when a different file was read, because `System.Text.Json` satisfies the public parameterless constructor. If you ever hand-edit that file, load it through `WidgetPreferences.Load(path)` as the app does; `Phase6c6EmptyStateTests` had been overwriting it on every test run and no longer does. To check the layout by hand: `dotnet test tests/OpdSimulator.App.Tests --filter ResultsPanelBufferTraceTests`. The widget scroller is **named `WidgetScroller`** in `Views/ResultsPanel.axaml`; several tests used to find it by guessing which `ScrollViewer` held the metrics, and that guess became false when the trace left the scroll body.
**Last verified:** 2026-10-03 — Phase 8Q.3 (Performance Measures section and the stability verdict — D-183): Release **and** Debug builds 0 errors / 0 warnings (both `--no-incremental`), full suite **771 green in each** (Core 125 / Data 134 / Cli 35 / App 477), headless evidence `logs/screenshots/phase-8q-performance-measures.png` (107,834 B, 1200×760). **No new package, no new project, no new prerequisite, no launch-step change** — the section is a rename of an existing Results group plus new content inside the panel that already scrolls, so §1–§3 are unchanged. What a reader must know: the per-stage results group is now headed **Performance Measures** (not "Overview"), and a screenshot test that asserts the group headings (`Phase8DScreenshots.ExpectedHeadings`) was updated to match — a test failing on that string is the heading list doing its job, not a regression. The stability verdict is decided by one pure function in Core (`StabilityClassifier.Classify`, `src/OpdSimulator.Core/Engine/StabilityVerdict.cs`); to check a boundary by hand, run `dotnet test tests/OpdSimulator.App.Tests --filter VerdictClassifier`. The red band is **unreachable from the GUI** (the engine refuses ρ >= 1 and Start is gated on it), so a red row in a hand-launched app means the gate was bypassed, not that the threshold is wrong.
**Last verified:** 2026-10-03 — Phase 8Q.2 (direct-to-doctor routing — D-179..D-182): Release **and** Debug builds 0 errors / 0 warnings (both `--no-incremental`), full suite **751 green in each** (Core 125 / Data 128 / Cli 35 / App 463), headless evidence `logs/screenshots/phase-8q-input-servers-warning.png` now rendered **in CI** from the committed `samples/sample_overcapacity.csv`. **No new package, project, SDK, env var or launch command**, so §§1-5 are unchanged and this entry supersedes the 8Q.1 "Last verified" line below it for build truth. **What it adds:** the engine now models a patient who goes **straight from Reception to the Doctor**, skipping Screening, via a `BypassProbability` (fitted from your file as `p_bypass`, or typed in the Parameters section; the field appears at 3+ stages). The bypass is a **second coin toss on the same service completion** that decides the screening exit, drawn **only when enabled** — so with bypass off every run is bit-identical and all FNV-1a64 sample hashes are unchanged. The per-stage arrival-rate formula is amended from a single-exit product to the **sum of every route reaching a stage** (`λ_doctor = λ₀·p_bypass + λ₀(1 − p_bypass)(1 − p_exit)`), which reduces to the old formula when bypass is off. **Two data-side corrections the owner ruled on:** `p_exit` is now fitted over the **screened** population only (it was diluted by direct-to-doctor traffic — 0.807 → 0.956 on the real capture), reported as `ScreenedPatients`; and blank `screening_*` on a bypass row is **accepted** instead of reported as an error, **only when a doctor record is present**. Reception blanks are still errors. **`samples/sample_bypass.csv` now loads clean** — that red banner documented for 8Q.1 is gone. **One committed fixture added:** `samples/sample_overcapacity.csv` (10 screening visits × 20 min = 200 busy minutes against 155 observed, 129 % at one server) so the over-capacity **frame runs in CI** rather than skipping on the uncommitted clinic capture; the real capture keeps its own explicitly-skipping frame as `phase-8q-real-capture.png`. **Owner visual inspection of the frames remains required** (§18) — this agent has no image input on this host (D-089); B-012 stands at twenty-two frames.
**Last verified:** 2026-09-29 — Phase 8O (observation window, dual arrival rates, Horizon split, calculations receipt — D-172..D-177): Release **and** Debug builds 0 errors / 0 warnings (both `--no-incremental`), full suite **693 green in each** (Core 114 / Data 114 / Cli 35 / App 430), headless evidence `logs/screenshots/phase-8o-input-single-session.png`, `phase-8o-input-multiday.png`, `phase-8o-horizon-clean.png`, `phase-8o-horizon-diagnostic.png`, `phase-8o-calculations.png`. **The clinic's opening hour was wrong in this project's own documents** — `AGENTS.md` and `CONTEXT.md` both said 09:00–11:00, and the clinic opens at **08:15**, so a session is **165 operating minutes** and every per-session count and window divisor was 45 minutes a day out, consistently, which is why no test could see it (D-172). **Data:** a new **optional** `session_date` column (`2026-09-14`, `2026-09-14 08:15`, `14/09/2026`) resets arrival ordering per session and supplies the observation window; a file without it is a single session, byte-for-byte the pre-8O path; Friday/Sunday rows validate clean but are excluded from the window. **Two arrival rates, both always shown** (D-173): MLE `1 ÷ mean(within-session gaps)` and window `arrivals ÷ selected window's operating minutes`, with the divergence stated in percent and MLE as the default. The **choice** travels on `SimulationParameters`, never the number, so a fitted value is never recorded as a typed one. **Horizon split** (D-174): the single `Time span` dropdown is **removed** — "1 week" meant 5 operating days for run length but **6 calendar days** for the generator count, so a five-day clinic ran for six, and no assertion caught it because each place's arithmetic was individually correct. A calendar run now takes length from **Days** alone; a diagnostic trace from a new **Duration** dropdown (1 hour / 15 minutes / Custom minutes…) **visible in diagnostic mode only**. **A calendar run does not read that field at all** — a mistyped value was silently refusing runs of a mode that never shows it. **The calculations receipt was fixed as well** (D-176), and this is the finding worth carrying: D-173 corrected the *run path* and left the mirror defect in the *receipt*, which reported the file's auto-detected λ for a run that used the selected window's — and printing the right number alone was not enough, because the line above still described a different window, so dividing the printed arrivals by the printed minutes would not reproduce the printed λ. The dialog now prints the **division** and names both windows apart. **Verified by reverting: 7 of 42 red with the fixes removed, 0 with them in place.** **Three tests were wrong rather than the code, and all three are recorded in `TODO.md` because each would have passed over a real defect** — notably the 1-hour duration test, which passed because 60 is *also* the placeholder value a calendar run carries. D-166 held: six **new** filenames, and `phase-8n-calculations.png` md5-verified unchanged (`3fdac621…`). **Owner visual inspection of all nineteen frames remains required** (§18) — this agent has no image input on this host (D-089); see `docs/BLOCKERS.md` B-012 for which frame to look at first. The 2026-09-29 Phase 8N and follow-up entries are retained below for history.
**Last verified:** 2026-09-29 — Phase 8N follow-up 2 (bottom buffer + per-server contribution — D-170, D-171): Release **and** Debug builds 0 errors / 0 warnings (both `--no-incremental`), full suite **620 green in each** (Core 114 / Data 92 / Cli 35 / App 379), headless evidence `logs/screenshots/phase-8n-calculations-buffer.png` and `phase-8n-contribution.png`. **Two owner-reported defects, one per ruling.** (1) *The last rows sat under the footer at maximum scroll.* A `ScrollViewer`'s own `Padding` is **not part of its scrollable extent** (D-142 measured `extent = content − 2 × padding`), so padding cannot buy clearance; the body now ends with a real `BottomBuffer` element inside the scrollable content, and the content sits in a `ThicknessSpaceL` `Border`, giving **50 px of clearance at maximum scroll** against the 32 px asked for. Verified by reverting the XAML: the buffer tests fail with **26.0 px** of clearance, pass with **50 px**. (2) *A bar's height is not the server's utilisation.* The utilisation chart is drawn at the **contribution** (`perServerUtilisation / serverCount`, D-160), so every surface that shows a per-server utilisation now shows its contribution beside it — chart tooltips (including the outlier tooltip), the "Per-server detail" rows, the calculations dialog's `UTILISATION` block, and the CLI. **No engine change and no new `StageMetrics` field**; the contribution is a display-time transformation of two figures the engine already produces. The block now prints a definition line, a per-server division (`6.37 ÷ (2 × 158.19)`) and a per-stage sum that the reader can check on screen. **One test-quality finding:** the clearance test originally asserted only "the last line is not *under* the footer" and **passed with the buffer deleted**, because the content `Border`'s padding alone gives 24 px — a green gate over the reported defect. It now asserts the owner's 32 px figure as a named constant, and the frame-writing test uses the same threshold. `phase-8n-calculations.png` (md5 `3fdac621…`) and `phase-8n-calculations-resized.png` are **frozen and untouched** (D-166); both new frames are new filenames. **Owner visual inspection of all four frames remains required** (§18) — this agent has no image input on this host (D-089). The 2026-09-29 Phase 8N and follow-up-1 entries are retained below for history.

**Last verified:** 2026-09-29 — Phase 8N follow-up (calculations dialog resize — D-169): Release **and** Debug builds 0 errors / 0 warnings (both `--no-incremental`), full suite **612 green in each** (Core 114 / Data 92 / Cli 35 / App 371), headless evidence `logs/screenshots/phase-8n-calculations-resized.png`. **The defect:** the owner found by hand that shrinking the dialog vertically made the Copy/Close buttons vanish and the body stop scrolling. The root was a **vertical `StackPanel`** — it arranges every child at its desired height and never shrinks one, so the last child (the footer) was laid out ~1900 px below the window bottom, and the unbounded `ScrollViewer` reported `Extent == Viewport` (nothing to scroll). **The fix:** the root is now `Grid RowDefinitions="Auto,*,Auto"` (title / body / footer), the footer is a sibling of the `ScrollViewer` in its own `Auto` row, `SizeToContent="Manual"` with `Height="800"`, and the `*` row is what bounds the body. Verified by reverting the XAML: **5 of 15 Phase 8N tests fail against the old layout, 0 against the fix.** Two new standing rules in AGENTS §10.6: a resizable dialog needs a test that *actually resizes it* (asserting the resize took effect, and asserting position rather than the `IsEffectivelyVisible` flag), and a scrolling region must be asserted `Extent > Viewport` at the default size too. `phase-8n-calculations.png` is **frozen and untouched** (md5-verified after the full gate) — it predates D-169 and is not a picture of the fixed layout; its writer is retired. **Owner visual inspection of both frames remains required** (§18) — this agent has no image input on this host (D-089). The 2026-09-29 Phase 8N entry is retained below for history.

**Last verified:** 2026-09-29 — Phase 8N (calculations dialog sizing and layout — D-165, D-166): Release **and** Debug builds 0 errors / 0 warnings (both `--no-incremental`), full suite **608 green in each** (Core 114 / Data 92 / Cli 35 / App 367), headless evidence `logs/screenshots/phase-8n-calculations.png`. The calculations dialog now opens in its own **800 px wide, resizable (640–1200 px) window** whose body is a real two-column layout (`Grid ColumnDefinitions="Auto,*"`), so values **wrap instead of being clipped**, and it grows to `MaxHeight=800` with a scrolling body so the Copy/Close buttons are always reachable — **superseded 2026-09-29 by D-169: the dialog no longer grows with its content** (see the follow-up entry above for why `SizeToContent` and user resize cannot coexist). `ThemedDialog` is unchanged and all four of its callers are unaffected. **Why the 8M frame did not catch this:** the Phase 8M screenshot test set `Width = 760` while the production path set none and ran at `ThemedDialog`'s 440 — the test rendered a dialog no user could see (D-166). Two standing rules now live in AGENTS §10.6: a test may not override a control's production sizing, and screenshot evidence is append-only. The 8M frame is **frozen** (md5-verified unchanged after the full gate); ten frames still await owner inspection — see `docs/BLOCKERS.md`. The 2026-09-29 Phase 8M verification is retained below for history.

**Last verified:** 2026-09-29 — Phase 8M (chart legibility, stage-identical colour, and a derivable "View calculations" — D-160 through D-164): Release **and** Debug builds 0 errors / 0 warnings, full suite **608 green in each** (Core 114 / Data 92 / Cli 35 / App 367), headless evidence `logs/screenshots/phase-8m-utilisation.png`, `phase-8m-utilisation-varied.png`, `phase-8m-queue-step.png`, `phase-8m-legend.png`, `phase-8m-calculations.png`. Three owner findings closed: the utilisation X axis now labels every category (LiveCharts2's non-null default `Labeler` shadows an attached `Labels` collection, and the one-series-per-server shape was the other half of the loss — D-161); the bars are now per-server **contributions** so a stage's bars sum to its stage utilisation, with an amber marker on top and a fixed `1 ÷ min(servers)` ceiling that cannot clip (D-160); the queue chart uses `StepLineSeries`, because a sloped segment draws a queue length the engine never recorded (D-162). `StageColourPalette.ForStageIndex` makes a stage the same colour in every chart, wrapping by hue past the fourth stage (D-163), and "View calculations" is a pure tested text builder rendered by the existing `ThemedDialog` with clipboard copy (D-164). Includes two defects the new control-level tests found: the hover formatters indexed a stage-local bar list with a run-wide point index (threw on the last bar of a 1/2/3 run), and the old ceiling came from the equal share rather than the server counts. **One thing outstanding and not fixable by this agent:** visual inspection of the six PNGs is owner-required — see `docs/BLOCKERS.md`.
**Earlier same day —** Phase 8J (per-stage service families reach Core: `SimulationParameters` + `SimulationCoordinator` + `ConfigPanelViewModel`, one additive Core `#if DEBUG` assertion — D-147/D-148) on top of Phase 8E (App-only bug fixes: one-stage `p_exit = 1.0` runs, brand-green dropdown focus ring, dropdown opens on the full list and mouse clicks commit, themed-dialog padding — D-139/D-140) on top of Phase 8D: Release build 0 warnings/0 errors and full suite **427 green** (Core 90, Data 58, Cli 35, App 244), including the 10 new Phase 8E tests (8 `Phase8EFixTests` + 2 `Phase8EScreenshots`); headless evidence `logs/screenshots/phase-8e-dropdown-open.png` and `phase-8e-clear-dialog.png`. **The 2026-09-26 pass did NOT re-run the dead-state restore, the CLI checks, or a real-display launch — those still stand on the 2026-09-18 full verification below, and the only source change since is App-only.** The 8D/8C/8B/8A/7D evidence listed below was verified on 2026-09-18 from a clean state: `phase-8d-final-layout.png`, `phase-8c-analytical.png`, `phase-8b-verification.png`, `phase-7d-input-empty.png`, `phase-7d-input-loaded.png`, `phase-7d-input-mismatch.png`, `phase-7d-config-strip.png`, plus the earlier 7C/7B/7A/6c PNGs (`phase-7c-manual-mode.png`, `phase-7b-stage-models.png`, `phase-7a-units.png`, `phase-6c-input-analysis.png`, `phase-6c-results-all.png`, `phase-6c-widget-toggled.png`, `phase-6c1-empty.png`, `phase-6c2-histograms.png`, `phase-6c3-chi-square.png`, `phase-6c4-utilisation.png`, `phase-6c5-charts.png`). M1 headless CLI verified: stable run (ρ 0.75) and clean unstable refusal (single-line stderr, no stack trace, exit 1 — ρ 1.25). M2 data CLI verified: `verify` (clean file → exit 0; dirty fixture → exit 1 listing all 5 issues), `fit` (prints params + chi-square, writes `logs/fit-*.json`), `simulate-data --servers 1,2,3` (three runs, exit 0), `export`. M3 `simulate-network` verified (incl. `--days 5 --cap 80 --verbose`). M4 `trace` verified against the frozen golden fixture (state/rng/events; `--output`; unstable refusal). **M5/Rebuild GUI verified: ava headless renders of MainWindow (phase-1 + controls-demo PNGs in `logs/screenshots/`); real-display launch/keyboard walk is owner-required on a machine with a display (this host is Wayland).** [Windows: TBD]
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

Expected: `Passed! - Failed: 0`. As of 2026-09-29 **620 tests pass**
(Core 114 / Data 92 / Cli 35 / App 379; the earlier counts are retained below
for history).

> **Run the gate in Debug as well as Release.** `StageSpec` carries the
> `ServiceRate == 1/Mean == μ` assertion under `#if DEBUG` (D-147), so a
> Release-only test run cannot observe it. Every phase's gate from 8K on is
> `dotnet build -c Release`, `dotnet build -c Debug`, then `dotnet test` in both
> configurations — **0 errors / 0 warnings and 620/620 green in each** as of
> 2026-09-29.
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
- `OpdSimulator.App.Tests` (262, headless Avalonia, GUI rebuild Phase 1–4, 4b, 4c, 5, 5c, 5c.4, 5d, 6c.1, 6c.2, 6c.3, 6c.4, 6c.5, 6c.6, 7A, 7B, 7C, 7D, 8B, 8C, 8D, 8E, 8F, 8G, 8H, 8I, 8J, 8K) — Avalonia.Headless
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
  **Phase 8C analytical M/M/c validation widget (12 tests + 1 screenshot):**
  `Phase8CValidationTests` covers the pure `AnalyticalValidationService`
  (`ComputeForStage` matches textbook M/M/1 and M/M/2 values and returns null
  when ρ ≥ 1, c = 0 or μ = 0; `Compare` returns nothing for a non-exponential
  service family, for an unstable stage, or for a transient horizon, and one
  row per stage when exponential families, ρ < 1 and a ≥ 100,000-minute
  steady-state run all hold) and the widget lifecycle (empty before a run;
  populated by a real 200,000-minute DiagnosticTrace run — λ=0.5, μ 0.8/0.6/0.4,
  servers 1/2/3, p_exit 0.4, seed 42, TraceLevel None — with every per-stage
  delta < 5%; explanatory empty state when the horizon is transient; cleared by
  Clear All; the rendered picker lists exactly eight widgets including
  "Analytical validation"). `Phase6c6WidgetSelectorTests` and
  `Phase6c6Screenshots` were updated in place 7→8 (results-in-one-frame window
  grew to 6200 px). The gate frame is saved as
  `logs/screenshots/phase-8c-analytical.png`. D-137.
  **Phase 8D final polish (2 tests + 1 screenshot):** `Phase8DTests` covers
  the config section-expansion commands (every section starts expanded;
  `CollapseAll` sets all five `IsXSectionExpanded` flags false; `ExpandAll`
  restores them) and `Phase8DScreenshots` walks the gate sequence — a real
  EnterManually run, the four renamed trace levels
  (`Minimal/Standard/Detailed/Debug`), Collapse All, then Expand All — and
  saves the final layout as `logs/screenshots/phase-8d-final-layout.png`
  (1200×3200). The `ResultsPanel.axaml` group headings (Overview, Server
  Performance, Charts, Statistical Validation, Simulation Verification,
  Analytical Validation, Event Trace) are visible only after a run
  (`HasRun`), so the welcome card is unchanged. D-138.

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

### 7.6 `trace --lambda λ --mu μ --servers c [--stages …] [--p-exit p] --patients n [--seed s] [--level minimal|standard|detailed|debug] [--output f] [--real-start HH:mm[:ss]]` — deterministic event trace (M4)

```bash
dotnet run --project src/OpdSimulator.Cli -- trace --lambda 3 --mu 4 --servers 1 --patients 5 --seed 42
```

Reruns the configured network and prints one line per state-changing point —
ARRIVAL / START_SVC / END_SVC / ROUTE / EXIT — stopping after `--patients` have
fully left the system, so a trace stays short and reviewable. Level control:
`minimal` (no rows), `standard` (core columns), `detailed` (default; adds the
server id and the `→ exit`/`→ next stage` destination), `debug` (adds one RNG
row per draw — `seed=42`, `draw#k U=0.6681 → service time 0.101 min …`). The
wall-clock column is
hours into the real anchor (default 08:15:00 via `--real-start`). Every number is
invariant-culture and the RNG stream is untouched (D-057), so the same seed
reproduces the same bytes and every row can be hand-checked against `−ln(U)/λ`.
Engine narration goes to the file logs only — stdout carries trace lines alone
(D-059). Exit 1 with a single stderr line for an unstable configuration
(ρ ≥ 1 at any stage); exit 2 for usage or file-write errors.

**Verified 2026-09-14 (Ubuntu 24.04):** the example command printed and matched
the frozen golden fixture `tests/OpdSimulator.Core.Tests/Fixtures/trace-5-patients.txt`
(draw-by-draw hand-verified against the reference `Random(42)` sequence);
`--level debug` showed `draw#1 U=0.6681` … and `draw#10 U=0.7613`; `--level standard`
dropped the state columns and RNG rows; `--output /tmp/t.txt` wrote the trace and
printed the confirmation line; an unstable config (`--lambda 5 --mu 1`) refused
with exit 1. (Level tokens renamed `events|state|rng` → `standard|detailed|debug`
with `minimal` added in Phase 8D; behaviour unchanged.)

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
│   ├── sample_3stage_clinic.csv     # committed 3-stage fixture (Reception→Screening→Doctor, p_exit 0.7)
│   ├── sample_multiday.csv          # committed 6-session fixture (Phase 8O, session_date column)
│   ├── sample_bypass.csv            # committed 3-stage fixture with 4 bypass rows — loads clean since 8Q.2 (D-179/D-180)
│   └── sample_overcapacity.csv      # committed fixture at 129 % of one server's capacity, for the over-capacity frame in CI (D-181)
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
| `samples/sample_bypass.csv` shows a red validation banner on load | **Fixed in Phase 8Q.2** — its 4 bypass rows now validate clean, so a banner here means a genuine regression in the blank-cell rules (D-180). If it reappears, check `DataValidator.StageMayBeBlankFor` and that each bypass row still has a `doctor_start`. |
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
| 2026-10-03 | **Phase 8R — cap field, effective λ, index-based bypass, backlog & drain** (`fix/phase-8r`, Core + App + docs): bypass becomes a stage index derived from the stage count (`S = n − 2`, `D = n − 1`), drawn at arrival when `S = 0` and on completion of `S − 1` otherwise, so a **two-stage capture runs instead of being refused** and no stage name is compared in the engine; the impossible case is now exactly `S > 0 && S − 1 == ExitStageIndex`. A **Maximum patients admitted per session** field (default 85, both calendar modes, blank = unlimited) sets `cap ÷ session minutes`, applied **only to Screening-bound admissions** — the bypass stream is never throttled — with the stability refusal reporting fitted AND cap-derived λ (D-190). `StageMetrics` gains `BacklogAtClose` and `DrainMinutes` with a per-session series, shown as a Performance Measures **Backlog and drain** table (D-191). The Phase 8J locked baseline now runs explicitly **uncapped**, with the reason in the test, because the new 85 default would otherwise rewrite a baseline recorded before the cap existed. Tests **+36 (789 → 825)**; Release and Debug 0/0. Evidence `logs/screenshots/phase-8r-{cap-field,backlog-drain,2stage-bypass}.png`. **Launch steps unchanged.** Check by hand: `dotnet test tests/OpdSimulator.App.Tests --filter Phase8R`. |
| 2026-10-03 | **Phase 8Q.4 — results bottom buffer + always-visible event trace** (`fix/phase-8q`, App + docs): the results grid becomes `Auto,*,Auto` and the event trace becomes a **sibling of the widget `ScrollViewer`** in row 2, `MaxHeight="240"` with its own nested scroller, so no scroll position can hide it; its heading is `Event Trace` and it is **removed from the Customise-results picker** (seven widgets, not eight). New theme tokens `SpaceXl`/`ThicknessSpaceXl` (32); the results bottom buffer is a trailing `Border` inside the scrollable content (not `ScrollViewer.Padding`, which sits outside the scroll extent — D-142), while the calculations dialog keeps `SpaceL` (24, D-170). The widget ScrollViewer is named `WidgetScroller`. **Bug fix D-185:** `WidgetPreferences.Load` returned the deserialised instance, whose `_filePath` came from the public parameterless constructor and was therefore always the *default* path — an object loaded from a temp file wrote to `~/.config/OpdSimulator/ui.json` and left the caller's file untouched; `Phase6c6EmptyStateTests` had been overwriting the developer's own preferences every run. Tests +8 (App 477 → 485): `ResultsPanelBufferTraceTests` (7) + `Phase8Q4Screenshot` (1). Evidence `logs/screenshots/phase-8q-results-buffer-trace.png`. **Launch steps unchanged.** Check by hand: `dotnet test tests/OpdSimulator.App.Tests --filter ResultsPanelBufferTraceTests`. |
| 2026-10-03 | **Phase 8Q.3 — Performance Measures section** (`fix/phase-8q`, App + Core, no new package): the per-stage results group is renamed from "Overview" to **Performance Measures** and gains a `#` stage-number column, a **stability** verdict per stage (green ρ < 0.9 / amber 0.9–1.0 / red ρ ≥ 1), and a caption naming the highest-ρ **bottleneck**. New `src/OpdSimulator.Core/Engine/StabilityVerdict.cs` (pure band function + `ClassifySet`) and `src/OpdSimulator.App/Services/StabilityBandPalette.cs`. **Launch steps unchanged.** `Phase8DScreenshots.ExpectedHeadings` was updated from `"Overview"` to `"Performance Measures"`. Evidence `logs/screenshots/phase-8q-performance-measures.png`. Check a threshold by hand: `dotnet test tests/OpdSimulator.App.Tests --filter VerdictClassifier`. |

| 2026-10-03 | **Phase 8Q.2 — direct-to-doctor routing** (`fix/phase-8q`, Core + Data + App + Cli + samples + docs): `NetworkTopology` gains `BypassProbability` / `BypassStageIndex` / `BypassDestinationIndex`, all constructor-validated, normalised to −1 when the probability is 0, refusing a destination that is not after its source and a source equal to the exit stage (`BypassEnabled`); `EffectiveArrivalRate` rewritten from a single-exit product to a **probability mass propagated over stages** (amends D-007); `Engine.RouteToNextStage` → `RouteTo`, the bypass draw added **after** the exit draw and **guarded by `BypassEnabled`** so no run with bypass off consumes an extra RNG value; `EndService` trace row moved **after** both decisions. `PExitCalculator` gains `ComputeBypassProbability`, a **screened-only** `p_exit` denominator, `PExitResult.TotalCandidates` → `ScreenedPatients` + `BypassExits`, and a throw when no patient was screened; `DataValidator.StageMayBeBlankFor` forgives exactly three patterns (Reception never, blank `doctor_*` on a Doctor departure, blank `screening_*` on a Doctor departure **only with a doctor record**); CLI JSON key `screenedPatients`. App: `SimulationParameters.PBypassOverride`, `RunOutcome.EffectiveBypassProbability`, coordinator precedence manual → fitted → 0, bypass applied only at 3+ stages, p_bypass field + blur validation, calculations-receipt lines. Tests +32 (App 456 → 463, Data 114 → 128, Core 114 → 125): `BypassRoutingTests`, `NetworkTopologyTests` bypass inflow/rho, `BypassCoordinatorTests`, `BypassValidityTests`, `PExitCalculator` denominator/bypass tests, `sample_overcapacity.csv` frame that **runs in CI**. **Three of my own test assertions were wrong and are recorded because each would have passed over a real defect:** two conservation assertions compared exact per-stage service counts (`PatientsServed` counts completions **at each stage**, so a patient screened then continuing is counted twice and the counts are not additive — now compared as flow rates over a stable horizon), and the Results-panel test compared service counts across stages for the same reason. **Docs:** D-179..D-182, PRD **v1.8.0** (FR-DATA-14, FR-SIM-11 added; FR-DATA-6 amended), REQUIREMENTS (75.3 %, 61/81), CONTEXT §1.2 + §5.4, USER_MANUAL (§7.1 blank-cell table, P-bypass field, changelog), VIVA_ANSWERS, PROGRESS, TODO, BLOCKERS. **Two documentation defects found while updating the docs, one of them mine:** `USER_MANUAL.md` had no 8Q.1 changelog row (pre-existing) — both phases are now recorded. `docs/CONTEXT.md` acquired a **duplicated §3-§5 block** (146 lines) *from this session's own scripted edit to that file*, because the edit anchored its end marker on a string that appears twice in the file; the duplicate was removed before the commit and §5.5-§5.7 preserved, so each section has one canonical copy (AGENTS §10.7). Attribution corrected 2026-10-03 after the owner asked for the self-caught defects to be confirmed in the diff — `98e83ac` has a clean 415-line `CONTEXT.md`. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release **and** Debug 0 errors / 0 warnings (`--no-incremental`); full suite **751/751 green in both** (Core 125 / Data 128 / Cli 35 / App 463); one new committed fixture and one new headless frame — **owner visual inspection remains owner-required**, this agent cannot read images |
| 2026-10-03 | **Phase 8Q.1 — recorded server count + historical utilisation** (`fix/phase-8q`, App + samples + docs): new `Services/HistoricalMetricsService.cs` (`ComputeFor`, `OperatingTimeFor`, `HistoricalStageMetrics`, `HistoricalOperatingBasis` — observed vs spanned divisor, chosen by whether `session_date` exists) + new `ViewModels/HistoricalMetricsViewModel.cs` (`HistoricalServerCountRow`, `HistoricalStageRow`, basis/percentage/division/warning text); `InputTabViewModel` gains `ServerCounts` (default 1, validated, cleared with the file), `HasServerCounts`, `HistoricalMetrics`, `SeedableServerCounts`; `ConfigPanelViewModel.SeedServerCountsFromHistory` writes matching `StageRows[].Servers`; `MainViewModel.OnUseForSimulationRequested` applies them **once**; `Views/InputTab.axaml` renders the fields (shared `ValidatedField`, `HelpAnchor="historical-server-counts"`) and the figures. Stage discovery **reuses** `StagePairDetector` + `ClinicStageOrder.Flow` — no second detector. New committed fixture `samples/sample_bypass.csv` (4 of 14 Screening departures converted to bypass, evenly spaced). Tests +26 (App 430 → 456): `Phase8Q1Tests` (23) + `Phase8Q1Screenshot` (3, incl. the D-169 resize test and the D-166 append-only frames). Docs: D-178, PRD v1.7.0, REQUIREMENTS, USER_MANUAL §7.1b, PROGRESS, TODO, BLOCKERS. **Deliberately NOT in this phase:** FR-DATA-6 `p_exit` denominator change, `PExitResult.TotalCandidates` → `ScreenedPatients`, bypass detection/routing, `p_bypass` wiring, per-server columns — all 8Q.2, so no requirement describes behaviour the code lacks. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release **and** Debug 0 errors / 0 warnings (`--no-incremental`); full suite **719/719 green in both** (Core 114 / Data 114 / Cli 35 / App 456); two new headless frames (D-089) — **owner visual inspection of the two PNGs remains owner-required**, this agent cannot read images |
| 2026-09-29 | **Phase 8O — observation window, dual arrival rates, Horizon split, calculations receipt** (`fix/phase-8o-window`, Data + App + docs): optional `session_date` column (`TimeParser` + `DataValidator`) with per-session arrival-order reset and closed-day handling; new `Services/ObservationWindowService.cs` + `ObservationWindow` record + `ObservationWindowSelection` enum; `DataBindingResult` gains `WindowLambda` / `ObservedWindow` / `SessionDates`; `DataAnalyzer` computes all three; new multi-day fixture `samples/sample_multiday.csv` (60 rows, 6 sessions, 54 within-session gaps) from `scripts/generate-multiday-fixture.py`; `SimulationParameters` gains `LambdaSource` and `WindowLambdaOverride`; `SimulationCoordinator` precedence manual → selected-window → binding window → MLE; `InputTabViewModel` owns the window selection, custom-hours validation and the dual-λ readout; `MainViewModel` owns the MLE/Window choice; `ConfigPanel` Horizon split; the custom-hours row uses the shared `controls:ValidatedField` with `HelpAnchor="observation-window"`; `CalculationsTextBuilder` reports the run's window λ and prints the division (D-176). Docs: D-172..D-177, PRD v1.6.0, REQUIREMENTS, AGENTS, CONTEXT, USER_MANUAL §6.11/§7.1/§7.1a, VIVA_ANSWERS, BLOCKERS, PROGRESS. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release **and** Debug 0 errors / 0 warnings (`--no-incremental`); full suite **693/693 green in both** (Core 114 / Data 114 / Cli 35 / App 430); six new headless frames rendered (D-089); **owner visual inspection of the six PNGs and a real-display launch remain owner-required** — this agent cannot read images |
| 2026-09-29 | **Phase 8N — calculations dialog sizing and layout** (`fix/phase-8n-dialog-sizing`, App-only + docs): (8N.1) `CalculationsTextBuilder.BuildRows()` returns `IReadOnlyList<CalculationRow>` (`Kind {Section,Field}`, `Label`, `Value`, `IndentLevel`), and **`Build()` is now rendered *from* those rows** rather than produced by a second traversal — so the clipboard text and the on-screen rows cannot disagree, and all 30 `Field()` call sites are byte-for-byte unchanged (D-165). `IndentLevel` is derived from the leading spaces the call sites already carry, and `NoRunMessage` is a single string both shapes read. (8N.2) New `Controls/CalculationsDialog.axaml`: a **dedicated `Window`**, not a `ThemedDialog` and not derived from one, declaring its own sizing in its own XAML — `Width=800`, `MinWidth=640`, `MaxWidth=1200`, `MinHeight=400`, `MaxHeight=800`, `CanResize=True`, **no fixed `Height`**. Body is a `Grid` with `ColumnDefinitions="Auto,*"` inside a `ScrollViewer`, so a long stage name widens the labels and a long value **wraps** rather than being truncated; section headings span both columns and drop the monospace `----` underline on screen. Footer is `*,Auto,Auto` (spacer, Copy, Close) with `MinWidth=100` and theme-token padding. `ThemedDialog` keeps its 440 px width and all four callers are unchanged. Building it exposed a latent defect — a dialog with `Rows` never assigned rendered a blank box instead of the no-run message — fixed in the constructor. (8N.3) **D-166 evidence discipline:** the Phase 8M screenshot test had set `Width = 760` while production ran at 440, so it exercised a configuration no user could reach. `Render_CalculationsDialog_SavePhase8mCalculationsPng` is **retired** (it built the replaced `ThemedDialog` path), deleted **before** the gate so the gate itself could not overwrite the evidence; `phase-8m-calculations.png` was verified **md5-identical** after the full Release *and* Debug run, and the new frame is `phase-8n-calculations.png` (107,896 B vs 69,212 B). Two standing rules added to **AGENTS §10.6**: a test may not override a control's production sizing, and screenshot evidence is append-only. B-012's wording is corrected — the 8M frame was **clean at 760 px**; the clipping was seen in the **running app at 440 px**. New `Phase8NTests` (11: six structural, four locking the refactor, one screenshot). Net **598 → 608** (App 357 → 367). Docs: D-165, D-166, AGENTS §10.6, BLOCKERS, PROGRESS, USER_MANUAL §6.11, REQUIREMENTS FR-UI-29. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release **and** Debug 0 errors / 0 warnings (`--no-incremental`); **608/608 green in both**; 8M frame md5-verified frozen across the whole gate; **owner visual inspection of `phase-8n-calculations.png` remains required** — this agent cannot read images |
| 2026-09-29 | **Phase 8M — chart legibility, stage-identical colour, and a derivable "View calculations"** (`fix/phase-8m-ui-clarity`, App-only + docs): (8M.1) `UtilisationChartService` emits each server's **contribution** (`PerServerUtilisation[i] / ServerCount`) instead of its raw utilisation, so a stage's bars sum to exactly the stage utilisation in the metrics table; per-server `Detail` rows and the fixed four-sentence `Caption` (D-160). `ChartControlBuilder.BuildUtilisationChart` draws **one `ColumnSeries` per stage** (null at foreign categories) plus a per-stage amber `ScatterSeries` marker on the same slot and a dashed equal-share `LineSeries` at `StageUtilisation / ServerCount`; the y-axis is **fixed** at `1 ÷ min(server count)`, taken from configuration so it cannot clip. (8M.2) The categorical X axis gets `Labeler = index => labels[index]` and `LabelsRotation = 30` — LiveCharts2 2.0.5 ships a **non-null default `Labeler`** that shadows an attached `Axis.Labels`, so assigning `Labels` alone renders nothing; the one-series-per-server shape was the second half of the label loss (D-161). (8M.3) The queue chart uses `StepLineSeries<ObservablePoint>` — the engine only changes a queue length at an event, so a slope draws a value the engine never recorded — ordered busiest-first (behind) with a `max + 1` axis (D-162). (8M.4) new `Services/StageColourPalette.cs`: `ForStageIndex` is the one answer to "what colour is this stage", reading `ColorChartSeries1..4` and hue-wrapping `15° × floor(i/4)`; `ChartTheme.axaml`'s `BrushChartSeries1..4` are repointed at those tokens, and the queue chart, wait histogram, utilisation chart and the new **dynamic stage legend** all call it (D-163). (8M.5) new pure `Services/CalculationsTextBuilder.cs`; the Results Overview row gains **View calculations**; `ThemedDialog` gains **optional** body content and a clipboard **Copy** button, so the message-only callers are untouched and there is still one dialog implementation (D-164, FR-UI-29). **Two defects the new control-level tests found, not inspection:** the hover formatters resolved a LiveCharts point index against the reporting stage's bar list, but every series spans all categories — the last bar of a 1/2/3 run threw `ArgumentOutOfRangeException` on hover; fixed with a named `ChartControlBuilder.BarAt(data, globalIndex)`. And the ceiling was derived from the tallest **equal share**, so a quiet single-server stage dragged it below a loaded sibling's outlier bar and the bar was drawn off the top of the plot. A stale method summary still describing one-series-per-server was corrected at the same time. **A passing-for-the-wrong-reason test was retired:** `QueueChart_UsesNativeStepLineSeries` located the builder by reflection and asserted nothing about the series drawn, which is the mistake that let 8M.2 hide — it now checks the concrete series type. New `Phase8MChartTests` (10), `Phase8MPaletteTests` (9), `Phase8MQueueAndCalculationsTests` (8), `Phase8MChartControlTests` (6, `[AvaloniaFact]` — the label defect is only observable on a rendered axis) + `Phase8MScreenshots` (5). Baseline 560 → **598** (Core 114 / Data 92 / Cli 35 / App 320 → **357**). Docs: D-160..D-164, PRD v1.6.0 (FR-UI-27/28/29 + FR-STAT-7 rendering clause), REQUIREMENTS, USER_MANUAL §6.4/6.5/6.10, VIVA_ANSWERS. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release **and** Debug builds 0 errors / 0 warnings (`--no-incremental`); full suite **598/598 green in both** configurations; all five headless frames re-captured after the ceiling change (D-089); **visual inspection of the six PNGs and a real-display launch remain owner-required** — this agent cannot read images |
| 2026-09-26 | **Phase 8J — App plumbing: per-stage service families reach Core** (`fix/post-merge-8e`, App + one additive Core `#if DEBUG` block + docs): `SimulationParameters` drops the stored `ServiceDistribution` constructor field and gains parallel `init` properties `ServiceFamilies` (`IReadOnlyList<DistributionSpec>`) + `ServiceRates` (`IReadOnlyList<double?>`), both defaulting to empty; `ServiceDistribution` survives as a derived, setter-less shim returning `ServiceFamilies[0].Family.ToString()` because `MainViewModel` reads it and is outside the phase's file set (8K removes it). `TryBuildRunParameters` builds both lists from the same `rate` value in the existing per-stage loop, so a family and its rate cannot disagree; `MapStringToFamily` is a private five-case switch. `SimulationCoordinator` resolves `ServiceRates[i] ?? ManualServiceRates[i] ?? FittedRateFor(...)`, passes μ through **unchanged** (no `1/Mean` inversion anywhere), and `RequirePerStageList` refuses a per-stage count mismatch naming both counts before the loop. Per-stage chi-square matches each data stage to its configured family by name. `StageSpec` gains one `#if DEBUG` assertion that a supplied spec's implied rate matches `ServiceRate` to a **relative** `1e-9 × |1/Mean|`, resolving D-146's deferral; compiled out of Release (D-147). **Byte-identity proved:** pre-8J output of the gate config captured via `git stash` and diffed — identical on all 8 system and all 3 per-stage metrics, last bit included. **Mutation test:** dropping the spec at coordinator construction left all 480 tests green, so `Coordinator_DeterministicFamily_ReachesEngineSampler` was added — it fails under the mutation (28/28 samples are Exponential draws 0.827/0.629/2.425 min vs the expected constant 1.25) and passes when restored (D-148). New `Phase8JTests.cs` (8) + `Phase8JScreenshot.cs` (1). Baseline 473 → **482** (Core 114 / Data 71 / Cli 35 / App **253 → 262**; all 253 pre-existing App tests unchanged). Evidence `logs/screenshots/phase-8j-same-behaviour.png`. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release **and** Debug builds 0/0; full suite 482 green in both configurations; headless gate frame rendered (D-089); **visual inspection of the PNG and a real-display launch remain owner-required** — this agent cannot read images |
| 2026-09-26 | **Phase 8E — four post-merge bug fixes** (`fix/post-merge-8e`, App-only + docs): (8E.1) the fitted `p_exit = 1.0` refusal in `SimulationCoordinator.Run` is gated on `hasDownstreamStage = parameters.StageNames.Count >= 2`, and a one-stage run passes `0.0` as the **routing** probability (the resolved fitted `1.0` is still reported) because `NetworkTopology` requires `< 1`; 2+/3-stage banner and the manual-override path unchanged (D-139). (8E.2) the `SearchableDropdown` focus ring is `BrushBrandGreen` + `ThicknessFieldBorder` via `TextBox:focus` / `TextBox:focus-visible`; the unfocused `BorderThickness=0` moved from a local attribute to a `TextBox` setter so the focus rule is not outranked. (8E.3) opening the dropdown clears the filter and shows the full list with the committed value re-marked; a new `ItemList.SelectionChanged` handler commits mouse clicks; `OnClearClick` nulls the value, clears the filter and keeps the full list open; all programmatic list mutations are wrapped in `_suppressListCommit`; `Popup.IsLightDismissEnabled` is now `False` and the control hooks its `TopLevel.PointerPressed` to close on an outside press (D-140). (8E.4) `ThemedDialog.axaml` outer padding 24, title→message 16 and message→buttons 24 as the `Spacing` of two nested `StackPanels`, button gap 12 — all theme tokens. New `Phase8EFixTests.cs` (8) + `Phase8EScreenshots.cs` (2). Baseline 417 → **427** (Core 90 / Data 58 / Cli 35 / App **234 → 244**). Evidence `logs/screenshots/phase-8e-dropdown-open.png`, `phase-8e-clear-dialog.png`. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release build 0/0; full suite 427 green; both headless frames rendered (D-089); interactive ×-click path still owner-confirmation |
| 2026-09-18 | **Phase 8D — final polish (Results grouping, trace rename, collapse/expand, reference docs)** (`feat/milestone-7-model-driven`, App-only + docs): `ResultsPanel.axaml` groups the run widgets under seven headings (Overview, Server Performance, Charts, Statistical Validation, Simulation Verification, Analytical Validation, Event Trace) inside the `HasRun` area only; no separate "Stage Performance" heading (the per-stage table shares the metrics card) and the "Charts" heading follows the existing physical widget order. `TraceLevel` members renamed **`Minimal/Standard/Detailed/Debug` preserving ordinals** (`None/Events/State/Rng`) — pure relabel, `Minimal` still collects nothing; CLI tokens `minimal|standard|detailed|debug` (default `detailed`), App default `Detailed`, `TraceLevelFromName` unknown → `Minimal` (D-138). `ConfigPanelViewModel` gains five section-expansion bools + `CollapseAll`/`ExpandAll` commands; `ConfigPanel.axaml` binds each section two-way and adds "Expand all"/"Collapse all" buttons above `§2 · Model`. New `docs/DEFINITION_OF_DONE.md`, `docs/WORKFLOW_DIAGRAM.md`, `docs/RESULTS_PANEL_STRUCTURE.md`; README links them. New `Phase8DTests.cs` (2) + `Phase8DScreenshots.cs` (1); seven test files updated for the enum rename; legacy M5 chart/event-log backlog rows reconciled. Baseline 414 → **417** (Core 90 / Data 58 / Cli 35 / App **231 → 234**). Evidence `logs/screenshots/phase-8d-final-layout.png` (1200×3200). | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release build 0/0; full suite 417 green; headless final-layout frame rendered (D-089) |
| 2026-09-18 | **Phase 8C — analytical M/M/c validation widget** (`feat/milestone-7-model-driven`, App-only): new pure `Services/AnalyticalValidationService.cs` (`ComputeForStage` Erlang-C; `Compare` returns one `ComparisonRow` per stage, or empty unless exponential arrivals + every stage exponential + every ρ < 1 + `result.OperatingTimeMinutes >= MinimumSteadyStateMinutes` = 100,000 min, D-137) and new `ViewModels/AnalyticalValidationViewModel.cs` (empty-until-run, sync `Apply` + background `ApplyAsync`, generation-guarded, long `EmptyMessage` naming all three conditions). The widget is the eighth Results key `analyticalValidation`: `ResultsPanelViewModel` (show/toggle/visible/migration), `WidgetPreferences` seed, `MainViewModel` shared instance + `ResetAll` clear + `ApplyAnalyticalValidation` on completion, `ResultsPanel.axaml` card + picker checkbox. New `Phase8CValidationTests.cs` (12 tests); `Phase6c6WidgetSelectorTests`/`Phase6c6Screenshots` updated in place 7→8. Baseline 402 → **414** (Core 90 / Data 58 / Cli 35 / App **219 → 231**). Evidence `logs/screenshots/phase-8c-analytical.png` (gate: EnterManually, DiagnosticTrace, 200,000-min horizon, λ=0.5, μ 0.8/0.6/0.4, servers 1/2/3, p_exit 0.4, seed 42; every per-stage delta < 5%). No Core/Data/Cli change. | **Ubuntu 24.04** (.NET SDK 8.0.131) — Release build 0/0; full suite 414 green; headless gate frame rendered (D-089) |
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