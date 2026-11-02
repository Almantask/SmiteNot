---
name: golden-replay
description: Verify golden replays; when battle outcomes changed on purpose, bump SimVersion and regenerate golden files with a changelog entry. The only allowed way to change sim/tests/golden/ (and, until M1, the pinned hashes in GodGame.Sim.Tests/PinnedHashes.cs).
disable-model-invocation: true
argument-hint: "verify | update <reason> | add <name> [godsim battle options]"
allowed-tools: Bash(dotnet test *), Bash(dotnet run *), Bash(git diff *)
---

Mode and reason: $ARGUMENTS

## verify
Run `dotnet test sim/GodGame.Sim.sln --filter Category=Golden`. For each failing scenario, run
`dotnet run --project sim/tools/GodGame.Sim.Cli -- compare --golden <scenario>` and report the first diverging second and the first differing cause events.

Also run the Fast tests: the pinned hashes in `sim/tests/GodGame.Sim.Tests/PinnedHashes.cs` (trig tables, world layout, the repo's rules, map, plan and deployed world) follow the same rule as goldens. Report any that fail, and for a divergence the first second at which `godsim hash` output differs from origin/main (`godsim compare <a> <b>`).

## update
1. Run verify. If nothing diverges, stop.
2. Explain each divergence from the changes not yet pushed (`git diff origin/main`, which covers unpushed commits and uncommitted work). If any divergence is unexplained, stop and suggest /desync-hunt.
3. Show the explanation and ask the user to confirm the reason.
4. Increment the constant in sim/src/GodGame.Sim/SimVersion.cs.
5. Run `dotnet run --project sim/tools/GodGame.Sim.Cli -- golden regenerate --all`, then update any constants in PinnedHashes.cs to the values the failing tests report.
6. Append to sim/CHANGELOG-sim.md: new version, date, reason, scenarios affected, notable statistic changes (casualties, swept, launched).
7. Run verify again and summarise the changed golden files.

## header-only changes
When rules data or a format changes a golden's header (rulesetHash, mapHash) but no outcome: run `godsim hash` for each golden's battle with the current build and check every per-second hash equals the golden's. If they all match, run `golden regenerate`, check the diff touches only header lines, add a CHANGELOG-sim.md line without bumping SimVersion ("header only: <reason>; outcomes unchanged"), and say so in the commit. If any hash differs, it is an outcome change: use update.

## add
Record a new golden scenario: `dotnet run --project sim/tools/GodGame.Sim.Cli -- golden add <name> [--map <dir>] [--plan <file>] [--seed <n>] [--seconds <n>]`. Names use a-z, 0-9, '_' and '-'. Adding a golden changes no outcome, so it needs no SimVersion bump; say in the commit what it protects.
