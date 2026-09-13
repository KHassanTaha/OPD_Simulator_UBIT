# REQUIREMENTS.md — Traceability Matrix

**Purpose:** Derived view of `PRD.md`. Every requirement is tracked from
spec → code → test → decision.

**Source of truth:** `PRD.md` (master). If this file disagrees with PRD.md,
PRD.md wins.

**Last synced with PRD.md:** 2026-09-13 (v1.3.0)

**Status vocabulary:** `[ ]` TODO · `[~]` IN PROGRESS · `[x]` DONE ·
`[?]` BLOCKED · `[-]` CANCELLED

---

## Functional Requirements — UI

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| FR-UI-1 | Left config panel + right results panel | [ ] | — | — | — |
| FR-UI-2 | Input validation (numeric, file type, mode mismatch) | [ ] | — | — | — |
| FR-UI-3 | Background thread + progress indicator | [ ] | — | — | — |
| FR-UI-4 | Charts via LiveCharts2 (P1 + P2) | [ ] | — | — | — |

## Functional Requirements — Data

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| FR-DATA-1 | Accept .xlsx and .csv | [ ] | — | — | — |
| FR-DATA-2 | Require columns: arrival_time, *_start, *_end, departure_stage | [ ] | — | — | — |
| FR-DATA-3 | Compute inter-arrival times | [ ] | — | — | — |
| FR-DATA-4 | Compute per-stage waiting + service times | [ ] | — | — | — |
| FR-DATA-5 | Fit selected distribution via MLE | [ ] | — | — | — |
| FR-DATA-6 | Compute p_exit from departure_stage | [ ] | — | — | — |
| FR-DATA-7 | Reject dirty data with specific errors | [ ] | — | — | — |
| FR-DATA-8 | Validate rate-wise vs mean-wise consistency | [ ] | — | — | — |
| FR-DATA-9 | N-stage generic loader (refactor plan) | [ ] | — | — | — |
| FR-DATA-10 | Optional per-server columns (graceful degradation) | [ ] | — | — | — |

## Functional Requirements — Simulation

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| FR-SIM-1 | 3-stage serial network (1/2/3 servers) | [ ] | — | — | — |
| FR-SIM-2 | DES with FEL; 4 event types | [ ] | — | — | — |
| FR-SIM-3 | RNG per selected distributions | [ ] | — | — | — |
| FR-SIM-4 | p_exit routing after Screening | [ ] | — | — | — |
| FR-SIM-5 | Arrival window 8:15 → cap/11:00 | [ ] | — | — | — |
| FR-SIM-6 | Services continue past close | [ ] | — | — | — |
| FR-SIM-7 | Day ends when cap served | [ ] | — | — | — |
| FR-SIM-8 | Skip Fri/Sun | [ ] | — | — | — |
| FR-SIM-9 | Single-day / multi-day modes | [ ] | — | — | — |
| FR-SIM-10 | Internal minutes; UI shows clock time | [ ] | — | — | — |

## Functional Requirements — Statistics & Validation

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| FR-STAT-1 | Chi-square on inter-arrival + service | [ ] | — | — | — |
| FR-STAT-2 | α default 0.05, user-selectable | [ ] | — | — | — |
| FR-STAT-3 | Auto bin count | [ ] | — | — | — |
| FR-STAT-4 | Display O, E, χ², df, p, decision | [ ] | — | — | — |
| FR-STAT-5 | Auto compare vs analytical M/M/c | [ ] | — | — | — |
| FR-STAT-6 | Display per-stage ρ (bottleneck) | [ ] | — | — | — |
| FR-STAT-7 | Per-server util + imbalance flag (>0.15) | [ ] | — | — | — |
| FR-STAT-8 | Histogram + fitted PDF overlay | [ ] | — | — | — |
| FR-VAL-1 | Refuse run if any ρᵢ ≥ 1 | [ ] | — | — | — |
| FR-VAL-2 | Assert 0 ≤ utilisation ≤ 1 | [ ] | — | — | — |
| FR-VAL-3 | Random seed (default 42, logged) | [ ] | — | — | — |
| FR-VAL-4 | Event log with all state + RNG draws | [ ] | — | — | — |
| FR-VAL-5 | (Stretch) N replications + CI | [ ] | — | — | — |

## Functional Requirements — Token Generator

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| FR-TOKEN-1 | Designed for from day 1, built last | [ ] | — | — | — |
| FR-TOKEN-2 | Sequential token + estimated wait | [ ] | — | — | — |
| FR-TOKEN-3 | Separate tab with UI flourish | [ ] | — | — | — |

## Non-Functional Requirements

| ID | Requirement | Status | Source | Test | Decision |
|----|-------------|--------|--------|------|----------|
| NFR-1 | Modular projects (Core/Data/App/Cli/Tests) | [ ] | — | — | — |
| NFR-2 | XML doc comments on public APIs | [ ] | — | — | — |
| NFR-3 | 4,000 patients / 30 days in <3s | [ ] | — | — | — |
| NFR-4 | Deterministic given seed | [ ] | — | — | — |
| NFR-5 | C# .NET 8, Avalonia, MathNet, ClosedXML, CsvHelper | [ ] | .gitignore, DEV_LAUNCH.md §3, appsettings.template.json | — | D-027 |
| NFR-6 | Charts <500ms, non-blocking UI | [ ] | — | — | — |

---

## Coverage Summary

- Total requirements: 46
- `[x]` DONE: 0
- `[~]` IN PROGRESS: 0
- `[ ]` TODO: 46
- `[?]` BLOCKED: 0
- `[-]` CANCELLED: 0

**Coverage:** 0%

---

## Update Protocol

Update this matrix in the SAME session as:
- Any PRD.md change (add/remove/modify requirement ID).
- Any requirement status change.
- Any source file implementing a requirement.
- Any test covering a requirement.
- Any decision logged in DECISIONS.md that shaped a requirement.

Run `scripts/verify-traceability.sh` (to be created) before each
milestone close to catch orphans — requirements with status `[x]`
but no source or test.

---

## Changelog

| Date | Change |
|------|--------|
| 2026-09-13 | Initial matrix created from PRD v1.3.0 |
| 2026-09-13 | Bidirectional rule (AGENTS §9.7) applied — matrix audited against PRD v1.3.0; zero orphan rows; D-025 logged |