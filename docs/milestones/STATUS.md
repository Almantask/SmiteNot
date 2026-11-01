# Milestone status

## Current: M1 — Deterministic core
Started: 2026-11-01

### Checklist
- [x] 1. GodGame.Sim.Math: Fix, 2D/3D vectors, integer square root, binary angles and trig tables, PCG32 streams, xxHash64; exhaustive and property tests — Fix and IntMath.Sqrt (against BigInteger, every root below 2^20); Angle (16-bit, centi-degree round trip); Trig (tables built with integer series; sin and cos within 1 raw unit for all 65,536 angles, atan2 within 1 unit; table bits pinned in PinnedHashes.TrigTables); FixVec2 and FixVec3 (lengths from unrounded squares); determinism-auditor: no High findings
- [ ] 2. World state as structure-of-arrays, the regiment table, entity ids with deterministic reuse
- [ ] 3. Tick scheduler with the fixed system order, IParallelRunner, rates and sub-rates
- [ ] 4. Spatial hash
- [ ] 5. Map format v0, plan format v0 (army layer), ruleset loader v0 and ruleset hash
- [ ] 6. Versioned snapshots, state hashes every N ticks, replay file v0
- [ ] 7. godsim CLI: run, hash, compare
- [ ] 8. Determinism harness (twice, snapshot and restore, thread counts, cross-OS, Unity Mono editor, IL2CPP player)
- [ ] 9. Golden replay infrastructure and the golden-replay skill end to end; SimVersion and CHANGELOG-sim.md

### Exit criteria
- [ ] 10,000 wandering units for three simulated minutes hash identically across two runs, snapshot and restore, 1 vs 8 threads, Windows vs Linux, and .NET vs Unity Mono vs IL2CPP
- [ ] At least 15× real time with placeholder systems; baseline in budgets.json
- [ ] determinism-auditor review of the whole simulation with no open high-severity findings

## Done

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
