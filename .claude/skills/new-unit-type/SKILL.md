---
name: new-unit-type
description: Add a unit type or specialist as data (movement class, load, launch response, AI rule, specialist action through the shared executor, charges, preview marker, art checklist, tests).
argument-hint: "<unit or specialist name>"
---

Unit: $ARGUMENTS

Stub (M0): the checklist is final; fill in file paths as units land in M2 and specialists in M5.

1. **Design:** its entry in docs/design/roster.md (spec S2/S4) and its wave in docs/design/content-roadmap.md. Specialists answer part of a god setup, never all of it (rule 7).
2. **Data** (data/rules/units.json or specialists.json): regiment size, mass and load, speed per movement class (at most four classes at launch), melee and volley values, charge and launch response, morale, swept and burn thresholds, price in bodies.
3. **Specialist action** (if any) through the shared executor: action as data, trigger rule, rate, charges on the regiment, seat lock, per-action reachability validation; charges are lost when the regiment falls.
4. **AI rule:** when it acts, how it is escorted and hunted (from M7), with hysteresis.
5. **Preview:** marker on the regiment lane; landing spreads if it launches or is launched.
6. **Art checklist:** rig, LODs and budgets, clips including work loops, team-colour areas, death gags per cause.
7. **Tests:** a scenario test per decision, the specialist's answer to its setup, conservation and determinism.
