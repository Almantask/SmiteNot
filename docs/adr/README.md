# Architecture decision records

One file per decision that is expensive to reverse (engine, Burst or not, backend, file formats), numbered in order: `NNNN-short-title.md`. Write them with the `adr` skill. An ADR is never edited after it is accepted; a later ADR supersedes it and both say so.

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-engine-unity.md) | Unity 6 (URP) presents; an engine-free C# simulation decides | Accepted |

## Template

```markdown
# ADR NNNN: <decision in one line>

- **Status:** Proposed | Accepted | Superseded by NNNN
- **Date:** YYYY-MM-DD
- **Source:** the brief entry, plan section or milestone that raised it

## Context
What forces the decision: requirements, constraints, measurements.

## Options
Each option with its main cost and benefit.

## Decision
What we do, stated so it can be checked.

## Consequences
What becomes easier, what becomes harder, what we must now enforce or test.
```
