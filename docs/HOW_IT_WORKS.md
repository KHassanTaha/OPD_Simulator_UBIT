# How the OPD Simulator Works — End to End

Two sections. **Section 1** is the fast plain-language walkthrough: everything, in the
simplest possible steps. **Section 2** is the complete technical reference: every
formula, every decision point, with `file:line` citations against the current tree.

> Citations are `path/to/File.cs:line`. Line numbers were read from the source on
> 2026-10-21; they drift as the code changes, so treat them as pointers, not contracts.

---

# Section 1 — The Quick Explanation

## The one-sentence version

A file of past patients comes in → we measure how fast they arrive and how fast each
stage serves them → those measurements become parameters → the simulator replays a
virtual clinic day event by event → out come average waits, queue lengths,
utilisation, charts, and a full trace of everything that happened.

## Step 1 — Data comes in

You press **Upload** on the Input tab and pick a `.csv` or `.xlsx` file. The file is a
table where **one row = one patient**. The columns we care about:

| Column | What it means |
|---|---|
| `arrival_time` | when the patient walked in (e.g. `08:23`) |
| `reception_start` / `reception_end` | when they started/finished at Reception |
| `screening_start` / `screening_end` | same for Screening |
| `doctor_start` / `doctor_end` | same for the Doctor |
| `departure_stage` | where they left the system: `Screening` or `Doctor` |
| `session_date` (optional) | which clinic day the row belongs to |

Only the `arrival_time` and `departure_stage` columns are mandatory, plus at least one
start/end pair. Reception is always expected to have times.

## Step 2 — It gets checked (validated)

Before anything is calculated, the file is checked row by row: do the times parse? Is
each `end` at or after its `start`? Are arrival times in non-decreasing order? Is
`departure_stage` spelled correctly? Every problem is collected and listed as an issue
— the checker never stops at the first one. A file with **any** issue is shown in the
UI but is **not usable** for a run (you'll see the error and can't start).

## Step 3 — Numbers are measured from the data

From valid rows we compute three things:

1. **Inter-arrival times** — the gap between one patient's arrival and the next,
   *within the same session only* (we never count the overnight gap). The arrival
   rate is **λ = 1 ÷ average gap**.
2. **Service times per stage** — `end − start` for Reception, Screening, Doctor
   separately. The service rate is **μ = 1 ÷ average service time** for that stage.
3. **Exit probability (p_exit)** — of the patients who reached Screening or beyond,
   what fraction left at Screening instead of continuing to the Doctor:
   **p_exit = (rows departing at Screening) ÷ (screened patients)**.
   A related figure, **p_bypass**, is the fraction of Doctor patients who skipped
   Screening entirely (blank screening columns).

All of this lands on the **Input tab**: histograms of the measured times with the
fitted curve drawn over them, and a chi-square goodness-of-fit table saying whether
the chosen distribution is a believable model of the data ("p ≥ 0.05 → fits").

## Step 4 — A distribution is fitted (optional but automatic)

For each series we fit candidate distributions by maximum likelihood (exponential,
normal, lognormal, gamma, uniform), test each with chi-square, and rank them by AIC.
The best one wins. You can also just pick a family yourself from the dropdown — this
is **Path A (fit from data)**. If you'd rather type λ and μ by hand with no file at
all, that's **Path B (manual entry)**. Both paths end in the same settings object and
run the identical engine.

## Step 5 — The simulation runs

Press **Start**. The engine creates a virtual clock and a list of future events, then
loops: *take the earliest event, move the clock to it, handle it, repeat.* Three
stages — Reception → Screening → Doctor — each with a FIFO queue and its own team of
servers (`c` servers, you set `c` per stage).

What happens at each event:

- **Arrival** — if we're inside clinic hours (08:15–11:00) and the daily cap isn't
  full, the patient enters stage 0 (or bypasses straight to the Doctor if
  bypassing is on). Idle server? They start service immediately. Otherwise they join
  the queue. Then the next arrival time is drawn from the exponential inter-arrival
  distribution.
- **Service start** — the patient's wait is recorded (now − when they entered this
  stage), a service time is drawn from *that stage's* distribution, and a
  "service end" event is scheduled.
- **Service end** — the server frees up. Should the patient leave or move on? At the
  Screening stage a coin is flipped with probability p_exit: heads → they exit.
  (A second coin decides bypass where applicable.) If they move on they enter the
  next stage's queue or an idle server. Then the freed server pulls the next patient
  from its own queue.

**Clinic schedule:** the run counts **operating sessions**, not calendar days. The
clinic is open Mon–Thu and Sat, 08:15–11:00 = **165 minutes per session**. Friday and
Sunday are closed: they consume clock time but produce no patients, no rows, no
averages. "Days = 4" starting Saturday means Sat, Mon, Tue, Wed. The daily patient
cap resets each session. Arrivals are generated as a continuous Poisson stream; closed
times act as a *gate* that rejects them, so demand still "exists" but nobody is let in.

**Stability:** before anything runs we compute ρ = λ / (c·μ) for every stage. If any
stage has ρ ≥ 1 the queue would grow forever, so the run is **refused** with a clear
message (and the GUI's Start button is gated so you normally can't get there).

## Step 6 — Results come out

At the end the engine divides what it accumulated by the right denominator:

| Result | How it's calculated |
|---|---|
| Average wait (per stage) | total waiting time ÷ patients served at that stage |
| Average wait (system) | sum of all stage waits ÷ patients who exited |
| Average queue length | (queue length × time, integrated over the run) ÷ operating time |
| Utilisation per server | busy minutes ÷ operating time — hard-checked to be in [0,1] |
| Stage utilisation | average of that stage's servers |
| Throughput | patients completed ÷ operating time |
| System time | exit time − first arrival time, averaged over completed patients |
| Backlog at close | queue + in-service at the moment arrivals stopped (per session) |
| Drain | how long after closing the last patient was finally served |
| Stability verdict | ρ < 0.9 green, 0.9–1.0 amber, ≥ 1.0 red (red unreachable in GUI) |

On screen (Results panel) you get: the metrics table, per-stage performance measures,
a utilisation bar chart, queue-length-over-time, a wait histogram, the chi-square
table, analytical validation, and a **pinned event trace** — every arrival, start,
end, route and exit, line by line, like a receipt for the whole run. The
**Calculations** dialog prints the full arithmetic (λ MLE vs window λ, c·μ, the
busy/T denominators, flow balance) so every number on screen can be justified in the
viva.

You can also hide/show results widgets; that preference is saved. Logs are written to
`logs/` (daily app log, error log, crash log), screenshots to `logs/screenshots/`, and
fit reports to `logs/fit-*.json`. A headless **CLI** offers the same engine
(`simulate-network`, `fit`, `trace`, `export`, `verify`, …) for scripted work.

**Token Generator tab:** currently a placeholder (Phase 6, not yet built) — it will
issue tokens with estimated waits during a run.

## The two paths (never merge them)

```
Path A: file → validate → measure/fit → λ, μ, p_exit ──┐
                                                       ├→ SimulationParameters → engine → results
Path B: type λ, μ, p_exit, c by hand ──────────────────┘
```

Both are first-class: you can run a complete simulation without ever uploading a file,
and uploading a file never forces you to type anything.

---

# Section 2 — Complete Technical Reference

## 2.0 Solution layout

```
src/OpdSimulator.Core/     Events/ Engine/ Queues/ Servers/ Patients/
                           Stages/ Calendar/ Distributions/ Trace/ Statistics/
src/OpdSimulator.Data/     Loaders/ Validation/ Preprocess/ Fitting/
src/OpdSimulator.App/      Views/ ViewModels/ Services/ Controls/  (Avalonia, MVVM)
src/OpdSimulator.Cli/      headless commands
tests/                     Core.Tests, Data.Tests, Cli.Tests, App.Tests
samples/                   sample CSV/XLSX files
```

Dependency rule: **App → Core/Data; Data → Core; Core depends on nothing** (except
MathNet.Numerics for distributions/χ² and Serilog.Abstractions for logging).

## 2.1 Ingestion

**Dispatch** — `src/OpdSimulator.Data/Loaders/DataLoaderFactory.cs:25-36`: `.xlsx` →
`ExcelLoader`, `.csv` → `CsvLoader`; unknown extension throws `NotSupportedException`.

**CSV** — `src/OpdSimulator.Data/Loaders/CsvLoader.cs:26-53`: CsvHelper, invariant
culture (`:29`), first record is the header (`:31`), header lookup is case-insensitive
(`:46`). Dirty data is deliberately tolerated: `MissingFieldFound = null` (`:32`),
`BadDataFound = null` (`:33`), missing trailing fields become `""` (`:48`). Nothing is
rejected at load time — rejection is the validator's job.

**Excel** — `src/OpdSimulator.Data/Loaders/ExcelLoader.cs:25-72`: first worksheet only
(`:28`), row 1 header (`:37-39`), data from row 2 (`:42`), cells read as raw text
(`:53-72`); date-typed cells are stringified as `yyyy-MM-dd HH:mm:ss` (`:67`) for
`TimeParser` to handle.

**No deduplication exists anywhere** in the load/analyse path — duplicate rows are
counted twice, by design (they are separate observed patients unless proven otherwise).

### 2.1.1 Validation — `src/OpdSimulator.Data/Validation/DataValidator.cs:48-181`

Rules (all issues collected, never fail-fast — `:30-31`, `:180`):

| Rule | Line |
|---|---|
| Required columns `arrival_time`, `departure_stage` | `:55-56` |
| ≥ 1 `<stage>_start`/`<stage>_end` pair | `:63-70` |
| `session_date` optional, but if present must parse on every row | `:61`, `:91-110` |
| Empty rows rejected | `:80-85` |
| `arrival_time` must parse | `:113-143` |
| Arrival order non-decreasing **within a session** (new session resets) | `:117-132` |
| `departure_stage ∈ {Screening, Doctor}` case-insensitive | `:149-158` |
| Per stage: `end >= start` | `:161-177` |

Blank-cell rules (`:201-258`): Reception always required (`:228-229`); `doctor_end`
may be blank on a Doctor departure (`:239-241`); stages after the departure stage may
be blank (`:244-245`); direct-to-Doctor bypass (blank Screening + doctor timestamps)
is allowed (`:247-255`).

**Documented conflict** (`:33-38`): the validator rejects `Reception` as a departure
stage while `PExitCalculator` still counts `ReceptionExited` — surfaced deliberately;
on the GUI path Reception-exit rows make the file unusable.

**Time parsing** — `src/OpdSimulator.Data/Preprocess/TimeParser.cs`: accepts `H:mm`,
`HH:mm`, `H:mm:ss`, `HH:mm:ss`, `h:mm tt`, `h:mm:ss tt` (`:23-31`), case-insensitive
AM/PM (`:48-54`); a bare number `< 1` is an Excel day fraction, `>= 1` is minutes since
midnight (`:62-66`). Dates: `yyyy-MM-dd`, ISO date-times, `dd/MM/yyyy`; time discarded
(`:102-122`).

**Usability gate** — `src/OpdSimulator.App/Services/DataBindingResult.cs:46`:
`IsUsable = dataset present ∧ no load error ∧ zero validation issues`. `DataAnalyzer`
computes fits even for invalid files (`DataAnalyzer.cs:47`, `:148-159`) so the UI can
show *why* it's unusable, but Start stays gated.

## 2.2 Preprocessing — turning rows into parameters

**Stage detection** — `src/OpdSimulator.Data/Preprocess/StagePairDetector.cs:22-56`:
any case-insensitive `<stage>_start`/`<stage>_end` pair; unmatched halves are reported.

**Service times** — `src/OpdSimulator.Data/Preprocess/ServiceTimeCalculator.cs:22-51`:
`service = end − start` (`:43`); a row contributes only when both cells parse and
`end >= start` (`:36-41`).

**Inter-arrival times** — the GUI does **not** use the naive
`InterArrivalCalculator.cs:20-31`; it uses
`DataAnalyzer.ComputeWithinSessionGaps` (`src/OpdSimulator.App/Services/DataAnalyzer.cs:82`,
`:186-199`) so overnight gaps are excluded. `λ = 1 / mean(gaps)` (`:83`).

**Window λ** — `DataAnalyzer.cs:103-108`: `arrivals ÷ operating minutes`, where the
window is defined by the distinct `session_date` values (`:99-104`).

**Per-stage μ** — `DataAnalyzer.cs:114-122`: `μ = 1 / mean(service times)`, `NaN` when
the stage has no timings.

**p_exit / p_bypass** — `src/OpdSimulator.Data/Preprocess/PExitCalculator.cs`:

```
screening exits = rows with departure_stage = Screening          (:60)
doctor exits    = rows with departure_stage = Doctor             (:61)
bypass          = Doctor rows with blank screening_start
                  (only when the column exists)                 (:51-77)
screened        = screening + doctor − bypass                   (:84)
p_exit          = screening / screened                          (:90)
p_bypass        = bypass / all rows                             (:114-123)
```

## 2.3 Fitting and chi-square goodness-of-fit

`src/OpdSimulator.Data/Fitting/`:

| Fitter | Method | Constraints | Line |
|---|---|---|---|
| `ExponentialFitter` | MLE `λ̂ = 1/mean`, MathNet `Exponential(rate)`, log-likelihood over raw samples | — | `:18-32` |
| `NormalFitter` | MLE mean, population variance `/n` | ≥ 2 distinct samples | `:20-35` |
| `LognormalFitter` | MLE on `log(x)` | strictly positive samples | `:20-38` |
| `GammaFitter` | method of moments: `shape = mean²/var`, `rate = mean/var`; log-likelihood over positive samples only (density diverges at 0 for shape < 1) | ≥ 20 positive samples | `:26-79` |
| `UniformFitter` | min/max of sample | ≥ 2 distinct values | `:20-35` |

**Binning** — `BinSelector.cs`: `k = clamp(ceil(√n), 5, 20)` (`:25-26`); interior edges
are equal-probability quantiles of the fitted CDF (`:41-45`); endpoints are sample
min/max (`:42-43`); monotonicity guard adds `1e-9` (`:47-52`); counts use `x <= upper`
(`:63-83`).

**Chi-square** — `ChiSquareTest.cs:33-75`:

```
Eᵢ = n / k                                  (:47)   — reject if any Eᵢ < 1 (:47-50)
χ² = Σ (Oᵢ − Eᵢ)² / Eᵢ                      (:52-57)
df = k − 1 − (number of fitted parameters)   (:59-63)
p  = 1 − ChiSquared(df).CDF(χ²)             (:65)
fit rejected iff p < α                       (:72)   — α = 0.05 default (FitsService.cs:16)
```

**Auto-fit** — `GeneralDistributionFitter.cs`: needs ≥ 20 samples (`:47`); candidates
Exponential/Normal/Lognormal/Gamma/Uniform (`:52-59`); Deterministic excluded
(`:23-26`); a fit/χ² exception marks that candidate rejected rather than crashing
(`:139-180`); extra rejection if minimum expected bin count < 5 (`:50`, `:183-184`);
`AIC = 2k − 2 ln L` (`:167`), `BIC = k ln n − 2 ln L` (`:168`); winner = lowest AIC →
lowest BIC → highest p (`:200-209`).

`FittedDistribution.cs:28-45` captures MathNet density/CDF/inverse-CDF;
`FromSpec` (`:111+`) rebuilds a configured distribution for output-side verification
without refitting.

**Where it's triggered**: `src/OpdSimulator.App/Services/FitsService.cs:22-45`
(failures become a null-result `FitReport`); input charts via
`InputAnalysisService.FitAll` (`InputAnalysisService.cs:119-154`), `BuildHistogram`
(`:183-214`), `BuildChiSquareChart` (`:225-250`) — charts reuse the *same* bins and
observed counts as the table, never recomputed (`:24-30`, `:174-181`), assembled on
the UI thread by `InputAnalysisViewModel.cs:61-146`.

## 2.4 Convergence: building `SimulationParameters`

Two paths (AGENTS §19) converge on one object; both are mandatory, neither deprecated.

**μ precedence in Path A** — `ConfigPanelViewModel.EffectiveMu`
(`src/OpdSimulator.App/ViewModels/ConfigPanelViewModel.cs:1569-1591`), first non-null
wins:

1. `FittedRateFor(stage)` — MLE from the loaded file
2. `ManualMuPerStage` comma-list (FitFromData mode, when no fit for that stage)
3. the per-stage row field

**μ in Path B** — the per-stage row field is authoritative; the comma list is hidden
(`IsCommaListMuVisible` false in EnterManually mode).

**Parameter construction** — `ConfigPanelViewModel.cs:1727-1952`: manual λ and μ
converted to per-minute units (`:1748-1767`); fitted-only stage rates left null for the
coordinator to resolve (`:1794-1823`); p_exit/p_bypass overrides validated to `[0,1)`
(`:1897-1907`); run mode mapped at `:1909-1916` (ClinicDay → 1 session, MultiDay →
Days sessions, DiagnosticTrace → ignored).

**Coordinator resolution** — `src/OpdSimulator.App/Services/SimulationCoordinator.cs`:

- λ: manual → chosen λ source (selected window → binding window → MLE) (`:123-128`)
- μ: `ServiceRates[i] → ManualServiceRates[i] → fitted` (`:294-297`); only `Mean` is
  replaced, the configured family is preserved, `Mean = 1/μ` (`:304-323`)
- p_exit: manual → fitted → default `0.4` (`:385-398`); p_bypass: manual → fitted →
  `0` (`:411-420`)
- fitted `p_exit = 1.0` refused on multi-stage networks with a specific message
  (`:102-107`); single-stage normalises to 0 (`:110-114`)
- run dispatch (`:200-203`):

```csharp
result = parameters.RunMode == RunMode.DiagnosticTrace
    ? engine.Run(topology, seed, parameters.HorizonMinutes, sink)
    : engine.Run(topology, calendar!, parameters.GeneratorDays,
                 parameters.Seed, parameters.DailyCap, sink);
```

- `UnstableSystemException` is caught and rendered as a refusal banner (`:208-217`)

**Start gating (D-128)**: Start is a completeness gate, not try-then-banner. FitFromData
requires a validated file + resolvable μ for every stage; EnterManually requires λ > 0,
every stage μ > 0, p_exit ∈ [0,1), all other fields valid. Disabled Start names the
missing fields.

**ConfigPanel input flow**: `ApplyLoadedFile` → `DataAnalyzer.Analyze`
(`ConfigPanelViewModel.cs:1002-1027`) → raises `DataBindingChanged` (`:1026`) →
`MainViewModel.OnConfigDataBindingChanged` (`MainViewModel.cs:171-186`) → Input tab
refresh via `SyncInputAnalysis` (`:212-217`). Preview: `InputPreviewViewModel.SetPreview`
(`:93-125`), bindings at `InputTab.axaml:51-55`.

## 2.5 The engine (Core)

### 2.5.1 Event model

- `Event` (`src/OpdSimulator.Core/Events/Event.cs:15`) = `{Time, Type, PatientId}`,
  ordered by **(time, type, patientId)** (`:44-58`) — a total order independent of
  insertion order, so a fixed seed reproduces exactly. Note: completion enum values
  (1–3) are greater than `Arrival = 0`, so at equal timestamps **arrivals sort before
  completions**; that is the intended DES tie-break.
- `EventType` (`EventType.cs:14-26`): `Arrival=0`, `ReceptionEnd=1`, `ScreeningEnd=2`,
  `DoctorEnd=3`. The enum value *is* the stage mapping:
  `EndEventTypeForStage(i) = (EventType)(i + 1)` (`Engine.cs:909-913`).
- `FEL` (`FEL.cs:14`) is a `PriorityQueue<Event, Event>` min-heap; the event is its own
  priority (`:28`); `Dequeue`/`Peek` throw when empty (`:34-50`).

### 2.5.2 Run setup — `Engine.RunCore` (`src/OpdSimulator.Core/Engine/Engine.cs:248-340`)

Order matters:

1. `topology.Validate()` **before** seeding (`:251`) — a refused run consumes no RNG
   draws (comment `:248-250`).
2. Attach trace sink, `_random.SetSeed(seed)`, emit the seed trace row (`:256-259`).
3. Build `CalendarGate` if a calendar was supplied (`:278`); session count = calendar's
   count, or 1 implicit session for a horizon run (`:282`).
4. Fresh runtime stages (`:284-286`), per-stage service samplers from
   `StageSpec.EffectiveServiceDistribution` (`:298-300`) — building them draws nothing.
5. FEL, `inService`/`patientServer` maps, clock, queue-area and wait accumulators,
   `totalSystemMinutes`, `completed`, `nextPatientId = 1` (`:302-313`).
6. Per-run buffers: wait samples, queue series, per-session served/queue-area,
   per-stage last service end, backlog arrays, generated-value samples (`:316-340`).
7. First arrival at t = 0 (`:343`) — always, even in calendar mode; if day 0 is closed
   the gate rejects it but the Poisson stream still advances.
8. `closeTime = gate?.StopTime ?? horizonMinutes` (`:349`).

### 2.5.3 The loop (`Engine.cs:351-439`)

```csharp
while (fel.Count > 0) {
    Event evt = fel.Dequeue();
    clock = evt.Time;                       // time advances to the event, never idles

    int intervalSession = gate?.CurrentSessionIndex ?? 0;   // BEFORE AdvanceTo
    gate?.AdvanceTo(clock);                                // move session cursor
    // capture backlog at first event >= close        (:373-394)
    // area += queue.Length × (clock − lastTime)      (:398-407)
    // log, then dispatch Arrival | *End              (:413-432)
    // optional stop after a completion cap           (:437-438)
}
```

Why capture `intervalSession` before `AdvanceTo`: an interval spanning a session
boundary is charged to the session that *produced* the queueing work (FR-STAT-9,
`Engine.cs:356-361`). Why capture backlog before the handler: a service ending exactly
at close still counts as present at close (`:373-386`).

Termination: arrivals are only scheduled strictly before `stopTime` (`:684`), so the
FEL drains. Services run past the window and past midnight; only arrivals stop.

### 2.5.4 Arrival handler (`Engine.cs:601-692`)

1. Window check first, before any draw (`:623`) — a rejected arrival consumes no
   routing/bypass draw (comment `:619-622`).
2. Bypass draw only when `BypassAtArrival` (2-stage topology, `S = 0`) (`:629-640`).
3. Destination = bypass target or stage 0; admission =
   `inWindow ∧ gate.TryAdmit(clock, screeningBound: destinationIndex == 0)` (`:642-646`).
   Rejected patients leave no trace row and consume no service draw.
4. Start service on an idle server (selection policy below) or enqueue (`:650-669`).
   Arrival trace row uses `q = queue + 1` because the patient is present either way
   (`:654-656`).
5. Next arrival: `interArrival = −ln(u)/λ`, `next = clock + interArrival`; only if
   `next < stopTime` is it enqueued *and* recorded in `_generatedInterArrivals`
   (`:672-690`). The draw itself happens on **every** arrival including rejected ones —
   demand is continuous, the calendar is the gate (`:186-193`).

### 2.5.5 Service start (`Engine.cs:694-732`)

```csharp
patient.MarkServiceStarted(clock);
server.StartService(clock);
stageWaitMinutes[stage] += clock - patient.ArrivalTime;          // wait stamped HERE
_stageWaitSamples[stage].Add(new WaitSample(clock, wait));       // timestamp for session bucketing
double serviceTime = _serviceSamplers[stage].NextSample();       // stage's own family
_generatedServiceSamples[stage].Add(serviceTime);
fel.Enqueue(new Event(clock + serviceTime, EndEventTypeForStage(stage), id));
```

### 2.5.6 Service end (`Engine.cs:734-853`)

- `gate?.NoteServiceEnd(clock, stage)` and per-session served counter (`:747-762`) —
  called after `AdvanceTo`, so completion is attributed to the session whose block
  contains it.
- Mark patient/server free, drop the maps (`:764-767`); record last service end per
  stage for drain (`:769-774`).
- **Exit decision** (`:776-789`): last stage always exits; otherwise only at
  `ExitStageIndex`: `leaves = u < ExitProbability`.
- **Bypass decision** (`:805-821`): fires at completion of stage `S−1`; the constructor
  guarantees `S−1 ≠ ExitStageIndex` so both draws can never fire on one completion
  (`NetworkTopology.cs:110-113`). When `S = 0` the trigger index is −1 and this branch
  never runs (handled at arrival instead, `NetworkTopology.cs:184`).
- Exit → `totalSystemMinutes += clock − SystemArrivalTime`, `completed++` (`:830-837`).
- Then the freed server pulls the next queued patient (`:846-852`) — *after* routing,
  so the freed server never jumps ahead of the routing decision.

### 2.5.7 Routing (`RouteTo`, `Engine.cs:875-907`)

`patient.AdvanceToStage(next, clock)` resets per-stage milestones, re-anchors
`ArrivalTime`, preserves `SystemArrivalTime` (`Patient.cs:94-103`); then start-or-queue
exactly as at arrival, then emit the ROUTE row.

### 2.5.8 Queues, servers, stages, patients

| Object | File | Key facts |
|---|---|---|
| `Queue` | `Queues/Queue.cs:14-51` | unbounded FIFO, **no capacity, no balking, no reneging**; one queue serves all `c` servers |
| `Server` | `Servers/Server.cs:13-83` | `BusyTimeMinutes`, `PatientsServed`; `EndService` refuses backwards intervals; `Utilisation = BusyTime / operatingTime` **throws** if outside [0,1] (`:73-83`) — hard invariant, not a clamp |
| `RandomIdleSelection` | `Servers/RandomIdleSelection.cs:17-48` | exactly one idle → deterministic, **no draw** (preserves M/M/1 regression) (`:30-33`); ≥2 idle → `floor(u·n)`-th idle (`:35-43`) |
| `Stage` | `Stages/Stage.cs:24-68` | owns `Queue` + `Servers`; `Rho = λ_eff / (c·μ)` (`:58`) |
| `Patient` | `Patients/Patient.cs:15-104` | `SystemArrivalTime` never changes; `ArrivalTime` re-anchored per stage; `SystemTimeMinutes` for aggregates is computed by the engine from `SystemArrivalTime` (`Engine.cs:835`) |

### 2.5.9 Topology, λ propagation, exit/bypass/cap — `Stages/NetworkTopology.cs`

Constructor validation (`:54-140`): λ > 0; ≥ 1 stage; `p_exit ∈ [0,1)`; exit stage must
exist when `p_exit > 0`; `p_bypass ∈ [0,1)`; bypass needs ≥ 2 stages; destination
strictly after the skipped stage; bypass trigger ≠ exit stage (`:110-113`); cap rate > 0
and capped stage exists (`:131-136`).

Derived arrival rates `ComputeArrivalRate(i, applyCap)` (`:268-357`): start with mass
1.0 at stage 0 (split at arrival for the 2-stage case); at the bypass trigger move
`incoming·p_bypass` to the destination **outside** the cap; at the exit stage forward
`incoming·(1 − p_exit)`; then apply the admitted-load cap from the Screening cap stage
onwards (`:343-353`), scaling only the non-bypass share:

```csharp
double capMass = capRate / ArrivalRate;
if (offered > capMass) { admittedShare = capMass / offered;
    mass[j] = freeAtDestination[j] + (mass[j] - freeAtDestination[j]) * admittedShare; }
```

`RhoFor(i) = EffectiveArrivalRate(i) / (cᵢ·μᵢ)` (`:364-371`).

Geometry: 3-stage OPD = Reception/Screening/Doctor, exit & bypass stage = 1, bypass
drawn at Reception's completion; 2-stage file topology = Screening/Doctor, exit &
bypass stage = 0, bypass drawn at arrival.

### 2.5.10 Clinic calendar — `Calendar/ClinicCalendar.cs`

| Constant | Value | Line |
|---|---|---|
| `MinutesPerDay` | 1440 | `:32` |
| `DefaultWindowStartMinutes` | 495 = 08:15 | `:35` |
| `DefaultWindowEndMinutes` | 660 = 11:00 | `:38` |
| `DefaultOpenDays` | Mon, Tue, Wed, Thu, Sat | `:41-44` |
| `OpenDurationMinutes` | **165** | `:92` |

Time anchor: **t = 0 is 08:15 on block 0**; `FormatClock` wraps at 1440 (`:182-192`);
`IsInArrivalWindow` uses `dayIndex = t/1440`, requires an open weekday and
`blockMinute < 165` — **the end is exclusive** (11:00:00 is closed) (`:163-174`).

**Session enumeration (D-199)** — `EnumerateSessions(n)` (`:130-147`) walks blocks and
keeps only open days: **Days = N means N operating sessions**, not N calendar blocks.
`ClinicSession = (Ordinal, BlockIndex, DayOfWeek)` (`ClinicSession.cs:31`). A closed
day consumes clock but produces no session, no metrics row and no `0.0` in any average.

**CalendarGate** (private nested class, `Engine.cs:962-1233`) owns all calendar state:

- `StopTime = CloseTimeForSession(SessionCount−1)`; `CloseTimeForSession(s) =
  BlockIndex·1440 + 165` (`:1001`, `:1063-1064`).
- `AdvanceTo(t)` (`:1090-1105`): cursor moves forward only, to the last session whose
  `BlockIndex <= t/1440`; **on move it resets both counters** — the daily cap resets
  per operating session, not per 1440-minute block.
- `TryAdmit` (`:1132-1148`): rejects outside window; rejects when
  `screeningBound ∧ DailyCap reached`; counts admitted, records session first arrival.
- `SessionIndexAt(t)` (`:1187-1199`): pure lookup used to bucket wait samples after the
  fact (clamped before first / after last session).
- Per-session denominators: `OperatingTimeMinutesForSession = lastServiceEnd −
  firstAdmittedArrival` (0 if none) (`:1213-1216`); total = Σ sessions (`:1223-1232`).

What the calendar explicitly does **not** do: no session open/close events exist (only
4 `EventType`s, 6 `TraceEventType`s); queues are never dropped at close; no queue
capacity; the Poisson stream runs through closed days and is gated, not paused.

### 2.5.11 Distributions and RNG

- `IRandomSource` (`Distributions/IRandomSource.cs:11`) = `SetSeed` + `NextDouble`.
  `SeededRandomSource` default seed **42** (`:15`); `SetSeed` rebuilds `System.Random`
  (`:29-32`). The engine always wraps it in `TraceRandomSource` (`Engine.cs:109`) which
  counts draws and exposes the last one for Debug traces.
- Exponential (inter-arrival): `ExponentialSampler.cs:11-41`:

```csharp
double u = _random.NextDouble();
if (u <= 0) u = double.Epsilon;       // guard: -ln(0) = +∞
return -Math.Log(u) / rate;
```

- Service: `DistributionSamplerFactory.Create` (`:24-59`) dispatches on
  `DistributionFamily` (Exponential=0 … Uniform=5): exponential hand-rolled
  (`ExponentialDistributionSampler.cs:41-48`, same transform deliberately so the draw
  sequence didn't change when the legacy sampler was replaced); Normal/Lognormal/
  Gamma/Uniform go through MathNet via `MathNetRandomAdapter` (`:32-129`), which
  overrides every `Random` member MathNet could call so **one** seed governs everything.
  `StageSpec.EffectiveServiceDistribution` falls back to Exponential with mean `1/μ`
  (`StageSpec.cs:113-117`) — that fallback is what preserves classic M/M/c behaviour.
- **Draw order per arrival** (determinism): bypass (2-stage only) → server selection
  (only if ≥2 idle) → service time (only if started immediately) → inter-arrival
  (always). **Per completion**: exit draw → bypass draw → next patient's service draw.
- Generated samples retained: `_generatedInterArrivals` (`:688`),
  `_generatedServiceSamples[stage]` (`:725`) → `SimulationResult.GeneratedInterArrivalSamples`
  / `GeneratedServiceSamplesByStage` for **output-side** goodness-of-fit.

### 2.5.12 Run modes and trace

`RunMode` (`src/OpdSimulator.App/Models/RunMode.cs:9-22`): `ClinicDay` (calendar, 1
session), `MultiDay` (calendar, Days sessions), `DiagnosticTrace` (minutes horizon, no
calendar/cap — `SimulationCoordinator.cs:173-179`). Cap stage = bypass stage when bypass
is on, else 0 (`:181-188`).

Trace levels (`Core/Trace/TraceLevel.cs:16-35`): `Minimal` (sink keeps nothing),
`Standard` (core columns), `Detailed` (+ server id, destinations), `Debug` (+ one row
per RNG draw). **The level affects rendering only** — the engine always emits the same
`TraceEvent` stream; `CollectionTraceSink` short-circuits on Minimal
(`CollectionTraceSink.cs:41-44`), capacity **50,000** lines, drops oldest (`:18`, `:52-55`).

`TraceFormatter.cs:29-81`: names `ARRIVAL/START_SVC/END_SVC/ROUTE/EXIT/RNG`; RNG rows
dropped below Debug (`:55-56`); location details dropped below Detailed (`:68-74`);
invariant culture (`:58`, `:71`, `:77`) so traces are byte-identical across locales;
layout `T=<time> <wall> <TYPE> <P#> <location> q=<n>` (`:80`).

Row semantics (`TraceEvent.cs:16-29`): Arrival → first-stage queue **+1**; StartService
→ queue after dequeue; EndService → queue **before** the freed server pulls; Route →
destination queue after placement; Exit → null.

### 2.5.13 Stability

- ρᵢ = λᵢ / (cᵢ·μᵢ) everywhere (`Stage.cs:58`, `NetworkTopology.cs:364-371`,
  `EngineConfig.cs:69`) — there is no single network-wide ρ.
- `NetworkTopology.Validate` collects **every** unstable stage and throws
  `UnstableSystemException` (`:390-405`) — before seeding (`Engine.cs:251`), so a
  refusal costs no draws. With a cap, the message shows capped λ *and*
  `OfferedArrivalRate` (`UnstableSystemException.cs:89-95`).
- Bands (`StabilityVerdict.cs:79-89`): Green ρ < 0.9, Amber [0.9, 1.0), Red ≥ 1.0;
  `ClassifySet` (`:103-123`) takes the max ρ, ties → earliest stage. Red is
  intentionally unreachable from the GUI (engine refuses + Start gate, D-128) and
  exists for hand-built metrics and tests (`:43-56`).

## 2.6 Statistics — exact formulas

Accumulators allocated at run start (`Engine.cs:308-340`): `areaUnderQueue[]`,
`lastMetricTime[]`, `stageWaitMinutes[]`, `totalSystemMinutes`, wait/queue samples,
per-session served/area, last-service-end, backlog arrays, generated samples.

Integration rule (`:398-407`): `area += queue.Count × (clock − lastMetricTime[i])`,
then `lastMetricTime[i] = clock`. The queue sampled is the **pre-event** length (clock
advanced first, handler run after).

| Metric | Formula | Line |
|---|---|---|
| `StageMetrics.AverageWaitMinutes` | `stageWaitMinutes[i] / servedHere` | `:507` |
| `StageMetrics.AverageQueueLength` | `areaUnderQueue[i] / operatingTime` | `:508` |
| `StageMetrics.StageUtilisation` | mean of per-server `busy / operatingTime` | `:509-510` |
| `StageMetrics.ThroughputPerMinute` | `servedHere / operatingTime` | `:511` |
| `StageMetrics.BacklogAtClose` | final session's `queue.Count + busy servers` | `:518-520` |
| `StageMetrics.DrainMinutes` | `max(0, lastServiceEnd − finalClose)` (D-191) | `:455-458` |
| `…BySession` variants | one entry per operating session (D-193) | `:522-532` |
| `SimulationResult.AverageWaitMinutes` | `Σ all stage waits / completed` | `:577` |
| `SimulationResult.AverageSystemTimeMinutes` | `totalSystemMinutes / completed` | `:578` |
| `SimulationResult.AverageQueueLength` | mean of per-stage means | `:579` |
| `SimulationResult.StageUtilisation` | mean of per-stage utilisations | `:580` |
| `SimulationResult.ThroughputPerMinute` | `completed / operatingTime` | `:582` |
| `OperatingTimeMinutes` | calendar: Σ per-session windows; horizon: final clock | `:583`, `CalendarGate:1213-1232` |

Per-session wait means bucket by **service-start timestamp** (`WaitSample.Time`),
`:474-475`; per-session queue means are `null` (not 0) for a session that served nobody
(`:477-487`); horizon runs project into one implicit session.

**Exclusions that matter**: patients still queued or in service at the end are not in
`completed` and contribute nothing to average wait/system time; a patient passing three
stages contributes three waits to the system average; system `AverageQueueLength` is
the mean of stage means, not one merged queue.

## 2.7 Outputs — what is generated

### 2.7.1 Results panel (`src/OpdSimulator.App/ViewModels/ResultsPanelViewModel.cs`,
`Views/ResultsPanel.axaml`)

- `MetricRow` / `StageMetricRow(..., BacklogAtClose, DrainMinutes)` (`:40-1160`);
  χ² rows built from `RunOutcome.Fits` (`:278-289`), columns `#, Series, Distribution,
  χ², df, p, Decision` (`ResultsPanel.axaml:497-522`), caption α default 0.05
  (`:833-842`).
- Widget order: metrics → utilisation → queue length → wait histogram → χ² →
  simulation verification → analytical validation; **event trace pinned** below the
  scroller (`WidgetScroller`, `ResultsPanel.axaml`), `MaxHeight=176`, always visible
  and removed from the customise list (FR-UI-35, Phase 8Q.4).
- Widget visibility persisted in `~/.config/OpdSimulator/ui.json` via
  `WidgetPreferences.Load/Save` (D-185: load must go through `Load(path)`).
- Chart services: `UtilisationChartService`, `QueueLengthChartService`,
  `WaitHistogramService`, assembled by `ChartControlBuilder` (LiveCharts2/Skia).
- The **Calculations** dialog is rendered by `CalculationsTextBuilder.cs`: run config,
  λ MLE vs window with divergence % (`:334-371` region), service processes
  (c, μ, c·μ, 1/μ, family), utilisation block distinguishing `busy/T` (server
  utilisation) from `busy/(c·T)` (share of stage capacity) with the contributions sum,
  flow balance (served, throughput, mean queue, mean wait, p_exit, p_bypass,
  λ_screening, λ_doctor) (`:374-401`), per-stage results with final-session vs
  mean-across-sessions backlog/drain (`:403-431`).

**Two different chi-squares — do not conflate** (§16.12 / VIVA_ANSWERS): the Input tab
χ² describes the **loaded file** against the configured family; the Results χ² table
describes the **run's** fit reports (input series and output-side generated samples).

### 2.7.2 Input tab (`Views/InputTab.axaml`, `ViewModels/InputAnalysisViewModel.cs`)

Preview (`:93-125`, XAML `:51-55`), observation window selector (`:189-219`),
`InputAnalysisView` (`:268-270`); chart cards built from the same bins/counts as the
table (§2.3). Empty state until a file is loaded; validation failures surface through
the FR-UI-17 error treatment.

### 2.7.3 CLI (`src/OpdSimulator.Cli/Commands/`)

| Command | Output |
|---|---|
| `simulate-params` / `simulate-data` / `simulate-network` | run + metrics; `simulate-network` prints resolved `Sessions`, window, cap (`SimulateNetworkCommand.cs:222-238`) |
| `fit` | per-dataset fit: `n`, params, sample mean, log-likelihood, AIC, χ², df, p, decision (`FitCommand.cs:129-141`); writes `logs/fit-yyyyMMdd-HHmmss.json` with bin edges/observed/expected and p_exit/p_bypass (`:143-231`) |
| `trace` | deterministic text trace, default `detailed`, `--real-start` 08:15, `TextWriterTraceSink`, 10,000-minute horizon (`TraceCommand.cs:124-181`) |
| `export` | one row per patient: `patient_id, departure_stage, arrival_minutes, inter_arrival_minutes`, then per-stage `_start/_end/_service_minutes`; invariant culture, `0.######` (`DataExporter.cs:42-91`) |
| `verify` | prints every `ValidationIssue`; exit 0 clean / 1 issues / 2 usage |

### 2.7.4 Logs and evidence

Serilog (`AGENTS §12`): console + `logs/app-YYYYMMDD.log` (7-day rolling) +
`logs/errors-*.log` + `logs/crash-*.log` (global handlers). Screenshots (headless frame
tests) land in `logs/screenshots/`. Fit reports in `logs/fit-*.json`.

### 2.7.5 Token Generator

**Placeholder only** — `MainWindow.axaml:93-97` (`PlaceholderContent`, Phase 6). Token
issuance with estimated waiting time is a Milestone 6 TODO, not implemented.

## 2.8 Sample data (`samples/`)

| File | Rows | Notable |
|---|---:|---|
| `opd_collection_28_sep_2026_1.csv` | 109 | real single session; 88 Screening / 21 Doctor; bypass rows present |
| `sample_3stage_clinic.csv` | 20 | p_exit = 0.7 |
| `sample_3stage_variable.csv` | 50 | all Doctor, p_exit = 0.0 |
| `sample_bypass.csv` | 20 | 4 bypass rows → p_exit = 10/16 = 0.625, p_bypass = 0.2 |
| `sample_multiday.csv` | 60 | 6 session dates, all Screening |
| `sample_overcapacity.csv` | 10 | all Screening |
| `sample_patients.csv` / `.xlsx` | 60 | Reception + Screening only; xlsx sheet named `Data` |

## 2.9 Known caveats (read before defending the code)

1. **Stage count is effectively 3.** `(EventType)(i+1)` and the loop's switch
   (`Engine.cs:909-913`, `:421-431`) only support three completion types; a 4th stage
   would throw `InvalidOperationException` on its first completion. GUI accepts up to 5
   stage rows — the topology validator does not cap the count.
2. **Daily-cap semantics differ between simulation and analytics when bypass is on.**
   `TryAdmit(screeningBound: destinationIndex == 0)` (`Engine.cs:646`) counts *entered
   stage 0*, so in the 3-stage topology the bypass share also consumes cap places,
   while `ComputeArrivalRate` holds the bypass share outside the cap
   (`NetworkTopology.cs:310-317`). With bypass off (or the 2-stage file) the two
   meanings coincide. D-190 describes the Screening-only rule.
3. **Validator vs `PExitCalculator` on `Reception` departures** — flagged in source at
   `DataValidator.cs:33-38`; unreachable on valid GUI data.
4. **`StageSpec` rate/distribution consistency guard is `#if DEBUG` only**
   (`StageSpec.cs:54-82`); Release compiles it out by design.
5. **Trace ordering on an immediate transfer** is `END_SVC → START_SVC → ROUTE` when
   the destination is idle, but `END_SVC → ROUTE` when it queues (`Engine.cs:875-907`)
   — the two cases are not ordered alike when reading a trace by hand.
6. **Completion attribution across an open-day boundary**: `AdvanceTo` runs before
   `NoteServiceEnd`, so a service finishing in the *next open day's* block is credited
   to that next session (closed blocks are unaffected) (`Engine.cs:366`, `:756`,
   `:1081-1088`).
7. **Stale comments**: `RunMode.cs:5-8` claims only `DiagnosticTrace` records a trace
   (the coordinator passes a sink in all modes, `SimulationCoordinator.cs:196-203`);
   `Engine.cs:429` says "five event types" — there are four.

---

*Created 2026-10-21 for viva preparation. Companion documents: `WORKFLOW_DIAGRAM.md`
(visual pipeline), `VIVA_ANSWERS.md` (oral Q&A), `USER_MANUAL.md` (how to use the UI),
`DEV_LAUNCH.md` (how to build/run).*
