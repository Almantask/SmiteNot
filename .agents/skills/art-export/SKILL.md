---
name: art-export
description: Validate and export changed .blend files to FBX with headless Blender scripts, then refresh assets and trigger VAT baking in Unity. Use after editing files under art/source/ or when invoked via /art-export.
---

# Art Export & Validation

## Inputs

Take target `.blend` path(s) from the prompt. When omitted, discover all changed or untracked `.blend` files under `art/source/` with `run_command`:

```powershell
git diff --name-only origin/main...HEAD -- "art/source/*.blend"
git status --porcelain -- "art/source/*.blend"
```

## Workflow

1. **Export FBX.** For each `.blend` file (`<name>` is the filename stem without `.blend`), run via `run_command`:
   ```powershell
   blender -b <file> --python tools/blender/export.py -- --out unity/Assets/_Game/Art/Generated/<name>
   ```
   If `blender` is not on `PATH` and the `bpy` Python package is installed, run `python tools/blender/export.py --blend <file> --out unity/Assets/_Game/Art/Generated/<name>`.
2. **Validate rules.** Run headless validation via `run_command`:
   ```powershell
   blender -b <file> --python tools/blender/validate.py -- --report TestResults/art/<name>.json
   ```
   Exit code `1` indicates rule errors. Read `TestResults/art/<name>.json` with `read_file` and check `.claude/rules/art-pipeline.md` when diagnosing failures.
3. **Summarise reports.** Report all errors and warnings (triangle budgets, materials, naming conventions, unapplied transforms, UVs outside `art/palettes/`, rig bone counts, animation clips).
   - On errors, propose fixes. If the `blender` MCP server is listed in `<mcp_servers>`, inspect its tool schemas and apply interactive fixes with `call_mcp_tool` (saving the `.blend` before large edits), then rerun from step 1.
4. **Refresh in Unity.** Check `<mcp_servers>` for `unityMCP` (or `UnityMCP`):
   - When connected, read the relevant tool schemas under `<appDataDir>/mcp/<serverName>/` and call `call_mcp_tool` to refresh assets, inspect console logs, verify `ArtImportPostprocessor` applied import settings, and run the VAT baker for exported units (from M8).
   - When the editor is closed, note that Unity import verification and VAT baking should run next time the project opens.
5. **Preserve generated outputs.** Never hand-edit files under `unity/Assets/_Game/Art/Generated/`. Stage and commit the source `.blend` (Git LFS) and exported FBX files together.
