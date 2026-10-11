---
paths:
  - "**/*.shader"
  - "**/*.hlsl"
  - "**/*.cginc"
  - "**/*.shadergraph"
  - "**/*.shadersubgraph"
  - "**/*.vfx"
---

# Shaders and effects

- **Fields arrive as integer textures in documented units:** ground, water, ice and snow in millimetres; temperature in hundredths of a degree; wetness, mud, fuel, fog, smoke and traffic on 0–65535; wind as two int16 components in cm/s. Decode in one shared include, not ad hoc.
- **Shaders never write gameplay.** The GPU draws; nothing it computes feeds back into the simulation.
- **What the player sees is what the units feel:** sample the same cells the simulation uses, then add procedural detail on top. Never draw a hazard that isn't in the fields, or hide one that is.
- **Every warning has a strategic-zoom form** that survives the Low preset: strain stages (creak, crack, sag), rising water, a fire's front, landing spreads, the no-cast ring.
- **No gore.** Deaths are cartoon gags (soot, pancakes, floating gear).
- **Team colours are colour-blind safe** and live on big areas (tabard, shield face, plume) via the team-colour vertex mask.
- **Windows only, DirectX 12 with a DirectX 11 fallback:** no Vulkan or Metal variants; keep keyword counts low for the integrated-graphics floor.
- Nothing that blocks movement is ever only an effect (VFX Graph is decoration).
