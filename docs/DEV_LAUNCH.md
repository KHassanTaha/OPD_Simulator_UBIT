# DEV_LAUNCH.md — Developer Launch Guide

**Purpose:** Launch this project from a dead state (fresh clone, no build artifacts)
with zero errors. Follow this file literally.

**Last verified:** 2026-09-13 — restore/build/test/CLI-run all pass from a dead state on **Ubuntu 24.04** (.NET SDK 8.0.131). [Windows: TBD]
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

**Configuration file:** the repo ships `appsettings.template.json` (Serilog sinks + `Simulation` defaults: `RandomSeed: 42`, servers Reception=1 / Screening=2 / Doctor=3). Create your local, git-ignored config **once** using the step above. `appsettings.json` is committed (the `.gitignore` negates `appsettings.*.json` for it and the template); per-environment overrides such as `appsettings.Development.json` stay ignored.

Run **once** (needs internet):

```bash
dotnet restore OpdSimulator.sln
```

This pulls all NuGet packages (MathNet.Numerics, Avalonia, ClosedXML, CsvHelper, etc.).
Expected output ends with `Restored ...`.

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

> **Status as of 2026-09-13:** the Avalonia GUI is not built yet. `src/OpdSimulator.App`
> is an **empty class library placeholder** (the Avalonia project template was not
> installed at scaffold time — BLOCKERS B-005). Until Milestone 5, run the headless
> CLI project instead.

**Linux / macOS:**
```bash
dotnet run --project src/OpdSimulator.Cli
```

**Windows (PowerShell):**
```powershell
dotnet run --project src\OpdSimulator.Cli
```

Expected output at scaffold stage: `Hello, World!` (the placeholder entry point).

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

> Both scripts invoke `src/OpdSimulator.App`. They will not launch a runnable app
> until the Avalonia UI project replaces the placeholder (Milestone 5). Until then,
> use the `OpdSimulator.Cli` commands above.

Once the real Avalonia app exists, the window should open within ~5 seconds. If it
does not, see **Troubleshooting** below.

---

## 6. Run Tests

```bash
dotnet test OpdSimulator.sln
```

Expected once tests exist: `Passed! - Failed: 0, Passed: N`.

> **Scaffold stage (2026-09-13):** the two test projects are intentionally empty,
> so the output reads `No test is available in ...` for each and the command still
> **exits 0**. Real tests are added in Milestone 1.

---

## 7. Headless Mode (for CI and demos without a display)

```bash
dotnet run --project src/OpdSimulator.Cli -- --input samples/sample_patients.xlsx --days 1
```

Prints metrics and chi-square results to stdout. This path is guaranteed to
work even if the GUI cannot open (e.g., remote demo machine).

> Status: **[NOT YET IMPLEMENTED]** — will be added in Milestone 2.

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
├── scripts/                    # run.sh, run.ps1 (invoke the App at M5)
├── samples/
│   ├── .gitkeep
│   └── sample_patients.xlsx    # NOT YET ADDED (owner; see BLOCKERS B-004)
├── src/
│   ├── OpdSimulator.Core/      # simulation engine (no UI)
│   ├── OpdSimulator.Data/      # Excel/CSV loader, fitting
│   ├── OpdSimulator.App/       # Avalonia UI — empty classlib placeholder until M5 (B-005)
│   └── OpdSimulator.Cli/       # headless runner
└── tests/
    ├── OpdSimulator.Core.Tests/
    └── OpdSimulator.Data.Tests/
```

> `samples/sample_patients.xlsx` will be added by the owner later (BLOCKERS B-004).
> The `samples/` folder is kept in git via `.gitkeep`.

---

## 9. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `A compatible .NET SDK was not found` | Wrong SDK installed | Install 8.0.x per `global.json` |
| `dotnet: command not found` | SDK not on PATH | Reopen terminal; check `~/.bashrc` |
| GUI window opens blank on Linux | Missing X11/display libs | `sudo apt install libx11-dev libice-dev libsm-dev libfontconfig1` |
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
- [ ] Confirm `samples/sample_patients.xlsx` exists and loads.
- [ ] Delete `bin/`/`obj/` once more and re-run `DEV_LAUNCH.md` §3–§5 to prove the dead-state path.
- [ ] Take a **screen recording** of a full successful run as fallback.
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
| 2026-09-13 | File created (starter template); OS corrected to Ubuntu 24.04 | Not yet verified |
| 2026-09-13 | Added §11 Resuming Work After a Session Ends (renumbered Changelog → §12) | Not yet verified |
| 2026-09-13 | Scaffold: solution + 6 projects created, NuGet pinned (D-026), appsettings.template.json + copy step in §3, §5 rewritten for CLI-until-M5, §8 layout updated, test-projects-empty note added to §6 | **Ubuntu 24.04** (dotnet SDK 8.0.131) — restore/build/test/CLI all pass, 0 warnings |
| 2026-09-13 | §3: config-copy step moved to top of First-Time Restore; .gitignore keeps template tracked via negation (D-027, D-028) | N/A (docs-only) |