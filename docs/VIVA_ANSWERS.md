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
