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

The cards update automatically whenever you load (or clear) a data file,
whenever you change the **Inter-arrival distribution** or the **significance
level** in the **Model** section, and whenever you change any stage's service
family on its row (the **Service distribution** dropdown under the row's
**Advanced** toggle, or the **Default service family for new stages** dropdown
together with **Apply to all stages**). Each stage's card is tested against
*that stage's own* family, so the three service cards can disagree — that is
expected, not a bug. If a fit could not be computed for a series, its card
shows *"Fit unavailable for this series."* instead of a chart.

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

**Each stage has its own service family.** The three stages really are
sampled differently, so an M/M/1 + M/D/2 + M/G/3 network behaves as three
different models rather than three copies of M/M/1.

One field per family, for **spread** only. The mean service time stays the
**μ** you typed — it is the only location parameter, and the family never
moves it. Each family adds exactly one extra field describing how much the
service times vary:

| Family | Extra field | Meaning |
|---|---|---|
| Exponential (M) | — | memoryless; no extra field needed |
| Deterministic (D) | — | every service takes exactly the same time |
| Normal, Lognormal | **Standard deviation σ** | typical spread around the mean |
| Gamma | **Shape k** | spread as a shape; higher k is more predictable |
| Uniform | **Half-width w** | service times are spread evenly within mean ± w |

Only the one field your family uses is shown — the others are hidden, so a
Gamma row never asks you for a σ. **Uniform additionally refuses w ≥ mean**,
because that would put the lower bound at zero or below and a negative
service time is not a service time; the field explains this itself.

**G/G/c and General** mean "work the family out for me". Pick a `G/G/c`
shortcut, load a data file, and the simulator searches all six families for
the one that best fits that stage's historical service times, then:

- keeps your **G/G/c** on screen and shows a badge naming the family it chose;
- **fills in μ only if you left it blank** — a μ you typed is never overwritten;
- copies across the **spread** the winning family needs, so its field appears
  ready to edit;
- and if it cannot (no data loaded, no family fits, or the file does not cover
  that stage) it **changes nothing** and tells you which of those it was.

**Gamma and Uniform need a service rate before you can start.** Their spread
is measured *from* the mean service time — Gamma's scale is the mean divided
by its shape, and Uniform's bounds are the mean plus or minus its half-width —
so with no μ there is no number to apply the spread to. If you leave μ blank
on a Gamma or Uniform stage and nothing else supplies it (a data file that
covers that stage, or the per-stage μ list), the stage shows a red message
naming the three places you can enter it, and **Start stays disabled**. The
other four families have no such requirement.

One thing to know: arrivals come from one stream, so the whole network uses
the **first stage's** arrival family. Service families, by contrast, are set
per stage.

### Horizon

How long to simulate. **Two separate controls**, because two different
questions are being asked — an earlier version of this panel had one control
answering both, which is withdrawn (D-174).

**For a calendar run** (Single day or Multi-day), the length comes only from
the **Days** field. Multi-day also has a **Start day** (which weekday the run
begins on). The clinic runs 08:15–11:00 on Monday–Thursday and Saturday, so a
"day" here is one 165-minute operating session, not a calendar day: the
weekends are skipped rather than simulated as empty days.

**For a Diagnostic trace**, the arrival window comes only from the
**Duration** dropdown, which appears in that run mode and nowhere else:

- **1 hour** — the default, and the first option in the list.
- **15 minutes** — a short window.
- **Custom minutes…** — reveals a field; type whole minutes (e.g. `45`).

If you mistype the custom value and then switch back to a calendar run mode,
the run is **not** blocked by it: that field belongs to the diagnostic run
only, so a run you are entitled to perform never fails because of a control it
does not show.

### Seed

The random-number seed. The same seed + the same inputs reproduce the exact
same run (FR-VAL-3), which is what makes validation against the M/M/c
formulas possible.

### P-exit

The probability that a patient exits after **Screening** instead of queueing
for the Doctor stage. Leave it empty to fit it from the `departure_stage`
column of your file; type a number (e.g. 0.4) to override the fitted value.

The fitted value is a fraction of the **screened** patients only — patients who
went to the Doctor without being screened are not counted, because they never
reached the decision this probability describes.

### P-bypass

The probability that a patient goes **straight from Reception to the Doctor**,
skipping Screening. Some clinics let a patient walk past screening; the file
records those rows as a doctor visit with no screening times. Leave it empty to
fit it from your file (roughly 1 in 6 rows in a real capture); type a number to
override it.

The field appears once your network has **three or more stages** — there has to
be a stage to skip. On a shorter network the value is ignored and the run
reports a bypass of **0**, so the number in the receipt always matches what the
engine actually did.

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

9. **Choose the run mode, then the length.**
   The **Horizon** section starts with the run-mode picker (three options), and
   the length controls follow it — each mode has exactly one:
   - **Clinic day** — one operating session (Monday–Thursday or Saturday,
     08:15 start, services continue past 11:00 until they finish). *Default.*
     No length field: the length is the session.
   - **Multi-day** — N consecutive operating days (Friday/Sunday are skipped).
     Shows the fields **Days**, **Start day**, and **Daily patient cap**. The
     run length comes from **Days** and nowhere else.
   - **Diagnostic trace** — a fixed-length run in minutes, used to walk
     DES correctness line by line. Shows the **Duration** dropdown
     (1 hour — the default — / 15 minutes / Custom minutes…, which reveals a
     whole-minutes field) and the **Trace level** dropdown
     (Minimal / Standard / Detailed / Debug).
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
panel) shows or hides the individual Results widgets below it: the **Performance
Measures** group (§6.1), the **per-server utilisation chart**, the **queue-length-over-time
chart**, the **waiting-time distribution**, the **chi-square results**, the
**simulation verification** (§6.8), the **analytical validation** (§6.9), and
the **event trace**. Your choice is
remembered between sessions. (The **data preview** moved to the **Input** tab
in Phase 7D and is no longer a Results widget.)

The charts live on two tabs and answer two different questions — see §6.7 for
how to tell them apart and read the data-derived ones.

### 6.1 Performance Measures

The first group on the results side is headed **Performance Measures**. It holds
two readings of the same run: the figures, and a verdict on them.

**System totals** — the whole network in one place: total patients served and
total waiting time.

**Per-stage table** — one row per stage, numbered `#` from 1 in the order the
patient visits them:

| Metric | Meaning |
|--------|---------|
| # | Stage number, in visit order |
| Stage | The stage name (Reception, Screening, Doctor) |
| Served | Number of patients who completed service at this stage |
| Avg wait | Mean time patients spent waiting in this stage's queue (minutes) |
| Avg queue | Mean number of patients waiting |
| Utilisation | Fraction of the stage's server time that was busy (0–100 %) |
| ρ | Arrival pressure = λ ÷ (c × μ). See the verdict table below. |

**Stability** — the same stages again, each with its ρ and a verdict:

| ρ | Verdict | What it means |
|---|---------|---------------|
| below 0.90 | **Stable** | The stage has spare capacity |
| 0.90 up to (not including) 1.00 | **Near capacity** | Arrivals are close to what the servers can absorb; a small change in λ or μ will make this stage the bottleneck |
| 1.00 or more | **Unstable** | Arrivals meet or exceed capacity |

One line under the table names the **bottleneck** — the stage with the **highest
ρ**. If two stages tie, the earlier one is named. The bottleneck is the stage to
fix first: raising its server count or service rate relieves every stage after
it.

The verdict is written as words as well as coloured, so it can be read without
relying on colour.

You will not normally see **Unstable**. A network where some stage reaches
ρ ≥ 1 cannot run to a steady state, so the simulator refuses it and explains
which stage failed. The verdict is still defined so that a figure printed for a
stage measured at the edge shows the truth rather than a blank.

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

**What one bar means.** A bar is **one server's contribution to its stage's
utilisation** — the minutes that server was busy, divided by *(number of
servers in the stage × total operating time)*. It is deliberately not the
server's own utilisation, because of this consequence:

> **The bars of one stage always add up to that stage's utilisation** — the
> same number printed in the Performance Measures table (§6.1).

A three-doctor stage at 60% utilisation draws three bars of 20% each, and
20 + 20 + 20 = 60, which is the figure beside it.

**A bar's height is not the server's utilisation.** It is that server's
*share* of its stage. Wherever a per-server utilisation appears in this app —
the bar tooltip, the per-server list below the chart, the calculations dialog,
and the CLI report — **the server's own utilisation is shown next to it**, so
you never have to guess which of the two you are reading. Divide the
contribution by the stage's server count and you get the server's own
utilisation; on a one-server stage the two are equal.

How to read it:

- A bar's **colour is its stage**, not its value: the same stage is the same
  colour in this chart, in the queue-length chart, in the waiting-time
  histogram, and in the stage legend above the charts. A run with more stages
  than the palette has colours cycles through a hue-shifted variant rather
  than repeating one.
- A bar turns **amber** when that server deviates from its stage's **average
  utilisation** by more than **0.15** (15 percentage points) — e.g. one doctor
  doing far more, or far less, work than the other two. The amber marker sits
  **on top of** the bar, and the text below the chart repeats the same fact,
  so the colour is never the only cue.
- The **dashed line** across each stage marks the **equal-share benchmark**:
  what a server would contribute if the stage's work were split perfectly
  evenly. Bars above the line carried more than their share; bars below it
  carried less. The gap between a bar and its line *is* the imbalance.
- The Y axis is **fixed**, running from 0 to `1 ÷ (fewest servers in the
  run)`. It is deliberately not rescaled to fit each run, so two runs of the
  same clinic are directly comparable and no bar is ever cut off at the top.
  The trade-off is that a clinic with many servers at every stage gets a
  flatter-looking chart, because the honest ceiling for a six-server stage is
  one sixth.
- The **per-server list** under the chart names every server with **both**
  numbers — its own utilisation and its contribution to the stage — and marks
  the deviating ones, e.g. `Screening S2: 80.30 % busy, contributes 40.15 % of
  stage  (stage util 60.23 %)`. Those are the same numbers the chart draws, in
  text.

The utilisation chart is a **Results** widget: it describes a run, so it
lives on the Simulation tab next to the metrics, not on the Input
tab with the data-derived figures.

### 6.5 Queue-Length-over-Time Chart
Shows, minute by minute, how many patients were waiting at **each stage's
queue** during a run (one coloured line per stage). Until a run finishes it
shows *"Run a simulation to see queue length over time."*

How to read it:

- Each line is a **step**, not a slope. The engine only changes a queue
  length at an event — a patient arrives or leaves — so the line stays flat
  and jumps. A smooth diagonal between two points would draw a queue length
  the clinic never actually had, which is why the chart does not use one.
- A steady low step means the stage is coping; steps that climb and stay
  high mean queues are building up (a sign the ρ ≥ 1 instability check should
  have caught).
- **Colour is the stage**, and the busiest stage is drawn **behind** the
  quieter ones, so where the lines overlap the smaller curve stays visible on
  top rather than being hidden by the larger one.
- The Y axis stops one patient above the tallest queue the run recorded, so
  the peak does not touch the frame.
- Long runs are **downsampled** to at most 2000 points per stage so the
  chart stays readable. The caption notes *"downsampled from N samples"*
  when that happens. Downsampling keeps the first and last points and the
  tallest queue peaks, so you can still see how bad the worst moment was.
- The legend above the charts lists the stages with their colours, so a line
  can be traced back to its stage without hovering. The X axis is minutes
  since the sim's start time.

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
the run — the numbers in the Performance Measures table (§6.1) are always present.

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

### 6.10 View Calculations

The **Overview** row of the results panel has a **View calculations** action.
It opens a dialog listing, in plain text, every derivation behind the run you
just finished. It is there for two readers: a clinic manager who wants to know
where a number came from, and you, if you are asked to justify the simulation
in the viva.

The button is only available **after a run finishes** — before that there are
no calculations to show, and the button is dimmed with a tooltip saying so.

What it lists, in order:

| Section | What it shows |
|---------|---------------|
| Run configuration | Parameter source ("fitted from …" / "entered manually"), random seed, run mode, start day, days generated, and the arrival window or daily cap if you set one |
| Arrival process | The arrival rate λ, the mean inter-arrival 1/λ, and the rule the engine used |
| Service processes | Per stage: the number of servers c, the rate μ per server, the capacity c·μ, the mean service time 1/μ, and the distribution family |
| Utilisation | The rule (`utilisation = busy time ÷ operating time`), the operating time T, **the two per-server quantities defined below**, each stage's observed utilisation, and **each server's busy time in minutes** plus its **contribution** to the stage |
| Flow balance | Patients served, throughput, mean queue length, mean wait, p_exit with its expected split, and the **effective p_bypass** with the share of patients that skipped Screening |
| Per-stage result | Per stage: patients served, mean wait, mean queue length |

Three things worth knowing:

- **Busy time is marked `(derived)`.** The engine records each server's
  *utilisation*, not its minutes; the minutes shown are that utilisation
  multiplied by the operating time. The label tells you it was reconstructed
  for display, so you never mistake it for a raw engine output.
- **Each server has TWO numbers, and they are not the same number.** This is
  the one thing worth reading twice:

  | Quantity | What it is | Do these sum? |
  |----------|------------|----------------|
  | **server utilisation** | how busy *that server* was: `busy ÷ T` | **No.** Three doctors at 60% each would mean 180% of one doctor's capacity |
  | **contribution** | that server's *share of its stage's* capacity: `busy ÷ (c × T)` | **Yes.** They add up to the stage utilisation |

  So for a two-server screening stage, a server that is 75.5% busy
  **contributes 37.75%**. The dialog prints both, the contribution line shows
  the division it used (e.g. `6.37 ÷ (2 × 158.19)`), and a per-stage
  "contributions sum" line shows the addition and that it matches the stage
  figure. Nothing about this is a new engine measurement — it is arithmetic on
  the two figures the engine already produced. On a **one-server** stage the
  two quantities are equal, because the stage *is* the server.
- **Every figure is dynamic.** A two-stage run prints two service rows, and a
  stage with six servers prints six utilisation rows. Nothing in the list is
  written for a particular clinic.

Use **Copy** to put the whole thing on your clipboard — useful for pasting into
a report or a viva answer sheet. The clipboard text is the plain monospace
form, so it pastes cleanly into a plain-text document.

### 6.11 Reading the Calculations Dialog

The dialog opens **800 × 800 pixels** and you can **resize it** by dragging its
edges: between 640 and 1200 pixels wide, and between 400 and 800 pixels tall.
Resize it freely — the **Copy** and **Close** buttons stay at the bottom of the
window at every size, and the body scrolls behind them, so nothing becomes
unreachable. You cannot drag it below 400 pixels tall. Press **Escape** to
close it.

The body is laid out in **two columns**: the label on the left, sized to fit the
longest label, and the value on the right, taking all the remaining width. A
value too long for its column **wraps onto the next line** — it is never
truncated, because a cut-off number is indistinguishable from a real one.

Two details that look like missing content but are not:

- **Section headings have no `----` underline on screen.** The underline is a
  feature of the monospace *clipboard* format, where it makes the heading
  stand out. It would be meaningless in a two-column layout.
- **Indented rows** — the per-server `busy (derived)` lines and the
  `expected share` lines — are indented to show they belong to the row above.
  In the clipboard text that same nesting is a pair of leading spaces.
- **The buttons never move because they are not part of the scrolling area.**
  The window is three horizontal bands: the title at the top, the scrolling
  body in the middle taking whatever room is left, and the button row at the
  bottom. Only the middle band scrolls.

#### The λ rows, and how to check them

For a run driven by a data file, the **ARRIVAL PROCESS** block prints both
arrival-rate estimates and says which one ran:

| Row | What it tells you |
|-----|-------------------|
| `λ source` | Which estimate drove the run — MLE or window |
| `λ — MLE` | `1 ÷ mean within-session inter-arrival gap` |
| `λ — window` | `arrivals ÷ operating minutes` |
| `window rule` | **The division itself**, e.g. `λ = 60 arrivals ÷ 825 operating minutes across 5 operating days (825 operating minutes)` |
| `Observation window` | The window the **file itself** covers |
| `Selected window (used)` | Appears **only** when you chose a window other than the file's own |
| `Estimate divergence` | How far apart the two estimates are, and which one ran |

**You can check the window λ by dividing.** The arrivals and the minutes are
both printed, so `60 ÷ 825` should equal the `λ — window` figure beside them. If
it does not, the dialog is wrong — please report it.

If you picked a window other than the file's own, the two are named apart and
the run's line is marked `(used)`: the file may cover six sessions while you ran
over five, and the receipt has to say which figure the engine sampled at. That is
the whole reason the row is split.

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

#### The optional `session_date` column

If your file covers more than one clinic session, add a `session_date` column
naming the day each row belongs to. **It is optional** — a file without it is
treated as one single session, exactly as before.

Accepted forms: `2026-09-14` (ISO date), `2026-09-14 08:15` (ISO datetime,
with a space or a `T`), and `14/09/2026` (**day first**). Anything else is
reported as a validation issue naming the row, rather than guessed at.

Two things it changes:

- **Arrival order is checked within each session.** 09:50 on Monday followed
  by 08:15 on Tuesday is a gap of roughly −155 minutes, not 1335. Arrivals must
  not go backwards *inside* a session; the order resets at each new date.
- **The observation window comes from it.** See below.

Rows dated on a Friday or Sunday are **not** treated as errors — historical
data may well include a session the clinic now runs differently — but they are
left out of the observation window, because the clinic is closed on those days.
If *every* date in the file falls on a closed day, the app says
`No operating sessions detected in this file.` rather than showing a rate
computed from time the clinic was shut.

`samples/sample_multiday.csv` is a ready-made six-session example
(60 rows, 10 per session).

### 7.1a Observation window and the two arrival rates

<a id="observation-window"></a>

Once a file is loaded, the **Input** tab works out two arrival rates from it and
shows you both. They are not competing versions of one number, and neither is a
correction of the other — they answer different questions.

**Observation window** — how much *operating* time the file covers. The clinic
runs 08:15–11:00 on Monday–Thursday and Saturday, so one operating session is
**165 minutes (2.75 hours)**, and the weekend is not counted: a file running
Monday to the following Monday spans seven calendar days but only six operating
sessions. The dropdown reads:

| Option | Operating sessions | Operating minutes |
|--------|--------------------|-------------------|
| Auto (from file) | whatever the `session_date` column says | — |
| 1 day | 1 | 165 |
| 3 days | 3 | 495 |
| 1 week | 5 | 825 |
| 2 weeks | 10 | 1650 |
| 1 month | 20 | 3300 |
| Custom… | you type operating **hours** | hours × 60 |

"One month" means four operating weeks — 20 sessions. That is an approximation
by construction, because there is no calendar month of this clinic to count.

Choosing a different option **changes the divisor the run uses**, and the
window-λ readout below names the window it came from, so you can check the
division yourself. For **Custom…**, type operating hours; 2.75 hours is one
clinic session, so `5.5` is two. An unusable value is refused in the field with
a message saying what to enter, and the Window option is disabled rather than
silently using the old value.

**The two estimates**

- **MLE λ** = `1 ÷ mean of the within-session inter-arrival gaps`. Gaps that
  cross a session boundary are excluded, because an inter-arrival time is a
  within-session property — overnight clinic closures are not inter-arrival
  times.
- **Window λ** = `total arrivals ÷ the selected window's operating minutes`.
  This one includes the idle tail of each session.

On the six-session sample file these differ by about 55 %, and the app states
the divergence and its direction rather than leaving you to work it out. That
gap is expected: MLE measures the rate *while patients were arriving*, window λ
measures it across the whole session.

**Use for this run** — pick which one drives the simulation. **MLE is the
default**, because it is the estimator the course teaches and the one every
earlier result was produced with. A manual λ typed in the **Parameters**
section overrides both, since you asked for that number explicitly. If you pick
Window on a file where no window λ can exist, the run falls back to MLE rather
than failing.

The **View calculations** dialog records which estimate ran, the value of both,
and the window each was computed from — with the division printed, so you can
confirm `60 ÷ 825` really is the λ beside it. A result can be read back later
without guessing. See [§6.11](#611-reading-the-calculations-dialog).

### 7.1b Recording how many servers were working, and what the file says about them

<a id="historical-server-counts"></a>

The Input tab asks you one thing the file cannot answer: **how many servers were
working at each stage while the data was being collected.** There is one field
per stage the file's own columns describe, in clinic order, each starting at 1.

These fields say "recorded during data collection" because that is what they
are — your record, not a value in the file. The note under them says plainly
that **server-ID columns are not present**: no column in the file says which
server a patient saw, so the count you type is an assumption about how many
could work at once. If two tables shared one queue, the honest answer is 1 for
that stage.

Enter a whole number of 1 or more. A blank or non-numeric field is refused in
place with a message saying what to type, rather than being quietly read as 1.

#### Historical utilisation, and how to check it

Underneath, each stage shows what utilisation *would* have been over the period
you recorded. It is calculated as:

```
utilisation = total service minutes recorded ÷ (servers × operating minutes)
```

So the number depends on the count you typed. Change the count and the figure
changes — that is the point of showing them together.

Every row prints the division that produced it, so you never have to trust a
percentage:

```
Screening   48.5 %   = 80.0 ÷ (1 × 165.0)   (observed window)
```

**Which operating minutes are used.** The app always names the basis, because
the two available answers are different numbers:

| Basis | When | How it is worked out |
|-------|------|---------------------|
| **Observed** | the file has a `session_date` column | operating days × 165 minutes |
| **Spanned** | no `session_date` column | last service completion − first arrival |

A file with no dates carries no evidence of how long the clinic was open, so the
app measures the span of the recorded work instead and says so. The spanned
window is measured to the **last service completion**, not the last arrival,
because the clinic was still open until the last patient finished.

#### A figure above 100 %

If a stage shows **more than 100 %**, the app does **not** cap it at 100. It
shows the real figure and a warning, because a number over 100 % is a finding
worth seeing rather than a glitch to hide. It means the recorded service time
cannot be explained by the number of servers you entered — either the true count
was higher, or the file's service times are wrong.

The remedy is to raise the server count for that stage and watch the figure
drop. Hiding the excess would leave a plausible-looking number that disagrees
with the data.

#### These counts and the run's server counts are two different things

The counts here describe the **collection period**. The **Stages** section of the
Simulation tab describes the **simulation**. They are shown separately and are
not linked while you type — editing a field here will not change the Stages
section.

To copy these counts across, use **Use for simulation**. It copies them into the
Stages rows **once**, at the moment you press it, and its label says that it
overwrites them. After that, the two are yours to set independently: a
simulation may deliberately try a busier clinic than the one that was recorded.

#### What historical utilisation is not

This figure is not the same as the **per-server utilisation** in the results
panel, which comes from the simulation's own run. One describes history, one
describes the model. They will not agree, and they are not meant to.

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
- **p_exit** — the fraction of **screened** patients exiting after Screening (used for routing).
- **p_bypass** — the fraction of **all** arrivals that went straight to the Doctor (used for routing).

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
| `p_exit = 1.0: every row…exits after Screening…` | Fitted `p_exit` is 1.0 **and your network has a later stage**, so no patient would ever reach it | Load different data, or override `p_exit` below 1 in Parameters. With a **single stage** there is nothing downstream to reach, so this message does not appear and the run proceeds normally. |
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
- **p_exit** — Probability a patient exits after Screening, out of the patients who were screened.
- **p_bypass** — Probability a patient skips Screening and goes straight from Reception to the Doctor.

---

## 12. Changelog

| Date | Change |
|------|--------|
| 2026-10-03 | Phase 8Q.3: the results panel's first group is now headed **Performance Measures** and carries a **stability verdict** per stage plus a named **bottleneck** (§6.1a). Stages are numbered `#` in visit order. Bands: **Stable** below ρ 0.90, **Near capacity** from 0.90 up to 1.00, **Unstable** at 1.00 or above (which the simulator refuses to run, so it is normally not seen) |
| 2026-10-03 | Phase 8Q.2: patients who skip Screening can now be modelled and read from data. A new **p_bypass** field in the Parameters section fits "went straight to the Doctor" from your file, and the **p_exit** fit now counts only the patients who were actually **screened** — before, direct-to-doctor traffic diluted it. Blank screening times on a bypass row are accepted instead of being reported as errors, so the sample bypass file now loads cleanly; blank Reception times still are not (§7.1). The event log now records the destination the patient actually went to, instead of printing the default next stage before the decision was made. A new sample file, `samples/sample_overcapacity.csv`, demonstrates a stage booked beyond its capacity. |
| 2026-10-03 | Phase 8Q.1: the **Input** tab now has a **Servers** field for each stage the file records, and a **Historical utilisation** line beside it showing `busy ÷ (servers × minutes)` with the division printed so you can check it. The divisor is named on screen — *observed* when the file has a `session_date` column, *spanned* otherwise. A stage above 100 % is shown as recorded, with a warning suggesting more servers, and is never silently capped. **Use for simulation** copies these counts into the Stages configuration once (§7.1b). |
| 2026-09-26 | Phase 8E: four fixes you may notice. (1) A **single-stage** network now runs even when the fitted `p_exit` is 1.0 — the "every row exits after Screening" message only appears when there is a later stage to reach (§9). (2) Dropdown fields show a **green** focus ring instead of the system blue, matching the rest of the app; the ring is still there when you tab through the form. (3) Opening a dropdown that already has a value now lists **all** the options instead of only the current one, **clicking an option selects it**, and **× clears the value and leaves the list open** so you can pick a replacement straight away. (4) The Clear-all confirmation dialog has more breathing room. §9 and the field descriptions updated |
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