#!/usr/bin/env python3
"""SessionStart: add the current milestone, next items and branch to Claude's context."""
import os
import re
import subprocess
import sys

root = os.environ.get("CLAUDE_PROJECT_DIR", ".")
short = "--short" in sys.argv
reminder = ("Reminder: the simulation decides and Unity only reads it; no floats or unordered "
            "iteration in sim/; outcome changes go through /golden-replay.")
lines = []
status = os.path.join(root, "docs", "milestones", "STATUS.md")
if not short and os.path.exists(status):
    with open(status, encoding="utf-8") as f:
        text = f.read()
    current = re.search(r"^## Current: (.+)$", text, re.M)
    if current:
        lines.append(f"Current milestone: {current.group(1)}")
    todo = re.findall(r"^- \[ \] (.+)$", text, re.M)[:3]
    if todo:
        lines.append("Next items: " + "; ".join(todo))
    try:
        branch = subprocess.run(["git", "branch", "--show-current"], cwd=root,
                                capture_output=True, text=True, timeout=10).stdout.strip()
        lines.append(f"Branch: {branch}")
    except (OSError, subprocess.TimeoutExpired):
        pass
lines.append(reminder)
print("\n".join(lines))
