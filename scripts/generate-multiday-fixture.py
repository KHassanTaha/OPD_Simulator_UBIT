#!/usr/bin/env python3
"""Generates samples/sample_multiday.csv, the six-session observation-window fixture.

Usage:  python3 scripts/generate-multiday-fixture.py
Output: samples/sample_multiday.csv  (deterministic, seed 42)

WHY THIS FILE EXISTS
--------------------
Phase 8O added the data OBSERVATION WINDOW and, with it, a second arrival-rate
estimate. Neither could be exercised before this fixture, because no loadable
file could say "I came from more than one day":

    arrival_time is a TIME OF DAY. TimeParser returns minutes since midnight
    and throws the date away, so every file ever loaded looked like one session
    on a weekday nobody could name. The observation window therefore had nothing
    to count, and window lambda was always just N/165.

So the input format gained an optional `session_date` column (D-175) and this
file is the first thing that uses it. It is the fixture behind
`ObservationWindow_FromFile_SixSessions_ReturnsSixDays` and
`DataAnalyzer_MultidayFile_WindowLambdaDiffersFromMle`.

SIX SESSIONS ACROSS TWO WEEKS
-----------------------------
    2026-09-14 Mon    2026-09-15 Tue    2026-09-16 Wed
    2026-09-17 Thu    2026-09-19 Sat    2026-09-21 Mon

Friday the 18th and Sunday the 20th are absent, and that is the point: the
clinic is closed on them (CONTEXT 1.1), so 2026-09-14 -> 2026-09-21 is a
SEVEN-calendar-day span carrying SIX operating sessions. The observation window
is 6 x 165 = 990 operating minutes, not 7 days. A window that counted elapsed
wall-clock time would report 7 days and be wrong by 17%.

10 PATIENTS PER SESSION, 60 ROWS TOTAL
--------------------------------------
Round numbers so a reader can check the arithmetic by eye: 6 sessions x 10
patients, and 10 x 6 = 60 arrivals over 990 operating minutes.

THE ARRIVALS SIT IN THE MORNING, AND THAT IS THE WHOLE LESSON
-------------------------------------------------------------
Each session's 10 patients arrive between 08:15 and roughly 09:50 - a morning
peak inside a session that stays open until 11:00. That leaves the back half of
every session empty, which is what makes the two rate estimates disagree:

    window lambda = 60 / 990            = 0.0606 /min  (spread over all 990 min)
    MLE lambda    = 1 / mean(gap)       = 0.1360 /min  (the observed burst rate)

They differ by 55%, far more than the 5% that triggers the divergence note, and
the reason is NOT a gap and NOT a data error. It is that MLE lambda describes
the rate WHILE PATIENTS WERE ARRIVING, and window lambda describes the rate
across the session the clinic actually offers. For a queueing simulation the
second is the one you want, because the run will be 165 minutes long and
applying the burst rate to all of it would over-predict arrivals by more than
double.

Both estimates leave the run stable at the fixture's service rates: MLE lambda
0.136 against Reception mu 0.5 on one server is rho = 0.27, and window lambda
0.0606 is rho = 0.12, so neither trips the rho >= 1 refusal.

This is also why the note in the Input tab says "the observation period is
shorter than the session length" rather than blaming a multi-day span. On a
SINGLE-session file the same gap between the two numbers appears, so any
explanation that mentions days would be false about most files.

The 54 inter-arrival gaps that survive are 59 gaps minus the 5 overnight ones.

OVERNIGHT GAPS ARE NOT INTER-ARRIVAL TIMES
------------------------------------------
Between the last patient on Monday (09:4x) and the first on Tuesday (08:15) the
time-of-day column reads as a DECREASE, and the naive gap is about -155 minutes.
D-173 rules those out of the inter-arrival sample: an inter-arrival distribution
is a within-session property, and overnight clinic closures are not
inter-arrival times. The validator resets its ordering check at each session
boundary for the same reason - a session boundary is not an out-of-order
arrival. Without both rules this file would be rejected as out-of-order AND
would produce a negative mean gap.

p_exit IS 1.0 HERE, DELIBERATELY
--------------------------------
Every patient departs at Screening, so the doctor_start/doctor_end cells are
left EMPTY. That is legal, not an oversight: DataValidator.StageMayBeBlankFor
permits a blank stage cell when the patient exited at an earlier stage in the
clinic flow. It keeps this fixture's job narrow - the arrival side - instead of
mixing in a p_exit question that Phase 8O does not touch. Fixtures that need
p_exit around 0.3 already exist (sample_3stage_clinic.csv).

Service times are whole minutes for the loader's H:MM clock format, and floored
at 1 so no row carries an impossible zero-minute service.
"""

import csv
import datetime
import os
import random

SEED = 42
PATIENTS_PER_SESSION = 10

# First Monday of the fixture fortnight; the rest are derived, then the closed
# days are skipped, so the weekday set cannot silently drift out of CONTEXT 1.1.
FIRST_SESSION = datetime.date(2026, 9, 14)
OPEN_WEEKDAYS = {0, 1, 2, 3, 5}  # Mon, Tue, Wed, Thu, Sat (Mon = 0)

SESSION_START = 8 * 60 + 15  # 08:15, the clinic opens (CONTEXT.md 1.1)
LAST_ARRIVAL = 9 * 60 + 50  # 09:50 - the morning peak ends here, tail is empty
MEAN_GAP = 7.0  # minutes, within-session
RECEPTION_MEAN = 2.0
SCREENING_MEAN = 4.0


def sessions(count):
    """The next `count` clinic-open days, starting at FIRST_SESSION, in order."""
    found, day = [], FIRST_SESSION
    while len(found) < count:
        if day.weekday() in OPEN_WEEKDAYS:
            found.append(day)
        day += datetime.timedelta(days=1)
    return found


def clock(minutes):
    """Whole minutes past midnight as the loader's H:MM text, e.g. 495 -> '8:15'."""
    return f"{int(minutes) // 60}:{int(minutes) % 60:02d}"


def service_minutes(rng, mean):
    """One exponentially-distributed service time in whole minutes, at least 1."""
    return max(1, round(rng.expovariate(1.0 / mean)))


def main():
    rng = random.Random(SEED)
    days = sessions(6)

    rows = []
    for day in days:
        arrival = SESSION_START
        for _ in range(PATIENTS_PER_SESSION):
            arrival = min(arrival, LAST_ARRIVAL)
            reception_end = arrival + service_minutes(rng, RECEPTION_MEAN)
            screening_end = reception_end + service_minutes(rng, SCREENING_MEAN)
            rows.append(
                {
                    # The new column first, so it is visible at a glance in the file.
                    "session_date": day.isoformat(),
                    "arrival_time": clock(arrival),
                    "departure_stage": "Screening",
                    "reception_start": clock(arrival),
                    "reception_end": clock(reception_end),
                    "screening_start": clock(reception_end),
                    "screening_end": clock(screening_end),
                    # Blank by design: the patient never reached the doctor.
                    "doctor_start": "",
                    "doctor_end": "",
                }
            )
            arrival += max(1, round(rng.expovariate(1.0 / MEAN_GAP)))

    out = os.path.join(os.path.dirname(__file__), "..", "samples", "sample_multiday.csv")
    out = os.path.normpath(out)
    fields = list(rows[0].keys())
    # lineterminator="\n", not csv's default "\r\n": every other file in
    # samples/ is LF, and a CRLF sample would make the committed fixture differ
    # from a freshly generated one on any platform that checks out text.
    with open(out, "w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)

    print(f"wrote {out}")
    print(f"  {len(rows)} rows across {len(days)} sessions: "
          + ", ".join(f"{d} {d.strftime('%a')}" for d in days))


if __name__ == "__main__":
    main()
