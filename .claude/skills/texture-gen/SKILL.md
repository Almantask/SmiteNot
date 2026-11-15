---
name: texture-gen
description: Generate textures locally with ComfyUI through the comfy MCP server (seamless terrain and ground tiles, power and UI icon drafts, look-development and concept images). Use when the game needs a new or revised texture or tile, or when asked to make game art images with the local image model.
argument-hint: "[what to make, e.g. 'mud terrain tile' or 'icon draft for the rain power']"
---

Request: $ARGUMENTS

Everything runs on this machine: ComfyUI with Z-Image Turbo (Apache 2.0) and the Universal Seamless Tiles nodes, driven through the `comfy` MCP server (tools `mcp__comfy__*`). `texture.py` (next to this file) writes the workflows, checks tiles and saves results. Run it as `uv run --no-project --with pillow --with numpy python .claude/skills/texture-gen/texture.py <command>`; `--help` lists the options.

## Scope

- **Tiles** (default): terrain and ground detail that wraps on both axes, 1024 × 1024. Two methods:
  - `repair` (default, two runs): generate a plain image, shift it by half so the edges wrap exactly, then repaint a soft cross band where the old edges meet. No ghost lines; use it for everything, and always for flat textures (mud, sand, snow, dirt, water).
  - `roll` (one run, 16 steps): the Universal Seamless Tiles nodes wrap the image while it is generated. Faster, but it can leave faint straight lines inside the tile, which only hide in busy textures (grass, foliage, gravel).
- **Images** (`--no-tile`): icon drafts, concept and look-development art, 8 steps. There is no transparent-background step yet; draft icons on a flat plain background.
- **Not unit textures:** units take flat colour from the shared palette (`.claude/rules/art-pipeline.md`).

## Steps

1. **Server.** If no `mcp__comfy__*` tools exist (cloud session, another machine), stop and say so. Call `server_info`; if ComfyUI is not running, call `launch_comfyui` with no `extra_args` (the saved defaults keep it on 127.0.0.1).
2. **Name and prompt.** Name it `T_<category>_<name>` in lowercase (`T_terrain_mud_a`, `T_icon_rain_a`, `T_concept_ford_a`). Write the prompt as: subject and material; the view (tiles: "seen from directly above, flat orthographic top-down, no horizon, no perspective"); the look ("stylized hand-painted game texture", playful, no gore); light ("soft even daylight, no cast shadows" for tiles, since the terrain shader adds its own lighting and detail); a few colour words. For tiles, ask for evenly spread small features: one big landmark repeats visibly. Never write "seamless" or "tileable" in the prompt: Z-Image then draws a pattern that repeats inside the image.
3. **Workflows.** Make three or four candidates with different seeds, each in its own folder (`S` below):
   `texture.py workflow --name <name> --prompt "<prompt>" --seed <n> --out TestResults/textures/<name>/s<n>/workflow.json`
   Add `--method roll` for a roll tile, or `--no-tile` for a plain image.
4. **Run each.** Pass absolute paths with forward slashes. `validate_workflow(workflow_path)` must return `valid: true`. Then `run_workflow(workflow_path, wait=false)`, then `job(action="wait", prompt_id=..., timeout_seconds=900)`, then `fetch_outputs(prompt_id, out_dir=S)`. The image arrives named after the prompt ID, not the texture.
5. **Repair** (repair-method tiles only). `texture.py repair <S/base png> --workflow S/workflow.json --out-dir S/repair` writes the shifted image, the mask and `repair_workflow.json`, and prints the two files to upload. Call `upload_file(paths=[both], overwrite=true)`, then run `S/repair/repair_workflow.json` as in step 4 with `out_dir=S/repair`.
6. **Check.** For tiles, run `texture.py check <final png>` and Read both files it writes, `_2x2.png` and `_contrast.png`. Reject on `pass: false`, a straight line or a dotted trail inside the tile in the contrast view, an obvious repeating landmark, perspective or a horizon, baked shadows, or text. A soft lighter or darker patch where the repair band crosses is normal; if it stands out, rerun step 5 with `--denoise 0.8` (repaints more, may invent shapes). For images, look at the PNG itself. Otherwise adjust the prompt or seed and go back to step 3.
7. **Approve.** Show the user the best candidates (the 2×2 previews for tiles). Save only the one they choose:
   `texture.py save <final png> --workflow <the workflow that made it>`, adding `--base-workflow S/workflow.json` for a repaired tile. It writes `art/textures/<category>/<name>.png` (Git LFS) and `<name>.json`, which holds the prompt, seed, model, licence and every workflow and setting needed to regenerate it.
8. **Free the GPU.** Call `free_memory()` when done; Unity and Blender share the card.
9. **Unity**, only when a material needs the texture now: copy the PNG to `unity/Assets/_Game/Art/Textures/<category>/` and set its import settings through the Unity MCP server (tiles: wrap mode Repeat; colour maps: sRGB). Never hand-edit `.meta` files.

## Rules

- Shipped assets come only from the Apache 2.0 model `texture.py` uses. The machine's Hugging Face cache holds other models (FLUX.1-dev and others) under non-commercial licences; switching models is the user's decision.
- Ask the user before `download_model`, `install_node`, `update_comfyui` or `switch_comfyui_version`. Never call `auth_login` or the `partner_*` tools, and never pass `confirm_spend=true`: they are paid cloud generation.
- Skip the StableMaterials nodes that are also installed: their PBR maps don't follow the input image, come out at 512 px with near-flat normals, and keep the model in VRAM until ComfyUI restarts.
- Skip `generate_image` (it runs an SD1.5 template that is not installed) and the slot tools `list_workflow_slots`, `set_workflow_slot` and `vary_workflow` (they need UI-format workflows; `texture.py` writes API format).

## Common problems

| Symptom | Cause and fix |
|---|---|
| `ws_timeout` or a slow first run | The first run after a launch loads about 20 GB of weights (one to two minutes). Don't resubmit; `job(action="wait")` on the same `prompt_id`. Later runs take 10–20 s. |
| Faint straight lines inside a roll tile | Leftovers from the roll; on flat textures they survive even 32 steps. Switch to the repair method. |
| A dotted trail or clump along a repaired tile's centre lines | The band was repainted without softening. Rerun `texture.py repair`; it blurs the band before repainting. |
| Repair run fails on a missing input image | The shifted image or mask was not uploaded, or was renamed: upload both with `overwrite=true`. |
| A pattern repeating inside one image | The prompt says "seamless" or "tileable"; remove it. |
| `ctypes objects containing pointers cannot be pickled` | A workflow set the VAE node to "Make a copy", which fails whenever ComfyUI runs with dynamic VRAM. `texture.py` uses "Modify in place"; regenerate the workflow with it. |
| `CUDA error: invalid argument` from a non-ComfyUI pipeline node | ComfyUI's dynamic VRAM conflicts with nodes that move models themselves. The saved launch defaults include `--disable-dynamic-vram`; relaunch with `launch_comfyui` and no `extra_args`. |
| Tool missing `SeamlessTileModelDiT` | The Universal Seamless Tiles node pack (github.com/OliverCrosby/ComfyUI-Universal-Seamless-Tiles) is not installed; ask the user. |
