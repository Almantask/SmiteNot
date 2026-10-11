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
- [ ] 7. Blender export and validation scripts, test soldier imported — scripts done; U_testsoldier_a exported and validated (Blender 5.2.2 LTS, bpy module); import into Unity happens on first open (ArtImportPostprocessor)
- [ ] 8. Exit criteria verified — three of six verified in the cloud session; the rest need a machine with Unity and Blender

### Exit criteria
- [x] dotnet test green on Windows and Linux; hashes match — CI run 1 (2026-10-11): both test jobs and cross-os green
- [ ] Unity opens cleanly; the cube moves with the simulation — needs a machine with Unity 6.3
- [x] A float in sim/src fails the build and the hook flags it — GG0001 fails the build (CI runs tools/ci/check_sim_analyzers.py); sim_lint.py flags it on write
- [ ] /context, /agents and /milestone work — a restarted session on this branch loaded CLAUDE.md, the art rule on touching tools/blender, all 13 agents and the SessionStart hook; run /milestone and /context once locally
- [ ] Unity MCP reads the console; Blender MCP lists the scene — the Blender MCP server starts from .mcp.json but needs Blender running with its add-on; MCP for Unity is a local install
- [x] Test soldier exports with a clean validation report — 0 errors, 2 expected warnings (capsule LODs under the hero and mid targets); a broken copy fails with 6 errors

### Decisions needed
- (none for M0)

### Notes
- The banned-API analyzer alone doesn't reject `float x = 1f;`; the project analyzer GG0001 (sim/analyzers) does. Recorded in the plan, Appendix A, "As built in M0".
- The three /review-idea reviewer agents were drafted in the repo because the claude.ai originals weren't available; replace them when copying the design docs.
- Package versions in unity/Packages/manifest.json were picked without a Unity editor to check them (URP and VFX Graph 17.3.0 match 6.3; the others are releases known to support Unity 6). If one doesn't resolve on your 6000.3 patch, take the version Package Manager offers and commit it.
- To finish M0 locally: copy the design docs; open unity/ in Unity 6.3 and run GodGame → Setup → Apply Project Settings, check the cube moves and the Edit Mode tests pass, and commit the generated files; install MCP for Unity and the Blender MCP add-on and check both; set branch protection on main.

## Done
- (nothing yet)
