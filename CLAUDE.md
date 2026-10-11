# SmiteNot: project instructions

A god helps a weak army survive a far stronger attacker, using only indirect powers that act on the world (weather, water, terrain, vegetation), never on soldiers. Up to 10,000 units; a deterministic simulation with a scrubbable timeline preview; a puzzle campaign (defend as the god), an attack mode against an adaptive AI god, and an asymmetric auto-battler PvP. Unity 6 (URP) presents; an engine-free C# simulation decides everything.

## Where things are
- What the game is: @docs/design/game-brief.md (source of truth). Other design docs in docs/design/ load on demand; docs/design/README.md lists which ones have been copied from the claude.ai project. If the brief is missing, say so and work from docs/implementation-plan.md ("The game in short").
- How we build it, and milestones: docs/implementation-plan.md; current status: docs/milestones/STATUS.md; decisions that are expensive to reverse: docs/adr/.
- Simulation: sim/ (pure .NET; sim/src is also a Unity package). Unity project: unity/. Rules data: data/. Blender sources: art/. Scripts: tools/.

## Commands
- Fast tests: `dotnet test sim/GodGame.Sim.sln --filter Category=Fast`
- All simulation tests: `dotnet test sim/GodGame.Sim.sln`
- Benchmarks: `dotnet run -c Release --project sim/bench/GodGame.Sim.Bench`
- Headless run: `dotnet run --project sim/tools/GodGame.Sim.Cli -- run --seconds 180` (M0 placeholder; `hash --out <file>` writes per-second hashes)
- Analyzers still reject banned code: `python tools/ci/check_sim_analyzers.py`
- Format: `dotnet csharpier format <path>` (the edit hook does this); CI runs `dotnet csharpier check sim`
- Unity tests: the unity-tests skill (MCP when the editor is open, batch mode otherwise)
- Blender: `blender -b <file.blend> --python tools/blender/export.py -- --out <dir>`, then `validate.py`

## Golden rules
1. The simulation decides every outcome; Unity only reads it and writes plans. No gameplay logic in Unity scripts; the simulation never reads Unity state.
2. Determinism in sim/: no float/double/decimal, System.Random, DateTime, Stopwatch, string.GetHashCode, unordered Dictionary/HashSet iteration, unstable sorts, ad-hoc threads or UnityEngine. Use GodGame.Sim.Math. The build enforces it (GG0001 and the banned-API analyzer); details in .claude/rules/sim-determinism.md.
3. A change that alters battle outcomes bumps SimVersion and regenerates goldens with /golden-replay. Never edit files in sim/tests/golden/ by hand.
4. Every simulation feature lands with tests, including a scenario test named after the brief decision it implements.
5. Performance first: run /perf-check after touching per-unit or per-cell code. Budgets: docs/implementation-plan.md section 8.
6. Don't invent game rules. Check the brief; if the rule is open there, use the plan's working default (section 10) behind a data flag and say so.
7. Small diffs, one checklist item per branch (m<N>/<name>), conventional commits.
8. Don't hand-edit Unity YAML (.unity, .prefab, .asset) or ProjectSettings unless asked; use the Unity MCP tools or editor scripts (unity/Assets/_Game/Editor/).
9. sim/src: C# 9, .NET Standard 2.1, no runtime NuGet packages (Unity compiles the source directly). Watch the namespace: inside GodGame.Sim.*, `Math` means GodGame.Sim.Math, so write `System.Math.Max` for integer helpers.

## Vocabulary
regiment · plan layers (army, terrain, casts) · range rule (100 m), fizzle · field · cause event, causality trace · keyframe, playback track · launch, landing spread · swept · teeter · strain stages (creak, crack, sag) · specialist, charges · seat (attacker, defender), round, lock-in, sunset

## Who does what
sim-engineer (simulation code) · determinism-auditor (before merging sim changes) · ai-engineer (regiment AI, later the AI god) · perf-engineer (budgets) · unity-engineer (Unity side) · tech-artist (shaders, VAT, VFX, Blender) · test-engineer (scenario tests, goldens, fuzz) · design-lead (game rules, decisions, spec passes) · level-designer (campaign levels) · code-reviewer (before a pull request) · reviewers/ (the /review-idea panel)

## Workflow
- Start with /milestone; use plan mode for anything beyond a one-line fix.
- Tick items in docs/milestones/STATUS.md when they merge.
- Design decisions are recorded with the game-design skill; mirror them to the claude.ai project so the two never drift.
