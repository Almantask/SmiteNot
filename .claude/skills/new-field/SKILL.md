---
name: new-field
description: Add a world field (water, temperature, fuel, wind, fog and so on) to the simulation, with storage, update order, interactions, GPU upload, shader sampling, tests and a budget check. Use when a power or system needs a field that doesn't exist yet.
argument-hint: "<field name>"
---

Field: $ARGUMENTS

Stub (M0): the checklist is final; fill in file paths as the field framework lands in M3.

1. **Justify it.** The field budget (content roadmap, rule 4): a new field ships only when at least three strong ideas need it. Name them, and check its milestone or chapter in docs/design/content-roadmap.md.
2. **Storage and units.** Integer grid in natural units (plan section 3.3 table); bytes per cell; memory at the largest map.
3. **Update order.** Its slot in the field step (water → wetness and mud → temperature → ice and snow → fuel and fire → wind plumes → fog and smoke); sub-rate; active-tile rules (when a tile wakes and sleeps). Changing the order bumps SimVersion.
4. **Interaction table rows** (data/rules/fields.json): what it reads, what it writes, strengths as integers.
5. **Units read it:** speed, cost field, hazard, morale; which movement classes care.
6. **Keyframes and hashing:** included in snapshots and StateHash.
7. **GPU upload** as an integer texture; the shader include that decodes it; its strategic-zoom warning form.
8. **Tests:** kernel unit tests, conservation or bound invariants, a scenario test per decision, the thread-count determinism test.
9. **Budget:** /perf-check with the field active on the largest test map.
