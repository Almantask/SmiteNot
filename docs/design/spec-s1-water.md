# Spec S1: water and the first power

Draft, 2026-11-02, for milestone M3 ("rain floods a ford and the attacker pays for it"). Implementation plan section 9 sets the scope: Rain (area, wind-up, duration, water added), river and mud rules, and the first terrain pieces (channel, ridge, spring, boulders). Draft rules data: `docs/design/s1-data/powers.json`, `terrain.json`, `fields.json`. These files are not in `data/rules/` yet. The implementer wires them in, because the loader rejects unknown files.

The brief (`docs/design/game-brief.md`) wins over this spec. Every number below carries one of three labels:

- **[Brief, date]**: settled in the brief. Not up for change here.
- **[Docs]**: taken from another design doc (power palette, tech notes, terrain and weather brainstorm, content roadmap, unit spec, implementation plan). These are recommendations, not decisions.
- **[Proposal]**: a working default invented for this spec, with the reason for it. The user should confirm or change these. The data files mark them `"status": "placeholder"`.

This spec covers only what M3 needs. Fire, cold, ice, wind drift, unit masses and prices belong to later passes (S2, S3, S5). They are listed in section 14.

---

## Decisions needed (from the user)

None of these are in the brief yet. Each has a recommended answer, and the data uses that answer until the user decides. Record the answers with the game-design skill, then mirror them to the claude.ai project.

| # | Question | Options | Recommended (used in the draft data) |
|---|---|---|---|
| D1 | **When does a power with a wind-up "fire" for the range rule?** The brief says "at the tick it fires" (2026-10-08) but doesn't say which tick that is for a power that builds up first. | (a) at the queued tick, when the clouds start to gather; (b) when the first water falls | **(a)**. The timeline marker, the range check and the visible warning (clouds) are then the same moment, and the wind-up becomes travel time the player has to anticipate (palette criterion 5). The range check doesn't run again once the rain starts. |
| D2 | **How big is Rain, and is its shape free?** | (a) a fixed circle; (b) a circle with a radius the player picks; (c) painted shapes | **(b) for M3: a circle with a radius from 40 to 100 m.** Painted areas can wait for the M4 planning interface. The price scaling with area comes in S5. |
| D3 | **How strong is god rain?** Real cloudbursts drop about 0.01–0.03 mm/s, which would do nothing in a 3-minute battle. | (a) exaggerated rain, 3 mm/s; (b) realistic rain plus a hidden catchment multiplier on rivers | **(a)**. What the player sees is what the river gets (the "visuals are the gameplay state" pillar), and the exaggeration matches the exaggerated physics decision (2026-10-09). The rate is tuned against the M3 flood test (section 13). |
| D4 | **The spring and the no-trap rule.** A spring that flows all battle can fill a closed basin and drown a route. The brief exempts "floods" made during the battle (2026-10-09) as long as they "clear on their own or are clearable". In M3, water in a closed basin never clears: there is no evaporation before heat arrives in S3, and no Pontoon Train before M5. | (a) treat spring water as a battle-created flood, so it is exempt; (b) at placement, estimate the spring's flood with a static fill and count it in the reachability check; (c) let the spring flow for a limited time only | **(a), plus a discharge cap (section 9.4)**, revisited in M5 when Pontoon Train and Engineers make water clearable. If playtests show springs used as walls, switch to (b). |
| D5 | **Earthworks in water.** | Can ridges be raised in rivers and ponds? Can boulders be set in them? | **Ridges: no.** A ridge across a river would be a dam, and dams are their own piece in S3/M6. **Boulders: yes**, as the palette says ("in a stream they dam it"), but only up to 2 m high, so deeper water flows over them. |
| D6 | **The rest of Wave 1 terrain.** Content roadmap Wave 1 also lists a hollow, a pond, swamp and a firm road. The M3 build list names only the four pieces. | Add them to M3, or defer them | **Hollow: add it as the ridge's negative variant**, since it is nearly free (`terrain.json` has a `hollow` entry marked optional). **Pond, swamp and firm road: defer to M5 / S2.** M3 maps get the `road` soil from the map author, so the rutted-road staple still works. |
| D7 | **Who gets the blame when water has mixed causes?** River water and rain water mix in the same cells. | (a) the cast that most recently added water to that area; (b) the largest contributor | **(a), tracked per tile (section 12)**. It's cheap and readable, and it makes the M3 exit criterion ("every drowned soldier traces back to the cast") testable. |
| D8 | **Which classes cross a dry channel and a ridge?** Movement-class slope limits are placeholders until S2. | | **Foot and horse cross both. Wheeled can't cross a ridge, or a channel deeper than 750 mm**, so siege goes around and both pieces become funnels for the siege train. The reachability check treats that as closed for wheeled (section 10). |
| D9 | **Do swept soldiers count toward their regiment's outline for the range rule?** | | **Yes**, extending the 2026-10-09 working assumption for launched and stunned soldiers, so flooding a regiment can't be used to cast closer to it. |
| D10 | **Do moats kill?** With sloped channel sides, a soldier pushed into still moat water paddles to the side and climbs out stunned. | (a) moats mostly stun, and drowning comes from flowing water; (b) steep moat walls that drown | **(a)**. It follows "shape first, kill second" (2026-10-09). The moat-opening staple ("plop in one by one") is a gag and a blocker, not a death trap. Flowing water (the flash-flood ford) is the killer. |

There is also a conflict between two design docs, resolved in this spec without a decision:

- Tech notes section 5 says the swept threshold is "lower for ... cavalry".
- Terrain and weather brainstorm #7 gives cavalry a deeper ford limit (about 1.2 m) than infantry.

Both are kept: horses can stand in deeper still water, but current knocks them over sooner (section 3).

---

## 1. Units and grid facts

From the code and implementation plan sections 3.3, 3.4 and 8.

| Item | Value | Source |
|---|---|---|
| Field cell | 1,500 mm (2.25 m² per cell) | [Docs] plan section 8 (working map scale) |
| Simulation tick | 20 per second (50 ms) | [Docs] plan section 3.4 |
| Field step | every 4 ticks (5 Hz, 200 ms), active tiles only | [Docs] plan section 3.4 |
| Ground and water heights | int32, mm | [Docs] plan section 3.3 |
| Wetness, mud | uint16, 0–65535 | [Docs] plan section 3.3 |
| Rates in data | µm/s for small water rates, L/s for discharges, mm/s for speeds, ticks for times, permille (‰) for ratios | [Proposal]: these units keep every number an integer |

**Implementer note [Proposal].** Rain and runoff add fractions of a millimetre per field step: 3 mm/s is 0.6 mm per step. Plan section 3.3 stores water height in mm. The water kernel should therefore keep sub-millimetre precision, either by storing depth in µm (int32 still covers ±2 km) or by carrying remainders. Otherwise thin sheets of runoff stall. This is a storage choice for the sim-engineer, not a game rule. The data gives every rate in µm/s so either choice reads it exactly.

---

## 2. The water model

The tech notes call for "virtual pipes" shallow water on the heightfield (section 5) [Docs]. Each cell holds a water depth. Each cell edge (pipe) holds a flux. Water surface = ground height + depth.

| Parameter | Value | Label and reason |
|---|---|---|
| Substeps per field step | 2 (water effectively at 10 Hz, dt = 100 ms) | [Proposal]. Stability: with a safety margin, a wave should travel at most half a cell (0.75 m) per step, so √(g·h)·dt ≤ 0.75 m. At dt = 200 ms that limits depth to about 1.4 m, shallower than the M3 river. At 100 ms it allows about 5.7 m. Costs 2× on water tiles only; perf-check in M3. |
| Gravity | 9,810 mm/s² | Physics |
| Pipe gain per substep | g·dt / cell = 9.81 · 0.1 / 1.5 ≈ 654‰ (flux in mm/s of depth change per mm of head difference) | Derived; pipe cross-section = cell² (standard virtual pipes) |
| Flux damping per substep | 990‰ | [Proposal] Friction stand-in so steady rivers don't accelerate forever. Tune it so the M3 river runs at about 0.5–1.0 m/s at base flow. |
| Outflow limit | a cell never sends out more water than it holds (standard scaling factor K = min(1, depth / (total outflux · dt))) | Standard virtual pipes. Keeps depths ≥ 0. |
| Dry threshold | 5 mm. Below this a cell counts as dry for units and rendering. | [Proposal] Ignore film-thin runoff. |
| Velocity minimum depth | 20 mm. Shallower water has flow speed 0. | [Proposal] Avoids huge speeds from dividing by tiny depths. |
| Flow speed (derived per cell) | average edge flux × cell / depth, as two components in mm/s | Standard |
| Flow speed cap | 5,000 mm/s | [Proposal] Stability and readability. 5 m/s is already a torrent. |
| Solid cells | boulders block flow (no pipes into a boulder cell below its top) | [Proposal]. See 9.5. |

No special code is needed for channels, ridges or dug ditches. The kernel reads the heightfield every substep, so carved or raised ground steers water by itself (tech notes section 3, unit-made changes) [Docs].

**Active tiles [Proposal].** A water tile wakes when any cell in it, or any edge-neighbour tile, gains water or has flux. It sleeps after 5 consecutive field steps with every |Δdepth| < 1 mm and every flux under 1 mm/s. Rivers never sleep, which is correct. A river 20 m wide across 800 m is about 7,000 cells, small next to the 430,000-cell map.

---

## 3. Water and movement, per movement class

The content roadmap's first-set systems include "per-type ford depth limits" (part of terrain and weather brainstorm #7) [Docs]. The brief makes "deep or fast water" lethal and leaves "shallow water" slow but open (2026-10-09, no full traps; tech notes section 5) [Brief, 2026-10-09].

Each class has four water states:

- **Shallow**: open; wading slows the unit.
- **Deep**: closed to the class's own steering.
- **Lethal (swept)**: the unit loses its footing. A cell where the class would be swept is closed to steering and counts in the distance-to-lethal field.
- **Regain footing**: the threshold for climbing back out (hysteresis).

A cell sweeps a unit if water depth ≥ `sweptDepth`, or if depth ≥ `sweptMinDepth` and the depth × flow-speed product ≥ `sweptDepthSpeed`. The product is in mm·m/s (depth in mm × speed in mm/s ÷ 1000). It is the standard flood-hazard product: people lose their footing at about 0.6 m²/s.

| | foot | horse | wheeled | Label and reason |
|---|---|---|---|---|
| Wading speed, depth (mm) → speed (‰), linear between points | 0→1000, 100→1000, 300→800, 600→550, 900→350 | 0→1000, 200→1000, 500→800, 900→550, 1200→400 | 0→1000, 100→900, 300→600, 500→300 | [Proposal] Ankle water costs nothing. Knee and waist water halves speed. Wheels feel water first. |
| Deepest water it will enter (closed above this) | 1,000 (= swept depth) | 1,200 (= swept depth) | **500** | [Docs] brainstorm #7: siege axles about 0.5 m, cavalry about 1.2 m. Foot 1.0 m [Proposal]: about waist to chest. |
| `sweptDepth` (mm) | 1,000 | 1,200 | 900 | Foot and horse [Docs and Proposal] as above. Wheeled: an engine stranded in rising water is carried off at 0.9 m [Proposal]. |
| `sweptDepthSpeed` (mm·m/s) | 600 | 450 | 400 | [Proposal] Foot uses the standard 0.6 m²/s. Horse and wheeled are lower because of drag: tech notes section 5, "lower for cavalry and siege crews". |
| `sweptMinDepth` (mm) | 300 | 400 | 300 | [Proposal] Fast sheet runoff a hand deep never sweeps anyone. |
| Regain footing | below 750‰ of both thresholds | same | same | [Proposal] Hysteresis, so soldiers at the margin don't flicker between states. |

Per-type modifiers (heavy armour sweeps sooner, per tech notes section 5) belong to unit types in S2. Until then every unit type uses its class values.

**Cost fields [Proposal].** Flow-field cost multiplier = 1,000,000 / speed‰ (so 500‰ speed doubles the cost). Deep and lethal cells are closed. The fields mark closed cells per class every field step. The movement step's hard "never enter a lethal cell under own steering" check reads those marks each tick. That is what the M3 fuzz invariant tests. The flow-field cost rebuild can lag behind:

| Item | Value | Reason |
|---|---|---|
| Flow-field water refresh | at most once per 20 ticks (1 Hz) per dirty tile | [Proposal] Rising water dirties many tiles; this keeps the navigation budget. |
| Cost hysteresis | a tile's water cost is recomputed only when some cell's depth moved ≥ 100 mm since the last rebuild, or a cell crossed a closed/open threshold | [Proposal] |

**Reachability [Brief, 2026-10-09; tech notes section 3].** Deep water is a hard blocker. Shallow water is costly but open. For the check, "deep" means depth ≥ the class's deepest-enterable depth (foot 1,000, horse 1,200, wheeled 500 mm), measured on the settled water state at tick 0 (section 7).

---

## 4. Swept, drowning, bank exit, teetering

Settled [Brief, 2026-10-09 and 2026-10-11]:

- Soldiers never walk into lethal water under their own steering.
- They die when water rises around them, when they are pushed or launched in, or when the ground under them fails.
- Each soldier reacts only to what is lethal now, never to warnings.

Swept behaviour [Docs: tech notes section 5]: the soldier moves with the flow, its drowning timer runs, and it can reach a bank and survive.

| Item | foot | horse | wheeled | Label and reason |
|---|---|---|---|---|
| Carried at (share of local flow velocity) | 900‰ | 900‰ | 1000‰ | [Proposal] Bodies drift slightly slower than the current. Engines float like debris. |
| Paddle speed toward the nearest cell where it could regain footing | 400 mm/s | 600 mm/s | 0 | [Proposal] Paddling gives a fair chance in slow water and none in a torrent (a 2 m/s flood carries a soldier 5× faster than he paddles). Horses swim. Engines don't. |
| Drowning timer | 300 ticks (15 s) | 400 ticks (20 s) | 120 ticks (6 s) | [Proposal] 15 s at 0.4 m/s paddle covers about 6 m, enough to escape a channel or a ford's edge but not the middle of a flooded river. Cold water (ice, S3) gets its own shorter timer. |
| Bank exit | in a cell below the regain-footing thresholds: footing back, stunned 40 ticks (2 s), then rejoins its regiment (morale effects in M5) | | | [Proposal] The stun is the readable beat. 2 s matches the short stagger in tech notes section 5. |
| Timer after bank exit | resets in full | | | [Proposal] Simple. A second sweep is a new event in the trace. |
| Death cause | `drowned`, source = the water cause of the cell where the timer ran out (section 12) | | | [Brief, 2026-10-11] Every death records its cause and source. |

**Teetering [Docs: tech notes section 5; unit spec].** A soldier at a lethal edge, with crowd pressure above the edge push, teeters before going over. Proposal: **teeter for 20 ticks (1 s)**. One second is long enough to show in the 5-per-second playback track and to read on screen. If the pressure still exceeds the push after the teeter, the soldier goes over and is swept (cause `pushed by the crowd`, source his own regiment or the regiment behind). The pressure numbers belong to steering (S2). This value belongs in `data/rules/hazards.json` as `teeterTicks`, the obvious home for it.

**Range rule while swept [Proposal, D9].** Swept soldiers stay in their regiment's outline.

---

## 5. Wetness

Wetness is uint16, 0–65535 [Docs: plan section 3.3]. It is how much water the soil holds. It drives mud and, later, fire resistance (S3) [Docs: tech notes section 6].

| Item | Value | Label and reason |
|---|---|---|
| Soil water capacity | 32,768 µm (about 32.8 mm) absorbed = wetness 65535 | [Proposal]. 1 µm absorbed = 2 wetness units exactly, so the conversion is an integer shift. 30-odd mm is about what topsoil holds before it runs off. |
| Infiltration rate | 1,000 µm/s (1 mm/s) from rain or standing water into unsaturated soil | [Proposal] Lower than the rain rate (3 mm/s), so even dry ground starts running off at once. Dry ground (8,192) saturates after about 29 s of rain. |
| Map default initial wetness | 8,192 (12.5%, dry ground) | [Proposal] Below the mud threshold, so ground is firm until rain or a river soaks it. Maps can author wetter ground per cell. |
| Cells under water | water ≥ 5 mm infiltrates at the same rate, so riverbeds and flooded fields saturate | [Proposal] Flood margins turn to mud after the water drops: the flood leaves the next obstacle behind. |
| Drying | −110 units per second (22 per field step) on cells with no surface water: full to dry in about 10 min | [Proposal] Longer than a 3-minute battle (plan section 10's working default), so rain-soaked ground stays wet for the rest of the battle. Heat speeds drying in S3 (reserved row). |

---

## 6. Mud and ruts

[Docs: power palette, "Mud: follows from wetness; worsens as many feet churn it (scales with mass)"; terrain and weather brainstorm #1, "the army ruts its own road"; content roadmap rule 2.] Mud is never lethal and never a hard blocker: mud is slow but open [Brief, 2026-10-09].

Mud is uint16, 0–65535. No separate traffic field is needed in M3 [Proposal]: churn writes into mud directly, and mud is bounded by two curves of wetness.

| Curve | Formula (w = wetness) | Meaning |
|---|---|---|
| `base(w)` | 0 if w ≤ 32,768, else w − 32,768 (max 32,767) | Untrodden saturated ground is soft mud (half scale) on its own. |
| `cap(w)` | 0 if w ≤ 16,384, else (w − 16,384) × 4 / 3 (max 65,535) | The most mud this wetness allows. As ground dries, the cap falls and ruts dry out. |

Each field step, mud = clamp(max(base(w), mud + churn), 0, cap(w) × soil cap ‰ / 1000). A cell's churn is the sum over units moving through it this step:

churn per moving unit = `churnWeight[class]` × 96 × soil churn ‰ / 1000 × max(0, w − 16,384) / 49,151

| Item | Value | Label and reason |
|---|---|---|
| Churn at full wetness, one foot soldier, one step | 96 | [Proposal] A 200-strong regiment puts about 70 soldier-steps into each road cell (about 14 soldiers through the cell at about 5 steps each). About five regiments (1,000 soldiers) take a cell from soft (32,767) to deep (65,535). The vanguard strides through and the rear wades, as in the rutted-road staple. |
| `churnWeight`: foot / horse / wheeled | 1 / 6 / 25 | [Proposal, placeholder until S2 masses] Roughly the mass ratio (soldier about 80 kg, horse and rider about 500 kg, engine about 2 t), as the palette says (scales with mass). A siege engine churns its own track deep in one slow pass, so the siege train sinks behind itself. |
| Soils (map layer): `earth` | churn 1000‰, cap 1000‰ | [Proposal] |
| Soils: `road` (firm) | churn 250‰, cap 500‰ | [Proposal] A road gets soft but never deep. Palette: "a hard road that resists it". |
| Soils: `swamp` | deferred (D6) | |

Mud slows movement, linear between points [Proposal]:

| Mud | foot | horse | wheeled |
|---|---|---|---|
| 0 (firm) | 1000‰ | 1000‰ | 1000‰ |
| 32,767 (soft) | 800‰ | 700‰ | 500‰ |
| 65,535 (deep) | 500‰ | 350‰ | 150‰ |

Reason: cavalry and siege suffer most (palette criterion 3; brainstorm #7 and #19). Wheeled at 150‰ is "sunk to its axles", but never 0, so mud never becomes a lock. Water and mud multiply: a muddy ford stacks both. Cavalry charges stopping in deep mud is an open question in the brief (it belongs to M5).

---

## 7. Rivers on maps: sources, sinks, initial state

Plan section 3.8 lists water sources in `map.json` and initial water in the binary layers [Docs]. Proposed map format (for the map schema, not this spec's data):

| Item | Value | Label and reason |
|---|---|---|
| Source | a line of edge (or interior) cells, plus a constant discharge in L/s, added evenly across its cells every substep. 1 L over 1 m² = 1 mm, so per cell: discharge ÷ (n × 2.25 m²) mm/s, carried with a remainder. | [Proposal] Constant flow is enough for M3. Scheduled flows (tides, snowmelt) come later. |
| Sink | a line of edge cells with free outflow: the virtual cell outside has a water surface equal to the edge cell's ground height, so water leaves under its own head | [Proposal] Simple and stable. River level at the sink follows the river. |
| Other map edges | closed (no flux) | [Proposal] |
| Initial state | the map exporter runs the same water kernel offline until steady state and stores depth and flux layers | [Proposal] Every preview from tick 0 starts with a flowing river at no cost, and the reachability check sees the real river. |
| Terrain pieces at tick 0 | applied to the heightfield before tick 0, over the settled water. Water then responds during the battle (a channel cut into a river fills in its first seconds). | [Brief, 2026-10-08] Pieces exist from the first tick and "water flows" on its own. |
| "Would-flood" for reachability | a channel whose bed lies below an adjacent water surface at tick 0 counts as filled to that surface (a static fill on the coarse grid) | [Proposal] The check runs at placement, before any water moves. Without this, a channel cut into a river would look dry to the check. |
| Ford | not a special object: a wide, shallow reach of the river | [Proposal]. Map guidance: at base flow a ford should be ≤ 400 mm deep (below the wheeled limit) and ≤ 300 mm·m/s (half the foot threshold), so every class crosses it until it floods. |

---

## 8. Rain

Palette [Docs]: "wide, slow; raises wetness and feeds rivers over time... its water reaches them through rivers, floods and soaked ground they march into." Rain does nothing to soldiers directly. It only adds water and wetness to cells, so it never harms the army itself [Brief, pillar 1].

| Item | Value | Label and reason |
|---|---|---|
| Area | circle, radius 40,000–100,000 mm, default 80,000 | [Proposal, D2] "Wide": up to a 200 m storm. The upper bound keeps the 100 m rule meaningful on an 800 m gap between zones. |
| Range rule | whole area ≥ 100,000 mm from both armies' regiment outlines, else fizzle | [Brief, 2026-10-08] |
| When it is checked | at the cast's queued tick, when the wind-up starts | [Proposal, D1] |
| Outline (M3 version) | the coarse cells (4.5 m, `routes.json` coarseCells 3) holding ≥ 2 soldiers of the regiment; distance = circle centre to the nearest edge of such a cell, minus the radius | [Proposal] The brief's working assumption (2026-10-08): "formation outlines, ignoring isolated stragglers". One lone soldier in a coarse cell doesn't count. Swept soldiers count (D9). Launched and stunned soldiers count from M5 [Brief, 2026-10-09]. Regiments under 25 soldiers not blocking casts is plan section 10's M7 default, not used in M3. |
| Wind-up | 200 ticks (10 s): clouds gather over the area, no water yet | [Proposal] Palette criterion 5 (anticipation). The clouds are the visible warning. 10 s is about 15 m of infantry march, so the wind-up matters without being a second range check. |
| Duration | 1,200 ticks (60 s) of rain after the wind-up | [Proposal] Fixed in M3. 60 s is a third of a 3-minute battle (plan section 10): long enough for a flood pulse, short enough that the river drops again before sunset, so "wait for the river to drop" remains a counter (palette staple 8). |
| Water added | 3,000 µm/s (3 mm/s) uniform over the area, from one per-cast accumulator | [Proposal, D3] About 180 mm over 60 s. Of that, about 30 mm soaks into dry soil and about 150 mm runs off. On the default 80 m radius (about 20,000 m²) that is about 3,000 m³ of runoff, roughly what it takes to raise a 60 m-wide ford by 0.3–0.5 m for a minute (section 13). Tunable from 2,000 to 5,000 against the M3 flood test. |
| Wetness | through infiltration (section 5), not a separate write | [Proposal] One source of truth: rain is water, and the soil decides what it keeps. |
| Feeding rivers | no special rule: rain on river cells raises them, and rain on land runs downhill through the water kernel into the river | [Docs: tech notes section 6, one field, one truth] Map authors make valleys that drain toward rivers. |
| Several rains | additive where their areas overlap | [Proposal] Paid for separately (S5). Turnabouts stay positional. |
| Stops early? | no. Once it passes the range check, it runs its full duration. | [Proposal] Only the firing tick is checked [Brief, 2026-10-08]. Armies may walk into the rain later: it is world state they march into. |
| Below freezing | falls as snow instead (palette) | Reserved for S3. M3 has no temperature field. |
| Drift with the wind | stays put in M3 | [Docs] Content roadmap open question (IB 1, M6). |
| Energy cost | not set | S5 |

**Placement checks (refused while planning, not fizzles) [Proposal]:** the area is not wholly inside the map, the radius is outside 40–100 m, or the tick is before 0 or after the battle cap. The planning interface shows these as red placements, like a blocked terrain piece.

**Fizzle reasons at the firing tick** (reported with the regiment id and the measured distance, as the plan's M4 red markers need):

- `TooCloseToAttacker`: the nearest attacker outline is under 100 m from the area.
- `TooCloseToDefender`: the same for the defender.

If both apply, report both. A fizzled cast writes nothing and emits a `cast fizzled` cause event. Whether its price is refunded belongs to S5.

---

## 9. Terrain pieces

### 9.1 Common rules

- **Buffer [Brief, 2026-10-08].** Every piece stays at least 100,000 mm from both deployment zones, measured from the edge of the zone to the nearest edge of the piece's whole footprint. Consequences (water) may flow into the buffer. Designer-placed pieces are exempt (plan section 10, working default).
- **No full traps and the 2× route cap [Brief, 2026-10-09].** These are checked on every drag on the coarse grid, with `routes.json` clearances.
- **Height edit order [Proposal].** Pieces edit the heightfield before tick 0 in this fixed order:
  1. All channels and hollows, by `min(ground, profile)`.
  2. All ridges, by `max`.
  3. All boulders.

  Within each kind the result doesn't depend on order. Across kinds the order is fixed, so plan order never changes the outcome.
- **Materials [Docs: palette].** Channel, ridge and hollow are earth. Boulders are stone (fire does nothing; a tremor in M6, a flood only for small ones). The spring is water.
- **Limits per piece** are below. How many pieces a player may place is a price question (S5) or the level's allowance (campaign).

### 9.2 Channel (dig)

Palette [Docs]: "routes a river toward or away from a ford; floods later with rain." Staple 5, the moat opening.

| Item | Value | Label and reason |
|---|---|---|
| Shape | a polyline of 2–8 points, drawn on the map | [Proposal] |
| Length | 15,000–150,000 mm (total of all segments) | [Proposal] Long enough to divert a river around a ford on the working map, short enough that one channel can't cut the map in half. |
| Bottom width | 3,000–6,000 mm (2–4 cells) | [Proposal] At least 2 cells, so the water kernel carries flow down it. |
| Depth | 500–2,000 mm below the original ground (the bed follows the terrain's slope) | [Proposal] Up to 2 m, so it can become a moat (foot `sweptDepth` 1,000 mm needs room) but never a cliff (`hazards.json` lethalDropMm 4,000). |
| Profile | bottom cells at −depth; one side cell on each edge at −depth/2 | [Proposal, D8] Step slopes are (depth/2) / 1,500 mm: 2,000 mm deep gives 667‰, so foot (1,000) and horse (700) cross dry. Wheeled (250) can only cross channels ≤ 750 mm deep. A swept soldier can paddle to the side cell and climb out (D10). |
| Footprint for the buffer | bottom plus side cells | [Brief, 2026-10-08] "the piece's whole footprint" |
| Spoil | vanishes (no bank) | [Proposal] Keeps the god's channel distinct from the Spademen's ditch and bank (S2). |
| In water | allowed: cutting into a river is the point | [Docs: palette] |
| Reachability | closed for any class whose slope limit it exceeds (wheeled, for channels over 750 mm). Counts as filled where it connects to tick-0 water (section 7), and then deep water closes it for every class. | [Proposal] |
| Warning and counter | rising water in the channel, then teetering soldiers (palette staple 5). Answers: wait for it to drain; Fascine Carriers or an Engineers' trestle (M5) | [Docs: palette, content roadmap] |

### 9.3 Ridge (raise), and its optional variant, the hollow

Palette [Docs]: "high ground for archers, a hollow that collects floodwater."

| Item | Value | Label and reason |
|---|---|---|
| Shape | polyline of 2–8 points; crest along the line | [Proposal] |
| Length | 10,000–120,000 mm | [Proposal] |
| Height | 1,000–3,000 mm above the original ground | [Proposal] High enough to matter for archers (M5) and as a levee that steers floodwater. Under the 4 m cliff threshold, so it is never lethal itself. |
| Crest width | 3,000 mm (2 cells) | [Proposal] One regiment of archers can stand along it. |
| Side slope | 400‰ (down to the original ground) | [Proposal, D8] Foot and horse climb it. Wheeled (250) can't, so siege goes around: the ridge funnels the siege train. |
| Footprint | crest plus both slopes (a 3 m ridge is about 18 m wide at the base) | [Brief, 2026-10-08] |
| In water | refused where any footprint cell has tick-0 water ≥ 5 mm | [Proposal, D5] Dams are their own piece (S3). |
| Hollow (optional, D6) | same shape and limits, depth 1,000–3,000 mm, applied by `min`. Allowed in dry ground only. Collects runoff into a pond. | [Docs: palette, content roadmap Wave 1] |
| Reachability | closed for wheeled along its slopes. Open for foot and horse. | Follows from the slopes |
| Warning and counter | visible on the map. Answers: go around. Ridges can't be destroyed in M3 (a tremor and Sappers come later). | [Docs] |

### 9.4 Spring

Palette [Docs]: "open a spring on a hillside to flood a valley floor gradually". Content roadmap Wave 1: "pond or spring stream" as terrain. (Spring as a timed power is Wave 3, S3/M6.)

| Item | Value | Label and reason |
|---|---|---|
| Footprint | a 3 × 3-cell source block (4,500 mm square), centred on the placed point | [Proposal] Small and easy to read. Its water is a consequence and may flow into the buffer. |
| Discharge | 500–3,000 L/s, default 1,500 | [Proposal] A god-sized spring. 1,500 L/s for 3 min is 270 m³: a stream and a slowly spreading pond on a valley floor (about 0.1 m over 2,700 m²), "gradually" as the palette says. The cap of 3,000 L/s (540 m³ over 3 min) keeps one spring from drowning a map-scale route (D4). |
| Flow | constant from tick 0 to the end of the battle | [Proposal, D4] Terrain is never timed [Brief, 2026-10-08]. |
| Placement | dry ground, not on a boulder | [Proposal] A spring in a river adds little. Refusing it keeps placement readable. |
| Reachability | ignored at placement: its water is battle-created (D4 recommendation) | [Brief, 2026-10-09] "floods" are battle-created |
| Warning and counter | the stream's path and the pond's spreading edge, both shown in the preview. Answers: go upstream or around; Pontoon Train or Engineers (M5) | [Docs] |

### 9.5 Boulders

Palette [Docs]: "hard blockers that funnel the column. Fire can't clear them... In a stream they dam it." Static in M3; rolling arrives with the tremor in M6 [Brief, 2026-10-11, rolling boulders].

| Item | Value | Label and reason |
|---|---|---|
| Unit | one boulder per placement | [Proposal] Players build clusters and lines one by one, and S5 prices them each (palette: priced above trees). |
| Footprint | 3,000 × 3,000 mm (2 × 2 cells), axis-aligned to the grid | [Proposal] At least 2 cells, so a single boulder reads as an obstacle at strategic zoom. |
| Height | +2,000 mm over the highest ground under it | [Proposal] Water deeper than 2 m flows over it. A boulder line in a stream backs water up to 2 m (palette: "in a stream they dam it"). |
| Navigation | closed to every class (a hard blocker, whatever the slope) | [Brief, 2026-10-09] boulders are hard blockers |
| Water | solid below its top: no pipes into its cells | [Proposal] |
| In water | allowed (D5) | [Docs: palette] |
| Overlap | not on another boulder | [Proposal] |
| Reachability | closed coarse cells; the clearances in `routes.json` apply (siege needs the widest gap) | [Brief, 2026-10-09] |
| Warning and counter | visible. Answers: go around, Engineers clear small boulders (M5) [Brief, 2026-10-11], a tremor (M6). | |

---

## 10. Reachability treatment, in one table

| Piece or state | foot | horse | wheeled | At lock-in or placement |
|---|---|---|---|---|
| Shallow water (below the class's deepest-enterable depth) | open | open | open | open, costly |
| Deep water at tick 0 (river, pond, connected channel) | closed ≥ 1,000 mm | closed ≥ 1,200 mm | closed ≥ 500 mm | hard blocker [Brief, 2026-10-09] |
| Water that arrives during the battle (rain, spring, overflow) | | | | exempt: battle-created [Brief, 2026-10-09] |
| Dry channel ≤ 750 mm deep | open | open | open | |
| Dry channel 750–2,000 mm deep | open | open | closed | slope [Proposal, D8] |
| Ridge | open | open | closed | slope [Proposal, D8] |
| Hollow (dry) | open | open | closed if its slope exceeds 250‰ | slope |
| Spring | (source block open) | | | its water exempt (D4) |
| Boulder | closed | closed | closed | hard blocker |
| Mud | open | open | open | never a blocker [Brief, 2026-10-09] |

---

## 11. Field interaction table v1

Field step order [Docs: plan section 3.4]: **rain writes → water (2 substeps) → wetness and mud.** Temperature and the later fields don't exist in M3, but their rows are reserved so the table's shape is stable. All strengths are in `fields.json`.

| Row | Reads | Writes | Rule | Label |
|---|---|---|---|---|
| `rain_to_water` | active rain casts | water depth | +3,000 µm/s per cell in the area | [Proposal] |
| `source_to_water` | map sources, springs | water depth | discharge ÷ cell area | [Proposal] |
| `water_flow` | ground height, water | water, flux, flow velocity | virtual pipes (section 2) | [Docs] tech notes section 5 |
| `sink_outflow` | map sinks | water | free outflow | [Proposal] |
| `water_to_wetness` | water ≥ 5 mm (including rain) | water −, wetness + | infiltration 1,000 µm/s, 2 units per µm, up to the soil capacity | [Proposal] |
| `wetness_drying` | water < 5 mm | wetness − | −110 per second | [Proposal] |
| `wetness_to_mud` | wetness, soil | mud | `base(w)` and `cap(w)`, section 6 | [Proposal] |
| `traffic_to_mud` | moving units per cell, class, wetness, soil | mud + | churn, section 6 | [Docs: brainstorm #1] numbers [Proposal] |
| `water_to_movement` | depth, flow speed | speed, cost, closed or lethal per class | section 3 | [Docs and Proposal] |
| `mud_to_movement` | mud | speed, cost per class | section 6 | [Proposal] |
| reserved S3: `heat_dries_wetness`, `heat_evaporates_water`, `cold_freezes_water`, `heat_melts_ice`, `wetness_damps_fire`, `rain_to_snow`, `wind_drifts_rain` | | | | [Docs] palette counter web; tech notes section 6 |

Counter-web rows touched in M3 (palette): "Rain, flood: countered by heat/drought, cold". Neither exists until S3, so M3 ships the reserved rows. The unit-side answers (Fascine Carriers, Pontoon Train, Engineers) arrive in M5 [Brief, 2026-10-11].

---

## 12. Causality for water [Proposal, D7]

M3 exit criterion: "every drowned soldier traces back to the cast". Water mixes, so the cause has to travel with it, cheaply.

- **Cause tag per water tile** (16 × 16 cells, 24 m). Rain stamps its cast's cause id on every tile in its area at each step it rains. A spring stamps its piece's id on its source tile.
- **Spreading the tag.** At each field step, a tile whose net inflow from a neighbouring tile exceeds 1,000 µm of depth (averaged over the tile) takes that neighbour's tag if the neighbour's tag is newer. Newer means a higher cause-event index, which is deterministic. Ties go to the larger inflow, then the lower tile index.
- **Base river water** has the tag `map:river` (the map itself).
- **Events:**
  - `river overflowed`: once per tile, the first time a cell in it becomes lethal for foot, where it had been non-lethal at tick 0. Source: the tile's tag.
  - `regiment swept`: once per regiment per 5 s. Source: the cell's tile tag.
  - `soldier drowned`: source: the cell's tile tag.
- **The chain then reads:** rain cast at 0:50 → river overflowed at tile (x, y) at 2:31 → regiment Attacker 4 swept → 37 drowned.
- **Pushed in.** If a soldier went in pushed by the crowd, the death records both causes: pushed by (regiment) and drowned in (tag).
- **Cost:** about 1,900 tiles on the working map, a few bytes each, only on active tiles.

---

## 13. M3 calibration: the test map and the targets

The exit criteria [Docs: plan, M3] become tuning targets for the [Proposal] numbers. Rain rate, damping, infiltration and churn are tuned here, never by changing the rules.

**Test map (to author):** the M2 working size (1.2 × 0.8 km), with the river running north to south as in `m2-bridge`.

- **Source:** 10,000 L/s at the north edge. **Sink:** the south edge.
- **River:** a channel 20 m wide and about 1.5 m deep, at about 0.5 m/s.
- **Ford:** a 60 m-wide reach about 300 mm deep where the attacker's direct route crosses. A bridge or second ford about 300 m away gives the reroute.
- **Valley:** sides slope 20–50‰ toward the river, so runoff reaches it.
- **Rain:** radius 80 m, centred on the river 250–350 m upstream of the ford, cast at tick 1,000 (0:50). Its edge stays more than 100 m from both armies at that tick.

| Target | Value |
|---|---|
| Ford lethal for foot (depth ≥ 1,000 mm, or ≥ 600 mm·m/s at ≥ 300 mm depth) | from about 2:40 (tick 3,200 ± 400), for at least 60 s |
| Attacker | reroutes, or its first regiment arrives at the far bank ≥ 60 s later than with no rain |
| Moat | a 1,500 mm channel across the valley floor below the rain holds ≥ 1,000 mm of water by the time the column reaches it |
| Rutted road | on a wet `earth` road, the 5th regiment through moves at ≤ 600‰ while the first moved at ≥ 800‰ |
| Never walk in | no soldier enters a cell that is lethal for its class under its own steering (fuzz) |

Rough check of the numbers:

- Rain on the river itself adds about 10 m³/s during the rain (3,200 m² at 3 mm/s), doubling base flow.
- Land runoff (about 2,400 m³) arrives over the next minute or two.
- A flood wave travels at about √(g·h) ≈ 3–4 m/s in 1.5 m of water.

So rain falling from 1:00 to 2:00 should crest at a ford 300 m downstream between about 2:20 and 3:00, and the river should drop again by about 3:30–4:00. If the crest falls short, raise the rain rate toward 5,000 µm/s before touching anything else.

---

## 14. Open questions that block numbers, and what's deferred

- **Unit masses, armour and per-type swept modifiers** (S2). They decide `churnWeight` and per-type thresholds; M3 uses class values.
- **Prices in energy** for Rain and each piece, refunds on fizzle, how many pieces a level allows (S5; campaign levels).
- **Battle length** (plan section 10: about 3 min, cap 15 min, needed by M4). It sizes the spring cap (D4) and the drying rate.
- **Temperature, heat, cold, ice, snow** (S3). Reserved interaction rows; rain to snow below freezing.
- **Does rain drift with the wind?** (content roadmap open question, M6).
- **Brief open question, "How immediate may a harmful consequence be?"** A flood from upstream rain takes 1–2 minutes, so this spec doesn't press on it.
- **Teeter pressure thresholds and crowd pressure** (steering, S2). Only the 1 s teeter time is proposed here.
- **Pond, swamp and firm road as placeable pieces** (D6). The `road` soil exists for map authors now.
- **The living ford** (brainstorm #7: traffic scours a ford deeper). Deferred; it would need bed erosion.

---

## 15. Files

- This spec: `docs/design/spec-s1-water.md`
- Draft rules data, to wire into `data/rules/` (with schemas in `data/schema/`) when M3 builds the loader for them:
  - `docs/design/s1-data/powers.json`: Rain
  - `docs/design/s1-data/terrain.json`: channel, ridge (plus hollow), spring, boulder
  - `docs/design/s1-data/fields.json`: water, wetness, mud, soils, movement in water and mud, swept, cause tags, interaction rows
- Suggested additions to existing data (for the implementer, not done here):
  - `data/rules/hazards.json`: `teeterTicks: 20`
  - map schema: `water.sources`, `water.sinks`, soil and initial water/flux/wetness layers
