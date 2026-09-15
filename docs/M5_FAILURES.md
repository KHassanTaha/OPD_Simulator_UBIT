# M5_FAILURES.md — M5 GUI Audit Log

> **Status:** Retrospective reconstruction (2026-09-15) of the M5 GUI audit
> that justified feat/gui-rebuild. Reconstructed from session logs; accurate
> to the best of available evidence.

## 1. Purpose

Records the M5 GUI audit run against the merged Milestone 5 view layer (merge
`71a816a`): the crashes, the owner-reported defects, the root causes, and why
the view layer was **rebuilt from scratch** (`feat/gui-rebuild`) instead of
patched. This is the viva evidence file for the rebuild mandate.

## 2. What was audited and when

- **Audited code:** M5 GUI view layer (`src/OpdSimulator.App`) as merged to
  `main` at `71a816a`, built 2026-09-14/15.
- **Auditor:** Taha (owner) on the running app; agent reproduced and
  categorised.
- **Evidence:** ~20 crash logs (`logs/crash-*.log`), a 27-item owner defect
  report, in-app reproductions, headless test runs.
- **Relation to B-007:** this audit surfaced the defects the B-007 keyboard
  acceptance pass is designed to catch.

## 3. The two crash root causes

### 3.1 Crash 1 — DataPreviewTable NullReferenceException
- **Symptom:** loading a data file and opening Data Preview crashed the window
  (main-thread NRE; window unusable in some repros).
- **Root cause:** `DataPreviewTable` resolved a table configuration
  (ProjectConfig-style columns dictionary) that was `null` for the active
  out-of-box configuration; the `Columns` access NRE'd instead of falling back
  to a safe default.
- **Disposition:** deleted with the fix branch; rebuilt with a null-config
  fallback and a virtualised, read-only, header-from-file model (G2).

### 3.2 Crash 2 — p_exit = 1 boundary violation
- **Symptom:** typing `1` into the *p_exit override* field crashed the app.
- **Root cause:** GUI validated p_exit on the **closed** interval `[0.0, 1.0]`,
  but Core `NetworkTopology` requires the **half-open** `[0.0, 1.0)` (p_exit =
  1 means the downstream stage can never receive a patient — degenerate
  topology). Boundary input passed GUI validation and violated the Core
  invariant at run time. The topology was constructed **outside** the
  `try` catching `UnstableSystemException`, so the exception was unhandled and
  took the process down.
- **Note:** GUI manifestation of the domain rule "all rows exit after
  Screening ⇒ fitted p_exit = 1.0 ⇒ clean refusal, never a crash."

## 4. FR-UI rows falsely marked [x]

Per AGENTS §18, `[x]` means *verified in the running app*. These were `[x]` on
"code exists + unit tests pass" and failed audit:

| FR | Claimed | Why the [x] was false |
|----|---------|------------------------|
| FR-UI-5 | [x] | Welcome card existed but layout/sizing non-conformant; unverified visually. |
| FR-UI-8 | [x] | Tooltips required on *every* control; many had none. Acceptance was pending. |
| FR-UI-12 | [x] | Collapsible behaviour untested in-app; visual acceptance pending. |
| FR-UI-14 | [x] | Widget Customise modal clipped at real window sizes (owner-reported). |
| FR-UI-18 | [x] | F1 opened the guide but focus never reached its search box. |
| FR-UI-19 | [x] | Store logic green, but in-app preset dropdown/persistence UX unverified. |
| FR-UI-20 | [x] | **Crashed on first real use** (Crash 1) — objectively false. |

Sticking at `[~]` with audit-confirmed gaps: FR-UI-9 (refused-run feedback,
§5-A), FR-UI-15/17 (keyboard pass B-007, live-region).

**Verdict:** M5 `[x]` meant "statically exists", not "verified". Phase 8 of
the rebuild re-derives REQUIREMENTS.md statuses using §18 entries + screenshots
only.

## 5. The 27 owner-reported items, clustered

Clustered from session logs of the owner's in-app report (per-item severity
and exact wording were on the deleted branch; see §8).

**A. Layout & sizing**
1. Banners not content-sized (spec: 32–96 px, content-sized, internal scroll).
2. Result-widget Customise modal clipped on smaller windows.
3. Fixed-width preview/table columns broke layout at larger widths.
4. ThemedDialog sizing/vertical-scroll inconsistent.
5. ScrollViewer coverage gaps on narrow windows.

**B. Labels, placeholders, validation**
6. Three fields had no persistent label — server counts for Reception,
   Screening, Doctor (fixed in the fix session by adding labels).
7. `p_exit = 1` accepted → crash (Crash 2).
8. Empty p_exit with empty arrival rate exercised an unvalidated-before-run path.
9. Refused-run summary said "N fields need attention" but visible red fields
   counted differently.
10. Inline-error styling inconsistent; no live-region announcements (FR-UI-17).

**C. Focus, keyboard & guide**
11. F1 opened the guide; focus did not move to its search box.
12. Escape + focus-return across dropdowns/modals undependable (B-007).
13. Whole-window Tab-order audit never run (B-007).

**D. Preview & data**
14. Data Preview NRE on first load (Crash 1).
15. Preview "recommended columns" hint inconsistent with spec-columns demo file.
16. 10k-row render/scroll contract (NFR-10) unverified.

**E. Toasts, dialogs, theme**
17. Warm non-fatal toast measure/reflow console noise during recording.
18. Theme values scattered across ControlStyles, not a single resource file.

**F. Stability & crash-log noise**
19. ~20 crash logs across the M5 build; visible 05:01:38 `TaskScheduler`
    unobserved DBus `com.canonical.AppMenu.Registrar` (platform noise, but
    must be explainable in the viva).
20. GUI-thread exceptions reached the process instead of the §12.3 handlers.

**G. Run flow & results trust**
21. Persisted widget-visibility prefs applied with no in-app visibility test.
22. Pinned-bar buttons inconsistent (Start primary, others secondary) style.
23. Metrics/Chi-square widgets not cross-checked against CLI output on same
    parameters during audit.
24. Token widget = placeholder summary only (count + avg wait; no ticket card,
    no Little's Law estimate).

**H. Startup & presets**
25. "(none)" dropdown + no auto-restore correct by design; never verified in-app.
26. Save-Preset overwrite prompt never demoed.
27. Info-icon HelpAnchor deep links not wired to guide anchors.

## 6. Root-cause groups the rebuild must fix fresh

| Group | Defect | Required fix |
|-------|--------|--------------|
| **G3** | p_exit accepted on closed `[0,1]`; Core invariant `[0,1)` | Field validates `[0,1)`; p_exit ≥ 1 refused with the exact Core message; degenerate topologies never reach the engine; p_exit hidden for 1-stage models. |
| **G4** | `NetworkTopology` constructed outside the UnstableSystemException try | Topology built **inside** the coordinator try; every Core rejection returns a refused-run outcome surfaced as an ErrorBanner — never a crash. |

These proved patching was insufficient and are re-implemented from scratch in
the rebuild, with acceptance tests rebuilt alongside.

## 7. Reproduction tests deleted with the fix branch

Authored on `fix/m5-gui-functionality`, destroyed with the branch. Names for
the record:

- `tests/OpdSimulator.App.Tests/ValidatedFieldErrorRenderTests.cs` — field
  rules incl. p_exit = 1 rejection and red/icon/message state.
- `tests/OpdSimulator.App.Tests/MainWindowRunFlowTests.cs` — validation
  refusal, first-invalid focus, refused-run banner with exact Core message.
- `tests/OpdSimulator.App.Tests/ConfigViewModelTests.cs` — `TryBuildParameters`
  rejection paths incl. p_exit ≥ 1 and empty arrival.
- `tests/OpdSimulator.App.Tests/SimulationCoordinatorTests.cs` — refused runs
  (ρ ≥ 1, p_exit = 1 via data, unstable) returned as outcomes, never exceptions.

None remain on `main` (verified absent from App.Tests). The rebuild re-creates
this coverage against the new view models.

## 8. Reconstruction confidence

- **Verbatim-grounded:** §3, §4 (verified against REQUIREMENTS.md), §6, §7
  (file absence verified).
- **Approximately reconstructed:** per-item wording in §5; counts/severity
  best-effort from session logs. Phase 7 (B-007 keyboard + FR-UI manual pass)
  re-audits authoritatively and supersedes this file.

## 9. Outcome

The audit justified disposal of the M5 view layer. M6 chart/token work stays
parked on `feat/milestone-6-charts-and-token`. The view layer is rebuilt as
`feat/gui-rebuild` (Phases 1–8) with AGENTS §18 manual verification as the
definition of `[x]`.