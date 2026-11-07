# Design docs

These files are copies of the design docs in the claude.ai project "Strategy God Game". Design conversations happen there; this folder is the build's read-only snapshot (implementation plan, section 12). Re-copy a doc whenever a decision lands in the project, and at the latest when a milestone starts.

`docs/design/game-brief.md` is the source of truth: it decides *what* the game is and wins any conflict, including with `docs/implementation-plan.md`.

Paths inside the copies keep the project's form: read `claude/<name>.md` as `docs/design/<name>.md`, and `claude/implementation-plan.md` as `docs/implementation-plan.md`.

## Files

| File | What it holds | In the repo |
|---|---|---|
| `game-brief.md` | Pillars, modes, dated decisions, open questions (source of truth) | Copied 2026-11-01 |
| `power-palette.md` | Powers, terrain pieces, counter web, combos | Copied 2026-11-01 |
| `tech-notes.md` | How to build it: scale, determinism, fields, timeline, rendering | Copied 2026-11-01 |
| `unit-spec.md` | Unit look, LODs, animation and death gags | Copied 2026-11-01 |
| `unit-brainstorm.md` | Unit and specialist ideas | Copied 2026-11-01 |
| `idea-bank.md` | Numbered ideas (IB n) | Copied 2026-11-01 |
| `terrain-weather-brainstorm.md` | Terrain and weather ideas, Top 25 (TW #n) | Copied 2026-11-01 |
| `idea-reviews.md` | Saved `/review-idea` verdicts | **Missing** |
| `gamer-review.md` | The gamer reviews | **Missing** |
| `content-roadmap.md` | What content comes first, by wave | Copied 2026-10-11 |
| `spec-s1-water.md` | Spec pass S1: water, wetness, mud, Rain and the first terrain pieces, in numbers (draft rules in `s1-data/`) | Written here 2026-11-02 (not yet in the claude.ai project); decisions settled 2026-11-03 |
| `spec-s2-armies.md` | Spec pass S2: armies, load, launches, herds, Lightning, Rot, Wind shift and the first six specialists, in numbers (draft rules in `s2-data/`) | Written here 2026-11-07 (not yet in the claude.ai project); decisions settled 2026-11-07 |

Later spec passes add `launch-powers.md` (S3), `attacker-behaviour.md` (S4) and `economy.md` (S5); see the implementation plan, section 9.

When a file arrives, change its row to "Copied YYYY-MM-DD". Pasting from the project can escape Markdown (`\#`, `\*`), leave `&#x20;` lines and put blank lines between table rows; undo those so the copy matches the original.
