#!/usr/bin/env python3
"""Antigravity lifecycle hook adapter delegating to .claude/hooks/ without duplication."""

from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys
from pathlib import Path
from urllib.parse import urlparse
from urllib.request import url2pathname

PENDING_LINT_FILE = Path(__file__).resolve().parent / ".pending_lint.json"
STOP_GUARD_FILE = Path(__file__).resolve().parent / ".stop_guard.json"


def _to_path(candidate: object) -> Path | None:
    if not candidate or not isinstance(candidate, str):
        return None
    if candidate.startswith("file://"):
        parsed = urlparse(candidate)
        local = url2pathname(parsed.path)
        if parsed.netloc:
            local = f"//{parsed.netloc}{local}"
        return Path(local)
    return Path(candidate)


def _resolve_root(payload: dict) -> Path:
    """Resolve the repository root from workspacePaths, cwd, or script location."""
    for candidate in (payload.get("workspacePaths") or []) + [payload.get("cwd")]:
        p = _to_path(candidate)
        if p and p.exists():
            return p
    return Path(__file__).resolve().parents[2]


def _tool_path_from_args(args: object) -> str:
    if isinstance(args, str):
        args = _load_json(args)
    if not isinstance(args, dict):
        return ""
    return str(
        args.get("Path")
        or args.get("TargetFile")
        or args.get("file_path")
        or args.get("path")
        or ""
    )


def _extract_tool_path(payload: dict) -> str:
    """Extract the target file path from an Antigravity edit/write toolCall payload."""
    return _tool_path_from_args((payload.get("toolCall") or {}).get("args"))


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


def _git(root: Path, *args: str) -> str:
    proc = subprocess.run(["git", *args], cwd=str(root), capture_output=True, text=True)
    return proc.stdout if proc.returncode == 0 else ""


def _sim_data_state(root: Path) -> str:
    """Return a SHA-1 fingerprint of uncommitted changes under sim/ or data/, or '' if clean."""
    lines = _git(root, "status", "--porcelain", "--", "sim", "data").splitlines()
    if not lines:
        return ""
    h = hashlib.sha1()
    h.update(_git(root, "diff", "HEAD", "--", "sim", "data").encode("utf-8"))
    for line in sorted(lines):
        h.update(line.encode("utf-8"))
        rel = line[3:].strip().strip('"').split(" -> ")[-1]
        p = root / rel
        if p.is_file():
            try:
                st = p.stat()
                h.update(f":{st.st_mtime_ns}:{st.st_size}".encode("utf-8"))
            except OSError:
                pass
    return h.hexdigest()


_EDIT_TOOLS = frozenset({
    "edit_file",
    "write_file",
    "replace_file_content",
    "multi_replace_file_content",
    "write_to_file",
    "apply_patch",
})


def _load_stop_guard() -> dict[str, list[str]]:
    if not STOP_GUARD_FILE.exists():
        return {}
    try:
        data = json.loads(STOP_GUARD_FILE.read_text(encoding="utf-8"))
        if isinstance(data, dict):
            out: dict[str, list[str]] = {}
            for k, v in data.items():
                if isinstance(v, list):
                    out[str(k)] = [str(x) for x in v]
                elif isinstance(v, str):
                    out[str(k)] = [v]
            return out
    except (OSError, json.JSONDecodeError):
        pass
    return {}


def _save_stop_guard(guard: dict[str, list[str]]) -> None:
    trimmed = dict(list(guard.items())[-32:])
    try:
        STOP_GUARD_FILE.write_text(json.dumps(trimmed), encoding="utf-8")
    except OSError:
        pass


def _is_sim_or_data_path(target: str, root: Path) -> bool:
    if not target:
        return False
    p = Path(target)
    try:
        resolved_root = root.resolve()
        resolved = p.resolve() if p.is_absolute() else (resolved_root / p).resolve()
        rel = resolved.relative_to(resolved_root)
    except (OSError, ValueError):
        return False
    return bool(rel.parts) and rel.parts[0].lower() in ("sim", "data")


def _blocked_this_turn_without_sim_edit(payload: dict, root: Path) -> bool:
    """Return True if a Stop block already fired this turn with no sim/data edit after it."""
    tp = _to_path(payload.get("transcriptPath"))
    if not tp or not tp.is_file():
        return False
    try:
        size = tp.stat().st_size
        with tp.open("rb") as f:
            if size > 524288:
                f.seek(size - 524288)
                f.readline()  # discard partial first line
            lines = f.read().decode("utf-8", errors="replace").splitlines()
    except OSError:
        return False

    for raw in reversed(lines):
        if not raw.strip():
            continue
        obj = _load_json(raw)
        step_type = obj.get("type")
        if step_type in ("USER_INPUT", "CHECKPOINT"):
            return False
        if step_type == "PLANNER_RESPONSE":
            for tc in obj.get("tool_calls") or []:
                name = str(tc.get("name") or "")
                if name == "run_command":
                    return False
                if name in _EDIT_TOOLS:
                    if _is_sim_or_data_path(
                        _tool_path_from_args(tc.get("arguments") or tc.get("args")), root
                    ):
                        return False
        if step_type == "SYSTEM_MESSAGE" and "Stop hook blocked termination:" in str(
            obj.get("content") or ""
        ):
            return True
    return False


def cmd_stop(payload: dict, root: Path) -> dict:
    if payload.get("continuationActive"):
        return {}
    if int(payload.get("executionNum") or 0) > 0:
        return {}
    if str(payload.get("lastUserInput") or "").startswith("Stop hook blocked termination:"):
        return {}
    term = payload.get("terminationReason", "")
    if term and term != "NO_TOOL_CALL":
        return {}
    if _blocked_this_turn_without_sim_edit(payload, root):
        return {}
    state = _sim_data_state(root)
    if not state:
        return {}
    conv_id = str(payload.get("conversationId") or "default")
    guard = _load_stop_guard()
    if state in guard.get(conv_id, ()):
        return {}
    proc = _run_claude_hook(root, "stop_checks.py", {"stop_hook_active": False})
    if proc.returncode == 2:
        reason = (proc.stderr or proc.stdout or "").strip()
        if reason:
            post_state = _sim_data_state(root)
            seen = list(guard.pop(conv_id, []))
            for s in (state, post_state):
                if s and s not in seen:
                    seen.append(s)
            guard[conv_id] = seen[-4:]
            _save_stop_guard(guard)
            return {"decision": "continue", "reason": reason}
    elif conv_id in guard:
        del guard[conv_id]
        _save_stop_guard(guard)
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
