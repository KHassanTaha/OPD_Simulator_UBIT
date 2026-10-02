# Product Requirements Document – OPD Clinic Queue Simulator

**Version:** v1.11.1
**Date:** 2026-09-14
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
- Data preview table (FR-UI-20)
- Charts (M6 output)
- Token generator output
- Export controls

Selection is via a settings icon or a "Customise View" button. Selections persist across sessions (stored in local app settings). Default view after first calculation: metrics table + chi-square results. The event trace is **not** one of these options: it is always shown (FR-UI-35).

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

**FR-UI-22 (Time Unit Selector)**
The Parameters section SHALL provide a searchable dropdown for the user to declare whether entered rates (λ, μ) or means (1/λ, 1/μ) are expressed per minute, per second, or per hour. The engine SHALL convert all values to minutes internally at parameter-build time. The chosen unit SHALL appear in any results-panel captions that display a rate.

**FR-UI-23 (Parameter Mode Relocation)**
The rate-wise / mean-wise parameter-mode selector SHALL live at the top of the Parameters section, adjacent to the time-unit selector and the manual λ/μ entries, so the user declares the interpretation and the unit together.

**FR-UI-24 (Horizon Duration, diagnostic mode only)** _(updated Phase 8O, D-174)_
The former time-span preset selector is **removed**. A calendar run's length SHALL come only from the Horizon **Days** field (`RunMode.GeneratorDays`), and a diagnostic trace's arrival window SHALL come only from a new **Duration** dropdown, **visible in diagnostic-trace mode alone**. The Duration options SHALL be, in order: `1 hour` (**default**), `15 minutes`, `Custom minutes…`. A free-text field SHALL back only the `Custom minutes…` option, SHALL validate on blur, and a calendar run SHALL NOT read or be blocked by it. The earlier behaviour — one dropdown driving both the minutes horizon and a generator-day count, with `1 week` meaning 6 open days and `1 month` meaning 26 — is withdrawn; it ran a five-day clinic for six days.

**FR-UI-27 (Stage-Identical Chart Colour)**
Every chart that plots a stage SHALL take that stage's colour from one shared palette keyed by **stage index**, so a stage is the same colour in the queue-length chart, the waiting-time histogram, the per-server utilisation chart and the stage legend. The palette SHALL be defined by theme resources and SHALL wrap (by hue shift) for runs with more stages than the palette defines, rather than repeating a colour. A stage index SHALL be the only input to the lookup, so a chart that reorders its series for legibility SHALL NOT change the colours.

**FR-UI-28 (Per-Server Utilisation Readability)**
The per-server utilisation widget SHALL satisfy all of:
- A **step** line chart wherever a quantity is piecewise constant (queue length over time), so no value is drawn that the engine did not record.
- A **fixed** vertical scale derived from the configuration (server counts), not from the values in the current run, so two runs are comparable and no bar is clipped.
- A **textual account of the imbalance**, in addition to the colour: a per-server detail list and a fixed caption naming the benchmark and the deviation rule.
- A **dynamic stage legend**, generated from the stages actually present in the result — never a hardcoded stage list.
- A **label on every category** of a categorical axis, verified against the rendered chart rather than the data behind it.

**FR-UI-30 (Observation Window and Dual Arrival-Rate Estimates)** _(new Phase 8O, D-172, D-173)_
The Input tab SHALL, whenever a data file is loaded:
- Derive an **observation window** from the file's optional `session_date` column: the count of **distinct** session dates falling on a clinic-open day, valued at **165 operating minutes** each (the clinic runs 08:15–11:00, Mon–Thu and Sat). A file with no `session_date` column SHALL be a single session. A file whose every session date falls on a closed day SHALL report no operating window and SHALL state that no operating sessions were detected.
- Offer a window preset selector: `Auto (from file)`, `1 day`, `3 days`, `1 week`, `2 weeks`, `1 month`, `Custom…`, resolving to 1, 3, 5, 10 and 20 **operating** sessions respectively. `Custom…` SHALL accept operating **hours** and SHALL report its window in hours and minutes.
- Show **both** arrival-rate estimates with their divisors named: **MLE λ** = `1 ÷ mean(within-session inter-arrival gaps)` and **window λ** = `total arrivals ÷ the selected window's operating minutes`. Neither SHALL be presented as a correction of the other, and the percentage divergence between them SHALL be stated with its direction.
- Let the user choose which estimate drives the run, defaulting to **MLE**, and SHALL apply that choice to the simulation. The window the user selects SHALL change the divisor the run uses; a calendar run SHALL NOT be blocked by an invalid diagnostic duration.
- The custom-hours input SHALL satisfy FR-UI-17 (inline cause-and-remedy message, error state, accessible name) via the shared `ValidatedField` control.

**FR-UI-31 (Recorded Server Count per Stage)** _(new Phase 8Q.1, D-178)_
The Input tab SHALL, whenever a data file is loaded, present one **server-count field per stage the file's own columns imply**, discovered by the existing `StagePairDetector` and ordered by `ClinicStageOrder.Flow`. The count SHALL default to **1**, SHALL be user-editable, SHALL satisfy FR-UI-16/FR-UI-17 through the shared `ValidatedField` control, and SHALL carry a unit. The field SHALL be labelled as **recorded during data collection** — it is an input the user supplies, not a value the file contains — and the UI SHALL state that no server-ID columns are present, so the count describes assumed simultaneous capacity rather than a measurement. Each field SHALL accept a value the section can compute a historical figure from.

**FR-UI-32 (Historical Stage Utilisation on the Input tab)** _(new Phase 8Q.1, D-178)_
The Input tab SHALL show, for each stage, the historical utilisation implied by the loaded file and the recorded server count: `sum(observed service minutes) ÷ (servers × operating minutes)`. Requirements:
- The divisor SHALL be the **observed** window (`operating sessions × 165` minutes) when the file carries a `session_date` column, and the **spanned** window (last service completion − first arrival) otherwise. The basis SHALL **always** be named on screen, never left to the reader to infer.
- Each row SHALL print the **division that produced the figure** — busy minutes, server count, and operating minutes as literal text beside the percentage — so the number is reproducible from the row alone.
- A figure **above 100 %** SHALL be shown **unclamped**, accompanied by a warning naming the remedy (raise the server count) or otherwise identifying the stage as inconsistent with its recorded capacity. Clamping to 100 % is forbidden.
- A figure at or below 100 % SHALL carry no warning.
- The section SHALL state that historical utilisation describes the **collection period** and is distinct from the utilisation a simulation run produces.

**FR-UI-33 (Use for simulation — one-time seeding)** _(new Phase 8Q.1, D-178)_
The Input tab's "Use for simulation" action SHALL copy the recorded server counts into the Stages configuration **once**, at the moment of the action. After that, edits on the Input tab SHALL NOT rewrite the Stages configuration, and the action's label SHALL state that it overwrites the Stages counts. The two SHALL remain separately displayed: the Input tab's counts describe the collection period, the Stages counts describe the run. *(Direction of travel is one-way by design — see D-178 ruling 6, which records the D-176 mirror defect this prevents.)*

**FR-UI-34 (Performance Measures section and stability bands)** _(new Phase 8Q.3, D-183; bands merged in Phase 8Q.4)_
The Results panel's per-stage performance group SHALL be headed **Performance Measures**, which names the measurement it contains. The group SHALL present, as one system-totals subsection and one per-stage table: **stage number (a `#` column, numbered from 1 in stage order)**, stage name, arrivals served, average wait, average queue length, utilisation, and **ρ (λ ÷ cμ)**. A stability subsection SHALL additionally state each stage's ρ with a plain-language verdict, under a single caption naming the **bottleneck stage** — the stage with the **highest ρ**, ties resolving to the earliest stage. The verdict SHALL be rendered as text, never as colour alone.

A stage's stability verdict SHALL be **green** when `ρ < 0.9`, **amber** ("near capacity") when `0.9 <= ρ < 1`, and **red** ("unstable") when `ρ >= 1`. The bands SHALL be half-open on the upper side, and the decision SHALL be produced by a single pure function with no UI dependency so every surface applies the identical rule. The verdict text SHALL be the primary signal and the colour a secondary one (AGENTS §16.9: red is never the only cue). The red band is a **defensive** state: the engine refuses an unstable network and the GUI gates Start (D-128), so the GUI cannot currently produce it.

**FR-UI-35 (Always-visible event trace)** _(new Phase 8Q.4, D-184)_
The Results panel's event trace SHALL be permanently visible after a run and SHALL NOT be offered as a toggleable widget in the Customise-results selector. It SHALL be pinned below the scrolling widget body rather than placed inside it, so that scrolling to any widget can never push the trace off screen — the trace is the artefact a viva is read from, and a trace you have to scroll to find is a trace that was not read. Its body SHALL scroll internally within a fixed maximum height, so a long trace cannot consume the panel and hide the widgets. A persisted preferences file written before this requirement that lists the trace among the selectable widgets SHALL continue to load, and the stale entry SHALL be ignored: it SHALL NOT restore a hidden trace and SHALL NOT reappear as a selectable widget.

**FR-UI-36 (Serial numbers and column alignment)** _(new Phase 8Q.5, D-187)_
Every **listing table** in the Results panel SHALL carry a serial-number column numbered from 1 in display order, contiguous and gap-free, so a row can be cited unambiguously in the viva ("stage 2", "verdict 3") without the reader counting rows. Numeric columns SHALL be **right-aligned** and text columns **left-aligned**, in the header as well as the body, so decimal points line up within a column and no text column inherits the numeric rule by accident.

A **listing table** is one with a header row and a shared column set across its rows. A **label:value list** — no header row, no shared columns, each row an independent pair — is NOT a listing table and SHALL NOT carry a serial number, because there is no fixed column set for the numbers to refer to; the alignment rule still applies to it. The Results panel's overview "System totals" listing and the calculations dialog are label:value lists.

The per-server detail listing SHALL be a listing table rather than pre-formatted text: each server's own utilisation, its contribution, and its stage utilisation SHALL occupy separate columns so the D-171 arithmetic (divide one by the server count, get the other) is checkable on screen rather than reconstructed from a sentence.

**FR-UI-29 (View Calculations)**
The Results panel SHALL offer a **View calculations** action that opens a themed dialog listing, as plain text, the derivations behind the run: run configuration and parameter provenance, the arrival process, the per-stage service processes (c, μ, capacity, mean service, family), utilisation and each server's **derived** busy time, flow balance (served, throughput, mean wait, mean queue, p_exit), and the per-stage result. The text SHALL be produced by a pure, unit-tested function of the result and its parameters, SHALL contain no per-network hardcoding, and SHALL be copyable to the clipboard. The dialog SHALL be a **dedicated resizable window** that declares its own sizing in its own XAML, with the footer outside the scrolling body — *superseding the earlier clause requiring reuse of the themed confirmation dialog, which D-165 reversed and D-169 further bounded: a 440-pixel message dialog cannot host a two-column layout, and a `StackPanel` root cannot keep a footer on screen.* Where the run was driven by a data file, the arrival block SHALL additionally report **which arrival-rate estimate the run used**, the value of both estimates, the observation window the file itself covers, the window the run actually used where that differs, and **the division that produces the window λ** (arrivals ÷ operating minutes) so the printed numbers are verifiable against each other on screen.

### 5.2 Data Handling

**FR-DATA-1:** Accept `.xlsx` (primary) or `.csv` (fallback).
**FR-DATA-2:** Required columns: `arrival_time`, `<stage>_start`, `<stage>_end`, `departure_stage`.
**FR-DATA-3:** Compute inter-arrival times from consecutive `arrival_time` values.
**FR-DATA-4:** Compute per-stage waiting and service times from the column triplets.
**FR-DATA-5:** Fit selected distributions via MLE.
**FR-DATA-6:** Compute `p_exit` from `departure_stage` column, over the **screened** population only. Rows with `departure_stage = Reception` trigger a **warning** and are **excluded** from the `p_exit` numerator and denominator (leaving at Reception = reneging; out of scope), and rows that reached a doctor **without a screening record** (direct-to-doctor, FR-DATA-14) are likewise excluded from the denominator — they never reached the screening exit decision. The denominator SHALL be reported as `ScreenedPatients`, never as "candidates". A file with no screened patients SHALL be rejected as `DataValidationException` rather than reported as `0/0`. _(Owner clarification, 2026-09-13; denominator amended 2026-10-03, D-179)_
**FR-DATA-7:** Reject dirty data (missing, non-numeric, unsorted, duplicate, inconsistent stage pairs). Report specific problems and request cleaned data.
**FR-DATA-8:** Validate that the uploaded data matches the declared mode (rate-wise vs. mean-wise). Warn on mismatch; do not silently reinterpret.
**FR-DATA-9:** Refactor plan: current format supports 1 stage. All logic is N-stage generic; adding columns later is a config change.
**FR-DATA-10 (Optional per-server columns for historical validation):** The uploaded file MAY include per-stage server-identity columns (`reception_server`, `screening_server`, `doctor_server`).
- If present → historical per-server utilisation is computed and validated.
- If absent → historical validation falls back to stage-level only.
- Missing columns do NOT trigger file rejection.

Note: This FR affects HISTORICAL validation only. SIMULATED per-server utilisation is always available (see FR-STAT-7). The imbalance flag (max−min > 0.15, FR-STAT-7) applies to historical per-server utilisation too when the columns are present.

**FR-DATA-12 (Optional `session_date` column):** The uploaded file MAY include a `session_date` column declaring which operating session each row belongs to. When present it SHALL be parsed in ISO date (`YYYY-MM-DD`), ISO datetime with a space or `T`, or `DD/MM/YYYY` form; an unparseable value SHALL be reported as a validation issue rather than guessed. Arrival-order validation SHALL reset at each session change. Rows dated on a clinic-closed day SHALL NOT be rejected — the file is historical and a Friday session is data, not an error — but SHALL be excluded from the observation window (FR-UI-30). The column SHALL be optional: a file without it SHALL behave exactly as it did before Phase 8O. _(new Phase 8O, D-172)_

**FR-DATA-13 (Two arrival-rate estimates):** The system SHALL compute and expose both the MLE arrival rate and the observation-window arrival rate as separate values on the data binding, and SHALL exclude cross-session gaps from the MLE sample. The MLE sample and the reported inter-arrival sample SHALL be the same set, so the fitted rate and the Input tab's chi-square verdict describe the same data. `SessionDates` SHALL hold **distinct** dates in first-appearance order, never one entry per row. The estimation and the window choice SHALL be recorded as a **choice** (`LambdaSource`), not as a number placed in the manual-λ field, so that a fitted value is never reported as a user-typed one. _(new Phase 8O, D-173)_

**FR-DATA-14 (Direct-to-doctor detection and the blank-cell rules it implies):** The system SHALL detect a direct-to-doctor visit as a row that reaches a doctor **without a screening record** — a blank `screening_start` on a row with `departure_stage = Doctor` — and SHALL expose `p_bypass = bypass rows ÷ all arrivals` on the data binding alongside the existing fitted parameters. Detection SHALL be gated on the `screening_start` **column** being present: a missing cell in a file that has the column means "not screened", while a missing column means a pre-bypass schema that cannot answer the question, and the legacy `p_exit` denominator SHALL be retained for such files. Validation SHALL forgive exactly three blank-stage patterns and no others (D-180): **Reception is never forgivable** (a row with no reception record never entered the clinic); a blank `doctor_*` cell on a Doctor departure **is** forgivable (incomplete `doctor_end` stamps lose nothing, the patient has already left); a blank `screening_*` cell on a Doctor departure is forgivable **only when a doctor record is present**. A row with neither a screening nor a doctor record on a Doctor departure SHALL still be rejected, so an incomplete capture cannot pass as real routing. A blank `screening_end` on a **Screening** departure SHALL still be rejected, because that timestamp is the service sample the stage's fitted μ is computed from. _(new Phase 8Q.2, D-179, D-180)_

### 5.3 Simulation Model

**FR-SIM-1:** Model as a 3-stage serial network (defaults: 1, 2, 3 servers). The engine is **N-stage generic from day one** — the 3-stage network is a configuration, not a hard-coded structure. _(Owner clarification, 2026-09-13)_
**FR-SIM-2:** DES with FEL. Events: Arrival, Reception End, Screening End, Doctor End.
**FR-SIM-3:** Inter-arrival times from selected distribution; service times per stage from selected distribution (shared initially; per-stage enabled later).
**FR-SIM-4:** `p_exit` applied after Screening. Estimated from data if available; otherwise user-configurable (default 0.5).
**FR-SIM-5:** Arrival generation window: 8:15 AM onward (configurable), until daily cap reached or 11:00 AM, whichever first — 165 operating minutes per session. _(Clock corrected 2026-09-29, D-172; was documented as 9:00 AM.)_
**FR-SIM-6:** Services in progress at close time continue to completion.
**FR-SIM-7:** Day ends when all capped patients are served.
**FR-SIM-8:** Closed days (Fri, Sun) skipped.
**FR-SIM-9:** Single-day and multi-day modes user-selectable.
**FR-SIM-10:** Internal clock in minutes (t=0 = arrival start); UI displays real clock time.

**FR-SIM-11 (Direct-to-doctor routing):** The network SHALL carry a `BypassProbability` in `[0, 1)` plus a bypass source stage and destination stage, both constructor-validated; a destination that is not strictly after its source SHALL be rejected, and a bypass source equal to the exit stage SHALL be rejected because two coin tosses on one completion leave the destination dependent on which was consulted first. When `BypassProbability == 0` both indices SHALL normalise to −1, the bypass SHALL be disabled, and **no additional random draw SHALL be consumed**, so that every pre-existing run is bit-identical. On a service completion the exit decision SHALL be taken first and the bypass decision second; the bypass SHALL apply only when the patient did not exit, and the `EndService` trace row SHALL be emitted **after** both decisions so the recorded destination is the one that occurred. Each stage's effective arrival rate SHALL be the **sum of the routed probability mass reaching that stage**, so a stage receives arrivals both from patients that passed through it and from those that skipped it; with bypass disabled this reduces to the original single-exit product (D-007 amended). A network with fewer than three stages SHALL run with the bypass normalised to zero, and the reported effective bypass probability SHALL then be zero rather than the configured value (D-182). _(new Phase 8Q.2, D-179, D-182)_

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
- **Rendering (Phase 8M, D-160).** The per-server utilisation widget draws each server's **contribution** to its stage's utilisation — `busy ÷ (server count × operating time)` — so the bars of a stage sum to exactly the stage utilisation reported alongside them. The widget SHALL draw one column series per stage (not per server), mark a deviating server with an amber marker on the same bar, draw a dashed **equal-share** reference line at `stage utilisation ÷ server count`, and fix the vertical axis at `1 ÷ (smallest server count in the run)`. Amber SHALL mean `|server utilisation − stage utilisation| > 0.15` and SHALL be accompanied by a textual cue, never colour alone.
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
| v1.5.0 | 2026-09-18 | New FR-UI-22/23/24 for time unit, parameter mode, and time-span presets (Phase 7A). FR-UI-22 marked `[~]` pending results-caption unit display. | §5.1, §6 |
| v1.11.1 | 2026-10-03 | **[FIXED] FR-UI-17 in the data preview** — `PreviewRow` has computed `RowBackground`, `RowBorderBrush` and `RowBorderThickness` since D-080, but the `DataPreviewTable` `DataTemplate` bound none of them, so a row the validator rejected rendered identically to a good one: **no red border, no icon, no reason**. Previous state: the computed values were discarded at render time and FR-UI-20's "invalid rows inherit the FR-UI-17 error treatment" promise was unmet while FR-UI-20 nonetheless read `[x]`. New state: the three values are bound on the row's `Border`, an inline issue icon appears in a fixed-width leading gutter and carries the validator's specific reason as its tooltip and accessible name, and the row itself is hoverable for the same reason. Three rendering tests (`DataPreviewInvalidRowTests`) assert the treatment on the realised elements, and each fails against the pre-fix template. **Serial header unified** — the three tables added in 8Q.5 read `No.` while the 8Q.3 per-stage table read `#`; owner ruling 2026-10-03 is `#` everywhere. Phase 8Q.6, D-188. | §5.1 |
| v1.11.0 | 2026-10-03 | **FR-UI-36 added** — every listing table in the Results panel carries a contiguous serial-number column; numeric columns are right-aligned and text columns left-aligned, in headers and bodies alike; a label:value list is explicitly *not* a listing table and carries no serial number; the per-server detail listing becomes a real table with separate utilisation, contribution and stage-utilisation columns instead of pre-formatted monospace strings. Phase 8Q.5, D-187. | §5.1 |
| v1.10.0 | 2026-10-03 | **FR-UI-35 added** — the event trace is permanently visible after a run, pinned below the scrolling widget body rather than placed inside it, scrolls internally within a fixed maximum height, is absent from the Customise-results selector, and a pre-existing preferences file that lists it loads with the stale entry ignored. **FR-UI-34 amended** — the Stability bands text, previously numbered FR-UI-35, is merged into FR-UI-34 verbatim; the number is reused for the trace so the two related performance requirements stay together. **FR-UI-14 amended** — "Event trace (M4 output)" removed from the toggleable options list, because FR-UI-35 makes it permanently visible; the default-view sentence corrected with it. Phase 8Q.4, D-184. | §5.1 |
| v1.9.0 | 2026-10-03 | **FR-UI-34 added** — Performance Measures section: system totals, per-stage table with a `#` serial column, and a stability subsection with the bottleneck named. **FR-UI-35 added** — stability bands green `< 0.9` / amber `[0.9, 1)` / red `>= 1`, decided by one pure Core function. Phase 8Q.3, D-183. The always-visible trace (referred to as FR-UI-34 in `TODO.md` prose but **never given a PRD requirement**, so this number is free) is **deferred to 8Q.4** and deliberately NOT recorded here, so no requirement describes behaviour the code does not yet have. | §5.1 |
| v1.8.0 | 2026-10-03 | **FR-DATA-14 added** — direct-to-doctor detection, `p_bypass = bypass rows ÷ all arrivals`, and the three blank-cell forgiveness rules it implies. **FR-SIM-11 added** — direct-to-doctor routing: `BypassProbability` + validated stage indices, exit draw before bypass draw, no RNG draw when disabled, `EndService` emitted after both decisions, and per-stage inflow as a sum over routes. **FR-DATA-6 amended** — the `p_exit` denominator is the screened population, reported as `ScreenedPatients`; a file with no screened patients is rejected. Phase 8Q.2, D-179, D-180, D-181, D-182. | §5.2, §5.3 |
| v1.7.0 | 2026-10-03 | **FR-UI-31 added** — per-stage recorded server count on the Input tab. **FR-UI-32 added** — historical stage utilisation with a named observed/spanned divisor, a printed division, and an unclamped above-100 % warning. **FR-UI-33 added** — one-time "Use for simulation" seeding of the Stages counts. Phase 8Q.1, D-178. FR-DATA-6 (p_exit) and the bypass/denominator rulings are **deferred to 8Q.2** and are deliberately NOT recorded here, so no requirement describes behaviour the code does not yet have. | §5.1 |
| v1.6.0 | 2026-09-29 | **FR-UI-24 rewritten** — time-span presets removed, diagnostic-only Duration dropdown (D-174). **FR-UI-30 added** — observation window and dual λ estimates (D-172, D-173). **FR-DATA-12 added** — optional `session_date` column. **FR-DATA-13 added** — two arrival-rate estimates. **FR-SIM-5 clock corrected** to 8:15 AM. **FR-DATA-11 removed** — never implemented and superseded by FR-DATA-12/13. | §5.1, §5.2, §5.3 | **FR-UI-29 corrected** — the clause requiring reuse of the themed confirmation dialog had been superseded by D-165 (dedicated resizable window) and D-169, and was never updated here, so the source of truth contradicted both the code and the decision log; the arrival block now also requires the run's own estimate and a checkable division (D-176).