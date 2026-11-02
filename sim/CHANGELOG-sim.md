# Simulation changelog

Every `SimVersion` bump and why. Written by the `golden-replay` skill; newest first.

| SimVersion | Date | Reason | Scenarios affected | Notable changes |
|---|---|---|---|---|
| 2 | 2026-11-02 | M2 item 3: flow-field movement with steering and crowd pressure replaces the placeholder wander; unit types gain speed and body radius (placeholders) | m1-ten-thousand-seed1 (regenerated) | attackers march east to the defender zone at 1.5 m/s and queue there; defenders hold; no deaths yet |
| 1 | 2026-11-02 | Header only: data/rules gained movement-classes.json (M2 item 1), so the ruleset hash in m1-ten-thousand-seed1 changed; per-second hashes verified identical, no bump | m1-ten-thousand-seed1 (header) | — |
| 1 | 2026-10-11 | M0 placeholder simulation (tick counter, wandering units, state hash) | none (no goldens yet) | — |
