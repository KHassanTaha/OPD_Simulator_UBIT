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

### Running without a GUI (currently the only runnable path)

The graphical interface is not built yet (expected Milestone 5). To try the
simulator today, open a terminal and run:

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

7. **Choose a time horizon.**
   Options: 15 min, 1 hour, 1 day, 1 week, 1 month, or a custom number of days.

8. **Choose a run mode.**
   - **Single day** — one clinic session.
   - **Multi-day** — consecutive operating days (Mon–Thu, Sat).

9. **Set the random seed** (default `42`).
   Same seed = same results. Change it to explore variability.

10. **(Optional) Set a daily patient cap.**
    Leave blank for no cap.

11. **Click "Run Simulation".**
    The right panel fills in within a few seconds.

---

## 6. Reading the Results

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
Chronological trace of every event. Useful for understanding the simulation
step by step.

---

## 7. Loading Real Data (from an Excel or CSV file)

The graphical interface is not ready (Milestone 5), but you can already upload a
real data file and have the program validate it, fit distributions, run
goodness-of-fit, and simulate — all from the terminal.

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

### 7.5 Regenerating the sample file

The committed sample was generated deterministically (seed 42). To regenerate it
and the validation fixture: `bash scripts/make-sample-data.sh`.

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
| 2026-09-13 | Added "Loading Real Data" (§7) — the M2 CLI path: `verify`, `fit`, `simulate-data`, required file format, error messages; renamed the headless command to `simulate-params` (§3) |
| 2026-09-13 | Added "Running without a GUI" section (§3) — the M1 CLI path with the metrics explained, the ρ < 1 rule, and the event trace location |
| 2026-09-13 | Initial draft |