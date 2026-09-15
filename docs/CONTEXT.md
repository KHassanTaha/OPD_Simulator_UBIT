# Context Document – OPD Clinic Queue Simulation

> **Purpose of this document:** To build the knowledge base needed for the viva.
> Every formula, decision, and assumption is explained so the author can defend
> it orally. This is not just a spec — it is a study guide.

---

## 1. The Real-World System

### 1.1 Clinic Environment
- **Location:** OPD (Outpatient Department), multi-floor clinic.
- **Operating days:** Monday–Thursday and Saturday.
- **Closed:** Friday, Sunday.
- **Official hours:** 9:00 AM – 11:00 AM.
- **Observed reality:** Patients begin arriving around **8:15 AM**; tokens issued from reception; screening service can begin as early as **8:45 AM**.
- **Patient cap:** Observed average of roughly 80–100 patients/day (subject to confirmation). Modelled implicitly; not enforced as a hard constraint unless the user sets it.
- **No shifts:** Staff do not rotate within the 2-hour window.

### 1.2 Patient Flow (3-Stage Serial Network)
[Arrival] → Reception (1 server) → Screening (2 servers) → ┬→ Exit (X-ray / Specialist / Discharge)
							│
							└→ Doctor Consultation (3 servers) → Exit
							
							

1. **Reception** — Single queue, one receptionist issues tokens.
2. **Screening** — Single queue, two parallel screening tables.
3. **Doctor** — Single queue, three parallel doctors (only for patients not exited after screening).

### 1.3 Early Exit Points
A patient may exit after **Screening** with a probability `p_exit`. This is an empirical parameter estimated from the `departure_stage` column in the uploaded data (see §5.4).

---

## 2. Why Queueing Theory? (Viva-Ready Explanation)

Queueing theory gives us **analytical (closed-form) formulas** for the long-run behaviour of a queue. These formulas are essential because they let us **validate** our simulation: if our simulator's output matches the analytical result under the same assumptions, we trust the simulator.

**But** real systems rarely satisfy all the assumptions of analytical models (exponential service, Poisson arrivals, single stage, no early exits). That's precisely **why we simulate** — to handle the messy reality. Analytical models are our **baseline check**, not our final answer.

### 2.1 Key Notation

| Symbol | Meaning | Units |
|---|---|---|
| λ (lambda) | Arrival rate (patients per minute) | 1/min |
| μ (mu) | Service rate per server (patients per minute) | 1/min |
| c | Number of parallel servers at a stage | integer |
| ρ (rho) | Traffic intensity = λ / (c·μ) | dimensionless |
| Lq | Average number of patients waiting in queue | patients |
| Wq | Average waiting time in queue | minutes |
| W | Average time in system (wait + service) | minutes |
| L | Average number in system | patients |

### 2.2 Kendall Notation
`A/S/c` where:
- **A** = inter-arrival distribution (`M` = Markovian/exponential, `G` = general, `D` = deterministic)
- **S** = service distribution (same codes)
- **c** = number of servers

Examples:
- `M/M/1` = exponential arrivals, exponential service, 1 server.
- `M/M/2` = our screening stage.
- `M/M/3` = our doctor stage.

### 2.3 Why ρ ≥ 1 Means the System Is Unstable

If λ ≥ c·μ, then arrivals come in at least as fast as the system can serve them. Over time, the queue grows without bound — the system never reaches steady state. The analytical formulas assume ρ < 1. **Our simulator must refuse to run if any stage has ρ ≥ 1**, because the results would be meaningless.

> **[VERIFIED — owner clarification, 2026-09-13]** ρ is **per-stage**: ρᵢ = λᵢ / (cᵢ·μᵢ) for stage i. There is **no single whole-network ρ** in a multi-stage model — the traffic intensity is defined only stage-by-stage. λᵢ is derived from the external arrival rate λ₀ and the routing probabilities (e.g., λ_screening = λ₀, λ_doctor = λ₀·(1 − p_exit)). The ρ ≥ 1 refusal check uses these routing-derived rates, and the results panel shows ρᵢ for every stage (PRD FR-STAT-6).

---

## 3. Distribution Choices (Viva-Ready Explanation)

### 3.1 Why Exponential for Inter-Arrival Times?
If arrivals are **independent** and **occur at a constant average rate** λ, the **Poisson process** describes them. The time between consecutive arrivals (inter-arrival time) is then **exponentially distributed** with rate λ.

The key property is **memorylessness**: the probability of an arrival in the next minute is independent of how long we've already waited. This matches most real queueing systems well.

**CDF:** F(x) = 1 − e^(−λx)
**PDF:** f(x) = λ·e^(−λx)
**Mean:** 1/λ
**Variance:** 1/λ²

### 3.2 Why Exponential for Service Times?
Exponential service times are a common simplifying assumption (M/M/c models). Real OPD service times often are **not** exponential — they may be lognormal or gamma (many short services, few long ones). Our simulator allows the user to select the distribution, and we fit it from data.

### 3.3 Distributions We Will Offer

| Distribution | Typical Use | Why |
|---|---|---|
| **Exponential** | Inter-arrival, service (default) | Classic M/M/c; simple, memoryless |
| **Normal** | Service (long-run average) | Symmetric; good for aggregated service |
| **Lognormal** | Service (right-skewed) | Common for real service times |
| **Gamma** | Service (flexible shape) | Generalizes exponential; can fit skew |
| **Uniform** | Sanity check / deterministic range | Simple; useful for testing |
| **Poisson** | Arrival *counts* (not times) | If data has counts per interval, not times |

**Excluded for now:** Erlang, Weibull — not mentioned by professor; may add later.

### 3.4 Fitting a Distribution — MLE

**Maximum Likelihood Estimation (MLE)** picks the parameter values that make the observed data most probable. For an exponential distribution, the MLE of λ is:

    λ̂ = 1 / (sample mean of inter-arrival times)

For other distributions (normal, lognormal, gamma), MLE requires numerical optimization; MathNet.Numerics provides built-in fitting.

### 3.5 Chi-Square Goodness-of-Fit (Viva-Critical)

**Purpose:** To test whether the observed data is consistent with the fitted distribution.

**Procedure:**
1. Divide the observed values into **k bins** (bins are auto-selected by the simulator based on sample size — a common rule is `k ≈ √n`).
2. Count observed frequencies `O_i` in each bin.
3. Compute expected frequencies `E_i` using the fitted CDF.
4. Compute the test statistic:
   `χ² = Σ (O_i − E_i)² / E_i`
5. Degrees of freedom: `df = k − 1 − p` where `p` = number of estimated parameters
   (p = 1 for exponential, p = 2 for normal/lognormal/gamma).
6. Compare χ² with the **critical value** from the chi-square table at the chosen significance level (default 5%), or compute the **p-value** directly.
7. **Decision rule:** If p-value < α, reject the fit. Otherwise, fail to reject (fit is acceptable).

**Interpretation caveat (say this in your viva):** "Fail to reject" does **not** mean the distribution is correct — it means we don't have enough evidence to rule it out. Always report the p-value, not just the verdict.

---

## 4. Discrete-Event Simulation (DES) — Viva-Ready Explanation

### 4.1 Why DES?
Time-stepped simulation advances in fixed ticks (e.g., every minute). That wastes computation when nothing happens. **DES jumps from event to event**, keeping a **Future Event List (FEL)** — a priority queue of upcoming events sorted by time. This is efficient and exact.

### 4.2 Event Types in Our Model

| Event | Trigger | Effect |
|---|---|---|
| **Arrival** | Scheduled inter-arrival time elapsed | Create patient; join Reception queue |
| **Reception Service End** | Reception service time elapsed | Move patient to Screening queue |
| **Screening Service End** | Screening service time elapsed | Route patient: exit (`p_exit`) or join Doctor queue |
| **Doctor Service End** | Doctor service time elapsed | Exit patient from system |

### 4.3 Simulation Loop (Pseudo-code)
clock ← 0
FEL ← {Arrival at t=0}
while FEL not empty and clock < horizon:
event ← FEL.pop_earliest()
clock ← event.time
handle(event)
if event is Arrival and clock < closing_time:
schedule next Arrival
if event opened a server:
draw service time from distribution
schedule corresponding Service End



### 4.4 Random Variate Generation

Given a uniform random `U ~ Uniform(0,1)`, we transform to the target distribution using the **inverse CDF**:

- **Exponential(λ):** `X = −ln(U) / λ`
- **Normal(μ, σ):** Box-Muller transform (or MathNet's built-in)
- **Lognormal, Gamma:** MathNet provides samplers directly

The **random seed** (default 42) ensures the same sequence of `U` values → same simulation output every time.

---

## 5. Data & Modelling Assumptions

### 5.1 Time Anchor
- Internally: `t = 0` is the **start of arrival generation** (currently 8:15 AM).
- The simulation clock is in **minutes since t = 0**.
- The UI displays **real clock times** (e.g., "08:42 AM") alongside internal minutes.

### 5.2 Day Length
A "day" begins at t = 0 and ends when **all patients within the daily cap have been served**. Services in progress at 11:00 AM continue to completion.

### 5.3 Closed Days
Friday and Sunday are skipped entirely. The simulator advances from Saturday end to Monday t = 0 with no activity in between.

### 5.4 Departure Stage Column

Each row of input data represents one patient. Columns:

| Column | Meaning |
|---|---|
| `arrival_time` | When the patient arrived (absolute or relative) |
| `<stage>_start` | When service at that stage began |
| `<stage>_end` | When service at that stage ended |
| `departure_stage` | Where the patient exited: `Screening`, `Doctor`, `Reception` |

**Exit probability:**
`p_exit = count(departure_stage = "Screening") / count(departure_stage ∈ {Screening, Doctor})`

> **[VERIFIED — owner clarification, 2026-09-13]** `departure_stage = "Reception"` is treated as a **data anomaly** (a patient leaving at Reception is reneging, which is out of scope). On upload, such rows trigger a **warning** and are **excluded** from the `p_exit` numerator and denominator. They are never modelled as a route.

### 5.5 Current Data Format (for Wednesday's Demo)
- One service stage only (Screening).
- Columns: `arrival_time`, `screening_start`, `screening_end`, `departure_stage`.
- All rows exit at `departure_stage = "Screening"` → `p_exit = 1.0`.
- The engine and loader will be built to **support N stages generically**, so refactoring to add Reception/Doctor columns later is a **config change, not a rewrite**.

> **[VERIFIED — owner clarification, 2026-09-13]** Demo target (2026-09-16): load the sample data → fit distributions (MLE) → chi-square goodness-of-fit → run a single Screening-stage (M/M/2) simulation via the CLI/engine path. The GUI is **not** required for this demo. Engine is N-stage generic from day one.

### 5.6 Rate-Wise vs Mean-Wise Input
Users may provide parameters as:
- **Rate-wise:** λ, μ (events per minute)
- **Mean-wise:** 1/λ, 1/μ (minutes per event)

The UI has a **toggle** so the user declares the mode **before** uploading. The loader validates consistency (e.g., if user says rate-wise but mean is > 1 for a service, that's a mismatch for typical OPD — warn, don't block).

**Manual λ override:** The user may enter λ manually. When this happens, the simulator:
1. Still runs fitting on the data.
2. Shows **both** the fitted distribution and the manual entry side by side.
3. Runs chi-square on **both** for comparison.
4. Uses the **manual λ** in the simulation (manual takes precedence).

### 5.7 Utilisation: Historical vs Simulated, Stage-Level vs Per-Server

**Two sources:**
1. Historical utilisation — derived from uploaded data. Per-server requires
   server-ID columns; without them, only stage-level is possible.
2. Simulated utilisation — computed by the engine. ALWAYS per-server,
   because the engine assigns patients to servers itself.

**Two granularities:**
- Stage-level: sum of all server busy times / (c × operating time).
- Per-server: each server's busy time / operating time.

**Relationship:** util_stage = mean(util_server_1, ..., util_server_c).
The mean can hide imbalance.

**Assignment policy (simulation):** random among idle servers — see
DECISIONS.md entry for rationale.

**Operating time (denominator):** time from the day's **first arrival** to
its **last service end**, identical for historical and simulated values. A
fixed 8:15–11:00 window would let close-time overtime push utilisation
toward (and past) 1, so it is rejected (Decision D-018).

**Imbalance flag:** applies to BOTH sources — flag when
max(util_server) − min(util_server) > 0.15.

> **[UNVERIFIED — assumption, 2026-09-13]** Blank cells inside an optional
> server-ID column: where the patient WAS served at that stage, a blank
> server cell is a **data-quality warning** and the row is excluded from
> that stage's per-server calculation; where the patient was NOT served at
> that stage (e.g., exited at Screening → no `doctor_server`), the blank is
> **expected** and excluded silently.

> **[UNVERIFIED — assumption, 2026-09-16 — D-100]** A blank per-stage
> "Service rate μ (per server)" field means *use the fitted value* (Phase 5),
> exactly like the Section-3 manual λ/μ overrides, so a factory-default
> config is Start-enabled on launch. Non-empty values must be positive
> doubles. Needs owner sign-off against the PRD wording "double > 0" (which
> the GUI reads as required-only-when-non-empty).

---

## 6. Visual Output Analysis

Charts are the standard way to communicate simulation results. For our
project, they serve three purposes:

1. **Input validation** — a histogram with the fitted PDF overlaid shows
   instantly whether the chosen distribution is plausible. If the fit is
   bad, you can see it before reading the chi-square p-value.

2. **Bottleneck identification** — a per-stage utilisation bar chart
   makes the bottleneck stage visually obvious. Combined with per-stage
   ρ, this is the single most informative output for a clinic manager.

3. **Behaviour over time** — a queue-length line chart shows whether the
   system reaches steady state or grows (a sign of instability that the
   ρ check should already have caught).

The simulator uses LiveCharts2. Charts are a presentation layer: the
underlying numbers are always available in the metrics table, so a
chart failure never blocks the results.

> Charts are an M5 (GUI) feature. The groundwork happens earlier: the M1
> statistics collector exposes per-server busy times and waiting-time
> samples, and the M2 fitting module exposes binned data and fitted PDF
> curve points — so chart consumers never need the engine reworked.

---

## 7. External Tooling — SPSS

SPSS is used **outside** the simulator for:
1. **Input analysis:** Fitting distributions independently to confirm MathNet.Numerics results.
2. **Output analysis:** Confidence intervals, ANOVA, regression across scenarios.
3. **Validation:** Cross-check chi-square p-values and parameter estimates.

It is **not** integrated into the simulator. In the viva, frame it as: *"SPSS was used as an independent statistical workbench; our simulator reimplements the core tests for self-containment."*

---

## 8. Validation Strategy

| Check | Method |
|---|---|
| **Unit correctness** | Unit tests for each class (queue, server, RNG). |
| **Model verification** | Event log trace — walk through a 5-patient example by hand. |
| **Analytical validation** | Compare simulator output against M/M/c formulas for exponential parameters. Report % error. |
| **Stability check** | Refuse to run if ρ ≥ 1 at any stage. |
| **Utilisation sanity** | Assert 0 ≤ utilisation ≤ 1 at all times. |
| **Data validation** | Reject dirty data; report specific issues. |
| **SPSS cross-check** | Manual comparison of fitted parameters and chi-square results. |

---

## 9. Glossary (Viva Quick-Reference)

- **DES** — Discrete-Event Simulation
- **FEL** — Future Event List
- **MLE** — Maximum Likelihood Estimation
- **ρ (rho)** — Traffic intensity
- **p_exit** — Probability a patient exits after Screening
- **λ, μ** — Arrival rate, service rate
- **Balking** — Customer refuses to join a long queue (out of scope)
- **Reneging** — Customer leaves before being served (out of scope)

---

## 10. References

1. Law, A.M. & Kelton, W.D. (2000). *Simulation Modeling and Analysis*. McGraw-Hill.
2. Banks, J. et al. (2010). *Discrete-Event System Simulation*. Pearson.
3. Gross, D. & Harris, C.M. (1998). *Fundamentals of Queueing Theory*. Wiley.
4. MathNet.Numerics documentation: https://numerics.mathdotnet.com/
5. Avalonia UI documentation: https://docs.avaloniaui.net/