# Content roadmap: what to add first, and when

Which units, god powers, terrain pieces and world systems from `claude/unit-brainstorm.md`, `claude/terrain-weather-brainstorm.md` and `claude/power-palette.md` get built first, and in which order. Recommendations, not decisions; `claude/game-brief.md` wins any conflict. Milestones (M) and spec passes (S) refer to `claude/implementation-plan.md`, section 9. Drafted 2026-10-11.

## Rules for picking

1. **Systems first, content rides on them.** A wave only adds content whose systems exist by that milestone.
2. **The premise first.** Early content must turn the attacker's mass against it (load, crowds, ruts) and teach the bank shot of the 100 m rule.
3. **Shape before kill.** Every wave has at least as many slowing and redirecting tools as lethal ones.
4. **Field budget.** A new world system ships only when at least three strong ideas need it (gamer review, 2026-10-09).
5. **Launch on four systems:** water, temperature (fire and ice), wind, load. Fog and smoke, then snow, arrive as campaign chapters (review, 2026-10-10).
6. **Every piece needs a warning and a counter** before it ships.
7. **Every god tool ships with a unit-side answer** (brief, 2026-10-11): when a wave adds a setup, it also adds a specialist that answers part of it, and the defender gets units that shape ground inside its own buffer.
8. **Launch roster:** the four base types plus nine specialists; hold back the movement-class units (stilts, skis, coracles) until their chapters.

## The first set (M3 and M5)

Build this first. It needs only water, load, launches and herds, and it carries the vertical slice (design gate B), the first four staple situations and the trailer.

- **Systems:** water, wetness and mud that the army ruts itself (TW #1); per-type ford depth limits (part of TW #7); load with one strain language (creak, crack, sag; IB 57); launches and trampling; herds and stampedes; morale shock.
- **Units:** infantry, cavalry, ranged, siege engine (a stone thrower), plus six specialists that shape the world or answer the god's first tools (brief, 2026-10-11):

  | Specialist | Shapes or answers |
  |---|---|
  | Spademen (defender) | Dig a ditch and bank inside the buffer, where the god can't reach; rain turns it into a moat |
  | Fascine Carriers (attacker) | Fill one crossing through a channel, a mud patch or a ford |
  | Pontoon Train (both) | Float a bridge over a flooded ford or a broken bridge; a flood or a drain can break it |
  | Drovers (both) | Walk a herd off the road, or keep one between them and the enemy; never start a stampede |
  | Crossing Marshals (attacker) | Meter a bridge or ice so one regiment crosses at a time: the bridge holds, the bank queues |
  | Engineers (both) | Build a short timber trestle over narrow water, a channel or a gap, or repair a weakened span; clear fallen trees, debris and wrecks from the road |

- **Powers:** Rain, Lightning (fells a tree, lights dry brush), Rot.
- **Terrain:** dig a channel, raise a ridge or dig a hollow, add a pond or spring stream, swamp or firm road, boulders (static), trees, wooden and stone bridges, hedgerows and drystone walls (as blockers), herds, herd pen; level-set frozen ponds (fixed ice, until cold arrives in M6).
- **Staple situations it enables:** rutted road, accordion, moat opening, flash-flood ford, bridge conga, polite queue into stampede express, ice rink.

Each god tool in the set has its answer: channel and moat against Fascine Carriers or an Engineers' trestle, a flooded ford against the Pontoon Train, a rotted bridge against Crossing Marshals or an Engineers' repair, a felled tree against Engineers clearing it, herds against Drovers, and the defender's Spademen against cavalry. Every answer covers part of a setup, not all of it (one fascine regiment fills one crossing, not a whole channel), so the god's energy stays the stronger lever.

Why this set: it shows every pillar with the fewest systems. The bank shot (rain upstream floods the ford later), mass against itself (ruts, bridge load, the accordion), a combo (lightning plus a herd), shaping before killing (mud, the queue) and lethal chains (the bridge, the flood) are all there, and all of it reads clearly at strategic zoom.

## Waves

### Wave 1 — Water (S1, M3)
- **Powers:** Rain.
- **Terrain:** channel, ridge or hollow, pond or spring stream, swamp or firm road, static boulders.
- **Systems:** water, wetness, mud with traffic ruts (TW #1), ford depth limits per type.

### Wave 2 — Armies, load and launches (S2, M5)
- **Units:** the four base types; Spademen, Fascine Carriers, Pontoon Train, Drovers, Crossing Marshals and Engineers through the shared specialist executor, with simple fixed triggers.
- **Powers:** Lightning, Rot; a simple wind (prevailing wind plus one plume) for arrows.
- **Terrain:** trees, wooden and stone bridges, hedgerows and drystone walls, herds, herd pen, fixed ice.
- **Systems:** load and the strain language (IB 57), launches, trampling, morale, herds and stampedes.

### Wave 3 — The launch world (S3, M6)
- **Powers (completing the launch dozen):** Spring, Dam break, Wither, Wind shift, Calm, Cold snap, Heat wave, Tremor (also sets boulders rolling), Thunderclap. With Rain, Lightning and Rot that makes twelve, all on the four launch systems.
- **Terrain:** watchtower, palisade, dam or weir; boulders that roll when a tremor releases them.
- **Systems:** temperature (heat and cold in one field), fuel and fire with smoke, ice that freezes and melts, wind plumes with shelter.
- **Behaviour to add here:** rain and smoke drift with the wind, so storms can be cast upwind to arrive on time (IB 1, a must-build for both gamer reviewers); open question below.
- **Emerges for free:** fire melts ice, black-line firebreaks that funnel the army (TW #17), fire turnabouts.
- **Staples it adds:** Strike!, dam-break surf.

### Wave 4 — The rest of the specialists and a smarter attacker (S4, M7)
- **Units:** Firebrands (attacker: burn hedges and wooden pieces, now that fire spreads), Sappers (attacker: undermine stone walls and towers), Wheel-Breakers (defender: wreck stalled siege engines).
- **AI rules:** both AIs hunt and escort specialists by fixed, visible rules.
- **More AI rules:** "water behind them" (TW #25): an attacker whose retreat route floods turns its rear back, the model for morale from real threats; Lure Routes (IB 2) if regiment stances are approved.
- **Optional:** soaked wool (IB 33), one number per regiment that makes waded troops heavier on ice ("they waded the ford, so the ice broke").
- **Staples it adds:** the fire chases them home.

### Wave 5 — Round play: attack mode and PvP (S5, M10–M11)
- **Attacker answers (attack mode needs them):** Testudo Cohort (against archers and towers, but heavy on ice), Hounds (the finisher that finds hiding defenders, since the defender wins by surviving).
- **Anchors for the counter loop:** Throwback Pikes (launchers beat dense blocks; anchors beat launchers).
- **Defender doubt and survival (PvP):** Straw Legion (one fresh doubt per round), Rally Drum (where broken regiments run).
- **Economy:** bought upgrades (drill and kit).

### Wave 6 — Early Access chapters (M12), one new field per chapter
1. **Fog and smoke chapter:** Fog bank power, smoke pooling under Calm; marching dust (TW #4) on the same density field; Smudgers, Linkmen, Kite Watchers.
2. **Ground chapter:** Landslide, Sinkhole, Liquefaction, Scour, Drain; old mine workings (reuses load); vineyard rows (TW #13, a cheap direction-cost piece that combs the column into files).
3. **Snow chapter:** snow depth, drifts, trail-breaking, snowmelt surge, avalanche; freezing rain (siege toboggan); Snowshoe Pathbreakers, Tobogganeers, Ski Runners.
- **Toy-box units across chapters:** Hill Wheel, Remount String, Pilum Throwers, Wagon Fort, Field Kitchen, War Drums (lockstep bridges, TW #5).
- **Shaping power:** Overgrowth.

### After Early Access — each needs a new system
| System | What it unlocks |
|---|---|
| Cold air that flows downhill, plus an inversion lid | Frost hollows, valley fog, smoke pooling, frozen ruts (TW #2, the most-converged brainstorm idea) |
| A belief map per army (seen vs true) | Attacker's eyes (TW #24), false crests, mirages, hidden ditches |
| A day clock | Sun in their eyes (TW #14), morning mist, freeze-thaw mornings, night |
| Off-map catchments with travel time | Distant cloudburst (TW #3) and slot canyons: the desert wadi chapter |
| Groundwater | Raised water table (TW #11), karst springs (IB 6), poljes |
| Tides | Tide-window causeway (TW #8), Call the tide, tidal bores: the coast chapter |
| A water movement class | Stilt Slingers, Coracles, Fen Skaters, Ice Yachts, Reindeer Sledges |
| Body state per regiment | Thirst and the one well (TW #6), heat fatigue |
| Map-dependent features | Lava vent, geysers, glacier outbursts |

## Parked or cut

- **Parked (conflict with a settled rule):** Mule Train (heeds warnings), Herald (acts on the enemy AI, not the world), the Sellswords bribe.
- **Parked (economy complexity):** Recruiting Wagon.
- **Later, needs extra systems:** Camel Lancers and anything else on scent plumes; Teeterboard Tumblers; Belfry; Leadfoots, Shove Line and Ploughshare Cataphracts (wait until the base counter loop is balanced).
- **Cut:** fog fratricide (reads as dice), lethal gas hollows and pyroclastic flows (one-hop harm), tornado, hail and ball lightning (strike soldiers directly).
- **Open in the brief:** omen and morale powers; swarms and wolves.

## Open questions this raises

- Does Rain drift with the wind from launch (cast upwind, IB 1), or stay a fixed area until later?
- Is Thunderclap in the launch dozen (a stampede trigger that works in the rain), or does Overgrowth take that slot (a pure shaping power)?
