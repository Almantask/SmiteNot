"""Texture helper for the texture-gen skill.

  workflow  write an API-format ComfyUI workflow (Z-Image Turbo, optional seamless tiling)
  check     score a tile's wrap seams and write a 2x2 preview next to it
  save      copy an approved image into art/textures/<category>/ with a JSON sidecar

Run through uv so Pillow and NumPy are available:
  uv run --no-project --with pillow --with numpy python .claude/skills/texture-gen/texture.py <command> --help
"""

import argparse
import datetime
import json
import re
import secrets
import shutil
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]

# The only model cleared for shipped assets (Apache 2.0). Files live in ComfyUI's models/ folders.
MODEL = {
    "name": "Z-Image Turbo bf16",
    "license": "Apache-2.0",
    "unet": "z_image_turbo_bf16.safetensors",
    "clip": "qwen_3_4b.safetensors",
    "clip_type": "lumina2",
    "vae": "ae.safetensors",
}

# Below 16 steps the latent-roll tiling leaves faint grid ghosts inside the tile.
TILE_STEPS = 16
IMAGE_STEPS = 8

# A wrap edge scoring above this is a visible seam (untiled images score about 5; good tiles under 1).
SEAM_LIMIT = 1.5

NAME_PATTERN = re.compile(r"^T_([a-z]+)_[a-z0-9_]+$")


def build_workflow(prompt: str, seed: int, width: int, height: int, steps: int, tile: bool, name: str) -> dict:
    wf = {
        "unet": {"class_type": "UNETLoader", "inputs": {"unet_name": MODEL["unet"], "weight_dtype": "default"}},
        "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": MODEL["clip"], "type": MODEL["clip_type"], "device": "default"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": MODEL["vae"]}},
        "shift": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["unet", 0], "shift": 3}},
        "positive": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["clip", 0]}},
        "negative": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["positive", 0]}},
        "latent": {"class_type": "EmptySD3LatentImage", "inputs": {"width": width, "height": height, "batch_size": 1}},
        "sampler": {
            "class_type": "KSampler",
            "inputs": {
                "seed": seed, "steps": steps, "cfg": 1.0, "sampler_name": "res_multistep", "scheduler": "simple",
                "denoise": 1.0, "model": ["shift", 0], "positive": ["positive", 0], "negative": ["negative", 0],
                "latent_image": ["latent", 0],
            },
        },
        "decode": {"class_type": "VAEDecode", "inputs": {"samples": ["sampler", 0], "vae": ["vae", 0]}},
        "save": {"class_type": "SaveImage", "inputs": {"images": ["decode", 0], "filename_prefix": f"texture-gen/{name}"}},
    }
    if tile:
        # Universal Seamless Tiles: roll the latent during sampling, wrap the VAE decoder's padding.
        wf["tile_model"] = {"class_type": "SeamlessTileModelDiT", "inputs": {"model": ["shift", 0], "tiling": "enable", "seed": seed}}
        # "Make a copy" deep-copies the VAE, which fails under ComfyUI's dynamic VRAM; in place is safe
        # because every workflow from this script sets the mode explicitly ("disable" restores normal padding).
        wf["tile_vae"] = {"class_type": "MakeCircularVAEDiT", "inputs": {"vae": ["vae", 0], "tiling": "enable", "copy_vae": "Modify in place"}}
        wf["sampler"]["inputs"]["model"] = ["tile_model", 0]
        wf["decode"]["inputs"]["vae"] = ["tile_vae", 0]
    else:
        wf["tile_vae"] = {"class_type": "MakeCircularVAEDiT", "inputs": {"vae": ["vae", 0], "tiling": "disable", "copy_vae": "Modify in place"}}
        wf["decode"]["inputs"]["vae"] = ["tile_vae", 0]
    return wf


def cmd_workflow(args: argparse.Namespace) -> None:
    if not NAME_PATTERN.match(args.name):
        sys.exit(f"name {args.name!r} must look like T_<category>_<name>, lowercase (e.g. T_terrain_mud_a)")
    if args.width % 16 or args.height % 16:
        sys.exit("width and height must be multiples of 16")
    tile = not args.no_tile
    seed = args.seed if args.seed is not None else secrets.randbelow(2**31)
    steps = args.steps or (TILE_STEPS if tile else IMAGE_STEPS)
    wf = build_workflow(args.prompt, seed, args.width, args.height, steps, tile, args.name)
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(wf, indent=2), encoding="utf-8")
    print(json.dumps({"workflow": str(out.resolve()), "seed": seed, "steps": steps, "tile": tile}))


def seam_scores(img) -> dict:
    import numpy as np

    a = np.asarray(img.convert("RGB"), dtype=np.float32)
    inner_x = np.abs(np.diff(a, axis=1)).mean()
    inner_y = np.abs(np.diff(a, axis=0)).mean()
    return {
        "seam_x": round(float(np.abs(a[:, 0] - a[:, -1]).mean() / inner_x), 2),
        "seam_y": round(float(np.abs(a[0, :] - a[-1, :]).mean() / inner_y), 2),
    }


def cmd_check(args: argparse.Namespace) -> None:
    from PIL import Image

    src = Path(args.image)
    img = Image.open(src).convert("RGB")
    w, h = img.size
    grid = Image.new("RGB", (w * 2, h * 2))
    for x in (0, w):
        for y in (0, h):
            grid.paste(img, (x, y))
    preview = src.with_name(src.stem + "_2x2.png")
    grid.resize((w, h), Image.LANCZOS).save(preview)
    # Contrast-stretched copy: faint straight ghost lines from the tiling roll only show up here.
    import numpy as np

    g = np.asarray(grid.convert("L"), dtype=np.float32)
    lo, hi = np.percentile(g, 2), np.percentile(g, 98)
    contrast = src.with_name(src.stem + "_contrast.png")
    Image.fromarray(np.clip((g - lo) / max(hi - lo, 1) * 255, 0, 255).astype(np.uint8)).resize((w, h), Image.LANCZOS).save(contrast)
    scores = seam_scores(img)
    scores["pass"] = scores["seam_x"] <= SEAM_LIMIT and scores["seam_y"] <= SEAM_LIMIT
    scores["preview"] = str(preview.resolve())
    scores["contrast"] = str(contrast.resolve())
    print(json.dumps(scores))


def cmd_save(args: argparse.Namespace) -> None:
    from PIL import Image

    wf = json.loads(Path(args.workflow).read_text(encoding="utf-8"))
    name = wf["save"]["inputs"]["filename_prefix"].split("/")[-1]
    category = NAME_PATTERN.match(name).group(1)
    tile = wf.get("tile_model") is not None
    img = Image.open(args.image)
    dest = REPO / "art" / "textures" / category
    dest.mkdir(parents=True, exist_ok=True)
    png = dest / f"{name}.png"
    if png.exists() and not args.overwrite:
        sys.exit(f"{png} exists; pass --overwrite to replace it")
    shutil.copyfile(args.image, png)
    sampler = wf["sampler"]["inputs"]
    sidecar = {
        "name": name,
        "kind": "tile" if tile else "image",
        "size": list(img.size),
        "prompt": wf["positive"]["inputs"]["text"],
        "seed": sampler["seed"],
        "steps": sampler["steps"],
        "model": MODEL["name"],
        "model_license": MODEL["license"],
        "seams": seam_scores(img) if tile else None,
        "created": datetime.date.today().isoformat(),
        "workflow": wf,
    }
    (dest / f"{name}.json").write_text(json.dumps(sidecar, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"saved": str(png.relative_to(REPO)), "sidecar": str((dest / f"{name}.json").relative_to(REPO))}))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("workflow", help="write an API-format ComfyUI workflow")
    p.add_argument("--name", required=True, help="T_<category>_<name>, e.g. T_terrain_mud_a")
    p.add_argument("--prompt", required=True)
    p.add_argument("--seed", type=int, help="default: random (recorded in the output)")
    p.add_argument("--width", type=int, default=1024)
    p.add_argument("--height", type=int, default=1024)
    p.add_argument("--steps", type=int, help=f"default: {TILE_STEPS} for tiles, {IMAGE_STEPS} otherwise")
    p.add_argument("--no-tile", action="store_true", help="plain image (icons, concept art) instead of a seamless tile")
    p.add_argument("--out", required=True, help="where to write the workflow JSON")
    p.set_defaults(func=cmd_workflow)

    p = sub.add_parser("check", help="score wrap seams and write <image>_2x2.png")
    p.add_argument("image")
    p.set_defaults(func=cmd_check)

    p = sub.add_parser("save", help="copy an approved image into art/textures/ with a sidecar")
    p.add_argument("image")
    p.add_argument("--workflow", required=True, help="the workflow JSON that produced the image")
    p.add_argument("--overwrite", action="store_true")
    p.set_defaults(func=cmd_save)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
