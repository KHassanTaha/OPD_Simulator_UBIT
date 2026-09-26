#!/usr/bin/env python3
"""Generates samples/sample_3stage_variable.csv, the G/G/c auto-fit success fixture.

Usage:  python3 scripts/generate-3stage-variable-fixture.py
Output: samples/sample_3stage_variable.csv  (deterministic, seed 42)

WHY THIS FILE EXISTS
--------------------
Phase 8K's G/G/c auto-fit searches the five continuous families by AIC and applies
the winner. That success path had no fixture to run against, and the reason is
worth recording because it is a property of the existing data, not a gap in it:

    samples/sample_3stage_clinic.csv has ZERO service-time variance.
        Reception: 20 samples, all exactly 2 minutes
        Screening: 20 samples, all exactly 4 minutes

A constant sample is a legitimate input, and the fitter rejects it correctly and
with a good reason for every family:

    Exponential -> "expected bin count below 5"   (a constant fills one chi-square bin)
    Normal / Lognormal / Gamma / Uniform
                -> "requires at least two distinct samples"

So that file can exercise the auto-fit REFUSAL path (and does, in
Phase8KTests) but can never exercise the success path. This fixture supplies
service times that actually vary, so the search has something to choose between.

SERVICE MEANS
-------------
Chosen to match the clinic in CONTEXT.md 1.2 and to be stable against the
1/2/3-server topology the manual walkthrough uses. The means are EXACT, not
aspirational - see recalibrate() - so the stability claim holds:

    Reception  mean 2.000 min -> mu 0.500/min, c=1, capacity 0.500, rho 0.74
    Screening  mean 4.000 min -> mu 0.250/min, c=2, capacity 0.500, rho 0.74
    Doctor     mean 5.000 min -> mu 0.200/min, c=3, capacity 0.600, rho 0.62

lambda = 0.371/min from the generated arrival column (mean gap 2.69 min), with
arrivals running 08:15 -> 10:27, inside the 08:15-11:00 observed window. All three
rho are below 1, so the walkthrough in docs/DEV_LAUNCH.md does not trip the
rho >= 1 refusal.

ACHIEVED DISPERSION (seed 42, n = 50 per stage)
-----------------------------------------------
    Reception  sd 2.30  9 distinct values  range 1-11
    Screening  sd 3.21 11 distinct values  range 1-12
    Doctor     sd 6.91 14 distinct values  range 1-36

Doctor carries one 36-minute service. That value comes from seed 42's raw
exponential draw (the largest of 50 draws at a 5-minute mean is a ~4.5 sigma
event, so it is a legitimate sample, not a bug), and recalibrate() does not
create or inflate it. It is left in rather than re-rolled: the ruling fixes the
seed, and a clinic that occasionally has one very long consultation is the
realistic case. It does raise Doctor's coefficient of variation to 1.38 against
the 1.00 an exponential would give, which is why Doctor's family search has the
most to discriminate between.

CHI-SQUARE p-VALUES ARE ~0 ON THIS FIXTURE, AND THAT IS REPORTED NOT FIXED
-------------------------------------------------------------------------
Every stage's lowest-AIC family comes back with p = 0.0000, i.e. the
goodness-of-fit test REJECTS the family the fitter selected. The cause is the
whole-minute lattice: rounding to integers makes the observations discrete, and
no continuous family fits a discrete sample, so the chi-square correctly says
"not this continuous model" while AIC still ranks one of them least-bad.

This is left as-is deliberately. Making the p-values pretty would mean choosing
a seed or a rounding rule for the purpose of producing a flattering number, and
Phase 8K's own test ruling forbids pinning a "good" p-value. It is also the more
instructive outcome: the badge reports AIC and the p-value side by side, so the
app shows that SELECTING a family by AIC and ACCEPTING it by goodness-of-fit are
two different questions with two different answers. That is a viva answer, not a
defect. See D-152.

p_exit IS ZERO HERE, DELIBERATELY
---------------------------------
Every one of the 50 patients visits all three stages, which is what "about 50
samples per stage" requires: with any patients exiting at Screening, the Doctor
stage could not also have 50. This fixture's job is the per-stage family search,
not early exit. Early exit is exercised by sample_3stage_clinic.csv, whose 6
departures give p_exit = 6/20 = 0.30.

TWO DETAILS THAT LOOK LIKE BUGS AND ARE NOT
------------------------------------------
1. Service times are floored at 1 minute. The raw exponential draws a zero with
   probability 1 - e^(-0.5) = 39% at a 2-minute mean, and a zero-minute service
   is a data-quality artefact rather than a measurement. It also matters
   technically: a zero sends Gamma's density to +infinity at the origin and
   produced the AIC = -inf defect fixed in D-156.
2. Times are written as whole minutes because the loader's clock format is
   H:MM. The variance the auto-fit needs survives the rounding; the chi-square
   bin count is computed on the loaded values, so it sees the same integers.
"""

import os
import random

SEED = 42
PATIENT_COUNT = 50
SERVICE_MEANS = {"reception": 2.0, "screening": 4.0, "doctor": 5.0}
MEAN_INTER_ARRIVAL = 3.0
FIRST_ARRIVAL = 8 * 60 + 15  # 08:15, the observed start of arrivals (CONTEXT.md 1.1)


def service_minutes(rng, mean):
    """One exponentially-distributed service time in whole minutes, at least 1.

    Rounded for the H:MM clock format, then floored at 1 so no row carries an
    impossible zero-minute service (see module docstring, note 1).
    """
    return max(1, round(rng.expovariate(1.0 / mean)))


def recalibrate(values, target_mean):
    """Nudge a column of whole-minute service times so its mean is exactly target.

    WHY: with n = 50 the sampling error of the mean is about mean/sqrt(50), so raw
    draws landed 25% off target (Screening drew 5.10 against a 4.0 target). The
    variance is what the auto-fit actually needs and the raw draws have plenty of
    it, but the docstring's stability claim - and the walkthrough that depends on
    rho < 1 - is written in terms of the MEANS, so the means have to be the ones
    quoted. Adding or removing single minutes round-robin fixes the sum without
    reshaping the distribution, and never pushes a value below 1.
    """
    target_sum = round(target_mean * len(values))
    current_mean = sum(values) / len(values)
    order = sorted(range(len(values)), key=lambda i: (abs(values[i] - current_mean), i))
    step = 1 if target_sum > sum(values) else -1
    index = 0
    guard = 0
    while sum(values) != target_sum and guard < 10_000 * len(values):
        i = order[index % len(order)]
        candidate = values[i] + step
        if candidate >= 1:
            values[i] = candidate
        index += 1
        guard += 1
    return values


def hhmm(minutes):
    return f"{minutes // 60}:{minutes % 60:02d}"


def main():
    rng = random.Random(SEED)

    # Draw first, correct second, then lay the day out on the clock. Recalibrating a
    # column only shifts that patient's own times, so the per-patient sequence
    # (arrival -> reception -> screening -> doctor) stays intact.
    arrivals = [FIRST_ARRIVAL]
    for _ in range(PATIENT_COUNT - 1):
        # Whole-minute inter-arrivals, so the clock stays an int and no float drift
        # accumulates down the column. See module docstring, note 2.
        arrivals.append(
            arrivals[-1] + max(1, round(rng.expovariate(1.0 / MEAN_INTER_ARRIVAL)))
        )

    services = {}
    for stage, mean in SERVICE_MEANS.items():
        services[stage] = recalibrate(
            [service_minutes(rng, mean) for _ in range(PATIENT_COUNT)], mean
        )

    rows = []
    for i, arrival in enumerate(arrivals):
        rec_start = arrival
        rec_end = rec_start + services["reception"][i]
        scr_start = rec_end
        scr_end = scr_start + services["screening"][i]
        doc_start = scr_end
        doc_end = doc_start + services["doctor"][i]

        rows.append(
            f"{hhmm(arrival)},Doctor,"
            f"{hhmm(rec_start)},{hhmm(rec_end)},"
            f"{hhmm(scr_start)},{hhmm(scr_end)},"
            f"{hhmm(doc_start)},{hhmm(doc_end)}"
        )

    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(root, "samples", "sample_3stage_variable.csv")
    header = (
        "arrival_time,departure_stage,"
        "reception_start,reception_end,"
        "screening_start,screening_end,"
        "doctor_start,doctor_end"
    )
    with open(out, "w", encoding="utf-8") as handle:
        handle.write(header + "\n")
        handle.write("\n".join(rows) + "\n")

    print(f"wrote {out} ({len(rows)} patients, seed {SEED})")
    for stage, mean in SERVICE_MEANS.items():
        print(f"  {stage:<10} target mean {mean:.1f} min")


if __name__ == "__main__":
    main()
