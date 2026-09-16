# VIVA_ANSWERS.md — Oral-Exam Speaking Points

Purpose: short, honest framings the author can say aloud in the viva without
hesitating. Entries are added as features land, newest first. This file was
created during the GUI rebuild (Phase 5); the earlier M5-era answer bank did
not survive the view-layer replacement (see `docs/M5_FAILURES.md`).

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
outputs are compared against the M/M/c formulas and the % error reported.
(3) `dotnet test` — 230 tests including the run-flow tests that assert refusal
banners, trace detail by level, and multi-day day counts.