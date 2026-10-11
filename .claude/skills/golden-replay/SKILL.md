---
name: golden-replay
description: Verify golden replays; when battle outcomes changed on purpose, bump SimVersion and regenerate golden files with a changelog entry. The only allowed way to change sim/tests/golden/ (and, until M1, the pinned hashes in GodGame.Sim.Tests/PinnedHashes.cs).
disable-model-invocation: true
argument-hint: "verify | update <reason>"
allowed-tools: Bash(dotnet test *), Bash(dotnet run *), Bash(git diff *)
---

Mode and reason: $ARGUMENTS

## verify
Run `dotnet test sim/GodGame.Sim.sln --filter Category=Golden`. For each failing scenario, run
`dotnet run --project sim/tools/GodGame.Sim.Cli -- compare --golden <scenario>` and report the first diverging second and the first differing cause events.

Until M1 builds the golden infrastructure (`compare` and `golden regenerate` don't exist yet), verify means: run the Fast tests and report any failing pinned-hash test in `sim/tests/GodGame.Sim.Tests/PinnedHashes.cs`, with the first tick at which `godsim hash` output differs from main.

## update
1. Run verify. If nothing diverges, stop.
2. Explain each divergence from the branch's changes (`git diff main...HEAD`). If any divergence is unexplained, stop and suggest /desync-hunt.
3. Show the explanation and ask the user to confirm the reason.
4. Increment the constant in sim/src/GodGame.Sim/SimVersion.cs.
5. Run `dotnet run --project sim/tools/GodGame.Sim.Cli -- golden regenerate --all` (before M1: update the constants in PinnedHashes.cs to the new values reported by the failing tests).
6. Append to sim/CHANGELOG-sim.md: new version, date, reason, scenarios affected, notable statistic changes (casualties, swept, launched).
7. Run verify again and summarise the changed golden files.
