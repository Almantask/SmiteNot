# Milestone status

## Current: M0 — Foundations
Started: 2026-10-11

### Checklist
- [ ] 1. Repository basics (gitignore, gitattributes + LFS, editorconfig, branch protection)
- [ ] 2. Design docs, ADR 0001, this file
- [ ] 3. Simulation solution with analyzers and first tests
- [ ] 4. CI: sim.yml with cross-OS hash check
- [ ] 5. Unity 6.3 LTS project with the sim package and SimHost cube
- [ ] 6. Claude Code: CLAUDE.md, rules, agents, skills, hooks, MCP
- [ ] 7. Blender export and validation scripts, test soldier imported
- [ ] 8. Exit criteria verified

### Exit criteria
- [ ] dotnet test green on Windows and Linux; hashes match
- [ ] Unity opens cleanly; the cube moves with the simulation
- [ ] A float in sim/src fails the build and the hook flags it
- [ ] /context, /agents and /milestone work
- [ ] Unity MCP reads the console; Blender MCP lists the scene
- [ ] Test soldier exports with a clean validation report

### Decisions needed
- (none for M0)

## Done
- (nothing yet)
