---
paths:
  - "art/**"
  - "tools/blender/**"
  - "unity/Assets/_Game/Art/**"
---

# Art pipeline

- **Scale and axes:** 1 Blender unit = 1 metre, model Z-up; the export script converts axes and applies transforms. Nobody sets FBX export options by hand.
- **Names:** `U_<type>_<variant>_LOD<n>` (units), `P_<prop>_<state>` (props), `A_<rig>_<clip>` (animations). One collection per LOD (`LOD0`, `LOD1`, ...).
- **Materials:** one material per mesh; UVs snap into cells of the shared palette texture (`art/palettes/`); no normal maps; the team-colour area is marked in the `TeamMask` vertex-colour layer.
- **Budgets (High preset):** hero 1,000–2,500 triangles with gear; mid 300–600; far under 100. Strategic zoom is procedural.
- **Rigs:** one humanoid rig (about 20 bones) for infantry, ranged, crews and specialists; one quadruped rig for horses and herds; siege engines as rigid parts with a few bones.
- **Exports only through scripts:** `tools/blender/export.py` writes FBX to `unity/Assets/_Game/Art/Generated/`, `tools/blender/validate.py` writes a JSON report; the art-export skill runs both. Generated files are never edited by hand.
- **Sources** (`.blend`) live in `art/source/` under Git LFS; save before asking the Blender MCP server for big edits.
- **Cosmetics never change silhouette, size or team-colour areas.** Playful tone, no gore.
