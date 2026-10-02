# VIVA_ANSWERS.md — Oral-Exam Speaking Points

Purpose: short, honest framings the author can say aloud in the viva without
hesitating. Entries are added as features land, newest first. This file was
created during the GUI rebuild (Phase 5); the earlier M5-era answer bank did
not survive the view-layer replacement (see `docs/M5_FAILURES.md`).

---

## Phase 8B — simulation-verification widget (2026-09-18)

### Q: You already have a chi-square test. Why is there a second one?

**Answer:** We run chi-square in two places. Input-side chi-square validates
that our MLE fit is a good model of the historical data — this is input
modelling. Output-side chi-square validates that the simulation engine is
generating values that match the distribution the user configured — this is the
standard model-verification step from Banks and Law & Kelton. The first checks
the assumption; the second checks the implementation. If the engine silently
drew from the wrong distribution, the input-side test would still pass (the
data is fine) while the output-side test would reject — so they cannot be
replaced by one another. The output-side test consumes the raw inter-arrival
and per-stage service samples the engine retains in `SimulationResult`
(Phase 8A, D-135) and lives as a Results widget because it describes a run
(D-136).

### Q: What does the verification widget do when the chosen family is not a real distribution?

**Answer:** It fails loud instead of faking a verdict. **Deterministic** output
is skipped with a note (a constant stream has no distribution to fit);
**General** is flattened to Exponential and the card says so (the engine still
samples every stage exponentially, D-127); a series with fewer than two samples
shows *"Insufficient samples for chi-square."* rather than a fabricated
p-value. That is the same honesty rule as the rest of the app — never silently
skip a check. It reuses the same `FitsService` and the same histogram binning
as the Input tab, so the two chi-squares are computed the same way and can be
compared directly (`Phase8BVerificationTests`).

---

## Phase 7D — merged Input tab (2026-09-18)

### Q: Why merge the Data upload section into the Input Analysis tab instead of keeping them separate?

**Answer:** They answer the same question — "what data did I load, and is it
usable?" — so splitting them forced the user across two surfaces to do one job
(upload, check the preview, read the fit). Phase 7D makes one **Input** tab
(tab 2 of four: Simulation | Input | Token Generator | Help) that holds upload,
preview, validation banner, stage-mismatch warning, and the fit charts
together. The semantic split from AGENTS §16.12 survives: data-derived figures
live on Input; run-derived figures live on the Results panel. The Simulation
tab's Data section collapses to a status strip ("Using file: …" /
"Entering parameters manually") with a **Manage input →** link, so the config
panel still tells you the data source at a glance (D-130..D-134).

### Q: Two buttons can both open a file picker — how do you know they don't load twice?

**Answer:** They don't own a picker each. `ConfigPanelViewModel` and the Input
tab both *raise an intent* — `UploadRequested` / `UploadFileRequested` — and
`MainViewModel.PickAndLoadDataFileAsync` is the single place that shows the
OS picker, loads, validates, and mirrors the result into both the config panel
and the Input tab (RULING 3, D-132). One path means one validation, one log
line, one preview. The test `InputTab_UploadRequested_UsesTheSinglePickerPath`
pins that both intents converge on the same handler.

---

## Phase 6C — chart suite (2026-09-17)

### Q: How do the charts prove the fitted distribution actually fits the data?

**Answer:** The Input tab histogram draws the observed frequencies in
exactly the same bins the chi-square test uses, and overlays the fitted PDF
(density × bin width × n, so curve and bars are on one scale). You see the
fit *before* the p-value. The chi-square widget then draws observed vs
expected per bin, so you can point at the specific bins that drive χ². The
picture and the verdict are guaranteed consistent because both are produced
from the same `ChiSquareResult` — the histogram never re-bins the data
(`Phase6c2HistogramTests.BuildHistogram_ReusesTheChiSquareBins_NeverRecomputes`).
"Fail to reject" is not "the distribution is correct"; we always report the
p-value, and the chart is the intuition behind it.

### Q: Why show one bar per server instead of a single stage-utilisation number?

**Answer:** Stage utilisation is the *mean* of its servers, and a mean hides
imbalance — one doctor at 90% plus one at 30% averages to a comfortable-looking
60%. The per-server chart exposes that: each bar is a real engine server, and
a bar turns amber when it deviates from its stage mean by more than 0.15, with
the exact gap in its tooltip (D-121). FR-STAT-7's analytical rule stays
max − min > 0.15; the widget is the per-server visual variant because max−min
only tells you the two extremes and never *which other* server is off
(CONTEXT §5.7 documents both). This is a run-derived figure, so it lives on
the Results tab, never the Input tab.

### Q: The queue-length chart plots a run with hundreds of thousands of samples. How is it not unusable?

**Answer:** Two things. First, the series is prepared off the UI thread, so
prep over 10,000 samples stays well under the 100 ms budget and the window
never freezes (`Phase6c6Nfr6Tests`, NFR-6). Second, the plotted series is
min-max bucket-decimated to at most 2000 points per stage (D-122): each
bucket keeps its first, last, minimum and maximum, so the tallest peaks and
the visible trend survive while the point count is bounded — O(n) in one pass.
The caption says "downsampled from N samples" when reduction happened, so the
number is never hidden. Full-fidelity data is still in the metrics; the chart
is a presentation layer.

---

## Phase 5d — config refinements (2026-09-16)

### Q: Why is the μ field gone from the Stages section?

**Answer:** Because there were two places to enter μ and they could disagree
(D-106). Now the Stages rows are *topology* (name + servers); each row shows a
read-only label stating where its rate comes from — "(from data)" for the
fitted rate, "(manual)" from the single Parameters comma list, "— (no source)"
when neither is available. One entry point, one source of truth, and a refused
run names the offending stage in its banner. (D-112)

### Q: How do you change the chi-square test's strictness?

**Answer:** The Model section has a significance level α, default 0.05, forced
strictly between 0 and 1 (α=0 or α=1 would make every fit pass or fall
vacuously). The value threads all the way into `ChiSquareTest.Run`, so every
verdict and the results-panel caption ("Chi-square goodness-of-fit (α = …)")
use the chosen level. (D-113)

### Q: Why does loading data sometimes show an amber warning I did not ask for?

**Answer:** The loader counts the stages covered by the file — e.g.
`sample_patients.csv` covers only **Screening**. If your configured list has
more (or fewer) stages, stages without data have no fitted μ and would be
refused at run time. The amber warning says exactly that, with counts and
names, and offers **Sync stages from data** (themed-confirm, then adopts the
file's topology) or **Keep current stages** (dismiss for the session — never a
silent auto-resize). (D-114)

## Phase 5 — Run flow, results, welcome card, refused runs (2026-09-16)

### Q: Why are there three run modes (Clinic day / Multi-day / Diagnostic trace)?

**Answer:** A clinic day is the real thing — arrivals from 08:15, services
continue past 11:00 until they finish. Multi-day chains several of those over
consecutive operating days (Mon–Thu, Sat). A Diagnostic trace run is a shorter
minutes-horizon run whose only purpose is to make the event trace usable: the
clinic-day engine emits no trace, and a real day spans thousands of events you
cannot hand-walk. So the trace is surfaced as a *diagnostic* mode, not a
co-equal clinic mode. (D-105)

### Q: Why is there no event trace for clinic-day runs?

**Answer:** There is now — the calendar `Engine.Run` overload gained an
optional `ITraceSink` (D-110) so the Coordinator's sink is forwarded in every
run mode. Clinic-day and multi-day runs populate the **Event trace** widget;
the Diagnostic trace mode still exists because its bounded minutes-horizon is
the only one you can hand-walk line by line (a real day spans thousands of
events). (D-105, D-110)

### Q: Why does the GUI refuse to run when nothing about the run is set?

**Answer:** The coordinator builds the network inside a try and returns a
structured outcome (`RunOutcome`), never a raw exception. Four specific
refusals are user-facing banners: no arrival rate available (no manual λ, no
loadable data), fitted `p_exit == 1.0` (every patient exits at Screening, so
the Doctor stage never sees anyone), a stage with **no service rate** (the
banner names the stage: "Stage 'Doctor' has no service rate…", D-112), and any
stage with ρ ≥ 1 (unstable — would grow without bound). The ρ ≥ 1 message is
the Core `UnstableSystemException` message passed through verbatim, so the GUI
can never disagree with the engine. (D-104, D-112)

### Q: Which λ/μ does the simulation actually use?

**Answer:** A manual λ entered in Parameters wins over the fitted value, which
is still computed and shown. Since 5d.1 a stage's μ comes from **one** of two
places: the **Parameters** comma list (positionally per stage; blank entries
use the fitted value) or the fitted rate from the loaded data — and the
Stages section is *topology only* (name + servers) with a read-only label
telling you which source each stage gets ("(from data)", "(manual)",
"— (no source)"). A run whose stage has neither is refused with a banner
naming that stage. Blank means "use the fitted value", so a factory-default
config is start-enabled. Both manual and fitted results are reported side by
side. (D-100 superseded, D-112)

### Q: How does the welcome card disappear?

**Answer:** The results panel shows the welcome card until the first run
attempt; the first attempt — even a refused one — swaps it out for either the
results widgets or the refusal banner. The card data (member names, course,
professor, logos) lives in a single `CourseInfo.cs` constants file, so the
viva slides can update it in one place. (FR-UI-5)

### Q: How do you prove the simulator is correct in the viva?

**Answer:** Three ways. (1) The diagnostic trace with `RNG` level records every
random draw (`draw#1 U=0.6681 → service time 0.101 min`) under a fixed seed, so
a handful of patients can be replayed by hand. (2) Analytical validation: with
exponential arrivals/service the engine's queue-length, wait, and utilisation
outputs are compared against the M/M/c formulas and the % error reported (the
Results-panel *Analytical validation* widget, §6.9 of the user manual). We guard
the comparison to steady-state runs — the analytical formulas do not apply to a
165-minute clinic day. To use the widget, run a DiagnosticTrace over 100,000 or
more simulated minutes. (3) `dotnet test` — 414 tests including the run-flow
tests that assert refusal banners, trace detail by level, and multi-day day
counts.

## Phase 8K — Per-stage service families and G/G/c auto-fit (2026-09-26)

### Q: Each stage has its own distribution now — how do you pick one, and how do you know it is right?

Two independent routes, and the simulator never mixes them.

**Route 1, by hand.** Turn on **Advanced** on a stage row and pick its
**Service distribution** from the six families. Each family then shows exactly
one extra field, and it is always a *spread* field, never a location one:
σ for Normal and Lognormal, shape k for Gamma, half-width w for Uniform, and
nothing for Exponential or Deterministic. The mean service time is still the μ
you typed. The architecture rule is that **μ is the only location parameter**
(D-150) — one family cannot disagree with itself about where it is centred,
which was the actual bug in the earlier draft where a row could name both a μ
and a raw parameter.

**Route 2, from the data.** Load a file, set a stage to `G/G/c`, and the
simulator runs an AIC search across all six families against that stage's
historical service times. It then keeps your `G/G/c` on screen, shows a badge
naming the winner, fills μ **only if you left it blank**, and copies across
the spread the winning family needs. The family is the fitter's decision; the
μ stays yours.

**How do you know it is right?** Two independent checks, and they are
deliberately not the same check:

- *Input side* — is the fitted family a reasonable model of the historical
  data? Inter-arrival and service histograms, the fitted PDF, and a
  chi-square goodness-of-fit verdict. This is the Input tab.
- *Output side* — did the engine actually produce the distribution that was
  configured? Each stage's generated service times are binned and
  chi-square tested **against that stage's configured spec** (D-157).

The second one is the interesting one, because it is where the project was
wrong for a while. The verification used to *refit* the engine's own output
and then test that refit against the same samples. That answers a question
nobody asked — it reports how well the service's own estimate fitted itself,
which passes for any output that is *some* plausible distribution. Worse, it
was handed a single family repeated across the stages, so it could not even
see a per-stage difference. Now each stage is judged against its own spec,
and a misconfigured or unconfigured stage is *named* instead of quietly
defaulted to exponential.

### Q: Why does one stage's chi-square say "not applicable" while the others show a curve?

Because that stage's service family has no spread to bin. **Deterministic**
service means every service takes exactly the same time, so there is no
variation to histogram and no distribution to test it against. Exponential,
Normal, Lognormal, Gamma and Uniform all have a genuine spread, so those
stages get a real curve and a real verdict.

Two further cases produce a stated reason instead of a curve, and both are
information rather than failure: a **unconfigured** stage (no spec was built
for it, which the master principle says must never be silently defaulted), and
a stage whose configured spread is so narrow that the chi-square binning runs
out of expected counts — which is genuine evidence that the configuration and
the output disagree.

### Q: What is the μ-only-location rule, and why is it stated as an invariant rather than a convention?

Because it is checkable. The invariant is

    ServiceRate  ==  1 / ServiceDistribution.Mean  ==  μ

with all three coming from one resolved value, so the assertion cannot be
satisfied by coincidence. It is enforced by a `StageSpec` assertion that is
compiled under `#if DEBUG` (D-147) — which is why the release gate is not
sufficient on its own: a Release-only test run literally cannot observe the
check. Every Phase 8K gate has therefore been run in Debug as well as Release.

The rule exists because of a concrete failure it prevents. Gamma is specified
by shape and scale, and its mean is `k · Scale`. If μ and the raw parameters
were both entered independently they could describe two different
distributions at once, and nothing downstream would catch it. Deriving
`Scale = mean / k` and `Min/Max = mean ± w` from the one mean removes the
possibility rather than testing for it afterwards.

### Q: A stage's family dropdown is greyed out and the badge says something. Why?

That is `G/G/c` working as intended. `G` names no family on purpose — the
whole point is that the family is unknown until it is fitted — so the family
dropdown stays disabled while the notation is `G/G/c`, and the badge names
whatever the fitter chose. If you have **not** loaded a data file, or the file
does not cover that stage, there is nothing to fit from, so the simulator
refuses: it reverts the notation, changes nothing else, and puts the reason on
the row. A refusal with a stated cause is the designed outcome; silently
keeping a family it could not determine would not be.


## Phase 8M — Chart legibility, stage colour, and derivable numbers (2026-09-29)

### Q: Why is a doctor's bar 20% when the metrics table says the Doctor stage is 60%?

Because the bar is a **contribution**, not a utilisation. A bar is one
server's busy time divided by *(servers in the stage × operating time)*. With
three doctors each 60% busy, each carries 60% ÷ 3 = 20% of the stage's
capacity, and 20 + 20 + 20 = 60 — the stage utilisation printed beside it.

The earlier version drew each server's raw 60%, so a 60% stage showed three
60% bars next to a metric card reading 60%. Nothing on screen said the chart
was on a different scale from the number it appeared to illustrate, and a
reader had to already know the rule to reconcile them. The contribution scale
removes the ambiguity by making the identity **checkable**: the bars of a
stage sum to the stage utilisation, so the chart and the table validate each
other. The tooltip still gives the server's own utilisation, so nothing is
hidden by the change — only moved to where it belongs.

### Q: Why is the utilisation Y axis fixed rather than scaled to fit the data?

So that two runs can be compared, and so nothing is ever cut off. The top of
the axis is `1 ÷ (fewest servers in the run)`, which is the largest a
contribution can ever be: one server's whole capacity. That bound comes from
the **configuration** (server counts), never from the values in the current
run.

This replaced a ceiling derived from the tallest equal-share line, and that was
a real bug rather than a matter of taste. Equal share depends on how balanced
the run happened to be, so a run with one quiet single-server stage and one
heavily loaded four-server stage produced a bound *below* the loaded stage's
outlier bar — the bar was drawn off the top of the plot. Because the ceiling
now cannot depend on the data, it cannot clip, and there is a regression test
(`UtilisationChart_YAxisNeverClipsADeviatingServer`) that constructs exactly
that scenario.

The cost is honest and worth stating: a clinic with many servers everywhere
gets a flatter-looking chart, because the correct ceiling for a six-server
stage is one sixth. A scale that moved to flatter the data would be a scale
that hides the very imbalance the chart exists to show.

### Q: The X axis was dropping labels. Why did every test pass while it was broken?

Because the data was never wrong — the bug was in how the chart *rendered* it,
and the existing tests asserted on the data. Two independent causes had to be
fixed together:

1. LiveCharts2 ships a **non-null default `Labeler`** on the axis. That
   default formats the numeric axis value and takes precedence over an
   attached `Labels` collection, so the strings were present, attached,
   correct — and never drawn. Assigning `Axis.Labels` alone is a no-op.
2. The chart drew **one series per server**, so each series had to pad the
   whole category list with nulls. That null-heavy category was the second
   half of the loss.

The lasting change is in the tests rather than the code. `Phase8MChartControlTests`
asserts against the **built** `CartesianChart`: the `Labels` array equals the
expected strings, `axis.Labeler(i)` returns `axis.Labels[i]` for every
category, the rotation is non-zero, and the series composition is
3 columns + 3 overlays + 3 reference lines for a three-stage run. A data-level
test could never have caught either half.

The same lesson retired a test that had been passing for the wrong reason:
`QueueChart_UsesNativeStepLineSeries` used to locate `BuildQueueChart` by
reflection and assert nothing about the series it produced. It now checks the
concrete `StepLineSeries` type on the chart that is actually built.

### Q: Why does the queue chart draw steps instead of a smooth line?

Because the engine only changes a queue length **at an event** — a patient
arrives at the stage or leaves it. Between two events the queue length is a
constant, so a straight or smoothed segment between samples draws a queue
length the clinic never had: it invents a gradual drain that the simulation
did not record. In a project whose purpose is to *not* invent values, that is
the wrong default, so the chart uses a step line and the shape of the curve is
literally the shape of the truth.

The other readability rule on that chart is draw order. Stage lines are opaque
to each other, so whichever is drawn last covers the others. The stages are
ordered by average queue length, **busiest first**, which puts the busiest line
at the back and the quietest on top — so where they overlap, the smaller curve
stays visible, which is where the eye is already looking. Ordering by average
is not perfect: a stage with a low average but one tall spike can be hidden at
exactly the moment it matters. That is a real limitation, stated rather than
hidden.

### Q: Why is the same stage the same colour in every chart, and what happens with more than four stages?

Because colour was previously being asked for by **series position**, so the
same stage came out a different colour in the queue chart than in the
utilisation chart — and the utilisation chart reorders its own series, so its
colours moved between runs. Colour was carrying draw order rather than stage
identity, which makes the panels impossible to read against each other. The
new dynamic stage legend is the evidence: a legend only has to exist when the
mapping was not already shared.

`StageColourPalette.ForStageIndex(stageIndex)` is now the single answer, called
by the queue chart, the waiting-time histogram, the utilisation chart and the
legend. The four base colours are theme tokens (`ColorChartSeries1..4`), so
restyling the charts is a one-file change. For a run with more than four
stages the palette wraps by rotating the hue 15° per extra group rather than
repeating a colour, because a repeated colour reads as the same series. Amber
is deliberately **not** part of this palette: amber is a *meaning* (this server
deviates from its stage mean by more than 15 points), not a stage, and mixing
the two would make a deviation look like an identity.

### Q: The "View calculations" dialog is plain text. Why not a table, or a graph?

Because the purpose is **derivability**: someone should be able to follow every
number on the results panel back to the rule that produced it. A table is
better for comparing values; this is for following a chain — λ to 1/λ, μ to
c·μ to 1/μ, utilisation to busy minutes, the stage totals to the flow
balance. Plain text wins because it copies cleanly into a report or an answer
sheet, and because the **Copy** button makes the whole derivation portable.

The structural choice matters more than the formatting: the text is produced by
a pure function (`CalculationsTextBuilder.Build`) taking the result and its
parameters, so every rule it prints — `ρ = λ/(c·μ)`, `utilisation = busy ÷ T`,
`contribution × T` — is assertable in a unit test against a real engine result,
headlessly. Had the text been assembled in the view model, the panel would have
been a derivation nobody could test, which is the same mistake the rest of this
project keeps correcting.

One honesty detail worth defending: busy time is labelled `(derived)`. The
engine records per-server *utilisation*; minutes are that utilisation times the
operating time. Marking the difference means a reader can never mistake a
reconstructed figure for a raw engine output. The dialog also names the
parameter source ("fitted from …" or "entered manually"), so it is always
clear which of the two configuration paths produced the numbers.

### Q: Why does a server's contribution differ from its utilisation? Which one is the "real" number?

Both are real; they answer different questions, and the project prints both
wherever either appears, because a bar's height is the one that is easy to
misread.

- **server utilisation** = `busy time ÷ operating time` — how much of the
  available time *that server* spent with a patient.
- **contribution** = `busy time ÷ (c × operating time)` — that server's
  **share of its stage's capacity**.

The reason the contribution is what the chart draws is arithmetic, not taste:
with every server of a stage drawn as a share, the bars of a stage add up
exactly to the stage utilisation. A three-doctor stage at 60% utilisation
becomes three bars of 20% each, and 20 + 20 + 20 = 60 — the same figure in the
metrics table. Drawn as utilisations instead, the bars would sum to 180% and
the chart would need a different axis ceiling for every different stage size.

The consequence is that a bar's height **is not** a server's utilisation, which
is the trap. A 37.75% bar can be a server that is 75.5% busy on a two-server
stage, or a server that is 37.75% busy on a one-server stage. So the
contribution is always shown with the server's own utilisation beside it — in
the bar tooltip, the per-server list, the calculations dialog and the CLI — and
the calculations block prints the division it used, so the reader can check it
rather than trust it.

Two details to have ready if asked:

1. **Do the per-server utilisations sum?** No. Three doctors at 60% each sum
   to 180%, which means nothing. The **contributions** sum, and their sum is
   the stage utilisation. On a **one-server** stage the two quantities are
   equal by definition, which is the case most likely to expose a formula that
   divides by the wrong thing — so the test fixture deliberately includes a
   one-server stage.
2. **Is the contribution a new engine metric?** No. It is computed at display
   time from two figures the engine already produces (`PerServerUtilisation`
   and `ServerCount`). Nothing was added to `StageMetrics` or the engine's
   result, because a metric stored for a display to show is in the wrong place:
   the engine would then be carrying a figure that has no meaning outside this
   chart, and a change to the definition would silently alter the engine's
   output rather than its presentation.

## Phase 8O — Observation window, two arrival rates, Horizon (2026-09-29)

### Q: Why does your app report two different arrival rates for one file?

Because they answer two different questions, and the MLE is not a "corrected"
version of the window rate.

**MLE λ** is `1 ÷ mean of the within-session inter-arrival gaps`. It measures
the rate *while patients were arriving*.

**Window λ** is `total arrivals ÷ operating minutes in the window`. It includes
the idle tail of each session — the last few minutes of every clinic morning
when nobody walks in.

On my six-session sample file they differ by about 55%, which is exactly why I
show both rather than picking one silently. MLE is the default because it is the
estimator the course teaches and it is what every earlier result in this
project was produced with.

**Why exclude cross-session gaps from the MLE?** An inter-arrival time is a
property of an arrival process that is running. Between Saturday 10:58 and
Monday 08:12 the clinic is shut — that is 2,700 minutes of no process, not one
inter-arrival time. Including it would both drag the mean down (so MLE λ would
fall) and put a huge value into a histogram whose exponential PDF has no
support there, so the chi-square verdict would be about a sample the engine
never drew from.

### Q: Why is the clinic day 165 minutes and not 180? And why is "1 month" twenty days?

165 is `11:00 − 08:15` in minutes. The clinic opens at 08:15, not 09:00 — I
corrected that on 2026-09-29 (D-172); both `AGENTS.md` and `CONTEXT.md` had it
wrong, and the 45-minute difference was silently changing every arrival count
per session.

The presets count **operating sessions**, not calendar days, because the clinic
is open Monday–Thursday and Saturday. A file running Monday to the following
Monday spans seven calendar days but only six operating sessions, and the
missing 165 minutes are minutes in which no patient could have arrived.
Dividing arrivals by them would produce a rate lower than the data supports.

"One month" is four operating weeks = 20 sessions. That is an **approximation
I declared rather than derived**, because there is no calendar month of this
clinic to count: the pattern is five-day weeks, so a month has no fixed length.
I say so in the UI and the manual rather than letting the label imply a
precision it does not have.

### Q: Your calculations dialog shows the run's λ, but the file covers a different window. Which is right?

Both, and the dialog says which is which — that is D-176, and it was a real
defect rather than a formatting preference.

If you pick "1 week" on a six-session file you have changed the *divisor*. The
run used `60 ÷ 825`; the file's own window would give `60 ÷ 990`. My first
implementation printed the file's figure on the `λ — window` line, so the
dialog reported `0.06061` for a run that sampled at `0.07273`. The receipt is
the more dangerous half of that pair, because it is what a reader trusts.

Printing the right number alone was not enough, and that is the part I nearly
missed: the window line above it still described the *file's* window, so
dividing the printed arrivals by the printed minutes would not give the printed
λ. So the dialog now prints the division itself — `λ = 60 arrivals ÷ 825
operating minutes` — and the two windows are named apart. You can check my
arithmetic on screen, which is the point of a receipt.

I proved the test is real by reverting the one line that fixes it: the test
fails, showing the old wrong figure.

### Q: Why did the Time span dropdown disappear?

Because one control was answering two unrelated questions, and both answers
were wrong in different ways.

It set a calendar run's length *and* a diagnostic trace's arrival window from
one list. "1 week" meant 5 operating days for the run length but 6 **calendar**
days for the generator count, so a five-day clinic ran for six. Nothing
asserted this, because the arithmetic in each place was individually correct —
the two readings simply did not mean the same thing.

The default was worse: `10000` minutes, about four operating sessions, which
made the dropdown's first option a value no user would pick and produced a
trace too long to read.

So the length now has one owner per mode. A calendar run's length is the **Days**
field and nothing else. A diagnostic trace's window is the **Duration**
dropdown — 1 hour, 15 minutes, or Custom minutes — and it is *visible in
diagnostic mode only*.

One consequence I had to fix: a calendar run must not read that field at all. A
user who mistyped the custom value, switched to Multi-day, and pressed Run was
getting a silent refusal about a control scrolled out of sight. A run you are
entitled to perform should never fail because of a control it does not show. I
pinned both directions with a pair of tests, because the calendar-mode test
would pass just as well if the field were ignored everywhere.

## Phase 8Q.2 — Direct-to-doctor routing, and the p_exit denominator (2026-10-03)

**Q. What did the project do with a patient who reaches a doctor without being
screened?**

Before this phase it had no honest answer. The validator reported the row as
dirty — 8 errors for 4 rows in the committed sample — so a quarter of the real
clinic capture could not be loaded at all. Screening such a patient anyway
would have invented a service the clinic never performed; rejecting the file
would have thrown away real observations. The engine now carries
`p_bypass` as a second coin toss on the *same* service completion that already
decides the screening exit: after Reception finishes, draw for the exit as
before, and if the patient did not exit, draw again — below the threshold means
go straight to the Doctor, above it means queue for Screening.

**Q. Why is it a second coin toss and not a fourth stage or a "skip" flag?**

Because it is the *same kind of decision* as the exit. A reader who understands
`p_exit` now understands `p_bypass` without learning anything new, and the
engine keeps exactly one place where routing happens. A flag would need its own
branching logic in three places, and a stage would be a lie — there is no
queue, no server and no service distribution at "Screening was skipped".

**Q. Why did you reject a bypass source stage equal to the exit stage?**

Because then two coin tosses happen on one completion and the destination
depends on which `if` was written first. That is not a style objection: it means
the model's answer is decided by source-code ordering, and reordering the two
lines silently changes every result. The constructor refuses the combination
instead, so the ambiguity cannot exist.

**Q. Why is the bypass draw conditional? What would happen if you always drew?**

The draw is guarded by `BypassEnabled`, which is false whenever
`p_bypass == 0`. If it ran unconditionally, every existing run would consume
one extra random number per service completion. That would change every
FNV-1a64 hash in `GeneratedServiceSamples_AreStableAcrossRuns`, break the
golden trace fixture, and quietly restate the project's reproducibility claims.
The hashes are byte-identical after this change, and that is the evidence the
feature is purely additive. This was the single most important detail in the
implementation.

**Q. You changed the formula for per-stage arrival rates. Why?**

The old one was a product: λᵢ = λ₀ × Π(1 − pⱼ) over the exit stages before i. A
product can only ever *remove* arrivals, because it assumes every patient
passes through every stage in order. A bypassed patient does not — they appear
at the Doctor having skipped Screening — so their arrival has to be *added*, and
a single-exit product has no way to add anything. The replacement propagates a
probability mass forward and multiplies by λ₀ at the end:

    λ_reception = λ₀
    λ_screening = λ₀(1 − p_bypass)
    λ_doctor    = λ₀·p_bypass + λ₀(1 − p_bypass)(1 − p_exit)

Each stage's λ is the **sum of every route that reaches it**. With `p_bypass = 0`
it collapses to the original product, which is how I know the change is a
generalisation rather than a different model — and every existing test passes
unmodified.

**Q. What was wrong with the p_exit denominator, and why does it matter?**

The denominator was "rows whose `departure_stage` is Screening or Doctor",
i.e. everyone with a departure stage. Once direct-to-doctor traffic exists,
that population includes patients who **never reached the screening decision at
all**, so `p_exit` was diluted by exactly the traffic this phase exists to
model: the more direct-to-doctor traffic the clinic had, the lower the reported
`p_exit`. On the real capture the old denominator gives 0.807 and the correct
one gives 0.956 — a difference of 0.15 in the parameter that decides whether the
Doctor stage is stable. The denominator is now the screened population, and
`TotalCandidates` was renamed `ScreenedPatients`, because a correct number
under a name that describes the wrong population gets read wrongly later.

**Q. How is the bypass detected in a data file, and what if the column is
missing?**

A row that reaches a Doctor with a blank `screening_start`. Detection is gated
on the `screening_start` **column** existing: a blank cell in a file that has
the column means "not screened", but a missing column means a pre-bypass schema
that cannot answer the question — and inferring from a missing column would
have re-read every legacy file as all-bypass. Those files keep the legacy
denominator.

**Q. Which blank cells does the validator forgive, and why those and no others?**

Three, and the third is the interesting one. Reception is **never** forgivable:
a row with no reception times describes someone who never entered the clinic. A
blank `doctor_end` on a Doctor departure **is** forgivable, because the capture's
end stamps are sometimes missing and nothing is lost — the patient has already
left. A blank `screening_*` on a Doctor departure is forgivable **only when a
doctor record is present**, and that condition is the whole fix: it is the
evidence that distinguishes "skipped screening" from "forgot to record it". A
row with neither a screening nor a doctor visit is still rejected, so an
incomplete capture cannot pass as real routing. The asymmetry with Screening is
deliberate: a blank `screening_end` on a *Screening* departure is still an error,
because that timestamp is the service sample μ is fitted from — forgiving it
would discard data rather than record a routing outcome.

**Q. Why did you move the `EndService` row in the trace?**

Because it was contradicting itself. The row was emitted *before* the exit draw,
so it always printed the default next stage — a patient leaving at Screening was
recorded as `→ Doctor` and then immediately recorded as an exit. A reader
replaying the trace could not tell which line was true. Moving the emission
after both decisions means the printed destination is the one that happened, and
the queue figures stay correct because routing only ever touches the queue being
entered, never the one being left. `q` did not move.

**Q. What happens if the user sets a bypass on a two-stage network?**

The run proceeds with the bypass normalised to zero and the receipt reports
**zero**, not what they typed. Refusing the run would be disproportionate for a
harmless slip, and honouring it would mean showing a number the engine did not
use — which is the defect D-176 exists to prevent. The receipt and the run
cannot disagree.

**Q. Anything in this phase you would do differently?**

Yes. I wrote two conservation assertions for the routing tests — "every patient
who leaves Reception is either screened or reaches the Doctor" — and both were
wrong, because `PatientsServed` counts service *completions at each stage*, so
a patient who is screened and then continues is counted at both, and the counts
are not additive. The assertions now compare *flow rates* over a horizon long
enough to be stable rather than exact counts over a short one. The failure was
in my test, not the engine, but it is worth recording because a test written
against a wrong belief passes happily and tells you nothing.

Second: `departure_stage = Reception` is rejected on its own value, because
D-008 treats reneging as out of scope. At the time of 8Q.2 I recorded that the
owner's validity table had described that row's *blank cells* as acceptable, and
that I had kept the rejection because `IsValidDepartureStage` was not part of
the ruling. **That reading was wrong, and the owner corrected it on
2026-10-03** by giving the full validity table, which lists every Reception row
as invalid. The distinction I had drawn — that the table governs which *blank
cells* are forgivable and not which *departure values* are legal — does not hold:
a patient with no reception times has no recorded entry into the clinic at all,
so there is nothing for the blank cells to be "missing" from. The table is now
pinned in full by `TheOwnersValidityTable_RejectedRows_AreRefused`, and D-008 is
unchanged, which is the correct outcome: the code was already right and the
*documentation* was the defect. Modelled reneging remains a separate decision
with its own probability and its own route.

## Phase 8Q.3 — Performance Measures and the stability verdict (2026-10-03)

**Q. What is the stability verdict, and where do the numbers 0.9 and 1 come from?**

The verdict is one function of ρ — the stage's arrival rate divided by its
capacity `c × μ`. ρ < 0.9 is **Stable**, `0.9 <= ρ < 1` is **Near capacity**, and
ρ >= 1 is **Unstable**. The two numbers are standard queueing results, not
tuned constants. A stage is stable only while ρ < 1, because ρ is the fraction
of capacity that arrivals demand: below 1 the queues drain, at or above 1 they
do not and waiting time grows without bound. The 0.9 band exists because
stability is a hard cliff, and a stage at ρ = 0.97 looks healthy on a utilisation
figure while having almost no margin — a plain utilisation percentage hides
that, because 97 % busy can mean either comfortable or one bad morning from
collapsing. Naming the band says it in words.

**Q. Why is the boundary at 0.9 inclusive of amber, and why are the bands
half-open?**

Because the last stable moment is strictly below the threshold. `<= 0.9` and
`< 0.9` look identical in the source and disagree at exactly one value, which
is why 0.89, 0.90, 0.99 and 1.00 are four separately named tests rather than one
data-driven case — a boundary bug has to be findable by reading a test name, not
by cross-referencing a table row against a threshold column.

**Q. If ρ >= 1 is unstable, why can I never see an "Unstable" verdict in the app?**

Because you cannot produce one. The engine throws `UnstableSystemException` on a
network with a stage at ρ >= 1, and the Start button is gated on the same
condition, so the refusal arrives before a run rather than after one. The
verdict band is still defined and still tested because a stage *measured* at the
edge — where demand and capacity came out level in the run — must show the
truth rather than render an empty cell, and an empty cell reads as "fine". It is
a defensive state, and the manual says so rather than implying a user can reach
it.

**Q. Why is the bottleneck the highest ρ and not the highest utilisation?**

They are usually the same number, because utilisation is ρ scaled by how much of
the horizon the stage was open. ρ is the one that means "this stage is close to
breaking", independent of the observation window — a stage can show 40%
utilisation and still be at ρ = 0.98 if the clinic opened for 165 minutes of the
300 the run covered. Naming the highest ρ also rules out the three wrong answers
a reader expects: the first stage, the last stage, and the lowest utilisation.
Ties resolve to the earliest stage, so the caption cannot flicker between two
names across runs with identical inputs.

**Q. Why did the section get renamed instead of a new section being added?**

Because the figures were already there. The panel already computed arrivals
served, average wait, average queue length, utilisation and ρ for every stage;
what was missing was a heading that said what they were, so a reader could not
tell whether the per-server bars underneath belonged to the same set of numbers.
"Overview" names nothing. Adding a second Performance Measures group would have
shown the same rows twice on one panel and made the duplication look like
richness. The cost of the rename is real and recorded in D-183: a heading
assertion in the screenshot tests had to change, which is exactly the test that
should notice a rename.
