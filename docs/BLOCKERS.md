# BLOCKERS.md — Active Blockers

Anything preventing progress, with owner and needed action. A task in
`TODO.md` marked `[?] BLOCKED` must have an entry here.

## Active

- **B-001:** Confirm daily patient cap with clinic management.
  - Owner: Taha
  - Impact: Affects default config in UI (maps to PRD OQ-1)
  - Status: Pending

- **B-002:** Confirm 8:15 AM arrival start with real data.
  - Owner: Taha
  - Impact: t=0 anchor (maps to PRD OQ-2)
  - Status: Pending

- **B-003:** Confirm "first distribution" = inter-arrival interpretation with professor.
  - Owner: Taha
  - Impact: UI labeling (maps to PRD OQ-3)
  - Status: Pending

- **B-004:** Sample patient data file (`samples/sample_patients.xlsx`) not yet received.
  - Owner: Taha
  - Impact: Blocks Milestone 2 demo path, loader integration tests, and DEV_LAUNCH §7 verification; CONTEXT §5.5 demo data format documented
  - Needed action: Provide the real sample file (or confirm I should generate a stand-in)
  - Status: Pending

- **B-005:** Avalonia MVVM template not installed (`dotnet new install Avalonia.Templates` not run).
  - Owner: Taha
  - Impact: `OpdSimulator.App` created as an empty classlib placeholder this session (per scaffold instructions); Avalonia packages referenced but App not runnable until M5. DEV_LAUNCH §5 already documents running against `OpdSimulator.Cli` until then.
  - Needed action: Approve `dotnet new install Avalonia.Templates` at M5 (or OK to hand-build the Avalonia project without the template).
  - Status: Pending

## Resolved

- (none yet)