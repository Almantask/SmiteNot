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
3. Commit the `.meta` files and the `ProjectSettings/` that Unity generated.
4. Open `Assets/_Game/Scenes/Bootstrap.unity` and press Play: the cube moves with the simulation tick.

## Commands

| What | Command |
|---|---|
| Fast simulation tests | `dotnet test sim/GodGame.Sim.sln --filter Category=Fast` |
| All simulation tests | `dotnet test sim/GodGame.Sim.sln` |
| Headless placeholder run, per-second hashes | `dotnet run --project sim/tools/GodGame.Sim.Cli -- hash --seconds 180 --out hashes.txt` |
| Benchmarks | `dotnet run -c Release --project sim/bench/GodGame.Sim.Bench` |
| Format check | `dotnet csharpier check sim` |
| Blender export | `blender -b <file.blend> --python tools/blender/export.py -- --out <dir>` |
| Blender validation | `blender -b <file.blend> --python tools/blender/validate.py -- --report <file.json>` |

## Branches, pull requests and CI

- Work on `m<N>/<short-name>` branches, one checklist item each; conventional commits (`feat(sim): …`); squash merge.
- `.github/workflows/sim.yml` runs on every push (and by hand from the Actions tab): it checks formatting, builds and tests the simulation on Windows and Linux, proves the determinism analyzers reject banned code, then compares the two runs' per-second state hashes. Any difference is a determinism bug.
- **Branch protection on `main`** (set it once in GitHub → Settings → Rules → Rulesets, or Branches): require a pull request before merging, require the status checks `test (ubuntu-latest)`, `test (windows-latest)` and `cross-os` (from the `sim` workflow), and block force-pushes and deletion.

## Licence

Apache 2.0, see [LICENSE](LICENSE).
