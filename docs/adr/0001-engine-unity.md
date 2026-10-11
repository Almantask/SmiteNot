# ADR 0001: Unity 6 (URP) presents; an engine-free C# simulation decides

- **Status:** Accepted
- **Date:** 2026-10-11
- **Source:** the game brief's engine and platform decisions of 2026-10-11; implementation plan, sections 1 and 3. The brief wasn't in the repository when this was written, so the reasons below come from the plan; check them against the brief's entry once it is copied to `docs/design/`.

## Context

The game needs:

- Up to 10,000 soldiers on screen, on a hardware floor of integrated graphics (Intel Iris Xe or Radeon 680M class) at 20 fps or better.
- A bit-identical battle from the same plan on every machine: reproducible campaign puzzles, lockstep PvP, and replays that are only map + plan + seed + versions.
- A scrubbable future: the battle simulated headless at 10 to 20 times real time on worker threads.
- Shader-heavy world fields (water, mud, ice, fire, fog) that the units and the picture read from the same cells.
- A PvP verifier that re-simulates rounds on a Linux server.
- Windows 10/11 x64 players only at launch, sold on Steam.

Options from the tech notes: Unity, Unreal (Mass, Niagara, Chaos), Godot 4 (compute through RenderingDevice, C# or GDExtension), or a custom engine.

## Decision

1. **The simulation is an engine-free C# library** (`sim/src`): integer and fixed-point maths, seeded random streams, a fixed system order. It targets .NET Standard 2.1 with C# 9 so that Unity compiles the same source, and it also runs on .NET 10 for tests, the `godsim` CLI and the Linux PvP verifier. No engine type ever enters it.
2. **Unity 6 with the Universal Render Pipeline presents** the simulation and edits the plan. Start on **Unity 6.3 LTS** (supported until December 2027); move to the next LTS when it ships, and to Unity 7 (CoreCLR, .NET 10, C# 14) once it is stable.
3. Unity consumes the simulation as a local package (`com.godgame.sim` → `file:../../sim/src`), with one assembly definition per project and `noEngineReferences: true`.

## Why Unity

- **One language, one source.** The simulation is C#; Unity compiles it directly, and the same code runs headless on .NET for tests and the server. Unreal would need the simulation in C++ or a second runtime; Godot's C# is viable but its tooling for this scale is thinner.
- **The integrated-graphics floor.** URP scales down well, and indirect instancing with vertex animation textures, Shader Graph, VFX Graph and compute shaders cover the 10,000-soldier rendering plan.
- **Tooling.** UI Toolkit for the planning interface, Cinemachine for the highlight camera, GameCI for headless tests and builds in GitHub Actions, and MCP servers that let Claude Code read the console and edit scenes.
- **Cost.** Unity Personal is free up to $200k annual revenue or funding; Unity Pro per seat after that.

## Consequences

- Gameplay never lives in Unity. Unity may read anything from the simulation and may write only the plan. Enforced by the `noEngineReferences` assembly definitions, the banned-API analyzer and review.
- Determinism has to hold across .NET, the Unity Mono editor and IL2CPP players. Cross-runtime golden replays guard this from M1, and again before every engine upgrade.
- Unity is moving to CoreCLR (6.7 is the last Mono LTS) and removing domain reloads: Unity-side scripts must not rely on static state being reset. Enter Play Mode runs without domain reload from day one.
- Burst stays optional. Hot kernels are static methods over flat arrays of plain structs, so a Unity-side adapter can Burst-compile them later; the decision is made at the end of M2 (a future ADR).
- Windows is the only player platform, so the project ships DirectX 12 with DirectX 11 as a fallback and no Vulkan or Metal variants. Linux x64 is a headless target only.
