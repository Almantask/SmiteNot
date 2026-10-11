---
name: art-export
description: Export changed .blend files to FBX with the headless Blender scripts, validate them against the art rules, then trigger VAT baking in Unity. Use after editing anything under art/source/.
argument-hint: "[path to a .blend file, or empty for every changed one]"
---

Files: $ARGUMENTS (empty means every .blend changed against main: `git diff --name-only main...HEAD -- 'art/source/*.blend'` plus uncommitted ones)

1. For each file, export: `blender -b <file> --python tools/blender/export.py -- --out unity/Assets/_Game/Art/Generated/<name>`.
2. Validate: `blender -b <file> --python tools/blender/validate.py -- --report TestResults/art/<name>.json`. Exit code 1 means errors.
3. Summarise each report: errors (budgets, materials, names, transforms, UVs outside the palette, bones, clips) and warnings. On errors, propose fixes; the Blender MCP server can apply them interactively, then rerun from step 1.
4. In Unity (MCP or the editor): refresh assets, check the AssetPostprocessor applied the folder's import settings, then run the VAT baker for the exported units (from M8).
5. Never edit files under unity/Assets/_Game/Art/Generated/ by hand; commit the .blend (Git LFS) and the exported FBX together.
