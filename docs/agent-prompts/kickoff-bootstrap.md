Simulator Start Prompt

RESUME SESSION

Before doing anything else, perform a cold-start reconciliation:

1. Read these files in this order:
   - AGENTS.md (§9 State Persistence, §13 Wrap-Up Format)
   - docs/TODO.md
   - docs/PROGRESS.md (top 3 entries)
   - docs/BLOCKERS.md
   - docs/DECISIONS.md (last 5 entries)
   - DEV_LAUNCH.md (Last verified date)

2. Run these commands and report the output:
   - git status
   - git log --oneline -5
   - git branch --show-current
   - ls logs/ (if it exists)

3. Reconcile the recorded state against reality:
   - Any [~] IN PROGRESS task from a previous session that has no
     corresponding [x] DONE?  Report it and ask if I want to
     resume, reset to [ ], or cancel it.
   - Any uncommitted changes in the working tree?  List the files.
     Do not commit them without my approval.
   - Any crash log newer than the last PROGRESS.md entry?  Read the
     top 20 lines and quote the exception.
   - Does the recorded "Last verified" date in DEV_LAUNCH.md match
     the latest commit?  If not, flag it.

4. Then produce a 6-line state summary using this exact format:

   Branch:        <name>
   Last commit:   <hash + message + time>
   Last task:     <last [x] DONE from TODO.md>
   In progress:   <current [~] or "None">
   Next task:     <top of Upcoming in TODO.md>
   Blocked:       <count from BLOCKERS.md or "None">

5. Wait for my confirmation before starting any new work.

Do NOT assume.  Do NOT start coding.  Do NOT commit.  Reconcile first.



--------------------------------------------------------------------------------



Workflow Cheat Sheet (Keep This by Your Laptop)
Moment	What You Type
Starting fresh	"RESUME SESSION. Perform AGENTS.md §14."
Mid-session break	"Save state: update TODO.md, PROGRESS.md, BLOCKERS.md, then produce a Session Handoff."
Ending cleanly	"Wrap up. Produce a Session Handoff, commit, and push."
Emergency exit	Just close the terminal. Next session's reconciliation catches the rest.
After a crash	"RESUME SESSION. Check logs/crash-.log first, then reconcile."*




