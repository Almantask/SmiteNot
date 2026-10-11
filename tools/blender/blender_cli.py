"""Shared helpers for the headless Blender scripts.

The scripts run two ways:
  blender -b <file.blend> --python tools/blender/<script>.py -- <args>     (Blender installed)
  python tools/blender/<script>.py --blend <file.blend> <args>             (the `bpy` module from PyPI)
"""
import argparse
import json
import os
import sys

import bpy

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def parse_args(parser: argparse.ArgumentParser):
    """Parse the script's own arguments (those after `--` when run inside Blender) and open --blend if given."""
    parser.add_argument("--blend", help="open this .blend first (needed when running under plain Python)")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    args = parser.parse_args(argv)
    if args.blend:
        bpy.ops.wm.open_mainfile(filepath=os.path.abspath(args.blend))
    return args


def blend_name():
    """The open file's name without extension, which is also the asset name (U_<type>_<variant>)."""
    return os.path.splitext(os.path.basename(bpy.data.filepath))[0]


def load_json(repo_relative_path):
    with open(os.path.join(REPO_ROOT, repo_relative_path), encoding="utf-8") as f:
        return json.load(f)


def write_json(path, data):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, sort_keys=True)
        f.write("\n")
