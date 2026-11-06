---
name: golden-replay
description: Verify and generate golden simulation replays, bump SimVersion when battle outcomes intentionally change, and record changelog entries. Use when invoked via /golden-replay or when verifying, updating, or adding files in sim/tests/golden/ or PinnedHashes.cs.
---

# Golden Replay Management

The **only** allowed workflow for modifying `sim/tests/golden/` or the pinned constants in `sim/tests/GodGame.Sim.Tests/PinnedHashes.cs`. Never hand-edit `.godreplay` files.

## Modes & Inputs

Parse the requested mode from the prompt (`verify` by default when none is given):
- `verify`
- `update <reason>`
- `add <name> [--map <dir>] [--plan <file>] [--seed <n>] [--seconds <n>]`

(`godsim` below refers to `dotnet run --project sim/tools/GodGame.Sim.Cli --`.)

## `verify`

1. Run the golden and fast test suites via `run_command`:
   ```powershell
   dotnet test sim/GodGame.Sim.sln --filter "Category=Golden|Category=Fast"
   ```
2. For each failing golden scenario, inspect the first diverging second and cause events:
   ```powershell
   dotnet run --project sim/tools/GodGame.Sim.Cli -- compare --golden <scenario>
   ```
3. `PinnedHashes.cs` (trig tables, world layout, rules, map, plan, and deployed world) follows the same rule as goldens. Report any failing pinned hash and, for battle divergences, the first second where `hash` output differs from `origin/main`.

## `update <reason>`

1. Run `verify` first. If nothing diverges, stop and report all goldens match.
2. Inspect `git diff origin/main` (covering both unpushed commits and uncommitted changes) and explain **every** divergence from intentional code or rules changes. If any divergence is unexplained, **stop** and recommend `/desync-hunt`.
3. Present the divergence explanation and ask for explicit confirmation of `<reason>` before regenerating.
4. Increment the integer constant in `sim/src/GodGame.Sim/SimVersion.cs` with `edit_file`.
5. Regenerate all goldens via `run_command`, then update any changed constants in `sim/tests/GodGame.Sim.Tests/PinnedHashes.cs` to the values reported by the test suite:
   ```powershell
   dotnet run --project sim/tools/GodGame.Sim.Cli -- golden regenerate --all
   ```
6. Append an entry to `sim/CHANGELOG-sim.md` with: new `SimVersion`, date, reason, scenarios affected, and notable statistic changes (casualties, swept, launched).
7. Re-run `verify` and summarise the updated golden files.

## Header-Only Changes

When a rules-data or format change updates a golden header (`rulesetHash`, `mapHash`) without changing battle outcomes:
1. Run `dotnet run --project sim/tools/GodGame.Sim.Cli -- hash` for each golden's battle and verify every per-second hash matches the existing golden.
2. If **all** per-second hashes match, run `golden regenerate --all`, confirm `git diff` touches only header metadata, append `"header only: <reason>; outcomes unchanged"` to `sim/CHANGELOG-sim.md` **without** bumping `SimVersion`, and note it in the commit message.
3. If **any** per-second hash differs, treat it as an outcome change and follow `update` above.

## `add <name>`

Record a new golden scenario (`<name>` uses `a-z`, `0-9`, `_`, and `-`):

```powershell
dotnet run --project sim/tools/GodGame.Sim.Cli -- golden add <name> [--map <dir>] [--plan <file>] [--seed <n>] [--seconds <n>]
```

Adding a new golden alters no existing outcome, so do not bump `SimVersion`; state in the commit message what invariant or decision it protects.
