---
name: texture-gen
description: Generate seamless terrain tiles, icon drafts, and concept art locally with ComfyUI and Z-Image Turbo. Use when creating or revising game textures, tiles, or icons with the local image model, or when invoked via /texture-gen.
---

# Local Texture & Tile Generation (ComfyUI)

Generates Apache-2.0-clean game textures locally via ComfyUI (`Z-Image Turbo bf16` + Universal Seamless Tiles) and [texture.py](./scripts/texture.py). Run helper commands via `run_command`:

```powershell
uv run --no-project --with pillow --with numpy python .agents/skills/texture-gen/scripts/texture.py <command> --help
```

## Scope

- **Tiles (default, `1024x1024`, wraps X & Y):**
  - `repair` (default, 2 passes): generates a base image, shifts by half so outer edges wrap cleanly, softens the centre cross seam, and repaints only the cross band. Always use for flat surfaces (`mud`, `sand`, `snow`, `dirt`, `water`).
  - `roll` (1 pass, 16 steps): wraps latents during sampling via Universal Seamless Tiles; faster, but faint interior ghost lines only hide in busy surfaces (`grass`, `foliage`, `gravel`).
- **Plain images (`--no-tile`, 8 steps):** power/UI icon drafts (flat plain background) and look-dev/concept art.
- **Never unit textures:** units use flat colours from `art/palettes/` (`.claude/rules/art-pipeline.md`).

## Workflow

1. **Check ComfyUI MCP server.** Look for `comfy` in `<mcp_servers>`. If absent, stop and inform that the `comfy` MCP server is not connected in this Antigravity session. Read its schema files under `<appDataDir>/mcp/comfy/` as needed and invoke via `call_mcp_tool` (or `mcp_comfy_*` if eager): call `server_info`, and if ComfyUI is stopped, call `launch_comfyui` with no `extra_args`.
2. **Name & prompt.** Lowercase `T_<category>_<name>` (e.g., `T_terrain_mud_a`, `T_icon_rain_a`). Include: subject & material; view (tiles: `"seen from directly above, flat orthographic top-down, no horizon, no perspective"`); style (`"stylized hand-painted game texture"`, playful, no gore); lighting (`"soft even daylight, no cast shadows"` for tiles); colour notes; evenly distributed small details (no single landmark). **Never** include `"seamless"` or `"tileable"` in the prompt text.
3. **Build 3–4 candidate workflows** with distinct seeds (`S = TestResults/textures/<name>/s<n>`):
   ```powershell
   uv run --no-project --with pillow --with numpy python .agents/skills/texture-gen/scripts/texture.py workflow --name <name> --prompt "<prompt>" --seed <n> --out S/workflow.json
   ```
   (Add `--method roll` or `--no-tile` when needed.)
4. **Execute workflows.** Pass absolute forward-slash paths to `comfy` MCP: `validate_workflow(workflow_path)` (`valid: true`) -> `run_workflow(workflow_path, wait=false)` -> `job(action="wait", prompt_id=..., timeout_seconds=900)` -> `fetch_outputs(prompt_id, out_dir=S)`.
5. **Repair seam (`repair` tiles only).** Run `texture.py repair <S/base.png> --workflow S/workflow.json --out-dir S/repair`, call `upload_file(paths=[rolled, mask], overwrite=true)` on `comfy`, then execute `S/repair/repair_workflow.json` as in step 4 (`out_dir=S/repair`).
6. **Inspect quality.** For tiles, run `texture.py check <final.png>` and inspect both `_2x2.png` and `_contrast.png` visually with `read_file`. Reject if `pass: false`, interior lines/trails appear in `_contrast.png`, a landmark repeats, perspective/horizon/shadows/text appear (rerun step 5 with `--denoise 0.8` if a soft cross patch stands out; otherwise adjust prompt/seed at step 3).
7. **Approve & save.** Present the best candidates (`_2x2.png` for tiles) and save **only** the chosen one:
   ```powershell
   uv run --no-project --with pillow --with numpy python .agents/skills/texture-gen/scripts/texture.py save <final.png> --workflow <workflow.json> [--base-workflow S/workflow.json]
   ```
   Writes `art/textures/<category>/<name>.png` (Git LFS) + `<name>.json` provenance sidecar.
8. **Free VRAM.** Call `free_memory()` on `comfy` so Unity and Blender reclaim GPU memory.
9. **Unity import (only when needed immediately).** Copy PNG to `unity/Assets/_Game/Art/Textures/<category>/` and configure import settings (`Repeat` wrap mode for tiles, `sRGB` for colour maps) via `unityMCP` / `UnityMCP` (`call_mcp_tool`). Never hand-edit `.meta` files.

## Guardrails & Troubleshooting

- **Licence & safety:** Only ship assets from `texture.py`'s Apache 2.0 model (`Z-Image Turbo bf16`). Ask before `download_model`, `install_node`, `update_comfyui`, or `switch_comfyui_version`. **Never** call `auth_login`, `partner_*`, or `confirm_spend=true` (paid cloud endpoints). Skip `StableMaterials`, `generate_image`, and `*_workflow_slot*` / `vary_workflow`.
- See [troubleshooting.md](./references/troubleshooting.md) for common ComfyUI/VRAM/seam fixes.
