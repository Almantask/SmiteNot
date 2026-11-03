# Simulation changelog

Every `SimVersion` bump and why. Written by the `golden-replay` skill; newest first.

| SimVersion | Date | Reason | Scenarios affected | Notable changes |
|---|---|---|---|---|
| 6 | 2026-11-03 | M3 item 3: water. Virtual-pipes shallow water at 5 Hz with two substeps, depth in µm and four outflows per cell (fields), depth-aware gravity and bed friction (c_f 0.0042), map water sources and sinks; wetness starts at 8,192 (S1) | m1-ten-thousand-seed1 (regenerated: the field set and rules changed; m1-flat has no water, so units move as before) | none on m1-flat |
| 5 | 2026-11-02 | M3 item 1: the field framework. The world carries its fields (water, wetness, mud from data/rules/fields.json), which join the state hash and the snapshot (format 4); no field models yet, so no unit moves differently | m1-ten-thousand-seed1 (regenerated: every hash covers the fields now) | none: positions identical, only the hashed state grew |
| 4 | 2026-11-02 | M2 exit tuning: attackers aim for the far quarter of the defender zone (no pile-up at its border), braking distance 0.3 m, blocked soldiers creep only while their overlap is under 0.25 m | m1-ten-thousand-seed1 (regenerated) | m2-bridge: 10,000 attackers cross in about 44 minutes; under 1% of soldiers overlap a neighbour by more than a quarter of contact |
| 3 | 2026-11-02 | Header only: data/rules gained routes.json (M2 item 6, the reachability check), so the ruleset hash in m1-ten-thousand-seed1 changed; per-second hashes verified identical, no bump | m1-ten-thousand-seed1 (header) | — |
| 3 | 2026-11-02 | Header only: data/rules gained hazards.json (M2 item 5, the lethal drop for the distance-to-lethal field), so the ruleset hash in m1-ten-thousand-seed1 changed; per-second hashes verified identical, no bump | m1-ten-thousand-seed1 (header) | — |
| 3 | 2026-11-02 | M2 item 4: formation slots behind an officer or standard-bearer (snapshot format 3: formation slot, anchor, facing, files); regiments deploy in ranks; officers wait for stragglers; pushes capped at a quarter cell; regiment sizes 50 to 200 (placeholders) | m1-ten-thousand-seed1 (regenerated) | regiments march east in formation; no deaths yet |
| 2 | 2026-11-02 | M2 item 3: flow-field movement with steering and crowd pressure replaces the placeholder wander; unit types gain speed and body radius (placeholders) | m1-ten-thousand-seed1 (regenerated) | attackers march east to the defender zone at 1.5 m/s and queue there; defenders hold; no deaths yet |
| 1 | 2026-11-02 | Header only: data/rules gained movement-classes.json (M2 item 1), so the ruleset hash in m1-ten-thousand-seed1 changed; per-second hashes verified identical, no bump | m1-ten-thousand-seed1 (header) | — |
| 1 | 2026-10-11 | M0 placeholder simulation (tick counter, wandering units, state hash) | none (no goldens yet) | — |
