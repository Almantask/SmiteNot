---
name: feedback-analyzer-script-in-place
description: tools/ci/check_sim_analyzers.py writes BannedApiProbe.cs into sim/src and builds in place; run it from a scratch copy whenever another build or test run is active
metadata:
  type: feedback
---

Run `python <scratch>/tools/ci/check_sim_analyzers.py` from a scratch copy of the repo (sim, data and tools/ci, without bin/obj). Never run the repo's own copy while the user has a build or test run going.

**Why:** the script resolves ROOT from its own path, writes sim/src/GodGame.Sim/BannedApiProbe.cs (a banned-construct probe) and runs `dotnet build` there. A concurrent `dotnet test` in the repo could compile the probe and fail, or lock files. On 2026-11-14 the user had a full test run going and asked for all probes to use a scratch copy. I nearly chained the in-repo script after a test run and had to kill it.

**How to apply:** with a tar copy into the scratchpad, `dotnet test --filter` and the analyzer script both run safely in the scratch tree.
