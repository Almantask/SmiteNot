---
name: unity-tests
description: Run Unity Edit Mode and Play Mode tests via Unity MCP or headless batch mode and report compiler errors and test failures. Use when validating Unity bridge, editor, or rendering behaviour, or when invoked via /unity-tests.
---

# Run Unity Tests

## Inputs

Extract the requested test platform from the prompt: `editmode`, `playmode`, or `all` (defaults to `all`).

## Workflow

1. **Editor open (`unityMCP` / `UnityMCP` in `<mcp_servers>`):**
   - Read the console and test-runner schemas under `<appDataDir>/mcp/<serverName>/` with `read_file` and call them via `call_mcp_tool` (or direct `mcp_<serverName>_*` if eager).
   - Check the Unity console for C# compiler errors **first**—test runs are meaningless while compilation fails.
   - Note: `Run In Background` is off in `ProjectSettings`, so Play Mode pauses when the editor window is unfocused; verify Play Mode progression via the simulation tick rather than `Time.frameCount`.
2. **Editor closed (PowerShell batch mode):**
   Locate the installed Unity 6 editor (project targets `6000.6.7f1`, typically under `C:\Program Files\Unity\Hub\Editor\6000.*\Editor\Unity.exe`) and run once per requested platform from the repository root via `run_command` (using PowerShell's `&` call operator):
   ```powershell
   $unity = (Get-Item "C:\Program Files\Unity\Hub\Editor\6000.*\Editor\Unity.exe" | Sort-Object FullName -Descending | Select-Object -First 1).FullName
   & $unity -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile TestResults/editmode.log
   ```
   - For `PlayMode`, pass `-testPlatform PlayMode` with `TestResults/playmode.xml` and `TestResults/playmode.log` (omit `-nographics` when running GPU/rendering tests).
   - Batch runs often continue as an async background task; end your turn and wait for Antigravity's completion wakeup rather than polling.
3. **Summarise results.** Read `TestResults/<platform>.xml` with `read_file`: report pass/fail/skip totals, then each failing test with its assertion message and the first stack frame inside repository code (`unity/Assets/_Game/` or `sim/`).
4. **Compiler errors first.** If no XML results were written or compilation failed, read `TestResults/<platform>.log` and report the first compiler errors instead.
5. **Scope reminder.** Gameplay outcomes belong in `dotnet test sim/GodGame.Sim.sln`; Unity tests cover only the `SimHost`/`BattleHost` bridge, authoring windows, and rendering/UI presentation.
