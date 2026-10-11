---
name: milestone
description: Show the current milestone, its open checklist items and recent work, and propose the next task and agent. Use at the start of a work session or when asked what's next.
disable-model-invocation: true
argument-hint: "[milestone number, e.g. 3 — defaults to the current one]"
allowed-tools: Bash(git log *), Bash(git status *), Bash(cat docs/milestones/STATUS.md), Read, Grep
---

Requested milestone (empty means the current one): $ARGUMENTS

## Status file
!`cat docs/milestones/STATUS.md`

## Recent commits
!`git log --oneline -15`

## Working tree
!`git status --short`

Then:
1. If a milestone number was requested and it isn't the current one, read its section in docs/implementation-plan.md (section 9, "### M<N>") and summarise its goal, build list, exit criteria, "Needs first" questions and spec pass; say what must finish before it can start. Stop there.
2. Otherwise summarise the current milestone and its exit criteria in three lines.
3. List the unchecked items. If recent commits seem to complete one, verify in the code and offer to tick it.
4. Propose the next item: the smallest unblocked item on the critical path. Give a branch name (m<N>/<short-name>), the agent(s), the skills and the tests to add.
5. If the item depends on an open question (docs/implementation-plan.md section 10), name it and the working default.
