#!/usr/bin/env python3
"""One-time setup for a fresh clone: Git LFS, the UnityYAMLMerge merge driver, .NET local tools.

Usage: python tools/setup_dev.py [--unity-yaml-merge <path to UnityYAMLMerge(.exe)>]

Standard library only. Safe to re-run; it only writes this clone's local git config.
"""
import argparse
import glob
import os
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Where Unity Hub installs editors by default, per OS.
YAML_MERGE_GLOBS = [
    r"C:\Program Files\Unity\Hub\Editor\*\Editor\Data\Tools\UnityYAMLMerge.exe",
    os.path.expanduser("~/Unity/Hub/Editor/*/Editor/Data/Tools/UnityYAMLMerge"),
    "/Applications/Unity/Hub/Editor/*/Unity.app/Contents/Tools/UnityYAMLMerge",
]


def run(*args, check=True):
    print("+ " + " ".join(args))
    return subprocess.run(args, cwd=ROOT, check=check)


def find_yaml_merge():
    found = []
    for pattern in YAML_MERGE_GLOBS:
        found.extend(glob.glob(pattern))
    # Prefer a 6000.3 (Unity 6.3 LTS) editor, then the newest version string.
    found.sort(key=lambda p: ("6000.3" in p, p), reverse=True)
    return found[0] if found else None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-yaml-merge", help="path to UnityYAMLMerge if it isn't found automatically")
    args = parser.parse_args()

    if shutil.which("git-lfs") or shutil.which("git-lfs.exe"):
        run("git", "lfs", "install")
        run("git", "lfs", "pull", check=False)
    else:
        print("! Git LFS is not installed: install it, then re-run this script.", file=sys.stderr)

    merge = args.unity_yaml_merge or find_yaml_merge()
    if merge and os.path.exists(merge):
        path = merge.replace("\\", "/")
        run("git", "config", "merge.unityyamlmerge.name", "Unity SmartMerge (UnityYAMLMerge)")
        run("git", "config", "merge.unityyamlmerge.driver", f'"{path}" merge -h -p --force %O %B %A %A')
        run("git", "config", "merge.unityyamlmerge.recursive", "binary")
    else:
        print("! UnityYAMLMerge not found; install Unity 6.3 LTS through Unity Hub, or pass --unity-yaml-merge.",
              file=sys.stderr)

    if shutil.which("dotnet"):
        run("dotnet", "tool", "restore")
    else:
        print("! The .NET 10 SDK is not installed (see README, Prerequisites).", file=sys.stderr)


if __name__ == "__main__":
    main()
