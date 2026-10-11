---
paths:
  - "data/**"
---

# Rules data

- **No floats in data.** Numbers are integers in documented units (mm, mm/tick, centi-degrees, 0–65535 scales, cm/s, ticks at 20 per second), or decimal strings converted exactly at load. Every field's unit is in its schema (`data/schema/`).
- **Rules** (`data/rules/*.json`): unit types, movement classes (at most four at launch: foot, horse, wheeled, one water class), specialist actions, powers, terrain pieces, the field interaction table, projectile tables, budgets. Changing any of them changes the ruleset hash, which replays record and PvP checks at lock-in, and usually the goldens (/golden-replay).
- **Adding content is data first:** a power (new-power skill), a terrain piece (new-terrain-piece), a unit type or specialist (new-unit-type), a field (new-field). Code only when the data can't express it.
- **Maps** (`data/maps/<id>/`): `map.json` plus binary layers (Git LFS). Authored with the Unity editor tools and exported; the simulation never reads scenes.
- **Levels** (`data/levels/<chapter>/<id>.json`): map, fixed army, allowed pieces, hand of powers, objectives, three side quests, teaching flags.
- **Plans** keep stable ids and a deterministic order; casts are pinned to ticks.
- Specs come from the design docs (spec passes S1–S5, plan section 9). If a number isn't in a spec, don't invent it: ask the design-lead agent or use the plan's working default behind a flag.
