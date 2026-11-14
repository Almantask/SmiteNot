---
name: sound-gen
description: Generate game audio locally with Stable Audio 3 through the thunder-fx MCP server (`smitenot-local_thunder-fx`) — one-shot sound effects, looping ambience beds, short instrumental music and stingers. Use when the game needs a new or revised sound, such as a cause-event sound (splash, creak, crack, wooden clack, stampede rumble, fire whoosh), a weather or battlefield ambience loop, or a music bed, or when asked to make SFX, sounds or music.
---

# Local Audio Generation (Thunder FX)

Everything runs on this machine: Thunder FX (`C:\Users\ITWORK\source\repos\thunder-fx`) serves Stable Audio 3 Medium via the `smitenot-local_thunder-fx` (or `thunder-fx`) MCP server, always at its Balanced preset in fp16. The shared helper script [.claude/skills/sound-gen/audio.py](../../../.claude/skills/sound-gen/audio.py) checks takes and saves the chosen one via `run_command`:

```powershell
uv run --no-project --with numpy python .claude/skills/sound-gen/audio.py <command> --help
```

## Inputs

Extract what audio is needed from the user's request (e.g., `splash for a soldier landing in water` or `rain ambience loop`).

## Scope

- **Sound effects** (`generate_sound_effect`): one-shots for cause events, 0.5–3 s, peak −1 dBFS. The plan (section 7.10) drives audio from cause events: creaks and cracks by strain stage, splashes, stampede rumble, wooden clacks on impact.
- **Ambience** (`generate_ambience`): looping beds (rain, wind, river, battlefield murmur), 20–60 s, −20 LUFS, `seamless_loop` on.
- **Music** (`generate_instrumental`): beds and stingers (lock-in, sunset, round won), −18 LUFS; pass `bpm` and `instruments`. Always instrumental.
- **Not voices.** Reaction barks need speech, which this model can't do; say so and stop.

## Steps

1. **Server.** Look in `<mcp_servers>` for `smitenot-local_thunder-fx` (or `thunder-fx`). If absent, stop and inform the user that the `thunder-fx` MCP server (`.agents/plugins/smitenot-local/mcp_config.json` with `HF_HUB_OFFLINE=1`) is not connected in this Antigravity session. Read its schemas under `<appDataDir>/mcp/<serverName>/` and call via `call_mcp_tool`:
   - Call `engine_status`.
   - Before the first generate, if `smitenot-local_comfy` (`comfy`) is running, call `free_memory` on it: the 3090 can't hold both models.
2. **Name.** `S_<category>_<name>_<variant>` in lowercase: `S_water_splash_a`, `S_strain_creak_a`, `S_amb_rain_a`, `S_music_lockin_a`. Categories follow what plays the sound: `impact`, `water`, `strain`, `fire`, `herd`, `siege`, `weather`, `ui`, `amb`, `music`.
3. **Prompt.** Call `search_prompt_catalog` (libraries `fx`, `ambience`, `music`) and start from the closest entry's wording: the catalog is stored in the form the model was trained on. Describe the source and its material, the action, the mic position and the space, then the decay: `"wooden toy shield clattering onto packed earth, short hollow clack, close mic, outdoor, fast decay"`. Under 45 words. Keep the playful, toy-box tone of the brief: cartoon plops, clacks, boings and puffs, never pain, screams, blood or bone (skip the catalog's gore and horror categories). Leave out the `TrackType:` and `Length:` tags; the server adds them.
4. **Takes.** Make three takes with different seeds, all with `category="SmiteNot"` so they're easy to find in the Thunder FX library. One call per take, one at a time; each blocks until the clip is done, usually in seconds. The first call after the server starts (or after 10 idle minutes) also loads the model from the `E:` hard drive, which takes several minutes while progress streams: wait for it and don't resubmit. Keep each take's `path`, `seed` and `prompt_sent`.
5. **Check.** Run via `run_command`:
   ```powershell
   uv run --no-project --with numpy python .claude/skills/sound-gen/audio.py check <path> --mode sfx|ambience|music [--loop]
   ```
   Add `--loop` for ambience and looping music. Reject a take on `pass: false` (clipping, DC offset, silence before a one-shot's hit, a click at the loop point, a near-silent take) and make another with a new seed, except for silence before the hit: the model often leaves 50–100 ms of air before a one-shot, so keep such a take and save it with `--trim` (step 7). A cut-off warning is worth a listen, not an automatic reject. You can't judge audio by ear: the user decides.
6. **Listen.** Present the passing take file paths to the user as clickable `file:///` links captioned with each take's seed and prompt, and ask which one to keep. If none fits, change the prompt (step 3) rather than rolling seeds forever.
7. **Save** only the chosen take:
   ```powershell
   uv run --no-project --with numpy python .claude/skills/sound-gen/audio.py save <path> --name <name> --mode <mode> [--loop] --description "<the description you passed>" --prompt-sent "<prompt_sent>" --seed <seed> --use "<what plays it>" [--trim]
   ```
   Add `--trim` for one-shots and stingers (it cuts the silence before the hit and after the tail, with short fades, and rechecks; never on loops). It writes `art/audio/<category>/<name>.wav` (Git LFS) and `<name>.json`, which holds the prompt, seed, model, licence and checks needed to regenerate it. For round-robin variants of one sound (footfalls, clacks), save two or three takes as `_a`, `_b`, `_c`; Unity's random pitch covers the rest.
8. **Unity**, only when something plays the sound now: copy the WAV to `unity/Assets/_Game/Audio/Clips/<category>/` and set its import settings through `unityMCP` (`call_mcp_tool`: one-shots: Decompress On Load, Vorbis, Force To Mono for positional sounds; ambience and music: Streaming, Vorbis). Unity compresses on import, so keep WAV in the repo. Never hand-edit `.meta` files.

## Rules

- **Licence.** Stable Audio 3 weights are under the Stability AI Community License: commercial use is allowed while annual revenue stays under USD 1M; past that the game needs Stability's enterprise licence. The sidecar records it; switching models is the user's decision.
- The MCP library is read-only apart from `export_clip`: rating, tagging and deleting takes stay in the Thunder FX app. Takes you didn't keep stay in its library (`%LOCALAPPDATA%\thunder-fx\library\<sfx|ambience|music>\SmiteNot\`); never delete them from disk.
- Sounds are presentation only. Audio code in `GodGame.Audio` reacts to cause events from the simulation; nothing in the simulation ever reads audio.

## Common problems

| Symptom | Cause and fix |
|---|---|
| `401 Client Error` or `Cannot access gated repo` on the first generate | The server tried to reach Hugging Face for the gated model. The registration sets `HF_HUB_OFFLINE=1` so it reads the cached weights in `E:\huggingface\hub`; if it's missing, add it back to `.agents/plugins/smitenot-local/mcp_config.json`. Never handle a Hugging Face token. |
| CUDA out of memory | The Thunder FX desktop app or ComfyUI holds the GPU. Free ComfyUI's memory, or ask the user to unload the model in the app. |
| `Another Thunder FX MCP server is already running` | A second session or client has the server. Generate from that session, or close it. |
| A one-shot starts late | Silence before the hit, common with this model. Save with `--trim`. |
| Music with vocals or speech | Rare; the trained `VocalType: Instrumental` tag usually stops it. Drop words like `"chant"` or `"choir"` and take another seed. |
