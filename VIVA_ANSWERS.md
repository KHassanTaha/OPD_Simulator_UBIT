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

**Q: Why did chi-square initially reject the exponential fit of our own sample data?**
Integer-minute granularity reduces chi-square power. With 60 samples rounded to whole minutes, a true exponential can be rejected because the empirical distribution looks discrete, not continuous. Our sample generator uses HH:MM:SS precision so the fit test has enough resolution to accept. Real clinic data at minute granularity will need to be evaluated with this limitation in mind; the simulator will honestly reject if the fit is genuinely poor.

## Milestone 3 — multi-stage network, day model, stage-aware data

**Q: Why is ρ defined per stage and how is λᵢ routed through the network?**
A: There is no single "network ρ" — traffic intensity only makes sense stage-by-stage (CONTEXT §2.3). The routing derives each stage's arrival rate from λ₀ and the exit probability: λ_reception = λ₀, λ_screening = λ₀, λ_doctor = λ₀·(1 − p_exit) (D-007). `NetworkTopology.EffectiveArrivalRate` walks the product rule over preceding stages; `RhoFor` computes ρᵢ = λᵢ/(cᵢ·μᵢ). The refusal (`Validate`) reports EVERY unstable stage with λᵢ, cᵢ, μᵢ and ρᵢ so the answer to "why did you refuse?" is the entire bottleneck list, not one ρ.

**Q: Why does simulate-data reject nothing when a Screening exit leaves doctor cells blank?**
A: A patient who exits at Screening genuinely never reached the doctor — an empty doctor_* cell is real, not dirt (D-052). The validator accepts a stage cell only when that stage comes strictly AFTER the row's `departure_stage` in the clinic flow (Reception → Screening → Doctor). Every other blank — arrival, an earlier stage, an unparseable time — is still rejected, so strictness is preserved exactly where it guards quality.

**Q: Why is simulate-data's --servers interpreted differently for a 3-stage file?**
A: For a single-stage file `--servers 1,2,3` is the M2 sweep — three independent M/M/c validation runs and the numerically identical M1 demo path. For a stage-aware file the stages are ordered by clinic flow and `--servers` must supply exactly one count per stage (Reception, Screening, Doctor) for one network run; a count mismatch is a usage error that names the detected stages, so the contract is explicit rather than silently mis-assigned.

**Q: How is the clinic calendar modelled as one continuous arrival stream?**
A: t = 0 anchors the day-0 arrival window; a "day block" is 24 hours from that anchor with weekday (startDay + block) mod 7 (D-051). Arrivals are ONE Poisson stream for the requested days — the clinic's potential demand is Poisson — and the engine's gate admits an arrival only when the calendar says open (Mon–Thu + Sat) and inside 08:15–11:00, and under the optional daily cap. Closed days therefore simply gate everything out; the stream keeps going, so no per-day reset and no first-arrival-after-weekend special case to defend.

**Q: Why did the c=2/c=3 sweep waits change for the same seed, while c=1 did not?**
A: Milestone-1 assigned patients to the lowest-numbered idle server, biasing server 0 at multi-server stages. D-050 changed assignment to random-among-idle for fairness. A single-server stage always has the same idle server, so the random selection never draws an RNG value there and the M1 c=1 numbers hold exactly (served 29892 / wait 0.724 / ρ 0.75, locked by `Run_M1Regression_SingleStage_GoldenValues` and the CLI golden test). Caveat: the simulation metrics are numerically identical, but the OUTPUT TEXT is not byte-for-byte — M3 normalised the stage-name casing to canonical form and unified the single-stage log line to the network form (D-054). Output text is documentation, not a contract. The sample sweep's c=2/c=3 waits refreshed from 0.25/0.04 to 0.315/0.030 under seed 42 (DEV_LAUNCH §7.4).

## Milestone 4 — deterministic event trace

**Q: Why a separate trace alongside Serilog logging? (D-055)**
A: The two channels answer different questions. Serilog records the whole run
as free-form, structured diagnostics for debugging — useful but not a contract,
and unusable as a story when 29892 patients pass through. The M4 trace is a
compact, ordered, deterministic record: one row per state-changing point
(ARRIVAL / START_SVC / END_SVC / ROUTE / EXIT), rendered as
`T=… wall TYPE P# location q=…`. Because it is byte-stable (invariant culture,
floor-truncated seconds), it is the viva's walk-through artefact AND a regression
target — the golden file can't drift silently the way log prose can.

**Q: How do I defend that the trace numbers are exactly what the simulation did? (D-058)**
A: There are three independent checks, all in `TraceRegressionTests`. (1) The
engine emits the trace from the same state bookkeeping that feeds the metrics,
and tests assert the trace and stats agree: the number of EXIT rows equals
`TotalPatientsServed`, and the per-patient average wait recomputed from trace
rows equals `AverageWaitMinutes` to 9 decimal places. (2) The RNG is wrapped in
a passive observer (`TraceRandomSource`, D-057) that forwards draws unchanged,
so attaching a sink changes no metric — locked by
`AttachingTraceSink_DoesNotChangeResults`. (3) The golden fixture was
hand-verified: every time in it is −ln(U)/λ or −ln(U)/μ for the corresponding
`draw#k U=…` of the reference `Random(42)` sequence.

**Q: Why is the trace byte-identical for the same seed across machines?**
A: Every number is formatted with the invariant culture (never the OS locale),
the wall-clock column truncates fractional minutes to whole seconds (a floor,
never a round), and the level-filtering lives only in the renderer — the engine
emits the same event stream at every level, so rerunning at a different
`--level` shows the same story with more or fewer columns. The test even
normalises CRLF so the fixture matches identically on Windows and Linux.

**Q: What does `--level debug` add, and how is the draw order defensible?**
A: One row per actual random draw: the seed, then `draw#k U=0.6681 → service
time 0.101 min at Reception s0 (end at t=0.101)`. Each draw's row closes the
loop: the reader can recompute the next event time by hand from U. The order is
whatever the engine's event handling demands — e.g. a service-time draw happens
when the server starts, an inter-arrival draw at each admission — and it never
skips a draw: `RngRows_TrackTheReferenceRandomSequence` asserts row k carries
exactly the k-th deviate of `new Random(42)`, so the trace cannot silently drop
a draw. Server selection at a single-idle server consumes no draw (D-050
behaviour), so no RNG row appears for a decision that used no randomness.

**Q: Why does the trace stop after `--patients` exits?**
A: A 5-patient trace is long enough to demonstrate every event and every draw
but short enough to hand-verify in the viva. Internally this is the engine's
optional `maxCompletedPatients` early break (D-058), which fires after a
patient's EXIT event and leaves the FEL consistent — the run is simply a
prefix of the full run, so the metrics of the stopped run still satisfy the
trace/stats agreement tests.

## Milestone 5 — UI/UX, presets, in-program guide

## M5-1. The Usability Contract

> "Every UI surface follows the same usability contract: searchable
> dropdowns with a '×' clear, disabled-field explanations via
> tooltips, hover tooltips on every control, themed dialogs and
> toasts, scrollable and collapsible configuration sections, clear-
> all with undo, user-selectable results widgets, full keyboard
> navigation with Tab and Shift+Tab, labels plus format-demonstrating
> placeholders on every input, and red error highlighting paired
> with icons and text so it never relies on colour alone. One theme
> file drives all styling."

## M5-2. Why Colour Alone Is Not Enough

> "Approximately 8% of men and 0.5% of women have red-green colour
> blindness. A red-only error indicator is invisible to them. We
> pair every red border with an error icon, an inline text message,
> and an updated screen-reader accessible name — three independent
> channels that any user can perceive. This satisfies WCAG 1.4.1
> (Use of Colour), and it means our error states work for everyone."

## M5-3. Why Keyboard Navigation Is a Contract, Not a Nice-to-Have

> "If a workflow cannot be completed with keyboard alone, it fails
> accessibility. We test by unplugging the mouse before every
> commit that touches the UI. The keyboard-only test is also the
> fastest way to catch tab-order bugs — the mouse hides them because
> you naturally click what you want. Removing the mouse forces the
> UI to declare its focus order, and that declaration is the
> contract."

## M5-4. Why Labels Are Not Placeholders

> "The single most common accessibility failure in desktop apps is
> using placeholder text as the only label. When the user types, the
> placeholder disappears and the field's purpose is lost. Worse,
> screen readers often skip placeholders entirely, so a user who
> can't see the screen has no idea what the field is for. We use
> both: a persistent visible label for identity, and placeholder
> text that demonstrates the format. The label never disappears."

## M5-5. Validate-on-Blur vs Validate-on-Keystroke

> "As a user types '0.5', they pass through '0', '.', '0.' on the
> way. Keystroke validation flashes red at '0' — a false error for
> something the user was about to fix. We validate on blur instead:
> the field validates when focus leaves, not on every keystroke.
> Dropdowns and file pickers don't have this problem, so they
> validate on selection. The trade-off is slightly delayed feedback
> in exchange for a calm interface."

## M5-6. The In-Program Guide Uses Embedded Markdown

> "The in-program guide renders the same markdown as USER_MANUAL.md
> — one source of truth, embedded as an app resource. A CI test
> compares the embedded copy against the repository file and fails
> on drift. This means documentation updates propagate to the guide
> automatically, and contributors write markdown rather than XAML.
> F1 opens it; Escape closes it; focus returns to wherever the user
> was. Every config field has a '?' icon that deep-links to its
> section."

## M5-7. Presets Are JSON Under ApplicationData

> "Presets are JSON files stored under the OS-standard application
> data directory — `%APPDATA%\OpdSimulator\presets\` on Windows,
> `~/.config/OpdSimulator/presets/` on Linux. We use .NET's
> `SpecialFolder.ApplicationData`, which resolves correctly on
> both platforms. Each preset carries a schema version so future
> changes remain backward-compatible, and a preset exported from
> one machine loads cleanly on another. If the referenced data
> file is missing on the target machine, the preset loads but
> shows a clear inline message asking the user to reselect the
> data."

## M5-8. Why Presets Are Not Stored Next to the Executable

> "On Windows, `Program Files` is read-only for non-admin users.
> On Linux, the install directory may be on a read-only mount.
> Any app that writes user config next to itself fails on half
> the machines it's installed on. ApplicationData is the standard
> cross-platform location and it survives app updates. We also
> avoid the current working directory because it changes with
> how the app is launched — a shortcut, a shell, or a debugger
> all see different CWDs."

## M5-9. Why Startup Is Empty by Design

> "On launch, every field is empty and no preset is auto-loaded.
> This is deliberate. An empty start is predictable — the user
> sees the same screen every time. Auto-restore hides state that
> the user may have forgotten, like a stale data file path from
> weeks ago. The empty start also forces a deliberate choice
> before running a calculation: the user must either fill the
> fields or explicitly select a preset. That deliberate step is
> worth the small extra friction."

## M5-10. Preset Loading Is Explicit

> "The Presets dropdown shows '(none)' until the user selects a
> preset via the dropdown, the Manage dialog, or Ctrl+O. Once
> loaded, the fields populate and the dropdown reflects the
> loaded name. If the preset references a data file that no
> longer exists on disk, every other field still populates — only
> the data field is emptied and shows an inline message asking
> the user to reselect the file. We never silently fail, and we
> never overwrite the user's other choices just because one file
> is missing."

## M5-11. What Persists Across Sessions

> "Three things persist across sessions: the list of saved preset
> files (they live on disk), the user's UI widget preferences from
> FR-UI-14, and the theme choice. Nothing else. The configuration
> itself, the last data file, the last simulation results — those
> all start clean each session. This is a deliberate boundary
> between 'user preferences' (persistent) and 'session state'
> (ephemeral)."

## M5-12. Why the Data Preview Is Read-Only

> "The preview exists to answer one question: 'did my file load
> correctly?' It is a verification surface, not an editor. It
> supports sort, scroll, copy, and row-level validation highlight,
> and that's it. Editing inside the app would require save-back
> semantics that contradict our immutable-input contract — the
> validator expects the file to be the source of truth. A smaller
> feature set is faster to build, easier to test, and easier to
> defend. If a user wants to edit, they export to Excel, fix, and
> re-upload."

## M5-13. Preview Performance — Virtualisation

> "The preview virtualises rows. With 10,000 rows, only the visible
> ~30 are materialised in the visual tree. Rendering happens in
> under a second; sorting completes in under 200 milliseconds. If
> we rendered all rows eagerly, the UI would freeze on load. The
> virtualisation is why the preview remains responsive at the scale
> of a full clinic year."

## M5-14. Invalid Rows in the Preview

> "Rows that fail validation are highlighted with the same red
> border, icon, and tooltip treatment as invalid form fields —
> FR-UI-17. The tooltip states the specific validator reason, so
> the user can fix that row in Excel and re-upload. The preview
> becomes a debugging aid: instead of 'validation failed, 3 rows
> wrong', the user sees exactly which rows and why."

## M5-15. Pre-Commit Accessibility Audit

> "Before any UI commit, we run a fixed audit: Tab through the
> entire window with the mouse unplugged, verify every control is
> reachable, confirm Escape closes every modal and returns focus
> to the opener, check that every input has a label and a
> placeholder, and submit with empty fields to confirm the error
> highlighting works end-to-end. If any line fails, the commit is
> not ready. This checklist is in AGENTS.md §16.8 so every session
> follows it."

---

## Glossary Additions

| Term | Definition |
|------|------------|
| WCAG 1.4.1 | Use of Colour — colour must not be the only means of conveying information |
| Focus trap | A state where Tab cannot leave a modal or control |
| Focus indicator | Visible highlight on the currently focused control |
| Live region | Screen-reader announcement channel for dynamic content |
| ApplicationData | OS-standard directory for user-scoped app data |
| Embedded resource | File compiled into the app binary, readable at runtime |
| Deep link | URL fragment that navigates directly to a specific section |
| Schema version | Field in persisted data declaring its format version |
| Virtualisation | Rendering only visible items in a long list |
