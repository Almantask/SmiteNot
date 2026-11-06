---
name: adr
description: Write an architecture decision record in docs/adr/ covering context, options, decision, and consequences for decisions that are expensive to reverse. Use when invoked via /adr or when asked to record, propose, or supersede an ADR.
---

# Architecture Decision Record (ADR)

## Inputs

Extract `<decision title>` from the prompt. If no decision topic is clear from the prompt or conversation context, ask for it before creating a file.

## Workflow

1. **Read existing ADRs.** Use `read_file` to inspect `docs/adr/README.md` (index and template) and any existing ADR in `docs/adr/` that this decision touches or supersedes.
2. **Assign the next number.** Pick the next free `NNNN` after the highest existing ADR in `docs/adr/`. Target path:
   ```text
   docs/adr/NNNN-<kebab-title>.md
   ```
3. **Gather context.** Collect constraints and measurements from the conversation, `docs/implementation-plan.md`, and relevant benchmarks or profiles. Use `ask_question` (or ask directly) for missing facts rather than inventing numbers or rationale.
4. **Draft the ADR.** Follow the template in `docs/adr/README.md` and write the new file with `write_file`:
   - **Context:** problem, constraints, and measured numbers.
   - **Options:** two to four realistic alternatives, each with its main benefit and cost.
   - **Decision:** stated concretely enough to be verified in code or tests.
   - **Consequences:** what becomes easier, harder, enforced, or tested.
5. **Update the index.** Set status to `Proposed` unless explicitly told the decision is settled (`Accepted`). Add the new row to the table in `docs/adr/README.md` with `edit_file`.
6. **Handle superseding.** When superseding an earlier ADR:
   - Update only the old ADR's status line (`Superseded by NNNN`) and leave the rest of its text unchanged.
   - Mark the old row `Superseded by NNNN` in `docs/adr/README.md`.
   - Update `docs/implementation-plan.md` wherever it described the superseded decision.

## Verification

Confirm the new `docs/adr/NNNN-<kebab-title>.md` exists, matches the template headings in `docs/adr/README.md`, and appears in the index table in numeric order.
