# SmiteNot

A god-powers strategy auto-battler. A small army defended by a god faces a much bigger one, and you can play either side. The god never touches a soldier: it works only on the world (rain, rivers, wind, fire, cold, terrain, herds), at least 100 m from both armies, so the attacker's own mass becomes its weakness. Plan, preview the deterministic future, then commit and watch. Up to 10,000 toy-like soldiers.

- What the game is: [`docs/design/game-brief.md`](docs/design/) (the source of truth)
- How it is built: [`docs/implementation-plan.md`](docs/implementation-plan.md)
- Where the build is now: [`docs/milestones/STATUS.md`](docs/milestones/STATUS.md)

## Layout

| Folder | What lives there |
|---|---|
| `sim/` | The engine-free, deterministic C# simulation (.NET); `sim/src` is also a Unity package |
| `unity/` | The Unity 6.3 LTS (URP) project: presentation, planning UI, editor tools |
| `data/` | Rules data, schemas, maps and levels |
| `art/` | Blender sources (Git LFS) and art rules |
| `tools/` | Blender export and validation scripts, CI helpers, dev setup |
| `docs/` | Design docs, implementation plan, ADRs, milestone status |
| `.claude/` | Claude Code instructions, rules, agents, skills and hooks |

## Prerequisites

- Git and Git LFS
- .NET 10 SDK (version pinned in `sim/global.json`)
- Unity Hub with Unity 6.3 LTS (`6000.3.x`), Windows Build Support (IL2CPP) module
- Python 3.10 or later, and [`uv`](https://docs.astral.sh/uv/) (for the Blender MCP server)
- Blender (current LTS)
- Claude Code; GitHub CLI (`gh`)

## Setup

```sh
git clone https://github.com/Almantask/SmiteNot.git
cd SmiteNot
python tools/setup_dev.py   # git lfs install + pull, UnityYAMLMerge merge driver, dotnet tool restore
```

`setup_dev.py` finds `UnityYAMLMerge` under Unity Hub's default install folders. If yours is elsewhere, pass `--unity-yaml-merge <path>`. It writes this to the clone's local git config:

```ini
[merge "unityyamlmerge"]
    name = Unity SmartMerge (UnityYAMLMerge)
    driver = "<path>/UnityYAMLMerge" merge -h -p --force %O %B %A %A
    recursive = binary
```

`.gitattributes` routes scenes, prefabs and other Unity YAML through that driver.

### First Unity open

1. Unity Hub → Add → `unity/` → open with 6000.3 LTS. Unity resolves the packages, including the simulation (`com.godgame.sim` → `sim/src`).
2. Run **GodGame → Setup → Apply Project Settings** once. It applies the settings from the plan (section 3.7): Linear colour, URP pipeline asset, Force Text, Visible Meta Files, IL2CPP, DirectX 12 with a DirectX 11 fallback, Enter Play Mode without domain reload. It also creates the M0 bootstrap scene.
3. Commit the `.meta` files and the `ProjectSettings/` that Unity generated (including those Unity writes into `sim/src`, which it treats as a local package).
4. Open `Assets/_Game/Scenes/Bootstrap.unity` and press Play: the cube moves with the simulation tick.
5. Window → General → Test Runner → EditMode → Run All: the `SimHostTests` pass.

If a package version in `Packages/manifest.json` doesn't resolve on your 6000.3 patch, pick the version Package Manager offers and commit the change.

### Claude Code

- Hooks run `python` (Windows: install Python from python.org with "Add to PATH", or change `python` to `py` in `.claude/settings.json`). They need `dotnet` and `git` on the PATH.
- **Blender MCP** is shared in `.mcp.json` (`uvx blender-mcp`, safe mode, telemetry off). Install its Blender add-on (see the [blender-mcp README](https://github.com/ahujasid/blender-mcp)) and start its server from Blender's sidebar; the socket has no authentication, so keep it on localhost. Without Blender running, the MCP server starts but its tools report that they can't connect.
- **Unity MCP** is per machine because its configuration holds local paths: install [MCP for Unity](https://github.com/CoplayDev/unity-mcp) through Package Manager (git URL from its README), then use its "Configure detected clients" menu for Claude Code. If it registers a server name other than `UnityMCP`, update the `mcp__UnityMCP` permission in `.claude/settings.json`.
- Check the setup: `/context` lists `CLAUDE.md` and the rules once you open a file they cover, `/agents` lists the agents, `/milestone` prints the current milestone.

## Commands

| What | Command |
|---|---|
| Fast simulation tests | `dotnet test sim/GodGame.Sim.sln --filter Category=Fast` |
| All simulation tests | `dotnet test sim/GodGame.Sim.sln` |
| Headless placeholder run, per-second hashes | `dotnet run --project sim/tools/GodGame.Sim.Cli -- hash --seconds 180 --out hashes.txt` |
| Benchmarks | `dotnet run -c Release --project sim/bench/GodGame.Sim.Bench` |
| Format check | `dotnet csharpier check sim` |
| Prove the determinism analyzers reject banned code | `python tools/ci/check_sim_analyzers.py` |
| Blender export | `blender -b <file.blend> --python tools/blender/export.py -- --out <dir>` |
| Blender validation | `blender -b <file.blend> --python tools/blender/validate.py -- --report <file.json>` |

## Branches, pull requests and CI

- Work on `m<N>/<short-name>` branches, one checklist item each; conventional commits (`feat(sim): …`); squash merge.
- `.github/workflows/sim.yml` runs on every push (and by hand from the Actions tab): it checks formatting, builds and tests the simulation on Windows and Linux, proves the determinism analyzers reject banned code, then compares the two runs' per-second state hashes. Any difference is a determinism bug.
- **Branch protection on `main`** (set it once in GitHub → Settings → Rules → Rulesets, or Branches): require a pull request before merging, require the status checks `test (ubuntu-latest)`, `test (windows-latest)` and `cross-os` (from the `sim` workflow), and block force-pushes and deletion.

## Licence

Apache 2.0, see [LICENSE](LICENSE).
