@[SmiteNot project instructions](CLAUDE.md)

## Antigravity Notes
- **Rules:** Path-scoped rules in `.claude/rules/` are mirrored without duplication via `.agents/rules/` (`trigger: glob`).
- **Skills:** Workspace skills live in `.agents/skills/`. Read the relevant `SKILL.md` with `read_file` when a task matches (e.g., `milestone`, `scenario-test`, `golden-replay`, `perf-check`, `unity-tests`, `art-export`, `model-asset`, `texture-gen`, `sound-gen`, `game-design`, `desync-hunt`, `review-idea`, `adr`, `new-power`, `new-terrain-piece`, `new-unit-type`, `new-field`).
- **Subagents:** The 13 project roles (`sim-engineer`, `determinism-auditor`, `code-reviewer`, `ai-engineer`, `perf-engineer`, `unity-engineer`, `tech-artist`, `test-engineer`, `design-lead`, `level-designer`, `game-designer`, `veteran-player`, `casual-gamer`) are registered by `.agents/plugins/smitenot/` and invoked with `invoke_subagent`.
- **MCP servers:** `unityMCP` runs globally; `smitenot_blender` (`blender`) is provided by `.agents/plugins/smitenot/`; `smitenot-local_comfy` (`comfy`) and `smitenot-local_thunder-fx` (`thunder-fx`) are provided by `.agents/plugins/smitenot-local/`. Read schemas under `<appDataDir>/mcp/<serverName>/` and call via `call_mcp_tool`.
- **Hooks:** `.agents/hooks.json` delegates `SessionStart`, `PreToolUse`, `PostToolUse`, `PostInvocation`, and `Stop` checks to `.claude/hooks/` via `.agents/hooks/bridge.py`.
