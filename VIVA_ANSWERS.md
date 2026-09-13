# VIVA_ANSWERS.md

Prepared answers for likely viva questions. Grows as decisions are logged.

## Entries

### M1 — Engine architecture (2026-09-13, feat/milestone-1-single-stage-engine)

**Q: Why is the simulator deterministic given a seed? (NFR-4)**
A: The engine calls `IRandomSource.SetSeed(seed)` at the start of every run
(`Engine.Run`), and every random draw flows through that single, seeded source.
The Future Event List (`FEL`) adds a second guarantee: `PriorityQueue` orders
events by `(time, event type, patient id)` (`Event.CompareTo`), so even events
landing at the same clock instant are processed in a fixed, documented order.
Same seed ⇒ same sequence of `U` values ⇒ same event order ⇒ same output.
This is what lets me write `Run_SameSeed_TwoRuns_ProduceIdenticalResults`.

**Q: Why `IComparable<Event>` on the Event class? (D-033)**
A: The BCL `PriorityQueue<Event, Event>` uses `Comparer<T>.Default`, which uses
`IComparable<T>` when available — so one method pins both the "earliest first"
rule and the tie-breaking convention. Arrivals sort before service ends at the
same instant; that specific convention means "a patient arriving exactly when a
server frees up is handed straight to that server", which I can defend precisely.
If I had left ties to insertion order, the same seed could give different runs
after a `List` resize — never reproducible (and unprovable in the viva).

**Q: How do you generate exponential inter-arrival and service times? (D-035)**
A: Inverse-CDF transform on a seeded `System.Random` uniform: `X = −ln(U)/λ`,
clamped so a draw of 0 (which can't happen — `Random.NextDouble()` ∈ [0,1))
is mapped to `double.Epsilon` rather than producing `+∞`. It is deliberately
hand-written so I can derive E[X] = 1/λ on the board; the 100,000-sample mean
test confirms it (within 2%). MathNet.Numerics is held in reserve for the
Normal/Lognormal/Gamma cases later.

**Q: What does ρ mean and why do you refuse when ρ ≥ 1? (FR-VAL-1, D-034)**
A: ρ = λ/(c·μ) is the traffic intensity. If arrivals come at least as fast as
the server can work (ρ ≥ 1), the queue grows without bound and there is no
steady state — every metric would be a moving target. `EngineConfig.Validate()`
throws `UnstableSystemException` carrying the exact ρ, λ, c, μ and stage name;
the CLI prints it and exits 1. The = 1 boundary uses a 1e-9 float guard so a
double like `6/(3*2)` that computes to 0.9999… isn't wrongly allowed.

**Q: How did the E7 analytical test catch a real bug? (AGENTS §12.6 evidence)**
A: `EngineTests.Run_MatchesAnalyticalMM1` expects the simulated average wait at
λ=3, μ=4 to land within 15% of the analytical M/M/1 value ρ/(μ−λ) = 0.75. The
first version read **0.41** — a 45% miss. The crash was silence, so the log file
was useless; instead the test itself was the tell. Inspecting `Engine.cs` showed
`totalWaitMinutes` was a `long` and each patient's sub-minute wait was cast
`(long)`, so 73% of waits truncated to 0. Fix: accumulate in `double`.
Proof this is the whole story: after the fix the run reads 0.724 (3.5% off the
analytical value). Lesson logged: never truncate accumulated statistics with
`(long)`.

**Q: Why is M1 a "generic single-stage" engine but the report can claim N-stage?**
A: The stage routing is already written generically. Stage i finishes when event
type `(i+1)` fires (`EndEventTypeForStage`), and the engine's event switch is on
*event type*, not on a hard-coded stage count. M1 wires one stage (index 0,
`ReceptionEnd`), which is exactly the M/M/1 validation target; the three-stage
network is a configuration change in Milestone 3, not a rewrite (PRD FR-SIM-1
carries this guarantee; D-006).

**Q: Where is the utilisation sanity check? (FR-VAL-2)**
A: `Server.Utilisation(operatingTime)` returns busy-time / operating-time and
`Debug.Assert`s the result into [0, 1]; `Engine.Run` calls it per server for the
result object. Operating time is defined as first arrival (t=0) to last service
end — the FEL draining past the horizon makes `clock` exactly that (D-017).
**Q: Why did inter-arrival fitting recover λ = 0.562 when the sample generator used λ = 0.5?**
A: The generator (D-047) draws exponential gaps with rate 0.5 from seed 42, but 59 gaps over 60 patients happen to average 1.78 min rather than 2.0 — sampling variation; the standard error of the mean gap is about 0.26, so the observed mean is within 1 SE. The MLE is just λ̂ = 1/x̄, so it inherits the sample mean by construction. This is exactly why the chi-square verdict (even a "reject") must be reported with its p-value rather than trusted as a reproduction of the ground truth — and why the generator is seeded and scripted: the numbers are reproducible in the viva.

**Q: Why is departure_stage = "Reception" rejected as an error, when CONTEXT §5.4 said warn-and-exclude?**
A: The kickoff (2026-09-13, B1) insisted departure_stage be strictly Screening/Doctor; CONTEXT §5.4's Reception handling predates that and never reachable in the M2 file (the model has one screening stage). Rather than average two contradicting specs, the validator is strict (D-038) and p_exit correctly excludes Reception via `PExitCalculator` wherever such data might appear. The contradiction is logged so the GUI never re-introduces it silently.

**Q: Why does the chi-square use equal-probability bins and df = k − 1 − p?**
A: Equal-probability bins (edges at fitted quantiles i/k) give each bin roughly the same expected count, which maximises power for the χ² statistic; k = ⌈√n⌉ clamped to [5, 20] follows the rule-of-thumb and is auto-chosen (FR-STAT-3, D-040). df subtracts k−1 degrees naturally plus p fitted parameters (1 for exponential, 2 for normal/lognormal/gamma), so the p-value is honest about the fact that the parameters came from the sample. Guards reject the test when an expected count < 1 or df < 1 rather than print a meaningless p (D-044).

**Q: How does simulate-data decide λ and μ, and what does its exit code mean?**
A: λ = 1/mean(inter-arrival) and μ = 1/mean(service) over the first detected stage (prints the stage name and both means). It then runs one full engine per `--servers` value from `1,2,3`. Each count is refused independently if its ρ ≥ 1 (one clean line, exit code recorded), and the command exits 0 if at least one count ran — so a sweep with a couple of unstable counts still gives useful results without a wall of stack traces (D-046).

**Q: Why is manual hand calculation of the ground truth needed when SPSS exists?**
A: SPSS validates our numbers (parameter estimates, chi-square p-values) but the simulator must be self-contained — the viva asks "does your code reproduce textbook values?" not "does a tool you bought agree?" Hence the engine is validated against analytical M/M/1 (D-035 test) and the data layer is cross-checkable against SPSS via the exported fit JSON (D-046).

**Q: Why does verify print every issue instead of stopping at the first?**
A: Stopping at the first error forces the user into a fix-reupload refix loop. Reporting all issues (one per row, with column name and reason) lets the file be cleaned in a single pass — and the dirty fixture proves it names rows 2–6 in order, with the clean row 1 never flagged (FixtureTests).
