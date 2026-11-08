"""Check and save game audio made with the thunder-fx MCP server (Stable Audio 3 Medium).

  check  <wav> --mode sfx|ambience|music [--loop]   objective checks on one take (JSON to stdout)
  save   <wav> --name S_<category>_<name> ...       copy the chosen take into art/audio/ with a sidecar

Run: uv run --no-project --with numpy python .claude/skills/sound-gen/audio.py <command> --help
"""

from __future__ import annotations

import argparse
import datetime
import json
import re
import shutil
import struct
import subprocess
import sys
import wave
from pathlib import Path

import numpy as np

REPO = Path(__file__).resolve().parents[3]
THUNDER_FX = Path(r"C:\Users\ITWORK\source\repos\thunder-fx")
NAME_PATTERN = re.compile(r"^S_([a-z]+)_[a-z0-9]+(?:_[a-z0-9]+)*$")
MODEL = {
    "name": "stabilityai/stable-audio-3-medium",
    "license": "Stability AI Community License (commercial use while annual revenue is under USD 1M)",
    "text_encoder": "T5Gemma (Gemma Terms of Use)",
    "preset": "balanced",
    "precision": "fp16",
}

# Thresholds. A one-shot fires on a cause event's tick, so silence before the hit reads as lag
# (an error); a music stinger may breathe a little (a warning). A loop with silence at its ends gaps.
LEAD_SILENCE_MS = {"sfx": 20.0, "ambience": None, "music": None}
LEAD_WARN_MS = {"sfx": None, "ambience": None, "music": 60.0}
LOOP_GAP_MS = 30.0
# --trim keeps this much before the onset and after the tail, with these fades.
TRIM_PRE_MS, TRIM_POST_MS, FADE_IN_MS, FADE_OUT_MS = 3, 40, 1, 30
SILENCE_DB = -50.0  # level below which a frame counts as silent
CLIP_LEVEL = 0.999
DC_LIMIT = 0.01


def read_wav(path: Path) -> tuple[np.ndarray, int]:
    """Samples as float64 in [-1, 1], shape (frames, channels), plus the sample rate."""
    data = path.read_bytes()
    if data[:4] != b"RIFF" or data[8:12] != b"WAVE":
        sys.exit(f"{path} is not a RIFF/WAVE file")
    fmt = None
    body = None
    pos = 12
    while pos + 8 <= len(data):
        chunk_id, size = data[pos : pos + 4], struct.unpack("<I", data[pos + 4 : pos + 8])[0]
        start = pos + 8
        if chunk_id == b"fmt ":
            fmt = struct.unpack("<HHIIHH", data[start : start + 16])
            if fmt[0] == 0xFFFE:  # WAVE_FORMAT_EXTENSIBLE: the real tag is the subformat's first two bytes
                fmt = (struct.unpack("<H", data[start + 24 : start + 26])[0],) + fmt[1:]
        elif chunk_id == b"data":
            body = data[start : start + size]
        pos = start + size + (size & 1)
    if fmt is None or body is None:
        sys.exit(f"{path}: missing fmt or data chunk")
    tag, channels, rate, _, _, bits = fmt
    if tag == 3 and bits in (32, 64):
        samples = np.frombuffer(body, dtype="<f4" if bits == 32 else "<f8").astype(np.float64)
    elif tag == 1 and bits == 16:
        samples = np.frombuffer(body, dtype="<i2").astype(np.float64) / 32768.0
    elif tag == 1 and bits == 24:
        raw = np.frombuffer(body, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
        ints = raw[:, 0] | (raw[:, 1] << 8) | (raw[:, 2] << 16)
        samples = np.where(ints >= 1 << 23, ints - (1 << 24), ints).astype(np.float64) / float(1 << 23)
    elif tag == 1 and bits == 32:
        samples = np.frombuffer(body, dtype="<i4").astype(np.float64) / float(1 << 31)
    else:
        sys.exit(f"{path}: unsupported WAV format (tag {tag}, {bits} bit)")
    frames = len(samples) // channels
    return samples[: frames * channels].reshape(frames, channels), rate


def db(x: float) -> float:
    return round(20.0 * float(np.log10(max(x, 1e-10))), 2)


def write_wav(path: Path, samples: np.ndarray, rate: int) -> None:
    """16-bit PCM, as Thunder FX writes it; TPDF dither so quiet tails don't turn into grit."""
    rng = np.random.default_rng(0)
    dither = (rng.random(samples.shape) - rng.random(samples.shape)) / 32768.0
    ints = np.clip(np.round((samples + dither) * 32767.0), -32768, 32767).astype("<i2")
    with wave.open(str(path), "wb") as out:
        out.setnchannels(samples.shape[1])
        out.setsampwidth(2)
        out.setframerate(rate)
        out.writeframes(ints.tobytes())


def trim(samples: np.ndarray, rate: int) -> np.ndarray:
    """Cut silence before the onset and after the tail, keeping a few ms with short fades."""
    level = np.abs(samples).max(axis=1)
    loud = np.nonzero(level > 10 ** (SILENCE_DB / 20.0))[0]
    if not len(loud):
        return samples
    start = max(0, loud[0] - rate * TRIM_PRE_MS // 1000)
    end = min(len(samples), loud[-1] + 1 + rate * TRIM_POST_MS // 1000)
    out = samples[start:end].copy()
    fade_in = min(len(out), rate * FADE_IN_MS // 1000)
    fade_out = min(len(out), rate * FADE_OUT_MS // 1000)
    if fade_in:
        out[:fade_in] *= np.linspace(0.0, 1.0, fade_in)[:, None]
    if fade_out:
        out[-fade_out:] *= np.linspace(1.0, 0.0, fade_out)[:, None]
    return out


def analyse(path: Path, mode: str, loop: bool) -> dict:
    samples, rate = read_wav(path)
    return analyse_samples(samples, rate, mode, loop, str(path))


def analyse_samples(samples: np.ndarray, rate: int, mode: str, loop: bool, file: str) -> dict:
    mono = samples.mean(axis=1)
    frames = len(mono)
    peak = float(np.abs(samples).max()) if frames else 0.0
    rms = float(np.sqrt(np.mean(samples**2))) if frames else 0.0
    hop = max(1, rate // 1000)  # 1 ms frames for the silence scan
    env = np.array([np.abs(mono[i : i + hop]).max() for i in range(0, frames, hop)]) if frames else np.zeros(0)
    loud = np.nonzero(env > 10 ** (SILENCE_DB / 20.0))[0]
    lead_ms = float(loud[0]) if len(loud) else float(len(env))
    trail_ms = float(len(env) - 1 - loud[-1]) if len(loud) else float(len(env))
    tail_n = max(1, rate // 100)  # last 10 ms
    tail_db = db(float(np.sqrt(np.mean(mono[-tail_n:] ** 2)))) if frames else -200.0
    dc = float(np.abs(samples.mean(axis=0)).max()) if frames else 0.0
    clipped = int(np.count_nonzero(np.abs(samples) >= CLIP_LEVEL))
    report = {
        "file": file,
        "mode": mode,
        "loop": loop,
        "seconds": round(frames / rate, 3) if rate else 0.0,
        "sample_rate": rate,
        "channels": samples.shape[1],
        "peak_dbfs": db(peak),
        "rms_dbfs": db(rms),
        "lead_silence_ms": round(lead_ms, 1),
        "trail_silence_ms": round(trail_ms, 1),
        "tail_dbfs": tail_db,
        "dc_offset": round(dc, 5),
        "clipped_samples": clipped,
    }
    errors, warnings = [], []
    if peak < 10 ** (-40 / 20):
        errors.append("near-silent take (peak under -40 dBFS)")
    if clipped:
        errors.append(f"{clipped} clipped samples")
    if dc > DC_LIMIT:
        errors.append(f"DC offset {dc:.3f}")
    limit = LEAD_SILENCE_MS[mode]
    if limit is not None and lead_ms > limit:
        errors.append(f"{lead_ms:.0f} ms of silence before the sound starts (limit {limit:.0f} ms)")
    soft = LEAD_WARN_MS[mode]
    if soft is not None and not loop and lead_ms > soft:
        warnings.append(f"{lead_ms:.0f} ms of silence before the music starts: late for a stinger")
    if loop and frames > 2:
        # A loop clicks when the jump from the last frame back to the first is far larger
        # than the clip's normal frame-to-frame movement.
        jump = float(np.abs(samples[0] - samples[-1]).max())
        typical = float(np.percentile(np.abs(np.diff(samples, axis=0)), 99.5))
        report["loop_seam_jump"] = round(jump, 5)
        report["loop_seam_typical"] = round(typical, 5)
        if jump > max(3.0 * typical, 0.02):
            errors.append("click at the loop point (seam jump far above the clip's normal movement)")
        if lead_ms + trail_ms > LOOP_GAP_MS:
            errors.append(f"{lead_ms + trail_ms:.0f} ms of silence across the loop point: the loop gaps")
    elif mode != "ambience" and tail_db > peak_db_threshold(peak):
        warnings.append("tail still loud in the last 10 ms: the sound may be cut off")
    report["errors"] = errors
    report["warnings"] = warnings
    report["pass"] = not errors
    return report


def peak_db_threshold(peak: float) -> float:
    """A tail within 30 dB of the peak counts as cut off."""
    return db(peak) - 30.0


def cmd_check(args: argparse.Namespace) -> None:
    print(json.dumps(analyse(Path(args.wav), args.mode, args.loop), indent=2))


def thunder_fx_commit() -> str | None:
    try:
        out = subprocess.run(
            ["git", "-C", str(THUNDER_FX), "rev-parse", "--short", "HEAD"],
            capture_output=True,
            text=True,
            timeout=20,
            check=True,
        )
        return out.stdout.strip() or None
    except (OSError, subprocess.SubprocessError):
        return None


def cmd_save(args: argparse.Namespace) -> None:
    match = NAME_PATTERN.match(args.name)
    if not match:
        sys.exit(f"bad name {args.name!r}: expected S_<category>_<name> in lowercase, e.g. S_water_splash_a")
    category = match.group(1)
    src = Path(args.wav)
    samples, rate = read_wav(src)
    if args.trim:
        if args.loop:
            sys.exit("--trim would break a loop; pick another take instead")
        samples = trim(samples, rate)
    report = analyse_samples(samples, rate, args.mode, args.loop, str(src))
    if not report["pass"] and not args.force:
        sys.exit(f"take fails its checks ({'; '.join(report['errors'])}); pass --force to save it anyway")
    dest = REPO / "art" / "audio" / category
    dest.mkdir(parents=True, exist_ok=True)
    wav = dest / f"{args.name}.wav"
    if wav.exists() and not args.overwrite:
        sys.exit(f"{wav} exists; pass --overwrite to replace it")
    if args.trim:
        write_wav(wav, samples, rate)
    else:
        shutil.copyfile(src, wav)
    sidecar = {
        "name": args.name,
        "mode": args.mode,
        "loop": args.loop,
        "use": args.use,
        "description": args.description,
        "prompt_sent": args.prompt_sent,
        "seed": args.seed,
        "seconds": report["seconds"],
        "trimmed": args.trim,
        "generator": "thunder-fx MCP server",
        "generator_commit": thunder_fx_commit(),
        "model": MODEL["name"],
        "model_license": MODEL["license"],
        "text_encoder": MODEL["text_encoder"],
        "preset": MODEL["preset"],
        "precision": MODEL["precision"],
        "source_clip": str(src),
        "checks": {k: v for k, v in report.items() if k not in ("file", "mode", "loop")},
        "created": datetime.date.today().isoformat(),
    }
    side = dest / f"{args.name}.json"
    side.write_text(json.dumps(sidecar, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"saved": wav.relative_to(REPO).as_posix(), "sidecar": side.relative_to(REPO).as_posix()}))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    modes = ["sfx", "ambience", "music"]

    p = sub.add_parser("check", help="objective checks on one take")
    p.add_argument("wav")
    p.add_argument("--mode", choices=modes, required=True)
    p.add_argument("--loop", action="store_true", help="the take must loop without a click")
    p.set_defaults(func=cmd_check)

    p = sub.add_parser("save", help="copy the chosen take into art/audio/<category>/ with a provenance sidecar")
    p.add_argument("wav", help="the take's path, as the generate tool returned it")
    p.add_argument("--name", required=True, help="S_<category>_<name>, e.g. S_water_splash_a")
    p.add_argument("--mode", choices=modes, required=True)
    p.add_argument("--loop", action="store_true")
    p.add_argument("--description", required=True, help="the description passed to the generate tool")
    p.add_argument("--prompt-sent", required=True, help="the prompt_sent field the generate tool returned")
    p.add_argument("--seed", type=int, required=True, help="the seed the generate tool returned")
    p.add_argument("--use", required=True, help="what plays it, e.g. 'cause event: splash (soldier into water)'")
    p.add_argument("--trim", action="store_true", help="cut silence before the hit and after the tail (one-shots and stingers)")
    p.add_argument("--overwrite", action="store_true")
    p.add_argument("--force", action="store_true", help="save even though a check fails")
    p.set_defaults(func=cmd_save)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
