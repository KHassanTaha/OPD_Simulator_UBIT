# PROGRESS.md — Narrative Progress Log

> Session handoffs (AGENTS §13) and resume lines (AGENTS §14.2) are stored here newest-first at the top.

### M3 sub-block H: docs pass + dead-state check — 2026-09-13 (feat/milestone-3-multi-stage-network)
Every living doc brought current and the dead-state rule re-run:
**DEV_LAUNCH** §7.4 documents the stage-aware `simulate-data` form with the
committed `samples/sample_3stage_clinic.csv` (verified command + output), new
§7.5 for `simulate-network` (verified example, exit-code + refusal semantics),
§8 layout lists the new sample, §12 changelog row with the Ubuntu-24.04
verification stamp. **USER_MANUAL** §7.4 gains the multi-stage paragraph,
new §7.5 (simulate-network, non-technical), §7.6 regenerating samples; §12
changelog row. **REQUIREMENTS.md** M3 rows: FR-SIM-1/4/6/7/8/9 → `[x]` with
network sources + tests + D-049→D-053, FR-SIM-10 → `[~]` (real-clock binding
is M5), FR-STAT-6/7 → `[x]`, FR-VAL-1 source refreshed to
`NetworkTopology.Validate`; coverage 63.0% (29/46 `[x]` + 3 `[~]`); changelog
row added. **VIVA_ANSWERS.md** +5 M3 Q&As (per-stage ρ routing, blank-doctor
days rule, --servers contract, calendar-as-one-stream, D-050 c=1 unchanged).
**BLOCKERS** — no stale M3 entries (B-001..003/005/006 unaffected). Dead-state
check (AGENTS §10.4): `bin`/`obj` deleted everywhere, `dotnet restore` →
`dotnet build` (0 warnings) → `dotnet test` 156 green. **M3 sub-blocks A–H all
DONE.**

### M3 sub-block G: acceptance pass — 2026-09-13 (feat/milestone-3-multi-stage-network)
Every kickoff acceptance item verified, live where a live check exists:
**≥128 tests / 0 warnings** → 156 tests (76 Core + 58 Data + 22 Cli), 0 warnings.
**M1 byte-for-byte** → live `simulate-params --lambda 3 --mu 4 --servers 1 --horizon
10000 --seed 42` prints served 29892, wait 0.724, ρ 0.75 (both EngineTests +
CliSimulateParamsTests guard it). **simulate-network per-stage** → 3 metric blocks
+ network totals; `--verbose` pre-run ρᵢ (0.4/0.4/0.1). **Unstable refusal via
CLI** → both commands exit 1 with one stderr line listing every unstable stage
(live: Reception ρ = 6.06 + Screening ρ = 4.00). **simulate-data 3-stage** →
fit λ0 + per-stage μᵢ + p_exit, 3 blocks, network totals. **Day-repeatability** →
same-seed `--days` runs byte-identical stdout. **M2 sweep c=2/3 refreshed**
→ D-050 changed assignment to random-among-idle; c=1 path unchanged
(6.058 min); `simulate-data samples/sample_patients.csv --servers 1,2,3 --seed
42` now reports 0.82/6.058, 0.41/0.315, 0.27/0.030 — DEV_LAUNCH §7.4 updated
with the exact command used (wording marks the refresh date and cause).

### M3 sub-block F: `simulate-network` command + day-model flags + B3 pre-run ρᵢ — 2026-09-13 (feat/milestone-3-multi-stage-network)
New `Cli/Commands/SimulateNetworkCommand.cs`: the parameter-driven twin of the
fitted path. `--lambda λ₀ --c 1,2,3 --mu μ₁,μ₂,μ₃` build `StageSpec[]` (names
default to the clinic flow unless `--stages` overrides), `--p-exit` binds to
the second-to-last stage and requires ≥ 3 stages, and the D-009 day model lands
as CLI flags: `--days N` → `Engine.Run(topology, ClinicCalendar(), N, seed,
--cap)` with `--start-day` naming day 0 (D-051 block-relative weekdays);
`--horizon` stays the no-`--days` run mode and the two are mutually exclusive.
`--verbose` prints the B3 pre-run routing-derived ρᵢ per stage
(`NetworkTopology.EffectiveArrivalRate/RhoFor`, pure reads); calendar runs add
a `── Clinic day model ──` header before `PrintNetworkMetrics`. `CliShared`
gains `TryParsePositive`. Unstable refusals exit 1 with the single stderr line
listing EVERY unstable stage. Tests: 12 new facts in
`CliSimulateNetworkTests` (3-stage run, p-exit run, --verbose ρᵢ block,
5-day same-seed reproducibility, all-stages unstable refusal, 7-case
usage-error theory) — full suite **156 green** (76 Core + 58 Data + 22 Cli),
0 warnings. Live smoke: `--verbose --days 5 --cap 80` serves 124 patients with
pre-run ρᵢ (0.4/0.4/0.1), day-model header, and 3 per-stage blocks. D-053
(logging the inline-flags-vs-JSON decision). The JSON-scenario deviation from
the kickoff TODO wording is intentional and owner-visible via D-053.

### M3 sub-block E: stage-aware `simulate-data` — 2026-09-13 (feat/milestone-3-multi-stage-network)
`simulate-data` now runs the fitted 3-stage network. New `ClinicStageOrder`
(Data/Preprocess): canonical clinic flow Reception → Screening → Doctor, used
to (1) order detected stage pairs regardless of CSV column order and (2) power
the only validator relaxation that M3 data needs — a stage column may be blank
only when that stage comes strictly after the row's `departure_stage` in the
flow (a Screening exit legitimately has no doctor times). `DataValidator`
recognises this via `StageMayBeBlankFor`; all prior strictness (blank arrival,
blank earlier-stage cells, unparseable times, inverted pairs, out-of-order
arrivals) is unchanged. `SimulateDataCommand` was rewritten: λ₀ = 1/mean
inter-arrival; per-stage μᵢ = 1/mean(service) over the stage's usable rows
(refusal if a stage has none); p_exit via `PExitCalculator` with the exit stage
bound to Screening **only when Doctor exists** (else all exit at the last stage
and no p_exit — routing λ_doctor = λ₀·(1−p_exit) only applies with Doctor).
Single-stage files keep the M2 `--servers 1,2,3` sweep byte-for-byte (the same
`Fitted from data: λ = 0.2 … stage 'Screening').` line and PrintMetrics);
stage-aware files require exactly one `--servers` count per detected stage in
flow order (mismatch = usage exit 2) and run one network. New
`Program.PrintNetworkMetrics` emits a per-stage block (patients served, avg
wait, avg queue, stage util, per-server util, throughput, ρᵢ) plus network
totals. Unstable networks refuse with exit 1 and the message listing EVERY
unstable stage (λᵢ, cᵢ, μᵢ, ρᵢ). Tests: 2 DataValidator facts (blank doctor
allowed for Screening exit; blank reception for Doctor exit still rejected) +
3 CLI facts (`CliSimulateDataNetworkTests`: 3-stage fit+p_exit=0.7+3 metric
blocks, all-stages unstable refusal, server-count mismatch exit 2). 144 tests
green (76 Core + 58 Data + 10 Cli), 0 warnings; live smoke run verified a full
3-stage run (109 served, per-stage blocks, network totals). D-052.

### M3 sub-block C: clinic calendar + engine arrival gating — 2026-09-13 (feat/milestone-3-multi-stage-network)
`ClinicCalendar` (Core/Calendar) encodes the OPD schedule: open Mon–Thu + Sat,
window 08:15–11:00 (CONTEXT §1.1), optional daily cap, and a start-day anchor
(future `--start-day`). The time model (D-051) anchors t = 0 at the day-0 window
so all times are ≥ 0: a day block is 24 h from the anchor, weekday =
`(startDay + block) mod 7`, admission window = first 165 minutes of an open
block. Arrivals are ONE continuous Poisson stream that the engine gates at fire
time — the demand exists, the calendar is the admission gate — so no per-day
draw resets and no first-arrival-after-the-weekend special case. New `Engine.Run
(topology, calendar, generatorDays, seed, dailyCap)` (+`CalendarGate` nested
class) shares `RunCore` with the horizon path; the byte-for-byte M1 loop is
untouched (calendar == null branch). Operating time per open day = first admitted
arrival → last service end, summed (D-018, not diluted by nights/weekends);
services drain past 11:00 (FR-SIM-6). 22 new tests (11 ClinicCalendar + 7 engine
facts + theory rows): defaults, window boundary (165 exclusive), Fri/Sun admit
zero, start-day-Friday shift, cap binds 4/day × 5 open days and resets daily,
same-seed reproducibility incl. AdmittedPerDay, drain-into-the-evening with
μ = 0.1/ρ = 0.5, arg guards. 139 tests green (76 Core + 56 Data + 7 Cli),
0 warnings. D-051.

### M3 sub-block B: per-stage ρ refusal at engine level — 2026-09-13 (feat/milestone-3-multi-stage-network)
B1/B2 landed inside sub-block A (NetworkTopology uses the D-007 product rule for
λᵢ = λ₀·Π(1−p_exit), and Validate() throws UnstableSystemException listing every
offender with λᵢ, cᵢ, μᵢ, ρᵢ). This letter proved the full engine path (FR-VAL-1)
with three new tests: doctor-only-unstable (ρ = 1.20, refusal carries the
routing-derived λ = 1.800 and does NOT mention the stable stages), two-stages-
unstable (both Reception 3.00 and Screening 1.50 listed with their cᵢ), and an
all-stable network whose StageMetrics report ρ = 0.30 / 0.375 / 0.375 per stage
(FR-STAT-6 engine groundwork). B3 (CLI `--verbose` per-stage ρᵢ before the run)
is genuinely a network-command feature, so it is folded into E/F rather than
half-built against the still-single-stage CLI. 117 tests green (54 Core + 7 Cli
+ 56 Data), 0 warnings. Milestone-3 sub-block plan persisted into TODO.md so the
kickoff survives sessions.

### M3 sub-block A: engine N-stage generalisation + server-assignment latent-bug fix — 2026-09-13 (feat/milestone-3-multi-stage-network)
First commit on the branch cleared the stale M2 `[~]` TODO row (`8d26fa6`,
message per kickoff). Then the engine became network-capable without a rewrite:
new `Core/Stages/{StageSpec,Stage,NetworkTopology}` and `Engine.Run(NetworkTopology,
seed, horizon)`; `SimulationResult.StageMetrics[]`; `Patient.SystemArrivalTime` +
`AdvanceToStage`; `UnstableSystemException` now lists ALL unstable stages
(`UnstableStage` payload). Routing uses the existing EventType mapping (stage i →
completion event i+1, D-006), and draws a uniform against p_exit only at the exit
stage (FR-SIM-3). Latent M1 bug fixed (D-017 vs actual FirstOrDefault code):
idle assignment is now `IServerSelectionPolicy` — `RandomIdleSelection` (default;
no draw when a single server is idle → single-server stream untouched) and test-only
`LowestIdSelection`. Byte-for-byte M1 guarded two ways: Core test
(`Run_SingleStage_ByteForByteRegression`) and a CLI test asserting the exact
simulate-params output lines (served 29892, wait 0.724, ρ 0.75). Balance regression
uses a low-load 2-server stage (λ=2, μ=4, c=2): random-diff < 0.10, lowest-ID-diff
≥ 0.10. Test infra fix: CLI tests share the static Serilog global, so the assembly
is now `CollectionBehavior(DisableTestParallelization)` (was a latent race — new
second CLI class tripped it). 114 tests green (51 Core + 7 Cli + 56 Data),
0 warnings. Decisions D-049 (NetworkTopology generalisation), D-050 (server fix).

### M2.5 Sample data precision fix — 2026-09-13 (fix/sample-data-precision)
Owner's smoke test found chi-square rejecting the sample's exponential fit at p ≈ 0
for inter-arrival AND service. Root cause: the sample stored whole minutes
(`H:mm`), collapsing continuous exponential draws to ties — the empirical
distribution looked discrete. Fix (D-048): generator now writes `H:mm:ss`
(quantised to 1/60 min); RNG stream untouched (same seed-42 draws), so the
underlying samples are identical. TimeParser already accepted `H:mm:ss`; a new
`TimeParserTests` case (`8:17:30` → 497.5) pins it — HH:MM cases unchanged.
Samples + dirty fixture regenerated. `fit` now reports p = 0.103 / 0.258, both
Fail-to-reject; sweep numbers refreshed (ρ 0.82/0.41/0.27; μ̂ 0.683 from the now
untruncated service mean 1.464). 98 tests green. VIVA line on rounding vs
chi-square power added; DEV_LAUNCH §7.4 verified block updated.

### Session Handoff — 2026-09-13 10:12
Branch: feat/milestone-2-data-and-fitting
Status: Clean (3 commits, not yet pushed; awaiting owner review — do not merge)

Done
- [x] M2 sub-blocks A–F: Data layer project (loaders, DataValidator, preprocessing, 5 fitters, chi-square, parameter modes, exporter; 52 Data tests green)
- [x] M2 sub-block G: CLI subcommand dispatcher (`simulate-params`, `verify`, `fit`, `simulate-data`, `export`; 5 new CLI tests green)
- [x] M2 sub-block H: `scripts/sample-data-generator` + `make-sample-data.sh`; committed `samples/sample_patients.xlsx`/`.csv` + dirty fixture; `.gitignore` negation
- [x] M2 sub-block I (Ubuntu half): dead-state Release build (0 warnings) + full suite (97) + live command verification (verify exit 0/1, fit + fit JSON, simulate-data sweep 0.81/0.41/0.27, export) + M1 regression (ρ 0.75, wait 0.724)
- [x] M2 sub-block J (docs): DEV_LAUNCH §5/§6/§7/§8/§10/changelog; USER_MANUAL §7 (Loading Real Data) + renumber; REQUIREMENTS 50.0% (23/46); DECISIONS D-045..D-047; VIVA_ANSWERS 6 entries; BLOCKERS B-004 → Resolved; TODO rows

In Progress
- (none — final `dotnet build`/`dotnet test` re-check before the docs commit is the last step of J)

What is complete:
Milestone-2 data path end-to-end on `feat/milestone-2-data-and-fitting`. Data: `DataLoaderFactory`/`ExcelLoader` (ClosedXML)/`CsvLoader` (CsvHelper)/`DataSet`; `TimeParser`; `StagePairDetector` (N-stage generic); `InterArrivalCalculator`; `ServiceTimeCalculator`; `PExitCalculator` (Reception excluded); `DataValidator` (per-row issues, strict Screening/Doctor — D-038); 5 fitters + factory (MLE; gamma MoM D-041; σ denom n D-043); `BinSelector` k=⌈√n⌉∈[5,20]; `ChiSquareTest` (equal-probability bins D-040, fail-loud guards D-044, CDF delegates D-042); `ModeValidator` soft warnings; `DataExporter`. CLI: dispatcher on args[0] (D-046); `fit` writes `logs/fit-*.json`; `simulate-data` sweeps servers, per-count refusal, exit 0 if any ran. Samples: deterministic seed-42 generator (D-047) → 60-row sample (Exp λ=0.5 / Exp μ=0.666…; all exit Screening ⇒ p_exit 1.0) + 1-defect-per-row dirty fixture; B-004 Resolved. Tests: 97 green (36 Core + 6 CLI + 55 Data), 0 warnings. Commits: 99cf03b (data layer), 3490cff (decisions D-038..D-044), 148d624 (CLI rework), 52b5ddf (generator + samples + fixtures). Push pending owner review.

What remains:
Owner review + merge of this branch into `main`. Milestone-3 (multi-stage network with p_exit routing, clinic calendar). Linux half of sub-block I is verified; Windows dead-state (B-006) remains gated. `scripts/verify-traceability.sh` still to be created.

Next Session Should Start With
- Await owner merge; then M3 (engine multi-stage wiring + routing from PExit, per-stage ρ refusal FR-VAL-1/FR-STAT-6, clinic hours)
- `scripts/verify-traceability.sh` orphan-check script
Blocked
- None active (B-005 Avalonia template, B-006 Windows dead-state are pending, not blocking)

Git State
Commits made this session: 99cf03b (feat: data project), 3490cff (docs: M2 decisions D-038..D-044), 148d624 (feat: CLI subcommands), 52b5ddf (feat: deterministic sample data + dirty fixture). Docs-pass commit follows this handoff.
Pushed to origin: No (branch created from main e722f04; push blocked until owner review — per §13 rules, awaiting approval)
Uncommitted changes: this PROGRESS handoff + the rest of the docs pass (DEV_LAUNCH/USER_MANUAL/REQUIREMENTS/DECISIONS/VIVA_ANSWERS/BLOCKERS/TODO edits) — to be committed as one `docs:` commit

Build & Test
dotnet build: PASS (0 warnings, Release)
dotnet test: PASS — 97 passed, 0 failed (36 Core + 6 Cli + 55 Data)
Warnings: 0

Files Touched
src/OpdSimulator.Data/...: added (Loaders, Preprocess, Validation, Fitting, Parameters, Export, csproj + MathNet 5.0.0)
src/OpdSimulator.Cli/...: added Commands/*; modified Program.cs, csproj (Data ref), Loggers
tests/OpdSimulator.Data.Tests/...: 7 test files + FixtureTests + Fixtures/dirty_missing.xlsx (55 tests)
tests/OpdSimulator.Cli.Tests/...: CliDataCommandTests (5); CliRefusalTests updated
samples/: sample_patients.xlsx + .csv (committed)
scripts/: sample-data-generator/ + make-sample-data.sh
docs/...: modified DEV_LAUNCH, USER_MANUAL, REQUIREMENTS, DECISIONS, VIVA_ANSWERS, BLOCKERS, TODO, PROGRESS
.gitignore: negates the sample CSV

Decisions Made
- D-045 Sample CSV committed via .gitignore negation (samples/sample_patients.xlsx|.csv)
- D-046 CLI subcommand dispatcher (simulate-params/verify/fit/simulate-data/export; server sweep)
- D-047 Sample data + dirty fixture generated deterministically (seed 42), not hand-written
Assumptions Added/Changed
- B-004 (waiting for real sample) → Resolved: project proceeds with the generated stand-in; real clinic data remains a drop-in at the same path (CONTEXT §5.5 format unchanged).
Notes for Next Session
- The unfitched docs commit must include everything in the handoff. Do not force-push; do not merge; report for owner review.
- `samples/sample_patients.csv` verified λ-hat 0.562 (mean 1.78 min) — sampling variation of seed 42, explained in VIVA_ANSWERS.

## Resume — 2026-09-13 09:37 — reconciled: 3 findings

1. **fix/cli-clean-refusal merged into `main`** (`e722f04 merge: clean CLI refusal output`): the PROGRESS top entry still says "awaiting owner review; do not merge". Reality wins — the clean-refusal fix (D-037) is on main. Next M2 branch must start from this main.
2. **B-004 (real sample file) resolved by owner instruction:** the M2 kickoff orders programmatic sample generation (samples/sample_patients.xlsx + .csv via scripts/make-sample-data, plus a dirty fixture), superseding D-014 "wait for the owner's real sample". B-004 to be moved to Resolved when M2 starts.
3. **DEV_LAUNCH §7 "Planned (M2): --input/--days" is stale:** the M2 kickoff specifies subcommands (simulate-params, verify, fit, simulate-data, export), not `--input/--days`. §7 must be rewritten during M2 (J1).
4. **Test-count drift:** kickoff I1 expects "34 + ≥20 = ≥54"; current main has **37** tests (M1 + Core.Tests 36 + Cli.Tests 1 after the clean-refusal merge), so the M2 target should read **37 + ≥20 = ≥57**.

### Pre-M2 Micro-Fix — 2026-09-13 09:30 (fix/cli-clean-refusal)
Owner requested a clean CLI refusal for unstable configurations (pre-M2 fix) after M1 was merged to
main as `5720772`. Completed: `Program.cs` converted from top-level statements to a class-based
`Program` exposing public `static int Run(args, stdout, stderr, fileLogger)` for in-process testing;
the `UnstableSystemException` catch now writes exactly one line `Refusing to run: {ex.Message}` to
stderr with **no stack trace** and logs the full exception to the file logs only via a file-only
Serilog logger (`shared: true` writers — D-037). `UnstableSystemException.Message` no longer repeats
the "Refusing to run: " prefix (removed to avoid "Refusing to run: Refusing to run: …"). New project
`tests/OpdSimulator.Cli.Tests` (added to sln) with `CliRefusalTests` asserting exit 1, clean stderr
(no `at OpdSimulator`, no exception type), empty stdout, and that the detail logger captured the
exception while the console-level logger captured none. Docs: DEV_LAUNCH §5 F3 example + §6 (37 tests)
+ §8 layout + changelog; DECISIONS D-037; TODO row. Verified: `dotnet build` 0 warnings; 37/37 tests
green; F3 runs show a single clean stderr line with exit 1 while `logs/errors-*.log` retains the full
stack trace; F2 (ρ=0.75) unchanged. Committed as `5b0d4c4` and pushed to `origin/fix/cli-clean-refusal`
— awaiting owner review; do not merge.

### Session Handoff — 2026-09-13 09:15
Branch: feat/milestone-1-single-stage-engine
Status: Clean (local branch, 4 commits, not pushed — awaiting owner approval per §13/§14 wrap-up rules)

Done
- [x] Implement Event, Queue, Server, Patient classes (M1: A1–A6)
- [x] Implement FEL (priority queue) and generic N-stage DES engine — validated against analytical M/M/1
- [x] Write unit tests for engine (E1–E8, 34 facts)
- [x] Add headless CLI mode to `OpdSimulator.Cli` (`--lambda/--mu/--servers/--horizon/--seed`)
- [x] FIX: wait/system accumulation `long`→`double` (E7 exposed 0.41 vs analytical 0.75; now 0.724)

In Progress
- Implement event log and step-by-step trace (viva trace file) — the engine's Debug event trace is
  live and FR-VAL-4 is `[x]` (review fix, EventTraceTests); the human-readable trace file +
  5-patient hand trace remain from this row.

What is complete:
Milestone 1 end-to-end on `feat/milestone-1-single-stage-engine` (commits 0a87fa0, fef6d01, 30b7771,
this docs commit). Core: `Patient`, `EventType`/`Event`/`FEL` (time→type→id total order), FIFO
`Queue`, `Server`, `IRandomSource`/`SeededRandomSource` (default seed 42), `ExponentialSampler`
(inverse CDF), `EngineConfig` (ρ = λ/(c·μ), refuses ρ ≥ 1), `UnstableSystemException`,
`SimulationResult`, and the generic single-stage `Engine` (arrival gating at horizon, services
draining past it, Debug event trace). CLI: full arg parsing + Serilog three-sink logging, metrics
table + ρ, exit codes 0/1/2. Tests: 34/34 green including same-seed determinism, M/M/1
average-wait 15% bound, and stability refusal; dead-state restore/build/test re-verified in
Release with 0 warnings. F2 (λ=3, μ=4): ρ=0.75, wait 0.724. F3 (λ=5, μ=4): refused, ρ=1.25,
exit 1. Docs updated: TODO, DECISIONS (D-033..D-036), REQUIREMENTS (10 rows `[x]` incl. FR-VAL-4,
coverage 21.7%), DEV_LAUNCH (§3/§5/§6/§7 + changelog), USER_MANUAL (§3 CLI path),
VIVA_ANSWERS (M1 entries). Decisions D-033..D-036 logged. Review fix: EventTraceTests added and
FR-VAL-4 promoted to `[x]` (36/36 tests green).

What remains:
Owner review + merge of this branch into `main`; then M2 (data loading & distribution fitting) —
loader, MLE Exponential fit, chi-square, sample `.xlsx`, per-stage ρ refusal extension, and the
data-driven CLI `--input/--days`.

Next Session Should Start With
Milestone 2 kickoff (reconcile → implement Data loader + fitting + chi-square + validation_report).
Second item: record the 5-patient hand trace (viva) using the engine's Debug event output.
Blocked
None new — BLOCKERS.md unchanged (B-001..B-006 all active, none touch this branch).

Git State
Commits made this session:
- 0a87fa0 feat: add Milestone 1 core engine (Patient/Event/FEL/Queue/Server/RNG/Engine)
- fef6d01 feat: add Milestone 1 headless CLI (--lambda/--mu/--servers/--horizon/--seed)
- 30b7771 test: add E1-E8 unit tests for queue, FEL, RNG, server, engine, stability
- 581c6b1 docs: update TODO/DECISIONS/REQUIREMENTS/DEV_LAUNCH/USER_MANUAL/VIVA_ANSWERS/PROGRESS for M1
Pushed to origin: No — per AGENTS §11.4/§13, the branch waits for owner review before push.

Uncommitted changes: None (checked after this commit)

Build & Test
dotnet build: PASS (Release, dead-state, 0 warnings)
dotnet test: PASS — 34 passed, 0 failed (OpdSimulator.Core.Tests; Data.Tests is an empty placeholder)
Warnings: 0

Files Touched
src/OpdSimulator.Core/Patients, Events, Queues, Servers, Distributions, Engine: added (14 files)
src/OpdSimulator.Core/OpdSimulator.Core.csproj: modified (Serilog 4.4.0)
src/OpdSimulator.Cli/Program.cs + .csproj: modified (CLI, Serilog.Sinks.File 7.0.0, ref → Core)
tests/OpdSimulator.Core.Tests/: added 8 test files + ProjectReference to Core
docs/: TODO, DECISIONS, REQUIREMENTS, DEV_LAUNCH, USER_MANUAL, PROGRESS modified; VIVA_ANSWERS at repo root modified

Decisions Made
D-033 — FEL ordering: time → event type → patient id (DECISIONS.md)
D-034 — UnstableSystemException refuses ρ ≥ 1 (FR-VAL-1)
D-035 — Deterministic RNG: System.Random wrapper + inverse-CDF exponential
D-036 — Engine event trace at Serilog Debug; console Info, file Debug, error file Warning+

Assumptions Added/Changed
None new — existing CONTEXT assumptions unchanged; no `[UNVERIFIED]` created this session.

Notes for Next Session
- Branch pushed to origin on owner approval; owner review in progress — do NOT start M2 until merged.
- FR-VAL-4 promoted to `[x]` on review (EventTraceTests added); the human-readable trace file + 5-patient
  hand trace remain tracked in TODO (not FR-VAL-4's PRD scope).
- FR-SIM-1 marked `[x]` with the interpretation noted in its REQUIREMENTS row (generic engine
  in place; 3-stage config is M3) — flagged pre-go in the resume line, owner approved.
- FR-SIM-5 `[x]` covers the M1 arrival-generation gate; the true clinic calendar
  (8:15–11:00, closed Fri/Sun) is M3/M4 work.
- `OpdSimulator.Data.Tests` is an empty placeholder — restore that project's content in M2.
- DEV_LAUNCH "Last verified" refreshed; Windows half still TBD (B-006).

## Resume — 2026-09-13 08:40 — reconciled: 3 findings

Findings (all non-blocking for M1):
1. **PROGRESS merge-status lag:** the top handoff still reads "awaiting review — do NOT merge without approval", but the docs branch was merged into `main` (d02da96). Reality wins; will refresh the CURRENT STATE line during M1 wrap-up.
2. **AGENTS §9.8 cited but absent:** the M1 kickoff references "Decision ID hygiene: AGENTS.md §9.8", but the file's §9 ends at §9.7. The rule ("scan for next free ID, never assume the last visible number") will be followed; codifying it in AGENTS.md is a proposed follow-up for owner approval.
3. **FR-SIM-1 marking needs an explicit reading:** kickoff G2 says mark FR-SIM-1 `[x]`, but FR-SIM-1's literal text is "3-stage serial network (defaults 1/2/3)" — the 3-stage *configuration* is not built until M3. PRD v1.3.0 FR-SIM-1 itself carries the clause "engine is N-stage generic from day one — the 3-stage network is a configuration, not a hard-coded structure", and D-006 commits to generic-first. Plan: build the engine as an N-stage-generic serial pipeline exercised as M/M/1 in M1, mark FR-SIM-1 `[x]` with a row note "generic engine in place; 3-stage config in M3", and log the interpretation as a decision. Flagged here so the owner can override at the "go".

### Session Handoff — 2026-09-13 08:30
Branch: docs/reconcile-decisions-and-paths
Status: Clean (pushed to origin, awaiting review — do NOT merge without approval)

Done
- [x] FIX: renumber duplicate decision IDs (D-027b→D-029, D-028b→D-030, D-029→D-031) + update references
- [x] FIX: align DEV_LAUNCH §3 + D-021/D-027 with template-only rule
- [x] FIX: fill PROGRESS.md gap for commit 9537d46
- [x] Initial bootstrap root commit task marked DONE (was stale `[ ]` in TODO despite 73787bb pushed)

In Progress
None

What is complete:
All three reconciliation drifts fixed and committed: duplicate decision IDs renumbered with every cross-reference updated and re-grepped to zero stale IDs; DEV_LAUNCH §3 + D-021/D-027 corrected to the actual template-only gitignore rule (verified via git check-ignore); PROGRESS.md gap for commit 9537d46 filled plus the §14 resume line and D-032 process-fix decision logged.

What remains:
Owner review and merge of the branch into main; then Milestone 1 (Event/Queue/Server/Patient + FEL + M/M/1 engine) on feat/milestone-1-single-stage-engine.

Next Session Should Start With
Milestone 1 kickoff (reconcile → branch feat/milestone-1-single-stage-engine → implement A1..A8 per kickoff prompt) — drifts now cleared.
Second item: none until M1 done.

Blocked
None new — BLOCKERS.md unchanged (B-001..B-006 all active, none touch this branch).

Git State
Commits made this session:
- 27a1a34 docs: renumber duplicate decision IDs and update references
- dee90c2 docs: align DEV_LAUNCH §3 and D-021/D-027 with template-only rule
- 2fecab9 docs: fill PROGRESS.md gap for commit 9537d46

Pushed to origin: Yes (docs/reconcile-decisions-and-paths → origin; PR creation intentionally skipped — owner opens it)

Uncommitted changes: none at wrap time (TODO.md Done-row additions committed below as 2fecab9)

Build & Test
dotnet build: PASS (0 warnings, 0 errors, Debug)
dotnet test: PASS (exit 0 — no tests yet; expected at scaffold, M1 adds them)

Warnings: 0

Files Touched
docs/DECISIONS.md: modified (renumber + D-021 note + D-032 entry)
docs/DEV_LAUNCH.md: modified (§3 wording, changelog D-030)
docs/PROGRESS.md: modified (resume line, gap entry, reference fixes, this handoff)
docs/TODO.md: modified (root-commit item → Done with D-028, three FIX items → Done)
AGENTS.md: read-only (its D-028 reference needed no change — verified by grep)

Decisions Made
D-032 — Reconciliation fixes gated before M1 (decision IDs renumbered, appsettings doc drift corrected, PROGRESS gap filled; rationale: ID collisions corrupt traceability)

ID renumber mapping (D-027b→D-029, D-028b→D-030, D-029→D-031); canonical D-028 = root commit unchanged.

Assumptions Added/Changed
None — no new assumptions; CONTEXT.md untouched.

Notes for Next Session
- Reference sweep verified: every D-ID cited resolves to exactly one DECISIONS.md entry (D-001..D-032 sequential, zero stale refs).
- git check-ignore verified: appsettings.json → `.gitignore:44 appsettings*.json`; appsettings.template.json → nothing (exit 1).
- DEV_LAUNCH §3 "Last verified" still reads 2026-09-13 (Ubuntu 24.04) — unchanged by this docs-only branch; restore+build re-run green.

## Resume — 2026-09-13 08:22 — reconciled: 3 findings

Findings (all pre-existing, none blocking M1):
1. **Git history vs PROGRESS lag:** `git log` shows 2 commits on main — `73787bb` (root scaffold) and `9537d46` ("docs: point DEV_LAUNCH/USER_MANUAL references at docs/; add B-006 blocker", 2026-09-13 08:16). PROGRESS.md CURRENT STATE + last handoff reference only `73787bb`. No orphaned `[~]` tasks; `9537d46` is consistent with D-028/D-031/B-006 already logged.
2. **Gitignore/doc drift:** `.gitignore` line 44–45 ignores `appsettings*.json` then negates `!appsettings.template.json` only — the `!appsettings.json` base-config negation described in DEV_LAUNCH §3 and D-021 is NOT present (reality matches D-027, which is the later decision). DEV_LAUNCH §3's "appsettings.json is committed" parenthetical is inaccurate.
3. **DECISIONS.md duplicate IDs:** D-027 appears twice (template negation), D-028 twice (second entry is actually "Single Canonical Location for Docs Instructions"), and "D-029 Root commit goes directly on main" duplicates the first D-028's content. Needs renumber/cleanup.

State summary follows below; awaiting owner "go".

## 2026-09-13 — Docs Path Fix + Reconciliation
- Commit 9537d46: DEV_LAUNCH/USER_MANUAL references repointed at docs/; B-006 blocker added.
- Reconciliation (§14) run: 3 pre-existing drifts found.
- Fixed under this branch: duplicate decision IDs, appsettings doc drift, PROGRESS.md gap.

### Session Handoff — 2026-09-13 07:06
Branch: main (no commits yet — everything untracked)
Status: In-Progress (scaffold done; awaiting owner approval to commit bootstrap)

Done
Repo cleanup: removed Python-era content (per owner go-ahead)

Set up solution structure (Core / Data / App / CLI / Tests) — all six projects in OpdSimulator.sln, template cruft removed

Add NuGet packages via NuGet, pinned (D-026) — MathNet.Numerics 5.0.0; ClosedXML 0.105.1; CsvHelper 33.1.0; Avalonia 11.3.3 family; CommunityToolkit.Mvvm 8.4.2; LiveCharts 2.0.5; Serilog 4.4.0 + sinks

Create appsettings.template.json + document copy step in docs/DEV_LAUNCH §3

Create samples/.gitkeep + docs/DEV_LAUNCH B-004 note; VIVA_ANSWERS.md stub

Dead-state verification (restore → build → test → run Cli) green on Ubuntu 24.04, 0 warnings

Update docs/DEV_LAUNCH.md: Last verified 2026-09-13; §5 rewritten (CLI until M5); §6 test note; §8 layout; §12 changelog

Create BLOCKERS.md B-005 (Avalonia template not installed)

In Progress
None (all scaffold items complete; next task from TODO is M1: Implement Event, Queue, Server, Patient classes)

What is complete:
Full scaffold: solution, 6 projects, pinned packages, config template, samples/.gitkeep, viva stub, B-005 logged, docs/DEV_LAUNCH updated and verified once; dead-state re-verified twice.

What remains:
Avalonia 12.x upgrade decision (reviewed change, needs SDK ≥ 9); M1 engine; owner commits bootstrap.

Next Session Should Start With
Implement Event, Queue, Server, Patient classes (M1)
Then: FEL + generic N-stage DES engine

Blocked
B-001..B-004 (owner-pending, none blocking scaffold)
B-005 (Avalonia template not installed → App is classlib placeholder until M5)

Git State
Owner approved the root commit directly on `main` (D-028) — committed at the end of this session; details in the final summary.

Build & Test
dotnet build: PASS (0 warnings, 0 errors, Debug)
dotnet test: PASS (exit 0 — no tests yet; M1 adds them)
Warnings: 0 (CS9057 resolved by pinning Avalonia 11.3.3)

Files Touched
src/: OpdSimulator.Core/.Data/.App/.Cli + tests/.../.Tests (all added; Class1.cs/UnitTest1.cs removed)
global.json, .gitignore: unchanged (verified)
scripts/: unchanged (verified run.sh +x, run.ps1)
appsettings.template.json: added
VIVA_ANSWERS.md: added (stub)
samples/.gitkeep: added
docs/BLOCKERS.md: B-005 added
docs/DECISIONS.md: D-026 added
docs/TODO.md: scaffold items → [x], Avalonia init → [?] B-005
docs/PROGRESS.md: scaffold lines + CURRENT STATE line + this handoff
docs/DEV_LAUNCH.md: Last verified, §3, §5, §6, §8, §12 updated

Decisions Made
D-026 — NuGet package versions pinned (incl. Avalonia 11.3.3 over 12.1.2 due to CS9057 on .NET 8 SDK)
D-027 — `appsettings.template.json` ship-in-repo (`.gitignore` negation so the §3 copy step works on a fresh clone)

Assumptions Added/Changed
None new tagged in CONTEXT.md (no new assumptions; B-005 is a blocker, not an assumption)

Notes for Next Session
`dotnet test` currently prints "No test is available" and still exits 0 — expected at scaffold, real tests arrive M1.
docs/DEV_LAUNCH §5 says run Cli ("Hello, World!") until the real Avalonia app exists (M5).
Avalonia 12.x upgrade is a reviewed change (CS9057 with .NET 8 SDK 4.8 compiler).

> **CURRENT STATE (2026-09-13):** scaffold complete, verified from a dead state (Ubuntu 24.04, 0 warnings), and **pushed to `origin/main` as the root commit `73787bb`** (exact message "chore: initial project scaffold", D-028 root-commit exception). 6 projects on net8.0; Avalonia pinned 11.3.3 (CS9057 fix, D-026). No simulation logic written yet. Next: Milestone 1 engine (Event/Queue/Server/Patient + FEL + M/M/1 validation). Blocked: B-004 (sample data), B-005 (Avalonia template → App is a placeholder until M5).

Initial commit pushed: 73787bb at 2026-09-13 07:30

## 2026-09-13 — Scaffold session

- **Solution scaffolded (D-026):** `OpdSimulator.sln` with 6 projects — Core (classlib, MathNet.Numerics 5.0.0), Data (classlib, ClosedXML 0.105.1 + CsvHelper 33.1.0), App (classlib placeholder, no Avalonia template installed — BLOCKERS B-005; references Avalonia 11.3.3 family + CommunityToolkit.Mvvm 8.4.2 + LiveCharts 2.0.5 + Serilog suite), Cli (console, Serilog 4.4.0 + Sinks.Console 6.1.1), Core.Tests + Data.Tests (xUnit, template defaults). All net8.0, zero warnings. Template placeholder files (`Class1.cs`, `UnitTest1.cs`) deleted so the scaffold carries no dead code; the test projects are intentionally empty until M1.
- **Avalonia pinned to 11.3.3 (CS9057 fix):** the initially-resolved 12.1.2 bundles analyzers requiring Roslyn 4.14 vs the .NET 8 SDK's 4.8 → 2 build warnings (CS9057), violating zero-warning policy. Pinned to 11.3.3 (builds clean; satisfies LiveCharts 2.0.5's `Avalonia ≥ 11.0`), documented as a reviewed-change gate for any future 12.x upgrade.
- **Config + samples + viva stubs:** `appsettings.template.json` (Serilog console + rolling file 7-day; `Simulation` RandomSeed 42, DefaultServers Reception 1 / Screening 2 / Doctor 3) with the copy-to-`appsettings.json` step documented in docs/DEV_LAUNCH §3; `samples/.gitkeep` + docs/DEV_LAUNCH sample-file note (B-004); `VIVA_ANSWERS.md` stub created. `.gitignore` negated the template (`!appsettings.template.json`, D-027) so it ships in the bootstrap commit instead of being swallowed by `appsettings.*.json`.
- **Dead-state verification PASSED (Ubuntu 24.04, SDK 8.0.131):** `rm -rf bin/obj` → `dotnet restore` → `dotnet build -c Debug` (0 warnings, 0 errors) → `dotnet test` (exit 0; no tests yet) → `dotnet run --project src/OpdSimulator.Cli` ("Hello, World!", exit 0). docs/DEV_LAUNCH updated: Last verified 2026-09-13, §5 rewritten for CLI-until-M5, §6 test note, §8 layout, §12 changelog.
- **Root commit approved (D-028):** owner approved committing the bootstrap directly on `main` (root-commit exception to §11.2 — no upstream base exists yet). `Simulator Start Prompt.txt` moved to `docs/agent-prompts/kickoff-bootstrap.md` to archive the session-start prompt; AGENTS §11.2 exception now references D-028. NOC docs (`docs/NOC_Data_Collection_SIPMR.{md,docx}`, `docs/make_noc.py`) deleted to complete the Python-era cleanup scope.

## 2026-09-13 — Scaffold session (Start)

- **Repo cleanup executed (2026-09-13):** Python-era content removed per owner go-ahead (`src/ku_modeling_hospital/`, `tests/` Python files, `data/`, `artifacts/`, `notebooks/`, `pyproject.toml`, `uv.lock`, `.python-version`, `project-ideas.*`, `.venv/`, pytest/ruff caches). `Simulator Start Prompt.txt` kept. TODO items + Done section updated.
- **Existing scaffold verified (2026-09-13):** `global.json` (8.0.100 / latestFeature, D-013), owner-template `.gitignore` (D-020), `scripts/run.sh` (+x) and `scripts/run.ps1` all present; no re-creation needed.

## 2026-09-13 — Bootstrap & documentation set

- Superseded the deprecated Python-era project (ku-modeling-hospital ML repo) with the **OPD Clinic Queue Simulator** (C#/.NET 8, discrete-event simulation). All old content is marked for deletion (see TODO).
- **Docs created in `docs/`:** `PRD.md` (v1.0.0 → owner clarifications logged as v1.0.1 in §10 Change History), `CONTEXT.md` (with `[VERIFIED — owner clarification, 2026-09-13]` tags in §2.3, §5.4, §5.5), `DECISIONS.md` (D-001…D-011), `TODO.md`, `docs/DEV_LAUNCH.md`, `docs/USER_MANUAL.md`, `PROGRESS.md` (this file), `BLOCKERS.md`.
- **Tech stack locked in (D-001…D-005):** C# / .NET 8 LTS, Avalonia UI (MVVM), MathNet.Numerics, ClosedXML + CsvHelper, xUnit.
- **Owner-scope clarifications applied (D-006…D-011):** `departure_stage = Reception` → warn + excluded from `p_exit`; run mode = structure, horizon = span; ρ guard uses effective per-stage arrival rate (λ·(1−p_exit) at Doctor); DES engine N-stage generic from day one (validated vs M/M/1 and M/M/2); demo 2026-09-16 scoped to load → fit → chi-square → simulate via the CLI path.
- **Root files:** `global.json` (pins SDK 8.0.100, `rollForward: latestFeature` — D-013), `README.md` (link paths corrected to `docs/`), `AGENTS.md` relocated from `docs/` to repo root (D-012).
- **Decisions added to DECISIONS.md (D-012…D-014):** AGENTS.md at repo root / two launch docs in `docs/`; global.json resolution policy; wait for owner's real sample data (no synthetic file — B-004).
- **Blocker opened:** B-004 — sample patient data file (`samples/sample_patients.xlsx`) not yet provided by owner; gates M2 demo path and loader integration tests.
- **Next (per TODO):** repo cleanup of deprecated Python content, `docs/REQUIREMENTS.md`, then solution scaffold + Milestone 1 engine.
- **Per-stage ρ model (D-015, PRD v1.1.0):** FR-VAL-1 rewritten as the per-stage stability check (ρᵢ = λᵢ/(cᵢ·μᵢ), λᵢ derived from λ₀ + routing; refusal reports ALL unstable stages with λᵢ, cᵢ, μᵢ, ρᵢ); new FR-STAT-6 (per-stage ρ display); CONTEXT §2.3 extended with the per-stage ρ / routing explanation; assumption 11 reworded; §10 Change History + TODO updated.
- **Per-server utilisation model (D-016…D-018, PRD v1.2.0):** FR-DATA-10 (optional server-ID columns → historical per-server validation, never rejects files); FR-STAT-7 (engine always reports per-server utilisation + stage-level mean; imbalance flagged when max−min > 0.15 on BOTH sources; assignment random among idle). Operating time defined as first-arrival → last-service-end per day (keeps util ≤ 1 under overtime). CONTEXT §5.7 added incl. [UNVERIFIED] blank-server-cell assumption. TODO updated with 5 implementation items + Done log entry.
- **Charts feature (D-019, PRD v1.3.0):** FR-UI-4 (LiveCharts2 charts: histogram + fitted PDF, chi-square bars, per-server utilisation bars; P2 queue-over-time + waiting-time histogram), FR-STAT-8 (fit-histogram bin count = chi-square bin count), NFR-6 (< 500 ms / 10k points, downsampling, non-blocking). New CONTEXT §6 Visual Output Analysis; SPSS, Validation, Glossary, References renumbered §7–§10. Charts assigned to M5; M1 stats + M2 fitting items annotated to expose binned/PDF/series data. TODO + Done log updated.
- **Version-control protocol adopted (AGENTS §11, D-020):** trunk-based short-lived branches, Conventional Commits, `dotnet build && dotnet test` gate, review-merge to `main`, CI on ubuntu+windows once the solution exists. Local `master` renamed to `main`; `.gitignore` replaced with the owner's template (build/IDE/test/NuGet/publish/OS/data/secrets rules; `samples/*.xlsx`+`*.csv` ignored except `sample_patients.xlsx`). Bootstrap commit queued on `chore/bootstrap` (not committed — awaiting owner go-ahead).
- **Logging & error monitoring policy (AGENTS §12, D-021):** Serilog console + rolling `logs/app-YYYYMMDD.log` (7-day) + `logs/errors-YYYYMMDD.log`; committed base `appsettings.json` (`!appsettings.json` gitignore negation); global handlers (AppDomain, UnobservedTaskException, Avalonia Dispatcher) → `logs/crash-YYYYMMDD.log` + dialog; log-level map incl. crash format (viva evidence). docs/DEV_LAUNCH §9.1 added with Linux/Windows log-tail commands; troubleshooting rows for crash/no-logs added. Serilog packages + config + handlers queued in TODO.
- **Session resume protocol (AGENTS §14, docs/DEV_LAUNCH §11, D-023):** cold-start reconciliation on "RESUME SESSION" (fixed file-read order, git/log inspection, drift detection, 6-line state summary, wait for "go"; reality wins, no guessing, never discard others' uncommitted work; `## Resume — ts — reconciled: N findings` line in PROGRESS.md). docs/DEV_LAUNCH gained §11 Resuming Work (clean/abrupt end, resume prompt, "what broke" note); Changelog renumbered to §12. PROGRESS header note extended to cover resume lines.
- **Traceability discipline (AGENTS §9.1 + §9.7, D-024):** REQUIREMENTS.md redefined as a derived PRD traceability matrix (status → source file → test → decision ID) with the PRD source-of-truth rule; §9.7 sets the update discipline ([~] on start, [x]+Source+Test on completion, Decision on design impact, re-ID in-session, ≤ 1 session drift). TODO creation item reworded to the matrix framing.
- **REQUIREMENTS.md created (2026-09-13):** derived traceability matrix synced to PRD v1.3.0 — 46 requirements (UI 4, Data 10, SIM 10, STAT/VAL 13, TOKEN 3, NFR 6), all `[ ]`, 0% coverage; FR-SIM-11 placeholder dropped (no such PRD requirement); header/net-count/changelog corrected from the draft (v1.0.0 → v1.3.0, 41 → 46). TODO gained the "Requirements not DONE until Source+Test row updated" gate and a verify-traceability script task.
- **Bidirectional rule (AGENTS §9.7, D-025):** §9.7 now forbids orphan/placeholder/"to-be-decided"/speculative rows — the matrix may only contain PRD requirements. FR-SIM-11 drop formalized (already executed at creation, approved earlier); REQUIREMENTS.md audited against PRD v1.3.0 (zero orphans); Changelog row added. "Last synced" unchanged (already today, v1.3.0).
- **Config template tracked via negation (D-027):** `.gitignore` now un-ignores `appsettings.template.json` (`!appsettings.template.json` after `appsettings.*.json`); docs/DEV_LAUNCH §3 copy step moved to the top of First-Time Restore ("Before building, copy the config template") with the `# Linux` / `# Windows` commands and the Serilog/defaults explanation folded in; bottom duplicate block removed; §12 changelog row added. REQUIREMENTS.md NFR-5 Source filled (`.gitignore, DEV_LAUNCH.md §3, appsettings.template.json`) + Decision `D-027`.
- **Single canonical location for docs instructions (AGENTS §10.7, D-030):** new §10.7 — every docs/DEV_LAUNCH.md / docs/USER_MANUAL.md instruction has exactly one canonical location; duplicates consolidated into cross-references; grep the key phrase before marking any docs task `[x] DONE`. Verification: `grep -c "cp appsettings.template.json appsettings.json" docs/DEV_LAUNCH.md` → **1** (the step exists once). `grep -c "appsettings.template.json" docs/DEV_LAUNCH.md` → **5** (2 in the §3 step command pair + 1 in the §3 Configuration-file paragraph + 1 §8 layout reference + 1 §12 changelog historical row — all legitimate references, not duplicate steps).
- **Root commit exception (AGENTS §11.2, D-028):** §11.2 now states the repository's first commit goes **directly on `main`** (root commit convention); all subsequent changes use feature branches. Supersedes the earlier `chore/bootstrap` branch plan noted in D-020. TODO bootstrap item updated to direct-on-`main`.
- **Blocker B-006 opened:** Windows dead-state verification (dead-state build + run per AGENTS §10.4) blocked until a Windows machine is available; target before M5 (UI). TODO "Verify dead-state launch on Linux + Windows" → `[?] BLOCKED` (Linux half already verified 2026-09-13 on Ubuntu 24.04).
## M2 Data layer — 2026-09-13 (feat/milestone-2-data-and-fitting)
Completed sub-blocks A–F on the Data project (no engine changes). New files:
Loaders/ (DataSet, IDataSource, ExcelLoader, CsvLoader, DataLoaderFactory),
Preprocess/ (TimeParser, StagePairDetector, StagePair, InterArrivalCalculator,
ServiceTimeCalculator, PExitCalculator, PExitResult), Validation/ (DataValidator,
DataValidationException, ValidationIssue), Fitting/ (IDistributionFitter +
5 fitter classes + factory, FittedDistribution, BinSelector, ChiSquareTest,
ChiSquareResult), Parameters/ (ParameterMode, ModeValidator), Export/DataExporter.
Added MathNet.Numerics 5.0.0 to Data; ProjectReference from Data.Tests.
52 new tests (loader, time-parser, validator, preprocess/export, fitting ×5 families,
chi-square, mode-validator) — all green; core 37 tests still green.
Fit `Distribution` holds CDF/InverseCDF delegates (MathNet's IContinuousDistribution
exposes no CDF — captured from the concrete type at construction).
Surfaced conflict: validator strictly rejects departure_stage ∉ {Screening, Doctor}
(incl. Reception) per kickoff B1 — stricter than CONTEXT §5.4 warn-and-exclude.
Logged as D-038 in DECISIONS.md (which behaviour the CLI must have is settled:
strict).
