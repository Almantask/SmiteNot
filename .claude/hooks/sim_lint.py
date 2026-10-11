#!/usr/bin/env python3
"""PostToolUse (Edit|Write): format C#; flag nondeterminism in sim/src at once.

The build enforces the same rules (GG0001 and the banned-API analyzer); this hook gives the feedback
the moment a file is written instead of at the next build.
"""
import json
import os
import re
import subprocess
import sys

event = json.load(sys.stdin)
path = (event.get("tool_input") or {}).get("file_path", "")
if not path.endswith(".cs") or not os.path.exists(path):
    sys.exit(0)
root = os.environ.get("CLAUDE_PROJECT_DIR", os.getcwd())
try:
    subprocess.run(["dotnet", "csharpier", "format", path], cwd=root, capture_output=True, timeout=30)
except (OSError, subprocess.TimeoutExpired):
    pass  # formatting is best-effort; CI checks it anyway

rel = os.path.relpath(os.path.abspath(path), root).replace("\\", "/")
if not rel.startswith("sim/src/"):
    sys.exit(0)

with open(path, encoding="utf-8") as f:
    source = f.read()
# Strip comments and string literals so they don't trigger false positives; keep line breaks for line numbers.
code = re.sub(r"//[^\n]*|/\*.*?\*/", lambda m: "\n" * m.group(0).count("\n"), source, flags=re.S)
code = re.sub(r'@?"(?:[^"\\\n]|\\.)*"', '""', code)

ERRORS = [
    (r"\b(float|double|decimal)\b|\bSystem\.(Single|Double|Decimal)\b", "floating-point type: use Fix or integers"),
    (r"\b\d+\.\d+\b|\b\d+(\.\d+)?[fFdDmM]\b", "floating-point literal: use Fix or integers"),
    (r"\bMathF\.|\bMath\.(Sqrt|Sin|Cos|Tan|Atan2?|Pow|Exp|Log\d*|Round|Floor|Ceiling)\b",
     "floating-point maths: use the GodGame.Sim.Math helpers"),
    (r"\bnew\s+Random\b|\bSystem\.Random\b", "System.Random: use SimRandom streams"),
    (r"\bDateTime\.(Now|UtcNow)\b|\bStopwatch\b|\bEnvironment\.TickCount", "wall-clock time in the simulation"),
    (r"\bGuid\.NewGuid\b", "random GUIDs: use deterministic ids"),
    (r"\bUnityEngine\b", "Unity reference in the simulation"),
    (r"\bParallel\.|\bTask\.Run\b|\bnew\s+Thread\b|\bThreadPool\.", "ad-hoc threading: use IParallelRunner"),
]
WARNINGS = [
    (r"\b(Dictionary|HashSet)<", "Dictionary/HashSet: make sure iteration order never affects state"),
    (r"\.GetHashCode\(\)", "GetHashCode: string hashes are randomised per process; use SimHash"),
    (r"\bstatic\s+(?!readonly\b|class\b)[\w<>\[\],\s]+?\s+\w+\s*(;|=(?!>))", "possible static mutable state"),
]


def scan(rules):
    found = []
    for pattern, message in rules:
        for m in re.finditer(pattern, code):
            line = code.count("\n", 0, m.start()) + 1
            entry = f"{rel}:{line}: {message}"
            if entry not in found:
                found.append(entry)
    return found


errors, warnings = scan(ERRORS), scan(WARNINGS)
if errors:
    print("Determinism violations (fix before continuing):\n" + "\n".join(errors), file=sys.stderr)
    sys.exit(2)
if warnings:
    print(json.dumps({"hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": "Determinism check warnings:\n" + "\n".join(warnings)}}))
sys.exit(0)
