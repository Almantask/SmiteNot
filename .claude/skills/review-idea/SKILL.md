---
name: "review-idea"
description: "Reviews game ideas with three parallel agents (game designer, veteran player, casual gamer) and merges their verdicts. Use for /review-idea or asks to review an idea from several perspectives."
---

# Review idea

Three independent reviewers judge the same idea in parallel, then you merge them into one call. The value is in where they differ: the designer judges fit and buildability, the veteran judges depth and fairness, the casual gamer judges whether a newcomer gets it and wants it.

## 1. Pin down the idea

- **Where it comes from:** the skill arguments; otherwise the user's latest message; otherwise the idea last discussed. References: "IB 14" is idea 14 in `docs/design/idea-bank.md`, "TW #3" is entry 3 of the Top 25 in `docs/design/terrain-weather-brainstorm.md`; for a bare title, search `docs/design/` with Grep.
- **Several ideas:** up to five per run, and every reviewer covers all of them in one pass (never three agents per idea). More than five: review the first five and offer the rest as the next batch.
- **Restate each idea** in two to four plain sentences, so all three review exactly the same thing. If an ambiguity would change the verdict, take the likeliest reading, note it in one line and carry on. Ask only when there is no idea at all, in one line.

## 2. Build the context once

Read `docs/design/game-brief.md`. Also read `docs/design/power-palette.md` when the idea is a power, terrain piece, counter or combo, and `docs/design/content-roadmap.md` to see where it would land. Check `docs/design/idea-reviews.md` (if it exists) and `docs/design/gamer-review.md` for earlier verdicts on the same idea.

Write two inputs for the prompts:
- **Design packet** (400 words at most): the pillars in brief; the settled decisions (with dates) and open questions this idea touches; existing powers, pieces and bank ideas it overlaps or combos with; earlier verdicts on it.
- **Store-page pitch** (120 words at most, no jargon): the game as its Steam page would describe it today. Base it on the brief's current state (for example asymmetric PvP: attacker versus defender, five attempts), not on older notes.

If the brief isn't in `docs/design/` yet, say so once and work from the game-design skill's snapshot.

## 3. Run the three reviewers in parallel

Send all three Agent calls in one message so they run at once and stay independent. Don't override their model.

| subagent_type | Its prompt gets |
|---|---|
| `game-designer` | Restated idea(s) + design packet |
| `veteran-player` | Restated idea(s) + design packet |
| `casual-gamer` | Restated idea(s) + store-page pitch only |

Prompt shape: a one-line task ("Review these ideas for the game, in your output format"), the restated ideas, then the packet or pitch. Pass any angle the user asked for (for example "focus on PvP") to all three.

**If those agent types aren't listed** (agent files load when a session starts, so a fresh session may not have them): read the definition from `.claude/agents/reviewers/<name>.md`, or else `~/.claude/agents/<name>.md`. Spawn a `general-purpose` agent with everything below the definition's frontmatter at the top of the prompt, then the inputs. Keep the casual gamer's prompt free of the design packet.

## 4. Merge

Don't paste the three reviews. The user reads on a phone, so aim for about one and a half screens per idea.

1. **The call**, in one sentence: Build, Build with changes, Prototype first, Park or Cut. You make it, weighing all three. A confused casual gamer is a readability problem to fix, not a reason to cut, unless the idea's appeal depends on being understood at a glance.
2. **Verdicts** as a two-column table, "Reviewer | Verdict and take", for example "Designer | Build with changes, 7/10: …".
3. **They agree:** one to three bullets.
4. **They split:** one or two bullets, each with why. The split is often the most useful finding.
5. **Explains itself?** Compare the casual gamer's "What I think it does" with the real idea. If they got it wrong, say which part misled them.
6. **Better version:** two to four sentences merging the reviewers' fixes.
7. **Fix early:** one to three risks.
8. **The decision for you**, in bold, last.

For several ideas: open with a ranked table, "Idea | Call and average score", then a short block per idea (the take, the better version, the biggest risk), and end with one bold question.

Keep the full reviews in context; if the user asks for one ("show me the veteran's review"), paste it.

## 5. Save

Append a dated entry to `docs/design/idea-reviews.md` (read it first; create it with a one-line header if it's missing), and remind the user to mirror it to the claude.ai project. Per idea, record the restated idea, the three verdicts and scores, your call, the better version, the risks and the open question. Don't store the raw reviews. Mention the save in one line.

Reviews are opinions, not decisions: never change `game-brief.md`, `power-palette.md` or the idea bank because of a review. If the user then makes a decision, the game-design skill records it.

## Options

- **"Debate" or "let them argue":** after the merge, `SendMessage` each reviewer the other two reviews and ask for a short rebuttal (what changes their mind and what doesn't). Merge the rebuttals in eight lines or fewer.
- **One perspective only** ("just the casual gamer"): spawn only that reviewer.
- **Compare two versions** of an idea: restate both as separate ideas and have the merge say which wins and why.