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
- [ ] 7. Blender export and validation scripts, test soldier imported
- [ ] 8. Exit criteria verified

### Exit criteria
- [x] dotnet test green on Windows and Linux; hashes match — CI run 1 (2026-10-11): both test jobs and cross-os green
- [ ] Unity opens cleanly; the cube moves with the simulation — needs a machine with Unity 6.3
- [x] A float in sim/src fails the build and the hook flags it — GG0001 fails the build (CI runs tools/ci/check_sim_analyzers.py); sim_lint.py flags it on write
- [ ] /context, /agents and /milestone work — needs a fresh Claude Code session on this branch
- [ ] Unity MCP reads the console; Blender MCP lists the scene — needs the local MCP installs
- [ ] Test soldier exports with a clean validation report

### Decisions needed
- (none for M0)

### Notes
- The banned-API analyzer alone doesn't reject `float x = 1f;`; the project analyzer GG0001 (sim/analyzers) does. Recorded in the plan, Appendix A, "As built in M0".
- The three /review-idea reviewer agents were drafted in the repo because the claude.ai originals weren't available; replace them when copying the design docs.

## Done
- (nothing yet)
