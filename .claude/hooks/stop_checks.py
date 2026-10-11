#!/usr/bin/env python3
"""Stop: if sim/ or data/ changed, run the fast tests; block the stop while they fail."""
import hashlib
import json
import os
import subprocess
import sys

event = json.load(sys.stdin)
if event.get("stop_hook_active"):
    sys.exit(0)  # already continuing because of this hook; avoid loops
root = os.environ.get("CLAUDE_PROJECT_DIR", os.getcwd())


def git(*args):
    return subprocess.run(["git", *args], cwd=root, capture_output=True, text=True).stdout


changed = [line[3:] for line in git("status", "--porcelain").splitlines()]
if not any(p.startswith(("sim/", "data/")) for p in changed):
    sys.exit(0)

# Skip if nothing changed since the last green run.
state = hashlib.sha1((git("diff") + "".join(sorted(changed))).encode()).hexdigest()
marker = os.path.join(root, ".claude", ".last_green")
if os.path.exists(marker):
    with open(marker, encoding="utf-8") as f:
        if f.read() == state:
            sys.exit(0)

try:
    result = subprocess.run(
        ["dotnet", "test", "sim/GodGame.Sim.sln", "--filter", "Category=Fast", "--nologo", "-v", "q"],
        cwd=root, capture_output=True, text=True, timeout=540)
except (OSError, subprocess.TimeoutExpired) as e:
    print(f"Stop hook couldn't run the fast tests: {e}", file=sys.stderr)
    sys.exit(0)  # don't block on a broken toolchain; CI still runs the tests
if result.returncode != 0:
    tail = "\n".join((result.stdout + result.stderr).splitlines()[-40:])
    print("Fast simulation tests are failing; fix them before finishing:\n" + tail, file=sys.stderr)
    sys.exit(2)
with open(marker, "w", encoding="utf-8") as f:
    f.write(state)
sys.exit(0)
