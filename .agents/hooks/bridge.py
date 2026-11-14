#!/usr/bin/env python3
"""Antigravity lifecycle hook adapter delegating to .claude/hooks/ without duplication."""

from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path
from urllib.parse import urlparse
from urllib.request import url2pathname

PENDING_LINT_FILE = Path(__file__).resolve().parent / ".pending_lint.json"


def _resolve_root(payload: dict) -> Path:
    """Resolve the repository root from workspacePaths, cwd, or script location."""
    for candidate in (payload.get("workspacePaths") or []) + [payload.get("cwd")]:
        if not candidate:
            continue
        if isinstance(candidate, str) and candidate.startswith("file://"):
            parsed = urlparse(candidate)
            local = url2pathname(parsed.path)
            if parsed.netloc:
                local = f"//{parsed.netloc}{local}"
            p = Path(local)
        else:
            p = Path(candidate)
        if p.exists():
            return p
    return Path(__file__).resolve().parents[2]


def _extract_tool_path(payload: dict) -> str:
    """Extract the target file path from an Antigravity edit/write toolCall payload."""
    args = (payload.get("toolCall") or {}).get("args") or {}
    return str(
        args.get("Path")
        or args.get("TargetFile")
        or args.get("file_path")
        or ""
    )


def _run_claude_hook(
    root: Path, script_name: str, stdin_obj: dict | None = None, extra_args: list[str] | None = None
) -> subprocess.CompletedProcess[str]:
    """Execute the canonical hook script under .claude/hooks/ with CLAUDE_PROJECT_DIR set."""
    env = os.environ.copy()
    env["CLAUDE_PROJECT_DIR"] = str(root)
    cmd = [sys.executable, str(root / ".claude" / "hooks" / script_name), *(extra_args or [])]
    return subprocess.run(
        cmd,
        cwd=str(root),
        env=env,
        input=json.dumps(stdin_obj or {}),
        capture_output=True,
        text=True,
        timeout=580,
    )


def _load_json(text: str) -> dict:
    try:
        data = json.loads(text)
        return data if isinstance(data, dict) else {}
    except json.JSONDecodeError:
        return {}


def cmd_session_start(payload: dict, root: Path) -> dict:
    proc = _run_claude_hook(root, "session_start.py")
    msg = (proc.stdout or "").strip()
    if not msg:
        return {}
    return {"injectSteps": [{"systemMessage": {"systemMessage": msg}}]}


def cmd_pre_bash(payload: dict, root: Path) -> dict:
    args = (payload.get("toolCall") or {}).get("args") or {}
    cmd_line = str(args.get("CommandLine") or args.get("command") or "")
    proc = _run_claude_hook(root, "guard_bash.py", {"tool_input": {"command": cmd_line}})
    out = _load_json(proc.stdout).get("hookSpecificOutput") or {}
    decision = out.get("permissionDecision")
    if decision in ("deny", "ask", "force_ask", "allow", "auto_approve"):
        res = {"decision": decision}
        if out.get("permissionDecisionReason"):
            res["reason"] = out["permissionDecisionReason"]
        return res
    return {"decision": "allow"}


def cmd_pre_edit(payload: dict, root: Path) -> dict:
    path = _extract_tool_path(payload)
    proc = _run_claude_hook(root, "guard_files.py", {"tool_input": {"file_path": path}})
    out = _load_json(proc.stdout).get("hookSpecificOutput") or {}
    decision = out.get("permissionDecision")
    if decision in ("deny", "ask", "force_ask", "allow", "auto_approve"):
        res = {"decision": decision}
        if out.get("permissionDecisionReason"):
            res["reason"] = out["permissionDecisionReason"]
        return res
    return {"decision": "allow"}


def cmd_post_edit(payload: dict, root: Path) -> dict:
    path = _extract_tool_path(payload)
    if not path:
        return {}
    proc = _run_claude_hook(root, "sim_lint.py", {"tool_input": {"file_path": path}})
    messages: list[dict] = []
    if PENDING_LINT_FILE.exists():
        try:
            existing = json.loads(PENDING_LINT_FILE.read_text(encoding="utf-8"))
            if isinstance(existing, list):
                messages = existing
        except (OSError, json.JSONDecodeError):
            messages = []

    if proc.returncode == 2 and (proc.stderr or "").strip():
        messages.append({"blocking": True, "message": proc.stderr.strip()})
    else:
        ctx = (_load_json(proc.stdout).get("hookSpecificOutput") or {}).get("additionalContext")
        if ctx:
            messages.append({"blocking": False, "message": ctx})

    if messages:
        PENDING_LINT_FILE.write_text(json.dumps(messages), encoding="utf-8")
    return {}


def cmd_post_invocation(_payload: dict, _root: Path) -> dict:
    if not PENDING_LINT_FILE.exists():
        return {}
    try:
        items = json.loads(PENDING_LINT_FILE.read_text(encoding="utf-8"))
        PENDING_LINT_FILE.unlink(missing_ok=True)
    except (OSError, json.JSONDecodeError):
        return {}
    if not isinstance(items, list) or not items:
        return {}
    combined = "\n\n".join(str(i.get("message", "")) for i in items if i.get("message"))
    if not combined:
        return {}
    out: dict = {"injectSteps": [{"systemMessage": {"systemMessage": combined}}]}
    if any(i.get("blocking") for i in items):
        out["terminationBehavior"] = "force_continue"
    return out


def cmd_stop(payload: dict, root: Path) -> dict:
    if payload.get("continuationActive"):
        return {}
    term = payload.get("terminationReason", "")
    if term and term != "NO_TOOL_CALL":
        return {}
    proc = _run_claude_hook(root, "stop_checks.py", {"stop_hook_active": False})
    if proc.returncode == 2:
        reason = (proc.stderr or proc.stdout or "").strip()
        if reason:
            return {"decision": "continue", "reason": reason}
    return {}


COMMANDS = {
    "session-start": cmd_session_start,
    "pre-bash": cmd_pre_bash,
    "pre-edit": cmd_pre_edit,
    "post-edit": cmd_post_edit,
    "post-invocation": cmd_post_invocation,
    "stop": cmd_stop,
}


def main() -> int:
    if len(sys.argv) < 2 or sys.argv[1] not in COMMANDS:
        print("{}")
        return 0
    raw = sys.stdin.read()
    payload = _load_json(raw) if raw.strip() else {}
    root = _resolve_root(payload)
    try:
        result = COMMANDS[sys.argv[1]](payload, root)
    except Exception:
        result = {"decision": "allow"} if sys.argv[1] in ("pre-bash", "pre-edit") else {}
    print(json.dumps(result))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
