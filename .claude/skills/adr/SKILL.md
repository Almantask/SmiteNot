---
name: adr
description: Write an architecture decision record in docs/adr/ (context, options, decision, consequences) for a decision that is expensive to reverse.
disable-model-invocation: true
argument-hint: "<decision title>"
---

Decision: $ARGUMENTS

1. Read docs/adr/README.md (index and template) and any ADR this one touches or supersedes.
2. Number it: the next free `NNNN` after the highest existing ADR. File: `docs/adr/NNNN-<kebab-title>.md`.
3. Gather the context from the conversation, docs/implementation-plan.md and measurements (benchmarks, profiles). Ask for anything missing rather than inventing it.
4. Fill the template: context; two to four real options, each with its main cost and benefit; the decision, stated so it can be checked; consequences (what is now easier, harder, enforced or tested).
5. Status `Proposed` unless the user says it is decided. Add the row to the index in docs/adr/README.md.
6. If it supersedes an ADR, mark the old one "Superseded by NNNN" in the index (the old file's text stays as it was, apart from its status line), and update docs/implementation-plan.md where it described the old decision.
