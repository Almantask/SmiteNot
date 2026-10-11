#!/usr/bin/env python3
"""PreToolUse (Bash): block dangerous git and shell commands."""
import json
import re
import sys

cmd = (json.load(sys.stdin).get("tool_input") or {}).get("command", "")
RULES = [
    (r"git\s+push\b.*\s(--force(?!-with-lease)|-f)\b", "deny", "Force-push is not allowed."),
    (r"--no-verify\b", "deny", "Don't skip git hooks."),
    (r"\brm\s+-[a-zA-Z]*[rR][a-zA-Z]*\s+(/|~|\.\.)", "deny", "Deleting outside the repository is not allowed."),
    (r"git\s+reset\s+--hard\b", "ask", "Hard reset discards work."),
]
for pattern, decision, reason in RULES:
    if re.search(pattern, cmd):
        print(json.dumps({"hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": decision,
            "permissionDecisionReason": reason}}))
        break
sys.exit(0)
