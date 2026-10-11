---
name: new-power
description: Add a god power as data plus the minimum code, with range-rule checks, timeline and preview support, counters and scenario tests. Use when implementing any power from docs/design/power-palette.md.
argument-hint: "<power name>"
---

Power: $ARGUMENTS

1. Read its entry in docs/design/power-palette.md, its wave in docs/design/content-roadmap.md, and every brief decision it touches. If the palette marks it "seed" or "open", or its systems don't exist yet at this milestone, ask the design-lead agent before building.
2. Write the consequence chain (power → world change → army behaviour → outcome) in the PR description; two or three hops.
3. Data (data/rules/powers.json): id, area shape, wind-up, duration, cost, fields written and amounts, mode-specific tuning (campaign vs PvP). Integers in documented units only.
4. Code only if a new field operation is needed; otherwise the generic cast executor applies the data. The range rule is checked at the firing tick against regiment outlines (100 m), with a fizzle reason.
5. Preview: its timeline bar (wind-up to peak), area placement with the mouse, overlays the player needs to read it.
6. Counters: add or check the field interactions that make it counter and be countered (palette counter-web table). Every god tool ships with a unit-side answer (content roadmap, rule 7).
7. Tests: a scenario test per brief decision it touches, one per counter-web row it appears in, and a range-rule fizzle test.
8. Update power-palette.md through the game-design skill if the build settled anything ("seed" → settled detail).
