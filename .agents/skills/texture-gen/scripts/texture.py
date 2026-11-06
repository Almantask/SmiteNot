"""Texture helper for the texture-gen skill.

  workflow  write an API-format ComfyUI workflow (Z-Image Turbo): a tile (roll or repair method) or a plain image
  repair    turn a repair-method base image into a seamless tile: shift, soften the seam band, write the repair workflow
  check     score a tile's wrap seams and write a 2x2 preview and a contrast-stretched copy next to it
  save      copy an approved image into art/textures/<category>/ with a JSON sidecar

Run through uv so Pillow and NumPy are available:
  uv run --no-project --with pillow --with numpy python .agents/skills/texture-gen/scripts/texture.py <command> --help
"""

import argparse
import datetime
import json
import re
import secrets
import shutil
import sys
from pathlib import Path


def _find_repo_root() -> Path:
    here = Path(__file__).resolve()
    for parent in here.parents:
        if (parent / ".git").exists():
            return parent
    return here.parents[4]


REPO = _find_repo_root()

# The only model cleared for shipped assets (Apache 2.0). Files live in ComfyUI's models/ folders.
MODEL = {
    "name": "Z-Image Turbo bf16",
    "license": "Apache-2.0",
    "unet": "z_image_turbo_bf16.safetensors",
    "clip": "qwen_3_4b.safetensors",
    "clip_type": "lumina2",
    "vae": "ae.safetensors",
}

# The roll method needs 16 steps or more; below that it leaves faint straight ghost lines inside the tile.
ROLL_STEPS = 16
IMAGE_STEPS = 8

# Repair method: half-width of the repainted cross band in pixels, and how much of it is repainted.
# Tested on flat mud: 0.6 keeps a smeared patch, 0.8 starts inventing shapes in the band.
REPAIR_BAND = 112
REPAIR_DENOISE = 0.7

# A wrap edge scoring above this is a visible seam (untiled images score about 5; good tiles under 1.5).
SEAM_LIMIT = 1.5

NAME_PATTERN = re.compile(r"^T_([a-z]+)_[a-z0-9_]+$")
MASK_PATTERN = re.compile(r"_b(\d+)_mask\.png$")


def base_nodes(prompt: str, seed: int, steps: int, name: str) -> dict:
    """Loaders, prompt, sampler, decode and save; callers add the latent source and any tiling."""
    return {
        "unet": {"class_type": "UNETLoader", "inputs": {"unet_name": MODEL["unet"], "weight_dtype": "default"}},
        "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": MODEL["clip"], "type": MODEL["clip_type"], "device": "default"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": MODEL["vae"]}},
        # "Make a copy" deep-copies the VAE, which fails under ComfyUI's dynamic VRAM. In place is safe because
        # every workflow from this script sets the mode explicitly ("disable" restores normal padding).
        "tile_vae": {"class_type": "MakeCircularVAEDiT", "inputs": {"vae": ["vae", 0], "tiling": "disable", "copy_vae": "Modify in place"}},
        "shift": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["unet", 0], "shift": 3}},
        "positive": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["clip", 0]}},
        "negative": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["positive", 0]}},
        "sampler": {
            "class_type": "KSampler",
            "inputs": {
                "seed": seed, "steps": steps, "cfg": 1.0, "sampler_name": "res_multistep", "scheduler": "simple",
                "denoise": 1.0, "model": ["shift", 0], "positive": ["positive", 0], "negative": ["negative", 0],
                "latent_image": ["latent", 0],
            },
        },
        "decode": {"class_type": "VAEDecode", "inputs": {"samples": ["sampler", 0], "vae": ["tile_vae", 0]}},
        "save": {"class_type": "SaveImage", "inputs": {"images": ["decode", 0], "filename_prefix": f"texture-gen/{name}"}},
    }


def build_workflow(prompt: str, seed: int, width: int, height: int, steps: int, roll: bool, name: str) -> dict:
    wf = base_nodes(prompt, seed, steps, name)
    wf["latent"] = {"class_type": "EmptySD3LatentImage", "inputs": {"width": width, "height": height, "batch_size": 1}}
    if roll:
        # Universal Seamless Tiles: roll the latent during sampling, wrap the VAE decoder's padding.
        wf["tile_model"] = {"class_type": "SeamlessTileModelDiT", "inputs": {"model": ["shift", 0], "tiling": "enable", "seed": seed}}
        wf["tile_vae"]["inputs"]["tiling"] = "enable"
        wf["sampler"]["inputs"]["model"] = ["tile_model", 0]
    return wf


def build_repair_workflow(base: dict, rolled: str, mask: str, denoise: float) -> dict:
    sampler = base["sampler"]["inputs"]
    name = base["save"]["inputs"]["filename_prefix"].split("/")[-1]
    wf = base_nodes(base["positive"]["inputs"]["text"], sampler["seed"], sampler["steps"], name)
    wf["src"] = {"class_type": "LoadImage", "inputs": {"image": rolled}}
    wf["mask"] = {"class_type": "LoadImageMask", "inputs": {"image": mask, "channel": "red"}}
    wf["encode"] = {"class_type": "VAEEncode", "inputs": {"pixels": ["src", 0], "vae": ["tile_vae", 0]}}
    wf["latent"] = {"class_type": "SetLatentNoiseMask", "inputs": {"samples": ["encode", 0], "mask": ["mask", 0]}}
    wf["sampler"]["inputs"]["denoise"] = denoise
    # Only the band changes; everything else, including all four edges, keeps the shifted base pixels.
    wf["composite"] = {
        "class_type": "ImageCompositeMasked",
        "inputs": {"destination": ["src", 0], "source": ["decode", 0], "x": 0, "y": 0, "resize_source": False, "mask": ["mask", 0]},
    }
    wf["save"]["inputs"]["images"] = ["composite", 0]
    return wf


def cmd_workflow(args: argparse.Namespace) -> None:
    if not NAME_PATTERN.match(args.name):
        sys.exit(f"name {args.name!r} must look like T_<category>_<name>, lowercase (e.g. T_terrain_mud_a)")
    if args.width % 16 or args.height % 16:
        sys.exit("width and height must be multiples of 16")
    roll = args.method == "roll" and not args.no_tile
    seed = args.seed if args.seed is not None else secrets.randbelow(2**31)
    steps = args.steps or (ROLL_STEPS if roll else IMAGE_STEPS)
    wf = build_workflow(args.prompt, seed, args.width, args.height, steps, roll, args.name)
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(wf, indent=2), encoding="utf-8")
    kind = "image" if args.no_tile else f"tile ({args.method})"
    report = {"workflow": str(out.resolve()), "kind": kind, "seed": seed, "steps": steps}
    if kind == "tile (repair)":
        report["next"] = "run it, then: texture.py repair <output png> --workflow <this workflow>"
    print(json.dumps(report))


def cmd_repair(args: argparse.Namespace) -> None:
    import numpy as np
    from PIL import Image, ImageFilter

    base = json.loads(Path(args.workflow).read_text(encoding="utf-8"))
    if "tile_model" in base or "src" in base:
        sys.exit("--workflow must be a repair-method base workflow (texture.py workflow --method repair)")
    name = base["save"]["inputs"]["filename_prefix"].split("/")[-1]
    seed = base["sampler"]["inputs"]["seed"]
    img = Image.open(args.image).convert("RGB")
    w, h = img.size
    band = args.band
    # Shift by half: the old edges meet in a cross in the middle, and the new edges wrap exactly.
    rolled = np.roll(np.asarray(img), (h // 2, w // 2), axis=(0, 1))
    m = np.zeros((h, w), np.uint8)
    m[:, w // 2 - band : w // 2 + band] = 255
    m[h // 2 - band : h // 2 + band, :] = 255
    mask = Image.fromarray(m).filter(ImageFilter.GaussianBlur(band // 2))
    # Soften the hard seam inside the band first, or the model traces it with a line of pebbles or dots.
    # A stronger blur smears dark features across the band and leaves a darker patch behind.
    rolled_img = Image.fromarray(rolled)
    softened = Image.composite(rolled_img.filter(ImageFilter.GaussianBlur(band // 4)), rolled_img, mask)
    out_dir = Path(args.out_dir or Path(args.image).parent)
    out_dir.mkdir(parents=True, exist_ok=True)
    rolled_path = out_dir / f"{name}_s{seed}_rolled.png"
    mask_path = out_dir / f"{name}_s{seed}_b{band}_mask.png"
    softened.save(rolled_path)
    mask.save(mask_path)
    wf = build_repair_workflow(base, rolled_path.name, mask_path.name, args.denoise)
    wf_path = out_dir / "repair_workflow.json"
    wf_path.write_text(json.dumps(wf, indent=2), encoding="utf-8")
    print(json.dumps({
        "upload": [str(rolled_path.resolve()), str(mask_path.resolve())],
        "workflow": str(wf_path.resolve()),
        "denoise": args.denoise,
        "band": band,
    }))


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
    import numpy as np
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
    # Contrast-stretched copy: faint ghost lines and repaint marks that are easy to miss at normal contrast.
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
    method = "roll" if "tile_model" in wf else "repair" if "src" in wf else None
    if method == "repair" and not args.base_workflow:
        sys.exit("a repaired tile needs --base-workflow (the workflow that made the base image)")
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
        "kind": "tile" if method else "image",
        "method": method,
        "size": list(img.size),
        "prompt": wf["positive"]["inputs"]["text"],
        "seed": sampler["seed"],
        "steps": sampler["steps"],
        "model": MODEL["name"],
        "model_license": MODEL["license"],
        "seams": seam_scores(img) if method else None,
        "created": datetime.date.today().isoformat(),
    }
    if method == "repair":
        # Regenerate: run base_workflow, then texture.py repair with this band and denoise, then workflow.
        sidecar["repair"] = {"band": int(MASK_PATTERN.search(wf["mask"]["inputs"]["image"]).group(1)), "denoise": sampler["denoise"]}
        sidecar["base_workflow"] = json.loads(Path(args.base_workflow).read_text(encoding="utf-8"))
    sidecar["workflow"] = wf
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
    p.add_argument("--method", choices=["repair", "roll"], default="repair", help="tiling method (default: repair)")
    p.add_argument("--steps", type=int, help=f"default: {ROLL_STEPS} for the roll method, {IMAGE_STEPS} otherwise")
    p.add_argument("--no-tile", action="store_true", help="plain image (icons, concept art) instead of a seamless tile")
    p.add_argument("--out", required=True, help="where to write the workflow JSON")
    p.set_defaults(func=cmd_workflow)

    p = sub.add_parser("repair", help="make a seamless tile from a repair-method base image")
    p.add_argument("image", help="the base image the workflow produced")
    p.add_argument("--workflow", required=True, help="the base workflow JSON")
    p.add_argument("--band", type=int, default=REPAIR_BAND, help=f"half-width of the repainted band in px (default {REPAIR_BAND})")
    p.add_argument("--denoise", type=float, default=REPAIR_DENOISE, help=f"how much of the band is repainted (default {REPAIR_DENOISE})")
    p.add_argument("--out-dir", help="default: the image's folder")
    p.set_defaults(func=cmd_repair)

    p = sub.add_parser("check", help="score wrap seams and write <image>_2x2.png and <image>_contrast.png")
    p.add_argument("image")
    p.set_defaults(func=cmd_check)

    p = sub.add_parser("save", help="copy an approved image into art/textures/ with a sidecar")
    p.add_argument("image")
    p.add_argument("--workflow", required=True, help="the workflow JSON that produced the image")
    p.add_argument("--base-workflow", help="for repaired tiles: the workflow that produced the base image")
    p.add_argument("--overwrite", action="store_true")
    p.set_defaults(func=cmd_save)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
