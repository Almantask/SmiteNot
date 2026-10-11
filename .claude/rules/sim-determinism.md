---
paths:
  - "sim/src/**/*.cs"
---

# Simulation rules (deterministic, engine-free)

Outcomes must be bit-identical across runs, thread counts, Windows and Linux, and the .NET, Mono and IL2CPP runtimes.

## Never
- float, double, decimal, MathF, floating-point Math overloads. Use Fix (64-bit, 16 fractional bits; arrives in M1) or integers in natural units. The build fails on these (GG0001).
- System.Random, DateTime.Now/UtcNow, Stopwatch, Environment.TickCount, Guid.NewGuid (RS0030, from sim/BannedSymbols.txt).
- string.GetHashCode or hashing object references. Use SimHash (xxHash64).
- Iterating Dictionary/HashSet where order affects state. Use arrays, sorted keys or insertion-ordered containers.
- Sorting without a total tie-breaker (key, then entity id).
- Threads, Tasks or Parallel directly. Use IParallelRunner (M1); each chunk writes a disjoint range; reduce in index order.
- Static mutable state; UnityEngine; runtime NuGet packages; C# above 9 (no global usings, no file-scoped namespaces, no records' `init` without the IsExternalInit polyfill in GodGame.Sim.Math).
- Culture-dependent parsing or formatting: always pass CultureInfo.InvariantCulture.

## Numbers
- Positions, velocities: Fix. Fields: integers (mm, centi-degrees, 0–65535 scales, cm/s). Angles: 16-bit binary angles with table trig.
- Prefer squared distances to square roots in hot loops.
- Arithmetic is unchecked; assert against overflow in Fix products in debug builds.
- Inside GodGame.Sim.* the name `Math` resolves to the GodGame.Sim.Math namespace; write `System.Math.Min` for integer helpers.

## Structure
- Units are structure-of-arrays in World; one system per tick step, registered in TickOrder.cs. Never change the order without a SimVersion bump. Tick order: plan events → fields (5 Hz, active tiles) → structures and load → regiment AI (4 Hz) → navigation → movement → ballistics and impacts → combat → hazards → morale → bookkeeping (plan, section 3.4).
- Decisions are per regiment (staggered rates, with hysteresis); per-soldier work is O(1) per tick: flow-field sample, steering, spatial-hash neighbours.
- Randomness: SimRandom.Stream(seed, systemId, entityId, tick). Never keep a generator across ticks unless it is part of saved state.
- Log a cause event (with links to its causes) for anything a player could ask "why?" about: casts, structure failures, swept, launched, routs, specialist actions, and every death with its cause and source (combat, trampled, crushed, burned, drowned, fell with the ice, pushed).
- Hash every piece of state that can affect the future in StateHash; a field left out of the hash hides desyncs.

## Before you finish
- Fast tests green; thread-count determinism tests green (from M1).
- If existing goldens or pinned hashes change, stop and report. Don't regenerate them; /golden-replay does that.
