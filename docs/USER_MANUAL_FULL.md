# Full User Manual — OPD Clinic Queue Simulator

**Version:** 1.0 · **Date:** 2026-11-11 · **Phase 8X**
**Audience:** anyone using the simulator — students, clinic staff, evaluators.

> This is the complete step-by-step guide: every workflow, every control, and
> what each control does. The compact reference is
> [`docs/USER_MANUAL.md`](USER_MANUAL.md); the theory is in
> `docs/CONTEXT.md`; build/launch instructions are in `docs/DEV_LAUNCH.md`.

---

## 1. What the simulator does

It is a **discrete-event simulation** of a hospital outpatient (OPD) clinic
queue. Patients arrive over time, wait in queues, are served at up to five
sequential stages, and leave. The simulator replays that process thousands of
times faster than real time and reports the measures a clinic manager cares
about: average waiting time, queue lengths, server utilisation, and how long
the day runs.

Two ways to configure it — **both are first-class**:

| Path | What you provide | What the simulator does |
|------|------------------|-------------------------|
| **A · Fit from data** | A CSV or XLSX file of real patient records | Fits distributions to your data (λ, per-stage μ, p_exit), tests each fit with chi-square, then simulates |
| **B · Enter manually** | Nothing but numbers you type | Simulates directly from the λ, μ, families and server counts you enter |

You can mix them: load a file and then override individual numbers in the
Parameters section. The engine always runs in **minutes**; a separate dropdown
changes only the **unit the fits are reported in**.

---

## 2. Starting and stopping

### Graphical app

```bash
dotnet run --project src/OpdSimulator.App -c Release
```

Wait a few seconds; the window opens maximised. Close it with the window's ✕,
or `Alt+F4`. Logs are written to `logs/` next to the repository.

### Command line (no GUI)

```bash
dotnet run --project src/OpdSimulator.Cli -- <command>
```

Every CLI workflow — verify a file, fit distributions, simulate from data or
parameters, print a line-by-line trace, validate against M/M/c theory — is
documented with exact commands in `docs/DEV_LAUNCH.md` §7 and
`docs/USER_MANUAL.md` §3.

---

## 3. The window at a glance

```
+--------------------------------------------------------------+
| 🏥  OPD Clinic Queue Simulator — Simulation & Modelling      |
+--------------------------------------------------------------+
| [ Simulation ] [ Input ] [ Help ]                            |
+------------------+-------------------------------------------+
|  Configuration   |  Results                                   |
|  (left, ~380px)  |  (right, fills the window)                 |
|                  |                                           |
|  Data source     |  Run header                                |
|  2 Model         |  Performance Measures                      |
|  3 Parameters    |  Per-stage metrics                         |
|  4 Stages        |  Charts (utilisation, queue, waits)        |
|  5 Horizon       |  Chi-square · verification · validation    |
|  6 Advanced      |  Event trace table                        |
|  [Start ▶]       |                                           |
+------------------+-------------------------------------------+
|  ← drag the splitter to widen either side                    |
+--------------------------------------------------------------+
```

- **Tabs** — switch with a click, or `Ctrl+Tab`-style tab navigation (Tab
  reaches the tab headers first, then the active tab's content).
- **Splitter** — drag the bar between Configuration and Results.
- **First launch** shows the **welcome card** in the results panel and every
  field at its default. Nothing from a previous session is restored.

---

## 4. Complete walkthroughs

### Workflow A — your first simulation in ten steps (no data file)

1. Launch the app (§2). The **Simulation** tab is already selected.
2. At the top, set **Data source** → **Enter parameters manually**.
3. In **2 · Model**, leave **Inter-arrival distribution** = `Exponential`.
4. In **3 · Parameters**, type **Manual λ (per minute)**, e.g. `0.5`
   (one patient every two minutes on average).
5. In **4 · Stages**, each row shows **Service rate μ**: type a positive
   number per stage, e.g. `0.8`, `0.6`, `0.4`.
6. If you have two or more stages, type **p_exit override**, e.g. `0.4`
   (the share of patients who leave after Screening).
7. Check the **Utilisation ρ per stage** line: every ρ must be **below 1**.
   If it reads 1.0 or more, raise μ or add servers — the simulator refuses
   unstable queues on purpose.
8. In **5 · Horizon**, leave **Single day** (one 165-minute clinic session).
9. Optional, **6 · Advanced**: type a **Random seed** (e.g. `42`) so the same
   configuration always reproduces the same run.
10. Click **Start Calculation**. The run executes on a background thread — the
    window never freezes — and the results panel fills in.

### Workflow B — simulate from your own data file

1. Click the **Input** tab.
2. Click **Upload Data** and choose your `.csv` or `.xlsx` (format in §7.1 of
   the compact manual; a sample lives at `samples/sample_3stage_clinic.csv`).
3. Read the **data preview**: every row the file loaded, with invalid rows
   outlined in red and the reason in a tooltip. A red banner names the
   problems; hover it for details.
4. Read the **Arrival rate (λ)** card. It shows **two estimates** —
   **MLE** (1 ÷ mean gap while patients were arriving) and **Window**
   (arrivals ÷ the whole operating window). Pick one under **Use for this
   run**. If they differ a lot, the note explains which is higher and why.
5. Below, the **Distribution fit** cards show a histogram and the fitted
   curve for the inter-arrival times and each stage's service times, each with
   its chi-square verdict (χ², df, p, Reject / Fail to reject).
6. Return to the **Simulation** tab. Set **Data source** → **Fit from an
   uploaded data file** (it is the default). The status strip names the file.
7. Check **4 · Stages** matches your file's stages (the strip warns you if the
   counts differ, with a link back to the Input tab).
8. Optional: override any fitted number in **3 · Parameters** — a blank field
   falls back to the fitted value; a typed value wins.
9. Click **Start Calculation**. Read the results (§6 below).

### Workflow C — simulate several clinic days

1. Configure either path (A or B) as above.
2. In **5 · Horizon**, choose **Multi-day**.
3. Type **Operating days**, e.g. `5`. Closed days (Friday and Sunday by
   default) are skipped and do not count.
4. Optionally pick **Start day** (Monday…Saturday) — it decides which days
   the clinic week covers.
5. Optionally set **Maximum patients admitted per session** (default 85) to
   cap how many patients enter screening per session.
6. **Start Calculation**. The results header names every session the run
   covered, and the queue-length chart can show one session at a time.

### Workflow D — line-by-line diagnostic trace

1. Configure a run as usual.
2. In **5 · Horizon**, choose **Diagnostic trace**.
3. Pick a **Duration** (default 1 hour) or **Custom duration (minutes)**.
4. In **6 · Advanced**, set **Trace level** — `Minimal`, `Standard`,
   `Detailed` (every event and calculation) or `Debug`.
5. **Start Calculation**. The event trace table in the results panel lists
   every simulation event: arrival, service start/end, exit — with clock time,
   patient, stage, server, queue length and detail. This is the mode to use
   when you want to prove the engine does what a DES should.

### Workflow E — read a fit in another unit

1. Load a data file (Workflow B, steps 1–2).
2. On the **Simulation** tab, in **2 · Model**, set **Distribution fitting
   unit** → `hours` (or `seconds`, `days`, `sessions`).
3. Switch to the **Input** tab: the histogram bins, fitted parameters, card
   titles and λ readout all move to the chosen unit; the chi-square verdict
   stays identical (a unit is a rescale, not a different sample).
4. The engine's outputs — waits, queues, backlog, the trace clock — remain in
   minutes regardless. The choice is never saved into a preset or a session.

### Workflow F — compare server counts (analytical check)

1. Run a simulation (any workflow).
2. In the results panel, find **Analytical validation (M/M/c, M/G/c, G/G/c)**.
3. Read the verdict: it names which model's assumptions your configuration
   satisfies and prints the theoretical figures beside the simulated ones.
4. Click **Run steady-state validation (100,000 min)** for a long-run
   comparison without replacing the clinic-day results on screen.
5. Click **View Calculations** in the run header to open the receipt: every
   number the run used and where it came from (fitted, manual or default).

### Workflow G — run without the GUI

See `docs/DEV_LAUNCH.md` §7 for the exact commands (`verify`, `fit`,
`simulate-data`, `simulate-network`, `simulate-params`, `trace`, `validate`).
The CLI takes the same parameters the GUI does, plus `--fit-unit` on `fit` and
`simulate-data`.

---

## 5. Control-by-control reference — Simulation tab

Everything on the left is grouped in collapsible sections. Click a header to
fold or unfold it; **Expand all** / **Collapse all** (above section 1) do the
whole panel. Every field's **? icon** links to its section of this manual.

### Top — Data source

| Control | What it controls |
|---------|------------------|
| **Data source** dropdown | Which of the two paths (§1) is active: **Fit from an uploaded data file** (default) or **Enter parameters manually**. The choice decides which fields below are editable and what Start requires. |
| Data status strip | Names the loaded file (or "Entering parameters manually"), with **Manage input →** jumping to the Input tab. Shows a one-line warning when the configured stages differ from the file's. |

### 2 · Model

| Control | What it controls |
|---------|------------------|
| **Inter-arrival distribution** | The family the engine samples inter-arrival times from. `Exponential`/`Poisson` = the classic Poisson process; `Gamma`, `Lognormal`, `Normal`, `Uniform`, `Deterministic` give a genuine G/G process. Changing it refits the Input-tab cards. |
| **Arrival spread** | The chosen family's spread parameter (σ, k, w). Only appears for families that have one; the arrival **mean is λ**, never typed here. |
| **Significance level (α)** | The cut-off for every chi-square test (default 0.05): p below α rejects the fit. |
| **Distribution fitting unit** | The unit fits are reported in — `minutes`, `seconds`, `hours`, `days`, `sessions` (Workflow E). Engine outputs stay in minutes. |

### 3 · Parameters (optional overrides)

Blank fields fall back to fitted values; typed values win.

| Control | What it controls |
|---------|------------------|
| **Parameter mode** radios | How λ/μ are typed: **Rate-wise** (events per minute) or **Mean-wise** (minutes per event). |
| **Time unit** | The unit of the λ/μ you type here (per minute / second / hour) — independent of the fitting unit. |
| **Manual λ (per minute)** | Overrides the fitted arrival rate. |
| **Manual μ per stage** | Comma list of per-stage service rates, used for stages the file does not cover. Hidden in manual mode (rows own their μ there). |
| **Utilisation ρ per stage** | Read-only: ρ = λ / (c·μ) per stage. All values below 1 are required for a stable queue. |
| **p_exit override** | Probability a patient leaves after Screening (0 ≤ p < 1). |
| **p_bypass override** | Probability an arrival skips Screening and goes straight to the Doctor. |

### 4 · Stages

| Control | What it controls |
|---------|------------------|
| **Default arrival/service family** + **Apply to all stages** | Families stamped onto **new** rows; the button writes them onto every existing row. |
| **Number of stages** | 1–5 sequential stages (default 3: Reception → Screening → Doctor). |
| Per-row **Model** | Kendall shortcut, e.g. `M/M/2` sets exponential arrival + exponential service + 2 servers. `G/G/c` asks the fitter to supply the family from data. |
| Per-row **Advanced** toggle | Reveals independent **Arrival distribution** / **Service distribution** dropdowns per stage. |
| Per-row spread fields (σ, k, w) | The chosen family's spread; μ stays the only location parameter. |
| Per-row **Servers** | Parallel servers at that stage (≥ 1). |
| Per-row **Service rate μ** | The stage's mean service rate (editable in manual mode; shows the fitted value otherwise, labelled with its source). |

### 5 · Horizon

| Control | What it controls |
|---------|------------------|
| Run mode radios | **Single day** (one 165-min session), **Multi-day**, or **Diagnostic trace** (Workflow D). |
| **Operating days** / **Start day** | Multi-day length and the first clinic day. Closed days don't count. |
| **Maximum patients admitted per session** | Daily cap on screening admissions (blank = unlimited). |
| **Duration** / **Custom duration** | The diagnostic trace's arrival window. |

### 6 · Advanced

| Control | What it controls |
|---------|------------------|
| **Random seed** | Fixed seed for reproducible runs; same seed + same configuration = identical run. |
| **Trace level** | How much per-event detail a diagnostic run records. |

### Footer

| Control | What it controls |
|---------|------------------|
| **Start Calculation** | Runs the simulation. It is a **completeness gate**: it stays disabled until every required input exists, and the banner above the footer names exactly what is missing (from your first click or keystroke in the panel — a fresh launch is not greeted by an error). |
| **Clear All** | Resets every field to the fresh-launch state, after a confirmation dialog. |

---

## 6. Control-by-control reference — results panel (right side)

The panel scrolls as **one sequence** from the run header down to the event
trace.

| Widget | What it shows |
|--------|---------------|
| Run header | The configuration that ran and a **View Calculations** button opening the full receipt. |
| **Performance Measures** | System totals (patients served, average wait, average queue length) plus a stability verdict and the bottleneck stage. |
| Per-stage metrics | Per stage: arrivals, served, average wait, average queue length, utilisation ρ, busy time. |
| **Per-server utilisation** | One bar per server; amber when a server deviates from its stage average. |
| **Queue length over time** | The run's queue lengths. With a multi-day run, the **Operating session** dropdown shows one session on a 0–165 minute-from-open axis; **All sessions** shows the whole run. |
| **Waiting time distribution** | Histogram of patient waits for a chosen **stage**. |
| Chi-square results | Input-side goodness-of-fit verdicts for the fitted distributions. |
| **Simulation verification** | Output-side chi-square: confirms the engine generates values matching the configured distributions. |
| **Analytical validation** | Workflow F. Compares the run with M/M/c, M/G/c or G/G/c theory. |
| **Event trace** (table) | Every event of a diagnostic-trace run; virtualised, selectable, copyable. |
| **Customise results** | Toggles which result widgets are visible (the event trace is always shown). |

---

## 7. Control-by-control reference — Input tab

| Control | What it controls |
|---------|------------------|
| **Upload Data** | Opens the OS file picker for a CSV/XLSX patient file (Workflow B). |
| **Clear File** | Unloads the file and clears preview and fit cards. |
| Status text | File name, row count, or the load failure summary. |
| **Data preview** | Read-only, sortable table of the loaded rows; invalid rows are outlined red with the reason in a tooltip. First sheet only. |
| **Servers** (per stage, when the file records them) | How many servers worked each stage; **Use for simulation →** copies them into the Stages configuration once. Historical utilisation is shown beside with its division. |
| **Arrival rate (λ)** card | Both λ estimates; the radio picks which drives the run. |
| **Distribution fit** cards | Histogram + fitted curve + chi-square verdict per series (inter-arrival, each stage's service). The unit comes from the Simulation tab's fitting unit (Workflow E). |

---

## 8. Help tab

Currently a placeholder for the in-program guide rendered from the embedded
manual (planned). Until then, this file and `docs/USER_MANUAL.md` are the
documentation.

---

## 9. Keyboard map

| Key | Action |
|-----|--------|
| `Tab` / `Shift+Tab` | Move forward/backward through every control, in visual order. |
| `Enter` / `Space` | Activate the focused button; `Enter` also commits a dropdown choice. |
| Arrow keys | Move the highlight inside an open dropdown; move between radio buttons. |
| `Escape` | Close a dropdown, dialog or popup without changing anything. |
| `Alt+F4` | Close the app. |

Every field has a visible label, a placeholder showing the expected format, a
hover tooltip, and — on numeric fields — its unit.

---

## 10. Where files are written

| Path | Contents |
|------|----------|
| `logs/app-*.log` | Full application log (every session). |
| `logs/errors-*.log` | Recoverable errors only. |
| `logs/crash-*.log` | Unhandled exceptions, with full stack trace. |
| `logs/fit-*.json` | Machine-readable copy of each distribution fit. |
| `logs/validation-report-*.txt` / `validation_report.txt` | Analytical validation output. |
| `logs/screenshots/` | Frames captured as evidence during testing. |

---

## 11. Troubleshooting quick table

Full message-by-message table: `docs/USER_MANUAL.md` §9.

| Symptom | First check |
|---------|-------------|
| Start stays disabled | The banner above the footer names the missing input; fill it or switch Data source. |
| Red banner on a row | Hover the field: the message states the cause and the expected value. |
| ρ ≥ 1 refused | Lower λ, raise μ, or add servers (Simulation tab · **3 · Parameters**). |
| File will not load | The banner names the first problem row; check the preview's red rows. |
| Charts show "Fit unavailable" | The series had too few usable values for that family. |

---

## 12. Glossary

- **λ (lambda)** — arrival rate, patients per minute.
- **μ (mu)** — service rate, patients per minute (1/μ = mean service time).
- **ρ (rho)** — utilisation, λ / (c·μ); below 1 means the queue is stable.
- **p_exit** — share of screened patients leaving after Screening.
- **p_bypass** — share of arrivals going straight to the Doctor.
- **M/M/c, G/G/c** — Kendall notation: arrival family / service family / servers.
- **χ² (chi-square)** — goodness-of-fit statistic; p ≥ α means the fit is accepted.
- **Session** — one clinic day of 165 operating minutes (08:15–11:00).
