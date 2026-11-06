---
name: milestone
description: Check the current milestone, inspect open checklist items and recent commits, and propose the next critical-path task. Use when starting a work session, when asked what to work on next, or when invoked via /milestone.
---

# Milestone Status & Next Task

## Inputs

Extract an optional milestone number `<N>` (for example, `3` or `M3`) from the prompt. Empty defaults to the active milestone in `docs/milestones/STATUS.md`.

## Gather Live Context

Run these checks in parallel first (Antigravity does not auto-expand shell snippets inside markdown):

1. Read `docs/milestones/STATUS.md` with `read_file`.
2. Inspect recent commits and working-tree state with `run_command`:
   ```powershell
   git log --oneline -15
   git status --short
   ```

## Workflow

1. **Specific non-current milestone requested:** Read `### M<N>` in `docs/implementation-plan.md` (section 9) with `read_file`. Summarise its goal, build list, exit criteria, "Needs first" questions, and spec pass, plus what prerequisites must finish before it starts. Stop there.
2. **Current milestone summary:** Otherwise, summarise the active milestone and its exit criteria in at most three lines.
3. **Checklist audit:** List all unchecked (`- [ ]`) checklist items. If recent commits or working-tree files appear to finish an unchecked item, verify in the codebase and offer to tick it in `docs/milestones/STATUS.md`.
4. **Propose the next task:** Recommend the smallest unblocked item on the critical path:
   - Specialist role(s) (`sim-engineer`, `determinism-auditor`, `ai-engineer`, `perf-engineer`, `unity-engineer`, `tech-artist`, `test-engineer`, `design-lead`, `level-designer`, `code-reviewer`)
   - Relevant workspace workflows (`/new-field`, `/new-power`, `/new-terrain-piece`, `/new-unit-type`, `/scenario-test`, `/perf-check`, `/golden-replay`, `/unity-tests`)
   - Concrete unit, scenario, and determinism tests to add
   - Reminder: commits happen directly on `main` (one checklist item per commit) and push straight to `origin/main`.
5. **Open questions check:** If the proposed item touches an open question in `docs/implementation-plan.md` section 10 or `docs/design/game-brief.md`, name the question and state the plan's working default.
