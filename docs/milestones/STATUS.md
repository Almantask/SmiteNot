# Milestone status

## Current: M2 — Armies that march
Started: 2026-11-01

Goal: armies totalling 10,000 soldiers march, bunch and queue across a real heightfield, within the tick budget.

Needs first: the map scale (working default: about 1.2 × 0.8 km with 1.5 m field cells, plan section 10) and a floor test laptop (integrated graphics, plan section 8), so budgets are measured on minimum-spec hardware from here on.

### Checklist
- [x] 1. Heightfield terrain, navigation grid, movement classes (foot, horse, wheeled, water), per-class cost fields — Terrain (sim/src/GodGame.Sim/Navigation): the map's heightfield on its 1.5 m field grid, integer bilinear height, slope as the steepest 4-neighbour step; CostFields: a ushort cost per class per cell from slope (impassable past each class's limit; water impassable until M3), built on creation, rebuilt by rectangle in parallel rows, with a version check against stale reads; data/rules/movement-classes.json (placeholder numbers until spec pass S2). Desktop: full rebuild of 430,000 cells 12.7 ms serial, 3.8 ms on 4 threads; one-cell edit 0.13 µs. The golden was regenerated header-only (ruleset hash; all 180 per-second hashes identical, no SimVersion bump). determinism-auditor: no High findings
- [x] 2. Flow fields per regiment goal with incremental tile rebuilds — FlowField (sim/src/GodGame.Sim/Navigation): per (movement class, goal area), integer distances over 8 neighbours (step = mean of the two cells' costs × 10, diagonals × 14, no corner cutting), directions as the cheapest neighbour with fixed tie order; full build by Dial's bucket queue, repair after an edit clears the cells whose route ran through the changed area and refills them (cell-exact rather than whole tiles), and gives bit for bit the field a full build gives (60 random edits checked); FlowFieldSet keeps fields in first-asked order and builds or repairs them one per parallel chunk; shortest distances checked against Bellman-Ford; pinned hash. Desktop, 800 × 534 cells with a bridged river: one full field 23 ms; six fields 154 ms serial, 77 ms on 4 threads (battle start); a one-cell edit on the main route, costs plus all six fields repaired, 18 ms serial, 9.5 ms on 4 threads (budget 50 ms)
- [x] 3. Local steering (separation, alignment, obstacle avoidance) and crowd pressure through the spatial hash — Navigation (cost fields, flow fields, which field each regiment follows; working default until regiment AI: attackers march to the defender zone, defenders hold) and MovementSystem replace the placeholder wander: each soldier aims four flow-field steps ahead, keeps pace with whoever is in its path (car-following, creeping at 1/8 speed when blocked so jams unwind), blends in its regiment's mean velocity, is pushed apart from overlapping neighbours (the overlap is its crowd pressure, in mm) and never enters a cell its class can't (moves slide along walls); the spatial hash is built at the start of the movement step; unit types gain speedMmPerS and radiusMm (placeholders). 200 soldiers queue through a 7.5 m gap in a wall without entering a closed cell or overlapping more than 0.25 m; identical on 1, 2 and 8 threads. SimVersion 2, golden regenerated. Desktop, 10,000 units on m1-flat over 180 s: 3.2 ms per tick serial, 1.4 ms on 4 threads (budget 3.3 ms)
- [ ] 4. Formation slots; regiments of 50–200 with an officer or standard-bearer as anchor
- [ ] 5. Distance-to-lethal field (cliffs and the map edge for now)
- [ ] 6. Reachability check: coarse grid, breadth-first distance fill, clearance map, the 2× route cap, route overlay data
- [ ] 7. Route preview: the attacker's predicted path from the flow fields
- [ ] 8. Unity: debug renderer (indirect instanced capsules tinted by regiment), flow-field and cost overlays, strategic-zoom camera, map authoring v0 (heightmap import, deployment-zone painter, exporter)
- [ ] 9. Unity CI with GameCI for Edit and Play Mode tests
- [ ] 10. The Burst decision: benchmark the hot kernels on .NET, IL2CPP and Burst; record it in an ADR

### Exit criteria
- [ ] 10,000 attackers cross 1 km through a bridge chokepoint; congestion emerges and regiments queue rather than overlap
- [ ] The reachability check rejects a full trap and a route longer than 2× in ≤ 5 ms per drag
- [ ] The tick stays within both budgets at 10,000 units (fields not yet on), on the floor laptop and the recommended machine, and stays deterministic across the M1 matrix
- [ ] The debug view runs at 60 fps with 10,000 capsules on the recommended machine and at 20 fps or better on the floor laptop

### Decisions needed
- Map scale (plan section 10, needed by M2; working default above)

### Notes
- Movement-class numbers (each class's steepest climb and slope cost) are placeholders in data/rules/movement-classes.json until spec pass S2; they only follow tech notes section 3 (siege cares about slope far more than infantry).
- Heights are fixed during the battle for now, so Terrain and CostFields are derived state. When battle-time digs and fills arrive: hash the heights (a running hash per edit), write them into snapshots and rebuild after restore, apply edits serially in a fixed order, pin when they become visible relative to the navigation step, cover planning-phase edits in the plan hash, and widen DirtyAround for fields with a wider stencil.
- Unity .meta files for sim/src/GodGame.Sim/Navigation and its two files are not committed yet: Unity's batch mode refused to start on 2026-11-02 ("No valid Unity Editor license found"); sign in to Unity Hub, open the project once and commit what it generates.

## Done

### M1 — Deterministic core (2026-11-01)
### Checklist
- [x] 1. GodGame.Sim.Math: Fix, 2D/3D vectors, integer square root, binary angles and trig tables, PCG32 streams, xxHash64; exhaustive and property tests — Fix and IntMath.Sqrt (against BigInteger, every root below 2^20); Angle (16-bit, centi-degree round trip); Trig (tables built with integer series; sin and cos within 1 raw unit for all 65,536 angles, atan2 within 1 unit; table bits pinned in PinnedHashes.TrigTables); FixVec2 and FixVec3 (lengths from unrounded squares); determinism-auditor: no High findings
- [x] 2. World state as structure-of-arrays, the regiment table, entity ids with deterministic reuse — World (sim/src/GodGame.Sim/State): unit and regiment arrays, EntityPool with generations and a last-in-first-out free stack (hashed, since free order decides future ids), freed slots reset to zero; hash layout pinned in PinnedHashes.WorldBattleSeed7; determinism-auditor: no High findings. Spawn and despawn must stay single-threaded, in ascending slot order, when the tick runner lands (item 3)
- [x] 3. Tick scheduler with the fixed system order, IParallelRunner, rates and sub-rates — Simulation runs TickOrder's 11 steps (empty systems registered) by TickRate; fields 5 Hz and regiment AI 4 Hz staggered by entity (TickRate.IsDueFor); SerialRunner in sim/src, ThreadedRunner in the new sim/hosts/GodGame.Sim.Hosting (outside the thread ban); fixed chunks, a fresh kernel copy per chunk, lowest failing chunk rethrown; 10,000 wandering units hash identically on 1, 2 and 8 threads; ban list widened (PLINQ, timers, Task`1, ValueTask, ProcessorCount); determinism-auditor: no High findings
- [x] 4. Spatial hash — SpatialHash (sim/src/GodGame.Sim/State): uniform grid built by counting sort, cells list units in slot order, queries visit cells row by row; exact raw-unit distances with an axis test first, so far-off units can't overflow; returns the total match count; derived state (not hashed or snapshotted) with a debug check that it was rebuilt after spawns and despawns. Desktop benchmark at 10,000 units: build 0.29 ms, a 2 m query per unit 1.0 ms, no allocations; determinism-auditor: no High findings
- [x] 5. Map format v0, plan format v0 (army layer), ruleset loader v0 and ruleset hash — a vendored strict JSON reader (integers only, no unpaired surrogates) and an ASCII canonical form without $comment for every hash; RulesetLoader v0 reads data/rules/unit-types.json (the four base types; health and morale marked placeholder until spec pass S2); MapData (map.json + optional height.bin, at most 30 km and 16 million cells) and BattlePlan (army layer sorted by id, at most 10,000 soldiers; terrain and casts empty until M3); BattleSetup deploys a placeholder square block at 1.5 m until M2 formations; schemas in data/schema; data/maps/m1-flat and data/plans/m1-ten-thousand.json; ruleset, map, plan and deployed-world hashes pinned; determinism-auditor: no High findings
- [x] 6. Versioned snapshots, state hashes every N ticks, replay file v0 — Snapshot format v2 (magic, format version, SimVersion, battle id from the rules, map and plan hashes, tick, seed, every slot of the world, an xxHash64 checksum); restore reads into a scratch world, checks the world's invariants and refuses another battle, so a bad snapshot never half-restores; a 10,000-unit snapshot is far under the 4 MB keyframe budget; Simulation.RunAndHash records the hash every N ticks; Battle builds the M1 tick (empty systems plus PlaceholderWanderSystem until M2); Replay (.godreplay v0) round-trips and refuses another SimVersion, ruleset or map; determinism-auditor: no High findings
- [x] 7. godsim CLI: run, hash, compare — builds battles from rules, map, plan and seed or from a replay; --threads picks the runner; run prints the speed and can save a replay; hash writes per-second CSV with a header naming SimVersion, rules, map, plan and seed; compare refuses files from different setups and reports the first differing second, and also re-runs replays and goldens; CI hashes the 10,000-unit plan on Windows and Linux, diffs serial against 4 threads on Linux and checks out LFS; determinism-auditor: no High findings
- [x] 8. Determinism harness (twice, snapshot and restore, thread counts, cross-OS, Unity Mono editor, IL2CPP player) — twice the same, snapshot and restore (byte for byte, on any runner), 1/2/3/4/8 threads, Windows vs Linux in CI (hash files and goldens), the Unity Mono editor (GoldenReplayTests in Play Mode: 2/2) and the Windows IL2CPP player (GodGame → Build → Determinism Player, run with -run-goldens on 2026-11-01: `runtime player scripting IL2CPP`, `ok m1-ten-thousand-seed1`). By hand now, nightly from M2
- [x] 9. Golden replay infrastructure and the golden-replay skill end to end; SimVersion and CHANGELOG-sim.md — goldens are .godreplay files in sim/tests/golden (first: m1-ten-thousand-seed1, 10,000 units for 180 s); godsim golden list, verify, add and regenerate, compare --golden; the Golden test category and CI verify them; the golden-replay skill describes verify, update and add with these commands; SimVersion stays 1, since no existing outcome changed (first goldens). Run /golden-replay verify once yourself to see the skill end to end

### Exit criteria
- [x] 10,000 wandering units for three simulated minutes hash identically across two runs, snapshot and restore, 1 vs 8 threads, Windows vs Linux, and .NET vs Unity Mono vs IL2CPP — all verified 2026-11-01: the golden m1-ten-thousand-seed1 (180 per-second hashes) matches on .NET (Windows and Linux CI, 1 to 8 threads), the Unity 6000.6.7f1 Mono editor and the IL2CPP Windows player
- [x] At least 15× real time with placeholder systems; baseline in budgets.json — one tick at 10,000 units takes 37.6 µs serial and 16.0 µs on 4 threads (godsim run: 350× real time over 180 s); recorded as baseline m1_2026_11_01 in budgets.json, on the dev desktop (Ryzen 9 5900X), not yet on the floor laptop
- [x] determinism-auditor review of the whole simulation with no open high-severity findings — 2026-11-01 at 78ce4d3: no High; the one Medium is the unverified IL2CPP player (item 8); CI green on Windows and Linux

### Notes
- The IL2CPP check: GodGame → Build → Determinism Player (IL2CPP), then `unity/Builds/DeterminismPlayer/GodGameDeterminism.exe -batchmode -nographics -run-goldens -repo <repo> -golden-results results.txt`; every line must read `ok`.
- Before or during M2 (Low findings from the M1 exit review): one shared rules-and-map loader in sim/src for both hosts (Unity's GoldenRunner still has its own); hash the map with zones sorted by side; a registry of RNG system ids with a uniqueness test, and a decision on keying streams by generation; an analyzer rule requiring readonly struct kernels, plus thread-count tests with the real movement systems; exact FixVec2.DistanceSquared before separation uses it; a tie-break for Angle.DeltaTo at half a turn in steering; build the spatial hash at a fixed point in Movement and check its tick; validate replay map ids before they become paths; a reusable scratch world for restores (M4 scrubbing).
- Placeholders to replace: BattleSetup's square block (M2 formations), unit-type health and morale (spec pass S2). PlaceholderBattle remains only for Unity's SimHost cube and its pinned hash.

### M0 — Foundations (2026-10-11 to 2026-11-01)
Started: 2026-10-11

### Checklist
- [x] 1. Repository basics (gitignore, gitattributes + LFS, editorconfig, protection on main) — GitHub ruleset `main` (2026-11-01) blocks deletion and force-pushes on the default branch; no required pull requests or checks, since commits go straight to main
- [x] 2. Design docs, ADR 0001, this file — ADR 0001, this file, the plan and seven more design docs (the brief among them) copied 2026-11-01; idea-reviews.md and gamer-review.md weren't in the copy and are optional (opinions, not decisions)
- [x] 3. Simulation solution with analyzers and first tests
- [x] 4. CI: sim.yml with cross-OS hash check
- [x] 5. Unity project (now 6000.6.7f1) with the sim package and SimHost cube — GodGame → Setup → Apply Project Settings run through MCP for Unity (2026-11-01): Linear, IL2CPP, DX12 then DX11, Enter Play Mode without domain reload, URP_Pipeline as the default pipeline, Bootstrap scene in the build list; Edit Mode tests 4/4 green
- [x] 6. Claude Code: CLAUDE.md, rules, agents, skills, hooks, MCP — all files are in and the hooks were exercised; MCP for Unity v10.3.0 installed locally and connected (stdio, port 6400)
- [x] 7. Blender export and validation scripts, test soldier imported — U_testsoldier_a exported and validated with Blender 5.2.2 LTS and imported through ArtImportPostprocessor: Generic rig, no materials, readable, uncompressed, clips only in _Anims; stands 1.8 m tall on y = 0 with an unrotated root
- [x] 8. Exit criteria verified — all six (2026-11-01)

### Exit criteria
- [x] dotnet test green on Windows and Linux; hashes match — CI run 1 (2026-10-11): both test jobs and cross-os green
- [x] Unity opens cleanly; the cube moves with the simulation — 6000.6.7f1 opens with no errors (2026-11-01); in Play Mode the cube went from (5.000, 3.508) m at tick 0 to (4.705, 8.039) m at tick 99, matching SimHost's blended position, at 20 ticks per game second
- [x] A float in sim/src fails the build and the hook flags it — GG0001 fails the build (CI runs tools/ci/check_sim_analyzers.py); sim_lint.py flags it on write
- [x] /context, /agents and /milestone work — run locally 2026-11-01: /milestone printed M0's status; /context lists CLAUDE.md under memory files and all 13 project agents; /agents is no longer a wizard in this Claude Code version (it points to .claude/agents/), so the /context agent table stands in for it
- [x] Unity MCP reads the console; Blender MCP lists the scene — Blender MCP verified in the cloud (5.2.2 on Xvfb) and locally (5.2.3, add-on 1.8): get_scene_info lists RIG_humanoid and the three U_testsoldier_a LODs; MCP for Unity v10.3.0 read_console returns the editor log (2026-11-01)
- [x] Test soldier exports with a clean validation report — 0 errors, 2 expected warnings (capsule LODs under the hero and mid targets); a broken copy fails with 6 errors

### Decisions needed
- (none for M0)

### Notes
- The banned-API analyzer alone doesn't reject `float x = 1f;`; the project analyzer GG0001 (sim/analyzers) does. Recorded in the plan, Appendix A, "As built in M0".
- The three /review-idea reviewer agents were drafted in the repo because the claude.ai originals weren't available; replace them when copying the design docs.
- unity/Packages/manifest.json holds the versions Unity 6000.6.7f1 upgraded the project to (URP and VFX Graph 17.6.0, uGUI 2.6.0, Test Framework 1.8.0, Input System 1.20.1, Burst 2.0.0, Collections 6.6.0, Mathematics 1.4.0, Cinemachine 6.6.0) plus MCP for Unity v10.3.0; ProjectVersion.txt names 6000.6.7f1 (b22767f04f04).
- The Blender scripts also pass under the real Blender 5.2.2 LTS binary, with the same results as the bpy module (exit code 1 on a broken file).
- Expected warnings in the Unity console: Input Manager deprecation (Active Input Handling is Both), and "inconsistent LOD naming" for U_testsoldier_a_LOD1 and _LOD2 because each LOD is its own FBX. Soldiers are drawn with indirect instancing, not LODGroups, so the second is harmless.
- Player Settings → Run In Background is off, so Play Mode pauses while the editor isn't focused, including when Claude drives it over MCP. Check Play Mode state through the simulation tick, not the frame count.
- For the VAT baker (M8): skinned meshes keep Blender's Z-up in mesh space (the rig and mesh nodes carry a 90° X rotation under an unrotated root), so sample in root space; the walk clip imports as `RIG_humanoid|A_humanoid_walk` with looping off.
