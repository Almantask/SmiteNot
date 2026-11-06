---
name: review-idea
description: Review game ideas with three parallel subagents (game designer, veteran player, casual gamer) and merge their verdicts. Use when invoked via /review-idea or when asked to evaluate game ideas from multiple player and design perspectives.
---

# Review Idea (Three-Perspective Panel)

Three independent reviewers judge the same idea(s) in parallel—systems fit & buildability (`game-designer`), depth, exploits & fairness (`veteran-player`), and newcomer clarity & appeal (`casual-gamer`)—before merging into one recommendation.

## 1. Pin Down the Idea(s)

- **Source:** prompt arguments, latest message, or idea last discussed. Shorthands: `IB 14` = idea 14 in `docs/design/idea-bank.md`; `TW #3` = Top 25 entry 3 in `docs/design/terrain-weather-brainstorm.md`; bare title = search `docs/design/` via `run_command` (`git grep -i "<title>" -- docs/design/`).
- **Batching:** up to **5 ideas** per run (each reviewer evaluates all of them in a single pass—never 3 subagents per idea). If given >5, review the first 5 and offer the remainder next.
- **Restate:** write 2–4 plain sentences per idea so all three judge the exact same mechanic. Note any ambiguity assumption in one line; ask only when no idea was provided at all.

## 2. Build Context Once

Read `docs/design/game-brief.md` with `read_file` (plus `power-palette.md` for powers/terrain/counters, `content-roadmap.md` for wave placement, and `idea-reviews.md` / `gamer-review.md` if present). Draft two self-contained prompt inputs:
- **Design packet (<= 400 words):** core pillars, relevant dated decisions & open questions, overlapping powers/pieces/bank ideas, and any prior verdicts.
- **Store-page pitch (<= 120 words, zero internal jargon):** how the Steam page would pitch SmiteNot right now based on `game-brief.md`.

## 3. Run the Three Subagents in Parallel

1. Check `<subagents>` for `game-designer`, `veteran-player`, and `casual-gamer`. Register any missing reviewer with `define_subagent` (`EnableWriteTools: false`) using the prompt bodies in [game-designer.md](./references/game-designer.md), [veteran-player.md](./references/veteran-player.md), and [casual-gamer.md](./references/casual-gamer.md).
2. Launch all three in **one turn** with `invoke_subagent` (`Model: "inherit"`) so they run concurrently and independently:

   ```text
   TypeName: "game-designer"  | Prompt: task + angle + restated idea(s) + Design packet
   TypeName: "veteran-player" | Prompt: task + angle + restated idea(s) + Design packet
   TypeName: "casual-gamer"   | Prompt: task + angle + restated idea(s) + Store-page pitch ONLY (instruct not to read docs/design/)
   ```

3. Save each returned `conversationId`, then **end the turn** (call no more tools) and wait for Antigravity's reactive wakeup as each subagent finishes.

## 4. Merge Verdicts

Do not paste raw subagent outputs (unless later asked, e.g. `"show me the veteran's review"`). Keep to ~1.5 mobile screens per idea:

1. **The call** (one sentence): `Build` | `Build with changes` | `Prototype first` | `Park` | `Cut` (a confused casual gamer signals a readability fix rather than `Cut`, unless instant legibility is the whole hook).
2. **Verdicts** (2-column table: `Reviewer | Verdict and take`).
3. **They agree:** 1–3 bullets.
4. **They split:** 1–2 bullets with *why* (often the sharpest insight).
5. **Explains itself?** Compare `casual-gamer`'s *"What I think it does"* against the actual mechanic; flag what misled them.
6. **Better version:** 2–4 sentences combining the best fixes.
7. **Fix early:** 1–3 concrete risks.
8. **The decision for you**, in **bold**, last.

*(For multiple ideas: start with a ranked `Idea | Call and average score` table, then a compact block per idea, ending with one bold question.)*

## 5. Save Summary

Append a dated entry to `docs/design/idea-reviews.md` with `edit_file` or `write_file` (creating a 1-line `# Idea reviews` header if absent): record restated idea, three verdicts/scores, merged call, better version, risks, and open question (not raw transcripts). Mention the save in one line and remind to mirror it to `claude.ai`. **Never** edit `game-brief.md`, `power-palette.md`, or `idea-bank.md` from a review—settled calls go through `/game-design`.

## Options

- **Debate (`"let them argue"`):** after merging, call `send_message` (`Recipient: <conversationId>`) to send each reviewer the other two reviews and request a brief rebuttal; end the turn, wait for replies, and merge rebuttals in <= 8 lines.
- **Single perspective:** invoke only the requested reviewer.
- **Compare A vs B:** restate both as separate entries and declare which wins in the merge.
