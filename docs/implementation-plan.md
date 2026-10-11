# Implementation plan: Strategy God Game

*Version 1 · 2026-10-11 · Unity 6 (URP) · Blender · Claude Code*

This plan turns the design (game brief, power palette, tech notes, unit spec, brainstorms and reviews) into a buildable project: the architecture, the repository, the Claude Code setup (instructions, agents, skills, hooks, MCP servers), the test strategy, the Blender pipeline and a milestone sequence with testable exit criteria. Appendix A holds ready-to-adapt starter files.

**Source of truth.** `docs/design/game-brief.md` decides *what* the game is; this plan decides *how* it is built. When they disagree, the brief wins and this plan is updated in the same change.

---

## The game in short

**The idea.** A small army defended by a god faces a much bigger one, and you can play either side. As the god, you help the small army survive. You can never touch a soldier: you work only on the world (rain, rivers, wind, fire, cold, terrain, herds). Every power must land at least 100 m from both armies when it fires, so you act ahead of them: flood the ford they will cross in two minutes, freeze the lake they will march over, light the brush behind a herd just as the column passes. The attacker's size is the weakness you exploit: bridges and ice fail under crowds, thousands of feet churn roads to mud, packed columns get pushed into rivers. As the attacker, you command the big army against a god who keeps reshaping the world against you, and you buy, place and upgrade units to break through. Up to 10,000 toy-like soldiers, exaggerated physics, and deaths played as cartoon gags.

**How a battle plays: plan, preview, watch.**
1. **Plan in three layers:** position your army; shape the terrain (dig channels, raise ridges, set boulders, place herds, build walls, towers, dams and bridges); queue god powers on a timeline. The defender's god does most of its work in the last two layers; the attacker's is mostly the first (buying, placing and upgrading units), with a few powers of its own.
2. **Scrub the future:** a deterministic preview shows exactly what will happen (the attacker's route, the flood's reach, who drowns, where launched soldiers land). Change the plan and watch the future change.
3. **Commit and watch:** the battle runs with no input; both armies are AI, and your whole influence is the plan. An end-of-battle highlight reel shows the cause and the payoff, ready to save as a clip.

**Modes**

| Mode | What it is |
|---|---|
| Campaign (you defend) | Puzzle levels as the god. Each gives you an army, a few terrain pieces and a small hand of powers; the obvious move fails and the right combination works. Levels teach one idea at a time, and three side quests per level (restraint, spectacle, protection) earn cosmetic stars. |
| Attack (you attack the AI god) | You lead the big army against an AI god who prepares terrain and powers, and changes them between rounds in answer to what it has seen of your army. You buy, place and upgrade units to break through: fascines against its channels, crossing marshals against its rotted bridges, firebrands against its hedges, open order against its thin ice. Same five-round structure and budgets as PvP; the god plays fair (it never sees your new purchases, and its energy is limited and grows each round like yours) and has a readable personality (storm, fire, builder). |
| PvP | An asymmetric auto-battler in the spirit of Mechabellum, one player in each seat. The attacker has many soldiers and little god energy; the defender has few soldiers and lots of energy. Up to five rounds (see Rounds and budgets below). |

**Goals**

| Mode | You win when |
|---|---|
| Campaign | Your army survives the battle under the level's constraints (or protects the level's objective, if that variant is chosen; open in the brief). Three optional side quests per level earn stars. |
| Attack | You wipe out the god's army in any of the five rounds. If any of it survives the fifth round, the god wins. |
| PvP, attacker | Wipe out the defender's army in any of the five rounds. |
| PvP, defender | Keep part of your army alive through all five rounds (or rout the attacker in a round, which counts as a hold). |

"Wiped out" means every regiment is gone, fled off the map, or down to under about 10% of its starting size, so one soldier hiding in a corner can't hold a round. Routed soldiers still on the field still count; the attacker has to ride them down.

**Rounds and budgets (attack mode and PvP)**
- **Up to five rounds.** Each round is a complete battle replayed from the start: both armies return at full strength and losses never carry over.
- **Two limited budgets per seat:** bodies (units and their upgrades) and energy (terrain shaping and god powers). Each round adds new income of both, more each round. You can't spend beyond what you have, and unspent income doesn't carry over (working assumption), so nobody can save up for one giant round. The attacker's income is mostly bodies; the defender's (and the AI god's) mostly energy.
- **Everything stays.** Units, upgrades, terrain pieces and queued powers carry over into every later round. A queued power is paid once and fires again each round, at the same time and place. Removing something costs extra; nothing is refunded, and moving or re-timing means removing and buying again.
- **New is hidden, old is known.** Each side sees everything the other placed in earlier rounds; this round's new placements stay hidden until the battle starts, then are revealed. So the attacker learns more of the defence every round, and the defender must keep adding fresh surprises.
- **Each round ends** at the wipe, or at sunset (the round's time limit), or when the attacker routs.
- **Fixed limits every round:** the 100 m range rule for powers and terrain; no placement may fully trap an army or stretch its route past twice the straight-line distance; a soldier cap per seat; in PvP, a planning timer (working assumption: about 60 to 90 seconds).
- **The campaign has no rounds:** each level is one battle with a fixed army, a few terrain pieces, a small hand of powers and a fixed energy budget, and you can re-plan and re-preview as often as you like before committing.

**What's the point**
- **The fun as the god:** watching a plan go off. A bank shot from 300 m away turns the attacker's own mass against it (rain → the river rises → the ford floods → the column is swept away), followed by the slapstick payoff.
- **The fun as the attacker:** reading the god's setups and out-thinking them with the right army: the bridge that should have collapsed holds because your marshals metered the crossing, and the slapstick now happens to your own soldiers when you guess wrong.
- **Mastery:** predicting the other side, building short, readable chains, and reading what the opponent built last round to surprise them this round.
- **Tone:** the strategy is serious and exact; the humour lives in the presentation, never in luck. The same plan always gives the same battle.

**What it is not:** not a real-time strategy game with micro (no commands during the battle); not a god game of direct damage (no lightning on soldiers); not random.

---

## Contents

- The game in short
0. How to use this plan with Claude Code
1. Decisions this plan builds on
2. What the design demands from the build
3. Architecture
4. Repository layout
5. Claude Code setup
6. Testing strategy
7. Art pipeline (Blender → Unity)
8. Performance budgets
9. Milestones
10. Open questions that block build decisions
11. Risks and mitigations
12. Working agreements
- Appendix A: starter files
- Appendix B: design decisions → scenario tests
- Appendix C: references

---

## 0. How to use this plan with Claude Code

1. Create an empty GitHub repository (private) and clone it. Install: Unity Hub + Unity 6.3 LTS, .NET 10 SDK, Git + Git LFS, Python 3.10+ and `uv`, Blender (current LTS), Claude Code, GitHub CLI.
2. Copy this file to `docs/implementation-plan.md`, and copy the design docs from the claude.ai project into `docs/design/` (file list in section 4).
3. Open Claude Code in the repository root, switch to plan mode, and give it this prompt:

   > Read docs/implementation-plan.md. Execute milestone M0 (section 9) step by step, creating the files in Appendix A and adapting them where the plan says to. After each numbered M0 step, stop and show me what changed.

4. From M1 on, each work session starts with `/milestone` (a skill from section 5.5), which shows the current milestone, its unchecked items and the next suggested task. Work one checklist item per branch.
5. Keep the plan alive. When a design decision changes the build, update this file and `docs/milestones/STATUS.md` in the same pull request.

---

## 1. Decisions this plan builds on

| Area | Decision |
|---|---|
| Engine | Unity 6 with the Universal Render Pipeline. Start on **6.3 LTS** (supported until December 2027); move to the next LTS when it ships, and to Unity 7 (CoreCLR, .NET 10, C# 14) once it is stable. |
| Simulation | An engine-free C# library that decides every outcome: deterministic, integer and fixed-point maths, seeded randomness, fixed system order. C# 9 and .NET Standard 2.1 until Unity 7. |
| Presentation | Unity only reads the simulation and writes plans. URP, indirect instanced soldiers with vertex animation textures, Shader Graph, VFX Graph, UI Toolkit, Input System, Cinemachine. |
| Art | Blender for models and animation, exported to FBX by script; animation baked to vertex animation textures by a Unity editor tool. |
| Platforms | Players: Windows 10/11 x64 only at launch (brief, 2026-10-11), sold on Steam. Linux x64 only as a headless target for CI and the PvP verifier. No macOS or Linux player builds; native ARM64 Windows is a later option. |
| Hardware | Minimum: integrated graphics (Intel Iris Xe or AMD Radeon 680M class, 2020+ laptop, 4-core/8-thread CPU, 8 GB shared memory) at 20 fps or better. Recommended: a mid-range dedicated GPU at 60 fps. Every preset simulates the identical battle. |
| Source control | Git + Git LFS on GitHub; Unity Smart Merge for scenes and prefabs. |
| CI | GitHub Actions: the .NET simulation on every push (Windows and Linux, with a cross-OS hash check); Unity tests and builds via GameCI from M2. |
| Development | Claude Code with a project `CLAUDE.md`, path-scoped rules, specialist subagents, skills, hooks, and MCP servers for Unity and Blender. |
| Licence | Unity Personal (free up to $200k annual revenue or funding), then Unity Pro per seat. |

---

## 2. What the design demands from the build

| Requirement (source) | Build consequence |
|---|---|
| The same plan gives the same battle on every machine: campaign puzzles, PvP (brief, 2026-10-08) | Integer and fixed-point maths, simulation-owned RNG, fixed update order, no engine physics or floats in gameplay, state hashes every few ticks, cross-runtime golden tests. |
| A scrubbable future at 10 to 20 times real time (2026-10-08) | Headless simulation on worker threads, keyframes plus a lightweight playback track, a budget per tick rather than per frame. |
| Up to 10,000 units (pillar 3) | Structure-of-arrays data, regiments as the unit of decision, flow fields, a spatial hash. |
| Visuals are the gameplay state (pillar 5) | Gameplay fields live on the CPU in integer grids and are uploaded as textures; shaders read the same cells the units feel. |
| The battle takes no input (2026-10-08) | The plan is the only input, so a replay is map + plan + seed + versions (kilobytes). |
| Hidden placements, exchanged at lock-in (PvP, 2026-10-09) | A plan-exchange protocol; a server can re-simulate on .NET to verify. |
| Exaggerated launches, no ragdolls, no wobble (2026-10-09) | Ballistic bodies in the simulation, baked tumble and crumple clips, launch events in the playback track. |
| Soldiers never walk into hazards; deaths have many causes (2026-10-09, 2026-10-11) | Lethal cells closed in the flow field, a distance-to-lethal field, crowd pressure that can overpower avoidance; one damage model with a cause and source on every death (combat, impact, a hazard that reached them, being put into one). |
| Range rule at the firing tick; no full traps; route at most 2× (2026-10-08, 2026-10-09) | Validation services in the simulation: per-cast range check at its tick; coarse reachability fill with a clearance map. |
| Specialists through one shared executor (2026-10-10) | Actions as data on unit types; one executor; charges on regiments. |
| Small edits must not flip outcomes (review, 2026-10-10) | Smooth fields, regiment decisions with hysteresis, hinge markers for close calls, a ±2 m / ±1 s perturbation test. |
| Performance before flourish (2026-10-09) | Benchmarks with budgets (section 8), run by a skill and in CI. |

---

## 3. Architecture

### 3.1 The big picture

```
┌─────────────────────── Unity (presentation only) ───────────────────────┐
│  Planning UI (UI Toolkit) ── edits ──────────────►  Plan (data)          │
│  Unit renderer (URP, VAT) ◄── reads ── Playback track + launch events    │
│  Terrain/water/ice shaders ◄── reads ── Field snapshots (textures)       │
│  Overlays, timeline       ◄── reads ── Cause events, validation results  │
│  Editor tools: map and level authoring ──► map and level files           │
└───────────────────────────────▲─────────────────────────────────────────┘
                                │ SimHost (worker threads, never the main thread)
┌───────────────────────────────┴─────────────────────────────────────────┐
│  GodGame.Sim  (engine-free C#, deterministic)                            │
│  Plan ─► fixed tick loop ─► world state ─► keyframes · playback track ·  │
│                                            cause events · state hashes   │
└───────────────────────────────▲─────────────────────────────────────────┘
                 same source    │ also runs in
   dotnet test · godsim CLI (headless battles, perturbation, validation) · PvP verifier (Linux)
```

**Rule of thumb:** Unity may read anything from the simulation and may write only the plan.

### 3.2 Simulation projects

| Project | Contents | Target |
|---|---|---|
| `GodGame.Sim.Math` | Fixed-point types, integer square root, angle and trig tables, PCG32 random streams, xxHash64 | netstandard2.1, C# 9 |
| `GodGame.Sim` | World state (structure of arrays), systems, tick scheduler, plan, validation (range rule, reachability), cause log, snapshots, playback track, timeline (keyframes, re-simulation scheduler) | netstandard2.1, C# 9 |
| `GodGame.Sim.Rules` | Loads rules JSON into an immutable ruleset with a hash; a small vendored JSON reader (no NuGet) | netstandard2.1, C# 9 |
| `GodGame.Sim.Tests` | Unit and property tests | net10.0 |
| `GodGame.Sim.Scenarios` | Design (scenario) tests named after brief decisions; golden replays | net10.0 |
| `GodGame.Sim.Bench` | BenchmarkDotNet benchmarks and budget checks | net10.0 |
| `GodGame.Sim.Cli` (`godsim`) | Headless runner: run, hash, validate data, perturb, export field images, compare runs | net10.0 |

No runtime NuGet dependencies in the three libraries: Unity compiles their source directly and can't restore packages. Analyzers and test packages are fine because they never reach Unity.

### 3.3 Numbers: integers first, fixed-point where needed

- **Positions and velocities:** `Fix`, a 64-bit signed value with 16 fractional bits (resolution about 0.015 mm; range far beyond any map). Products use a 64-bit multiply and shift, with overflow asserts in debug builds.
- **Fields:** plain integers in natural units, which are deterministic, compact for keyframes and upload straight to GPU textures:

  | Field | Storage |
  |---|---|
  | Ground height, water height, ice thickness, snow depth | int32, millimetres |
  | Temperature | int16, hundredths of a degree |
  | Wetness, mud, fuel, fog/smoke density, traffic | uint16, 0–65535 |
  | Wind | two int16 components, cm/s |
  | Flow-field directions | byte index into a direction table |

- **Angles:** 16-bit binary angles (65,536 per turn); sine and cosine from a table; atan2 by octant plus table.
- **Square root:** integer bit-by-bit method; avoid it in hot loops where squared distances will do.
- **Randomness:** PCG32 streams derived by hashing (seed, system id, entity id, tick). No shared mutable generator across threads.
- **Hashing:** xxHash64 over the state arrays every N ticks, and over keyframes.

### 3.4 The tick

A fixed rate of **20 ticks per second**, identical on every machine and every graphics setting. Systems run in this fixed order (one class per step, the order written once in code):

1. **Plan events fire.** Casts reaching their tick are checked against the range rule (whole area at least 100 m from both armies' regiment outlines, stragglers ignored, launched and stunned soldiers still counted); a failed check fizzles with a reason.
2. **Fields step** at a sub-rate (starting point: 5 Hz, staggered by tile) on active tiles only, in a fixed interaction order: water → wetness and mud → temperature → ice and snow → fuel and fire → wind plumes (rebuilt only when a wind cast starts or ends) → fog and smoke advection. Interaction strengths come from the rules table.
3. **Structures and load.** Bridge segments, ice patches, dams, mine cells and snow slopes compare load with strength; failures emit cause events.
4. **Regiment AI** (starting point: 4 Hz, staggered by regiment id): goals, waiting, rerouting, garrisons, specialist actions, morale state, with hysteresis so near-ties don't flicker.
5. **Navigation.** Rebuild dirty flow-field tiles, cost fields and the distance-to-lethal field.
6. **Movement.** Sample flow fields, steer, separate, apply crowd pressure, resolve teetering and lethal edges.
7. **Ballistics and impacts.** Step launched soldiers and rolling boulders along their paths; impacts (stampedes, charges, boulders, falling trees, collapses) trample, crush or launch through the spatial hash; resolve landings (stun, damage by surface, a short knock-down for neighbours that never kills or launches).
8. **Combat.** Melee contacts through the spatial hash; regiment volleys from the projectile table plus wind along the shot and height.
9. **Hazards.** Swept state and drowning timers; burning state and burn timers when a fire front reaches soldiers; reaching a bank or safe ground. Every death records its cause and source.
10. **Morale.** Casualty events → shock and contagion; routing; fleeing off the map counts as wiped.
11. **Bookkeeping.** Cause events, playback samples (5 per second), launch events, state hash, keyframe every 5 s.

All rates are tuning values; changing any of them changes outcomes, so it bumps `SimVersion` (section 3.9).

**Why fields need a sub-rate and active tiles.** The 2026-10-10 review's estimate: nine fields × about 430,000 cells (a 1.2 × 0.8 km map at 1.5 m) × 20 Hz × 15× real time is over a billion cell updates a second. Ticking fields at 5 Hz and only on tiles that changed recently (typically 5–20% of the map) brings that down by one to two orders of magnitude.

### 3.5 Determinism rules (enforced, not just written down)

In the simulation libraries:
- No `float`, `double`, `decimal`, `System.Math`/`MathF` floating-point overloads.
- No `System.Random`, `DateTime.Now`/`UtcNow`, `Environment.TickCount`, `Stopwatch`, `Guid.NewGuid`.
- No `string.GetHashCode()` (randomised per process on .NET) and no hashing of object references.
- No iteration over `Dictionary`/`HashSet` where order can affect state; use arrays, sorted keys or insertion-ordered containers.
- No unstable sorts without a total tie-breaker (sort by key, then by entity id).
- Parallel work only through the simulation's runner, writing disjoint index ranges; reductions happen in index order on one thread.
- No `UnityEngine` (the assembly definition forbids it), no static mutable state, no reading wall-clock time or thread identity into state.

Enforcement, in layers: the analyzer `Microsoft.CodeAnalysis.BannedApiAnalyzers` with `BannedSymbols.txt` (build fails); a Claude Code hook that scans edited simulation files (instant feedback); the `determinism-auditor` subagent on every simulation change; golden replays plus cross-thread, cross-OS and cross-runtime hash tests in CI.

### 3.6 Threading

- **In Unity:** `SimHost` owns the simulation on background threads: one preview worker that re-simulates the edited future progressively (first 30 s first, then onward), plus the parallel runner inside each tick. The main thread never waits on the simulation; it renders the newest playback samples from a triple buffer.
- **Inside a tick:** `IParallelRunner.For(count, chunkSize, kernel)` with fixed chunking. A .NET implementation uses threads; a Unity implementation may use the job system. Tests prove 1, 2 and N threads give identical hashes.
- **Burst:** optional. Hot kernels are written as static methods over flat arrays of plain structs, so they can be Burst-compiled later from a Unity-side adapter if profiling shows the need (decision point at the end of M2). Integer maths gives identical bits either way.

### 3.7 Unity project

- **Packages:** URP, Input System, Shader Graph, VFX Graph, Cinemachine 3, Unity Test Framework, Mathematics/Collections/Burst (render-side jobs and optional kernels), an MCP package (section 5.6), Addressables (M8), Steamworks.NET (M11).
- **Settings:** Force Text serialization, Visible Meta Files, Linear colour space, IL2CPP for player builds, DirectX 12 by default with DirectX 11 as a fallback for weak integrated-graphics drivers (Windows is the only player platform, so no Vulkan or Metal shader variants), and Enter Play Mode without domain reload. Unity is removing domain reloads on its way to CoreCLR, so Unity-side scripts must not rely on static state being reset.
- **Assemblies:**

  | Assembly | Role |
  |---|---|
  | `GodGame.Sim.*` (local package) | The simulation source, compiled with no engine references |
  | `GodGame.Bridge` | SimHost, playback buffer, field uploader, plan-editing API, replay loading |
  | `GodGame.Rendering` | Unit renderer (indirect instancing, compute culling, LOD bins), VAT playback, field-driven materials, overlays |
  | `GodGame.UI` | Planning interface, timeline, causality trace, results screens |
  | `GodGame.Audio` | Event-driven audio from cause events |
  | `GodGame.Editor` | Map and level authoring, exporters, VAT baker, import validation |
  | `GodGame.Tests.EditMode`, `GodGame.Tests.PlayMode` | Unity-side tests |

- **How Unity consumes the simulation:** `sim/src/` is a Unity package (`package.json` at its root, one `.asmdef` per project folder with `noEngineReferences: true`), referenced from `unity/Packages/manifest.json` as `"com.godgame.sim": "file:../../sim/src"`. MSBuild output is redirected to `sim/.build/`, which Unity ignores because the folder name starts with a dot.

### 3.8 Data and file formats

| Kind | Where | Notes |
|---|---|---|
| Rules | `data/rules/*.json`, schemas in `data/schema/` | Unit types, movement classes (cap four at launch: foot, horse, wheeled, one water class), specialist actions, powers, terrain pieces, the field interaction table, projectile tables, budgets. Numbers are integers in documented units, or decimal strings converted exactly at load. A ruleset hash is stamped on replays and checked at PvP lock-in. |
| Maps | `data/maps/<id>/` | `map.json` (deployment zones, climate, prevailing wind, water sources, retreat edges) plus binary layers (height, soil, vegetation and fuel, initial water and snow). Authored with Unity editor tools and exported; the simulation never reads scenes. |
| Plans | JSON | Three layers (army, terrain, casts); stable ids; deterministic order. Casts are pinned to ticks; the schema reserves a slot for world-field triggers (open question). |
| Levels | `data/levels/<chapter>/<id>.json` | Map, fixed army, allowed terrain pieces, hand of powers, objectives, three side quests, teaching flags. |
| Replays | `.godreplay` (JSON) | Simulation version, ruleset hash, map id and hash, seed, plan(s). |
| Golden files | `sim/tests/golden/` | Per-second hashes and summary statistics per scenario. Only written by the `golden-replay` skill. |

### 3.9 Versions

- `SimVersion` (an integer constant) changes whenever outcomes change. Golden replays fail until it is bumped deliberately, with a line in `sim/CHANGELOG-sim.md`.
- The ruleset hash changes whenever rules data changes.
- The snapshot format has its own version, so old keyframes are never misread.

---

## 4. Repository layout

```
god-game/
├── CLAUDE.md                       # project instructions for Claude Code (Appendix A1)
├── README.md
├── .mcp.json                       # shared MCP servers (Blender); Unity MCP is configured per machine
├── .claude/
│   ├── settings.json               # permissions + hooks (A2)
│   ├── rules/                      # path-scoped instructions (A4)
│   ├── agents/                     # subagents (A5), reviewers/ for /review-idea
│   ├── skills/                     # skills (A6)
│   └── hooks/                      # hook scripts, Python, stdlib only (A3)
├── .github/workflows/              # sim.yml (A10), unity.yml (M2), claude.yml (optional)
├── .gitattributes  .gitignore  .editorconfig   (A8)
├── docs/
│   ├── implementation-plan.md      # this file
│   ├── design/                     # copied from the claude.ai project:
│   │   ├── game-brief.md           #   source of truth
│   │   ├── power-palette.md  tech-notes.md  unit-spec.md
│   │   ├── unit-brainstorm.md  idea-bank.md  terrain-weather-brainstorm.md
│   │   ├── idea-reviews.md  gamer-review.md
│   │   └── content-roadmap.md      #   what content comes first, by wave
│   ├── adr/                        # architecture decision records: 0001-engine-unity.md, ...
│   └── milestones/STATUS.md        # current milestone and checklist (A11)
├── sim/
│   ├── GodGame.Sim.sln  global.json  Directory.Build.props  BannedSymbols.txt   (A9)
│   ├── CHANGELOG-sim.md            # every SimVersion bump and why
│   ├── src/                        # ← also a Unity package (package.json here)
│   │   ├── GodGame.Sim.Math/       # .csproj + .asmdef side by side
│   │   ├── GodGame.Sim/
│   │   └── GodGame.Sim.Rules/
│   ├── tests/
│   │   ├── GodGame.Sim.Tests/
│   │   ├── GodGame.Sim.Scenarios/
│   │   └── golden/
│   ├── bench/GodGame.Sim.Bench/    # + budgets.json
│   └── tools/GodGame.Sim.Cli/
├── unity/
│   ├── Assets/_Game/{Bridge,Rendering,UI,Audio,Editor,Shaders,VFX,Art,Scenes,Settings}/
│   ├── Assets/Tests/{EditMode,PlayMode}/
│   ├── Packages/manifest.json      # "com.godgame.sim": "file:../../sim/src"
│   └── ProjectSettings/
├── data/
│   ├── rules/  schema/  maps/  levels/
├── art/
│   ├── source/                     # .blend files (Git LFS)
│   ├── palettes/
│   └── README.md                   # naming, budgets, export rules
├── tools/
│   ├── blender/                    # headless export and validation scripts
│   └── ci/                         # hash comparison, helpers
└── server/                         # PvP match service and verifier (M11)
```

---

## 5. Claude Code setup

### 5.1 What goes where

| Mechanism | Location | Loaded | Use it for |
|---|---|---|---|
| Project instructions | `CLAUDE.md` | Every session | The few rules that apply everywhere: architecture boundary, commands, golden rules, vocabulary. Keep it under 200 lines. |
| Path-scoped rules | `.claude/rules/*.md` with `paths:` frontmatter | When Claude reads or edits a matching file | Determinism rules for `sim/`, Unity conventions, shader rules, data rules, art rules. |
| Subagents | `.claude/agents/*.md` | On delegation | Specialists with their own context, tools and model. |
| Skills | `.claude/skills/<name>/SKILL.md` | When invoked (`/name`) or when relevant | Repeatable procedures: add a power, regenerate goldens, run Unity tests, export art. Custom slash commands are now skills. |
| Hooks | `.claude/settings.json` → `hooks` | Automatically, on events | Things that must always happen: lint the simulation on edit, guard protected files, run fast tests before Claude stops. |
| MCP servers | `.mcp.json` (shared) or local scope | Session start | Talking to the Unity editor and Blender. |
| Permissions | `.claude/settings.json` → `permissions` | Always | Pre-approve safe commands; deny dangerous ones. |
| Personal notes | `CLAUDE.local.md`, `.claude/settings.local.json` | Every session | Your machine paths and preferences; gitignored. |

Instructions are context, not enforcement: anything that must never happen belongs in a hook, an analyzer or a test, not only in `CLAUDE.md`.

### 5.2 CLAUDE.md

Full draft in Appendix A1. It covers: one-paragraph pitch; where things are (imports `@docs/design/game-brief.md` so the brief is always in context); build and test commands; nine golden rules (simulation decides and Unity reads; determinism; `SimVersion` and goldens; tests with every feature; performance; don't invent game rules; small diffs; no hand-edited Unity YAML; no NuGet in `sim/src`); vocabulary; which subagent does what.

### 5.3 Path-scoped rules

| File | `paths` | Contents |
|---|---|---|
| `sim-determinism.md` | `sim/src/**/*.cs` | The full determinism rule list (section 3.5), the numeric conventions (section 3.3), the tick order (section 3.4), how to add a system, how to log cause events. Full text in A4. |
| `sim-tests.md` | `sim/tests/**` | Test naming (`Decision_YYYY_MM_DD_<Name>_<Expectation>`), map and plan builders, categories (`Fast`, `Slow`, `Golden`, `Perf`), never edit golden files. |
| `unity-csharp.md` | `unity/Assets/**/*.cs` | No gameplay logic in Unity; read the simulation through `GodGame.Bridge`; no static state (no domain reload); no `Update()` per soldier; jobs and indirect draws for anything per unit. |
| `shaders.md` | `**/*.shader`, `**/*.hlsl`, `**/*.shadergraph`, `**/*.vfx` | Fields arrive as integer textures in documented units; shaders never write gameplay; every warning (strain, rising water, fire risk) has a strategic-zoom form; no gore; colour-blind-safe team colours. |
| `data-rules.md` | `data/**` | Units and schemas, how to add a power, terrain piece or unit type as data, ruleset hash, never floats in data. |
| `art-pipeline.md` | `art/**`, `tools/blender/**` | Naming, triangle budgets per LOD, palette texture, team-colour mask, export only through scripts. |

### 5.4 Subagents

Built-in `Explore` and `Plan` agents already exist; these add the project's specialists. Full definitions for two of them are in A5; the rest follow the same pattern.

| Agent | Model | Tools | Job |
|---|---|---|---|
| `sim-engineer` | opus | Read, Edit, Write, Bash, Grep, Glob, Skill | Implements simulation features test-first: systems, fields, validation, timeline. Follows the determinism rules; ends every task with the fast test suite green. |
| `determinism-auditor` | opus | Read, Grep, Glob, Bash (no Edit/Write) | Reviews every simulation diff for nondeterminism (floats, unordered iteration, unstable sorts, shared RNG, races, hidden statics), runs the thread-count and golden tests, reports findings by severity. Use before merging any `sim/` change. |
| `ai-engineer` | opus | Read, Edit, Write, Bash, Grep, Glob | Regiment AI for both armies: route choice, waiting, rerouting, garrisons, specialist targeting and escorts, defender openings; hysteresis and hinge margins; readable fixed rules. From M10 also the AI god: the between-rounds planner, setup libraries and personalities, never reading hidden information. |
| `perf-engineer` | sonnet | Read, Edit, Write, Bash, Grep, Glob | Benchmarks, profiling, data layout, budgets (section 8); proposes optimisations with before/after numbers. |
| `unity-engineer` | sonnet | Read, Edit, Write, Bash, Grep, Glob + Unity MCP | Bridge, rendering plumbing, planning UI (UI Toolkit), editor tools, Unity tests. Never puts gameplay in Unity. |
| `tech-artist` | sonnet | Read, Edit, Write, Bash, Grep, Glob + Unity MCP + Blender MCP | Shaders (terrain, water, ice, snow, fire), VAT baker and playback, VFX Graph, Blender export scripts and asset validation. |
| `test-engineer` | sonnet | Read, Edit, Write, Bash, Grep, Glob, Skill | Turns brief decisions into scenario tests, maintains builders, fuzzing and golden replays (through the skill), Unity Play Mode tests. |
| `design-lead` | opus | Read, Grep, Glob, Edit, Write (docs only, by instruction), Skill | The game-design partner inside the repo: answers rule questions from `docs/design/`, checks implementations against the brief, records decisions via the `game-design` skill, and drafts the spec passes S1–S5 (section 9) as docs and rules data. Never edits code. |
| `level-designer` | sonnet | Read, Edit, Write, Bash, Grep, Glob | From M9: builds campaign levels as data, runs the solver and perturbation checks, writes side quests. |
| `code-reviewer` | sonnet | Read, Grep, Glob, Bash (no Edit/Write) | General review of a branch before its pull request: correctness, clarity, tests, plan conformance. |
| `reviewers/game-designer`, `reviewers/veteran-player`, `reviewers/casual-gamer` | inherit | Read (as today) | Ported unchanged from the claude.ai project (`claude/reviewers/*.md`) for the `review-idea` skill. |

Tips:
- Give implementing agents `isolation: worktree` when you run two in parallel (for example `sim-engineer` on fields while `unity-engineer` builds the timeline UI), so they never edit the same checkout.
- Keep agent descriptions short and specific; Claude delegates by matching the description.
- `memory: project` on `determinism-auditor` and `perf-engineer` lets them accumulate recurring findings.

### 5.5 Skills

| Skill | Invoked by | What it does |
|---|---|---|
| `milestone` | You (`/milestone`) | Shows the current milestone from `docs/milestones/STATUS.md`, its unchecked items and recent commits; proposes the next task and which agent should take it. (A6) |
| `game-design` | Claude or you | The existing design-partner skill, ported: reads `docs/design/`, answers design questions, records decisions in the brief. Update its doc paths from `claude/…` to `docs/design/…`. |
| `review-idea` | You | The existing three-reviewer panel, ported, using the agents in `.claude/agents/reviewers/`. |
| `new-power` | Claude or you | Checklist for adding a god power as data: fields written, range rule, wind-up and duration, timeline bar, preview overlay, counters, scenario tests, palette doc. (A6) |
| `new-field` | Claude or you | Adding a world field: storage and units, slot in the update order, interaction-table rows, active-tile rules, GPU upload, shader sampling, tests, budget check. |
| `new-terrain-piece` | Claude or you | Footprint, 100 m buffer check, reachability effect, navigation stamping, material counters, placement UI, tests. |
| `new-unit-type` | Claude or you | Unit or specialist as data: movement class, load, launch response, AI rule, specialist action through the shared executor, charges, preview marker, art checklist, tests. |
| `scenario-test` | Claude or you | Turns a brief decision into an executable scenario test with the map and plan builders (Appendix B lists the first ones). |
| `golden-replay` | You (`/golden-replay`) | Verifies golden replays; when outcomes changed on purpose, bumps `SimVersion`, regenerates the files through the CLI and logs why. The only way golden files change. (A6) |
| `desync-hunt` | Claude or you | Finds the first diverging tick between two runs (threads, OS, runtime), bisects to the system and entity, and reports likely causes. |
| `perf-check` | Claude or you | Runs the benchmarks in Release, compares them with `sim/bench/budgets.json`, and reports regressions. |
| `unity-tests` | Claude or you | Runs Edit Mode and Play Mode tests through the Unity MCP server when the editor is open, or in batch mode when it is closed, and summarises failures. |
| `art-export` | Claude or you | Runs the headless Blender export and validation scripts for changed `.blend` files, then triggers VAT baking in Unity. |
| `adr` | You | Writes an architecture decision record in `docs/adr/` (context, options, decision, consequences). |

Skills can inject live context with `` !`command` `` (for example the milestone skill pulls `git log` and the status file), and can pre-approve the commands they need with `allowed-tools`. Keep each `SKILL.md` under 500 lines and put long reference material in supporting files next to it.

### 5.6 MCP servers

| Server | Choice | Notes |
|---|---|---|
| Unity editor | **MCP for Unity** (CoplayDev, free, MIT; package `com.coplaydev.unity-mcp`) as the default. Alternative: Unity's official MCP server in the AI Assistant package (`com.unity.ai.assistant`), in open beta since May 2026, which needs a Unity Cloud project and a Unity AI trial or subscription. | Lets Claude read the console, inspect and edit scenes and components, create scripts, refresh assets and run tests. Install the package, then use its "Configure detected clients" menu for Claude Code. Its configuration holds machine-specific paths, so keep it in local scope, not in the shared `.mcp.json`. |
| Blender | **blender-mcp** (`uvx blender-mcp` plus its Blender add-on) | Scene inspection, object and material edits, Python execution inside Blender. The add-on's socket has no authentication, so keep it on localhost; set `BLENDER_MCP_SAFE_MODE=1` and `DISABLE_TELEMETRY=true`; save before asking for big edits. Production exports still go through the headless scripts in `tools/blender/`, not through MCP. |
| GitHub | `gh` CLI (no MCP needed) | Issues, pull requests, CI logs. |

### 5.7 Hooks

Hook scripts are Python 3 with the standard library only, so they run the same on Windows and Linux. Configuration is in A2 and the scripts in A3.

| Event | Matcher | Script | Effect |
|---|---|---|---|
| `SessionStart` | `startup\|resume\|clear` | `session_start.py` | Adds the current milestone, its next three unchecked items and the branch to Claude's context. |
| `SessionStart` | `compact` | `session_start.py --short` | Re-injects the golden rules after context compaction. |
| `PreToolUse` | `Edit\|Write` | `guard_files.py` | Denies edits to golden files (use `/golden-replay`) and hand-written `.meta` files; asks before edits to `ProjectSettings/`, `Packages/manifest.json`, Unity scene/prefab YAML and `docs/design/game-brief.md`. |
| `PreToolUse` | `Bash` | `guard_bash.py` | Denies force-pushes, `--no-verify` and deleting outside the repo; asks before `git reset --hard`. (`git push` itself asks through the permission rules.) |
| `PostToolUse` | `Edit\|Write` | `sim_lint.py` | For `sim/src/**/*.cs`: scans for banned constructs and returns them to Claude (exit 2) so it fixes them at once. For any `.cs`: runs CSharpier on the file. |
| `Stop` | (all) | `stop_checks.py` | If `sim/` or `data/` changed in the working tree, runs the fast test suite; on failure, blocks the stop with the failing tests so Claude keeps fixing (respects `stop_hook_active` to avoid loops). |
| `Notification` | (all) | user-level, optional | Desktop notification when Claude needs input. Put it in your own `~/.claude/settings.json`. |

### 5.8 Permissions

Pre-approve the safe, frequent commands (`dotnet build/test/run`, `dotnet csharpier`, read-only `git` commands, the hook and tool scripts, the Unity and Blender MCP tools), ask before `git push`, and deny reading secrets and force-pushing. See A2.

### 5.9 GitHub integration (optional, from M1)

Run `/install-github-app` in Claude Code to add the Claude GitHub App and a workflow, so `@claude` works in issues and pull requests, plus an automatic review workflow. Point the review at `CLAUDE.md`'s golden rules. Keep `--max-turns` low and the workflow timeouts short to control cost.

### 5.10 A normal working session

1. `/milestone` → pick one unchecked item → create a branch (`m3/rain-power`).
2. Plan mode: Claude reads the relevant rules and design docs and proposes a plan; you adjust it.
3. Claude delegates (for example `sim-engineer` implements, `test-engineer` adds the scenario test); the hooks lint and test as it goes.
4. Before the pull request: `determinism-auditor` (simulation changes), `/perf-check` (hot paths), `code-reviewer`, and `/golden-replay` if outcomes changed on purpose.
5. Merge when CI is green; tick the item in `STATUS.md`.
6. Monthly: run `/doctor prompt-audit` to find outdated or conflicting instructions in `CLAUDE.md`, rules, skills and agents.

---

## 6. Testing strategy

The simulation is where correctness lives, and it runs headless, so most testing happens in seconds with `dotnet test`, without Unity. Unity tests cover the bridge, the tools and the picture.

### 6.1 Layers

| Layer | What it checks | Where | When |
|---|---|---|---|
| Unit | Fixed-point arithmetic against exact references (BigInteger), RNG streams, hashing, containers, field kernels | `GodGame.Sim.Tests` | Every edit (Stop hook), CI |
| Property | Invariants over random inputs (CsCheck or FsCheck): arithmetic error bounds; the reachability check never accepts a full enclosure; flow fields always resolve; soldiers are conserved (alive + dead + fled = start) | `GodGame.Sim.Tests` | CI |
| Determinism | Same plan twice → same hashes; snapshot, restore and continue → same hashes; 1, 2 and N threads → same hashes | `GodGame.Sim.Tests` | Every edit, CI |
| Cross-platform | Windows (players) vs Linux (PvP verifier) hash files compared in CI; Unity Mono editor and IL2CPP player replay the goldens and must match (from M1) | CI + Unity Play Mode | Every push (OS), nightly (Unity) |
| Golden replays | A library of battles with per-second hashes and summary statistics; any change in outcome fails until `/golden-replay` bumps `SimVersion` | `GodGame.Sim.Scenarios`, `sim/tests/golden/` | CI |
| Scenario (design) | Each brief decision as an executable test, asserting on cause events and statistics (Appendix B) | `GodGame.Sim.Scenarios` | CI |
| AI behaviour | Route choice, waiting out rain, rerouting, garrison assignment, hunting and escorting specialists, defender openings; decision margins reported | `GodGame.Sim.Scenarios` | CI |
| Stability | Perturbation: move each placement ±2 m and each cast ±1 s; the outcome may not swing past a set limit; decisions won by under 10% become hinge markers | `godsim perturb` | Every campaign level, nightly |
| Fuzz and soak | Random valid plans on test maps; invariants (no exceptions, nobody inside a closed cell unless launched or swept, no regiment stuck without a cause, conservation, rerun hash equal) | `godsim fuzz` | Nightly |
| Performance | BenchmarkDotNet suites compared with `sim/bench/budgets.json` | `GodGame.Sim.Bench` | `/perf-check` locally; nightly on the floor laptop and the recommended machine |
| Unity Edit Mode | Map export round trip, rules import, VAT baker output, asset import presets | `unity/Assets/Tests/EditMode` | `unity-tests` skill, CI from M2 |
| Unity Play Mode | SimHost replays goldens with matching hashes; 10,000-instance render smoke test; frame-time capture; replays redraw identical launch arcs | `unity/Assets/Tests/PlayMode` | `unity-tests` skill, nightly |
| Visual | Screenshots of fixed replay frames compared with a tolerance (from M8) | Play Mode | Nightly |
| Playtests | Design gates A, B and C (section 9) | People | At the gates |

### 6.2 Test categories

`Fast` (whole set under about 30 s; run by the Stop hook), `Slow`, `Golden`, `Perf` and `Fuzz`. CI runs everything except `Perf` and `Fuzz` on every push; a nightly workflow runs all of them.

### 6.3 Scenario test builders

Scenario tests read like the design:

```csharp
[Test, Category("Fast")]
public void Decision_2026_10_08_WeakIceBreaksUnderCrowds_ThinFileCrosses()
{
    var map = MapBuilder.Flat(600, 300)
        .FrozenRiver(x: 300, width: 40, iceMm: 120)
        .Deployment(Side.Attacker, x: 50).Deployment(Side.Defender, x: 550);
    var plan = PlanBuilder.For(map)
        .Regiment(Side.Attacker, UnitType.Infantry, count: 60, formation: Formation.File)
        .MarchTo(x: 550);
    var run = Sim.Run(map, plan, seconds: 120);
    Assert.That(run.Causes.Of<IcePatchBroke>(), Is.Empty);
    Assert.That(run.Stats.Swept, Is.Zero);
}
```

Each scenario test that matters is also saved as a golden replay, so later changes that alter it are caught even if its assertions still pass.

---

## 7. Art pipeline (Blender → Unity)

### 7.1 Look and budgets

From `unit-spec.md`: TABS-level detail with the game's own look; few, simple parts; flat colour from one shared palette texture, no normal maps; gear carries identity; two to four seeded variants of helmet, build and shade per type; team colour on big areas (tabard, shield face, plume); one officer or standard-bearer per regiment with a tall plume or banner; cosmetics never change silhouette, size or team-colour areas; playful tone, no gore.

| Level of detail | What is drawn | Starting budget |
|---|---|---|
| Hero (near camera, a few hundred soldiers) | Full parts and gear, ground-driven clips, tumbling launches | 1,000–2,500 triangles with gear |
| Mid | One merged mesh with gear baked in | 300–600 triangles |
| Far | Simplified mesh or impostor card: team colour and type silhouette | under 100 triangles |
| Strategic zoom | Shaded dots or regiment shapes, tinted by morale and state | procedural, no art |

These are High-preset budgets. The Low preset (integrated graphics, 20 fps) caps full-detail soldiers at about 100 near the camera and switches to mid, far and dot representations sooner; tune both on the floor laptop.

### 7.2 Look-development spike (art direction is still open)

Before mass production (early in M8, or in parallel from M2): build one infantry and one cavalry model in each candidate look (painted tabletop miniatures, carved and painted wood, lacquered tin soldiers), drop them into a 10,000-unit test battle, and judge them at strategic and mid zoom. Choose by silhouette readability at strategic zoom (the 2026-10-10 review: the veteran thought carved wood reads chunkiest; the casual player liked tin soldiers). Record the choice in the brief.

### 7.3 Blender conventions

- 1 Blender unit = 1 metre; model Z-up; the export script handles axis conversion and applies transforms, so nobody sets export options by hand.
- One material per mesh; UVs snap into cells of the shared palette texture; the team-colour area is marked in a vertex-colour channel.
- Names: `U_<type>_<variant>_LOD<n>` (units), `P_<prop>_<state>` (props), `A_<rig>_<clip>` (animations). One collection per LOD.
- Rigs: one humanoid rig (about 20 bones) shared by infantry, ranged, crews and specialists; one quadruped rig shared by horses and herd animals; siege engines as rigid parts with a few bones (arm, wheels).
- Sources live in `art/source/` under Git LFS; exported FBX files live under `unity/Assets/_Game/Art/Generated/` (also LFS) and are never edited by hand.

### 7.4 Animation clips (first set)

Walk, run, wade (raised arms), swept, brace, melee swing, shoot (bow, crossbow, sling), slip in mud, arms out on ice (wider as load nears breaking), high-step in snow, teeter at an edge, launched tumble, landing crumple, on fire (sooty, smoking, running), the death gags from `unit-spec.md` (drown: wave and sink; wave-front tumble and shield-surf; pancake peel-off; trampled flat; soot blink and crumble; drop through ice; buried hand; cliff windmill), stunned (dizzy), flee, death with an exaggerated throw-back, cheer; plus specialist work loops (dig, carry bundle, torch, pick, mallet, pontoon) and herd clips (graze, walk, mill and agitate, stampede). A seeded phase and stride per soldier keeps ranks out of step.

### 7.5 Export and validation

- `blender -b <file.blend> --python tools/blender/export.py -- --out <dir>` exports each LOD and the clips to FBX.
- `tools/blender/validate.py` checks triangle budgets, material count, UVs inside the palette, applied transforms, names, bone counts, and clip names and lengths, and writes a JSON report. The `art-export` skill runs both for changed files.
- Optional later: a CI job runs headless Blender on Linux to validate every changed `.blend` file.

### 7.6 Into Unity

- An `AssetPostprocessor` enforces import settings per folder (no material import, the right animation type, read/write enabled for baking).
- The **VAT baker** (an editor tool) samples each clip on each LOD mesh (30 fps for hero, 15 for mid), writes position and normal textures (RGBAHalf) and a clip table (start frame, length, loop), and assigns them to the unit's render data. The unit renderer picks the clip and phase per instance from simulation state, plus a rotation from the simulated spin for launched soldiers.
- Crowd variety: per-instance variant ids from the simulation seed select helmet, build and shade.

### 7.7 Props and their states

| Prop | States |
|---|---|
| Trees (forest tiles, single oaks) | standing, burning, burnt, falling (pivot), fallen |
| Bridge segments (wood, stone, pontoon) | intact, strained (cracks driven by load), broken pieces |
| Ice | shader only: thickness, cracks from load ratio, holes |
| Walls, hedgerows, palisades, towers | intact, burning, breached, collapsed |
| Dams and weirs | intact, overtopping, breached |
| Siege engines | moving, firing, stalled, wrecked (stamped into navigation) |
| Boulders, debris, felled trees | static props stamped into navigation |

Collapses may use Unity physics for falling pieces, but only as decoration after the simulation has decided the failure.

### 7.8 Terrain, water and weather looks

The terrain mesh comes from the simulation heightfield; terrain, water, ice, snow and fire shaders read the field textures (water depth and flow, wetness, mud, temperature, fuel and burn state, ice thickness, snow depth and packed trail, fog and smoke density) and add detail procedurally. Every warning the player relies on (strain stages, rising water, a fire's front) must stay readable at strategic zoom.

### 7.9 Blender MCP in practice

Good for blockouts, batch renames, checking a scene, quick variants and fixing validation failures interactively. The headless scripts remain the production path, so every export is reproducible.

### 7.10 Effects and sound

VFX Graph splashes, dust, debris, smoke and embers sized up to the exaggerated physics and fading quickly; nothing that blocks movement is ever only an effect. Audio is driven by cause events (creaks and cracks by strain stage, splashes, stampede rumble, wooden clacks on impact, a few reaction barks).

---

## 8. Performance budgets

The floor is set (brief, 2026-10-11): integrated graphics at 20 fps or better.

| Machine | Spec |
|---|---|
| **Minimum (floor)** | 2020-or-later laptop: Intel Iris Xe or AMD Radeon 680M-class integrated graphics, 4-core/8-thread CPU, 8 GB memory shared with the GPU. Older integrated chips (Intel UHD 620 class) are below spec. |
| **Recommended** | 6-core desktop CPU (2020+), mid-range dedicated GPU, 16 GB. |

| Item | Minimum (Low preset) | Recommended (High preset) |
|---|---|---|
| Frame rate (10,000 soldiers, fields, effects, 1080p output) | ≥ 20 fps, rendered at about 720p and upscaled | 60 fps at native 1080p |
| Simulation tick (10,000 units, all fields, largest map) | ≤ 6 ms, giving about 8× real time at 20 ticks per second | ≤ 3.3 ms, giving 15× real time |
| Preview after a cast edit: first 30 s / full 3-minute battle | ≤ 3 s / ≤ 25 s | ≤ 1.5 s / ≤ 12 s |
| Preview after an army or terrain edit | navigation rebuild ≤ 100 ms; first 30 s ≤ 4 s | navigation rebuild ≤ 50 ms; first 30 s ≤ 2 s |
| Reachability check while dragging a piece | ≤ 10 ms | ≤ 5 ms |
| Memory for one battle's future (keyframes + playback track) | ≤ 1 GB in memory; older keyframes spill to disk | ≤ 1.5 GB for a 15-minute battle |
| Full-detail soldiers near the camera | about 100 | a few hundred |

Shared by every preset, because they decide outcomes: 20 ticks per second; 1.5 m field cells (about 430,000 cells on a 1.2 × 0.8 km map) ticked at 5 Hz on active tiles; keyframes every 5 s (≤ 4 MB each before delta compression); per-seat soldier caps in PvP (brainstorm: about 8,000 attacker, 1,500 defender), which keep round 5 inside the floor budget too.

Graphics presets change presentation only, and every warning (strain cracks, rising-water tint, fire fronts, landing spreads, the no-cast ring) must read on Low.

`sim/bench/budgets.json` holds both columns; the `perf-check` skill reports against them. Get a floor laptop before M2 and run the nightly benchmarks and the M8 frame-rate tests on it.

---

## 9. Milestones

The order follows risk: determinism, scale and the preview loop first, because everything else depends on them and they are the hardest to retrofit; art and content later, when the systems they dress are stable. Sizes are relative (S < M < L < XL); calibrate them after M1 from how long M0 and M1 actually took.

| # | Milestone | Proves | Size | Gate |
|---|---|---|---|---|
| M0 | Foundations | The repo, toolchain and Claude Code setup work end to end | S | |
| M1 | Deterministic core | Bit-identical battles across runs, threads, OSes and runtimes | M | |
| M2 | Armies that march | 10,000 units on flow fields within the tick budget | L | |
| M3 | Water: first field, first power, first deaths | A full consequence chain: rain → flood → swept → cause trace | L | |
| M4 | The timeline and the scrubbable future | Fluid plan–scrub–replan loop | L | **A: is planning fun?** |
| M5 | Combat, morale, load, launches and the first specialists | The signature moments (crowds break bridges, stampedes, landing preview), each with a unit-side answer | XL | **B: first vertical slice** |
| M6 | The full world and the counter web | All launch fields and every counter-web row | XL | |
| M7 | AI and specialists | Readable, adaptive attacker; exploiting defender; all nine specialists hunted and escorted | L | |
| M8 | Presentation and art | The toy-box look at 60 fps with 10,000 soldiers | XL | |
| M9 | Campaign vertical slice | Chapter 1, teachable and replayable | L | |
| M10 | Attack mode and the AI god | Round play against an adaptive, fair AI god | L | **C: is attacking fun?** |
| M11 | PvP prototype | Seat balance and zero desyncs | L | **D: seat balance** |
| M12 | Content, balance, Early Access readiness | A shippable Early Access build | XL | |

```
M0 → M1 → M2 → M3 → M4 → M5 → M6 → M7 → M9 ─────────────→ M12
                              └──→ M8 (art, parallel from M5) ──┘
                                             M7 → M10 → M11 ────┘
```

Each milestone's checklist is copied into `docs/milestones/STATUS.md` when it starts; tick items there as they merge.

### Design spec track (runs ahead of the code)

The design docs describe what each unit, power and AI does in words; the code needs numbers. Five spec passes turn the design into numbered specs and rules data, each finished before the milestone that needs it. They're written with the `design-lead` agent and the `game-design` skill, kept in `docs/design/` (and the claude.ai project), and mirrored as `data/rules/*.json` so tests can use them.

| Spec | Needed by | Specifies | Output |
|---|---|---|---|
| **S1 — Water and the first power** | M3 | Rain (area, wind-up, duration, water added), river and mud rules, the first terrain pieces (channel, ridge, spring, boulders) | `powers.json`, `terrain.json`, `fields.json` v1 |
| **S2 — The base army** | M5 | The four base types (infantry, cavalry, ranged, siege): regiment size, mass and load, speeds per movement class, melee and volley values, charge and launch response, morale, swept and burn thresholds; siege engines (move, fire, crew); the first six specialists (Spademen, Fascine Carriers, Pontoon Train, Drovers, Crossing Marshals, Engineers: action, trigger rule, rate, charges, seat lock, price); herds; Lightning and Rot; bridges, hedgerows and drystone walls (as blockers), fixed ice | `docs/design/roster.md`; `units.json`, `specialists.json`, `projectiles.json` v1 |
| **S3 — The launch powers** | M6 | The launch dozen powers and built pieces with area, wind-up, duration, fields written, energy cost and counters; the field interaction table v1 | `docs/design/launch-powers.md` (the selection and numbers; seeds stay in the power palette); `powers.json`, `terrain.json`, `fields.json` v2 |
| **S4 — The attacker and the specialists** | M7 | The campaign attacker's behaviour sheet (route choice, waiting, rerouting, escorts, when it routs); the defender's openings; the remaining specialists (Firebrands, Sappers, Wheel-Breakers) and the rules for hunting and escorting specialists | `docs/design/attacker-behaviour.md`; `roster.md` v2; `specialists.json` v2 |
| **S5 — Economy and the AI god** | M10 | Prices in bodies and energy, upgrades (drill and kit), income per round and its growth, removal costs, soldier caps, sunset length; AI god personalities and the setup-library format | `docs/design/economy.md`; `economy.json`; one setup library per test map |

Which units, powers, terrain pieces and systems each spec covers, and what waits until after Early Access, is set out in `docs/design/content-roadmap.md` (waves 1–6 line up with S1–S5 and M12). Specs are first drafts: numbers get tuned through tests and playtests, and every change goes through the rules data (so the ruleset hash and the golden replays catch it).

### M0 — Foundations (S)

**Goal:** a repository where Claude Code can build, test and lint the simulation, Unity opens the project, and an asset flows from Blender into Unity.

**Steps** (Claude stops after each for review):
1. **Repository basics.** README, `.gitignore`, `.gitattributes` with LFS patterns and Unity Smart Merge, `.editorconfig`, `git lfs install`, branch protection on `main` (pull request + green CI). Configure UnityYAMLMerge as the merge driver in local git config (documented in the README).
2. **Docs.** Copy the design docs into `docs/design/`; write ADR 0001 (engine: Unity) from the brief's 2026-10-11 decision; create `docs/milestones/STATUS.md` (A11).
3. **Simulation solution.** The projects in section 3.2, `Directory.Build.props`, `BannedSymbols.txt`, `global.json` (.NET 10 SDK), CSharpier as a local dotnet tool, a placeholder simulation (tick counter, a few units, state hash) and first unit tests.
4. **CI.** `sim.yml`: build and test on Windows and Linux, upload hash files, compare them in a final job (A10).
5. **Unity project.** Unity 6.3 LTS URP project in `unity/` with the settings and packages of section 3.7; the local package reference to `sim/src`; asmdefs with `noEngineReferences`; `GodGame.Bridge` with a `SimHost` that ticks the placeholder simulation and moves a cube; a first Edit Mode test.
6. **Claude Code.** `CLAUDE.md`, the rules, the agents, the skills (`milestone`, `golden-replay` and `new-power` complete; the others as stubs with their checklist headings), hooks and scripts, permissions, `.mcp.json` (Blender). Install MCP for Unity locally. Port the `game-design` and `review-idea` skills and the three reviewer agents, changing doc paths to `docs/design/`.
7. **Blender.** `tools/blender/export.py` and `validate.py`; a test soldier (a capsule with palette UVs and a walk clip) exported and imported through the `AssetPostprocessor`.
8. **Verify** the exit criteria.

**Exit criteria**
- A fresh clone (with LFS) runs `dotnet test` green locally and in CI on both operating systems, and the hash files match across them.
- Unity 6.3 opens the project without errors; entering Play Mode shows the cube driven by the simulation tick.
- Writing `float x = 1f;` in `sim/src` fails the build (analyzer), and the hook flags it as soon as Claude writes it.
- `/context` lists `CLAUDE.md` and the rules; `/agents` lists the agents; `/milestone` prints M0's status.
- Through MCP, Claude can read the Unity console and list the objects in the Blender test scene.
- The export script produces the test FBX and a clean validation report.

### M1 — Deterministic core (M)

**Goal:** the deterministic skeleton everything else hangs on.

**Build**
- `GodGame.Sim.Math`: `Fix` (64-bit, 16 fractional bits), 2D/3D vectors, integer square root, binary angles and trig tables, PCG32 streams, xxHash64; exhaustive and property tests.
- World state in structure-of-arrays form (position, velocity, type, regiment, health, morale, state flags), the regiment table, entity ids with deterministic reuse.
- The tick scheduler with the fixed system order (empty systems registered), `IParallelRunner` (.NET implementation), rates and sub-rates.
- Spatial hash.
- Map format v0 (heightfield and deployment zones), plan format v0 (army layer; casts stubbed), ruleset loader v0 (unit types) and ruleset hash.
- Versioned snapshots, state hashes every N ticks, replay file v0.
- `godsim` CLI: `run`, `hash`, `compare`.
- Determinism harness: twice the same, snapshot and restore, thread counts, cross-OS in CI; a Unity Play Mode test that replays a golden in the editor (Mono); an IL2CPP Windows player that replays the goldens from the command line (by hand now, nightly from M2).
- The golden replay infrastructure and the `golden-replay` skill working end to end; `SimVersion` and `CHANGELOG-sim.md`.

**Exit criteria**
- 10,000 units wandering on seeded paths for three simulated minutes produce identical hash sequences across: two runs, snapshot and restore, 1 vs 8 threads, Windows vs Linux, and .NET vs the Unity Mono editor vs the IL2CPP player.
- At least 15× real time with the placeholder systems; the baseline is recorded in `budgets.json`.
- A `determinism-auditor` review of the whole simulation with no open high-severity findings.

### M2 — Armies that march (L)

**Goal:** armies totalling 10,000 soldiers march, bunch and queue across a real heightfield, within the tick budget.

**Needs first:** the map scale, and a floor test laptop (integrated graphics, section 8) so the budgets are measured on real minimum-spec hardware from here on.

**Build**
- Heightfield terrain, navigation grid, movement classes (foot, horse, wheeled, water), per-class cost fields.
- Flow fields per regiment goal with incremental tile rebuilds; local steering (separation, alignment, obstacle avoidance) and crowd pressure through the spatial hash; formation slots; regiments of 50–200 with an officer or standard-bearer as anchor.
- A distance-to-lethal field (cliffs and the map edge for now).
- The reachability check: coarse grid, breadth-first distance fill, clearance map, the 2× route cap, route overlay data.
- Route preview: the attacker's predicted path drawn from the flow fields.
- Unity: debug renderer (indirect instanced capsules tinted by regiment), flow-field and cost overlays, a camera with strategic zoom, map authoring v0 (heightmap import, deployment-zone painter, exporter).
- Unity CI with GameCI for Edit and Play Mode tests (A10 notes).
- The Burst decision: benchmark the hot kernels on .NET, IL2CPP and Burst, and record the result in an ADR.

**Exit criteria**
- 10,000 attackers cross 1 km through a bridge chokepoint; congestion emerges and regiments queue rather than overlap.
- The reachability check rejects a full trap and a route longer than 2× in ≤ 5 ms per drag.
- The tick stays within both budgets at 10,000 units (fields not yet on), on the floor laptop and the recommended machine, and stays deterministic across the M1 matrix.
- The debug view runs at 60 fps with 10,000 capsules on the recommended machine and at 20 fps or better on the floor laptop.

### M3 — Water: first field, first power, first deaths (L)

**Goal:** rain floods a ford and the attacker pays for it, through the whole consequence chain.

**Specs first:** S1.

**Build**
- The field framework: integer grids, active-tile tracking, sub-rate scheduling, the interaction table as data, field snapshots, upload to GPU textures, debug shaders.
- Water: virtual-pipes shallow water on the heightfield, river sources and sinks, flow speed; wetness; mud that worsens with traffic (the army ruts its own road).
- Units read the fields: speed by depth and mud per movement class; the swept state with drowning timer and bank exit; lethal cells closed in the flow field; distance-to-lethal rebuilt on change; crowd pressure can push soldiers over an edge; teetering.
- The casts layer of the plan; the Rain power (area, wind-up, duration) with the range rule at the firing tick and fizzle reasons.
- Terrain pieces v0: dig a channel, raise a ridge, add water or a spring, set boulders, all through the reachability check.
- Cause log v1 (cast executed → river overflowed → regiment swept → soldier drowned) and a causality-trace query.

**Exit criteria**
- Scenario tests pass: rain upstream at 0:50 floods the ford by about 2:40, and the attacker reroutes or arrives at least 60 s later; a dug channel plus rain becomes a moat; every drowned soldier traces back to the cast.
- The never-walk-into-hazards fuzz invariant holds: no soldier enters a lethal cell under its own steering.
- The field step stays within budget with water active on the largest test map.

### M4 — The timeline and the scrubbable future (L)

**Goal:** plan, scrub and re-plan fluidly. The preview is the game.

**Needs first:** the battle length (sunset) cap and whether foresight is limited. The plan schema reserves world-field triggers in case the open question on event-pinned casts goes that way.

**Build**
- Keyframes every 5 s; re-simulation from the earliest change; the preview worker in `SimHost`; progressive fill with a buffer bar and first-30-s priority; the playback track (5 samples per second plus launch events) and field snapshots for scrubbing.
- Range-rule re-validation of later casts after every re-simulation, with red markers and reasons.
- Ghost of the previous plan; hinge markers (AI decisions won by under 10%); the causality trace in the UI (click a drowned regiment, see the chain).
- Planning UI v0 in UI Toolkit: place regiments and terrain pieces, paint the rain area, drag casts along the timeline, held-key overlays, the no-cast area drawn as a ring.

**Exit criteria**
- Moving a cast updates the first 30 s of the preview in ≤ 1.5 s and a full 3-minute battle in ≤ 12 s; scrubbing holds 60 fps.
- An invalid cast turns red with its reason within one re-simulation.
- **Design gate A: is planning fun?** Five people play three grey-box puzzles built from rain, channels and boulders. Pass: they read the 100 m rule as a bank shot rather than an error, use scrubbing without being told, and ask for another level.

### M5 — Combat, morale, load, launches and the first specialists (XL)

**Goal:** the signature moments work: crowds breaking bridges and ice, stampedes, charges, launches and the landing preview.

**Needs first:** per-regiment stance at deployment (open or close order), and the campaign win condition (implement both survival and objective variants as data until decided). **Specs first:** S2.

**Build**
- Melee through the spatial hash; regiment volleys from the projectile tables, with wind along the shot (prevailing wind plus a simple cast plume) and height; morale per regiment with contagion, casualty events, routing; fleeing off the map and regiments under about 10% count as wiped.
- Hedgerows and drystone walls as navigation blockers (burning and toppling come in M6), so funnels exist for the vertical slice.
- The shared specialist executor (actions as data, charges on the regiment, per-action reachability validation, markers on regiment lanes, unit-made edits through the incremental navigation and heightfield path) and the first six specialists from S2: Spademen, Fascine Carriers, Pontoon Train, Drovers, Crossing Marshals and Engineers (trestle bridges, repairs, clearing), each acting on a simple fixed trigger. Hunting and escorting specialists waits for M7.
- The load system: bridge segments with load against strength, rot; ice as a fixed-thickness field until M6, checked per patch; one strain language (creak at 70%, crack at 85%, sag at 95% of strength) exposed to shaders and UI; collapse into launches and the swept state.
- Launches: ballistic bodies with reduced gravity, landings resolved by surface, a 2 m shallow fringe that only stuns, a knock-down for neighbours that never chains; launched and stunned soldiers stay in their regiment's outline; landing spreads aggregated for the preview.
- The four base types from S2: infantry and ranged regiments, cavalry, and siege engines (wheeled movement, crews, firing stones from the projectile table, launching what they hit; their targets among built pieces arrive in M6).
- Cavalry charges that trample (damage) and launch; herds (agents, grazing, occupancy in the congestion cost) and stampedes past a heat threshold, which trample and launch the same way; Lightning v0 (fells a tree that is stamped into navigation; ignites dry fuel minimally).
- Unity: strain overlay, landing-spread overlay, launch arcs from playback events, morale tints at strategic zoom, a results screen that names causes.

**Exit criteria**
- Scenario tests pass: a thin file crosses a bridge (and fixed ice) that a dense column breaks; a rotted bridge fails only under the bulk of the army; lightning near a herd stampedes it into a column, and the landing-spread preview matches the live result exactly; a charge or stampede tramples and launches soldiers but never chains; each first specialist answers part of a setup (fascines fill one crossing through a channel, a pontoon spans a flooded ford until a surge breaks it, marshals meter a rotted bridge so it holds while the bank queues, drovers walk a herd off the road, Spademen's ditch becomes a moat in the rain, engineers span a channel with a trestle, repair a rotted span and clear a felled tree).
- **Design gate B: first vertical slice.** One grey-box campaign level from deployment to results, with rain, lightning and wind. Pass: testers can explain why they won or lost.

### M6 — The full world and the counter web (XL)

**Goal:** the launch set of fields and powers, with every counter in the palette working through the fields.

**Needs first:** wind reach past the cast area, prevailing wind per map, whether ice stops a river's flow, and whether morale or omen powers count as reaching the army. **Specs first:** S3.

**Build** (in the review's order, so PvP can launch on the first four systems):
1. Temperature (heat and cold in one field) and ice: freezing and melting, thickness, per-patch load.
2. Fuel and fire: cellular spread damped by wetness, driven by wind, capped below infantry running speed on open ground; soldiers a front reaches burn (burning state and timer); regiments evacuate an approaching front by a fixed rule; smoke.
3. Wind: analytic plumes with reach and a shelter mask; arrows read it.
4. Load and slopes generalised: dams, mine workings, scour; landslides, and boulders a tremor sets rolling down the fall line (deterministic rolling bodies, rumble warning, path in the preview).
5. Fog and smoke advection, then snow (depth, drifts, trail-breaking, melt into water, avalanches through slope load); these two can ship as campaign chapters.
- Powers as data from the palette: the launch dozen from `content-roadmap.md` (Rain, Lightning and Rot from earlier milestones, plus Spring, Dam break, Wither, Wind shift, Calm, Cold snap, Heat wave, Tremor and Thunderclap).
- Built pieces: walls, hedgerows, towers, palisades, dams, herd pens, bridges; garrisons (slots, height bonus, cover, blinded by smoke, structure strength).
- Every row of the palette's counter-web table, and each listed turnabout, becomes a scenario test.

**Exit criteria**
- Every counter-web scenario passes, including the cost difference between a pre-emptive and a reactive counter (rain before a fire vs on a raging one).
- The tick stays within budget with all fields active on the largest map.

### M7 — AI and specialists (L)

**Goal:** an attacker that adapts readably, a defender that exploits openings, and world-shaping specialists through one executor.

**Needs first:** fixed or adaptive campaign attacker (the reviews recommend fixed and learnable); whether fire costs energy. **Specs first:** S4.

**Build**
- Attacker: route scoring from cost fields, rerouting, waiting out rain or a high river, engineers clearing debris, escorts for specialists; the campaign attacker as fixed, readable rules with a published behaviour sheet; hinge margins exposed to the preview.
- Defender: garrison assignment (brief rules); a small set of openings it exploits (counterattack a split column, hold a ridge, fall back behind a flood); it values keeping regiments alive; noticed openings shown in the preview.
- Specialists: the remaining three on the executor from M5 (Firebrands, Sappers, Wheel-Breakers), rising prices per extra regiment of the same specialist, both AIs hunting and escorting specialists by fixed, visible rules; Firebrand ignition rules (dry fuel, wind away from their side, no soldier within 30 m; the review also suggests 60 m from herds).
- `godsim perturb` (stability contract) wired into CI for test maps.

**Exit criteria**
- Behaviour-sheet tests pass; the perturbation test passes on all test maps.
- 50 fuzzed battles reviewed against an AI blunder checklist (stuck regiments, ignoring an open flank, walking into the obvious) with none found.

### M8 — Presentation and art (XL; runs in parallel from M5)

**Goal:** the toy-box look at 60 fps with 10,000 soldiers, with every warning readable.

**Build**
- The look-development spike and the art direction decision (section 7.2).
- The full Blender pipeline, the VAT baker, the LOD ladder; the indirect renderer with compute culling; per-instance surface state (wet, mud, frost, soot, snow).
- Death gags per cause (unit spec table): a data table from cause to clip, shader parameters (flatten, char, sink) and instanced props; floating gear on the water-flow field and ash on the wind field; prop caps per preset.
- Terrain, water, ice, snow and fire shaders on field textures; VFX Graph effects; strategic-zoom forms of every warning; banners as wind gauges; dizzy stars over the stunned; the drowned bobbing along the current; sound driven by cause events.
- The highlight camera (off, end-of-battle reel by default, live cuts, picture-in-picture; slow motion as a toggle) with Cinemachine; clip export (video encoded from the playback track, plus a replay file); the splash ledger.

**Exit criteria**
- 60 fps at 1080p with 10,000 soldiers and all field shaders on the recommended machine (High preset), and 20 fps or better on the floor laptop (Low preset) with every warning still readable.
- A Play Mode test shows the preview, the live battle and a replay drawing identical launch arcs.
- The no-gore checklist passes; a 20-second highlight clip exports from a replay.

### M9 — Campaign vertical slice (L)

**Goal:** chapter 1, teachable, fair and replayable.

**Needs first:** the campaign win condition, the campaign army (fixed or budget), and phase locking.

**Build**
- The level format and level-editor tools; per-phase constraints; the teaching ladder (Spill, Spark, Gust, Wet line, Loosen a plank, Open the sluice, Chill a pond, Nudge a log); the "Fix the Apprentice's Plan" format; three side quests per level ticking live in the preview; stars unlocking cosmetics; a results screen with histograms; hints that point without solving.
- A brute-force solver that confirms no single power wins a level alone; the perturbation check per level; saves.

**Exit criteria**
- Chapter 1 (8–10 levels) is playable from start to finish; every level passes the solver and perturbation checks.
- An external playtest (10 people) meets the "first hour" checklist from the gamer review.

### M10 — Attack mode and the AI god (L)

**Goal:** the singleplayer attack mode (brief, 2026-10-11): the player leads the big army against an AI god who prepares terrain and powers and changes them between rounds; the player buys, places and upgrades units in response. It builds the round structure PvP reuses, and the AI god becomes PvP's practice bot.

**Needs first:** attack mode format (five rounds, or a run across maps) and whether the player gets a small energy pool; budget growth and removal cost (first values, tuned here and again in M11). **Specs first:** S5.

**Build**
- Round play shared with PvP: five rounds, each replaying from the start; two limited budgets per seat (bodies and energy) with income growing each round, spending capped at what the seat has and unspent income lost; queued powers paid once and repeated in later rounds; carry-over; removal costs; hidden new placements revealed at battle start; the combined reachability check at lock-in with the fixed gap rule; the sunset limit; the wipe rule.
- The army shop: buying regiments, placing them, bought upgrades (drill and kit levels per regiment, hidden like new placements), specialists.
- The AI god, a between-rounds planner that runs in the background while the player plans:
  - It sees only what a human opponent would: earlier rounds' battles and carried-over units, never the player's new purchases.
  - It picks from a per-map **setup library** (designer-authored terrain-and-power setups tagged with what they counter: heavy cavalry, massed infantry, siege, specialists) and adapts their parameters to the army it has seen.
  - It checks its best two or three candidates with short preview simulations against the player's known army, within its budget, and commits the best. Seeded and deterministic, so a replay of the match reproduces its choices.
  - Personalities (storm god, fire god, builder) weight the library, so its style can be learned; difficulty comes from budget growth and personality, never from extra information.
- The reveal: at battle start, the AI's new placements appear one by one; the end-of-battle reel shows which ones mattered.

**Exit criteria (design gate C: is attacking fun?)**
- Against each personality on two maps, testers can say after each round what the god changed and why, and adjust their army in response.
- The AI god never uses hidden information (a test feeds it identical histories with different hidden purchases and checks it chooses the same setup).
- Its planning finishes within the player's typical planning time on the floor laptop.

### M11 — PvP prototype (L)

**Goal:** prove seat balance before building anything around PvP (the review's "prototype first" call).

**Needs first:** budget growth per round, removal cost, sunset length, seat assignment.

**Build**
- Match flow: the round play from M10 between two humans with fixed seats.
- Bots for internal matches: the AI god from M10 in the defender seat, plus a simple attacker bot.
- Backend ADR. Recommended: a small .NET match service on Linux that exchanges plans and re-simulates each round to verify it, with Steam for identity and friends.
- Desync detection and reporting, replays, the loser's reel with hidden pieces revealed in order, a per-seat rating stub.

**Exit criteria (design gate D)**
- On one map with 12 powers and 7 unit types, the attacker wins 40–60% of 30 internal matches, and the median match reaches round 4.
- 1,000 automated matches run with zero desyncs across Windows and Linux.

### M12 — Content, balance and Early Access readiness (XL)

**Build:** more chapters (one new field per chapter); more AI god personalities and setup libraries for attack mode; roster growth from the brainstorm shortlist (Throwback Pikes, Testudo Cohort, Stilt Slingers, Hill Wheel, Hounds, Straw Legion, Rally Drum, Crossing Marshals, Remount String); balance tooling (bot leagues, telemetry); accessibility (colour-blind-safe teams and overlays, remappable keys); graphics settings that change presentation only; Steam integration; localisation-ready UI; crash reporting; the move to the next Unity LTS and later Unity 7 (CoreCLR), guarded by the cross-runtime hash tests.

**Exit criteria:** an Early Access checklist: stability, performance on the minimum spec, onboarding, store assets.

---

## 10. Open questions that block build decisions

From the brief's open questions, ordered by when the build needs an answer. Until then the plan uses the working default and keeps the choice in data where possible. Record each answer in the brief (the `game-design` skill does this) and update this table.

| Question | Needed by | Working default |
|---|---|---|
| Camera and map scale | M2 | Maps around 1.2 × 0.8 km, 1.5 m field cells |
| Battle length (sunset cap) | M4 | About 3 minutes per campaign battle; hard cap 15 minutes |
| Is foresight limited? | M4 | Unlimited |
| Casts pinned to events, or clock only? | M4 (schema) | Clock only; the plan schema reserves world-field triggers (load, water level, temperature), which both gamer reviewers favour |
| Per-regiment stance at deployment (open or close order) | M5 | Data flag, off; both reviews recommend adding it |
| Campaign win and loss conditions | M5, M9 | Both variants as data: survival, and protecting an objective (the review's recommendation) |
| Wind reach past the cast area; prevailing wind; crosswinds | M6 | Plume reaches about 150 m past the area; prevailing wind set per map; only the along-shot part affects arrows |
| Does ice stop a river's flow? | M6 | Water keeps flowing under ice |
| Do morale or omen powers count as reaching the army? | M6 | Not in the launch set |
| Can the attacker capture towers and walls? Is a dam's crest a crossing? | M6 | Capture yes (recommended); crest not a crossing |
| Does fire cost energy? Which specialists first? | M7 | Bodies and charges only; the recommended seven |
| Small regiments and the no-cast bubble; a Support class | M7, M10 | Regiments under about 25 soldiers, kites and decoys don't block casts (brainstorm) |
| Attack mode: five-round match or a run across maps; does the player get a small energy pool? | M10 | Five-round match; yes, the PvP attacker's pool |
| Campaign attacker fixed or adaptive; campaign army fixed or budget; phase locking | M9 | Fixed and learnable; fixed army; phases stay editable |
| Does the 100 m buffer bind designer-placed pieces? | M9 | No, it binds the player only |
| Body and energy income per round and its growth, removal cost, sunset length, seat assignment and ratings | M10 (first values), M11 | Tuned in attack mode, then against gate D |
| Campaign energy per level; PvP planning timer | M9, M11 | A fixed energy budget per level; a 60–90 s timer in PvP only |
| Art direction | M8 (spike can run earlier) | Decided by the silhouette test (section 7.2) |
| Do cartoon death remains (pancakes, soot figures, floating gear) vanish or stay all battle? | M8 | Vanish after a few seconds |
| Are decoys hidden from the human opponent? | M12 | From the enemy AI in battle only |

---

## 11. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Field simulation blows the tick budget ("field soup") | Fields at a sub-rate on active tiles; PvP launches on four systems (water, temperature/fire/ice, wind, load); budgets enforced by benchmarks; one new field per campaign chapter; a field ships only if three strong ideas need it. |
| Determinism regressions | Analyzer + hook + `determinism-auditor` + golden replays + the cross-thread, cross-OS and cross-runtime matrix; the `desync-hunt` skill. |
| The preview feels slow | First-30-seconds priority, keyframes, incremental navigation rebuilds, a battle length cap, a soldier cap per seat. |
| The AI looks dumb, and players stop trusting it | Fixed, readable rules with a behaviour sheet; hinge markers; a blunder checklist on fuzzed battles; the preview shows intent and noticed openings. |
| Small edits flip outcomes (deterministic but unstable) | Hysteresis on regiment decisions, smooth fields, seeded spreads tied to the source's heading, the perturbation test on every level. |
| PvP seats are unbalanced | Gate D before investing in PvP infrastructure; budget growth and removal cost as data knobs, first tuned in attack mode. |
| The AI god feels like a cheat, or is too expensive to run | It sees only what a human opponent would (tested); a per-map setup library plus a few short preview checks instead of open-ended search; readable personalities; a reveal and reel that explain each setup. |
| MCP tools change (Unity's official server is in beta; community tools move fast) | MCP is a convenience: everything also works through the CLI, batch mode and headless Blender scripts. |
| Unity's move to CoreCLR (no domain reload; 6.7 is the last Mono LTS) | No static state in Unity scripts; the engine-free simulation; cross-runtime golden tests before every engine upgrade. |
| Integrated graphics vary widely (the fastest are several times the slowest), and laptops throttle under sustained load | Pin a named floor device and test on it from M2; measure sustained (not burst) performance; Low preset caps that hold under throttling; warnings that never depend on High-preset detail. |
| Asset volume (54 unit ideas, many props) | Launch with the four base types plus nine specialists (six of them from the first set); shared rigs, one palette texture, VAT, procedural strategic-zoom dots. |
| Scope creep from the brainstorms (over 200 ideas) | `design-lead` checks every addition against the brief and pillars; ideas wait in the idea bank until a milestone needs them. |
| Claude Code instructions drift or bloat | `CLAUDE.md` under 200 lines; path-scoped rules; hooks and tests for anything critical; a monthly `/doctor prompt-audit`. |

---

## 12. Working agreements

- **Definition of done** for any checklist item: code and data merged through a pull request; tests added (a scenario test for any game rule); CI green; `determinism-auditor` passed for simulation changes; `/perf-check` run for per-unit or per-cell code; docs updated (this plan, the brief via `game-design`, ADRs); `STATUS.md` ticked.
- **Branches and commits:** `m<N>/<short-name>`; conventional commits (`feat(sim): …`, `fix(unity): …`); squash merge.
- **ADRs** for decisions that are expensive to reverse (Burst or not, backend choice, file formats).
- **Design docs sync.** The claude.ai project stays the place for design conversations; `docs/design/` in the repo is a copy. Re-copy the docs whenever a decision lands there (at the latest when a milestone starts). When a decision is recorded from Claude Code through the `game-design` skill, update the claude.ai project too, so the two never drift.
- **Golden files** change only through `/golden-replay`, with a `SimVersion` bump and a changelog line.
- **No gameplay in Unity, ever.** If a feature seems to need it, the simulation is missing something.

---

## Appendix A: starter files

Adapt names and paths as needed; these are starting points for M0.

### A1. `CLAUDE.md`

````markdown
# God game: project instructions

A god helps a weak army survive a far stronger attacker, using only indirect powers that act on the world (weather, water, terrain, vegetation), never on soldiers. Up to 10,000 units; a deterministic simulation with a scrubbable timeline preview; a puzzle campaign (defend as the god), an attack mode against an adaptive AI god, and an asymmetric auto-battler PvP. Unity 6 (URP) presents; an engine-free C# simulation decides everything.

## Where things are
- What the game is: @docs/design/game-brief.md (source of truth). Other design docs in docs/design/ load on demand.
- How we build it, and milestones: docs/implementation-plan.md; current status: docs/milestones/STATUS.md
- Simulation: sim/ (pure .NET; sim/src is also a Unity package). Unity project: unity/. Rules data: data/. Blender sources: art/.

## Commands
- Fast tests: `dotnet test sim/GodGame.Sim.sln --filter Category=Fast`
- All simulation tests: `dotnet test sim/GodGame.Sim.sln`
- Benchmarks: `dotnet run -c Release --project sim/bench/GodGame.Sim.Bench`
- Headless battle: `dotnet run --project sim/tools/GodGame.Sim.Cli -- run --map <id> --plan <file> --hash-every 20`
- Format: `dotnet csharpier format <path>` (the edit hook does this)
- Unity tests: the unity-tests skill (MCP when the editor is open, batch mode otherwise)

## Golden rules
1. The simulation decides every outcome; Unity only reads it and writes plans. No gameplay logic in Unity scripts; the simulation never reads Unity state.
2. Determinism in sim/: no float/double/decimal, System.Random, DateTime, Stopwatch, string.GetHashCode, unordered Dictionary/HashSet iteration, unstable sorts, ad-hoc threads or UnityEngine. Use GodGame.Sim.Math. Details in .claude/rules/sim-determinism.md.
3. A change that alters battle outcomes bumps SimVersion and regenerates goldens with /golden-replay. Never edit files in sim/tests/golden/ by hand.
4. Every simulation feature lands with tests, including a scenario test named after the brief decision it implements.
5. Performance first: run /perf-check after touching per-unit or per-cell code. Budgets: docs/implementation-plan.md section 8.
6. Don't invent game rules. Check the brief; if the rule is open there, use the plan's working default (section 10) behind a data flag and say so.
7. Small diffs, one checklist item per branch (m<N>/<name>), conventional commits.
8. Don't hand-edit Unity YAML (.unity, .prefab, .asset) or ProjectSettings unless asked; use the Unity MCP tools or editor scripts.
9. sim/src: C# 9, .NET Standard 2.1, no runtime NuGet packages (Unity compiles the source directly).

## Vocabulary
regiment · plan layers (army, terrain, casts) · range rule (100 m), fizzle · field · cause event, causality trace · keyframe, playback track · launch, landing spread · swept · teeter · strain stages (creak, crack, sag) · specialist, charges · seat (attacker, defender), round, lock-in, sunset

## Who does what
sim-engineer (simulation code) · determinism-auditor (before merging sim changes) · ai-engineer (regiment AI) · perf-engineer (budgets) · unity-engineer (Unity side) · tech-artist (shaders, VAT, VFX, Blender) · test-engineer (scenario tests, goldens, fuzz) · design-lead (game rules, decisions) · level-designer (campaign levels) · code-reviewer (before a pull request)

## Workflow
- Start with /milestone; use plan mode for anything beyond a one-line fix.
- Tick items in docs/milestones/STATUS.md when they merge.
````

### A2. `.claude/settings.json`

```json
{
  "$schema": "https://json.schemastore.org/claude-code-settings.json",
  "permissions": {
    "allow": [
      "Bash(dotnet build *)",
      "Bash(dotnet test *)",
      "Bash(dotnet run *)",
      "Bash(dotnet csharpier *)",
      "Bash(dotnet tool restore)",
      "Bash(git status *)",
      "Bash(git diff *)",
      "Bash(git log *)",
      "Bash(git branch *)",
      "Bash(git switch *)",
      "Bash(git add *)",
      "Bash(git commit *)",
      "Bash(python tools/*)",
      "Bash(gh pr view *)",
      "Bash(gh run view *)",
      "mcp__blender",
      "mcp__unity"
    ],
    "ask": ["Bash(git push *)"],
    "deny": [
      "Bash(git push --force *)",
      "Bash(git push -f *)",
      "Read(./.env)",
      "Read(./**/*.pem)"
    ]
  },
  "hooks": {
    "SessionStart": [
      { "matcher": "startup|resume|clear",
        "hooks": [{ "type": "command", "command": "python \"$CLAUDE_PROJECT_DIR\"/.claude/hooks/session_start.py" }] },
      { "matcher": "compact",
        "hooks": [{ "type": "command", "command": "python \"$CLAUDE_PROJECT_DIR\"/.claude/hooks/session_start.py --short" }] }
    ],
    "PreToolUse": [
      { "matcher": "Edit|Write",
        "hooks": [{ "type": "command", "command": "python \"$CLAUDE_PROJECT_DIR\"/.claude/hooks/guard_files.py" }] },
      { "matcher": "Bash",
        "hooks": [{ "type": "command", "command": "python \"$CLAUDE_PROJECT_DIR\"/.claude/hooks/guard_bash.py" }] }
    ],
    "PostToolUse": [
      { "matcher": "Edit|Write",
        "hooks": [{ "type": "command", "command": "python \"$CLAUDE_PROJECT_DIR\"/.claude/hooks/sim_lint.py", "timeout": 60 }] }
    ],
    "Stop": [
      { "hooks": [{ "type": "command", "command": "python \"$CLAUDE_PROJECT_DIR\"/.claude/hooks/stop_checks.py", "timeout": 600 }] }
    ]
  }
}
```

Notes: replace `mcp__unity` with the server name your Unity MCP package registers. On Windows, use `py` instead of `python` if that is how Python is installed. Check the permission-rule syntax against the current Claude Code docs if a rule doesn't match as expected.

### A3. Hook scripts (`.claude/hooks/`, Python standard library only)

**`session_start.py`**

```python
#!/usr/bin/env python3
"""SessionStart: add the current milestone, next items and branch to Claude's context."""
import os, re, subprocess, sys

root = os.environ.get("CLAUDE_PROJECT_DIR", ".")
short = "--short" in sys.argv
reminder = ("Reminder: the simulation decides and Unity only reads it; no floats or unordered "
            "iteration in sim/; outcome changes go through /golden-replay.")
lines = []
status = os.path.join(root, "docs", "milestones", "STATUS.md")
if not short and os.path.exists(status):
    text = open(status, encoding="utf-8").read()
    current = re.search(r"^## Current: (.+)$", text, re.M)
    if current:
        lines.append(f"Current milestone: {current.group(1)}")
    todo = re.findall(r"^- \[ \] (.+)$", text, re.M)[:3]
    if todo:
        lines.append("Next items: " + "; ".join(todo))
    try:
        branch = subprocess.run(["git", "branch", "--show-current"], cwd=root,
                                capture_output=True, text=True, timeout=10).stdout.strip()
        lines.append(f"Branch: {branch}")
    except Exception:
        pass
lines.append(reminder)
print("\n".join(lines))
```

**`guard_files.py`**

```python
#!/usr/bin/env python3
"""PreToolUse (Edit|Write): deny or ask for protected paths."""
import fnmatch, json, os, sys

event = json.load(sys.stdin)
path = (event.get("tool_input") or {}).get("file_path", "")
root = os.environ.get("CLAUDE_PROJECT_DIR", os.getcwd())
rel = os.path.relpath(os.path.abspath(path), root).replace("\\", "/")

DENY = [
    ("sim/tests/golden/*", "Golden files change only through /golden-replay (it bumps SimVersion)."),
    ("*.meta", "Unity generates .meta files; don't write them by hand."),
]
ASK = [
    ("unity/ProjectSettings/*", "Unity project settings"),
    ("unity/Packages/manifest.json", "Unity package manifest"),
    ("*.unity", "Unity scene YAML"),
    ("*.prefab", "Unity prefab YAML"),
    ("docs/design/game-brief.md", "the game brief (source of truth)"),
]

def decide(decision, reason):
    print(json.dumps({"hookSpecificOutput": {
        "hookEventName": "PreToolUse",
        "permissionDecision": decision,
        "permissionDecisionReason": reason}}))
    sys.exit(0)

for pattern, reason in DENY:
    if fnmatch.fnmatch(rel, pattern):
        decide("deny", reason)
for pattern, what in ASK:
    if fnmatch.fnmatch(rel, pattern):
        decide("ask", f"Editing {what}: confirm this is intended.")
sys.exit(0)
```

**`guard_bash.py`**

```python
#!/usr/bin/env python3
"""PreToolUse (Bash): block dangerous git and shell commands."""
import json, re, sys

cmd = (json.load(sys.stdin).get("tool_input") or {}).get("command", "")
RULES = [
    (r"git\s+push\b.*\s(--force(?!-with-lease)|-f)\b", "deny", "Force-push is not allowed."),
    (r"--no-verify\b", "deny", "Don't skip git hooks."),
    (r"\brm\s+-rf?\s+(/|~|\.\.)", "deny", "Deleting outside the repository is not allowed."),
    (r"git\s+reset\s+--hard\b", "ask", "Hard reset discards work."),
]
for pattern, decision, reason in RULES:
    if re.search(pattern, cmd):
        print(json.dumps({"hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": decision,
            "permissionDecisionReason": reason}}))
        break
sys.exit(0)
```

**`sim_lint.py`**

```python
#!/usr/bin/env python3
"""PostToolUse (Edit|Write): format C#; flag nondeterminism in sim/src at once."""
import json, os, re, subprocess, sys

event = json.load(sys.stdin)
path = (event.get("tool_input") or {}).get("file_path", "")
if not path.endswith(".cs") or not os.path.exists(path):
    sys.exit(0)
root = os.environ.get("CLAUDE_PROJECT_DIR", os.getcwd())
try:
    subprocess.run(["dotnet", "csharpier", "format", path], cwd=root, capture_output=True, timeout=30)
except (OSError, subprocess.TimeoutExpired):
    pass  # formatting is best-effort; CI checks it anyway

rel = os.path.relpath(os.path.abspath(path), root).replace("\\", "/")
if not rel.startswith("sim/src/"):
    sys.exit(0)

source = open(path, encoding="utf-8").read()
# Strip comments and string literals so they don't trigger false positives.
code = re.sub(r"//[^\n]*|/\*.*?\*/", "", source, flags=re.S)
code = re.sub(r'@?"(?:[^"\\]|\\.)*"', '""', code)

ERRORS = [
    (r"\b(float|double|decimal)\b", "floating-point type: use Fix or integers"),
    (r"\bMathF\.|\bMath\.(Sqrt|Sin|Cos|Tan|Atan2?|Pow|Exp|Log\d*|Round|Floor|Ceiling)\b",
     "floating-point maths: use FixMath"),
    (r"\bnew\s+Random\b|\bSystem\.Random\b", "System.Random: use SimRandom streams"),
    (r"\bDateTime\.(Now|UtcNow)\b|\bStopwatch\b|\bEnvironment\.TickCount",
     "wall-clock time in the simulation"),
    (r"\bGuid\.NewGuid\b", "random GUIDs: use deterministic ids"),
    (r"\bUnityEngine\b", "Unity reference in the simulation"),
    (r"\bParallel\.|\bTask\.Run\b|\bnew\s+Thread\b", "ad-hoc threading: use IParallelRunner"),
]
WARNINGS = [
    (r"\b(Dictionary|HashSet)<", "Dictionary/HashSet: make sure iteration order never affects state"),
    (r"\.GetHashCode\(\)", "GetHashCode: string hashes are randomised per process; use SimHash"),
    (r"\bstatic\s+(?!readonly\b|class\b)[\w<>\[\],\s]+?\s+\w+\s*(;|=(?!>))",
     "possible static mutable state"),
]

def scan(rules):
    found = []
    for pattern, message in rules:
        for m in re.finditer(pattern, code):
            line = code.count("\n", 0, m.start()) + 1
            entry = f"{rel}:{line}: {message}"
            if entry not in found:
                found.append(entry)
    return found

errors, warnings = scan(ERRORS), scan(WARNINGS)
if errors:
    print("Determinism violations (fix before continuing):\n" + "\n".join(errors), file=sys.stderr)
    sys.exit(2)
if warnings:
    print(json.dumps({"hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": "Determinism check warnings:\n" + "\n".join(warnings)}}))
sys.exit(0)
```

**`stop_checks.py`**

```python
#!/usr/bin/env python3
"""Stop: if sim/ or data/ changed, run the fast tests; block the stop while they fail."""
import hashlib, json, os, subprocess, sys

event = json.load(sys.stdin)
if event.get("stop_hook_active"):
    sys.exit(0)  # already continuing because of this hook; avoid loops
root = os.environ.get("CLAUDE_PROJECT_DIR", os.getcwd())

def git(*args):
    return subprocess.run(["git", *args], cwd=root, capture_output=True, text=True).stdout

changed = [l[3:] for l in git("status", "--porcelain").splitlines()]
if not any(p.startswith(("sim/", "data/")) for p in changed):
    sys.exit(0)

# Skip if nothing changed since the last green run.
state = hashlib.sha1((git("diff") + "".join(sorted(changed))).encode()).hexdigest()
marker = os.path.join(root, ".claude", ".last_green")
if os.path.exists(marker) and open(marker).read() == state:
    sys.exit(0)

try:
    result = subprocess.run(
        ["dotnet", "test", "sim/GodGame.Sim.sln", "--filter", "Category=Fast", "--nologo", "-v", "q"],
        cwd=root, capture_output=True, text=True, timeout=540)
except (OSError, subprocess.TimeoutExpired) as e:
    print(f"Stop hook couldn't run the fast tests: {e}", file=sys.stderr)
    sys.exit(0)  # don't block on a broken toolchain; CI still runs the tests
if result.returncode != 0:
    tail = "\n".join((result.stdout + result.stderr).splitlines()[-40:])
    print("Fast simulation tests are failing; fix them before finishing:\n" + tail, file=sys.stderr)
    sys.exit(2)
open(marker, "w").write(state)
sys.exit(0)
```

Add `.claude/.last_green` to `.gitignore`.

### A4. `.claude/rules/sim-determinism.md`

````markdown
---
paths:
  - "sim/src/**/*.cs"
---

# Simulation rules (deterministic, engine-free)

## Never
- float, double, decimal, MathF, floating-point Math overloads. Use Fix (64-bit, 16 fractional bits) or integers in natural units.
- System.Random, DateTime.Now/UtcNow, Stopwatch, Environment.TickCount, Guid.NewGuid.
- string.GetHashCode or hashing object references. Use SimHash (xxHash64).
- Iterating Dictionary/HashSet where order affects state. Use arrays, sorted keys or insertion-ordered containers.
- Sorting without a total tie-breaker (key, then entity id).
- Threads, Tasks or Parallel directly. Use IParallelRunner; each chunk writes a disjoint range; reduce in index order.
- Static mutable state; UnityEngine; runtime NuGet packages; C# above 9 (no global usings, no file-scoped namespaces). `init` needs the IsExternalInit polyfill in GodGame.Sim.Math.

## Numbers
- Positions, velocities: Fix. Fields: integers (mm, centi-degrees, 0–65535 scales, cm/s). Angles: 16-bit binary angles with table trig.
- Prefer squared distances to square roots in hot loops.

## Structure
- Units are structure-of-arrays in World; one system per tick step, registered in TickOrder.cs. Never change the order without a SimVersion bump.
- Decisions are per regiment (staggered rates); per-soldier work is O(1) per tick: flow-field sample, steering, spatial-hash neighbours.
- Randomness: SimRandom.Stream(seed, systemId, entityId, tick).
- Log a cause event (with links to its causes) for anything a player could ask "why?" about: casts, structure failures, swept, launched, routs, specialist actions, and every death with its cause and source (combat, trampled, crushed, burned, drowned, fell with the ice, pushed).

## Before you finish
- Fast tests green; thread-count determinism tests green.
- If existing goldens change, stop and report. Don't regenerate them; /golden-replay does that.
````

### A5. Two agent definitions (the rest follow the table in 5.4)

**`.claude/agents/sim-engineer.md`**

````markdown
---
name: sim-engineer
description: Implements deterministic simulation features in sim/ (systems, fields, validation, timeline), test-first. Use for any change under sim/src.
tools: Read, Edit, Write, Bash, Grep, Glob, Skill
model: opus
color: blue
---

You are the simulation engineer for a deterministic, engine-free C# battle simulation (up to 10,000 units, 20 ticks per second, run at 15× real time for a scrubbable preview).

Process:
1. Read the checklist item, the design docs it touches (docs/design/), and .claude/rules/sim-determinism.md.
2. Write or extend the scenario test that expresses the design decision; watch it fail.
3. Implement in the system that owns the behaviour, with data in the World's arrays and integer or Fix maths only. New systems are registered in TickOrder.cs.
4. Add cause events for everything a player could ask "why?" about.
5. Run the fast tests and the thread-count determinism tests. If existing goldens change, stop and report the scenarios and your explanation; never regenerate goldens.
6. Report: what changed, tests added, hot paths that need /perf-check, and any game rule you had to assume (implemented behind a data flag).

Never use floats, Unity types, runtime NuGet packages, or game rules that are not in the brief.
````

**`.claude/agents/determinism-auditor.md`**

````markdown
---
name: determinism-auditor
description: Reviews simulation changes for nondeterminism and runs the determinism tests. Use before merging any change under sim/ or data/rules/.
tools: Read, Grep, Glob, Bash
disallowedTools: Write, Edit
model: opus
memory: project
color: red
---

You audit a deterministic lockstep simulation. Outcomes must be bit-identical across runs, thread counts, Windows and Linux, and the .NET, Mono and IL2CPP runtimes.

1. Get the diff against main (git diff main...HEAD -- sim/ data/rules/).
2. Check every change for: floating point anywhere in state or decisions; unordered iteration (Dictionary, HashSet) affecting state; unstable sorts or missing tie-breakers; shared or reused RNG streams; parallel writes to shared data or reductions in completion order; static mutable state; wall-clock time; culture-dependent parsing or formatting; uninitialised memory; integer overflow in Fix products; reading the state of a later system earlier in the tick; anything that depends on hardware thread count.
3. Run: dotnet test sim/GodGame.Sim.sln --filter "Category=Fast|Category=Golden", plus the thread-count tests.
4. Report findings as High / Medium / Low with file:line, why it can diverge, and the fix. Say clearly when you found nothing.
5. Save recurring patterns to your memory so future audits check them first.
````

### A6. Three skills

**`.claude/skills/milestone/SKILL.md`**

````markdown
---
name: milestone
description: Show the current milestone, its open checklist items and recent work, and propose the next task and agent. Use at the start of a work session or when asked what's next.
disable-model-invocation: true
allowed-tools: Bash(git log *) Bash(git status *) Read
---

## Status file
!`cat docs/milestones/STATUS.md`

## Recent commits
!`git log --oneline -15`

## Working tree
!`git status --short`

Then:
1. Summarise the current milestone and its exit criteria in three lines.
2. List the unchecked items. If recent commits seem to complete one, verify in the code and offer to tick it.
3. Propose the next item: the smallest unblocked item on the critical path. Give a branch name, the agent(s), the skills and the tests to add.
4. If the item depends on an open question (docs/implementation-plan.md section 10), name it and the working default.
````

**`.claude/skills/golden-replay/SKILL.md`**

````markdown
---
name: golden-replay
description: Verify golden replays; when battle outcomes changed on purpose, bump SimVersion and regenerate golden files with a changelog entry. The only allowed way to change sim/tests/golden/.
disable-model-invocation: true
argument-hint: "verify | update <reason>"
allowed-tools: Bash(dotnet test *) Bash(dotnet run *) Bash(git diff *)
---

Mode and reason: $ARGUMENTS

## verify
Run `dotnet test sim/GodGame.Sim.sln --filter Category=Golden`. For each failing scenario, run
`dotnet run --project sim/tools/GodGame.Sim.Cli -- compare --golden <scenario>` and report the first diverging second and the first differing cause events.

## update
1. Run verify. If nothing diverges, stop.
2. Explain each divergence from the branch's changes (`git diff main...HEAD`). If any divergence is unexplained, stop and suggest /desync-hunt.
3. Show the explanation and ask the user to confirm the reason.
4. Increment the constant in sim/src/GodGame.Sim/SimVersion.cs.
5. Run `dotnet run --project sim/tools/GodGame.Sim.Cli -- golden regenerate --all`.
6. Append to sim/CHANGELOG-sim.md: new version, date, reason, scenarios affected, notable statistic changes (casualties, swept, launched).
7. Run verify again and summarise the changed golden files.
````

**`.claude/skills/new-power/SKILL.md`**

````markdown
---
name: new-power
description: Add a god power as data plus the minimum code, with range-rule checks, timeline and preview support, counters and scenario tests. Use when implementing any power from docs/design/power-palette.md.
argument-hint: "<power name>"
---

Power: $ARGUMENTS

1. Read its entry in docs/design/power-palette.md and every brief decision it touches. If the palette marks it "seed" or "open", ask the design-lead agent before building.
2. Write the consequence chain (power → world change → army behaviour → outcome) in the PR description; two or three hops.
3. Data (data/rules/powers.json): id, area shape, wind-up, duration, cost, fields written and amounts, mode-specific tuning (campaign vs PvP).
4. Code only if a new field operation is needed; otherwise the generic cast executor applies the data. The range rule is checked at the firing tick against regiment outlines (100 m), with a fizzle reason.
5. Preview: its timeline bar (wind-up to peak), area placement with the mouse, overlays the player needs to read it.
6. Counters: add or check the field interactions that make it counter and be countered (palette counter-web table).
7. Tests: a scenario test per brief decision it touches, one per counter-web row it appears in, and a range-rule fizzle test.
8. Update power-palette.md through the game-design skill if the build settled anything ("seed" → settled detail).
````

### A7. `.mcp.json` (shared)

```json
{
  "mcpServers": {
    "blender": {
      "command": "uvx",
      "args": ["blender-mcp"],
      "env": { "BLENDER_MCP_SAFE_MODE": "1", "DISABLE_TELEMETRY": "true" }
    }
  }
}
```

Unity MCP is added per machine (its configuration contains local paths): install the package, then use its client-configuration menu for Claude Code, or `claude mcp add` at local scope.

### A8. `.gitattributes` and `.gitignore` (key lines)

```gitattributes
* text=auto
*.cs      text eol=lf diff=csharp
*.md      text eol=lf
*.json    text eol=lf
*.unity   merge=unityyamlmerge eol=lf
*.prefab  merge=unityyamlmerge eol=lf
*.asset   merge=unityyamlmerge eol=lf
*.meta    text eol=lf
# Git LFS
*.blend filter=lfs diff=lfs merge=lfs -text
*.fbx   filter=lfs diff=lfs merge=lfs -text
*.png   filter=lfs diff=lfs merge=lfs -text
*.tga   filter=lfs diff=lfs merge=lfs -text
*.psd   filter=lfs diff=lfs merge=lfs -text
*.exr   filter=lfs diff=lfs merge=lfs -text
*.wav   filter=lfs diff=lfs merge=lfs -text
*.ogg   filter=lfs diff=lfs merge=lfs -text
*.mp4   filter=lfs diff=lfs merge=lfs -text
*.ttf   filter=lfs diff=lfs merge=lfs -text
data/maps/**/*.bin filter=lfs diff=lfs merge=lfs -text
```

```gitignore
# Unity
/unity/[Ll]ibrary/
/unity/[Tt]emp/
/unity/[Oo]bj/
/unity/[Bb]uild*/
/unity/[Ll]ogs/
/unity/[Uu]ser[Ss]ettings/
/unity/*.csproj
/unity/*.sln
# .NET
/sim/.build/
TestResults/
# Tools and editors
.vs/  .idea/  .vscode/
# Claude Code personal files
CLAUDE.local.md
.claude/settings.local.json
.claude/.last_green
```

### A9. Simulation build files

**`sim/Directory.Build.props`** (all simulation projects)

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <!-- Keep build output out of sim/src, which Unity imports as a package. -->
    <BaseOutputPath>$(MSBuildThisFileDirectory).build/bin/$(MSBuildProjectName)/</BaseOutputPath>
    <BaseIntermediateOutputPath>$(MSBuildThisFileDirectory).build/obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath>
  </PropertyGroup>
</Project>
```

**`sim/src/Directory.Build.props`** (the three libraries Unity also compiles)

```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <!-- Build-time only; never reaches Unity. Use the latest version. -->
    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.3.4" PrivateAssets="all" />
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)../BannedSymbols.txt" />
  </ItemGroup>
</Project>
```

Tests, benchmarks and the CLI target `net10.0` with the latest C# in their own project files.

**`sim/BannedSymbols.txt`**

```text
T:System.Single;Use Fix or integers (GodGame.Sim.Math)
T:System.Double;Use Fix or integers (GodGame.Sim.Math)
T:System.Decimal;Use Fix or integers (GodGame.Sim.Math)
T:System.MathF;Floating-point maths is banned in the simulation
T:System.Random;Use SimRandom streams
P:System.DateTime.Now;Wall-clock time is banned in the simulation
P:System.DateTime.UtcNow;Wall-clock time is banned in the simulation
P:System.Environment.TickCount;Wall-clock time is banned in the simulation
P:System.Environment.TickCount64;Wall-clock time is banned in the simulation
T:System.Diagnostics.Stopwatch;Timing belongs in GodGame.Sim.Bench
M:System.Guid.NewGuid;Use deterministic ids
M:System.String.GetHashCode;Randomised per process; use SimHash
T:System.Threading.Tasks.Parallel;Use IParallelRunner
T:System.Threading.Thread;Use IParallelRunner
```

**`sim/src/package.json`** (makes `sim/src` a Unity package)

```json
{
  "name": "com.godgame.sim",
  "version": "0.1.0",
  "displayName": "God Game Simulation",
  "description": "Engine-free deterministic simulation shared by Unity and .NET.",
  "unity": "6000.3"
}
```

**`sim/src/GodGame.Sim/GodGame.Sim.asmdef`**

```json
{
  "name": "GodGame.Sim",
  "rootNamespace": "GodGame.Sim",
  "references": ["GodGame.Sim.Math"],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": [],
  "autoReferenced": true,
  "noEngineReferences": true
}
```

**`unity/Packages/manifest.json`** (add the entry to the existing dependencies)

```json
{
  "dependencies": {
    "com.godgame.sim": "file:../../sim/src"
  }
}
```

### A10. `.github/workflows/sim.yml`

```yaml
name: sim
on:
  push:
    branches: [main]
  pull_request:
jobs:
  test:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v6
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: sim/global.json
      - run: dotnet tool restore
      - run: dotnet csharpier check sim
      - run: dotnet test sim/GodGame.Sim.sln -c Release --filter "Category!=Perf&Category!=Fuzz"
      - run: dotnet run -c Release --project sim/tools/GodGame.Sim.Cli -- golden hashes --out hashes-${{ matrix.os }}.txt
      - uses: actions/upload-artifact@v4
        with:
          name: hashes-${{ matrix.os }}
          path: hashes-${{ matrix.os }}.txt
  cross-os:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
      - run: diff hashes-ubuntu-latest/hashes-ubuntu-latest.txt hashes-windows-latest/hashes-windows-latest.txt
```

From M2, add `unity.yml` with GameCI's test runner and builder (`game-ci/unity-test-runner`, `game-ci/unity-builder@v4`), project path `unity`, a cache for `unity/Library`, and the `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` repository secrets as GameCI's activation guide describes. A nightly workflow adds `Perf`, `Fuzz`, the IL2CPP golden run and the perturbation checks.

### A11. `docs/milestones/STATUS.md`

```markdown
# Milestone status

## Current: M0 — Foundations
Started: YYYY-MM-DD

### Checklist
- [ ] 1. Repository basics (gitignore, gitattributes + LFS, editorconfig, branch protection)
- [ ] 2. Design docs, ADR 0001, this file
- [ ] 3. Simulation solution with analyzers and first tests
- [ ] 4. CI: sim.yml with cross-OS hash check
- [ ] 5. Unity 6.3 LTS project with the sim package and SimHost cube
- [ ] 6. Claude Code: CLAUDE.md, rules, agents, skills, hooks, MCP
- [ ] 7. Blender export and validation scripts, test soldier imported
- [ ] 8. Exit criteria verified

### Exit criteria
- [ ] dotnet test green on Windows and Linux; hashes match
- [ ] Unity opens cleanly; the cube moves with the simulation
- [ ] A float in sim/src fails the build and the hook flags it
- [ ] /context, /agents and /milestone work
- [ ] Unity MCP reads the console; Blender MCP lists the scene
- [ ] Test soldier exports with a clean validation report

### Decisions needed
- (none for M0)

## Done
- (nothing yet)
```

---

## Appendix B: design decisions → scenario tests

The first scenario tests, each named after the brief decision it protects. The `scenario-test` skill writes them; `test-engineer` owns them.

| Decision (brief) | Scenario tests | Milestone |
|---|---|---|
| Range rule, 100 m (2026-10-08) | A cast whose area comes within 99 m of a regiment outline fizzles with a reason; at 100 m it fires; stragglers don't block; launched and stunned soldiers still count toward the outline | M3, M5 |
| Terrain pieces 100 m from deployment zones (2026-10-08) | A piece 99 m from a zone is refused; consequences may still flow into the buffer | M3 |
| Powers can kill only through consequences (2026-10-08) | A flood sweeps a regiment that fords at the peak; every death has a cause chain ending at a world change | M3 |
| Weak ice breaks under crowds (2026-10-08) | A thin file crosses; a dense column breaks it; heavy units count more; breaks spread through a dense column and stop at a thin file | M5, M6 |
| Cold freezes water; fire melts ice (2026-10-08) | A cold snap thickens ice over time; fire on the bank thins it until heavies go through | M6 |
| Wind affects arrows (2026-10-08) | A headwind shortens volley range by the table, a tailwind lengthens it, only the along-shot part counts, bolts feel it less than arrows | M5, M6 |
| Powers counter powers (2026-10-08) | Each counter-web row in the power palette, including pre-emptive vs reactive cost and turnabouts | M6 |
| Herds stampede (2026-10-08) | Fire near a herd starts a stampede after a visible delay; the heading follows the heat gradient; boulders turn it; it hits either side | M5 |
| Towers fight only through garrisons; the defender AI chooses garrisons (2026-10-08) | An empty tower does nothing; a garrison gains range and cover; smoke blinds it; assignment is deterministic and stable under ±2 m edits | M6, M7 |
| No full traps; route at most 2× (2026-10-09) | An enclosure is refused; a maze over 2× is refused; mud doesn't count toward the cap; at PvP lock-in the newest closing piece gets a gap | M2, M10 |
| Exaggerated physics, no wobble (2026-10-09) | Landings follow a seeded spread around the source's heading; on open ground most survive stunned; the 2 m fringe only stuns; landings never launch neighbours | M5 |
| Never walk into hazards; many causes of death (2026-10-09, 2026-10-11) | Fuzz invariant (no soldier enters a lethal cell under its own steering); teetering precedes every crowd-pushed death; every death has a cause and source; infantry outrun a fire front on open ground, but a regiment trapped against a river burns; boulders released by a tremor follow the fall line and crush only after their rumble warning; a stampede tramples (damage) as well as launches; landings never kill or launch neighbours | M3, M5, M6 |
| The preview shows where soldiers land (2026-10-09) | The previewed landing spread equals the live result | M5 |
| Three side quests per level (2026-10-09) | Quest counters tick from cause and launch events during preview | M9 |
| PvP rounds reset; wipe rule (2026-10-09) | Regiments under 10% count as wiped; fleeing off the map counts as wiped; routed units on the field don't; budgets and placements carry over | M10 |
| Units shape the world; specialists only (2026-10-09, 2026-10-10) | Actions run through the executor; charges are lost when a regiment falls; a dig stops before closing the last gap; Firebrand ignition rules hold; drovers never start a stampede | M7 |
| Stability (review 2026-10-10) | Moving any placement ±2 m or any cast ±1 s keeps the outcome within the limit | M7, M9 |

---

## Appendix C: references

- Unity 6.3 LTS announcement (December 2025): https://unity.com/blog/unity-6-3-lts-is-now-available
- Unity release support (6.3 LTS supported until December 2027): https://unity.com/releases/unity-6-releases
- CoreCLR, scripting and serialization update (June 2026: 6.7 LTS last Mono release, Unity 7 on CoreCLR with .NET 10 and C# 14): https://discussions.unity.com/t/coreclr-scripting-and-serialization-update-june-2026/1723299
- Unity pricing and the cancelled runtime fee: https://unity.com/runtime-fee
- Unity's official MCP server (open beta, May 2026): https://unity.com/en/blog/unity-ai-mcp-how-to-get-started
- MCP for Unity (CoplayDev): https://github.com/CoplayDev/unity-mcp
- blender-mcp: https://github.com/ahujasid/blender-mcp
- GameCI license activation: https://game.ci/docs/github/activation
- Claude Code docs: subagents https://code.claude.com/docs/en/sub-agents · skills https://code.claude.com/docs/en/skills · hooks https://code.claude.com/docs/en/hooks-guide · memory and rules https://code.claude.com/docs/en/memory · GitHub Actions https://code.claude.com/docs/en/github-actions

