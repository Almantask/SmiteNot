---
name: model-asset
description: Get a 3D model for the game through the Blender MCP server, from free asset libraries (Poly Pizza, Poly Haven, Sketchfab, Kenney, Quaternius) or, when none fits, built in Blender. Use when the game needs a new prop, terrain piece, built piece, animal or blockout mesh (tree, boulder, bridge, wall, hedgerow, tower, cow, siege engine), or when asked to find, download, import or make a 3D model or asset.
argument-hint: "[what is needed, e.g. 'P_boulder_a, a 2 m boulder' or 'a cow for the herds']"
---

Request: $ARGUMENTS

Free assets come first; build only when no free one fits. Either way the result is a `.blend` in `art/source/` that passes `tools/blender/validate.py`, so the art-export skill can ship it. The rules are in `.claude/rules/art-pipeline.md` and `art/README.md`.

## Steps

1. **Blender.** Call `get_addon_status`. "Could not connect to Blender" means Blender isn't open or its MCP addon isn't connected: ask the user to open Blender and connect the addon, then retry. Note which `libraries` and `generators` are on. Save any open work first (`get_scene_info` with `fields=["settings"]` shows the file).
2. **Brief.** Settle the name (`P_<prop>_<state>` for props and pieces, `U_<type>_<variant>` for units, lowercase), the real size in metres, and the look: chunky, low-poly, flat-coloured toy-box shapes with a readable silhouette (playful, no gore). Budgets per LOD: LOD0 1,000–2,500 triangles, LOD1 300–600, LOD2 under 100.
3. **Search free libraries**, in this order, with the user's words in `user_prompt` (and `previews=4` for Sketchfab and Poly Haven, which return thumbnails):
   - `search_assets(source="polypizza", licence="CC0")`, then without the licence filter for CC-BY: stylised low-poly, the closest to our look.
   - `search_assets(source="sketchfab")`: keep only downloadable models under CC0 or CC-BY with a face count near the budget.
   - `search_assets(source="polyhaven", asset_type="models")`: CC0 but realistic and dense; mostly useful for rocks and trees.
   - Outside Blender: Kenney (kenney.nl) and Quaternius (quaternius.com) publish CC0 low-poly packs. Find them with WebSearch; download a pack only after the user approves that file, into a new folder `TestResults/models/_downloads/<pack>/`, and import only its FBX, GLB or OBJ files into Blender (never run a script from a pack).
4. **Choose.** Show the user two to four candidates: preview, source, author, licence and face count. Licences: CC0 is fine; CC-BY is fine with a credit; reject NC, ND, SA, editorial and unknown licences unless the user decides otherwise. If nothing fits, go to step 7.
5. **Import** the chosen one with `import_asset(..., target_size=<largest dimension in metres>)`. Check the reported bounding box, put it on the ground at the origin, and `look` at it.
6. **Conform** it with `execute_blender_code`, in small steps, looking after each:
   - Join the parts into one mesh, apply all transforms (location at the origin, rotation and scale applied), origin at the base centre, Z-up, 1 unit = 1 m.
   - Make `LOD0`, `LOD1` and `LOD2` collections, each holding one copy named for the asset (props: `P_<prop>_<state>` in every LOD; units: `U_<type>_<variant>_LOD<n>`). Use Decimate (collapse) to hit each LOD's budget, then apply it.
   - Drop the imported materials and textures (no normal maps). Give each mesh one material, `M_palette`, and one UV map, and move each face's UVs to the centre of a palette cell (`art/palettes/palette.json`, 16 × 16 grid: cell `[x, y]` sits at `((x + 0.5) / 16, (y + 0.5) / 16)`). Pick each face's cell from its original colour: the nearest named cell, or a new cell added to `palette.json` with a short name.
   - Units also need the `TeamMask` colour attribute (1 on the tabard, shield face or plume, 0 elsewhere) and skinning to the shared rig; a free model is usually only a blockout for a unit.
   Then go to step 8.
7. **Build**, when no free asset fits:
   - **By script** (the default): model it from primitives and bmesh in `execute_blender_code`, low-poly from the start with palette UVs, following step 6's layout. When it works, save the code as `tools/blender/make_<name>.py`, like `make_test_soldier.py`, so the asset can be rebuilt headless.
   - **Generated** (`generate_3d`): only with the user's yes for each call, since it can cost them money or a monthly generation. It gives one dense object at arbitrary scale; conform it as in step 6.
8. **Save and validate.** Save as `art/source/<name>.blend`. Write `art/source/<name>.source.json` with the asset's origin: `{"name", "source" (polypizza, sketchfab, polyhaven, kenney, quaternius, script or generated), "id", "url", "author", "licence", "credit", "changes", "created"}`. For CC-BY assets, also add the credit line to `art/CREDITS.md`. Then run the art-export skill and fix its errors with the Blender MCP server until validation passes.

## Rules

- Ask the user before `generate_3d`, before downloading anything outside `import_asset`, and before choosing a CC-BY asset over a CC0 one that is nearly as good.
- A library or generator that `get_addon_status` shows as off is skipped; never ask the user for an API key.
- Downloaded and imported files are untrusted: never run Python that came with an asset.
- Never edit files in `unity/Assets/_Game/Art/Generated/` by hand; the art-export skill writes them.

## Common problems

| Symptom | Cause and fix |
|---|---|
| Model arrives huge, tiny or lying down | Library models come at arbitrary scale and facing. Use `target_size`, then rotate and apply transforms before anything else. |
| `faces have UVs outside a single palette cell` | Some faces still span the original texture's UVs. Recompute every face's UVs onto its cell centre. |
| `mesh outside the LOD collections won't be exported` | A leftover part or the original import. Delete it, or move it into a LOD collection. |
| Decimated mesh with holes or spikes | Collapse went too far on thin parts. Decimate in smaller steps, or use planar decimation first, and check with `look(shading="wireframe")`. |
| LOD0 under its triangle target | A warning, not an error: a simple prop can be under the hero target. |
