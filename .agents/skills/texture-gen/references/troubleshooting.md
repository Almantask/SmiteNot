# Texture Generation Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `ws_timeout` or slow first run | First run after launch loads ~20 GB of weights (1–2 min). Do not resubmit; call `job(action="wait")` on the same `prompt_id`. Subsequent runs take 10–20 s. |
| Faint straight lines inside a `roll` tile | Roll-method artifacts; on flat surfaces they survive even at 32 steps. Switch to `--method repair`. |
| Dotted trail or clump along a repaired tile's centre lines | Band repainted without pre-softening. Rerun `texture.py repair` (blurs the seam band before repainting). |
| Repair run fails on missing input image | Shifted image or mask wasn't uploaded or was renamed. Call `upload_file` with both paths and `overwrite=true`. |
| Pattern repeating inside a single image | Prompt contains `"seamless"` or `"tileable"`; remove those words. |
| `ctypes objects containing pointers cannot be pickled` | Workflow set `MakeCircularVAEDiT` to `"Make a copy"` under dynamic VRAM. Regenerate workflow with `texture.py` (uses `"Modify in place"`). |
| `CUDA error: invalid argument` from custom node | ComfyUI dynamic VRAM conflicts with custom model-moving nodes. Relaunch via `launch_comfyui` with no `extra_args` (saved defaults pass `--disable-dynamic-vram`). |
| Missing `SeamlessTileModelDiT` node | `ComfyUI-Universal-Seamless-Tiles` node pack is missing; ask before installing. |
