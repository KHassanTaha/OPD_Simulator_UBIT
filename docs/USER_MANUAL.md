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

The window has two panels:

```
+----------------------------+--------------------------------+
|  LEFT: Configuration       |  RIGHT: Results                |
|  ------------------------  |  --------------------------    |
|  - Parameter mode          |  - Metrics table               |
|  - Inter-arrival dist.     |  - Chi-square results          |
|  - Service dist.           |  - Event log (scrollable)      |
|  - Servers per stage       |                                |
|  - Manual λ (optional)     |                                |
|  - [Upload Data]           |                                |
|  - Time horizon            |                                |
|  - Random seed             |                                |
|  - Daily cap (optional)    |                                |
|  - [Run Simulation]        |                                |
+----------------------------+--------------------------------+

      [ Tab: Simulation ]   [ Tab: Token Generator ]
```

---

## Config fields explained

The subsections below are the targets of the "?" help icons on every
configuration field (press **F1** anytime or click a "?" icon to jump here).

### Arrival rate

The arrival rate λ₀ (patients per minute) arriving at **Reception**. In
**Rate-wise** mode enter the rate directly (e.g. 0.5 = one patient every two
minutes). In **Mean-wise** mode enter the mean inter-arrival time in minutes
(e.g. 2), which the app converts via λ = 1/mean.

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

### Servers

The number of parallel servers c at each stage. The clinic reference model
uses **Reception 1, Screening 2, Doctor 3**, but you can change them to
experiment (e.g. an M/M/1 baseline single stage).

### Horizon

How long to simulate. **Days** mode runs whole clinic days (Mon–Thu and Sat,
9:00–11:00 with arrivals from 8:15). **Minutes** mode runs a raw window of
minutes from the start of arrival generation.

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

1. **Choose parameter mode.**
   Select **Rate-wise** (λ, μ) or **Mean-wise** (1/λ, 1/μ). This must match the
   data you are about to upload.

2. **Select the inter-arrival distribution.**
   Default: **Exponential** (recommended for arrivals).

3. **Select the service distribution.**
   Default: **Exponential**. Other options: Normal, Lognormal, Gamma, Uniform.

4. **Set number of servers per stage.**
   - Reception: `1`
   - Screening: `2`
   - Doctor: `3`

5. **(Optional) Enter a manual λ.**
   If you enter a value, the simulator will still fit the data and show both
   results side by side. The manual value is used in the simulation.

6. **Click "Upload Data"** and choose your Excel file.
   The file must contain one row per patient with columns:
   - `arrival_time`
   - `<stage>_start`
   - `<stage>_end`
   - `departure_stage` (`Screening` or `Doctor`)

   If the file has problems (missing values, wrong columns), the app will
   list them and ask you to clean the data.

7. **Choose a run mode.**
   The **Horizon** section is a run-mode picker with three options:
   - **Clinic day** — one operating session (Monday–Thursday or Saturday,
     08:15 start, services continue past 11:00 until they finish). *Default.*
   - **Multi-day** — N consecutive operating days (Friday/Sunday are skipped).
     Shows the fields **Days**, **Start day**, and **Daily patient cap**.
   - **Diagnostic trace** — a fixed-length run in minutes, used to walk
     DES correctness line by line. Shows **Horizon (minutes)** (default
     `10000`) and the **Trace level** dropdown (None / Events / State / RNG).
     Only this mode produces an event trace (clinic-day runs have none).

8. **(Diagnostic trace only) Set the trace level.**
   "State" records arrivals, service start/end, routes and queue changes;
   "RNG" also prints every random-number draw, so you can replay any run
   by hand. Same seed → same bytes.

9. **(Multi-day only) Set the horizon and cap.**
   Days, start day, and an optional daily patient cap (blank = no cap).

10. **Set the random seed** (default `42`, under the **Advanced** section).
    Same seed = same results. Change it to explore variability.

11. **Click "Start Calculation".**
    The right panel fills in a few seconds later.

The run is refused — with an explanation banner, never silently — if no
arrival rate is available (you entered no λ in **Parameters** and loaded no
data), if the fitted `p_exit` is 1.0 (every patient exits after Screening, so
no one reaches the Doctor stage — override `p_exit` in **Parameters**), or if
any stage has ρ ≥ 1 (unstable: arrivals outpace service).

---

## 6. Reading the Results

Before the first run, the right panel shows a **welcome card** (project, course,
members, professor). It is replaced by the results the moment you start a
calculation. If the run was refused, an error banner explains exactly why.

The widget selector (a "Customise results" toggle at the top of the right
panel) shows or hides the individual widgets below it.

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
queue-state snapshots and (at `RNG`) random-number draws.

---

## 7. Loading Real Data (from an Excel or CSV file)

The graphical interface can upload a data file in the **Data** section and fit
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
Use `--level rng` to also print every random-number draw with its sampled value
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
| 2026-09-16 | GUI run flow live (rebuild Phase 5): three run modes — Clinic day / Multi-day / **Diagnostic trace** — in the Horizon section; welcome card on first launch; results widgets (metrics, chi-square, trace, data preview) with a "Customise results" toggle; refused runs show an explanation banner instead of failing silently |
| 2026-09-14 | M4 CLI: new `trace` command (§7.6) prints a deterministic line-by-line event trace (ARRIVAL/START_SVC/END_SVC/EXIT, plus RNG draw rows with `--level rng`); use it to walk through any simulation by hand before the viva |
| 2026-09-13 | M3 CLI: `simulate-data` now runs multi-stage files (per-stage μᵢ, p_exit, one network run — §7.4) and a new `simulate-network` command with `--days`/`--start-day`/`--cap`/`--verbose` (§7.5); blank doctor cells for Screening exits are accepted; multi-stage runs print a per-stage block and network totals |
| 2026-09-13 | Added "Loading Real Data" (§7) — the M2 CLI path: `verify`, `fit`, `simulate-data`, required file format, error messages; renamed the headless command to `simulate-params` (§3) |
| 2026-09-13 | Added "Running without a GUI" section (§3) — the M1 CLI path with the metrics explained, the ρ < 1 rule, and the event trace location |
| 2026-09-13 | Initial draft |