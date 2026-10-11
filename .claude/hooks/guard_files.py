#!/usr/bin/env python3
"""PreToolUse (Edit|Write): deny or ask for protected paths."""
import fnmatch
import json
import os
import sys

event = json.load(sys.stdin)
path = (event.get("tool_input") or {}).get("file_path", "")
if not path:
    sys.exit(0)
root = os.environ.get("CLAUDE_PROJECT_DIR", os.getcwd())
rel = os.path.relpath(os.path.abspath(path), root).replace("\\", "/")

ALLOW = [
    "sim/tests/golden/README.md",
]
DENY = [
    ("sim/tests/golden/*", "Golden files change only through /golden-replay (it bumps SimVersion)."),
    ("*.meta", "Unity generates .meta files; don't write them by hand."),
]
ASK = [
    ("unity/ProjectSettings/*", "Unity project settings"),
    ("unity/Packages/manifest.json", "Unity package manifest"),
    ("*.unity", "Unity scene YAML"),
    ("*.prefab", "Unity prefab YAML"),
    ("docs/design/game-brief.md", "the game brief (source of truth)"),
]


def decide(decision, reason):
    print(json.dumps({"hookSpecificOutput": {
        "hookEventName": "PreToolUse",
        "permissionDecision": decision,
        "permissionDecisionReason": reason}}))
    sys.exit(0)


if rel in ALLOW:
    sys.exit(0)
for pattern, reason in DENY:
    if fnmatch.fnmatch(rel, pattern):
        decide("deny", reason)
for pattern, what in ASK:
    if fnmatch.fnmatch(rel, pattern):
        decide("ask", f"Editing {what}: confirm this is intended.")
sys.exit(0)
