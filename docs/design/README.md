# Design docs

These files are copies of the design docs in the claude.ai project "Strategy God Game". Design conversations happen there; this folder is the build's read-only snapshot (implementation plan, section 12). Re-copy a doc whenever a decision lands in the project, and at the latest when a milestone starts.

`docs/design/game-brief.md` is the source of truth: it decides *what* the game is and wins any conflict, including with `docs/implementation-plan.md`.

Paths inside the copies keep the project's form: read `claude/<name>.md` as `docs/design/<name>.md`, and `claude/implementation-plan.md` as `docs/implementation-plan.md`.

## Files

| File | What it holds | In the repo |
|---|---|---|
| `game-brief.md` | Pillars, modes, dated decisions, open questions (source of truth) | **Missing: copy it from the project** |
| `power-palette.md` | Powers, terrain pieces, counter web, combos | **Missing** |
| `tech-notes.md` | How to build it: scale, determinism, fields, timeline, rendering | **Missing** |
| `unit-spec.md` | Unit look, LODs, animation and death gags | **Missing** |
| `unit-brainstorm.md` | Unit and specialist ideas | **Missing** |
| `idea-bank.md` | Numbered ideas (IB n) | **Missing** |
| `terrain-weather-brainstorm.md` | Terrain and weather ideas, Top 25 (TW #n) | **Missing** |
| `idea-reviews.md` | Saved `/review-idea` verdicts | **Missing** |
| `gamer-review.md` | The gamer reviews | **Missing** |
| `content-roadmap.md` | What content comes first, by wave | Copied 2026-10-11 |

Later spec passes add `roster.md` (S2), `launch-powers.md` (S3), `attacker-behaviour.md` (S4) and `economy.md` (S5); see the implementation plan, section 9.

When a file arrives, change its row to "Copied YYYY-MM-DD".
