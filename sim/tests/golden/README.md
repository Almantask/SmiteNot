# Golden replays

Per-second hashes and summary statistics per scenario. These files change only through the `/golden-replay` skill, which bumps `SimVersion` and logs the reason in `sim/CHANGELOG-sim.md`. A Claude Code hook denies hand edits here.

Each scenario is a `.godreplay` (replay format v0): the SimVersion, ruleset hash, map id and hash, seed, plan, length and the state hash after every simulated second. `godsim golden verify` and the `Golden` test category re-run every one with the current build (the CI runs it on Windows and Linux; Unity's Play Mode test and the IL2CPP player replay the same files), and `godsim compare --golden <name>` reports the first second that differs.

| Scenario | What it protects |
|---|---|
| `m1-ten-thousand-seed1` | 10,000 units on m1-flat wandering for 180 s (the M1 exit battle): the tick, the parallel runner, deployment and the state hash |
