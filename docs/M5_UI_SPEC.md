# M5 UI Specification — Quick Reference

**Status:** Captured for M5; not yet implemented.
**Related docs:** PRD §5.1 (FR-UI-5..21), PRD §6 (NFR-7..10),
AGENTS §16–17, DECISIONS (M5 entries), TODO M5 block.

---

## Feature Map

| FR | Feature | Effort | Priority |
|----|---------|--------|----------|
| FR-UI-5 | Welcome panel with logos, course, members, professor | S | P0 |
| FR-UI-6 | Searchable dropdowns with clear button | M | P0 |
| FR-UI-7 | Disabled field explanation tooltips | S | P0 |
| FR-UI-8 | Hover tooltips on every control | S | P0 |
| FR-UI-9 | Accessibility feedback on blocked actions | S | P0 |
| FR-UI-10 | Themed dialogs and toasts | M | P0 |
| FR-UI-11 | Scrollable config panel + pinned action | S | P0 |
| FR-UI-12 | Collapsible config sections | S | P1 |
| FR-UI-13 | Clear All with undo | S | P1 |
| FR-UI-14 | User-selectable results widgets | M | P1 |
| FR-UI-15 | Full Tab navigation | M | P0 |
| FR-UI-16 | Labels + placeholders + units | S | P0 |
| FR-UI-17 | Red error highlighting with icon + text | M | P0 |
| FR-UI-18 | In-program guide (F1, searchable, deep links) | L | P1 |
| FR-UI-19 | Preset save / load / import / export | L | P1 |
| FR-UI-20 | Selected data preview table (read-only) | M | P0 |
| FR-UI-21 | Empty startup; explicit preset selection | S | P0 |

S = small, M = medium, L = large.

---

## Asset Inventory (before M5 starts)

| Asset | Source | Where it goes |
|-------|--------|---------------|
| UoK logo (green variant) | Request from department | `Assets/uok-logo-green.svg` |
| UBIT CS logo | Request from department | `Assets/ubit-cs-logo.svg` |
| Theme palette | Decide with team | `Theme.axaml` |
| Course info text | Confirm with group | `CourseInfo.cs` |
| Professor name | Dr. Shaista Rais | `CourseInfo.cs` |
| Member names | Confirm with group | `CourseInfo.cs` |

---

## Reusable Controls to Build First

Build these before any screen that uses them:

1. `ValidatedField.axaml` — every input goes through this
2. `SearchableDropdown.axaml` — every dropdown goes through this
3. `ThemedDialog.axaml` — every popup goes through this
4. `ThemedToast.axaml` — every notification goes through this
5. `CollapsibleSection.axaml` — every config group goes through this
6. `InfoIcon.axaml` — every "?" goes through this
7. `PinnedFooterBar.axaml` — the bottom bar goes through this
8. `DataPreviewTable.axaml` — every data file preview

---

## Keyboard Map

| Key | Action |
|-----|--------|
| Tab | Move focus forward |
| Shift+Tab | Move focus backward |
| Enter | Activate focused button / select item |
| Space | Toggle checkbox / expand section |
| Escape | Close dropdown / modal / guide |
| Up/Down | Navigate within dropdown / list |
| F1 | Open in-program guide |
| Ctrl+S | Save current config as preset |
| Ctrl+O | Load preset (opens manager) |
| Ctrl+Shift+C | Clear all selections (with confirm) |

---

## Error Message Contract

Every error message must answer:

1. **What is wrong?** (specific)
2. **What should it be?** (expected format / range)
3. **What can the user do?** (actionable)

Example — good:
> "Arrival rate λ must be greater than 0. You entered 0.
> Enter a positive number, e.g., 0.5."

Example — bad:
> "Invalid input."

---

## Preset File Location

| OS | Path |
|----|------|
| Linux | `~/.config/OpdSimulator/presets/` |
| Windows | `%APPDATA%\OpdSimulator\presets\` |
| macOS | `~/Library/Application Support/OpdSimulator/presets/` |

No `_lastSession.json` auto-restore — startup is empty by design
(FR-UI-21).

---

## Startup Sequence

1. All fields empty / factory defaults.
2. Presets dropdown shows `(none)`.
3. Welcome card visible in the results panel.
4. User either:
   - Fills fields manually, then clicks "Start Calculation", OR
   - Loads a preset (dropdown, Manage dialog, or Ctrl+O).

Welcome card dismisses on "Start Calculation" click only.

---

## Data Preview Contract

| Attribute | Value |
|-----------|-------|
| Read-only | Yes |
| Virtualised | Yes |
| Sortable | Yes (asc → desc → original) |
| Editable | No |
| Row highlight for invalid | Yes (FR-UI-17 treatment) |
| Empty state | Not shown before file load |
| Error state | Replaced by validation summary |
| Performance | 10k rows < 1s; sort < 200ms |

---

## Accessibility Acceptance Criteria

Before M5 can be marked `[x] DONE`:

- [ ] Every workflow completable with mouse unplugged
- [ ] Every input labelled (persistent label + placeholder + tooltip + accessible name)
- [ ] Every error shown with red + icon + text + screen-reader announcement
- [ ] Every disabled control explains why
- [ ] Guide opens with F1, closes with Escape
- [ ] Preset saves, reloads, imports, exports
- [ ] Startup is empty (no auto-restore)
- [ ] Preview renders 10k rows responsively
- [ ] Theme change in one file restyles the whole app