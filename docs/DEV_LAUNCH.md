# DEV_LAUNCH.md — Developer Launch Guide

**Purpose:** Launch this project from a dead state (fresh clone, no build artifacts)
with zero errors. Follow this file literally.

**Last verified:** 2026-09-14 — restore/build/test/CLI-run all pass from a dead state on **Ubuntu 24.04** (.NET SDK 8.0.131), full suite 176 green, 0 warnings. M1 headless CLI verified: stable run (ρ 0.75) and clean unstable refusal (single-line stderr, no stack trace, exit 1 — ρ 1.25). M2 data CLI verified: `verify` (clean file → exit 0; dirty fixture → exit 1 listing all 5 issues), `fit` (prints params + chi-square, writes `logs/fit-*.json`), `simulate-data --servers 1,2,3` (three runs, exit 0), `export`. M3 `simulate-network` verified (incl. `--days 5 --cap 80 --verbose`). M4 `trace` verified against the frozen golden fixture (state/rng/events; `--output`; unstable refusal). [Windows: TBD]
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

> **Status as of 2026-09-14:** the Avalonia GUI shell is up (Program.cs + App.axaml +
> ViewLocator + CrashReporter, foundry hand-built — no template installed, see
> DECISIONS.md D-078). The window opens but shows placeholder config/results panels;
> the real layout lands in M5-D. Until then the CLI stays the primary path for
> verified command-line runs.

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

> Both scripts invoke `src/OpdSimulator.App`. The Avalonia shell launches a window
> as of 2026-09-14 (M5-A), still with placeholder panels; the in-GUI workflow is
> completed in M5-D..H. Until then, CLI verification stays via `OpdSimulator.Cli`.

The window should open within ~5 seconds. If it does not, see **Troubleshooting** below.

---

## 6. Run Tests

```bash
dotnet test OpdSimulator.sln
```

Expected: `Passed! - Failed: 0`. As of 2026-09-14 **176 tests pass**:
- `OpdSimulator.Core.Tests` (83) — queue, event/FEL ordering, RNG determinism, exponential
  sampling, server utilisation, engine M/M/1 analytical bound, stability refusal, event trace,
  **M4 trace regression (golden fixture, draw-by-draw RNG parity, stats cross-check, sink passivity)**.
- `OpdSimulator.Data.Tests` (58) — Excel/CSV loaders, TimeParser, validator (per-row issues),
  preprocessing (inter-arrival/service/p_exit), all 5 fitters, chi-square (accept/reject/k
  bounds), parameter-mode warnings, export, committed fixtures (samples + dirty file).
- `OpdSimulator.Cli.Tests` (35) — `simulate-params` refusal (clean stderr, no stack, exit 1,
  D-037); `verify` exit 0/1 + issue listing; unknown command → global usage, exit 2;
  `simulate-data` multi-server sweep; non-exponential refusal, exit 2; **M4 `trace` end-to-end
  (golden stdout, levels, refusal exit 1, `--output` mode, usage exit 2)**.

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
    └── OpdSimulator.Data.Tests/
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
| 2026-09-14 | M5-A: §1 prerequisites add Linux system libs row (libx11-6 libice6 libsm6 libfontconfig1, D-079 — installed by owner, not the agent); §9 blank-window row replaced with cross-ref to §1 (one canonical apt command, AGENTS §10.7); §5/§8 status updated: App is now a real Avalonia shell (D-078), placeholder panels until M5-D | docs-only (owner installed libs; App launch smoke-tested 2026-09-14) |
| 2026-09-14 | M4: deterministic event trace — new `trace` command (§7.6) emitting ARRIVAL/START_SVC/END_SVC/ROUTE/EXIT (+ RNG draw rows at `--level rng`); golden fixture + regression tests (draw-by-draw RNG parity, stats cross-check, sink passivity, D-055..D-059); "CLI run requested" demoted to Debug so trace stdout is pure lines; §6 refreshed to 176 tests; §8 layout note for `src/OpdSimulator.Core/Trace/` | **Ubuntu 24.04** (dotnet SDK 8.0.131) — full suite 176 green, 0 warnings; `trace` (state/rng/events, `--output`, unstable refusal) live-run verified against the frozen fixture |
| 2026-09-13 | M3: `simulate-data` is stage-aware (per-stage μᵢ, p_exit, per-stage blocks, D-052), new `simulate-network` command with `--days`/`--start-day`/`--cap`/`--verbose` (D-053) and `--p-exit` routing; `samples/sample_3stage_clinic.csv` tracked; §7.4/§7.5 + §8 updated; M2 sweep c=2/3 waits refreshed (§7.4); 156 tests green (§6) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — full suite 156 green, 0 warnings; `simulate-network` (incl. `--days 5 --cap 80 --verbose`) and stage-aware `simulate-data` live-run verified; M1 regression live (29892/0.724/0.75); §7.4 c=2/3 refresh command run |
| 2026-09-13 | M2: CLI is a subcommand dispatcher — `simulate-params` (renamed M1 form), `verify`, `fit`, `simulate-data` (server sweep), `export` (§5/§7); Data layer lands (loaders, validator, preprocessing, fitters, chi-square); sample files committed + regenerable via `scripts/make-sample-data.sh` (§8); 97 tests green (§6) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — dead-state restore/build/test pass, 0 warnings; verify/fit/simulate-data/export live-run verified, exit codes observed |
| 2026-09-13 | M1: CLI takes `--lambda/--mu/--servers/--horizon/--seed`, prints metrics + ρ, exits 0/1/2 (§5); tests exist (34 green, §6); headless mode updated to the rate-driven form, M2 data form noted (§7); Serilog.Sinks.File 7.0.0 + Core Serilog 4.4.0 + ProjectReferences noted (§3) | **Ubuntu 24.04** (dotnet SDK 8.0.131) — dead-state restore/build/test + M1 CLI F2/F3 all pass, 0 warnings |
| 2026-09-13 | File created (starter template); OS corrected to Ubuntu 24.04 | Not yet verified |
| 2026-09-13 | Added §11 Resuming Work After a Session Ends (renumbered Changelog → §12) | Not yet verified |
| 2026-09-13 | Scaffold: solution + 6 projects created, NuGet pinned (D-026), appsettings.template.json + copy step in §3, §5 rewritten for CLI-until-M5, §8 layout updated, test-projects-empty note added to §6 | **Ubuntu 24.04** (dotnet SDK 8.0.131) — restore/build/test/CLI all pass, 0 warnings |
| 2026-09-13 | §3: config-copy step moved to top of First-Time Restore; .gitignore keeps template tracked via negation (D-027, D-030) | N/A (docs-only) |