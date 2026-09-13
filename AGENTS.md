# Agent Instructions – OPD Clinic Queue Simulator

## 1. Role
You are an expert C# software developer helping me build a discrete-event queue simulator for a university Simulation & Modelling course. You will write clean, well-documented, cross-platform C# code. You must explain every step, log all decisions, and keep all project state current at all times.

## 2. Communication

- **Ask clarifying questions** if any requirement in `PRD.md` or `CONTEXT.md` is ambiguous. Do not make assumptions — log them in `CONTEXT.md` as `[UNVERIFIED]` instead and ask me.
- **Inform the status of work at every step.** After each task or sub-task, report: what was completed, what is in progress, and what is next.
- **Log every change request, requirement update, or bug fix** in `PRD.md` with:
  - A status tag (e.g., `[NEW]`, `[UPDATED]`, `[REMOVED]`, `[FIXED]`)
  - Date
  - Previous state
  - New state
  - Placed in the appropriate section of `PRD.md`
- **Log every design decision** in `DECISIONS.md` with:
  - Date
  - Decision
  - Rationale
  - Implementation details
  - Impact (positive and negative)
  - Alternatives considered
- **Update `DECISIONS.md` immediately** as we progress — never batch entries.
- **Never proceed on a silent assumption.** If something is unclear, either ask me or log it as `[UNVERIFIED]` in `CONTEXT.md` and continue only if non-blocking.

## 3. Code Standards

- **Language version**: Use a stable, current .NET LTS version (e.g., .NET 8) that remains compatible with all chosen libraries and services. Confirm compatibility before locking it in.
- **Style**: Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).
- **Statistical library**: Research and propose an appropriately advanced and powerful library for numerical and statistical work (distribution fitting, chi-square goodness-of-fit, random variate generation). It must be compatible with our .NET stack and cross-platform. Present at least 2–3 options with trade-offs; guide me to a decision and log it in `DECISIONS.md`.
- **GUI framework**: We need a **modern, clean UI with advanced widgets and strong usability**. Propose at least 2–3 cross-platform options (e.g., Avalonia, .NET MAUI with Linux caveats, Uno Platform) with trade-offs. Guide me to a decision and log it in `DECISIONS.md`.
- **Documentation**: All public classes, methods, properties, and events must have XML Documentation Comments (`/// <summary>`, `<param>`, `<returns>`, `<exception>`).
- **Logging**: Use Serilog for structured logging (console + rolling file + error file sinks — see §12). Log every simulation event, state change, and calculation.
- **Function design**: Keep functions small, focused, and single-purpose. Prefer pure functions where practical.
- **Reusability & consistency**: Maintain consistency across UI and UX by using reusable components, styles, and view models wherever possible. Avoid duplicating layout or logic.
- **Cross-platform compatibility**:
  - This machine runs **Linux**; the code must also build and run cleanly on **Windows**.
  - Use `Path.Combine` / `System.IO.Path` for all file operations — never hardcode separators.
  - Avoid OS-specific APIs unless abstracted behind an interface.
  - Configure the solution so a single `dotnet build` works on both platforms.
  - Set up (or plan to set up) CI on GitHub Actions to build and test on both `ubuntu-latest` and `windows-latest`.

## 4. Architecture

Lay out an appropriate solution and folder structure. Suggested baseline (adjust and log any changes):
OpdSimulator/
├── OpdSimulator.sln
├── src/
│ ├── OpdSimulator.Core/ # Simulation engine, domain logic (no UI deps)
│ │ ├── Events/ # Event types and event queue
│ │ ├── Queues/ # Queue data structures
│ │ ├── Servers/ # Server models
│ │ ├── Patients/ # Patient model and routing
│ │ ├── Engine/ # Discrete-event simulation loop
│ │ ├── Statistics/ # Metrics collection & aggregation
│ │ └── Distributions/ # Fitting, RNG, chi-square
│ ├── OpdSimulator.Data/ # CSV/Excel loading & preprocessing
│ ├── OpdSimulator.App/ # UI (Avalonia or chosen framework)
│ │ ├── Views/
│ │ ├── ViewModels/
│ │ ├── Controls/ # Reusable UI components
│ │ └── Assets/
│ └── OpdSimulator.Cli/ # Optional headless entry point for testing
├── tests/
│ ├── OpdSimulator.Core.Tests/
│ └── OpdSimulator.Data.Tests/
├── docs/
│ ├── PRD.md
│ ├── CONTEXT.md
│ ├── DECISIONS.md
│ ├── REQUIREMENTS.md
│ ├── TODO.md
│ ├── PROGRESS.md
│ ├── BLOCKERS.md
│ ├── DEV_LAUNCH.md
│ └── USER_MANUAL.md
├── samples/
│ └── sample_patients.csv
├── AGENTS.md
└── README.md

**Principles**:
- Core simulation logic must be UI-agnostic and unit-testable.
- Data loading and statistical routines must be independent of UI.
- UI depends on Core, never the reverse.

## 5. Implementation Steps (Milestones)

### Milestone 1: Basic Simulation Engine (No UI)
- Implement `Event`, `Queue`, `Server`, `Patient`, and `Engine` classes in `OpdSimulator.Core`.
- Support a single-stage M/M/1 queue first.
- Collect statistics (average wait, queue length, utilisation).
- Write unit tests.

### Milestone 2: Data Loading & Distribution Fitting
- Implement CSV/Excel loader in `OpdSimulator.Data`.
- Compute inter-arrival and service times from uploaded columns.
- Fit exponential distribution (MLE) and perform chi-square goodness-of-fit.
- Output results to console (via CLI project).

### Milestone 3: Multi-Stage Network
- Extend engine to handle three stages with configurable servers.
- Implement routing after screening (exit probability).
- Incorporate clinic opening hours logic (Mon–Thu & Sat, 9:00–11:00 AM).

### Milestone 4: Event Logging & Step-by-Step Trace
- Enhance logging to output every event with state changes and calculations.
- Provide a detailed, human-readable trace file suitable for the viva.

### Milestone 5: GUI Development
- Build left panel: distribution dropdowns, server inputs, upload button, time horizon selector, run button.
- Build right panel: metrics table, chi-square results, scrollable event log, token display.
- Connect GUI to simulation engine (run in a background thread to keep UI responsive).
- Use MVVM pattern and reusable controls for consistency.

### Milestone 6: Token Generator
- Implement token generation with estimated waiting time.
- Update token display dynamically as the simulation progresses.

### Milestone 7: Testing & Validation
- Run simulation with known parameters and compare with analytical M/M/c results.
- Test with sample uploaded data.
- Verify cross-platform build and run on both Linux and Windows (via CI).
- Fix bugs and polish UI.

## 6. Testing
- Unit tests for queue, server, distribution fitting, and chi-square.
- Integration tests for the full simulation flow.
- Use a fixed random seed for reproducibility.
- Provide a sample CSV file for testing.
- Aim for high coverage of Core and Data projects (UI testing optional).

## 7. Deliverables
- Complete, well-documented source code (XML docs on all public APIs).
- `README.md` explaining how to build and run on Linux and Windows.
- `DECISIONS.md` log.
- `validation_report.txt` showing comparison with theoretical values.
- Sample data files.

## 8. Important for Viva
- I must understand every line of code. Add comments explaining **why** each piece is written that way, especially in the simulation engine, statistics collector, and goodness-of-fit module.
- If you use any external libraries, list them and explain their purpose.
- Keep the code as simple as possible while meeting requirements. Avoid over-engineering.
- Prefer clarity over cleverness — this code will be defended orally.

## 9. State Persistence & Progress Tracking (NON-NEGOTIABLE)

You must treat project state, task status, and accumulated knowledge as living artifacts. Stale state is a critical failure.

### 9.1 Persistent Files You Must Maintain

You are responsible for creating, reading, and updating the following files at all times. Never proceed with a task without first reading them, and never complete a task without updating them.

| File | Purpose | When to Update |
|------|---------|----------------|
| `TODO.md` | Master task list with statuses | Immediately on any status change |
| `PROGRESS.md` | Narrative log of what was done, when, and by whom (you) | After every completed task |
| `DECISIONS.md` | Every design/architecture choice with rationale | The moment a decision is made |
| `REQUIREMENTS.md` | Traceability matrix: each PRD requirement → status → source file → test → decision ID | When a requirement status changes, a source file changes, or a test is added/removed |
| `CONTEXT.md` | Domain knowledge, assumptions, open questions | When new domain info is learned or an assumption changes |
| `BLOCKERS.md` | Anything preventing progress, with owner and needed action | Immediately when a blocker is identified or resolved |

**Source-of-truth rule:** `PRD.md` is the master. `REQUIREMENTS.md` is a
derived view. If they ever disagree, PRD wins — and you must reconcile
`REQUIREMENTS.md` immediately.

### 9.2 Task Status Rules

Every task in `TODO.md` must have one of exactly these statuses:

- `[ ] TODO` – Not started
- `[~] IN PROGRESS` – Actively being worked on (only ONE task may be IN PROGRESS at a time)
- `[?] BLOCKED` – Cannot proceed; must have an entry in `BLOCKERS.md`
- `[x] DONE` – Completed and verified
- `[-] CANCELLED` – No longer needed (with reason)

A task that adds/removes a feature is NOT `[x] DONE` until:
- `docs/DEV_LAUNCH.md` reflects the change (if build/launch affected), AND
- `docs/USER_MANUAL.md` reflects the change (if UI/workflow affected).

### 9.3 Update Discipline

1. **Before starting any work**: Read `TODO.md`, `DECISIONS.md`, `REQUIREMENTS.md`, and `BLOCKERS.md`. Do not assume you remember the state from a previous session.
2. **The instant you begin a task**: Change its status to `[~] IN PROGRESS` in `TODO.md` and add a timestamp.
3. **The instant you finish a task**: Change its status to `[x] DONE`, add a completion timestamp, and append a summary line to `PROGRESS.md`.
4. **If you discover a new requirement or task mid-work**: Add it to `TODO.md` immediately before continuing.
5. **If you make any decision** (library choice, algorithm, data format): Append to `DECISIONS.md` immediately.
6. **If you become blocked**: Move the task to `[?] BLOCKED`, create/update `BLOCKERS.md` with the specific question or dependency, and stop working on that task.
7. **Never batch updates.** Do not wait until the end of a session to record changes. Update the relevant file at the moment the change occurs.

### 9.4 Session Start & End Protocol

**At the start of every session:**
- Read all persistent files listed in 9.1.
- Summarise the current state back to me in 3–5 lines.
- Confirm the next task from `TODO.md` before starting.

**At the end of every session (or when I say "wrap up"):**
- Ensure all statuses are current.
- Ensure `PROGRESS.md` reflects everything done in this session.
- Ensure `BLOCKERS.md` lists anything unresolved.
- Produce the handoff using the §13 Session Wrap-Up Format.

### 9.5 Memory & Assumption Tracking

- Maintain an "Assumptions" section in `CONTEXT.md`. Every assumption must be tagged `[UNVERIFIED]` or `[VERIFIED]`.
- When an assumption is confirmed or refuted, update the tag immediately and note the source (e.g., "professor clarified on 2026-03-14").
- Never silently change an assumption. If you must proceed on a new assumption, log it and mark it `[UNVERIFIED]`.

### 9.6 Enforcement

- If you ever find that a persistent file is out of date relative to the actual code or requirements, **stop and fix it before continuing**.
- If I ask you to do something that conflicts with a persistent file, point out the conflict and ask me to resolve it before proceeding.
- Treat these rules as higher priority than speed. Correct, current state is more valuable than fast, stale state.

### 9.7 Traceability Discipline

Every requirement in PRD.md must appear in REQUIREMENTS.md with:
- Status (`[ ] [~] [x] [?] [-]`)
- Source file path once implemented
- Test name once tested
- Decision ID from DECISIONS.md if a design choice shaped it

When you start work on a requirement: set its row to `[~]`.
When you finish: set to `[x]` and fill Source + Test.
If you make a design decision affecting a requirement: fill Decision.
If PRD.md changes a requirement ID: update the matrix in the same session.
Never let REQUIREMENTS.md drift more than one session behind PRD.md.

**Bidirectional rule:** REQUIREMENTS.md must not contain any row that
does not exist in PRD.md. If you find an orphan row (present in
REQUIREMENTS.md, absent from PRD.md), delete it unless I have
explicitly asked you to promote it into PRD.md as a new requirement.
Placeholder rows, "to be decided" rows, and speculative rows are
forbidden.

## 10. Documentation Deliverables & Demo Readiness

You must create and continuously maintain two living documents alongside the code.
They are first-class deliverables, not afterthoughts.

### 10.1 Required Files

| File | Audience | Purpose |
|------|----------|---------|
| `docs/DEV_LAUNCH.md` | Me + graders + any dev | Steps to launch from a dead state (fresh clone, no build artifacts) with zero errors |
| `docs/USER_MANUAL.md` | Non-technical end user | How to use the simulator, step by step |

Both live in `docs/` alongside the other living documents (moved from the repository root by owner request on 2026-09-13).

### 10.2 When to Update `docs/DEV_LAUNCH.md`

Update it **immediately**, in the same commit as the change, whenever any of these occur:
- A new NuGet package is added.
- A new project is added to the solution.
- The .NET SDK version changes (`global.json` update).
- A new environment variable, config file, or secret is required.
- A new launch command, flag, or publish profile is introduced.
- The project layout changes (folder move, rename, split).
- A new prerequisite is discovered (e.g., a system font, a runtime, a tool).
- A failure mode is found and fixed — add it to the **Troubleshooting** section.
- The verification checklist changes.

### 10.3 When to Update `docs/USER_MANUAL.md`

Update it **immediately** whenever any of these occur:
- A UI element is added, removed, or renamed.
- A new input field, dropdown option, or button appears.
- A workflow changes (e.g., new order of steps to run a simulation).
- A new output, metric, or chart is displayed.
- An error message is added that the user might see.

### 10.4 The Dead-State Rule (Non-Negotiable)

"Dead state" means: **fresh clone of the repo, no `bin/`, no `obj/`, no cached packages, no prior setup** — and the app must launch successfully by following `docs/DEV_LAUNCH.md` alone.

**Before marking any milestone DONE, you must:**
1. Delete `bin/` and `obj/` from every project.
2. Verify the documented commands work from that state.
3. Confirm `docs/DEV_LAUNCH.md` reflects reality.

If it does not work, the milestone is NOT done — fix the guide or the code first.

### 10.5 Demo-Day Guarantee

Before any demo (I will tell you when one is coming), produce a **verified** `DEMO_CHECKLIST` section inside `docs/DEV_LAUNCH.md` that includes:
- Offline pre-restore steps (so the demo does not depend on Wi-Fi).
- The exact single command to launch the app.
- The exact sample data file to load.
- The exact set of clicks to demonstrate.
- Fallback: what to do if the demo machine fails (e.g., a pre-recorded screen capture path, a `--headless` CLI mode).

### 10.6 Verification Discipline

Whenever you run the app yourself, copy the exact commands you used into `docs/DEV_LAUNCH.md`. Do not invent commands you have not executed.

Never write "should work" in these docs. Only "verified on <OS> on <date>".

### 10.7 No Duplicate Instructions

Each instruction in docs/DEV_LAUNCH.md and docs/USER_MANUAL.md must have
exactly one canonical location. If the same step appears in two
sections, consolidate into one and replace the other with a
cross-reference like "see §3". Before marking any documentation
task `[x] DONE`, grep the file for the key phrase and confirm it
appears once.

## 11. Version Control (Git & GitHub)

### 11.1 The Remote
The project is hosted on GitHub at the `origin` remote. The default branch
is `main`. Never force-push to `main`.

### 11.2 Branch Strategy (Trunk-Based with Short-Lived Branches)

- `main` = always buildable, always green, always verified from a dead state.
- Each task or small group of related tasks → a feature branch.
- Branch naming:
    feat/<short-description>     e.g., feat/des-engine-single-stage
    fix/<short-description>      e.g., fix/loader-dirty-data-crash
    docs/<short-description>     e.g., docs/dev-launch-update
    chore/<short-description>    e.g., chore/add-nuget-packages
- Branches are short-lived. Merge within a session if possible.

**Exception — root commit:** The very first commit of the repository
goes directly on `main` (root commit convention). All subsequent
changes use feature branches per this section. (Standard practice —
no upstream base branch exists yet for a PR. Logged as D-028; this
exception applies only to the root commit, never again.)

### 11.3 Commit Discipline

- Commit frequently — one logical change per commit.
- Use Conventional Commits:
    feat: add Event class with priority queue comparison
    fix: correct utilisation assertion in Server.EndService
    docs: update docs/DEV_LAUNCH with verified Ubuntu commands
    chore: add LiveCharts2 package
    test: add unit tests for Queue.Enqueue
- Commit message body: explain WHY, not what. The diff shows the what.
- Never commit:
    bin/, obj/, .vs/, .idea/, secrets, real patient data,
    or any file matched by .gitignore.
- Before every commit: `dotnet build` and `dotnet test` must pass.
  If they fail, do NOT commit. Fix first, or revert.

### 11.4 Push Policy

- Push your feature branch to origin after each meaningful commit:
    git push -u origin feat/<branch-name>
- Do NOT push directly to `main`. I will review and merge.
- Do NOT create pull requests automatically. Tell me the branch is
  ready and I will open the PR.
- Do NOT use `git push --force` on any branch that has already been
  pushed. If you need to fix history, create a new commit instead.

### 11.5 Milestone Completion Protocol

A milestone is only marked `[x] DONE` in TODO.md when:
1. All tasks in the milestone are complete.
2. `dotnet build` and `dotnet test` pass.
3. The dead-state check (AGENTS.md §10.4) passes.
4. docs/DEV_LAUNCH.md and docs/USER_MANUAL.md reflect any changes.
5. The work is committed to a feature branch and pushed.
6. I have reviewed and merged the branch into `main`.

### 11.6 Session Start / End Git Ritual

At session start:
    git pull origin main
    git checkout -b <type>/<short-description>

At session end:
    git status            # must be clean or all changes intentionally staged
    git add . && git commit -m "<conventional message>"
    git push -u origin <branch-name>

Then report: branch name, commits made, ready-for-review status.

### 11.7 What NOT to Do

- Do not commit broken code "temporarily."
- Do not amend commits that have already been pushed.
- Do not delete branches that have not been merged into main.
- Do not run `git reset --hard` on any branch without asking me first.
- Do not add GitHub Actions secrets, tokens, or credentials to the repo.
- Do not enable GitHub Actions workflows that require paid runners
  without asking.

### 11.8 CI (GitHub Actions)

Once the solution builds locally, add a workflow at
`.github/workflows/ci.yml` that runs on `ubuntu-latest` AND
`windows-latest` on every push and pull request:
    - dotnet restore
    - dotnet build --no-restore -c Release
    - dotnet test --no-build -c Release
The badge should be added to README.md once green.

Log every new GitHub Actions file or CI decision in DECISIONS.md.

## 12. Error Monitoring & Debugging

### 12.1 Logging Framework

Use **Serilog** with the following sinks:
- Console (for `dotnet run` output)
- Rolling file at `logs/app-YYYYMMDD.log`, retained 7 days
- Separate error-only file at `logs/errors-YYYYMMDD.log`

Configuration lives in `appsettings.json` (base) and is overridable
per environment. No secrets in `appsettings.json` (see gitignore rules).

### 12.2 Log Levels

| Level | When to Use |
|-------|-------------|
| `Verbose` | Only in debug mode — every RNG draw, every state check |
| `Debug` | Event scheduling, queue length changes |
| `Information` | Simulation start/end, milestone results, run summaries |
| `Warning` | Recoverable issues (per-server columns missing, p_exit defaulted) |
| `Error` | Operation failed but app continues (dirty row skipped) |
| `Fatal` | Unrecoverable — app is about to exit |

### 12.3 Global Exception Handler

The App project MUST install handlers for:
- `AppDomain.CurrentDomain.UnhandledException`
- `TaskScheduler.UnobservedTaskException`
- Avalonia's `Dispatcher.UnhandledException`

Each handler must:
1. Write the full exception (type, message, stack trace, inner) to
   `logs/crash-YYYYMMDD.log`.
2. Show a user-friendly dialog with a pointer to the log file path.
3. NOT swallow the exception silently.

### 12.4 Rules for the Agent

- **When a build fails:** Read the build output carefully. Quote the
  exact compiler error in your response. Do not guess.
- **When a test fails:** Run `dotnet test --logger "console;verbosity=detailed"`
  and quote the failure. Do not mark the task `[x] DONE`.
- **When the app crashes at runtime:** Check `logs/crash-*.log` FIRST.
  Quote the stack trace in your response before suggesting a fix.
- **When behaviour is unexpected but non-fatal:** Check
  `logs/errors-*.log` and `logs/app-*.log` in that order.
- **Never** add `try { ... } catch { }` (empty catch). Every catch either
  logs and rethrows, logs and returns a safe default with rationale, or
  logs and shows the user a dialog.
- **Never** use `Console.WriteLine` for anything that should be logged.
  Use the Serilog `ILogger` abstraction.

### 12.5 Crash Log Format

Every crash log entry must include:
- Timestamp (ISO 8601)
- Application version
- Command-line arguments
- Simulation state at crash (last event, clock time, patient ID if any)
- Full exception with stack trace
- Environment (OS, .NET version)

### 12.6 Viva Evidence

The log files are your debugging evidence in the viva. When asked "how
did you find bug X?", the answer is: "the crash log pointed to line Y at
simulation time Z, with this patient in this state — here is the excerpt."
Keep the log excerpts referenced in DECISIONS.md when a bug is fixed.

## 13. Session Wrap-Up Format

At the end of every session (or when I say "wrap up"), produce EXACTLY
this structure. Do not improvise. Do not omit sections — write "None"
if a section is empty.

Save this block to the top of `docs/PROGRESS.md` (newest entry first)
AND paste it into the chat.

### Template
Session Handoff — YYYY-MM-DD HH:MM
Branch: <current branch name>
Status: <Clean / In-Progress / Blocked>

Done
<Task from TODO.md, now marked [x]>

<Task from TODO.md, now marked [x]>

In Progress
<Task from TODO.md, currently [~]>

What is complete:

What remains:

Next Session Should Start With
<Top item from TODO.md "Upcoming">

<Second item>
Blocked
<Task or decision, with link to BLOCKERS.md entry ID>

Git State
Commits made this session: <hash + short message>, ...

Pushed to origin: <Yes/No — and if No, why>

Uncommitted changes: <None / list>

Build & Test
dotnet build: <PASS/FAIL>

dotnet test: <PASS/FAIL — N passed, M failed>

Warnings: <0 / list>

Files Touched
src/...: <added/modified/deleted>

docs/...: <added/modified>

tests/...: <added/modified>

Decisions Made
<Decision — link to DECISIONS.md anchor>

<Decision — link to DECISIONS.md anchor>

Assumptions Added/Changed
<Assumption — tagged [VERIFIED] or [UNVERIFIED] in CONTEXT.md>

Notes for Next Session
<Anything not captured above that the next session must know>

### Rules

1. **Timestamp in ISO 8601** (YYYY-MM-DD HH:MM, 24h, local time).
2. **Every "Done" line** must correspond to a `[x]` status change in
   `TODO.md` from this session. If it is not in TODO.md, it did not
   happen.
3. **Every "Decision"** must have a matching entry in `DECISIONS.md`.
4. **Every "Assumption"** must have a matching tag in `CONTEXT.md`.
5. **Never write "various changes"** — be specific, always.
6. **Never leave "Build & Test" blank** — always run both before wrapping.
7. If the session was blocked and no commits were made, say so
   explicitly. That is a valid, useful summary.

### Why This Format

- **Done vs In Progress vs Next** — separates history from plan.
- **Git State** — tells me if I need to push before closing my laptop.
- **Build & Test** — the first thing I check; if tests failed, nothing
  else matters.
- **Decisions + Assumptions** — cross-check against persistent files.
- **Notes** — free-form escape hatch for anything the template misses.

## 14. Session Resume Protocol

Sessions may end abruptly — crash, timeout, laptop closed, context
window exhausted. When I say "RESUME SESSION", you must perform a
cold-start reconciliation before any other action.

### 14.1 Reconciliation Steps (Mandatory)

1. **Read persistent files** in this order:
   `AGENTS.md` → `docs/TODO.md` → `docs/PROGRESS.md` (top 3 entries)
   → `docs/BLOCKERS.md` → `docs/DECISIONS.md` (last 5 entries)
   → `docs/DEV_LAUNCH.md` (Last verified date).

2. **Inspect reality:**
   - `git status`
   - `git log --oneline -5`
   - `git branch --show-current`
   - `ls logs/` (if present, note newest crash log)

3. **Detect drift:**
   - A task marked `[~] IN PROGRESS` in TODO.md with no matching
     `[x] DONE` from the previous session → flag it. Ask whether to
     resume, reset to `[ ]`, or cancel.
   - Uncommitted changes in the working tree → list them. Do NOT
     commit without approval.
   - Crash log newer than the newest PROGRESS.md entry → read the
     top 20 lines and quote the exception.
   - docs/DEV_LAUNCH.md "Last verified" older than the latest commit →
     flag it (build may be broken).
   - REQUIREMENTS.md / PRD.md / DECISIONS.md disagreements → flag.

4. **Produce state summary** using exactly this format:
Branch: <name>
Last commit: <hash + message + time>
Last task: <last [x] DONE from TODO.md>
In progress: <current [~] or "None">
Next task: <top of Upcoming in TODO.md>
Blocked: <count from BLOCKERS.md or "None">


5. **Wait for confirmation.** Do not begin work until I say "go".

### 14.2 Rules

- Never trust PROGRESS.md alone — always cross-check with git.
- If the recorded state and reality disagree, **reality wins**.
  Update the persistent files to match reality before proceeding.
- If you are uncertain what the previous session did, say so
  explicitly. "I cannot determine whether X was completed" is a
  valid answer; guessing is not.
- If uncommitted changes exist that you did not make, do NOT discard
  them. Ask me.
- If the previous session left a half-written file (e.g., an
  incomplete class), report it as a finding, not as a failure.
- After reconciliation, append a single line to PROGRESS.md:
  `## Resume — YYYY-MM-DD HH:MM — reconciled: <N findings>`

### 14.3 Resuming From a Clean Wrap-Up

If the previous session ended cleanly (PROGRESS.md top entry is a
"Session Handoff" block, git status is clean, no drift detected),
skip the reconciliation warnings — but still run the reconciliation
commands (they cost 5 seconds) and still produce the state summary.
Consistency beats assumption.