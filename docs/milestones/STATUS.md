# Milestone status

## Current: M0 — Foundations
Started: 2026-10-11

### Checklist
- [ ] 1. Repository basics (gitignore, gitattributes + LFS, editorconfig, branch protection) — files done; branch protection on `main` needs a repository admin (README, "Branches, pull requests and CI")
- [ ] 2. Design docs, ADR 0001, this file — ADR 0001, this file, the plan and the content roadmap are in; copy the other design docs from the claude.ai project (docs/design/README.md lists them)
- [x] 3. Simulation solution with analyzers and first tests
- [x] 4. CI: sim.yml with cross-OS hash check
- [ ] 5. Unity 6.3 LTS project with the sim package and SimHost cube — scaffold, SimHost, setup script and Edit Mode tests are in; open it in Unity, run GodGame → Setup → Apply Project Settings, commit the generated ProjectSettings and .meta files
- [ ] 6. Claude Code: CLAUDE.md, rules, agents, skills, hooks, MCP — all files are in and the hooks were exercised; install MCP for Unity locally (README, "Claude Code")
- [ ] 7. Blender export and validation scripts, test soldier imported — scripts done; U_testsoldier_a exported and validated with Blender 5.2.2 LTS; import into Unity (ArtImportPostprocessor) waits for a licensed editor
- [ ] 8. Exit criteria verified — three of six verified in the cloud session, plus the Blender half of the MCP criterion; the rest wait for a Unity license

### Exit criteria
- [x] dotnet test green on Windows and Linux; hashes match — CI run 1 (2026-10-11): both test jobs and cross-os green
- [ ] Unity opens cleanly; the cube moves with the simulation — the project moved to Unity 6000.6.7f1 (2026-11-01) and opens locally in batch mode with no compile errors; the cube check and Apply Project Settings are still open
- [x] A float in sim/src fails the build and the hook flags it — GG0001 fails the build (CI runs tools/ci/check_sim_analyzers.py); sim_lint.py flags it on write
- [ ] /context, /agents and /milestone work — a restarted session on this branch loaded CLAUDE.md, the art rule on touching tools/blender, all 13 agents and the SessionStart hook; run /milestone and /context once locally
- [ ] Unity MCP reads the console; Blender MCP lists the scene — Blender MCP verified in the cloud (5.2.2 on Xvfb) and locally (5.2.3, add-on 1.8): get_scene_info lists RIG_humanoid and the three U_testsoldier_a LODs; MCP for Unity v10.3.0 is in the manifest and shared .mcp.json, and a fresh session connects to the editor with 48 UnityMCP tools including read_console; reading the console itself is still to check
- [x] Test soldier exports with a clean validation report — 0 errors, 2 expected warnings (capsule LODs under the hero and mid targets); a broken copy fails with 6 errors

### Decisions needed
- (none for M0)

### Notes
- The banned-API analyzer alone doesn't reject `float x = 1f;`; the project analyzer GG0001 (sim/analyzers) does. Recorded in the plan, Appendix A, "As built in M0".
- The three /review-idea reviewer agents were drafted in the repo because the claude.ai originals weren't available; replace them when copying the design docs.
- unity/Packages/manifest.json holds the versions Unity 6000.6.7f1 upgraded the project to (URP and VFX Graph 17.6.0, uGUI 2.6.0, Test Framework 1.8.0, Input System 1.20.1, Burst 2.0.0, Collections 6.6.0, Mathematics 1.4.0, Cinemachine 6.6.0) plus MCP for Unity v10.3.0; ProjectVersion.txt names 6000.6.7f1 (b22767f04f04).
- The Blender scripts also pass under the real Blender 5.2.2 LTS binary, with the same results as the bpy module (exit code 1 on a broken file).
- To finish M0 locally: copy the design docs; open unity/ in Unity 6000.6.7f1 and run GodGame → Setup → Apply Project Settings, check the cube moves and the Edit Mode tests pass, and commit the generated files; run MCP for Unity's Configure All Detected Clients and check it reads the console; set branch protection on main. (Or add the Unity license variables to the cloud environment and a cloud session can do the Unity steps.)

## Done
- (nothing yet)
