---
name: pattern-data-hashing
description: Checks for rules/map/plan loading and their hashes (JsonReader, JsonCanonical, RulesetHash, MapData, BattlePlan, BattleSetup) - lone surrogates and non-ASCII in canonical UTF-8, hash over file order vs sorted order, $comment in hashes, map size caps vs Fix overflow, unpinned cross-platform hashes
metadata:
  type: project
---

Check first when anything under sim/src/GodGame.Sim.Rules/Json, RulesetLoader/Hash or sim/src/GodGame.Sim/Data changes (first audited 2026-11-01, M1 item 5):
- JsonCanonical was made pure ASCII (\uXXXX for >= 0x7F) by 2026-11-01 (M1 item 6 audit); re-check lone-surrogate rejection in JsonReader if touched.
- Canonical form keeps array order. Plan army is sorted by id for deployment but hashed in file order: same outcome, different hash (false PvP/replay mismatch, not a divergence). Hash should be built from the sorted army.
- $comment is inside every hash (ruleset, map, plan): comment edits move pinned hashes and goldens. Decide deliberately; flag if surprising.
- Map widthMm/depthMm allowed up to 100 km while FixVec2.LengthSquared is exact only to ~32 km (see [[pattern-tables-vectors]], [[pattern-derived-spatial]]). Large maps also mean platform-dependent OOM on the height array. Want a cap near the plan's map sizes.
- Heights are unbounded int32; zone rects only validated in code, not schema (code is stricter than schema; schema "integer" also admits 1.0, code does not).
- Sorts there are all on unique keys (ordinal ids, Side), so unstable List.Sort is fine on CLR/Mono/IL2CPP. Dictionaries/HashSets were lookup-only. Re-verify on change.
- Tests ran deploy twice in one process only; ask for pinned map, plan and deployed-world hashes (cross-runtime evidence).
- JsonReader skips a BOM written as a raw invisible U+FEFF char literal; prefer '﻿'.
- .meta files missing again for Data/, Json/ and the new Rules files (sixth time). See [[pattern-overflow-assert-side-effects]].

**Why:** No High in the first audit, but these break replay/PvP hash agreement once real content and Unity-authored data arrive.
**How to apply:** grep WriteString/ReadString for surrogate handling, compare what is hashed with what is deployed (order), and re-check map size caps against Fix limits.
