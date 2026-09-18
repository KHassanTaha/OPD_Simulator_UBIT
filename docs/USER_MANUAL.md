# User Manual — OPD Clinic Queue Simulator

**Version:** 1.0 (draft)
**Audience:** Non-technical users (clinic staff, students, evaluators)

> This file lives in `docs/` (owner decision 2026-09-13).

---

## 1. What This Program Does

The simulator predicts how patients move through an OPD clinic:

- **Reception** → tokens are issued.
- **Screening** → two tables screen patients.
- **Doctor consultation** → three doctors see referred patients.

It uses real historical data (from an Excel file) to estimate arrival and
service patterns, then runs many simulated days to show:

- How long patients wait at each stage.
- How busy each server is.
- Whether the system is stable (queues do not grow forever).
- How well the fitted distributions match the data.

You can also see a **token generator** that estimates how long a new patient
would wait.

---

## 2. Before You Start

- You need a computer running Windows or Linux.
- You need the sample data file: `samples/sample_patients.xlsx`.
- You do **not** need to install anything else if the app has been packaged
  for you. If you are running from source, see `docs/DEV_LAUNCH.md`.

---

## 3. Starting the Program

### If you have the packaged app
- Windows: double-click `OpdSimulator.App.exe`.
- Linux: run `./OpdSimulator.App` from the app folder.

### If you are running from source
See `docs/DEV_LAUNCH.md` §5.

### Running from source with the graphical interface

```bash
dotnet run --project src/OpdSimulator.App
```

This launches the full Milestone 5 desktop app. See §5 for the step-by-step
workflow and press **F1** inside the app for an in-program copy of this guide.

### Running without a GUI (headless)

The command-line tool supports the same experiments for scripting and
validation:

```bash
dotnet run --project src/OpdSimulator.Cli -- simulate-params --lambda 3 --mu 4 --servers 1 --horizon 10000 --seed 42
```

This simulates a single queue with one server where patients arrive at a rate
of 3 per minute (λ) and the server works at 4 per minute (μ). The screen shows:

- **Patients served** — how many patients finished service.
- **Average wait (min)** — how long patients queued on average.
- **Average queue length** — patients waiting at a typical moment.
- **Stage / Server utilisation** — fraction of time each server was busy (0–100%).
- **Throughput** — patients served per minute.
- **ρ = λ/(c·μ)** — traffic intensity; **must be below 1**.

**If ρ is 1 or more** the system cannot cope and the program refuses to run —
for example `--lambda 5 --mu 4` prints a single `Refusing to run: …` line (with
`ρ = 1.25`) and stops. This is an intentional refusal, not an error — no error
dialog or stack trace appears; the full detail is only written to
`logs/errors-YYYYMMDD.log`. Lower the arrival rate, raise the service rate, or
add servers (`--servers 2`).

Every run also writes a full event-by-event trace to `logs/app-YYYYMMDD.log` —
useful if you want to see exactly what happened.

---

## 4. The Main Screen

The window has a header and four tabs — **Simulation | Input | Token
Generator | Help**. The **Simulation** tab is split into two panels:

```
+----------------------------+--------------------------------+
|  LEFT: Configuration       |  RIGHT: Results                |
|  ------------------------  |  --------------------------    |
|  - Parameter mode          |  - Metrics table               |
|  - Inter-arrival dist.     |  - Utilisation chart (per-server) |
|  - Service dist.           |  - Queue-length-over-time chart |
|  - Servers per stage       |  - Waiting-time histogram (one stage) |
|  - Manual λ (optional)     |  - Chi-square results          |
|  - Data source status      |  - Event log (scrollable)       |
|  - Time horizon            |                                |
|  - Random seed             |                                |
|  - Daily cap (optional)    |                                |
|  - [Run Simulation]        |                                |
+----------------------------+--------------------------------+
```

The **Input** tab holds everything about the loaded data: the **Upload Data**
button, the **data preview** table, the validation banner, and the
distribution-fit charts. Until a data file is loaded the charts show a hint
instead: *"Load a data file to see fit analysis."*

Once a data file is loaded, the tab shows **two chart cards** per fitted
series, in fit order:

- A **histogram** of the observed values (green columns) with the **fitted
  distribution curve** (teal line) drawn over it. The curve is the fitted
  PDF scaled to the histogram's bin sizes — same bins the chi-square verdict
  used, so the chart and the "Chi-square goodness-of-fit" table in the
  results always agree.
- A **caption** under each chart title restating the fit: which family was
  fitted, its parameters, the chi-square statistic (χ²), degrees of freedom,
  the p-value, and the verdict ("Fail to reject" / "Reject").
- A **chi-square card** ("Chi-square: …") with the same observed bins as
  green columns next to the **expected** frequencies (teal columns) the test
  predicted. The bars sit side by side so you can see each bin's
  observed-vs-expected gap — the gaps that add up to the χ² statistic. Its
  caption repeats the verdict exactly as the results table shows it
  (χ² = …, df = …, p = … — Reject / Fail to reject).

The cards update automatically whenever you load (or clear) a data file and
whenever you change the Inter-arrival or Service distribution or the
significance level in the **Model** section. If a fit could not be computed
for a series, its card shows *"Fit unavailable for this series."* instead of
a chart.

---

## Config fields explained

The subsections below are the targets of the "?" help icons on every
configuration field (press **F1** anytime or click a "?" icon to jump here).

### Data source — two ways to configure

At the top of the configuration panel, a **Data source** dropdown chooses how
the simulation gets its parameters:

- **Fit from an uploaded data file** (default) — upload a file on the
  **Input** tab; the app fits λ and each stage's μ from it. The Simulation
  tab's **Data source** strip shows *"Using file: …"* and a **Manage input →**
  link to that tab, and the **Manual μ per stage** comma-list override is shown.
- **Enter parameters manually** — no file needed. The comma-list μ override is
  hidden, the per-stage **Service rate μ** fields become editable, and p_exit
  defaults to **0.4** (you can change it). The strip reads *"Entering
  parameters manually"*.

Both paths are complete configurations. **Start Calculation stays disabled
until the chosen path is complete**, and the banner above the Start button
names exactly what is missing (for example *"Cannot start: upload a usable
data file, Reception has no μ."*). Switching the dropdown back and forth does
not lose values you already typed.

### Arrival rate

The arrival rate λ₀ (patients per minute) arriving at **Reception**. In
**Rate-wise** mode enter the rate directly (e.g. 0.5 = one patient every two
minutes). In **Mean-wise** mode enter the mean inter-arrival time in minutes
(e.g. 2), which the app converts via λ = 1/mean.

### Parameter mode

How you write the numbers you type into the manual λ/μ fields:

- **Rate-wise** — λ and μ as events per unit time (the default).
- **Mean-wise** — 1/λ and 1/μ as time per event; the app inverts them
  internally (rate = 1/mean).

The radio now sits at the top of the **Parameters** section, directly above
the **Time unit** selector and the manual λ/μ fields, so the mode and the
units you are typing in are chosen together. Enabling the Parameters toggle
turns both on.

### Time unit

The unit your manual λ/μ entries are written in: **Minutes** (default),
**Seconds**, or **Hours**. It applies to the manual Parameters fields only —
the simulation engine always works in per-minute values, so the app converts
your input at the parameter boundary (Seconds are multiplied by 60, Hours
divided by 60, per event; in Mean-wise mode the mean is inverted first, then
converted). Values fitted from an uploaded file are unaffected.

### Inter-arrival distribution

The probability distribution fitted to the inter-arrival times of the
uploaded file (or used to generate arrivals when no file is loaded).
**Exponential** was the default in every recorded clinic dataset; the Fitted
histogram tab shows how well it describes your data.

### Service distribution

The distribution family fitted to each stage's service times from the
uploaded file. When no data file is loaded this family is used to **generate**
service durations at the service rates you enter.

### Service rate

Service rate μ per server per stage (patients per minute). With **c** servers
in parallel the stage capacity is `c × μ`, so keep ρᵢ = λᵢ/(cᵢ·μᵢ) below 1 —
the app refuses to run an unstable stage (FR-VAL-1).

Where μ comes from depends on the **Data source** you chose. In every case the
stage row shows a read-only label telling you the source:

- **`μ = 0.50 (from data)`** — fitted from the uploaded file's
  `<stage>_start`/`<stage>_end` times.
- **`μ = 0.50 (manual)`** — taken from the **Manual μ per stage** comma list,
  in stage order (rate-wise or mean-wise per the parameter mode).
- **`μ = 0.50 (per-stage)`** — typed into that row's **Service rate μ** field.
- **`μ = — (no source)`** — none available; Start stays disabled and the
  banner names the stage until you supply one.

In **Fit from an uploaded data file** mode the comma list is the bulk override,
and a fitted rate wins over a comma-list entry for the same stage; a row whose
stage the data does not cover shows an editable **Service rate μ** field as a
fallback. In **Enter parameters manually** mode the per-stage **Service rate
μ** fields are the single entry point and the comma list is hidden.

### Significance level (α)

The level at which the chi-square goodness-of-fit verdicts are made (default
**0.05**). Must be strictly between 0 and 1. The results panel's chi-square
caption always shows the α used.

### Servers

The number of parallel servers c at each stage. The clinic reference model
uses **Reception 1, Screening 2, Doctor 3**, but you can change them to
experiment (e.g. an M/M/1 baseline single stage).

### Per-stage model setup (Kendall notation)

Each stage row has a **Model** dropdown with the standard Kendall shortcuts.
Picking one fills the row in for you — no separate typing:

| Code | Meaning | Sets |
|---|---|---|
| **M** | exponential (Markovian) | the arrival or service family |
| **D** | deterministic | the arrival or service family |
| **G** | general | the arrival or service family |
| **/c** | server count | the row's **Servers** field |

Example: choosing **M/M/2** sets exponential arrivals, exponential service
and **Servers = 2**. The 11 shortcuts are M/M/1–M/M/5, M/D/1–M/D/3, D/M/1,
D/M/2 and G/G/1.

**Advanced** (the toggle on the same row) turns the shortcut off and reveals
two extra dropdowns — **Arrival distribution** and **Service distribution** —
so you can set the two families independently. Turn it back off to return to
the single Model shortcut.

Two things to know (current behaviour, not a fault):

- Arrivals come from one stream, so the whole network uses the **first
  stage's** arrival family; and today all stages **share one service
  family** — changing a later stage's family is remembered in the form but
  does not yet change the engine's service sampling.
- **Deterministic** and **General** are offered for notation completeness.
  The engine currently samples **exponentially** for every stage, so an
  M/D/1 or G/G/1 run behaves like M/M/1.

### Horizon

How long to simulate. Pick a **Time span** preset:

- **15 minutes** / **1 hour** — a short window inside one clinic day (the
  run-mode stays **Single day**).
- **1 day** — one full clinic operating session.
- **1 week** — six consecutive operating days (Mon/Tue/Wed/Thu/Sat plus the
  following Monday, so a full clinic week runs).
- **1 month** — 26 operating days (30 calendar days at five operating days
  per week, with a small buffer).
- **Custom days** — type the number of consecutive clinic days in the
  **Custom days** field that appears.

Day+ spans automatically switch the run to **Multi-day** and set the day
count; **Single day** uses the short-span window. The **Diagnostic trace**
run mode is separate: it runs a raw window of minutes from the start of
arrival generation (the **Horizon (minutes)** field).

### Seed

The random-number seed. The same seed + the same inputs reproduce the exact
same run (FR-VAL-3), which is what makes validation against the M/M/c
formulas possible.

### P-exit

The probability that a patient exits after **Screening** instead of queueing
for the Doctor stage. Leave it empty to fit it from the `departure_stage`
column of your file; type a number (e.g. 0.4) to override the fitted value.

---

## 5. Step-by-Step: Run a Simulation

1. **Choose the data source, then the parameter mode.**
   At the top, pick **Fit from an uploaded data file** (default) or **Enter
   parameters manually** (no file needed). Then select **Rate-wise** (λ, μ) or
   **Mean-wise** (1/λ, 1/μ). This must match the data you are about to upload.

2. **Select the inter-arrival distribution.**
   Default: **Exponential** (recommended for arrivals).

3. **Select the service distribution.**
   Default: **Exponential**. Other options: Normal, Lognormal, Gamma, Uniform.

4. **Set the significance level (α).**
   Default **0.05**. Must be strictly between 0 and 1; the chi-square verdicts
   in the results panel use it.

5. **Set number of servers per stage.**
   - Reception: `1`
   - Screening: `2`
   - Doctor: `3`
   Each stage row also shows a read-only **service-rate μ label** — "(from
   data)", "(manual)", "(per-stage)", or "— (no source)" — telling you where
   its rate will come from (see *Service rate* above). In **Enter parameters
   manually** mode each row also shows an editable **Service rate μ** field.

6. **(Optional in Fit mode) Enter a manual λ.**
   If you enter a value, the simulator will still fit the data and show both
   results side by side. The manual value is used in the simulation. In
   **Enter parameters manually** mode λ is **required** — Start stays disabled
   until it is a positive number.

7. **(Optional in Fit mode) Enter manual service rates μ.**
   In **Fit from an uploaded data file** mode this is a single comma-separated
   list in the **Parameters** section, in stage order (rate-wise or
   mean-wise per the mode toggle; a trailing blank uses the fitted value). In
   **Enter parameters manually** mode the comma list is hidden — type each
   stage's μ into its row's **Service rate μ** field instead.

8. **Open the "Input" tab and click "Upload Data"**, then choose your Excel
   or CSV file. The **Input** tab is the data workspace: it holds the upload
   button, the **data preview** table, any validation banner, the
   distribution-fit charts, and (when the file's stages differ from the
   configured list) the stage-mismatch warning. The Simulation tab's
   **Data source** strip shows which file is in use and links back here with
   **Manage input →**.
   The file must contain one row per patient with columns:
   - `arrival_time`
   - `<stage>_start`
   - `<stage>_end`
   - `departure_stage` (`Screening` or `Doctor`)

   If the file has problems (missing values, wrong columns), the app will
   list them and ask you to clean the data.

   If the file's stages differ from the configured list, an amber warning
   appears on the Input tab with two buttons — **Sync stages from data**
   (adopt the file's stage names and count) or **Keep current stages**
   (dismiss the warning; uncovered stages keep "— (no source)" and the run is
   refused until they gain a rate). The Simulation strip shows a one-line
   *"Stages differ from data — see the Input tab."* reminder until you decide.

9. **Choose a time span and run mode.**
   The **Horizon** section starts with the **Time span** dropdown
   (15 minutes / 1 hour / 1 day / 1 week / 1 month / custom days). Day-or-longer
   spans switch the run to Multi-day and set the day count automatically
   (1 week = 6 operating days, 1 month = 26); a custom span shows a
   **Custom days** field. Below it is the run-mode picker with three options:
   - **Clinic day** — one operating session (Monday–Thursday or Saturday,
     08:15 start, services continue past 11:00 until they finish). *Default.*
   - **Multi-day** — N consecutive operating days (Friday/Sunday are skipped).
     Shows the fields **Days**, **Start day**, and **Daily patient cap**.
   - **Diagnostic trace** — a fixed-length run in minutes, used to walk
      DES correctness line by line. Shows **Horizon (minutes)** (default
     `10000`) and the **Trace level** dropdown (Minimal / Standard / Detailed / Debug).
     Clinic-day and multi-day runs also record an event trace — it appears in
     the right panel's **Event trace** widget, most detailed in diagnostic mode.

10. **(Diagnostic trace only) Set the trace level.**
    "Minimal" records nothing; "Standard" records arrivals, service start/end,
    routes and queue changes with core columns; "Detailed" (default) adds the
    serving server and the exit/next-stage destination; "Debug" also prints
    every random-number draw, so you can replay any run by hand. Same seed →
    same bytes.

11. **(Multi-day only) Set the horizon and cap.**
    Days, start day, and an optional daily patient cap (blank = no cap).

12. **Set the random seed** (default `42`, under the **Advanced** section).
    Same seed = same results. Change it to explore variability.

13. **Click "Start Calculation".**
    The right panel fills in a few seconds later.

14. **Reset everything with "Clear All".**
    The footer's **Clear All** button asks for confirmation, then returns the
    app to the fresh-launch state: every field back to default, the uploaded
    file unloaded, the results panel cleared and the welcome card shown
    again. (Which result widgets you chose to show or hide are kept.)

The run is refused — with an explanation banner, never silently — if no
arrival rate is available (you entered no λ in **Parameters** and loaded no
data), if the fitted `p_exit` is 1.0 (every patient exits after Screening, so
no one reaches the Doctor stage — override `p_exit` in **Parameters**), if any
stage has ρ ≥ 1 (unstable: arrivals outpace service), or if a stage has no
service rate (its banner names the stage: upload data covering it or enter a
manual μ in **Parameters**).

---

## 6. Reading the Results

Before the first run, the right panel shows a **welcome card** (project, course,
members, professor). It is replaced by the results the moment you start a
calculation. If the run was refused, an error banner explains exactly why.

The widget selector (a "Customise results" toggle at the top of the right
panel) shows or hides the individual Results widgets below it: the **metrics
table**, the **per-server utilisation chart**, the **queue-length-over-time
chart**, the **waiting-time distribution**, the **chi-square results**, the
**simulation verification** (§6.8), the **analytical validation** (§6.9), and
the **event trace**. Your choice is
remembered between sessions. (The **data preview** moved to the **Input** tab
in Phase 7D and is no longer a Results widget.)

The charts live on two tabs and answer two different questions — see §6.7 for
how to tell them apart and read the data-derived ones.

### 6.1 Metrics Table
For each stage:

| Metric | Meaning |
|--------|---------|
| Average wait | Mean time patients spent waiting in queue (minutes) |
| Average queue length | Mean number of patients waiting |
| Server utilisation | Fraction of time each server was busy (0–100%) |
| Throughput | Number of patients served |

### 6.2 Chi-Square Results
Shows how well the chosen distribution fits the data:

- **χ² statistic** — lower is better.
- **Degrees of freedom** — depends on bin count and parameters.
- **p-value** — if below 0.05, the fit is rejected.
- **Decision** — "Accept" or "Reject".

### 6.3 Event Log
Chronological trace of every event, rendered only by a **Diagnostic trace**
run (§5 step 7). Choose the trace level in the Horizon section to include
queue-state snapshots and (at `Debug`) random-number draws.

### 6.4 Per-Server Utilisation Chart
Shows what the engine's own servers actually did during a run (one bar per
server, grouped by stage). Until a run finishes it shows
*"Run a simulation to see utilisation."*

How to read it:

- A **green** bar means that server worked roughly like its
  stage-mates. The bar turns **amber** when a server deviates from its
  stage's **average utilisation** by more than **0.15** — e.g. one doctor
  doing far more (or far less) work than the other two.
- Hover a bar for the tooltip: the server's utilisation plus the exact gap
  ("Above average by 17.5%" / "Below average by 17.5%").
- The thin line across each stage marks that stage's average utilisation.
- This chart shows what the stage-level numbers hide: stage utilisation is
  the *mean* of all its servers, so a very busy plus a very idle server can
  average out to "looks fine".

The utilisation chart is a **Results** widget: it describes a run, so it
lives on the Simulation tab next to the metrics, not on the Input
tab with the data-derived figures.

### 6.5 Queue-Length-over-Time Chart
Shows, minute by minute, how many patients were waiting at **each stage's
queue** during a run (one coloured line per stage). Until a run finishes it
shows *"Run a simulation to see queue length over time."*

How to read it:

- Each line jumps when a patient joins or leaves that stage's queue, so a
  steady low line means the stage is coping and a climbing curve means queues
  are building up (a sign the ρ ≥ 1 instability check should have caught).
- Long runs are **downsampled** to at most 2000 points per stage so the
  chart stays readable. The caption notes *"downsampled from N samples"*
  when that happens. Downsampling keeps the first and last points and the
  tallest queue peaks, so you can still see how bad the worst moment was.
- The legend and the tooltips name the stage for each line. The X axis is
  minutes since the sim's start time.

### 6.6 Waiting-Time Distribution
Shows how long patients actually **waited in each stage's queue** during a
run — the spread, the typical wait, and the long tail. Until a run finishes
it shows *"Run a simulation to see the waiting-time distribution."*

How to read it:

- The **Stage** dropdown picks one stage at a time (the stages can differ a
  lot: doctor waits are usually longer and more skewed than reception
  waits). It resets to the first stage whenever you start a new run.
- The bars are **16 equal-width bins** across that stage's waiting times.
  A tall bar near the left means most waits were short; a long thin right
  tail means a few patients waited far longer than the rest.
- Tick **Log-scale Y axis** if the tail is so long that the short-wait bars
  look invisible. The Y axis then becomes logarithmic, and empty bins are
  dropped (log 0 is undefined) — they show as gaps, which is a true
  absence, not a zero.

Both widgets are **Results** widgets: they describe a run, so they live on
the Simulation tab next to the metrics, never on the Input tab
(which shows the data-derived fit charts instead).

### 6.7 Reading the Charts — Data-Derived vs Run-Derived

The charts answer two different questions, and each one appears on
exactly one tab:

| Chart | Tab | Describes | Where the numbers come from |
|-------|-----|-----------|-----------------------------|
| Inter-arrival / per-stage service histogram + fitted PDF | Input | the **loaded data** | MLE fit + chi-square bins (§7.3) |
| Chi-square observed-vs-expected bars | Input | the **loaded data** | the goodness-of-fit test (§6.2) |
| Per-server utilisation bars | Results | a **run** | `logs`/engine last run (§6.4) |
| Queue length over time | Results | a **run** | engine last run (§6.5) |
| Waiting-time distribution | Results | a **run** | engine last run (§6.6) |
| Simulation-verification histogram + chi-square | Results | a **run** | the engine's own generated samples (§6.8) |

So: if a figure describes the file you uploaded, it is on **Input**;
if it describes what the simulator just did, it is on **Results**. The
Results tab's "Customise results" selector never hides Input charts —
that set is fixed by the data and always shown together.

**Histogram + fitted PDF (Input).** The bars are the observed
frequencies in the same bins the chi-square test uses. The smooth curve is the
fitted probability density scaled onto the same axis: the closer the curve
tracks the bar tops, the better the chosen distribution fits. A curve sitting
well above the bars on one side and below on the other warns you *before* you
read the p-value that the fit is poor.

**Chi-square observed-vs-expected bars (Input).** One pair of bars
per bin — observed (O) and expected (E) frequency. Large gaps in a few bins
are what drive the χ² statistic up; a flat, evenly matched profile is a good
fit. The verdict text under the chart repeats the p-value decision from §6.2,
so the picture and the number always agree.

**Blank/placeholder states.** Before a file is loaded the Input tab
says *"Load a data file to see fit analysis."* Before a run the three Results
charts each show their own *"Run a simulation to see …"* message. If a chart
cannot be drawn, the widget falls back to that same text rather than crashing
the run — the numbers in the metrics table are always present.

### 6.8 Simulation Verification (Output-Side Chi-Square)

This widget checks the simulator itself, not the data file. After a run it
takes the inter-arrival and per-stage service times the **engine actually
generated**, fits the distribution you configured, and runs a chi-square
goodness-of-fit test on those generated values. Until a run finishes it shows
*"Run a simulation to verify its output."*

How to read it:

- One card per series: **Inter-arrival time** plus one per stage
  (e.g. *Reception service time*, *Screening service time*, *Doctor service
  time*), each with a histogram of the generated values and the χ² p-value.
- **p > 0.05** means the engine's output is consistent with the distribution
  you configured — verification passes. A collapsing p-value would mean the
  engine is not drawing from the distribution it claims, which is an
  implementation bug, not a data problem.
- **Deterministic** stages are skipped with a note (a constant stream has no
  distribution to fit). **General** is treated as Exponential for verification
  and says so. A stage with too few generated samples shows
  *"Insufficient samples for chi-square."* instead of inventing a verdict.

**Input chi-square vs output chi-square (§6.2 vs §6.8).** The Input tab's
chi-square validates that our MLE fit is a good model of the *historical*
data — that is input modelling. This widget validates that the simulation
*engine* generates values matching the configured distribution — that is the
standard model-verification step. The first checks the assumption; the second
checks the implementation.

This is a **Results** widget: it describes a run, so it lives under
"Customise results" on the Results panel, never on the Input tab.

### 6.9 Analytical Validation (M/M/c)

This widget is the simulator's **analytical check**: it compares the
simulated per-stage wait and queue length against the closed-form M/M/c
(Erlang-C) values for the same arrival rate, service rate and server count.

The closed-form formulas only apply under **all three** of these conditions:

1. **Exponential** inter-arrival and service times (M/M/c).
2. Every stage **stable**, i.e. ρ < 1 at each stage.
3. A **steady-state run** — an operating time of at least **100,000
   simulated minutes** (about 70 operating days).

If any condition fails, the widget stays on its empty state and says why:
*"Analytical comparison requires a steady-state run — exponential service,
ρ < 1 at every stage, and a horizon of at least 100,000 simulated minutes
(~70 operating days). Clinic-day runs are transient and will not match M/M/c
formulas."*

Why the horizon condition: M/M/c results describe long-run (steady-state)
behaviour. A single 2-hour clinic day is a **transient**, so the simulated
averages would differ from the formula for statistical reasons even when the
engine is perfectly correct. To use the widget, run a **Diagnostic trace**
(§5 step 7) with the **horizon** set to at least `100000` minutes, then start
the calculation.

How to read the table — one row per stage:

| Column | Meaning |
|--------|---------|
| Sim wait | Simulated average wait in queue (minutes) |
| M/M/c wait | Closed-form average wait, Wq (minutes) |
| Sim queue | Simulated average queue length |
| M/M/c queue | Closed-form average queue length, Lq |
| Δ% | \|simulated − analytical\| as a percentage of the analytical value |

A small Δ% (a few percent) means the engine agrees with queueing theory on
that configuration — that is the validation. This widget is a **Results**
widget: it needs a finished run, so it lives under "Customise results", never
on the Input tab.

---

## 7. Loading Real Data (from an Excel or CSV file)

The graphical interface can upload a data file on the **Input** tab and fit
it from there. The same operations are available from the terminal — validated,
distribution-fitted, goodness-of-fit checked, and simulated — without opening
the GUI, which is useful for testing and for the viva.

### 7.1 Required File Format

One row per patient, with these columns:

| Column | Meaning |
|--------|---------|
| `arrival_time` | When the patient arrived (e.g. `8:17`) |
| `<stage>_start` | When service began at that stage (e.g. `screening_start`) |
| `<stage>_end` | When service ended at that stage (e.g. `screening_end`) |
| `departure_stage` | Exit stage: **`Screening`** or **`Doctor`** (case-insensitive) |

Times can be `8:17`, `08:17`, `8:17:30`, or `8:17 PM` style. Arrivals must not go
backwards, and each stage's `_end` must not be before its `_start`. A ready-made
example is `samples/sample_patients.xlsx` (or `.csv`).

### 7.2 Check your file first

```bash
dotnet run --project src/OpdSimulator.Cli -- verify --file samples/sample_patients.csv
```

A clean file prints `File is valid: 60 row(s), 1 service stage pair(s).`
A file with problems prints **one line per problem** (each naming its row number),
then `Validation failed: …` — fix and re-upload before simulating.

### 7.3 Fit a distribution and run goodness-of-fit

```bash
dotnet run --project src/OpdSimulator.Cli -- fit --file samples/sample_patients.csv --stage all
```

This fits the **Exponential** distribution (or choose `--family normal`,
`lognormal`, `gamma`, `uniform`) to the inter-arrival times and to each stage's
service times, then prints:

- **Fitted parameters** (e.g. rate) and the sample mean.
- **Log-likelihood** and **AIC** (lower = better).
- **χ², df, p, decision** — if the p-value is below 5%, the fit is **Reject**ed;
- **p_exit** — the fraction of patients exiting after Screening (used for routing).

A machine-readable copy is written to `logs/fit-YYYYMMDD-HHMMSS.json`.

### 7.4 Simulate a whole day from your data

```bash
dotnet run --project src/OpdSimulator.Cli -- simulate-data --file samples/sample_patients.csv --servers 1,2,3 --seed 42 --horizon 10000
```

The program reads λ = 1/mean inter-arrival and μ = 1/mean service from your file,
then runs one complete simulation **per server count**. For each count it prints a
metrics block (see §6.1) incl. **ρ = λ/(c·μ)**. Compare the counts to see how much
waiting extra servers remove. Curve counts that are unstable (ρ ≥ 1) are refused
with a single clear line.

**Multi-stage files (3 stages).** If your file contains more than one service stage
(columns like `reception_*`, `screening_*`, `doctor_*`), the program orders the
stages by the clinic flow (Reception → Screening → Doctor), fits each stage's own
μᵢ, estimates `p_exit` from the `departure_stage` column, and runs **one** network
simulation — `--servers` then takes exactly one count per stage in that order:

```bash
dotnet run --project src/OpdSimulator.Cli -- simulate-data --file samples/sample_3stage_clinic.csv --servers 1,2,3 --seed 42 --horizon 500
```

You will see a per-stage block for Reception/Screening/Doctor, the network totals,
and the fitted `p_exit`. A stage not visited by some patients (a Screening exit has
no doctor times) may leave those cells blank — the program accepts them.

### 7.5 Simulate a network from parameters (no data file)

```bash
dotnet run --project src/OpdSimulator.Cli -- simulate-network --lambda 0.2 --c 1,2,3 --mu 0.5,0.25,0.2 --p-exit 0.7 --days 5 --cap 80
```

Run the named 3-stage clinic directly, without uploading data: `--lambda` is the
arrival rate, `--c`/`--mu` give one server count and one service rate per stage,
`--p-exit` the probability of leaving after Screening. Add `--days N` to simulate
real clinic days (open Mon–Thu + Sat, arrivals 08:15–11:00, `--start-day` and
`--cap` optional) or `--horizon <minutes>` for the classic fixed-window run (the
two modes are mutually exclusive). `--verbose` prints each stage's ρᵢ before the
run. If any stage has ρ ≥ 1 the program refuses with one line naming every
unstable stage.

### 7.6 Trace a short run line-by-line (M4)

```bash
dotnet run --project src/OpdSimulator.Cli -- trace --lambda 3 --mu 4 --servers 1 --patients 5 --seed 42
```

Reruns the network you configure and prints one line per event — arrival, service
start/end, route, exit — stopping after `--patients` have fully left the system.
Use `--level debug` to also print every random-number draw with its sampled value
(`draw#1 U=0.6681 → service time 0.101 min`); same seed → same bytes (FR-VAL-3).
By default the trace prints to the terminal; add `--output trace.txt` to save it
to a file instead. The full flag list is in **DEV_LAUNCH §7.6**.

### 7.7 Regenerating the sample files

The committed samples were generated deterministically (seed 42). To regenerate
them and the validation fixture: `bash scripts/make-sample-data.sh`.

---

## 8. The Token Generator Tab

Shows a visual token for the next arriving patient:

- **Token number** — sequential.
- **Estimated wait** — based on current queue length and service rate.

*(This feature is being finalised and may appear as a preview in this version.)*

---

## 9. Common Errors and Fixes

| Message | Meaning | What to do |
|---------|---------|------------|
| `ρ ≥ 1 at stage X` | The system is unstable — arrivals outpace service | Lower the arrival rate, add servers, or use different data |
| `Nothing to run yet: enter an arrival rate λ…or load a valid data file.` | No λ is available from Parameters or the loaded data | Enter a manual λ (§5 step 5) or upload data |
| `p_exit = 1.0: every row…exits after Screening…` | Fitted `p_exit` is 1.0, so no patient reaches the Doctor stage | Load different data, or override `p_exit` below 1 in Parameters |
| `File is missing required columns` | Excel file does not match expected format | Check column names; see §7.1 |
| `Row N: missing value` | Dirty data | Clean the row in Excel and re-upload |
| `Departure stage 'X' must be 'Screening' or 'Doctor'` | Invalid exit stage (ui) | Fix the `departure_stage` cell to `Screening` or `Doctor` |
| `Validation failed: N issue(s)` | The file has data problems | Run `verify` again; every issue lists its row number |
| `Parameter mode mismatch` | You selected Rate-wise but the data looks Mean-wise | Change the mode or re-check your data |

---

## 10. Getting Help

- Read `docs/CONTEXT.md` for the theory behind the simulator.
- Read `docs/DEV_LAUNCH.md` if the app will not start.
- Report bugs by adding an entry to `docs/BLOCKERS.md`.

---

## 11. Glossary

- **λ (lambda)** — Arrival rate.
- **μ (mu)** — Service rate.
- **ρ (rho)** — Traffic intensity; must be < 1.
- **M/M/c** — Standard notation for a queueing model.
- **p_exit** — Probability a patient exits after Screening.

---

## 12. Changelog

| Date | Change |
|------|--------|
| 2026-09-18 | Phase 7D: the **Data** section and the old **Input Analysis** tab are merged into one **Input** tab (tabs are now **Simulation \| Input \| Token Generator \| Help**). The upload button, data preview table, validation banner, distribution-fit charts and the stage-mismatch warning all live on the Input tab. The **data preview** is no longer a Results widget (the "Customise results" list drops it). The Simulation tab's Data section becomes a **Data source status strip** showing which file is in use (or "Entering parameters manually") with a **Manage input →** link to the Input tab, plus a one-line reminder when the file's stages differ from the configured list. §4, §5 step 8, §6 and the "Data source" field description updated |
| 2026-09-18 | Phase 7C: a **Data source** dropdown at the top chooses between **Fit from an uploaded data file** (default — Data section + comma-list μ override shown) and **Enter parameters manually** (Data section hidden; per-stage **Service rate μ** fields become editable; p_exit defaults to 0.4). **Start Calculation is now a completeness gate** — it stays disabled and a banner names exactly what is missing (D-128, supersedes the old "runnable in principle" behaviour). See §Config fields → "Data source" and "Service rate" |
| 2026-09-18 | Phase 7B: each stage row now has a **Model** dropdown (Kendall shortcuts like M/M/2, M/D/1, G/G/1) that sets the row's server count and distribution families, plus an **Advanced** toggle that reveals independent **Arrival distribution** / **Service distribution** dropdowns (§Config fields → "Per-stage model setup"). Deterministic/General are notation-only placeholders — the engine still samples exponentially |
| 2026-09-18 | Phase 7A: manual λ/μ inputs now take a **Time unit** selector (per minute/second/hour) with the **Parameter mode** radios moved next to them at the top of the Parameters section, and the Horizon section gained a **Time span** preset (15 min / 1 hour / 1 day / 1 week / 1 month / custom days) covering §Config fields → "Time unit" / "Horizon" |
| 2026-09-17 | Phase 6c.4: the **Simulation** tab's results now include a **Per-server utilisation** widget (§6.4) — one bar per server, amber when a server deviates from its stage's average utilisation by more than 0.15, with a thin stage-average reference line. It appears once a run finishes ("Run a simulation to see utilisation." before that) and is toggleable via "Customise results" (§6) |
| 2026-09-16 | Phase 6c.3: each fitted series now shows a second card in the **Input Analysis** tab — a chi-square card ("Chi-square: …") plotting the observed (green) vs expected (teal) frequencies per bin side by side, with the verdict caption repeated exactly as the results table shows it (§4) |
| 2026-09-16 | Phase 6c.2: the **Input Analysis** tab now plots the loaded data — one histogram card per fitted series (observed columns + fitted-PDF overlay, bins shared with the chi-square verdict) with a fit/χ² caption, updating automatically on data load/clear and on distribution / significance-level changes (§4) |
| 2026-09-16 | Phase 6c.1: the **Input Analysis** tab is no longer a placeholder — it now shows a themed hint ("Load a data file to see fit analysis.") until a data file is loaded; distribution-fit charts replace this hint in Phase 6C (see §4) |
| 2026-09-16 | GUI run flow live (rebuild Phase 5): three run modes — Clinic day / Multi-day / **Diagnostic trace** — in the Horizon section; welcome card on first launch; results widgets (metrics, chi-square, trace, data preview) with a "Customise results" toggle; refused runs show an explanation banner instead of failing silently |
| 2026-09-14 | M4 CLI: new `trace` command (§7.6) prints a deterministic line-by-line event trace (ARRIVAL/START_SVC/END_SVC/EXIT, plus RNG draw rows with `--level rng`); use it to walk through any simulation by hand before the viva |
| 2026-09-13 | M3 CLI: `simulate-data` now runs multi-stage files (per-stage μᵢ, p_exit, one network run — §7.4) and a new `simulate-network` command with `--days`/`--start-day`/`--cap`/`--verbose` (§7.5); blank doctor cells for Screening exits are accepted; multi-stage runs print a per-stage block and network totals |
| 2026-09-13 | Added "Loading Real Data" (§7) — the M2 CLI path: `verify`, `fit`, `simulate-data`, required file format, error messages; renamed the headless command to `simulate-params` (§3) |
| 2026-09-13 | Added "Running without a GUI" section (§3) — the M1 CLI path with the metrics explained, the ρ < 1 rule, and the event trace location |
| 2026-09-13 | Initial draft |