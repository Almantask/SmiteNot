---
name: pattern-tables-vectors
description: Checks for lookup tables built in static initialisers (Trig) and Fix vectors - overflow margins, pinned-bit tests, floored squares on short vectors, half-turn tie in DeltaTo
metadata:
  type: project
---

Check first when trig tables or Fix vectors change:
- Table builders (Trig.BuildQuarterSine / BuildAtanTable): tightest intermediate is term*x2 at i=16384, about 4.47e18, only 2x under long.MaxValue. Raising WorkBits to 31 overflows x*x. Re-derive the margin on every change.
- Static readonly tables that are never written are safe on CLR, Mono and IL2CPP and with Enter Play Mode without domain reload. Flag any write to them, or any lazy cache that is not static readonly.
- The Fast tests check trig to within one raw unit of System.Math, which does not pin the bits. Ask for a test that hashes Sin over all 65,536 angles and an Atan2 grid against a fixed constant.
- Fix vectors: LengthSquared floors every square at 2^-16 m^2, so Length/WithLength lose accuracy below about 0.1 m and return Zero below about 4 mm. This matters for crowd separation. The fix is exact raw squares in a ulong plus IntMath.Sqrt.
- Angle.DeltaTo at exactly half a turn always returns -32768 (clockwise), so mirrored scenarios do not stay mirrored.
- Missing .meta files came up again (2026-11-01, Angle/Trig/FixVec2/FixVec3). Check for them on every new sim/src file. See [[pattern-overflow-assert-side-effects]].

**Why:** These came up in the 2026-11-01 Trig/Angle/FixVec audit. None of them diverges across runtimes, but they are traps for future changes.
**How to apply:** grep for static readonly arrays and writes to them, re-check the multiply margins, and look for a test that pins the table bits.
