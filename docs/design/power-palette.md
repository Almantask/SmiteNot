# Power palette
 
God powers, terrain pieces, counters and combos. The game-design skill reads this when designing or reviewing powers. Entries are seeds, not final designs; decisions in `claude/game-brief.md` win if they disagree, and settled power rules belong here.
 
## What makes a good power here
 
Score a candidate against these. A strong power hits most of them.
 
1. **Indirect:** acts on the world; its whole area stays at least 100 m from both armies when it fires (the range rule), so its effect reaches them only by flowing, spreading, drifting or blowing in.
2. **Scales with mass:** gets more effective against a bigger, denser army.
3. **Differentiated:** hits some unit types much harder than others.
4. **Combos:** writes to or reads from at least one shared field other powers use.
5. **Anticipation:** has a wind-up, travel time or slow build-up, so it rewards predicting where the army will be.
6. **Readable:** the cause and the effect are both visible at strategic zoom.
7. **Counterable:** the attacker AI has a believable response, so it buys time or advantage rather than an automatic win.
8. **Double-edged (optional but good):** can also hurt the defender if used carelessly, since both armies share the world.
9. **Lethal only through the chain:** if it can kill, the deaths come later from world state the army enters, overloads or is caught by (a flood, a failing bridge, breaking ice, a stampeding herd, a fire front, a rolling boulder), with a visible warning first, never from the cast itself.
## Terrain shaping (campaign phase 2, PvP round 2)
 
Settled toolkit: dig or raise ground, add water, grow trees, add swamp, set boulders, place herds, and built pieces (walls, towers, hedgerows, dams, herd pens, bridges and the like; see Built pieces below). Pieces are placed before the battle and exist from the first tick, at least 100 m from every deployment zone; they are never timed, but some then act on their own (water flows, herds roam). They build the stage the god powers play on, so pair each piece with the powers it sets up.
- **Raise a ridge or dig a hollow:** high ground for archers, a hollow that collects floodwater.
- **Dig a channel:** routes a river toward or away from a ford; floods later with rain.
- **Add water:** a pond, lake or spring-fed stream. Rain swells it; cold freezes it into an ice crossing or an ice trap.
- **Grow or clear trees:** cover and a cavalry brake, or open ground. Forest is also fuel for fire and lightning, and shelters archers behind it from wind.
- **Add swamp, or firm ground:** soft ground that rain turns to deep mud, or a hard road that resists it.
- **Set boulders:** hard blockers that funnel the column. Fire can't clear them, so price them above trees. In a stream they dam it. On a slope, a tremor or landslide sets them rolling down the fall line, crushing what they hit (settled 2026-10-11); rumbling and dust are the warning, and the preview draws the path. They also split or turn a stampede.
- **Place herds:** animals that roam and block paths as they move, so a route opens and closes over time and the timeline preview shows when. Herds read the fields like soldiers do (flee fire, avoid deep water, fall through weak ice, churn mud). Fire or lightning nearby spooks them into a stampede that can run into an army (settled; see Creatures).
- **Build, weaken or remove a bridge:** sets up a later load collapse or forces a ford. Wooden bridges burn and rot; stone bridges carry more and fall only to a tremor or flood scour.
### Built pieces (settled: allowed)
 
Built pieces follow the same rules as natural ones: placed before the battle, at least 100 m from every deployment zone. They block, funnel, hold water, carry load or shelter troops, but never harm soldiers by themselves (no stakes or pits). Walls and towers fight only through the troops garrisoning them (settled): an empty tower does nothing. Material is their counter axis: wood and hedges burn and rot, stone falls only to a tremor, flood or siege engines, so none is a permanent lock.
- **Hedgerows and drystone walls (seed):** drawn as lines with the mouse. Infantry climb over slowly; cavalry and siege need a gap, so the army funnels through the openings. Hedges burn (fire opens a gap) and shelter archers from wind; walls don't burn and fall only to a tremor. Cheaper than boulders per metre, so they are the main funnelling tool.
- **Watchtower (seed, settled: allowed, fights only through its garrison):** a strongpoint one regiment can garrison. Height adds range for its archers and lets them shoot over hedges and the front ranks; a tower upwind gains the most. Archers gain the most from a tower, cavalry nothing. Wooden towers burn, stone ones fall to a tremor or siege engines, and smoke drifting over a tower blinds its garrison. It sits at least 100 m from the deployment zone, so the defender must march out and man it before the attacker arrives, which the timeline preview shows; flooding or blocking the approach keeps the garrison out.
- **Palisade or curtain wall (seed, settled: allowed, fights only through its garrison):** taller than a drystone wall, so nobody climbs it. Infantry and archers garrisoned behind it take far fewer arrow hits and hold a line; the attacker must breach it with siege or go around. Palisades burn; stone walls fall to a tremor or siege, which finally gives siege engines a target of their own.
- **Dam, weir or millpond (seed):** holds water back from the first tick, keeping the ford below dry. Rain raises the reservoir; a tremor breaks the dam (or fire, if it's wooden) and releases a flood downstream. The placed counterpart of the dam-break power.
- **Herd pen (seed):** a fence that keeps a herd in place as a static blocker. Fire or a tremor breaks it open and the frightened herd bolts, so a pen makes a stampede easier to time than a roaming herd.
- **Old mine workings (seed):** ground that looks firm but has a load limit, like ice. A dense column or siege train breaks through into the shafts; rain lowers its strength. Scales with mass and reuses the ice and bridge load system. Warning: the ground sags and cracks as the load nears the limit.
## Fields and seed powers
 
All powers obey the 100 m range rule at the moment they fire; the notes below say how each one's effect still reaches the armies.
 
### Water (water height, flow)
- **Rain / storm:** wide, slow; raises wetness and feeds rivers over time. Falls ahead of or beside the army; its water reaches them through rivers, floods and soaked ground they march into. Where the temperature is below freezing it falls as snow instead (see Snow).
- **Dam break / beaver dam:** fell trees or rocks into a stream to back water up, then release it later.
- **Spring:** open a spring on a hillside to flood a valley floor gradually.
- **Divert river:** carve a small channel so a river changes course, cutting or opening a ford.
- **Scour (seed):** the current eats under a riverbank or a stone bridge pier. It holds until a crowd stands on it, then the bank slumps or the span drops. The stone counterpart of Rot; rain and floods speed it up. Warning: cracks and slumping earth along the bank.
- **Drain (seed):** lowers a lake or river for a while, exposing a lakebed or a new ford. Bait: lure the attacker onto the lakebed, then refill it (rain, a dam break, a snowmelt surge). Also opens a route for a defender counterattack.
### Ground (wetness, firmness, slope)
- **Mud:** follows from wetness; worsens as many feet churn it (scales with mass).
- **Sinkhole / subsidence:** collapses ground under a road ahead of the army; heavy siege makes it worse.
- **Landslide:** loosen a saturated slope (combo with rain) to bury a road.
- **Quake tremor:** cracks a bridge or opens a ravine, and sets boulders on slopes rolling (settled 2026-10-11); never cast at the army itself.
- **Liquefaction (seed):** a tremor on saturated ground turns it to quicksand under load. Siege sinks first, then cavalry; a light file crosses what a dense column can't. Needs rain first, so it's a combo by design. Reuses the load system. Warning: water wells up and the ground ripples.
### Vegetation (trees, brush, fuel)
- **Lightning fells tree:** blocks or redirects a road ahead of the army (from the user's original idea). The tree is at least 100 m from the troops when struck, so it can never land on them.
- **Overgrowth:** thickens brush to slow cavalry and break formations, but gives cover to the defender too.
- **Rot:** weakens a wooden bridge so it fails under heavy load.
- **Wither (seed):** dries a strip of vegetation into fuel and strips its cover; the inverse of Overgrowth. Sets up fires that rain would otherwise stop, and removes the forest that shelters archers from wind.
### Fire (heat, fuel)
- **Wildfire seed:** a lightning strike in dry brush, driven by wind; wet ground stops it.
- **Fire burns soldiers it reaches (settled 2026-10-11):** soldiers never walk into fire, but a front that reaches them burns them. On open ground a front spreads more slowly than infantry can run (a tunable cap that wind and dry fuel push toward), so fire kills those it traps against water, cliffs, crowds or other fire, not everyone it touches. Warning: smoke and a visible front, which regiments flee. Toy-like: soot, a puff of smoke, the soldier topples and vanishes.
- **Smoke:** a fire's smoke drifts downwind and blinds archers.
- **Fire melts ice (settled):** fire's heat turns ice back into water. Ice holds no fuel, so the heat comes from fuel burning beside it (reeds, bank forest) or a heat power. Thinning ice loses strength, so ice that held the column at first can fail under it mid-crossing. Meltwater wets nearby fuel, which slows the fire that caused it: a built-in limit. Since the fire must start 100 m from the armies, melting ice under a column means lighting the reeds early enough for the fire to spread to the crossing in time.
- **Fire spooks herds (settled):** a fire or lightning strike near a herd sets off a stampede; see Creatures.
### Air (wind, visibility)
- **Wind shift:** pushes fire, smoke and fog, and bends archery (settled): arrows shot into a strong wind fly slower and fall shorter, shots downwind carry further, which favours archers placed upwind. Seed tuning: only the along-shot part of the wind counts; light arrows are hit hardest, crossbow bolts less, siege stones barely; cap the range loss so archers never become useless. The cast area stays 100 m from the armies, so the wind must keep blowing downwind past it to reach the archers (recommended; open question in the brief).
- **Fog bank:** cuts sight lines, causing regiments to lose cohesion or get lost; ranged suffer most. Laid where the army will march, or upwind so it drifts onto them.
- **Sandstorm or blizzard:** large, slow visibility and speed debuff. It can't be cast over the armies, so it builds 100 m or more away and drifts onto them with the wind.
- **Calm (seed):** stills the wind in an area. Fire stops spreading and arrows fly true, but smoke and fog pool in low ground. Mainly a PvP counter-power, with a turnabout: pooled smoke in the opponent's valley.
- **Thunderclap (seed):** a fear source without fire. It stampedes herds even when the ground is too wet to burn (answering rain as a stampede counter) and can release a loaded avalanche. It affects animals and snow only, which keeps it clear of the omen question.
### Temperature (cold, heat)
- **Cold snap:** freezes water into ice (settled). Ice forms over time, thin first and thickening while the cold lasts.
  - **Weak ice breaks under crowds (settled):** each stretch of ice holds a load set by its thickness. When too many units stand on it at once, it breaks and they fall into the water. A thin file crosses ice that a dense column breaks; heavy units (siege, cavalry, armour) count for more. Spreading cracks are the warning, and crossing in spread-out files or waiting for thicker ice is the attacker's counterplay.
  - A frozen river is a new crossing for both armies.
- **Heat wave:** dries mud, raises fire risk, tires armoured troops over a long march; melts ice like fire does, and melts snow into water.
- **Freezing rain (seed):** rain that freezes where it lands, glazing roads, slopes and trees. Siege slides back down slopes and cavalry can't charge; iced trees shed limbs onto roads, more so in wind. Heat clears it.
- **Ice break-up (seed):** thaw a frozen river upstream. The floes ride the current, jam at the narrowest bridge or ford, back the water up and ram the bridge. Cold then heat, far from both armies, with the payoff arriving downstream.
### Snow (snow depth; settled: a world field, set per map)
 
The level sets whether a map starts with snow and how deep, like a climate (settled). Snow is world state every unit reads, and it ties the cold, water, wind and fire fields together.
- **Snow on the ground (settled):** slows every unit: cavalry lose their charge and siege bogs down most, infantry slow least. Fuel under snow won't burn, so fire is weak on winter maps. Heat melts snow into water that runs downhill into rivers. Seed: regiments pack snow into a trail, so the vanguard wades and later regiments move faster; fresh snow or drifts erase the trail.
- **Snowfall (seed, working assumption):** not a separate power. Rain falling where the temperature is below freezing builds snow instead of wetness, so on a cold map rain becomes snowfall; elsewhere it needs a cold snap first.
- **Snowmelt surge (seed):** heat (a heat wave, or fire on a forested slope) on a distant snowfield turns it into meltwater that runs down the valley and arrives minutes later as a flood pulse. The cast can be far from both armies, so the skill is all in the timing; the timeline shows the wave front travelling. Works only where there is snow to melt.
- **Avalanche (seed):** snow on a steep slope has a load limit, like ice. More snow, a tremor, heat or a thunderclap releases it onto the pass below, burying the road and anyone on it. Scales with mass: the denser the column in the pass, the more it buries. The slope must be 100 m from the armies; the slide is the consequence. Warning: cornices crack and small slides run first.
- **Drifts (seed):** strong wind moves loose snow downwind and piles it where the wind drops: against hedgerows, walls and forest edges, and into hollows and dug channels. Drifts close hedge gaps and fill channels, so wind reshapes the funnel mid-battle.
### Map-dependent (seeds)
Powers that need a map feature the level provides, like snow.
- **Lava vent (seed):** a slow flow creeps downhill and cools into a new ridge, reshaping the map without locking it. Lava meeting a river makes steam fog; it melts ice and snow. Expensive.
- **Call the tide (seed, coastal maps):** a causeway or tidal flat floods on a schedule. A longer column takes longer to cross, so its tail gets caught.
### Light and omens (open pillar question)
- **Eclipse, comet, ominous sky:** lower attacker morale. This acts on minds rather than terrain, so confirm with the user whether morale-affecting powers count as "reaching the army directly" before designing them.
### Creatures
- **Herds as terrain (settled):** placed in terrain shaping; they roam and block paths. See the terrain section.
- **Stampede (settled):** fire, lightning or another fear source spooks a herd, which bolts away from the fear, favouring open and downhill ground, for a limited run before it calms. It tramples and breaks formations in its path, and panic spreads from the regiments it hits. It is never aimed at an army: the setup is where the herd grazes and where the fire starts, and deep water, ridges and boulders turn it. It doesn't pick sides. Warning: the herd mills and raises dust a few seconds before it bolts. Seed tuning: mostly disruption and morale shock, few deaths; one stampede per herd, after which it scatters; braced formations and siege engines split it, loose archers and marching columns suffer most.
- **Swarm, wolves (open):** animals driven or summoned to attack an army. This still blurs the "never attack directly" line, so ask before designing.
### Rejected
- **Tornado, hail, ball lightning:** each strikes soldiers directly, which breaks the indirect pillar.
## Counter web
 
Powers counter each other through the shared fields, never through rules that delete a power. Every power should have one or two soft counters and work as a counter itself. A counter's strength depends on how much of the area overlaps, how much was applied and when: pre-emptive counters (rain before a fire) are cheap, reactive ones (rain on a raging fire) are expensive.
 
| Power | Countered by | How (through the fields) |
|---|---|---|
| Rain, flood | Heat or drought; cold | Heat dries ground and lowers rivers; cold freezes the flood into ice the army can cross |
| Fire | Rain; snow cover; a backfire | Wet fuel won't burn, nor will fuel under snow; burning a strip of fuel first leaves nothing for the fire to spread into |
| Wind | Opposing wind; forest or hills; calm | Winds add up in the wind field, so opposite winds cancel; terrain shelters the area behind it (including archers from a headwind) |
| Fog, smoke | Wind; heat | Wind blows it away; heat lifts fog |
| Cold, ice | Fire or heat; heavy units; crowds | Heat melts ice; weight beyond what the ice holds breaks it |
| Heat, drought | Rain; cold | Re-wets the ground; lowers temperature |
| Snow | Heat or fire; wind | Heat melts it into water (which may then flood downhill); wind drifts it from one place into another |
| Avalanche | Heat, a tremor or a thunderclap used early | Releasing the slope before the column arrives empties it harmlessly |
| Felled tree, debris | Fire; flood; Engineers (unit) | Fire burns the blockage; a flood washes it downstream; engineers clear it from the road |
| Boulders | Tremor; flood (small ones) | Shakes them loose or rolls them aside; fire does nothing |
| Walls, hedgerows | Fire (hedges); tremor (walls); drifts | Fire burns a gap in a hedge; a tremor topples stone; drifting snow closes the gaps |
| Towers, palisades | Fire (wood); tremor or siege (stone); smoke; a blocked approach | Burns or topples them; smoke drifting over blinds the garrison; flooding or blocking the route keeps the garrison from reaching an empty tower |
| Herds | Fire; water | Herds flee heat and avoid deep water, so they can be steered off a road or stampeded |
| Herd pen | Fire; tremor | Breaks the fence and frees the herd, often straight into a stampede |
| Stampede | Rain; water, ridges, boulders | Wet fuel won't catch, so the herd isn't spooked (unless a thunderclap does it); deep water, steep ground and boulders turn the stampede |
| Dam | Tremor; fire (wooden dam) | Breaks it, releasing the stored water |
| Rot, tremor (weakened bridge or slope) | Reinforce; Engineers (unit) | Restores strength before the load arrives; engineers repair a weakened span |
| Any queued power (PvP) | Marching near its area | An army within 100 m of a power's area when it fires makes it fizzle, so routing past enemy setups shuts them down |
 
**Turnabouts beat cancels.** The most exciting counters turn a setup against its owner rather than erasing it:
- Wind turns the opponent's fire or smoke back toward their own army.
- A tremor releases the opponent's dam onto the side they built it to protect.
- Cold turns the opponent's flood barrier into an ice bridge for your army.
- Fire burns away the opponent's tree blockade and opens the road.
- Fire on the banks melts the opponent's ice bridge while their heavies are on it.
- A wind shift turns the opponent's tailwind into a headwind for their archers.
- Fire on the far side of the opponent's herd stampedes it back through their own army.
- Fire burns a gap in the opponent's hedgerow line, turning their funnel into a road for your army.
- Smoke from a fire upwind of the opponent's tower blinds its garrison just as your column passes beneath it.
- Wind drifts snow into the gaps the opponent left in their own hedgerow line, so their army has to go round.
- A thunderclap releases the avalanche the opponent loaded for your column while their own column is in the pass.
**Counter-counters.** Because powers are fields, each counter can be answered again: wind can push your rain away from their fire; heat can melt your ice bridge just as the heavy units arrive. Keep chains short enough to read (about three steps), and make sure no single power answers everything.
 
**Cost parity.** A counter should cost roughly as much preparation as the setup it answers, or PvP turns into undoing everything the other player builds.
 
**Reveal (open idea):** in PvP, a power that exposes some of the opponent's new placements before the battle starts. Strong, so it should be costly and partial.
 
## Campaign puzzle pieces
 
Every combo below is a candidate level. A good puzzle level usually gives the player two or three powers whose obvious use fails and whose combination succeeds.
 
Levels are planned in three phases (army, terrain, powers), and each phase is a lever the level designer controls:
 
| Phase | What the level controls |
|---|---|
| 1. Army | The troops given and the deployment slots or zone |
| 2. Terrain | A short list of pieces (for example one channel, two boulders, one herd) |
| 3. Powers | A small hand of powers and their costs |
 
- The strongest levels make the phases depend on each other: a placement only works with a terrain piece, and that piece only pays off with a power (archers on a raised ridge above a hollow that a later rain floods).
- No phase should solve a level on its own; in particular, the defender is weak enough that no deployment wins without the god.
- Keep each phase tightly constrained (slots rather than free placement, a few terrain pieces), so the solution space stays designable and cheese is easy to find in testing.
- The level also sets the climate (starting snow, and so which snow powers can work). A winter chapter can teach snow, melt and avalanches together.
## Strong combos to point toward
 
- Rain, then cold snap: ice fields that crack under siege.
- Rain, then landslide: saturated slopes give way.
- Rain, then a tremor under the siege train: liquefaction swallows the heaviest units first.
- Lightning in a storm, then wind: fire that the rain then contains, shaping a burned corridor.
- Felled tree in a stream: dam, flooded valley upstream, dry ford downstream.
- Fog plus a redirected path: the army gets lost and splits.
- Rot or tremor on a bridge plus congestion: the bridge fails only when the bulk of the army is on it.
- Channel dug in phase 2, then rain in phase 3: a dry ditch becomes a flooded moat just as the column reaches it.
- Forest planted on a riverbank in phase 2, a cold snap, then lightning into the bank upstream before the column arrives: the fire spreads along the bank, the ice thins and the heavy units go through.
- Boulders or a herd blocking the far bank of a frozen river: the column backs up on the ice until the crowd breaks it.
- Wind at the defender's back: their archers on a raised ridge outrange attacker archers shooting into it.
- A herd grazing beside the road, with lightning into dry brush behind it as the column passes: the herd bolts across the column, and boulders placed downhill keep it from turning onto the defender.
- Dam built in phase 2, rain over its reservoir, then a tremor as the column fords below: the stored flood arrives at its peak.
- Hedgerows funnelling the column onto a rotted bridge: the whole army crosses at one point, at full density.
- A watchtower on a raised ridge with the wind at its back: its archers outrange anything shooting into the wind.
- Hedgerows funnelling the column past a tower at close range: every regiment marches under its arrows.
- On a winter map, rain over a high pass falls as snow and loads the slope; a tremor as the column fills the pass releases the avalanche.
- Heat on a snowfield upstream, timed so the meltwater pulse reaches the ford mid-crossing.
- Wind drifting snow into a hedgerow line's gaps during the battle: the funnel closes behind the vanguard and splits the army.
- Boulders set on a slope above a narrow road, then a tremor as the column fills it: they roll down the fall line through the densest stretch.
- Wind driving a wildfire toward a river the column is crossing: the regiments trapped between the front and the water have nowhere to run.
## Staple funny situations (starter set, candidates)
 
Situations to build first: each is funny on screen, scales with the attacker's mass, has a visible warning and a counter, and needs only systems from the early milestones (implementation plan M3–M6). Together they can carry the first campaign chapter and the first trailer. The death gags they end in are in `claude/unit-spec.md`. Candidates, not decisions.
 
**Funny without killing (shape first)**
1. **Rutted road, stuck siege (M3).** Thousands of feet churn a wet road; the vanguard strides through and the siege train at the back sinks to its axles, wheels spinning, crews heaving, mud spraying. Warning: the road darkens as the ruts deepen. Counter: a firm road or a different route. Funny because the army does it to itself.
2. **The polite queue (M5).** A grazing herd wanders onto the road and 8,000 soldiers stop and wait in a column that backs up for minutes. Stage two: lightning in the brush behind the herd, and the queue becomes a bowling lane (see 7). Warning: the herd's grazing path in the preview. Counter: drovers, or a route round.
3. **Ice rink (M5 with fixed ice, M6 with cold snaps).** Running or charging units on ice slip and spin, skidding into each other like bowling pins and lying stunned; nobody dies until the crowd gets too heavy. Warning: arms out, wider as the load grows. Counter: march in file, or wait.
4. **The accordion (M3).** A fallen tree or boulders stop the vanguard; the rear keeps marching, the column squeezes up like an accordion, and the regiments in the middle bunch into one block. Payoff only if it happens on a riverbank or a bridge (5, 6). Warning: density tint along the column.
**Funny and lethal (kill second)**
5. **Moat opening (M3).** The column crosses a dry channel the god dug; rain fills it behind the vanguard; the next regiments arrive at a moat, the rear keeps pushing, and the front rank teeters, windmills and plops in one by one. Warning: water rising in the channel, teetering soldiers. Counter: wait for it to drain, fascines.
6. **The bridge conga (M5).** Hedgerows funnel the whole column onto a rotted bridge; the vanguard crosses and cheers, the span creaks and sags, the bulk crowds on, the middle drops: waving arms, shields drifting downstream, half the army stranded on the near bank. Warning: creak, crack and sag stages. Counter: crossing marshals, a second crossing.
7. **Stampede express (M5).** Lightning in dry brush behind a grazing herd as the column passes: the herd bolts across the road, soldiers fly with long hang time, a few land in the pond, the rest lie stunned or hoof-printed flat. Warning: the herd mills and raises dust. Counter: rain (wet brush won't catch), drovers.
8. **Flash-flood ford (M3).** Rain far upstream under a clear sky; the ford the column chose rises mid-crossing; regiments in the middle wave their arms and go under, and helmets float past the ones still on the bank. Warning: the river gauge in the preview, the water's edge creeping up. Counter: wait for the river to drop, a bridge.
9. **Strike! (M6).** A tremor on the slope above a switchback road: boulders roll down the fall line through every tier of the column, pancaking some and bowling the rest aside, with a bowling-alley crash as they hit. Warning: rumbling, dust, the boulder path in the preview. Counter: spread out on the switchbacks, sappers.
10. **Dam-break surf (M6).** A dam built in phase 2, rain over the reservoir, a tremor as the column fords below: the wave front carries a whole regiment downstream, a few surfing on their shields, and dumps a dazed heap on a sandbar. Warning: the reservoir gauge, the dam's cracks. Counter: ford elsewhere, crossing marshals.
11. **The fire chases them home (M6, M7 with Firebrands).** The attacker's firebrands light a hedge, the god shifts the wind, and the fire turns back down the road; the column runs back the way it came, trailing soot-black soldiers who crumble to ash. A turnabout, so the best PvP clip. Warning: smoke bending with the wind. Counter: don't burn upwind of yourself.
12. **Siege toboggan (M6, needs freezing rain or glazed slopes).** The siege train climbing an iced slope slides back down into its own column, bowling it over, wagons spinning. Warning: wheels slipping on the climb. Counter: wait for heat, sand the road (a specialist).
 
**Rules some of these need (open)**
- Cavalry hitting deep mud at a charge: do the horses stop dead and throw the riders forward over their heads (a launch, mostly stun)? It would make rain the cavalry counter in one funny beat.
- Ice slipping for running and charging units (3): a stun-only rule, separate from the load check.
- Herds caught in water, ice or fire: do the animals die like soldiers, or always paddle off and wander away unharmed? It sets the tone of every stampede gag.
