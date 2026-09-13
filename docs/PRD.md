# Product Requirements Document – OPD Clinic Queue Simulator

**Version:** v1.3.0
**Date:** 2026-09-13
**Author:** Taha Hassan
**Course:** Simulation & Modelling
**Master document:** Yes. `REQUIREMENTS.md` is derived from this file.
**Change History:** See §10.

---

## 1. Overview

A desktop application simulating patient flow through an OPD clinic with a
3-stage serial queueing network. The user configures the model, uploads
historical data, runs a discrete-event simulation, and views validation
results — all through a modern cross-platform UI.

**Goal:** A transparent, data-driven, statistically validated simulator
that the author can fully explain in a viva.

---

## 2. Problem Statement

Simulate the following process:

1. **Reception** – 1 server, single FIFO queue (token issuance).
2. **Screening** – 2 servers, single FIFO queue.
3. **Doctor Consultation** – 3 servers, single FIFO queue (only for
   patients not exited after Screening).

After Screening, a patient exits with probability `p_exit` (empirically
estimated) or proceeds to Doctor consultation.

**The simulator must:**
- Load real patient data (Excel) and fit distributions.
- Accept user-configurable server counts and distributions.
- Run discrete-event simulation over user-chosen time horizons.
- Report per-stage performance metrics.
- Perform chi-square goodness-of-fit and validate against analytical M/M/c.
- Show every calculation step in a traceable event log.
- Refuse to run unstable configurations.
- Support both "rate-wise" and "mean-wise" parameter input.
- Show fitted vs. manually entered λ for comparison.
- Provide a token generator (built last, but designed for from day 1).

---

## 3. Goals & Objectives

- Model a multi-stage queueing network correctly.
- Implement data-driven input modelling (MLE fitting + chi-square).
- Learn and demonstrate DES internals (FEL, event handling, routing).
- Produce a UI usable by a non-programmer.
- Pass viva with complete understanding of every component.

---

## 4. Scope

### 4.1 In Scope

**Process:**
- 3-stage serial network: Reception (1) → Screening (2) → Doctor (3).
- Probabilistic exit after Screening (via `p_exit`).

**Data:**
- Upload `.xlsx` (primary) or `.csv` (fallback).
- Columns: `arrival_time`, per-stage `<stage>_start` / `<stage>_end`,
  and `departure_stage`.
- One row per patient.
- Rate-wise **or** mean-wise parameter interpretation (user-selected via toggle).

**Model Configuration:**
- Per-stage server count (defaults: 1, 2, 3).
- Per-stage service distribution (initially shared; per-stage enabled as new data arrives).
- Inter-arrival distribution selection.
- Manual λ override (with fitted distribution shown alongside).

**Time:**
- t = 0 anchored to arrival generation start (currently 8:15 AM).
- UI displays real clock time.
- Single-day **or** multi-day run modes (user-selectable).
- Closed days (Fri, Sun) skipped instantly.
- A day ends when all patients within the cap are served.

**Statistics:**
- Chi-square goodness-of-fit (default α = 5%, user-selectable).
- Auto bin count.
- p-value displayed.
- Automatic comparison against analytical M/M/c results (where applicable).
- Simulated per-server utilisation + stage-level mean (stage-level = mean of per-server); imbalance flag when max − min > 0.15 (historical included when server-ID columns present).
- Charts (LiveCharts2): histogram + fitted PDF, chi-square observed/expected bars, per-server utilisation bars (P2: queue length over time, waiting-time histogram).

**UI:**
- Left panel: configuration.
- Right panel: results (metrics table, chi-square output, event log).
- Separate tab: token generator (built last).

**Validation:**
- Refuse to run if ρ ≥ 1 at any stage.
- Hard assertion: 0 ≤ utilisation ≤ 1.
- Reproducibility via random seed (default 42, user-editable).

### 4.2 Out of Scope

- Priority queues (all FIFO).
- Staff breaks or shift changes.
- Dynamic routing based on patient characteristics.
- Balking, reneging, patient abandonment (may be added if course requires).
- Erlang and Weibull distributions (not covered by professor).
- Deep learning or ML components.

### 4.3 Modelled Implicitly (Real-World Observations)

- Patient cap of ~80–100/day — observed average, not enforced as a hard
  constraint unless the user configures it. Default: configurable, blank = no cap.

---

## 5. Functional Requirements

### 5.1 User Interface

**FR-UI-1 (Layout)**
- Left panel:
  - Parameter input mode toggle: **Rate-wise** / **Mean-wise**
  - Dropdown: inter-arrival distribution
  - Dropdown: service distribution
  - Numeric inputs: servers per stage (Reception, Screening, Doctor)
  - Numeric input: manual λ (optional; if entered, disables auto-fit for λ but fitting still runs for comparison)
  - Button: Upload Data (`.xlsx`, `.csv`)
  - Dropdown: time horizon (15 min, 1 hour, 1 day, 1 week, 1 month, custom days)
  - Dropdown: run mode (**Single day** / **Multi-day**)
  - _Semantics ([VERIFIED] — owner clarification, 2026-09-13): run mode = **structure**, horizon = **span**. Single-day → horizon trims how much of that day runs (15 min / 1 hour / full day). Multi-day → horizon selects 1 week / 1 month / custom days; closed days (Fri, Sun) are skipped; the daily cap resets each day._
  - Numeric input: Random seed (default 42)
  - Numeric input: daily patient cap (default blank = uncapped)
  - Button: Run Simulation
- Right panel:
  - Performance metrics table (per stage)
  - Chi-square results (fitted + manual side-by-side if applicable)
  - Scrollable event log
- Separate tab: Token Generator (built last; placeholder in v1)

**FR-UI-2 (Input Validation)**
- Positive integers for servers, cap, seed.
- File upload accepts `.xlsx` and `.csv`.
- Data type mismatch (rate vs. mean) triggers a warning with option to reinterpret or reupload.
- Clear error messages for invalid input.

**FR-UI-3 (Responsiveness)**
- Simulation runs on a background thread.
- Progress bar / status message during run.
- UI remains interactive.

**FR-UI-4 (Charts / Visual Output)**
The results panel SHALL display the following charts using LiveCharts2:
- Histogram of inter-arrival times with fitted PDF overlaid.
- Histogram of service times per stage with fitted PDF overlaid.
- Chi-square observed vs expected frequencies (bar chart).
- Per-server utilisation bar chart per stage.
- (P2) Queue length over time (line chart).
- (P2) Waiting time distribution histogram.

Charts are rendered after a simulation run and updated when the user re-runs with different parameters. Charts are a presentation layer only — they must not be required for the simulation engine to produce metrics.

### 5.2 Data Handling

**FR-DATA-1:** Accept `.xlsx` (primary) or `.csv` (fallback).
**FR-DATA-2:** Required columns: `arrival_time`, `<stage>_start`, `<stage>_end`, `departure_stage`.
**FR-DATA-3:** Compute inter-arrival times from consecutive `arrival_time` values.
**FR-DATA-4:** Compute per-stage waiting and service times from the column triplets.
**FR-DATA-5:** Fit selected distributions via MLE.
**FR-DATA-6:** Compute `p_exit` from `departure_stage` column. Rows with `departure_stage = Reception` trigger a **warning** and are **excluded** from the `p_exit` numerator and denominator (leaving at Reception = reneging; out of scope). _(Owner clarification, 2026-09-13)_
**FR-DATA-7:** Reject dirty data (missing, non-numeric, unsorted, duplicate, inconsistent stage pairs). Report specific problems and request cleaned data.
**FR-DATA-8:** Validate that the uploaded data matches the declared mode (rate-wise vs. mean-wise). Warn on mismatch; do not silently reinterpret.
**FR-DATA-9:** Refactor plan: current format supports 1 stage. All logic is N-stage generic; adding columns later is a config change.
**FR-DATA-10 (Optional per-server columns for historical validation):** The uploaded file MAY include per-stage server-identity columns (`reception_server`, `screening_server`, `doctor_server`).
- If present → historical per-server utilisation is computed and validated.
- If absent → historical validation falls back to stage-level only.
- Missing columns do NOT trigger file rejection.

Note: This FR affects HISTORICAL validation only. SIMULATED per-server utilisation is always available (see FR-STAT-7). The imbalance flag (max−min > 0.15, FR-STAT-7) applies to historical per-server utilisation too when the columns are present.

### 5.3 Simulation Model

**FR-SIM-1:** Model as a 3-stage serial network (defaults: 1, 2, 3 servers). The engine is **N-stage generic from day one** — the 3-stage network is a configuration, not a hard-coded structure. _(Owner clarification, 2026-09-13)_
**FR-SIM-2:** DES with FEL. Events: Arrival, Reception End, Screening End, Doctor End.
**FR-SIM-3:** Inter-arrival times from selected distribution; service times per stage from selected distribution (shared initially; per-stage enabled later).
**FR-SIM-4:** `p_exit` applied after Screening. Estimated from data if available; otherwise user-configurable (default 0.5).
**FR-SIM-5:** Arrival generation window: 8:15 AM onward (configurable), until daily cap reached or 11:00 AM, whichever first.
**FR-SIM-6:** Services in progress at close time continue to completion.
**FR-SIM-7:** Day ends when all capped patients are served.
**FR-SIM-8:** Closed days (Fri, Sun) skipped.
**FR-SIM-9:** Single-day and multi-day modes user-selectable.
**FR-SIM-10:** Internal clock in minutes (t=0 = arrival start); UI displays real clock time.

### 5.4 Statistics & Validation

**FR-STAT-1:** Chi-square goodness-of-fit on inter-arrival and service fits.
**FR-STAT-2:** Default α = 0.05; user-selectable.
**FR-STAT-3:** Automatic bin count based on sample size.
**FR-STAT-4:** Display: observed, expected, χ², df, p-value, decision.
**FR-STAT-5:** Automatic comparison against analytical M/M/c results (when exponential + single-stage-compatible config).
**FR-STAT-6 (Per-Stage ρ Display):** Display the traffic intensity ρᵢ = λᵢ / (cᵢ · μᵢ) for every stage i in the results panel, so the bottleneck stage is visible at a glance. Referenced from FR-VAL-1, which refuses to run if any ρᵢ ≥ 1.
**FR-STAT-7 (Simulated per-server utilisation):** The simulation engine SHALL track and report utilisation for EACH individual server at every stage, regardless of input columns, because the engine assigns patients to servers during simulation.
- Report per-server utilisation AND stage-level utilisation.
- Stage-level = mean of per-server utilisation values.
- Flag imbalance if max(server_util) − min(server_util) > 0.15.
- Server-assignment policy: random among idle servers (see DECISIONS.md).
- Operating time (denominator) = time from the day's first arrival to its last service end — identical for historical and simulated values.
- The imbalance flag applies to both simulated and historical per-server utilisation.
**FR-STAT-8 (Visual Output for Fits):** For each fitted distribution (inter-arrival, per-stage service), the simulator SHALL render a histogram of the observed data with the fitted PDF (or PMF) overlaid on the same axes. Bin count for the histogram matches the chi-square bin count (FR-STAT-3) to keep the visual and the test consistent.
**FR-VAL-1 (Per-Stage Stability Check):** Before running, compute ρᵢ = λᵢ / (cᵢ · μᵢ) for every stage i, where λᵢ is derived from the external arrival rate λ₀ and the routing probabilities. If any ρᵢ ≥ 1, refuse to run and report ALL unstable stages with their λᵢ, cᵢ, μᵢ, and ρᵢ. Display the computed values as per FR-STAT-6.
**FR-VAL-2:** Hard assertion: 0 ≤ utilisation ≤ 1 per server. Failure crashes the run with diagnostic.
**FR-VAL-3:** Random seed input (default 42). Logged with every run.
**FR-VAL-4:** Event log records every event with time, type, patient ID, queue lengths, server status, and RNG draws.
**FR-VAL-5:** (Stretch) N replications with confidence intervals.

### 5.5 Token Generator (Extra Feature)

**FR-TOKEN-1:** Design for from day 1 (reserve UI tab, engine hook), but implement last.
**FR-TOKEN-2:** For each arrival, generate a token with:
- Sequential token number
- Estimated waiting time based on current queue state
**FR-TOKEN-3:** Token displayed on a separate tab with UI flourish.

---

## 6. Non-Functional Requirements

**NFR-1 (Modularity):** Separate projects for Core, Data, App, CLI, Tests.
**NFR-2 (Documentation):** XML doc comments on all public APIs.
**NFR-3 (Performance):** Simulate 4,000 patients over 30 days (~134/day) in under 3 seconds on a mid-range laptop (excluding UI).
**NFR-4 (Reproducibility):** Deterministic given seed; logged with each run.
**NFR-5 (Technology):**
- Language: C# on .NET 8 LTS
- UI: Avalonia UI (MVVM)
- Statistics: MathNet.Numerics
- Data: ClosedXML (.xlsx) + CsvHelper (.csv)
- Platform: Cross-platform (Linux dev, Windows deploy)
- Build: `dotnet publish` single-file per OS

**NFR-6 (Chart Rendering Performance):**
Charts SHALL render in under 500 ms for datasets up to 10,000 points, using downsampling if necessary. Chart rendering SHALL NOT block the UI thread.

---

## 7. Success Criteria

- Uploads data and runs without errors on both Linux and Windows.
- Chi-square correctly evaluates fits and displays p-value.
- Event log traces a full simulation end-to-end.
- Unstable configurations (ρ ≥ 1) are refused with clear messaging.
- Utilisation is always within [0, 1]; assertion enforces this.
- Simulator matches analytical M/M/c within acceptable tolerance for validation cases.
- Charts correctly visualise the fitted distributions and utilisation, and match the numerical results shown in the tables.
- Token generator produces a plausible estimate.
- Every line of code is explainable in the viva.

---

## 8. Assumptions

| # | Assumption | Status |
|---|---|---|
| 1 | t=0 anchored to 8:15 AM arrival start | [UNVERIFIED] |
| 2 | Data uploaded as `.xlsx` exported from Google Sheets | [VERIFIED] |
| 3 | One row = one patient, per-stage start/end columns | [VERIFIED] |
| 4 | Daily cap ~80–100 (implicit, configurable) | [UNVERIFIED] |
| 5 | `p_exit` derived from `departure_stage` column | [VERIFIED] |
| 6 | Manual λ override applies to λ only (professor instruction) | [VERIFIED] |
| 7 | SPSS used externally, not integrated | [VERIFIED] |
| 8 | Rate-wise and mean-wise both supported | [VERIFIED] |
| 9 | `departure_stage = Reception` rows are excluded from `p_exit` (reneging, warn only) | [VERIFIED] |
| 10 | Run mode = structure; time horizon = span (single-day vs multi-day semantics) | [VERIFIED] |
| 11 | ρᵢ computed per stage (ρᵢ = λᵢ/(cᵢ·μᵢ)); λᵢ derived from external λ₀ and routing probabilities; all unstable stages reported | [VERIFIED] |
| 12 | Engine is N-stage generic from day one; 3 stages are a configuration | [VERIFIED] |

---

## 9. Open Questions (to resolve with professor)

- OQ-1: Confirm daily cap value and whether it is a hard constraint.
- OQ-2: Confirm 8:15 AM arrival window or adjust based on incoming data.
- OQ-3: Confirm that "first distribution" = inter-arrival and "second" = service.
- OQ-4: Confirm exit probability should be per-stage or global.

---

## 10. Change History

| Version | Date | Change | Sections Impacted |
|---|---|---|---|
| v1.0.0 | 2026-09-13 | Initial complete draft | All |
| v1.0.1 | 2026-09-13 | Owner clarifications applied (2026-09-13): `departure_stage = Reception` warn + excluded from `p_exit`; run-mode/horizon semantics defined; ρ guard uses effective per-stage arrival rate; engine N-stage generic from day one; demo (2026-09-16) scoped to load→fit→chi-square→run via CLI. OQ-1..4 remain open. | §4, §5, §8, §10 |
| v1.1.0 | 2026-09-13 | Per-stage stability model: FR-VAL-1 rewritten (ρᵢ = λᵢ/(cᵢ·μᵢ), λᵢ derived from external λ₀ and routing probabilities, refusal reports ALL unstable stages); new FR-STAT-6 (per-stage ρ display in results panel); CONTEXT §2.3 extended with per-stage ρ + routing explanation; decision D-015. | §4, §5, §8, §10 |
| v1.2.0 | 2026-09-13 | Per-server utilisation: new FR-DATA-10 (optional server-ID columns → historical per-server validation, never rejects); new FR-STAT-7 (simulated per-server utilisation always available, stage-level = mean of per-server, imbalance flag when max−min > 0.15 on both sources, random-among-idle assignment); operating time defined as first-arrival → last-service-end per day; CONTEXT §5.7; decisions D-016..D-018. | §4, §5, §10 |
| v1.3.0 | 2026-09-13 | Charts: new FR-UI-4 (LiveCharts2 charts in results panel: histogram + fitted PDF, chi-square bars, per-server utilisation bars, P2 queue-over-time + waiting-time histogram; presentation layer only); new FR-STAT-8 (visual output for fits, histogram bin count = chi-square bin count); new NFR-6 (chart render < 500 ms / 10k points, downsampling, non-blocking); success criteria + §4.1 bullets; CONTEXT §6 Visual Output Analysis added, later CONTEXT sections renumbered (SPSS→§7, Validation→§8, Glossary→§9, References→§10); decision D-019. Charts belong to M5, with M1 statistics collector and M2 fitting exposing the binned/PDF/series data. | §4, §5, §6, §7, §10 |