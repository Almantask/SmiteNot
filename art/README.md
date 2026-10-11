# Art

Blender sources and the rules every asset follows. The full pipeline is in `docs/implementation-plan.md`, section 7; Claude Code loads `.claude/rules/art-pipeline.md` whenever it touches this folder.

## Folders

| Folder | What lives there |
|---|---|
| `art/source/` | `.blend` files (Git LFS), one asset per file, named after the asset: `U_<type>_<variant>.blend`, `P_<prop>_<state>.blend` |
| `art/palettes/` | The shared palette: `palette.json` (grid and named cells); `palette.png` arrives with the look-development spike |
| `unity/Assets/_Game/Art/Generated/<asset>/` | Exported FBX and `export.json` (Git LFS). Never edited by hand |

## Rules (checked by `tools/blender/validate.py`)

| Rule | Value |
|---|---|
| Scale and axes | 1 Blender unit = 1 metre, Z-up; transforms applied (location at the origin, no rotation, scale 1) |
| Names | Meshes `U_<type>_<variant>_LOD<n>` or `P_<prop>_<state>`; rigs `RIG_humanoid`, `RIG_quadruped`, `RIG_siege`; clips `A_<rig>_<clip>` |
| LODs | One collection per LOD: `LOD0` (hero), `LOD1` (mid), `LOD2` (far) |
| Triangle budgets (High preset) | LOD0 1,000–2,500 · LOD1 300–600 · LOD2 under 100 (over the maximum is an error, under the target a warning) |
| Materials | One per mesh: the shared palette material. No normal maps |
| UVs | Every face inside one cell of the 16 × 16 palette grid |
| Team colour | A `TeamMask` colour attribute: 1 on team-colour areas (tabard, shield face, plume), 0 elsewhere |
| Rigs | Humanoid at most 24 bones (about 20 planned); quadruped 32; siege 12 |
| Clips | 0.1–10 s each; 30 fps for hero-quality clips |

Budgets and limits live in `tools/blender/art_rules.json`; change them there, in the same pull request as this table.

## Commands

```sh
# Export every LOD and the clips to FBX
blender -b art/source/U_testsoldier_a.blend --python tools/blender/export.py -- --out unity/Assets/_Game/Art/Generated/U_testsoldier_a
# Validate and write a JSON report (exit code 1 on errors)
blender -b art/source/U_testsoldier_a.blend --python tools/blender/validate.py -- --report TestResults/art/U_testsoldier_a.json
# Rebuild the test soldier from its script
blender -b --factory-startup --python tools/blender/make_test_soldier.py -- --out art/source/U_testsoldier_a.blend
```

Without a Blender install, the same scripts run under plain Python with the `bpy` module from PyPI (`bpy` 5.2 needs Python 3.13): replace `blender -b <file> --python <script> --` with `python <script> --blend <file>`.

The `art-export` skill runs export and validation for every changed `.blend`. In Unity, `ArtImportPostprocessor` applies the import settings for everything under `Art/Generated/` (axis conversion baked, no material import, readable meshes for the VAT baker, clips only from `_Anims.fbx`).

## The test soldier

`U_testsoldier_a` is a capsule in three LODs on the humanoid rig, with palette UVs, a team-colour band and a one-second walk clip. `tools/blender/make_test_soldier.py` is its source; regenerate it rather than editing the `.blend`. Its LODs are far under the hero and mid triangle targets on purpose, so validation reports two warnings and no errors.
