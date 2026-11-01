---
name: texture-gen
description: Generate textures locally with ComfyUI through the comfy MCP server (seamless terrain and ground tiles, power and UI icon drafts, look-development and concept images). Use when the game needs a new or revised texture or tile, or when asked to make game art images with the local image model.
argument-hint: "[what to make, e.g. 'mud terrain tile' or 'icon draft for the rain power']"
---

Request: $ARGUMENTS

Everything runs on this machine: ComfyUI with Z-Image Turbo (Apache 2.0) and the Universal Seamless Tiles nodes, driven through the `comfy` MCP server (tools `mcp__comfy__*`). `texture.py` (next to this file) writes the workflows, checks tiles and saves results. Run it as `uv run --no-project --with pillow --with numpy python .claude/skills/texture-gen/texture.py <command>`; `--help` lists the options.

## Scope

- **Tiles** (default): terrain and ground detail that wraps on both axes, 1024 × 1024, 16 steps.
- **Images** (`--no-tile`): icon drafts, concept and look-development art, 8 steps. There is no transparent-background step yet; draft icons on a flat plain background.
- **Not unit textures:** units take flat colour from the shared palette (`.claude/rules/art-pipeline.md`).

## Steps

1. **Server.** If no `mcp__comfy__*` tools exist (cloud session, another machine), stop and say so. Call `server_info`; if ComfyUI is not running, call `launch_comfyui` with no `extra_args` (the saved defaults keep it on 127.0.0.1).
2. **Name and prompt.** Name it `T_<category>_<name>` in lowercase (`T_terrain_mud_a`, `T_icon_rain_a`, `T_concept_ford_a`). Write the prompt as: subject and material; the view (tiles: "seen from directly above, flat orthographic top-down, no horizon, no perspective"); the look ("stylized hand-painted game texture", playful, no gore); light ("soft even daylight, no cast shadows" for tiles, since the terrain shader adds its own lighting and detail); a few colour words. For tiles, ask for evenly spread small features: one big landmark repeats visibly. Never write "seamless" or "tileable" in the prompt: Z-Image then draws a pattern that repeats inside the image.
3. **Workflows.** Make three or four candidates with different seeds, each in its own folder:
   `texture.py workflow --name <name> --prompt "<prompt>" --seed <n> --out TestResults/textures/<name>/s<n>/workflow.json`
4. **Run each.** Pass absolute paths with forward slashes. `validate_workflow(workflow_path)` must return `valid: true`. Then `run_workflow(workflow_path, wait=false)`, then `job(action="wait", prompt_id=..., timeout_seconds=900)`, then `fetch_outputs(prompt_id, out_dir=<that seed's folder>)`.
5. **Check.** For tiles, run `texture.py check <png>` and look at both files it writes (Read them): `_2x2.png` and `_contrast.png`. Reject on `pass: false`, any straight line inside the tile in the contrast view (flat textures such as mud, sand or snow often get them at any step count; try other seeds), an obvious repeating landmark, perspective or a horizon, baked shadows, or text. For images, look at the PNG itself. Adjust the prompt or seed and repeat from step 3.
6. **Approve.** Show the user the best candidates (the 2×2 previews for tiles). Save only the one they choose:
   `texture.py save <png> --workflow <its workflow.json>` writes `art/textures/<category>/<name>.png` (Git LFS) and `<name>.json`, which holds the prompt, seed, model, licence and the full workflow, so the texture can be regenerated exactly.
7. **Free the GPU.** Call `free_memory()` when done; Unity and Blender share the card.
8. **Unity**, only when a material needs the texture now: copy the PNG to `unity/Assets/_Game/Art/Textures/<category>/` and set its import settings through the Unity MCP server (tiles: wrap mode Repeat; colour maps: sRGB). Never hand-edit `.meta` files.

## Rules

- Shipped assets come only from the Apache 2.0 model `texture.py` uses. The machine's Hugging Face cache holds other models (FLUX.1-dev and others) under non-commercial licences; switching models is the user's decision.
- Ask the user before `download_model`, `install_node`, `update_comfyui` or `switch_comfyui_version`. Never call `auth_login` or the `partner_*` tools, and never pass `confirm_spend=true`: they are paid cloud generation.
- Skip the StableMaterials nodes that are also installed: their PBR maps don't follow the input image, come out at 512 px with near-flat normals, and keep the model in VRAM until ComfyUI restarts.
- Skip `generate_image` (it runs an SD1.5 template that is not installed) and the slot tools `list_workflow_slots`, `set_workflow_slot` and `vary_workflow` (they need UI-format workflows; `texture.py` writes API format).

## Common problems

| Symptom | Cause and fix |
|---|---|
| `ws_timeout` or a run that takes minutes | The first run after a launch loads about 20 GB of weights from disk. Don't resubmit; `job(action="wait")` on the same `prompt_id`. Later runs take 10–20 s. |
| Faint straight lines inside a tile | Leftovers from the tiling roll. Under 16 steps they are common; on flat textures they can survive 32. Use another seed, or a busier prompt. |
| A pattern repeating inside one image | The prompt says "seamless" or "tileable"; remove it. |
| `ctypes objects containing pointers cannot be pickled` | A workflow set the VAE node to "Make a copy", which fails whenever ComfyUI runs with dynamic VRAM. `texture.py` uses "Modify in place"; regenerate the workflow with it. |
| `CUDA error: invalid argument` from a non-ComfyUI pipeline node | ComfyUI's dynamic VRAM conflicts with nodes that move models themselves. The saved launch defaults include `--disable-dynamic-vram`; relaunch with `launch_comfyui` and no `extra_args`. |
| Tool missing `SeamlessTileModelDiT` | The Universal Seamless Tiles node pack (github.com/OliverCrosby/ComfyUI-Universal-Seamless-Tiles) is not installed; ask the user. |
