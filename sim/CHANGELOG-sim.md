# Simulation changelog

Every `SimVersion` bump and why. Written by the `golden-replay` skill; newest first.

| SimVersion | Date | Reason | Scenarios affected | Notable changes |
|---|---|---|---|---|
| 3 | 2026-11-02 | Header only: data/rules gained routes.json (M2 item 6, the reachability check), so the ruleset hash in m1-ten-thousand-seed1 changed; per-second hashes verified identical, no bump | m1-ten-thousand-seed1 (header) | — |
| 3 | 2026-11-02 | Header only: data/rules gained hazards.json (M2 item 5, the lethal drop for the distance-to-lethal field), so the ruleset hash in m1-ten-thousand-seed1 changed; per-second hashes verified identical, no bump | m1-ten-thousand-seed1 (header) | — |
| 3 | 2026-11-02 | M2 item 4: formation slots behind an officer or standard-bearer (snapshot format 3: formation slot, anchor, facing, files); regiments deploy in ranks; officers wait for stragglers; pushes capped at a quarter cell; regiment sizes 50 to 200 (placeholders) | m1-ten-thousand-seed1 (regenerated) | regiments march east in formation; no deaths yet |
| 2 | 2026-11-02 | M2 item 3: flow-field movement with steering and crowd pressure replaces the placeholder wander; unit types gain speed and body radius (placeholders) | m1-ten-thousand-seed1 (regenerated) | attackers march east to the defender zone at 1.5 m/s and queue there; defenders hold; no deaths yet |
| 1 | 2026-11-02 | Header only: data/rules gained movement-classes.json (M2 item 1), so the ruleset hash in m1-ten-thousand-seed1 changed; per-second hashes verified identical, no bump | m1-ten-thousand-seed1 (header) | — |
| 1 | 2026-10-11 | M0 placeholder simulation (tick counter, wandering units, state hash) | none (no goldens yet) | — |
