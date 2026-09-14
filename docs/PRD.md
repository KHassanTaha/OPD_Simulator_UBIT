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

**FR-UI-5 (Welcome / Landing Panel)**
On first launch, the right-hand results panel SHALL display a welcome card containing:
- University of Karachi logo (green variant)
- Department of Computer Science (UBIT) logo
- Course name and course code
- Group member names
- Professor name: Dr. Shaista Rais

The welcome card SHALL remain visible until the user clicks the "Start Calculation" button on the left configuration panel. On click, the card fades out (≤ 300 ms) and is replaced by the metrics/results view. The card is not shown again during the same session unless the app is restarted.

The card is defined in `Views/WelcomeCard.axaml` with its view model in `ViewModels/WelcomeCardViewModel.cs`. Course info, member names, and professor name come from a single `CourseInfo.cs` constants file so they can be updated in one place.

**FR-UI-6 (Searchable Dropdowns)**
Every dropdown SHALL support:
- Type-to-search filtering of options
- A visible "×" clear button that resets the selection to empty
- Keyboard navigation (Up/Down/Enter/Escape)
- A "no matches" empty-state message when filtering yields nothing

Applies to: distribution selectors, time horizon, run mode, start day, trace level, preset selector, and any future dropdown.

**FR-UI-7 (Disabled Field Treatment)**
Disabled inputs SHALL be visually dimmed (reduced opacity, muted text colour, disabled cursor). Each disabled field SHALL have a tooltip explaining WHY it is disabled and WHAT the user must do to enable it. Never leave a disabled field without an explanation.

**FR-UI-8 (Hover Tooltips)**
Every interactive control (buttons, dropdowns, numeric inputs, checkboxes, tabs) SHALL have a hover tooltip that states what it does in ≤ 120 characters. Tooltips appear after a 500 ms hover delay. Longer explanations (rationale, constraints) live in a separate "?" info icon next to the control.

**FR-UI-9 (Accessibility Feedback)**
When an action cannot be performed, the UI SHALL:
- Highlight every offending field per FR-UI-17.
- Show a summary banner near the action button, e.g.: "Cannot start: 3 fields need attention."
- Each inline error message states cause and remedy.
- Never fail silently, never only log to file.

**FR-UI-10 (Themed Dialogs and Toasts)**
All toasts, error dialogs, confirmation popups, and progress notifications SHALL use the application's visual theme (colours, fonts, corner radii, icons). No platform-native dialogs. Consistent placement, animation, and dismissal behaviour across all notifications.

**FR-UI-11 (Scrollable Configuration Panel)**
The left configuration panel SHALL be scrollable when content exceeds the viewport height. Scrollbar appears only when needed. The "Start Calculation" / "Run Simulation" action button SHALL remain pinned to the bottom of the panel and always visible, regardless of scroll position.

**FR-UI-12 (Collapsible Configuration Sections)**
When the configuration panel contains more than 4 logical sections, sections SHALL be collapsible. Collapsed state persists within a session. Section headers show a chevron and are keyboard-activatable (Space/Enter). A "Collapse All" / "Expand All" control is available at the top of the panel.

**FR-UI-13 (Clear All Selections)**
A "Clear All" button SHALL reset every configuration field to its default value in one action. Before clearing, show a confirmation dialog. The action is undoable within the current session via a subsequent "Undo" toast.

**FR-UI-14 (User-Selectable Results Panel Content)**
The user SHALL be able to choose which information appears in the right-hand results panel. Options include (at minimum):
- Metrics table (per-stage)
- Chi-square / goodness-of-fit results
- Event trace (M4 output)
- Data preview table (FR-UI-20)
- Charts (M6 output)
- Token generator output
- Export controls

Selection is via a settings icon or a "Customise View" button. Selections persist across sessions (stored in local app settings). Default view after first calculation: metrics table + chi-square results + event trace.

**FR-UI-15 (Full Tab Navigation)**
Every interactive control SHALL be reachable and operable using only the keyboard.
- Tab moves focus forward through the UI in a logical reading order (left-to-right, top-to-bottom within a section; sections in configuration order).
- Shift+Tab moves focus backward.
- The tab order follows the visual layout — no "focus traps" and no controls skipped.
- Focus indicator is always visible on the currently focused control, with a minimum contrast ratio of 3:1 against its background.
- Enter or Space activates buttons and toggles.
- Up/Down arrows navigate within dropdowns and lists.
- Escape closes dropdowns, modals, and popups, returning focus to the element that opened them.
- After a modal closes, focus returns to the control that triggered it.
- The welcome card (FR-UI-5) is dismissible via keyboard — Tab to "Start Calculation", Enter to activate.
- No functionality is mouse-only.

**FR-UI-16 (Labels and Placeholder Text)**
Every input field SHALL have:
- A visible, persistent label (not placeholder-as-label) placed adjacent to the field, describing what the field is for.
- Placeholder text inside the field that demonstrates the expected format or a realistic example, distinct from the label.
- Both label and placeholder meet contrast requirements.
- Numeric fields show units in the label or a suffix (e.g., "Arrival rate λ (per minute)").
- When a field has a default value, the default is visibly marked (e.g., "(default: 42)").
- If a field is required, the label carries a visible required marker and an accessible name that states the field is required.
- If a field is optional, the label says "(optional)".

Placeholders must not be the only source of the field's meaning. Screen readers and low-vision users must be able to identify a field's purpose from the label alone.

**FR-UI-17 (Invalid Field Highlighting)**
When the user attempts to start a calculation but one or more fields are missing, empty, or invalid, the UI SHALL:
- Highlight every offending field with a red border (≥ 2 px, minimum 3:1 contrast against adjacent background).
- Show an inline error message directly beneath or beside each offending field, stating what is wrong and what is expected.
- Move keyboard focus to the first offending field in tab order.
- Announce the error to screen readers via live-region semantics.
- Keep the error visible until the user corrects the field, at which point the red border clears immediately.
- Not start the calculation until every error is resolved.

Error highlighting MUST NOT rely on colour alone. Every red field also carries:
- A visible error icon (e.g., ⚠) inside or beside the field.
- The inline error message text.
- An accessible name update including the word "invalid".

This satisfies WCAG 1.4.1 (Use of Colour): colour is a redundant cue, not the only cue.

The same rule applies to dropdowns, numeric fields, file uploads, and any control that participates in validation.

Real-time behaviour: fields validate on blur (focus leaving the field) and on submit. Validating on every keystroke is discouraged for numeric fields because it produces false errors while the user is still typing. Dropdowns and file pickers validate on selection.

**FR-UI-18 (In-Program Guide)**
The application SHALL include an in-program guide accessible from a persistent "Help" or "?" control in the top bar and via the F1 key. The guide:
- Opens as a themed, resizable panel (side panel or overlay, not a separate window).
- Contains at minimum: Getting Started, Configuration Reference, Running a Simulation, Reading the Results, Presets, Common Errors, Glossary.
- Is searchable — type-to-filter across section titles and body text, with matches highlighted.
- Supports keyboard navigation (Tab between sections, Enter to expand, Escape to close, focus returns to opener).
- Loads content from an embedded resource file so the app is self-contained and works offline.
- Uses the same Theme.axaml colours and fonts as the rest of the UI — no separate visual style.
- Contains contextual deep links: a "?" icon next to any configuration field opens the guide directly at the relevant section.
- Is included in the packaged app (single-file publish).

The in-program guide renders the same source markdown as `USER_MANUAL.md` so the two cannot drift.

**FR-UI-19 (Preset Save / Load)**
The application SHALL allow users to save their current configuration as a named preset and reload it later — both from disk and from an in-program list.

Scope of a preset:
- All configuration fields: parameter mode, distributions, server counts, manual λ, time horizon, run mode, start day, daily cap, random seed, p_exit override, trace level.
- Path reference to the data file used (not the file's contents).
- UI preferences: which result widgets are visible, collapsed/expanded section state.

Preset storage:
- In-program preset list stored under the user's application data directory (Windows: `%APPDATA%\OpdSimulator\presets\`, Linux: `~/.config/OpdSimulator/presets/`), resolved via `Environment.GetFolderPath(SpecialFolder.ApplicationData)`.
- Each preset is a JSON file with a `schemaVersion` field.
- Preset name is user-supplied; must be unique; filename is sanitised.

Operations:
- Save current config as new preset (prompts for name).
- Overwrite existing preset (with confirmation).
- Load preset from in-program list (dropdown or manager dialog).
- Import preset from an arbitrary `.json` file on disk.
- Export preset to an arbitrary `.json` file on disk.
- Rename preset.
- Delete preset (with confirmation).
- Duplicate preset.

Startup behaviour:
- On launch, the application starts with empty fields and no preset loaded (see FR-UI-21).
- The user explicitly selects a preset to load its configuration.
- The Presets dropdown reflects the currently loaded preset, or `(none)` if none is loaded.

Handling file paths on load:
- If the referenced data file does not exist at the stored path, the preset loads but the data field is shown as empty with an inline message: "The file referenced by this preset was not found. Please reselect it." (FR-UI-9 + FR-UI-17.)
- Never silently fail to load.

**FR-UI-20 (Selected Data Preview Table)**
After the user uploads a data file, the application SHALL display a preview table of the loaded data so the user can verify the file was read correctly before running a simulation.

Required columns shown (at minimum):
- `arrival_time`
- One or more `<stage>_start` / `<stage>_end` pairs (as present)
- `departure_stage`

The table is a **verification surface, not a spreadsheet editor**. It intentionally supports a limited feature set:
- Column headers with the actual names from the file.
- Scrollable vertically and horizontally.
- Fixed header row while scrolling.
- Row numbers aligned with the source file's row numbering (1-based; header is row 0).
- Sortable by clicking a column header (ascending / descending / original order toggle).
- Cell text is selectable (Ctrl+C copies a cell or a range).
- Values formatted using the validator's display rules:
  - Times as parsed (HH:MM:SS if sub-minute precision present, else HH:MM).
  - `departure_stage` shown as its canonical value.
  - Empty cells shown as a dimmed em-dash (—).
- Rows flagged as invalid by the validator are highlighted per FR-UI-17 (red border + icon + tooltip with the specific reason).
- Pagination or virtualisation: only render what's visible. Must handle 10,000+ rows without lag (NFR-10).

**Explicitly NOT supported (out of scope for the preview):**
- Editing cells.
- Adding or deleting rows or columns.
- Formula evaluation.
- Multiple-sheet selection (only the first sheet is previewed).
- Charts or aggregates within the preview.
- Exporting the preview.

The preview lives in the results panel as a selectable widget (FR-UI-14) and appears automatically after a successful upload. It is not shown before a file is loaded.

When the file fails validation entirely (FR-DATA-7), the preview is replaced with the error summary, listing every problem per FR-UI-9.

**FR-UI-21 (Empty Startup; Explicit Preset Selection)**
On application launch, the configuration panel SHALL start with **empty/default fields**. No preset is auto-loaded, even if the user ran a simulation in a previous session.

Startup behaviour:
- Every input field, dropdown, and selector starts empty or at its factory default (e.g., random seed = 42).
- The Presets dropdown shows `(none)` as the current selection.
- The results panel shows the welcome card (FR-UI-5) until the user clicks "Start Calculation".
- No `_lastSession.json` auto-restore. The user chooses what to load.

To load a previously saved configuration, the user explicitly:
1. Opens the Presets dropdown, or
2. Uses Preset → Manage… (FR-UI-19), or
3. Uses Ctrl+O, or
4. Selects a preset from the Manage dialog.

Once loaded, the fields populate from the preset. The Presets dropdown then shows the loaded preset name.

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

**NFR-7 (Accessibility Baseline):**
The UI SHALL meet these accessibility baselines:
- Full keyboard navigation (Tab order, focus indicators, Escape to close modals, Enter/Space to activate)
- Minimum contrast ratio of 4.5:1 for normal text, 3:1 for large text (WCAG AA)
- All icons paired with text labels or accessible names
- Screen-reader-compatible automation properties on every control
- Respect user's OS-level reduced-motion preference

**NFR-8 (Consistency):**
A single theme resource file SHALL define all colours, fonts, spacing, and corner radii. No hardcoded style values anywhere in the UI code. Reusable controls (searchable dropdown, themed toast, collapsible section, validated field) SHALL be built once and used everywhere.

**NFR-9 (Preset Portability):**
A preset exported from one installation SHALL load on another installation of the same app version, on either Windows or Linux. Paths that do not resolve on the target machine produce a clear inline error (FR-UI-9), not a crash. The JSON schema carries a version field so future changes remain backward-compatible or produce a clear "incompatible preset" message.

**NFR-10 (Data Preview Performance):**
The data preview table SHALL remain responsive (scroll, sort, selection) with datasets up to 10,000 rows. Rendering must use virtualisation so only visible rows are materialised. Sorting 10,000 rows completes in under 200 ms. No UI thread block longer than 100 ms during any preview interaction.

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
| v1.4.0 | 2026-09-14 | New M5 UI/UX requirements: FR-UI-5..21 (welcome panel, searchable dropdowns, disabled-field treatment, tooltips, accessibility feedback, themed dialogs/toasts, scrollable+collapsible config panel, clear-all, customisable results panel, full tab navigation, labels+placeholders, invalid-field highlighting, in-program guide, presets, data preview table, empty startup) and NFR-7..10 (accessibility baseline, consistency, preset portability, preview performance). Decisions D-060..D-076. | §5.1, §6, §8 |