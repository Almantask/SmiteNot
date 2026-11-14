# Spec S2: armies, load, launches and the first specialists

Written 2026-11-07, decisions settled the same day, for milestone M5 ("the signature moments work: crowds breaking bridges and ice, stampedes, charges, launches and the landing preview"). Implementation plan section 9 sets the scope (the S2 row and M5's build list); `docs/milestones/STATUS.md` lists the M5 checklist and exit criteria this spec is calibrated against (section 17). Draft rules data: `docs/design/s2-data/*.json` (section 19). These files are not in `data/rules/` yet. The implementer wires them in with the items that read them, because the loader rejects unknown files.

The brief (`docs/design/game-brief.md`) wins over this spec. Every number carries one of three labels, as in S1:

- **[Brief, date]**: settled in the brief. Not up for change here.
- **[Docs]**: taken from another design doc (implementation plan, tech notes, power palette, unit spec, unit brainstorm, content roadmap, idea bank, S1). Recommendations, not decisions.
- **[Proposal]**: a working default invented for this spec, with the reason for it. The user should confirm or change these. The data files mark them `"status": "placeholder"`.

Units in data, so every number is an integer: mm, mm/s, mm/s², kg, ticks (20 per second), permille (‰), centi-degrees (cd, 36,000 to a turn), health points, morale on 0–1,000, kg/m² for strength. S1's units (µm, L/s, wetness and mud on 0–65,535) are unchanged.

This spec covers only what M5 needs. Prices, fire spread, temperature, cold, cast wind with shelter, towers and garrisons, the remaining specialists and the real regiment AI belong to S3, S4 and S5. They are listed in section 18.

---

## Decisions (settled 2026-11-07)

The user accepted every recommendation below on 2026-11-07; they are recorded in the brief ("Armies, load, launches and the first specialists (spec pass S2)"). The proposals elsewhere in this spec are still working numbers, tuned against the M5 tests (section 17), not decisions.

| # | Question | Options | Decided (the recommendation) |
|---|---|---|---|
| D1 | **Stance at deployment** (brief open question; plan section 10 default: a data flag, off). The brief's wording offers behavioural stances (hold, skirmish, counterattack); the plan and STATUS frame it as open or close order. | (a) none; (b) one order flag per regiment, close or open, set in the army layer; (c) behavioural stances | **(b), flag on, close order by default.** Open order doubles file and rank spacing, so it quarters a regiment's load per square metre (the thin file that crosses ice), halves the arrows that hit it, and lengthens the column (slower over a crossing, more exposed to a stampede across it); close order is the only order that braces against a charge. It is a geometry change, so every effect comes from systems that already exist, and the brief already leans on it ("open order against thin ice", 2026-10-11 attack mode). Behavioural stances need the regiment AI and its openings (M7); leave them for S4. |
| D2 | **Campaign win condition** (brief open question; plan section 10: both variants as data). | (a) survival only, as in PvP; (b) protect an objective; (c) the level picks either | **(c), survival by default.** Survival: the defender wins if any defending regiment isn't wiped at sunset, or the attacking army routs; it loses when every defending regiment is wiped. Objective: the same, plus a loss if the attacker holds the level's objective area (at least 50 of its soldiers inside) for 30 s without a break. Survival matches PvP and attack mode, so the campaign trains them; objectives give level designers villages, bridges and relics to defend (IB 14, IB 44). Numbers in section 7.5. |
| D3 | **Do routed regiments rally?** The brief counts routed units still on the field as alive (PvP working assumption) but says nothing about recovery. | (a) never: a rout runs to the map edge; (b) once, when safe; (c) any number of times | **(b).** A routed regiment that has no enemy within 80 m for 20 s re-forms once at reduced morale; a second rout is final and runs off the map (wiped). One rally keeps "where they run" a plan question and gives cavalry a reason to ride routers down (2026-10-09, "the attacker needs a breach and a hunt"), without regiments bouncing between states. |
| D4 | **Siege crews.** The plan says "siege engines (wheeled movement, crews, firing stones)". | (a) the crew is a number on the engine; (b) crew are real foot soldiers in the engine's regiment | **(b): 8 crew per engine, foot soldiers that walk beside it.** Crews can then be swept, launched, shot and drowned like anyone else, which is what makes a stalled siege train vulnerable (Wheel-Breakers, M7) and gives the death gags a crew to throw. An engine moves at full speed with 4 or more crew beside it, crawls with 1–3 and is abandoned with none. A siege regiment is 1–4 engines. Cost: 8 more soldiers per engine. |
| D5 | **Who picks where a specialist works?** The brief: units act "only through fixed regiment rules bought in the army layer". | (a) every specialist acts on a fixed trigger read from the route; (b) the player draws the work for all of them; (c) the player draws the Spademen's line; everyone else triggers | **(c).** Spademen work inside their own side's zone and buffer, before the enemy arrives, so drawing the ditch line during deployment is planning, not battle input, and it is the defender's main tool inside the buffer. Every other specialist answers a setup it can't see in advance (hidden in PvP, unknown to the campaign attacker), so a fixed trigger on the route is the only rule that works in every mode; the preview shows each trigger as a marker on the regiment's lane. |
| D6 | **Animals in hazards** (brief open question: do herds die like soldiers, or paddle off unharmed?). M5 builds herds. | (a) animals die like soldiers; (b) animals never die | **(b).** Swept animals drift and paddle out with no drowning timer; they are never launched, crushed or burned. It keeps every stampede gag about soldiers (playful tone, 2026-10-09), keeps a herd on the map as a piece the defender paid for, and spares a whole set of animal death gags. |
| D7 | **Cavalry charging into deep mud** (brief open question). | (a) mud only slows them (S1 speeds); (b) a charging rider who hits deep mud or knee-deep water stops dead and is thrown, stunned | **(b), as a stop and stun, not a launch.** The rider is stunned 3 s where he stops and his regiment's charge ends; the presentation throws the rider over the horse's head. No ballistic body, because horse and rider are one simulated unit. Rain becomes the cavalry counter in one funny beat, as the palette asks. |
| D8 | **Slipping on ice** (brief open question). M5 has fixed ice. | (a) slip and spin rule now; (b) no charge starts or continues on ice, and slipping waits for M6 | **(b).** Ice in M5 is a load surface; a charge ends at the ice edge (riders slow to a walk). The ice rink staple needs skid physics and stun chains that are worth their own pass (S3, with real ice). |
| D9 | **Wind in M5.** Design gate B needs "rain, lightning and wind", and arrows read wind (2026-10-08), but prevailing wind, wind reach past the cast area and crosswinds are open in the brief (plan section 10, needed by M6). | (a) no wind until S3; (b) a prevailing wind per map only; (c) a prevailing wind per map plus a Wind shift v0 power with a plume | **(c), with the plan's working defaults:** each map sets a prevailing wind (calm by default); Wind shift v0 is a circle whose plume blows on for 150 m past its edge; only the along-shot part of the wind changes arrow range. In M5 wind moves only arrows (rain drift, smoke and fire wait for S3). This answers three brief open questions provisionally; S3 revisits them with shelter and fire. |
| D10 | **How Lightning v0 is aimed.** Palette: "lightning that fells a tree across a path"; the brief's desktop pillar favours shaped casting (drag a tree's fall). | (a) the tree falls in a seeded or wind-set direction; (b) the player clicks the strike point and drags the fall direction | **(b).** The strike hits the tallest tree within 8 m of the point and the tree falls the way the player dragged. For the range rule the cast's area is a fixed 30 m circle around the point, which covers the strike, any tree's fall and the fire patch, so a tree can never land on troops (palette). |
| D11 | **Fire in M5.** The plan says Lightning v0 "ignites dry fuel minimally"; fuel, spread and burning are S3. | (a) no ignition until S3; (b) a static burning patch that doesn't spread | **(b).** A strike in dry fuel lights the fuel cells within 6 m for 30 s. They are lethal and closed (soldiers never walk in; a launch or push into them kills, cause `burned`) and they are the fear source that stampedes herds. Wet fuel (rain) won't catch, so rain counters the stampede from the first playtest. No spread, no fire front, no burning timer until S3. |
| D12 | **How much does Rot weaken a bridge?** The palette: "fails under heavy load"; M5's exit test: "a rotted bridge fails only under the bulk of the army". | a fixed fraction, applied at once or over time | **To 500‰ of the segment's strength, over 30 s after a 10 s wind-up**, permanent for the battle. At half strength a marching regiment crosses a rotted wooden bridge at about half load (no warning), while the bunched bulk behind it breaks it (section 8). Gradual loss shows as greying planks, the warning; Engineers repair it (section 14.7). |
| D13 | **Which new pieces does the player place in M5?** The roadmap's first set lists trees, wooden and stone bridges, hedgerows, drystone walls, herds, herd pens and level-set frozen ponds. | | **Player pieces: trees (grove or row), hedgerow, drystone wall, wooden bridge (and a footbridge variant), herd, herd pen. Map-authored only: stone bridges and frozen ponds.** Stone bridges can't fail in M5 (no tremor or scour yet), so a placeable one would be an unanswerable road; fixed ice belongs to the level (roadmap: "level-set frozen ponds"). Removing a bridge waits for S3. |
| D14 | **Battle-made blockers without Engineers.** The brief (2026-10-09, 2026-10-11): they must "clear on their own or be clearable"; Engineers make clearing "fast rather than possible". A felled tree is a battle-made blocker. | (a) felled trees rot away after a time; (b) any foot regiment can clear one slowly | **(b): any foot regiment clears a felled tree in 120 s (Engineers: 30 s), but only when it closes the last route of its army for some class.** Otherwise the army goes round, which is the redirect the player paid for. Clearing by line infantry is a regiment action under a fixed rule, not a specialist world action, so no charges. |
| D15 | **Small specialist regiments and the range rule.** Drovers (20) and Crossing Marshals (24) are under the plan's 25-soldier line (section 10, an M7 default: such regiments don't block casts). | (a) they block casts like any regiment; (b) regiments under 25 soldiers don't block casts, from M5 | **(b), behind a data flag.** Otherwise a 20-man drover team shields a flank from every cast, which the brainstorm warns about ("a cheap shield against the rival god's casts"). They still count for wiping and holding. |

Conflicts between design docs, resolved in this spec without a decision:

- **IB 57 (one language of strain)** has attacker regiments read the late strain stage ("some balk and some push on"). The brief (2026-10-09) parks that half: regiments never heed warnings. The strain stages here are for the player only.
- **The plan's S2 row** names the outputs `roster.md`, `units.json`, `specialists.json` and `projectiles.json`. This spec is `spec-s2-armies.md`, and its unit data extends the existing `unit-types.json` instead of a new `units.json`, so the loader and the schema keep one file per kind.
- **The plan's S2 row** asks for specialists' prices; prices are S5's job (economy). Section 14.1 gives the brainstorm's figures as [Docs] for orientation only, and the cost-parity rule S5 must meet.
- **The brainstorm** prices Crossing Marshals at 24 soldiers and Drovers at 20 drovers plus 20 dogs. Dogs are presentation only; the simulation counts the drovers.

---

## 1. Units, grid facts and what exists

From the code (STATUS M1–M4) and plan sections 3.3, 3.4 and 8.

| Item | Value | Source |
|---|---|---|
| Tick | 20 per second (50 ms) | [Docs] plan 3.4 |
| Field cell; field step | 1,500 mm; every 4 ticks on active tiles | [Docs] plan 8; S1 |
| Tick order | plan events → fields → structures and load → regiment AI (4 Hz) → navigation → movement → ballistics and impacts → combat → hazards → morale → bookkeeping | [Docs] plan 3.4 |
| Positions and velocities | Fix (16 fractional bits), shown here in mm and mm/s | code |
| Crowd pressure today | the summed overlap of a soldier's body with its neighbours, in mm; blocked soldiers creep only while it is under 250 mm; the separation push is capped at a quarter cell (375 mm) and one body radius per tick | code (MovementSystem) |
| Teetering today | pressure ≥ `teeterPressureMm` (250, placeholder) toward water that sweeps the class: teeter 20 ticks, then over | code; S1 section 4 |
| Lethal edges today | swept water (pushes); cliff tops and the map edge in the distance-to-lethal field (lethal drop 4,000 mm, placeholder) | code |
| Regiments | 50–200 soldiers for every type, officer in slot 0, formation files and spacing from `unit-types.json` | code (placeholders) |
| Health and morale | 100 and 100, unused | code (placeholders) |

---

## 2. The base roster

### 2.1 The four types, plus the engine crew

The brief names the four base types (pillar 3). Silhouettes come from the unit spec. Numbers [Proposal] unless marked.

| | infantry | ranged | cavalry | siege (engine) | crew |
|---|---|---|---|---|---|
| Movement class | foot | foot | horse | wheeled | foot |
| March speed (mm/s) | 1,500 | 1,500 | 3,500 | 800 | 1,500 (walks with its engine) |
| Run speed (rout, evacuation) | 3,000 | 3,200 | 5,000 | — | 3,000 |
| Charge speed | — | — | 7,000 | — | — |
| Body radius (mm) | 350 | 350 | 700 | 1,500 | 350 |
| Health | 100 | 70 | 150 | 400 (structure) | 80 |
| Mass for load (kg) | 90 | 80 | 600 | 3,000 | 85 |
| Morale shock taken (‰) | 1,000 | 1,200 | 900 | — (crew decides) | 1,200 |
| Regiment size | 50–200 | 50–200 | 30–120 | 1–4 engines | 8 per engine (D4) |
| Formation files; file × rank spacing (mm) | 20; 1,000 × 1,200 | 25; 1,100 × 1,400 | 10; 1,800 × 3,000 | 4; 6,000 × 9,000 | around the engine, within 3 m |
| `churnWeight` (S1) | 1 | 1 | 6 | 25 | 1 |
| Swept: `sweptDepthSpeed` ‰ of class | 900 | 1,000 | 1,000 | 1,000 | 1,000 |

Reasons:

- **Speeds** keep the M2 placeholders (march 1.5 m/s, trot 3.5 m/s, an ox-drawn engine 0.8 m/s) [Docs: unit-types v0], which the M3 and M4 calibrations already used. Run speeds are about twice the march, and the charge is a gallop at 25 km/h: fast enough to read as a charge at strategic zoom, slow enough that a charge across 80 m takes about 13 s and shows in the preview.
- **Health** is set so a soldier survives one launch landing on open ground (10) plus one trample or charge hit (40–60), and dies to a second (shape first, kill second). Ranged are lighter and die faster; cavalry (horse and rider as one body) take more.
- **Masses** are a soldier with kit (90 kg), an archer (80), a horse and rider (600), and an engine (3 t). They set the load (section 8). The ratio to infantry (1 : 6.7 : 33) is close enough to S1's churn weights (1 : 6 : 25) that **S1's `churnWeight` stays as it is** [Proposal]; this answers S1 section 14's question with no change to the M3 goldens.
- **Swept modifier.** The tech notes say heavy armour loses footing sooner [Docs: tech notes 5]. Infantry carry shields and mail, so their depth × speed threshold is 900‰ of the foot class's (540 instead of 600 mm·m/s). Horse and wheeled already have lower class thresholds (S1). Depth thresholds stay at class values.
- **Regiment sizes.** Infantry and ranged keep the tech notes' 50–200 [Docs]. Cavalry regiments are smaller (30–120): a 200-rider block is 60 m deep and too heavy for any bridge. Siege is counted in engines (D4).
- **Morale shock** scales every morale loss (section 7): archers and crews are shakier than line infantry; cavalry steadier.

**Siege engine (a stone thrower, the roadmap's first engine).** One wheeled body per engine. It moves at full speed with ≥ 4 crew within 6 m, at 300‰ with 1–3, and stops (abandoned, a hard blocker for every class, like a boulder) with none. Arrows don't hurt engines; melee hurts them at 250‰ (crews defend them). An engine at 0 structure becomes a wreck stamped into navigation like a felled tree (section 13.2). Crew are the targets that matter. Stones in section 6.4.

**Plan format.** A siege regiment's `soldiers` is its engine count (1–4); the loader adds 8 crew per engine. Same for the Pontoon Train's boats (section 14.5).

### 2.2 Open and close order (D1)

| Item | Close order | Open order | Label |
|---|---|---|---|
| File and rank spacing | as in 2.1 | × 2,000‰ | [Proposal] Doubles both, so a quarter of the soldiers per square metre |
| Load per m² of a marching regiment (infantry) | about 75 kg/m² | about 19 kg/m² | follows |
| Arrows that hit (section 6.2) | 1,000‰ | 500‰ | [Proposal] Fewer bodies under each arrow; set explicitly because volleys are resolved per regiment |
| Braces against a charge (section 10) | yes, when standing and facing it | no | [Proposal] |
| Changing order | only at deployment, and on a span metered by Crossing Marshals (section 14.6) | | [Proposal] |
| Default | close | | [Proposal] |

Everything else (column length, melee frontage, stampede exposure) follows from the spacing.

---

## 3. Movement classes: slopes, ice and route clearances

S1's water and mud tables are settled and unchanged. S2 replaces the slope placeholders.

### 3.1 Slope limits and speeds

Slope is the steepest 4-neighbour step, rise over run, in ‰ (1,000 = 45°), as the code computes it.

| | foot | horse | wheeled | Label and reason |
|---|---|---|---|---|
| Steepest slope it can climb (closed above) | 1,000 | 700 | 250 | [Proposal] Keeps the M2 placeholders, because S1's settled D8 depends on them: a 2 m channel's 667‰ side step is crossable by foot and horse, a ridge's 400‰ side only by foot and horse, and wheeled crosses channels up to 750 mm deep (a 250‰ step). The Spademen's ditch (section 14.3) is cut to 833‰ so it stops horses and engines but not infantry. |
| Speed on slope (‰), linear between points | 0→1,000, 200→900, 500→700, 1,000→450 | 0→1,000, 200→850, 400→650, 700→400 | 0→1,000, 100→800, 250→450 | [Proposal] Steep ground halves speed at each class's limit; engines feel slope first (tech notes 3: "siege cares about slope far more than infantry"). |
| Flow-field cost | 1,000,000 / (slope speed × water speed × mud speed / 1,000,000) | | | [Proposal] One cost rule for all three grounds: cost follows travel time, like S1's water and mud costs. |
| Linear fallback (if the code keeps `slopeCost`) | `slopeCost` 122 | 214 | 489 | Derived: the values that give the same cost at each class's steepest slope |

### 3.2 Ice (fixed ice in M5)

| | foot | horse | wheeled | Label |
|---|---|---|---|---|
| Speed on intact ice (‰) | 850 | 700 | 700 | [Proposal] Careful steps; arms out (unit spec) |
| Charges on ice | — | end at the ice edge | — | [Proposal, D8] |
| Ice is a deck | units on intact ice stand on it, not in the water below | | | [Docs: tech notes 5] |

### 3.3 Route clearances (the reachability check)

`routes.json` asks S2 to revisit the siege clearance (M2 notes: a 13.5 m corridor is "wider than an engine needs").

| Item | Value | Label and reason |
|---|---|---|
| Coarse grid | 3 field cells (4.5 m), unchanged | [Docs] M2 |
| `clearanceCells` foot / horse / wheeled | 1 / 1 / **1** (was 2) | [Proposal] An engine is 3 m wide. A coarse cell is open only when all nine field cells are passable, so any open 4.5 m coarse cell already fits it. The brief's "wide units such as siege get wide enough gaps" (2026-10-09) holds; narrow gates that sit badly on the grid stop being refused. |
| Cap | 200%, unchanged | [Brief, 2026-10-09] |

---

## 4. Steering and crowd pressure

S1 left the pressure numbers to S2. The brief settles the rules: soldiers never walk into a lethal cell; crowd pressure from behind can overpower the push back from the edge, so density at an edge is what kills; a pressed soldier teeters first [Brief, 2026-10-09; 2026-10-11].

### 4.1 Pressure and pushes, weighted by mass

| Item | Value | Label and reason |
|---|---|---|
| Pressure | the sum over overlapping neighbours of overlap × 2 × m_other / (m_self + m_other), in mm | [Proposal] Two soldiers of equal mass feel exactly today's pressure. A soldier against a horse feels 1.74× the overlap and the horse 0.26×; an engine barely feels a crowd at all (0.06×), so crowds can't push engines over edges, and a cavalry block shoves infantry hard. |
| Separation push share | weighted the same way | [Proposal] Same reason |
| Push cap per tick | min(body radius, 375 mm), unchanged | code |
| Creep limit (blocked soldiers creep only below it) | 250 mm, unchanged | code |
| Teeter threshold (pressure toward a lethal edge) | foot 250 mm, horse 350 mm, wheeled never | [Proposal] Keeps the M3 value, which produced the moat scenario's 104 of 600 pushed in, each after teetering. Horses are bigger bodies and need more to go over. |
| Teeter time | 20 ticks (1 s), unchanged | S1 settled 2026-11-03 |
| Lethal edges pushes can cross in M5 | swept water (M3); cliff tops (drop ≥ 4,000 mm to a 4-neighbour); burning cells (fire v0, section 12.2); open water where a bridge segment or ice patch failed | [Brief, 2026-10-09; 2026-10-11] |
| Pushed off a cliff | killed if the drop is ≥ 4,000 mm (cause `fell`, linked to `pushed by the crowd`); stunned 80 ticks and 40 damage for 2,000–3,999 mm | [Proposal] Same as a launch landing (section 9.3) |
| Herds | the same rules; animals weigh 500 kg | [Brief, 2026-10-09: "herds follow the same rule"] |

### 4.2 Calibration targets for steering

The load system reads crowd density, so the density steering produces is a design number. M2 measured about 4 soldiers a second through a 20 m bridge; real crowds manage about 1.3 per metre per second [Docs: STATUS M2 notes].

| Target | Value | Label and reason |
|---|---|---|
| Marching throughput through a bottleneck | ≥ 1.0 soldier per metre of width per second (20 a second over a 20 m bridge) | [Proposal] Below this, every bridge is a jam by default and nothing about the god's setups matters. |
| Peak density in a jam (front stopped, rear pushing) | 2.0–3.0 soldiers/m² | [Proposal] What a packed crowd reaches. Section 8's strengths assume it. |
| Close-order march density | about 0.8 soldiers/m² (infantry) | follows from 2.1's spacing |
| Routing regiments | spacing × 700‰ (panic packs them tighter), run speed | [Brief, 2026-10-09: "panic packs them tighter"]; number [Proposal] |

The sim-engineer meets these by tuning steering constants (brake distance, car-following, slot gains), not rules.

---

## 5. Melee

Melee resolves per soldier through the spatial hash, in the combat step [Docs: tech notes 2, plan M5]. Hits and damage use the seeded stream (combat, soldier, tick), so they are deterministic.

| | infantry | ranged | cavalry | crew and specialists | Label |
|---|---|---|---|---|---|
| Damage per hit | 25 | 12 | 30 | 10 | [Proposal] |
| Hit chance | 550‰ | 450‰ | 600‰ | 400‰ | [Proposal] |
| Interval between swings | 30 ticks (1.5 s), each soldier's phase seeded | | | | [Proposal] |
| Reach beyond body contact | 600 mm | 400 mm | 900 mm | 400 mm | [Proposal] Spear and sword; lance and sabre |
| Melee damage taken | 900‰ (shield) | 1,000‰ | 800‰ | 1,000‰ | [Proposal] |

- **Engagement.** A soldier swings at the nearest enemy within contact + reach (ties: lower entity id). Soldiers who are stunned, launched, swept or teetering don't swing. A soldier engaged in melee moves at 200‰ of march speed.
- **Rate check.** Twenty infantry in contact on each side: 20 × 0.55 × 25 × 0.9 / 1.5 s / 100 ≈ 1.6 deaths a second per side. A 200-strong regiment loses 10% in about 12 s, which the morale shock then turns into a rout within a minute (section 7). Battles are about 3 minutes (plan section 10), so a melee decides within one.
- **Engines** take melee at 250‰ and no arrows.
- **Regiment targeting (working default until the regiment AI, M7) [Proposal]:** attackers march to the defender zone (as now) and turn on any enemy regiment whose outline comes within 30 m; defenders hold their deployment and engage within 40 m; ranged regiments stop and shoot when a target is in range and fall back 30 m (once per 20 s) when enemy melee comes within 25 m; cavalry charge by section 10's rule. These are fixed and readable; M7 replaces them.
- **Death cause:** `combat`, source the striking regiment [Brief, 2026-10-11].

---

## 6. Volleys and projectiles

Resolved per regiment volley, never per arrow [Docs: tech notes 5, "Arrows and wind"]. Arrows on screen are GPU particles on the computed arc.

### 6.1 Projectile table

| | light arrow | crossbow bolt | siege stone | Label |
|---|---|---|---|---|
| Used by in M5 | ranged | (reserved: Pavise Crossbowmen later) | siege engine | |
| Range on flat ground, calm (mm) | 160,000 | 120,000 | 250,000 (minimum 60,000) | [Proposal] Archers outreach the 100 m no-cast distance, so a ranged regiment's reach always shows past the no-cast ring |
| Flight time at full range | 40 ticks (2 s), linear with distance | 30 ticks | 80 ticks (4 s) | [Proposal] |
| Wind sensitivity (‰ of range per m/s of along-shot wind) | 30 | 10 | 3 | [Docs: palette, "light arrows hit hardest, bolts less, siege stones barely"]; numbers [Proposal] |
| Range clamp (‰ of calm range) | 700–1,250 | 850–1,100 | 950–1,050 | [Docs: palette, "cap the range loss so archers never become useless"]; numbers [Proposal] |
| Height gain (mm of range per mm the shooter stands above the target) | 4, capped at ±40,000 | 3 | 2 | [Proposal] A 3 m ridge adds 12 m; a 10 m tower (M6) adds 40 m. Exaggerated, as everything here is |
| Hit chance at ≤ 30% of range / at full range (linear between) | 300‰ / 100‰ | 400‰ / 150‰ | (stone: section 6.4) | [Proposal] |
| Damage | 35 | 50 | (section 6.4) | [Proposal] |
| Reload | 160 ticks (8 s) per volley | 240 | 300 per engine | [Proposal] |
| Ammunition | 20 volleys (160 s of shooting) | 20 | 30 stones | [Proposal] Lasts most of a 3-minute battle, so ammo rarely decides one; a level can still starve a long battle |

### 6.2 Resolving a volley

[Proposal] unless marked:

1. Every 160 ticks, if the regiment has a target whose nearest outline cell is within its current range (6.3), every living archer that isn't stunned, swept, launched or in melee shoots.
2. **Aim point** = the target regiment's centroid + its velocity × flight time.
3. **Hits** = shooters × hit chance × target order factor (close 1,000‰, open 500‰) / 1,000,000, with the remainder rolled on the seeded stream.
4. At the landing tick, the hits go to soldiers of the target regiment within 12 m of the aim point, picked by the seeded stream; hits with nobody there miss. A regiment that moves fast enough dodges part of a volley, which is readable.
5. Damage = 35 × the target's arrow damage taken: infantry 600‰ (shields), ranged 1,000‰, cavalry 800‰, crew and specialists 1,000‰, engines 0.
6. Death cause `combat`, source the shooting regiment.

Rough check: 200 archers at mid range (about 200‰) against close-order infantry: 40 hits × 21 damage ≈ 8 deaths per volley, one a second. Open order halves it.

### 6.3 Wind along the shot and height

- **Along-shot wind** [Brief, 2026-10-08: wind affects arrows; Docs: tech notes, sample shooter, midpoint and target]: the mean, over the shooter's, midpoint and target cells, of the wind vector's component along the shot, in mm/s (positive = tailwind). Only the along-shot part counts (plan section 10 default; D9).
- **Range** = calm range × clamp(1,000 + along × sensitivity / 1,000, clamp) / 1,000 + height gain × (shooter ground − target ground), capped. Ground heights from the heightfield; a regiment's height is its officer's cell.
- **Preview:** each ranged regiment's reach is drawn from the same numbers, stretched downwind [Docs: tech notes].

### 6.4 Siege stones

| Item | Value | Label and reason |
|---|---|---|
| Fires when | ≥ 4 crew within 6 m, a target regiment's outline between 60 and 250 m (after wind and height), every 300 ticks per engine | [Proposal] |
| Landing point | the aim point (6.2) plus a seeded scatter: radius 2,000 mm at 60 m rising linearly to 8,000 mm at full range | [Proposal] An engine hits regiments, not soldiers |
| Crush | soldiers within 1,000 mm of the landing point die (cause `combat`, source the engine's regiment; gag `crushed`) | [Brief, 2026-10-11: siege stones are combat]; radius [Proposal] |
| Launch | soldiers 1,000–4,000 mm away are launched away from the landing point (siege-stone source, section 9.2) | [Brief, 2026-10-09: siege stones launch]; radii [Proposal] |
| The stone after landing | gone (rolling stones wait for M6's rolling bodies) | [Proposal] |
| Targets among built pieces | M6 | [Docs] plan M5 |

---

## 7. Morale, routing, wiped and the win condition

Morale is per regiment, with contagion between neighbours, driven by casualty events rather than per-death checks [Docs: tech notes 4 and 5]. Updated in the morale step at 4 Hz. Morale 0–1,000; every loss below is multiplied by the regiment's type shock (2.1).

### 7.1 Morale changes [Proposal]

| Event | Change |
|---|---|
| Start | 1,000 |
| Own losses in an event | −(lost × 2,000 / starting size): losing 10% at once costs 200. Launched or swept soldiers who aren't dead count as half a loss until they die or recover |
| A friendly regiment's casualty event within 60 m | −(count × 300 / own starting size), at most 150 per event; half that at 60–120 m |
| A friendly regiment routing within 60 m | −30 per second (contagion) |
| A friendly regiment wiped within 60 m | −100 once |
| An enemy regiment routing within 60 m | +10 per second |
| Recovery | +4 per second after 10 s with no loss, up to a ceiling of 1,000 − (losses so far, ‰ of starting size) |

Reasons: a regiment that loses 10% at once and sees a neighbour break falls from 1,000 to about 500 in seconds; a second shock routs it. Mass launches shake regiments even when few die (tech notes 5), through the half-loss rule. Losses lower the ceiling, so a mauled regiment never fully recovers.

### 7.2 Routing and rallying (D3)

| Item | Value | Label |
|---|---|---|
| Routs below | 250 | [Proposal] |
| Routed regiment | runs to its own retreat edge (the nearest map edge, as the reachability check uses), at run speed, packed tighter (4.2); still never walks into lethal cells | [Brief, 2026-10-09]; behaviour [Proposal] |
| Rally (D3) | once: no enemy outline within 80 m for 400 ticks (20 s) and at least 60 m from any lethal cell → re-forms at morale 450 | [Proposal] |
| Second rout | final: runs off the map | [Proposal] |
| Off the map | the soldier is fled: counts as wiped [Brief, 2026-10-09] | |

### 7.3 Wiped

| Item | Value | Label |
|---|---|---|
| A regiment is wiped when | its soldiers on the map number below 100‰ of its starting size (alive × 1,000 < start × 100) | [Brief, 2026-10-09: "below about 10%"]; 100‰ exact [Proposal] |
| Fled soldiers | count as lost | [Brief, 2026-10-09] |
| Routed regiments still on the field | alive [Brief, PvP working assumption] | |
| The remnant of a wiped regiment | routs permanently and runs off the map; it no longer counts or blocks casts | [Proposal] So one soldier hiding in a corner never holds a round (brief) |
| Specialist charges | lost when the regiment is wiped or routs [Brief, 2026-10-10] | |

### 7.4 The attacking army routs

The brief's PvP working assumption: the defender also holds "if the attacking army routs". [Proposal]: the attacking army has routed when at least 700‰ of its starting soldiers are dead, fled or in routed or wiped regiments at the same moment. The battle then ends as a defender hold.

### 7.5 Win and loss (D2)

| Variant | Defender wins | Defender loses | Label |
|---|---|---|---|
| `survival` (default) | at sunset, any defending regiment isn't wiped; or the attacking army routs (7.4) | every defending regiment is wiped (the battle ends at once) | [Brief, 2026-10-09 PvP rule, applied to the campaign]; D2 |
| `objective` | as survival | as survival, or the attacker holds the objective area: ≥ 50 attacking soldiers inside it for 600 consecutive ticks (30 s) | [Proposal, D2] |
| Sunset | set per level; working default 3,600 ticks (3 min), hard cap 18,000 (15 min) | | [Docs] plan section 10 |

The objective area is a polygon of up to 8 points in the level file. The preview shows the hold timer filling while attackers stand in it. Both variants live in level data; the plan schema doesn't need to change.

---

## 8. Load: bridges, ice and the strain language

The brief settles the rules: ice holds a load set by its thickness; too many units on a stretch break it; heavy units count for more; a thin file crosses where a dense column breaks [Brief, 2026-10-08]. Bridges collapse under too much weight [Brief, 2026-10-08, pillar 1]. Warnings are for the player only [Brief, 2026-10-09].

### 8.1 Structures and segments

A **structure layer** marks deck cells: segment id, deck height and material. Units on a deck cell stand on the deck (at deck height) and are not in the water below while the deck holds [Docs: tech notes 5].

| Kind | Placed by | Segment | Width | Strength (kg per m² of deck) | Rots | Label and reason |
|---|---|---|---|---|---|---|
| Footbridge (wood) | player (13.4) | 2 cells (3 m) along the span | 1,500–3,000 mm | 150 | yes | [Proposal] Foot only. Breaks under a dense crowd (≥ 1.7/m²) |
| Wooden bridge | player (13.4) | 2 cells along | 3,000–6,000 mm | 300 | yes | [Proposal] A packed jam (2.0–3.0/m², 180–270 kg/m²) takes it to 60–90%: it creaks and sags but holds an infantry jam; a jam with cavalry or an engine on it fails. A rotted one (150) fails under any jam. |
| Stone bridge | map | 2 cells along | map-set | 1,200 | no | [Proposal] Doesn't fail in M5: only a tremor or scour can (S3, M6) [Docs: palette] |
| Pontoon deck | Pontoon Train (14.5) | 2 cells along, 3 cells wide | 4,500 mm | 250 afloat; × 400‰ grounded (water under it < 300 mm), linear between 300 and 600 mm | yes | [Docs: brainstorm #33, "a drain grounds the boats, cutting its load limit"]; numbers [Proposal] |
| Trestle | Engineers (14.7) | 2 cells along, 2 cells wide | 3,000 mm | 400 | yes | [Brief, 2026-10-11: "carry a set load like any bridge segment", wood]; 400 decided 2026-11-08 (was 220, which a single 3,000 kg engine broke, against the exit scenario): one engine 83%, two break it |
| Ice patch (fixed) | map: frozen pond | 4 × 4 cells (6 × 6 m), on a grid aligned to the map | the pond | 1 kg/m² per mm of thickness; map thickness 60–250 mm | no | [Docs: tech notes 5, "patches of 4 by 4 cells, strength from thickness"]; numbers [Proposal]. 120 mm ice: marching infantry 62%, marching cavalry 93% (sag), any jam breaks it, a thin file 10–20% |

Strength of a segment = deck area (m²) × kg/m² × its current strength ‰ (1,000 when sound) / 1,000.

### 8.2 Load and failure [Proposal]

| Item | Value | Reason |
|---|---|---|
| Load | the sum of the masses of units whose centre is on the segment's cells: soldiers, engines, boats, animals | [Docs: tech notes 5] |
| Not counted | launched (airborne) and swept units | They aren't standing on it |
| Engines | whole mass on the segment under the engine's centre | Simple; a single engine takes a sound wooden bridge segment (13.5 m²) to 74%, a creak, which is the right drama for the siege train |
| Checked | every field step (4 ticks), in the structures-and-load step | Matches the fields; the load spike of one landing doesn't decide it |
| Fails | load ≥ strength at 2 consecutive checks (0.4 s) | One jostle doesn't break a bridge; a crowd does |
| Neighbours of a failed bridge segment | lose 250‰ of their maximum strength at once (lost support) | "The middle drops" (palette staple 6) and the span weakens outward, but a sound neighbour under a thin load survives, so a collapse never takes the whole bridge by itself |
| Neighbours of a broken ice patch | lose 150‰ | Breaks can spread through a dense column but stop at a thin file (tech notes 5) |
| A broken ice patch | open water at once, for the rest of the battle (no refreezing until S3) | [Docs: tech notes 5] |

### 8.3 Strain stages: one language

| Stage | Load ÷ strength | Label |
|---|---|---|
| (quiet) | < 700‰ | |
| Creak | ≥ 700‰ | [Docs] plan M5 and STATUS (creak 70%, crack 85%, sag 95%); IB 57 |
| Crack | ≥ 850‰ | [Docs] |
| Sag | ≥ 950‰ | [Docs] |
| Fail | ≥ 1,000‰ at 2 checks | [Proposal] 8.2 |
| Stage falls back | when the ratio drops 50‰ below the stage's threshold | [Proposal] Hysteresis, so the UI and shaders don't flicker |

The ratio and the stage are exposed per segment to shaders and UI (strain overlay, M5 item 8). Regiments never read them [Brief, 2026-10-09].

### 8.4 Collapse

| Item | Value | Label |
|---|---|---|
| A failed segment | its deck cells are removed; the ground and water below take over; navigation and the distance-to-lethal field update incrementally | [Docs: tech notes 5] |
| Units on it | launched with the collapse source (9.2), then land on what is below: usually water, so swept (or the 2 m fringe stun) | [Docs] plan M5: "collapse into launches and the swept state" |
| Units on a broken ice patch | swept at once, with the cold-water drowning timer (below) | [Docs: tech notes 5] |
| Cold-water drowning timer (ice only, until S3) | foot 160 ticks, horse 240, wheeled 80 | [Proposal] Shorter than S1's timers (300 / 400 / 120): cold water is the ice trap's sting. S3 owns cold. |
| Climbing out of a hole in the ice | onto shore or onto intact ice, where the soldier's mass is load again | [Proposal] So survivors crowding onto the edge break the next patch, which is the gag |
| Cause chain | `bridge segment failed` (load, the two regiments with the most mass on it, and the latest Rot cast on it if any) → `launched` → `swept` → `drowned` | [Brief, 2026-10-11: every death records cause and source] |
| Water over a deck | if the water surface reaches deck height, the deck is awash and units on it read that depth for wading and sweeping (bridges, trestles; pontoons float) | [Proposal] A flood can sweep a column off a sound bridge, the scour staple's little brother |

---

## 9. Launches

Settled [Brief, 2026-10-09]: exaggerated launches live in the simulation as ballistic bodies with reduced gravity; launch distance and damage are tuned separately, so most launched soldiers survive stunned; landing in water, off a cliff or into fire kills; a landing knocks neighbours down but never launches them; launch direction follows the source's heading with a fixed seeded spread; a shallow fringe of about 2 m along deep water only stuns; launched and stunned soldiers stay in their regiment's outline. [Brief, 2026-10-11]: a landing never kills or launches neighbours.

### 9.1 The body

| Item | Value | Label and reason |
|---|---|---|
| State | position (x, y, z), velocity (x, y, z), spin, source id; one arc step per tick in "ballistics and impacts" | [Docs: tech notes 5, unit spec] |
| Gravity for launched bodies | 4,000 mm/s² (about 0.4 g) | [Brief: reduced gravity]; number [Proposal]. Triples the hang time of a real throw. |
| Spin | seeded per soldier, 36,000–108,000 cd/s about a horizontal axis | [Proposal] In the state, so the playback track redraws the same tumble |
| Landing | the first tick its z falls to or below the ground (or deck, or water surface) under it | [Docs: tech notes 5] |
| Regiment outline (range rule) | counts while launched and while stunned | [Brief, 2026-10-09] |
| Load | not counted while airborne | [Proposal] 8.2 |

### 9.2 Launch sources

Speeds are the launch velocity before the spread. Flight time = 2 × vertical / gravity; distance = horizontal × flight time.

| Source | Horizontal (mm/s) | Vertical (mm/s) | Flight; distance | Heading | Label |
|---|---|---|---|---|---|
| Cavalry charge | 5,000 | 6,000 | 3 s; 15 m | the rider's heading | [Proposal] |
| Stampede | 6,000 | 7,000 | 3.5 s; 21 m | the herd's run heading | [Proposal] The biggest throw: the "stampede express" staple |
| Falling tree (soldiers beside the trunk) | 3,000 | 5,000 | 2.5 s; 7.5 m | perpendicular away from the trunk | [Proposal] Rare: the tree falls 100 m from troops (D10); this is for herds' neighbours and later sources |
| Siege stone | 4,000 | 6,000 | 3 s; 12 m | away from the landing point | [Proposal] |
| Collapse (bridge segment) | 1,000 | 2,000 | 1 s; 1 m | seeded, any direction | [Proposal] A short hop, then the fall into the water below |
| Spread, every source | heading ± 2,500 cd (25°), speed ± 200‰, both seeded per soldier and source event | | | | [Brief, 2026-10-09: "a fixed seeded spread"]; numbers [Proposal] |

The seed for the spread is (source event id, soldier entity id), never contact geometry, so a small edit moves the landing spread smoothly [Brief, 2026-10-09].

### 9.3 Landings by surface

| Lands on | Outcome | Preview colour | Label |
|---|---|---|---|
| Dry ground, mud, road, a deck | 10 damage, stunned 60 ticks (3 s), out of formation; rejoins | stun | [Proposal] |
| Shallow water (not sweeping for the class) | stunned 60 ticks, no damage | stun | [Proposal] |
| Deep water within 2,000 mm of a cell where the class can stand (the fringe) | moved to that nearest standable cell, stunned 60 ticks, no damage | stun | [Brief, 2026-10-09: "a shallow fringe of about 2 m along deep water only stuns"]; snapping [Proposal] |
| Deep or sweeping water beyond the fringe | swept (S1: drift, paddle, drowning timer), no damage | **red** (lethal) | [Brief, 2026-10-09; S1] |
| Intact ice | as ground; the soldier is load again | stun | [Proposal] |
| Ground ≥ 4,000 mm below the ground he was launched from | killed, cause `fell`, linked to the launch | **red** | [Brief: "off a cliff ... kills"]; uses `lethalDropMm` |
| Ground 2,000–3,999 mm lower | 40 damage, stunned 80 ticks | stun | [Proposal] |
| A burning cell (fire v0) | killed, cause `burned`, linked to the launch | **red** | [Brief, 2026-10-09; 2026-10-11] |
| A hard blocker (boulder, wall, abandoned engine) | moved back along the arc to the nearest open cell, 20 damage, stunned 60 ticks | stun | [Proposal] |
| Off the map | clamped to the edge cell, then as ground | stun | [Proposal] Being thrown off the map never counts as fleeing |
| Within body contact of standing soldiers | as above, plus every soldier within 1,000 mm is knocked down: stagger 20 ticks, no damage, never launched; a knocked-down soldier knocks nobody down | | [Brief, 2026-10-09 and 2026-10-11: knock-downs never chain or kill]; numbers [Proposal] |

Launch damage (above) and impact damage (trampling, section 10.3) are separate numbers, as the brief asks: spectacle never quietly becomes lethality.

### 9.4 The landing-spread preview

[Brief, 2026-10-09; Docs: tech notes 10]

- The playback track records each launch (tick, start position, velocity, spin, source event id) and each landing (tick, cell, outcome).
- Per launch source event (one charge contact, one stampede, one collapse, one stone), the preview aggregates landings into a heatmap of 1.5 m cells, coloured stun or red (by water, cliff or fire).
- Drawn on a held key and while a placement is dragged; exact, because it comes from the simulated future.

---

## 10. Charges and trampling

### 10.1 When cavalry charge [Proposal]

| Item | Value |
|---|---|
| Starts when | a cavalry regiment's target regiment's outline is 20–80 m ahead, the straight line to it is open for horse (flow distance ≤ 1,200‰ of the straight line), and no cell on that line has mud ≥ 32,767, water ≥ 300 mm or ice |
| Acceleration | 3,500 mm/s², to the charge speed (7,000 mm/s) |
| A rider counts as charging | while his speed is ≥ 5,000 mm/s |
| Ends | at contact; after 100 m; at the ice edge (D8); when a rider hits deep ground (10.2) |
| Cooldown | 400 ticks (20 s), and the regiment must be at least 40 m from the enemy to charge again |

### 10.2 Deep mud and water stop a charge (D7)

A charging rider whose cell has mud ≥ 49,151 (three quarters of deep) or water ≥ 400 mm stops dead: speed 0, stunned 60 ticks, no damage, cause `thrown in the mud` (presentation: the rider flies over the horse's head). His regiment's charge ends. [Proposal; D7]

### 10.3 Contact: launches and trampling

| Item | Value | Label |
|---|---|---|
| Targets per charging rider | at most 2 enemy soldiers in body contact in front of him (within 4,500 cd of his heading), per charge | [Proposal] A charge breaks the front ranks and scatters them; it can't bowl through a whole block |
| Each target | launched (charge source) with 500‰, otherwise trampled: 40 damage (armour ignored), stunned 40 ticks | [Brief, 2026-10-11: trampled by a charge]; numbers [Proposal] |
| Braced target | infantry in close order, standing (speed < 300 mm/s), regiment facing within 6,000 cd of the charge's reverse: launch chance 250‰, and the rider takes 25 damage per target | [Proposal] The counter loop's anchor (brainstorm), without a new unit |
| After contact | the rider slows to 1,500 mm/s and fights in melee | [Proposal] |
| Morale | one casualty event per hit regiment per charge, launched soldiers as half losses (7.1) | [Docs: tech notes 5] |
| Death cause | `trampled`, source the cavalry regiment | [Brief, 2026-10-11] |
| Never chains | launched soldiers' landings only knock down (9.3) | [Brief] |

---

## 11. Herds and stampedes

Settled: herds are terrain pieces that roam and block (2026-10-08); fire, lightning or another fear source spooks a herd into a stampede that flees away from the fear, steered by terrain, never aimed, and that doesn't pick sides (2026-10-08); herds follow the never-walk-into-hazards rule (2026-10-09); units may herd at walking pace but never start or steer a stampede (2026-10-09).

### 11.1 The herd [Proposal]

| Item | Value | Reason |
|---|---|---|
| Animals per herd | 20–120, default 60; at most 400 animals per map | Tech notes: "tens to a few hundred, capped per map" |
| Animal | radius 700 mm, mass 500 kg, movement class horse | Cattle; heavy on ice |
| Grazing area (its footprint for the 100 m buffer) | a circle of radius 15,000–40,000 mm | [Brief, 2026-10-08: "a herd's grazing area"] |
| Grazing | each animal walks to a seeded waypoint inside the circle every 200 ticks, at 300 mm/s | Tech notes: "grazing waypoints plus seeded random wander" |
| Driven (by drovers) | 1,200 mm/s | Walking pace [Brief] |
| Stampede speed | 6,500 mm/s | A touch slower than a cavalry charge |
| In the cost field | a cell holding an animal costs × 4 for every class, written each tick as congestion (no flow-field rebuild) | [Docs: tech notes 3] |
| Reachability | ignored (herds move) | [Docs: tech notes 3] |
| Hazards | never walks into lethal cells; swept animals drift and paddle with no drowning timer; never launched, crushed or burned (D6) | [Brief, 2026-10-09]; D6 |

### 11.2 Fear and the stampede trigger

Each herd has one fear value, 0–1,000, recomputed every field step as the **largest** of its sources (largest, not summed, so each source reads on its own) [Proposal]:

| Source | Fear | Label |
|---|---|---|
| A fire v0 patch | 1,000 within 50 m of its nearest burning cell, falling linearly to 0 at 120 m | [Proposal] So a fire lit 100 m from the column spooks a herd grazing 40 m from it |
| A lightning strike | 500 within 80 m, decaying 100 per second | [Proposal] Below the threshold: thunder alone makes the herd mill (the warning) but not run, so rain, by stopping the fire, stops the stampede (palette counter web) |
| Drovers | 400 within 30 m of the drover regiment's centroid | [Brief, 2026-10-09: drovers never start a stampede]; capped well under the threshold |
| Threshold | 700 | [Docs] plan M5: "stampedes past a heat threshold" (read as fear, since M5 has no temperature field); number [Proposal] |

When fear first reaches 700 the herd is **agitated**: it mills and raises dust for 60 ticks (3 s), the warning [Docs: palette], then stampedes. The trigger commits: it doesn't cancel if the fear drops during the 3 s.

### 11.3 The run [Proposal]

| Item | Value |
|---|---|
| Heading at the start | directly away from the strongest source's nearest point (the fire patch, the strike) |
| Steering each field step | if the cell 6 m ahead on the heading is closed for horse or lethal, turn 1,500 cd toward the side with the lower cost (left on a tie); where the slope exceeds 100‰ and the fall line is within 4,500 cd of the heading, turn up to 500 cd per second toward it (favouring downhill) |
| Animals | follow the herd heading at stampede speed, with separation; never enter lethal cells |
| Ends | after the herd's centroid has run 300 m or after 800 ticks (40 s) |
| Then | scattered: the animals wander in place at grazing speed, with no grazing circle, and the herd can never stampede again [Docs: palette seed, "one stampede per herd"] |

### 11.4 Impacts

| Item | Value | Label |
|---|---|---|
| A stampeding animal in body contact with a soldier (either side) | the soldier is launched (stampede source) with 600‰, otherwise trampled: 60 damage, stunned 60 ticks | [Brief, 2026-10-11]; numbers [Proposal]. Palette seed: "mostly disruption and morale shock, few deaths" |
| Rate cap | one victim per animal per 10 ticks; after a hit the animal runs at 800‰ for 20 ticks | [Proposal] |
| Engines, boulders, walls | hard blockers: the herd splits around them | [Docs: palette, "braced formations and siege engines split it"] |
| Morale | one casualty event per hit regiment per stampede | [Docs: tech notes 3] |
| Cause | `trampled by herd N` or `launched by herd N`, linked to `herd stampeded` ← the fear source (`fire started` ← the Lightning cast) | [Brief, 2026-10-11: "trampled by herd 2 ← lightning at 1:10"] |

### 11.5 Herd pen

[Docs: palette seed: a fence that keeps a herd in place as a static blocker; fire or a tremor breaks it open]

| Item | Value | Label |
|---|---|---|
| Shape | a ring fence, 1 cell wide, around a herd whose grazing circle is 10,000–25,000 mm | [Proposal] |
| Navigation | the fence closes horse and wheeled; foot climbs it at 300‰; animals can't cross it | [Proposal] Like a hedgerow |
| Herd inside | grazes within the pen; never wanders out | |
| Breaking | when the penned herd's fear reaches the threshold, the fence cells within 9,000 mm of the point of the ring farthest from the fear source are removed and the herd stampedes through the gap | [Proposal] In M5 the herd bursts the pen itself; fire and tremor breaking fences come with S3 |

---

## 12. Powers: Lightning v0, Rot v0, Wind shift v0

All three obey the range rule as Rain does: the whole area at least 100,000 mm from both armies' regiment outlines at the cast tick, else fizzle with the reason [Brief, 2026-10-08; S1 D1: checked at the queued tick, when the wind-up starts]. Energy costs are S5.

### 12.1 Lightning v0 (D10)

| Item | Value | Label and reason |
|---|---|---|
| Placed as | a point and a fall heading (cd), dragged with the mouse | D10 |
| Area for the range rule | a circle of radius 30,000 mm around the point | [Proposal] Covers the 8 m search, the longest tree's fall (18 m) and the 6 m fire patch |
| Wind-up | 60 ticks (3 s): the sky darkens and crackles over the point | [Proposal] Short: lightning is the "now" power; the delay comes from the herd and the march |
| Strike | at the end of the wind-up; fells the tallest tree within 8,000 mm of the point (ties: lowest tree index); no tree, no felling | [Docs: palette, "lightning fells tree"] |
| The fall | 40 ticks (2 s) along the chosen heading; stamped into navigation at the landing tick (13.2) | [Docs: tech notes 5, "the stamp is the moment the redirect happens"] |
| Ignition (D11) | if any fuel cell lies within 6,000 mm of the point and its wetness is < 24,576: those fuel cells burn for 600 ticks (30 s) | [Proposal] Fuel in M5: tree, woodland and hedgerow cells and map-authored brush. 24,576 is 37.5% wetness, between dry ground (8,192) and S1's mud threshold (16,384 starts mud, 32,768 soft): any rain soaking the ground stops it |
| Burning cells | closed and lethal to every class; fear source (11.2); no spread | D11 |
| Thunder | the strike is a fear source of 500 (11.2) | [Proposal] |
| Causes | `lightning struck` → `tree felled` and/or `fire started` | [Brief, 2026-10-11] |
| Counter | rain beforehand (wet fuel won't catch; the tree still falls); Engineers clear the tree; any foot regiment clears it slowly when it closes the last route (D14) | [Docs: palette counter web] |

### 12.2 Fire v0

Fire v0 is only what 12.1 needs: a static patch of burning fuel cells with a timer, no spread, no heat field, no smoke [Proposal, D11]. A soldier is burned only when launched or pushed into a burning cell (teetering applies at its edge, 4.1). S3 replaces it with the fuel and temperature fields; the patch's numbers then become the first row of the fire table.

### 12.3 Rot v0 (D12)

| Item | Value | Label and reason |
|---|---|---|
| Area | a circle, radius 10,000–30,000 mm | [Proposal] Small: one bridge, not a district |
| Wind-up | 200 ticks (10 s): fungus spreads over the planks, the warning | [Proposal] |
| Effect | every wooden segment (footbridge, wooden bridge, trestle, pontoon) with a cell in the area loses strength linearly to 500‰ of its maximum over 600 ticks (30 s), and stays there | [Docs: palette, "weakens a wooden bridge so it fails under heavy load"]; numbers [Proposal, D12] |
| A second Rot on the same segment | lowers it by another 200‰ of maximum, to at least 300‰ | [Proposal] Stacking is allowed but shallow |
| Stone, ice | unaffected | [Docs: palette] |
| Presentation | the shader reads each segment's strength ‰ (greying, sagging planks) | [Proposal] |
| Counter | Engineers repair it (14.7); Crossing Marshals keep the load under it (14.6); a second crossing | [Brief, 2026-10-11; Docs: palette] |

### 12.4 Wind shift v0 and the prevailing wind (D9)

| Item | Value | Label and reason |
|---|---|---|
| Prevailing wind | per map: a vector in mm/s (`map.json`), calm by default, at most 15,000 mm/s | [Docs] plan section 10 default |
| Wind shift v0 area | a circle, radius 30,000–80,000 mm, plus a direction (cd) | [Proposal] |
| Wind-up; duration | 100 ticks; 1,200 ticks (60 s) | [Proposal] As long as Rain |
| Strength | 10,000 mm/s in the chosen direction over the circle | [Proposal] A strong wind: 10 m/s moves light arrows by 300‰, the clamp's edge |
| Plume past the area | downwind, for 150,000 mm past the circle's edge, widening at 15° each side, fading linearly to 0 | [Docs] plan section 10 default (about 150 m) |
| Combining | vectors add: prevailing + every active plume | [Docs: tech notes 6] |
| Computed | analytically per cell, rebuilt only when a plume starts or ends | [Docs: tech notes 6] |
| What reads it in M5 | arrows only (6.3) | [Proposal, D9] Shelter, fire, smoke and rain drift are S3 |

---

## 13. Terrain pieces new in M5

All obey the 100 m buffer from deployment zones (whole footprint, zone edge to footprint edge) and the reachability check (no full trap, the 2× cap) [Brief, 2026-10-08; 2026-10-09]. Designer-placed pieces are exempt from the buffer (plan section 10). Height-editing pieces keep S1's edit order; the new pieces don't edit height, they stamp navigation and the structure layer after it. Prices are S5.

### 13.1 Trees (grove or row)

| Item | Value | Label and reason |
|---|---|---|
| Shapes | grove: a circle, radius 6,000–30,000 mm; row: a polyline of 2–8 points, 10,000–150,000 mm | [Proposal] |
| Trees | one per 3 × 3 cells (4.5 m) in a grove, on a grid offset by the seed; one per 4.5 m along a row | [Proposal] |
| Tree height | seeded per tree, 12,000–18,000 mm | [Proposal] |
| Navigation | the trunk cell closes every class; other grove cells are woodland: foot 800‰, horse 500‰, wheeled closed | [Docs: palette, "cover and a cavalry brake"]; numbers [Proposal] |
| Reachability | trunks and woodland are hard blockers for wheeled; open for foot and horse | [Brief, 2026-10-09 lists dense forest as a hard blocker; this open wood blocks only engines] |
| Fuel | yes (fire v0; S3) | [Docs: palette] |
| In water | refused where tick-0 water ≥ 5 mm | [Proposal] |

### 13.2 Felled trees and other battle-made blockers

| Item | Value | Label |
|---|---|---|
| Footprint | trunk: 1 cell wide, as long as the tree; crown: the last 4,500 mm, 3 cells wide | [Proposal] |
| Navigation | trunk: foot 300‰ (climb over), horse and wheeled closed; crown: closed to every class | [Proposal] |
| In water | the felled tree lies on the bed; water flows over and around it (a log in a stream, no damming in M5) | [Proposal] Damming waits for S3's dam rules |
| Crushing | soldiers in the trunk or crown cells at the landing tick die (cause `crushed`); within 1,500 mm of them, launched (falling-tree source); animals are pushed aside (D6) | [Brief, 2026-10-11]; numbers [Proposal] |
| Reachability | battle-made: exempt at placement [Brief, 2026-10-09] | |
| Clearing | Engineers 600 ticks (30 s); any foot regiment 2,400 ticks (120 s), only if the blocker closes its army's last route for some class (D14) | [Brief, 2026-10-11]; D14 |
| Wrecked or abandoned engines | the same stamp as a trunk; Engineers clear in 900 ticks | [Docs: brainstorm #37]; [Proposal] |

### 13.3 Hedgerow and drystone wall

[Docs: palette seed: "drawn as lines with the mouse. Infantry climb over slowly; cavalry and siege need a gap, so the army funnels through the openings ... the main funnelling tool."]

| Item | Hedgerow | Drystone wall | Label |
|---|---|---|---|
| Shape | polyline, 2–16 points, 10,000–200,000 mm, 1 cell wide | same | [Proposal] Long, so they're the cheap funnel |
| Height (presentation, height bonus later) | 1,500 mm | 1,200 mm | [Proposal] |
| Foot | crosses at 250‰ | crosses at 300‰ | [Docs: "infantry climb over slowly"]; numbers [Proposal] |
| Horse and wheeled | closed | closed | [Docs] |
| Reachability | hard blocker for horse and wheeled, so every line needs gaps the check accepts | [Brief, 2026-10-09] | |
| Material | wood and hedge: fuel; burns in M6 | stone: falls to a tremor (M6) | [Docs: palette] |
| Cover, wind shelter | none in M5 (garrisons, cover and the shelter mask arrive in M6) | | [Proposal] See section 18 |
| In water, on boulders | refused | refused | [Proposal] |

### 13.4 Wooden bridge and footbridge

[Docs: palette, "build, weaken or remove a bridge: sets up a later load collapse or forces a ford"]

| Item | Value | Label |
|---|---|---|
| Placed as | two bank points (dry ground ≥ 5 mm above tick-0 water) and a width | [Proposal] |
| Span | 6,000–40,000 mm | [Proposal] |
| Width | footbridge 1,500–3,000 mm (foot only); wooden bridge 3,000–6,000 mm (every class) | [Proposal] |
| Deck height | the higher bank point's ground, flat | [Proposal] |
| Segments and strength | 8.1 | |
| Water | flows under the deck; awash when the surface reaches the deck (8.4) | [Proposal] |
| Footprint for the buffer | the deck plus one cell at each end | [Brief, 2026-10-08] |
| Reachability | open for the classes it carries | |
| Counters | Rot; a flood that tops it; M6: fire, tremor, removal | [Docs: palette] |

### 13.5 Herd and herd pen

Section 11. The herd's footprint is its grazing circle; the pen's is the fence ring.

### 13.6 Map-authored: stone bridges and frozen ponds

| Item | Value | Label |
|---|---|---|
| Stone bridge | a structure in `map.json`: polygon, deck height, 1,200 kg/m² | [Proposal, D13] |
| Frozen pond | an ice-thickness layer (mm per cell, 60–250) over map water; patches 4 × 4 cells, aligned to the map grid | [Docs: content roadmap, "level-set frozen ponds"]; numbers [Proposal] |
| Ice and water flow | the pond is still water under the ice; frozen rivers wait for S3 and the brief's open question on whether ice stops flow | [Docs] plan section 10 |

---

## 14. The first six specialists

Settled: only specialist unit types shape the world, each with one signature action, two at most (2026-10-10); actions run through fixed regiment rules bought in the army layer and show as markers on the regiment's timeline lane (2026-10-09); changes write to the same fields and grids as powers; unit changes are small, local and limited by charges, while the god keeps the big changes; the 100 m rule doesn't bind units; unit-made blockers skip the lock-in check but each action leaves gaps for every unit type and stays clearable; specialists fight poorly; a wiped or routed specialist loses its unspent charges; each extra regiment of the same specialist costs more (2026-10-10); the first six and their seats (2026-10-11).

### 14.1 The shared executor and cost parity

**One executor** [Docs: tech notes 3]: each action is data on its unit type: trigger, edit kind, rate, charges and their unit, seat lock and validation. The executor runs every action; a new specialist is a data entry, art, an AI rule and a preview marker.

| Common rule | Value | Label |
|---|---|---|
| Trigger checks | at the regiment AI rate (4 Hz) | [Docs] plan 3.4 |
| Edits | batched into a few edits a second, applied serially in a fixed order (regiment id, then action order), through the incremental navigation and heightfield path; water tiles over an edited area wake | [Docs: tech notes 3; STATUS M2 notes] |
| Per-action reachability | before each edit batch, the coarse check runs with the edit applied; a batch that would close the last route or break the 2× cap for any class of either army is skipped, leaving a gap, and the action moves on | [Brief, 2026-10-09; Docs: tech notes 3] |
| Under threat | an enemy regiment's outline within 40 m pauses the action; the specialists fall back 30 m and resume after 10 s clear | [Proposal] "Specialists fight poorly" (brief); M7 adds escorts and hunting |
| Charges | on the regiment; lost when it is wiped or routs | [Brief, 2026-10-10] |
| Markers | a cause event when an action starts and ends, carrying the regiment id, drawn on the regiment's lane | [Brief, 2026-10-09] |
| Fighting | melee 2.1 (crew and specialists row); health 80; morale shock 1,300‰ | [Brief, 2026-10-10: specialists fight poorly]; numbers [Proposal] |
| Hidden | specialists and their markers stay hidden from the opponent until the battle starts, like any new placement | [Brief, 2026-10-09] |

**Cost parity** [Brief, 2026-10-09: "a unit that undoes a god setup for less than the setup cost would make energy worthless"; 2026-10-11: "each counter answers part of a setup, not all of it"]. S2 meets it in capacity; S5 must meet it in price.

| Specialist | One regiment answers | Leaves for the god |
|---|---|---|
| Fascine Carriers | one crossing about 4.5 m wide through one channel, ditch, mud patch or shallow ford | the rest of the channel; the siege queues at one point; a surge washes the fill out |
| Pontoon Train | one floating crossing up to 72 m | a surge snaps it; a drain grounds it; Rot weakens it |
| Crossing Marshals | the collapse risk at two crossings | a third of the throughput, a queue on the bank, and losing the marshals lapses the gate |
| Drovers | two herds moved off the road (attacker) or one onto it (defender), at walking pace | the stampede itself: fire still spooks the herd, and it scatters the dogs |
| Engineers | one trestle up to 21 m, or about four repairs, or four felled trees or two boulders cleared | the trestle is load-limited and wooden; the clearing takes time |
| Spademen | 30 m of ditch and bank inside the defender's buffer | Fascine Carriers fill one crossing; water can top the bank |

Price rules for S5 [Proposal]: a specialist regiment costs about 0.8–1.2 × the energy-equivalent of the setup it answers; each extra regiment of the same specialist in one army costs 25% more than the last [Brief, 2026-10-10: "each extra regiment ... costs more"; number Proposal]. Brainstorm prices for orientation [Docs]: Spademen 110 bodies per 100, Fascine Carriers 100 per 100, Pontoon Train 120 for 8 boats, Drovers 80 for 20, Crossing Marshals 200 for 24; Engineers unpriced.

### 14.2 The roster

| Specialist | Seat [Brief, 2026-10-11] | Size | Class; mass | Charges | Label |
|---|---|---|---|---|---|
| Spademen | defender | 50–100 | foot; 95 kg | 30 m of ditch per 100 soldiers (3 m per 10) | [Docs: brainstorm #36]; numbers [Proposal] |
| Fascine Carriers | attacker | 50–100 | foot; 90 kg + 40 kg bundle, radius 450 mm and 1,300 mm/s while carrying (walking haystacks) | 1 bundle per living soldier | [Docs: brainstorm #35]; [Proposal] |
| Pontoon Train | both | 4–8 boats + 5 crew each | boats wheeled, 1,500 kg; crew foot | 3 deck segments (9 m) per boat: 72 m for 8 | [Docs: brainstorm #33, "about 80 m in 45 s"]; [Proposal] |
| Drovers | both | 10–20 | foot; 75 kg | attacker: 2 herd jobs; defender: 1 | [Docs: brainstorm #43]; [Proposal] |
| Crossing Marshals | attacker | 12–24 | foot; 80 kg | 2 crossings | [Docs: brainstorm #45]; [Proposal] |
| Engineers | both | 20–40 | foot; 95 kg | timber 21 m and 2,400 ticks of clearing per 40 soldiers | [Brief, 2026-10-11: "charges are timber (metres of bridge) and clearing time"]; numbers [Proposal] |

### 14.3 Spademen (defender): dig a ditch and bank (D5)

| Item | Value | Label and reason |
|---|---|---|
| Where | a polyline of up to 3 points the player draws in the army layer, total length ≤ the regiment's charges, inside its own deployment zone or the 100 m buffer around it, starting within 60 m of the regiment's deployment point | [Brief, 2026-10-11: "dig ditches and banks inside the buffer"]; D5 |
| Profile | ditch 1 cell wide, 1,250 mm below the original ground; then 1 cell of untouched berm; then a bank 1 cell wide, +500 mm, on the regiment's side | [Proposal] The ditch's 833‰ step stops horse (700) and wheeled (250), not foot (1,000); the berm keeps the climb from ditch floor to bank top crossable for infantry; filled to the rim (1,250 mm) it sweeps foot (S1 `sweptDepth` 1,000), so rain makes it a moat |
| Rate | one 3 m section (2 cells along) per 120 ticks (6 s) with the full regiment, scaled by living ÷ starting soldiers (at least 250‰): 30 m in 60 s | [Docs: tech notes 3, "batch dig progress"]; number [Proposal] |
| Order | from the first point to the last; the regiment walks to the line at tick 0 and digs from behind the bank | [Proposal] |
| Gaps | each section passes per-action reachability; a section that would close the last horse or wheeled route is skipped | [Brief, 2026-10-09] |
| Clearable | Fascine Carriers fill it; M6: a tremor, frost | [Brief: "everything it makes stays clearable"] |
| Exit test | the ditch becomes a moat in the rain (section 17) | |

### 14.4 Fascine Carriers (attacker): fill one crossing

| Item | Value | Label and reason |
|---|---|---|
| Trigger | the regiment's foot path to its army's goal, within 80 m ahead of its officer, enters a fillable run: cells ≥ 300 mm below the ground on both sides (channel, ditch, hollow), mud ≥ 32,767, or water 200–1,500 mm deep | [Brief, 2026-10-11: "fill channels, mud and fords"]; numbers [Proposal] |
| The crossing | a strip 3 cells (4.5 m) wide along the path, through the run to the first cells open for wheeled on the far side, at most 12 cells (18 m) long | [Proposal] Wide enough for an engine (3 m) |
| Fill | each bundle raises one cell's bed by 400 mm, up to the lower of the two rims; filled cells get mud 0 and the `fascine` soil (churn 250‰, cap 500‰, like a road) | [Docs: brainstorm, "each bundle firms a cell and raises its bed"; S1 soils] |
| Rate | 2 bundles per second | [Proposal] A 2 m channel 6 m wide (about 70 bundles) fills in about 35 s |
| Charges | one bundle per living soldier; a dead carrier's bundle is lost | [Proposal] Casualties visibly cut the fill |
| In water | the raised bed backs water up (a brush weir); a filled cell where depth × flow speed ≥ 800 mm·m/s for 40 ticks loses 400 mm of fill (washes out) | [Docs: brainstorm, "a filled stream becomes a brush weir that washes out in a surge"]; numbers [Proposal] |
| Leftover bundles | used on the next fillable run; usually not enough for a second channel | [Proposal] One regiment, one crossing (brief) |
| Fuel | the bundles are fuel in S3 (the burning roadblock); not in M5 | [Docs] |

### 14.5 Pontoon Train (both): float a bridge

| Item | Value | Label and reason |
|---|---|---|
| Trigger | attacker: its army's foot path to the goal, within 150 m ahead of the vanguard, crosses water closed to wheeled (≥ 500 mm or sweeping). Defender: its own retreat route does, at battle start | [Brief, 2026-10-11: "float a bridge over a flooded ford or a broken bridge"]; [Proposal] |
| Site | within 50 m either side of where the path meets the water, the line (perpendicular to the path) with the lowest peak flow speed | [Docs: brainstorm, "where the flow on its route is weakest"] |
| Length | the water's width plus one cell at each end; refused if longer than the charges | [Proposal] |
| Laying | one segment (2 cells along, 3 wide) every 40 ticks (2 s) from the near bank: 72 m in 48 s | [Docs: brainstorm, "about 80 m in 45 s"] |
| Deck | floats on the water (never awash); strength 8.1 (grounded below 300 mm) | [Docs: brainstorm] |
| Surge breaks it | a segment where the flow speed is ≥ 1,800 mm/s for 40 consecutive ticks breaks loose; it and every segment beyond it (away from the near bank) are removed and swing downstream; units on them are swept (cause `pontoon broke loose` ← the water's cause tag) | [Docs: brainstorm, "a surge snaps the cables"]; numbers [Proposal], tuned in section 17 |
| Boats | each boat must reach the bank; a boat whose crew all die loses its 3 segments | [Proposal] |
| Exit test | spans a flooded ford until a surge breaks it | |

### 14.6 Crossing Marshals (attacker): meter a crossing

| Item | Value | Label and reason |
|---|---|---|
| Trigger | its army's route, within 150 m ahead of the vanguard, crosses a span: bridge segments, a pontoon, a trestle, or ice patches | [Brief, 2026-10-11: "meter a bridge or ice"] |
| Gate | the marshals stand at the near end; one regiment on the span at a time; the next waits at a holding line 20 m before the span until the last soldier of the one before has stepped off | [Docs: brainstorm #45]; 20 m [Proposal] |
| On the span | the crossing regiment takes open order (2.2), with files = max(1, span width ÷ (2 × file spacing)) | [Docs: brainstorm, "in open file"] |
| Never reads strain | the gate is a fixed rule (occupancy), not a reaction to creaks | [Brief, 2026-10-09] |
| Lapses | when the marshals are wiped or rout: the queue surges on at once | [Docs: brainstorm] |
| A crossing ends | when every regiment of its army whose route uses it has crossed, or after 6,000 ticks (300 s) | [Proposal] |
| Charges | 2 crossings | [Proposal] |
| Exit test | meters a rotted bridge so it holds while the bank queues | |

### 14.7 Engineers (both): bridgework and clearing

Two actions, the most a specialist may have [Brief, 2026-10-10; 2026-10-11].

| Item | Value | Label and reason |
|---|---|---|
| **Bridgework, trestle.** Trigger | attacker: its army's path meets water, a channel or a ditch closed to wheeled whose width along the path is ≤ 21 m, and no Pontoon Train has taken it; defender: the same on its own retreat route | [Brief, 2026-10-11]; [Proposal] |
| Trestle | segments 2 cells along, 2 cells wide (3 m), one every 160 ticks (8 s): 21 m in 56 s, slow next to the pontoon as the brief asks; stands on the bed, so water flows under it; 400 kg/m² (decided 2026-11-08), wood (rots) | [Brief, 2026-10-11: "short (up to about 20 m) and slow ... stand on the bed ... carry a set load ... wood"] |
| **Bridgework, repair.** Trigger | a wooden segment on the path (attacker) or the retreat route (defender), within 100 m, whose strength is below 700‰ of its maximum | [Brief, 2026-10-11: "restore strength to a damaged bridge span"; "repairs answer Rot"] |
| Repair | +25‰ of maximum per second, one segment at a time, up to 1,000‰; 1 m of timber per 250‰ restored | [Proposal] A segment rotted to 500‰ is sound again in 20 s for 2 m of timber |
| **Clearing.** Trigger | a battle-made blocker (felled tree, wreck) or a placed boulder on the path within 100 m | [Brief, 2026-10-11: "remove a fallen tree, debris, a wreck or a small boulder"; S1: "Engineers clear small boulders (M5)"] |
| Clearing time | felled tree 600 ticks; wreck 900; boulder 1,200; drawn from the regiment's clearing pool (2,400 ticks per 40 soldiers); the blocker is unstamped when done | [Proposal] Two boulders or four trees per regiment |
| Charges | timber 21 m and the clearing pool, both scaled by soldiers at purchase | [Brief, 2026-10-11] |
| Reading strength | the repair trigger reads a segment's strength (a property of the bridge), never its load or strain stage | [Proposal] Keeps "regiments never heed warnings" intact (section 18) |
| Exit test | spans a channel with a trestle, repairs a rotted span, clears a felled tree | |

### 14.8 Drovers (both): walk a herd

| Item | Value | Label and reason |
|---|---|---|
| Fear source | 400 within 30 m of the regiment's centroid, so the herd walks away from them at 1,200 mm/s; never more (threshold 700) | [Brief, 2026-10-09: "herd at walking pace but never start or steer a stampede"] |
| Attacker trigger | a herd with any animal within 40 m of its army's path, within 200 m ahead of the vanguard: the drovers go round to the path side and walk the herd away until every animal is ≥ 50 m from the path, hold 30 s, then take the next job | [Docs: brainstorm #43]; [Proposal] |
| Defender trigger | at battle start, the nearest herd within 200 m: walk it onto the attackers' predicted route (the route preview) and hold it there until an enemy outline comes within 60 m, then withdraw | [Brief, 2026-10-11: "walk herds off a road or into place"]; [Proposal] |
| Dogs scatter | a fire v0 patch or a lightning strike within 60 m of the drovers switches their fear source off for 400 ticks | [Docs: brainstorm, "fire or thunder scatters the dogs first"] |
| Exit test | drovers walk a herd off the road | |

---

## 15. Reachability treatment, additions to S1 section 10

| Piece or state | foot | horse | wheeled | At lock-in or placement |
|---|---|---|---|---|
| Hedgerow, drystone wall, herd-pen fence | open (slow) | closed | closed | hard blocker for horse and wheeled |
| Grove: trunk cells | closed | closed | closed | hard blocker |
| Grove: woodland cells | open | open | closed | hard blocker for wheeled |
| Wooden bridge | open | open | open | open |
| Footbridge | open | closed | closed | open for foot only |
| Stone bridge (map) | open | open | open | open |
| Frozen pond (intact ice) | open | open | open | open (the load can fail it in battle: battle-made) |
| Herds | ignored | | | they move [Docs: tech notes 3] |
| Felled tree, wreck, broken ice, collapsed segment | | | | battle-made: exempt, clearable (D14) [Brief, 2026-10-09] |
| Specialists' edits | | | | per-action check (14.1) |

Clearances: section 3.3 (wheeled 1).

---

## 16. Cause events and the results screen

New cause events, each with links to its causes, per the brief's rule that every death records its cause and source [Brief, 2026-10-11] and the tech notes' list (section 10):

- **Structures:** `strain stage` (segment, stage; for the overlay and the trace, not for regiments), `bridge segment failed`, `ice patch broke`, `pontoon broke loose`.
- **Launches:** `launched` (source event, soldier), `landed` (outcome).
- **Combat and impacts:** `trampled`, `crushed`, `fell`, `burned`, `thrown in the mud`, `combat` (melee, volley, stone).
- **Herds:** `herd agitated`, `herd stampeded` (fear source), `herd scattered`.
- **Powers:** `lightning struck`, `tree felled`, `fire started`, `rot took hold` (segments), `wind shifted`.
- **Morale:** `regiment routed`, `regiment rallied`, `regiment wiped`, `soldiers fled`, `army routed`.
- **Specialists:** `ditch dug`, `bundles dropped`, `pontoon laid`, `trestle built`, `span repaired`, `blocker cleared`, `herd driven`, `crossing metered` (start and end), each with the regiment id ("ditch dug by Spademen 2").
- **Outcome:** `objective held` (timer), `sunset`, `defender wiped`.

Results screen (M5 item 8): deaths by cause (combat, trampled, crushed, burned, drowned, fell, fell with the ice, pushed), each clickable to its trace; regiments wiped, fled and routed; the battle's result and why (which condition of 7.5).

---

## 17. M5 calibration: test maps and targets

As S1 section 13 did for M3, the exit criteria become tuning targets. Tune the listed numbers, never the rules. Maps are grey-box, at the working scale (1.2 × 0.8 km, 1.5 m cells), built by `tools/maps/` scripts like M3's.

| Exit-criterion scenario | Test setup | Target | Numbers tuned here |
|---|---|---|---|
| **A thin file crosses a bridge that a dense column breaks** | m2-bridge's river with a wooden bridge piece 4.5 m wide and 30 m long; (a) one regiment of 200 infantry in open order, single file at the bridge; (b) 2,000 infantry in close order queueing onto it | (a) peak load < 700‰ (no warning) and everyone crosses; (b) the queue's density on the span reaches ≥ 2.0/m² and a segment fails within 60 s of the jam forming, preceded by creak, crack and sag | 8.1 wooden 300 kg/m²; 4.2 jam density; 8.2 two checks |
| **...and fixed ice** | a frozen pond 120 mm thick, 60 m across, on the route | (a) the thin file crosses; (b) the column breaks a patch, and the breaks spread through the column but stop short of a regiment in open order behind it | 8.1 ice 1 kg/m² per mm; 8.2 neighbour loss 150‰ |
| **A rotted bridge fails only under the bulk of the army** | the wooden bridge with Rot cast at 0:30; 6 regiments of 200 | the first regiment crosses with peak load < 850‰; a segment fails only once 3 or more regiments are on or bunched at the span; with no Rot, the same army crosses (warnings allowed, no failure) | 12.3 rot floor 500‰; 8.1 |
| **Lightning near a herd stampedes it into a column; the landing preview matches the live result exactly** | a 60-animal herd grazing 40 m beside the road; a grove with dry ground 100 m beside it on the far side; Lightning on the grove at the tick that puts the run across the column's middle | the herd runs across the road, ≥ 50 soldiers launched, ≤ 15% of the hit soldiers die; the preview's landing spread (cells and outcomes) is identical to the battle's, cell for cell; with Rain over the grove beforehand, no fire and no stampede | 11.2 fear ranges and threshold; 11.3 run length; 9.2 stampede launch; 11.4 victim odds; 12.1 ignition wetness 24,576 |
| **A charge or stampede tramples and launches but never chains** | a cavalry regiment of 60 charging a close-order and an open-order infantry block, braced and unbraced | launches and tramples happen; no soldier is launched except by a charge contact, a stampede animal, a stone, a tree or a collapse (an invariant checked every tick, fuzzed); the braced block suffers about half the launches | 10.3 targets per rider and odds; 9.3 knock-down |
| **Fascines fill one crossing through a channel** | the S1 channel (2 m deep, 6 m wide) across the siege route, 4 engines and a Fascine regiment of 100 | one crossing 4.5 m wide is filled in ≤ 60 s, the engines cross by it in single file; the rest of the channel stays a channel | 14.4 bundle height 400 mm, rate 2/s |
| **A pontoon spans a flooded ford until a surge breaks it** | m3-exit with S1's storm; a Pontoon Train of 8 boats; rain at 0:50 | the pontoon is laid across the flooded ford and regiments use it; the crest (about 3:05 on m3-exit) breaks it while regiments are on it, and they are swept | 14.5 cable break 1,800 mm/s for 40 ticks (must sit above the flooded ford's flow when laid and below the crest's) |
| **Marshals meter a rotted bridge so it holds while the bank queues** | the rotted bridge (above) and the 6 regiments, plus Crossing Marshals | no segment fails; peak load < 950‰; a queue of ≥ 3 regiments forms on the near bank; crossing takes ≥ 2.5× as long as unmetered | 14.6 open-file rule; 2.2 open-order spacing |
| **Drovers walk a herd off the road** | a herd grazing on the road ahead of the column; attacker Drovers | the herd is ≥ 50 m from the path before the vanguard arrives; no stampede; without drovers the column queues behind the herd (the polite queue) | 14.8 fear 400 and 30 m; 11.1 driven speed |
| **Spademen's ditch becomes a moat in the rain** | a valley floor sloping 20‰ toward a 30 m Spademen ditch in the defender's buffer; Rain 100 m upslope at 0:30 | the ditch holds ≥ 1,000 mm (lethal for foot) before the attacker arrives, and soldiers pushed in by the crowd teeter first | 14.3 depth 1,250 mm; S1's rain (unchanged) |
| **Engineers span a channel with a trestle, repair a rotted span and clear a felled tree** | three setups on one map: a 6 m channel, a rotted wooden bridge, a felled tree across the road | trestle done in ≤ 60 s and engines cross it; the rotted span back above 950‰ before the column arrives; the tree cleared in 30 s; with no engineers, a foot regiment clears the tree in 120 s only when it closes the last wheeled route | 14.7 rates; D14 |

Rough checks behind the numbers:

- Marching close-order infantry: 0.8/m² × 90 kg = 75 kg/m², a quarter of a wooden bridge and half of a rotted one: the vanguard crosses quietly, a jam (2–3/m², 180–270 kg/m²) breaks a rotted bridge and makes a sound one sag.
- Under Crossing Marshals one 200-strong regiment in open order on a 40 m × 4.5 m span is about 1.1/m² at worst: 100 kg/m², 67% of a rotted span.
- A 120 mm frozen pond: marching infantry 62%, marching cavalry 93%, a single file 10–20%, any jam > 100%.
- The pontoon: the M3 flooded ford runs at roughly 0.5–1.2 m/s when the train arrives, the crest above 2 m/s; 1.8 m/s sits between.

---

## 18. Open questions that block numbers, and what's deferred

- **Prices** (bodies and energy) for every unit, specialist, piece and power; the extra-regiment surcharge; refunds on fizzle (S5). Section 14.1 gives the parity rule S5 must meet.
- **Brief open question, "How immediate may a harmful consequence be?"** A stampede set off by lightning reaches a column within about 10 s of the strike (the brief already notes it). This spec doesn't shorten or lengthen it.
- **Cover from low walls and hedges.** The brief (2026-10-08) gives "infantry behind a wall cover from arrows", but towers and walls "fight only through their garrisons", and garrisons arrive in M6. This spec gives hedgerows and drystone walls no cover in M5. Does a drystone wall shelter soldiers standing behind it without a garrison (recommended: yes, from S3, with the shelter mask)?
- **Slipping on ice, ice under rivers, cold timers** (D8; S3).
- **Fire spread, the burning state, heat drying, smoke** (S3). Fire v0 is static (D11).
- **Wind shelter behind ridges and forest; rain drifting with the wind** (S3, M6).
- **Behavioural stances; specialists hunted and escorted; the defender's openings; the campaign attacker's behaviour sheet** (S4, M7). Section 5's regiment targeting is a working default.
- **Soaked wool (IB 33):** wet regiments heavier on ice (wave 4, optional).
- **Death remains** (brief open question): unchanged working assumption, they vanish after a few seconds.
- **Defender Pontoon Trains cutting their bridge loose after the last crossing** (brainstorm): M7, with retreat rules.
- **The engineers' repair trigger** reads a bridge's strength. That is a property of the structure, not a warning stage, so it keeps "regiments never heed warnings"; flag it if the user reads it otherwise.

---

## 19. Files

- This spec: `docs/design/spec-s2-armies.md`
- Draft rules data, to wire into `data/rules/` (with schemas in `data/schema/`) as M5's items read them. All integers, units documented in each `$comment`, `"status": "placeholder"`:
  - `docs/design/s2-data/unit-types.json`: the four base types, the crew, the six specialists and the herd animal (replaces `data/rules/unit-types.json` v0)
  - `docs/design/s2-data/movement-classes.json`: slope curves, ice speeds (S1's water and mud unchanged; replaces v0)
  - `docs/design/s2-data/hazards.json`: teeter thresholds per class, falls (replaces v0)
  - `docs/design/s2-data/routes.json`: wheeled clearance 1 (replaces v0)
  - `docs/design/s2-data/combat.json`: melee, volley rules, charges, open order, regiment targeting v0
  - `docs/design/s2-data/projectiles.json`: light arrow, crossbow bolt (reserved), siege stone
  - `docs/design/s2-data/morale.json`: morale, routing, rallying, wiped, army rout
  - `docs/design/s2-data/outcomes.json`: the survival and objective win conditions and sunset defaults (for the level format)
  - `docs/design/s2-data/load.json`: structure kinds, strengths, strain stages, failure and collapse, cold-water timers
  - `docs/design/s2-data/launches.json`: gravity, sources, spread, landings by surface, knock-downs
  - `docs/design/s2-data/herds.json`: herd, fear sources, agitation, stampede, impacts, pen
  - `docs/design/s2-data/powers.json`: Lightning v0, Rot v0, Wind shift v0 (to add to `data/rules/powers.json` next to Rain)
  - `docs/design/s2-data/terrain.json`: trees, hedgerow, drystone wall, wooden bridge, footbridge, herd, herd pen (to add to `data/rules/terrain.json`), plus the map-authored stone bridge and frozen pond
  - `docs/design/s2-data/specialists.json`: the executor's common rules and the six specialists' actions
- Suggested additions elsewhere (for the implementer, not done here):
  - map schema: `wind` (prevailing, mm/s), `structures` (stone bridges), an ice-thickness layer, `fuel` (brush) areas
  - plan schema: per-regiment `order` (`close` or `open`, D1); the Spademen's line (D5); Lightning's fall heading and Wind shift's direction on casts
  - level format (M9): `outcome` (`survival` or `objective`, with the objective polygon) and `sunsetTick`
