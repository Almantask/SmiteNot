# Terrain, weather and army obstacles: brainstorm (2026-10-09)

This is the output of a 40-agent brainstorm. Twenty agents covered terrain, weather and the other obstacles armies run into, and twenty covered god powers. Each agent had its own lens (rivers, cold, logistics, history, catalysts, PvP counters and so on), and all were asked for realistic coverage and fun. About 340 raw ideas were merged into roughly 200.

- "×N" means N agents raised the idea independently.
- "IB n" means `claude/idea-bank.md` already lists it as idea n, which counts as one more independent vote.

Everything here is a candidate, not a decision. `claude/game-brief.md` wins any conflict, and the settled seeds in `claude/power-palette.md` aren't repeated here.

## The short version

1. **The attacker's own feet are the best weapon (×10+).** Ten lenses raised the same idea: ground remembers traffic. That covers ruts, churned fords, broken salt crust, snow packed to ice, compacted lanes that channel runoff and dust plumes. One cheap per-cell traffic count makes every wet, crusty or snowy surface get worse with army size, which is the premise of the game.
2. **Cold air flows like water (×12).** This was the most-converged idea of the run. Chill a hilltop far from both armies and the dense air drains downhill. It pools in hollows, freezes mud, and traps fog and smoke under an inversion lid. It obeys the 100 m rule naturally, can reuse the water solver, and reads at a glance: a white lake in the valley, your ridge in the sun.
3. **Rain far away, flood here (×10).** A cloudburst on a distant catchment sends a flood down the dry wadi the attacker picked as its road, minutes later, under a blue sky. The cause is remote, the travel time shows on the timeline, and no stored water is needed. Karst conduits and groundwater do the same thing underground.
4. **The AI plans from what it has seen (×8).** About half the deception ideas need one shared system. Examples are false crests, ditches under floodwater, grikes under snow, mirages, sand skins over bog and a bridge burned behind a ridge. The system is a per-army belief map (what was scouted vs what is true), plus an overlay showing where the two differ. With it, deception stays physical and readable.
5. **A day clock unlocks about 15 ideas.** Examples: dawn fog that burns off on schedule, ground that thaws by 10:00, sea breezes that flip at dusk, low sun in the archers' eyes, night halts. One level-set clock (sun angle, light, shade) is cheap and turns time itself into a lever.
6. **Bodies wear out on battle timescales.** Thirst, soak and chill after fording, stamina in heat and mud, and arrows left in the baggage are each one number per regiment. Attrition that really takes hours goes into the level's "long approach" as starting values, so the 1–15 minute battle only handles what's fast.
7. **Siege engines are the tall, heavy, fragile thing (×8).** Several agents independently found failures that only siege suffers:
   - towers leaning on softening ground
   - wagons sliding back down glazed slopes
   - axles snapping on frozen ruts
   - fords too deep for axles
   - hairpin bends too tight to turn

   That gives siege its own counterplay instead of making it "slower infantry".

8. **The load system is the workhorse.** Salt crust, quaking bog mats, cornices, cut banks, geyser sinter, pingos, scree aprons and swaying bridges all reuse the ice and bridge load check. What changes is the trigger: pressure instead of weight, accumulated traffic, or the rhythm of marching feet.

## New systems these ideas need

| System | Ideas it unlocks |
|---|---|
| **Traffic count per cell** (decays slowly) | Ruts and mud, crust and bog-mat damage, snow packed to glaze, compacted lanes that channel runoff, dust plumes, desire paths, poached fords (about 20 ideas) |
| **Temperature that flows downhill, plus an inversion-lid bit** | Frost hollows, valley fog, smoke pooling, katabatic tailwinds, dusk inversions, frozen ruts (about 12) |
| **Groundwater layer** | Water-table rise, spring lines, poljes, artesian caps, sand boils, wells, late river crests (about 10) |
| **Off-map catchments with a travel-time flood curve** | Wadi floods, slot canyons, monsoon bursts, debris flows (about 8) |
| **Day clock: sun angle, light, shade** | Thaw clock, glare, fog burning off, breeze flips, dusk deadlines, night, mirages, shadow lines (about 15) |
| **Belief map per army (seen vs true)** | False crests, hidden ditches and grikes, mirages, skins over bog, stale routes, flat light, ghost camps (about 12) |
| **Per-regiment body state** (thirst, soak, stamina, ammo) | Wells, chill after fording, heat fatigue, spent quivers, the long approach (about 10) |
| **Scent and noise as plumes on the wind** (cheap: reuse smoke drift) | Camel lines, predator musk steering herds, acoustic shadows, roaring rapids, howling gaps (about 8) |

Recommended to skip or defer:

- A salinity field. Only two ideas use it, and both can use a "weaker ice" flag on estuary maps instead.
- CO₂ gas, because of the pillar problem below.
- Drifting ice floes and floating log rafts as full physics. They're fun but costly, so start with scripted floe and jam paths.

## Pillar calls

**Cut or reshape:**

- **Lethal CO₂ hollows** (mazuku, choke-damp, brimstone). The gas harms soldiers in one hop. At most, keep a non-lethal version where horses and oxen balk at the hollow; otherwise cut it.
- **Pyroclastic flow.** The set-piece agent rejected it too, as an "I win" button. Ashfall replaces it.
- **Fire whirls that move like tornadoes.** Keep any whirl confined to cells that are already burning.
- **Sundogs** (the "three suns" omen at Mortimer's Cross). It's pure morale; nothing physical changes.
- **Hold the Sun / Hasten Dusk as a cast.** It's global, so it skips the range rule. Make day length a level-set fact instead.
- **Frost making bows and blades brittle.** That acts on soldiers' kit. Keep brittleness for structures and siege engines only.

**Your call:**

- **Ambient insects** (horsefly belts, gadfly hatches, midges, locusts). Recommended: allow them only as fields bred by world state (warm, still, wet) that bother animals, never summoned or aimed. This answers the open "swarms" question narrowly.
- **Scent plumes spooking horses** (camels, predator musk). Recommended: allow them, since a horse reading a scent field is like an arrow reading the wind field.
- **Disease from foul water.** Recommended: only between PvP rounds or campaign levels, never inside a battle, or cut it.
- **St Elmo's fire on spear tips.** It's a harmless glow that makes pike blocks visible at night, on both sides. It's borderline because it sits on soldiers' gear.

**The omen question.** The brainstorm suggests an answer. Morale effects should come only from the AI correctly reading a real threat in the world, never from portents. Examples of real threats:

- the river rising behind the army, cutting off its retreat
- darkness
- a real herd's dust taken for cavalry
- a heap of the army's own dead

The test is simple: is there a physical fact the AI is reacting to? That keeps the pillar and makes fear learnable. "Water behind them" (#25 below) is the model.

## Top 25

Ranked on realism, the clip-worthy moment, mass scaling, pillar fit and reuse of existing systems.

1. **The army ruts its own road** (×10, IB 60). Every regiment over wet ground deepens the ruts. The vanguard strides through, and the siege train sinks in its own army's mud. Soil type sets how bad it gets: clay stays churned, chalk drains, and sand turns firmer when wet. *Agincourt 1415; Napoleon's Polish "fifth element", 1806–07.*
2. **Cold-air drainage and the inversion lid** (×12, IB 41). Chill a far hilltop. Cold air pours into the valley and freezes churned mud into ankle-breaking ruts. It also traps the army's own dust and campfire smoke under a lid until the sun breaks it. *Austerlitz 1805; Trasimene 217 BC; Meuse Valley smog 1930.*
3. **Distant cloudburst** (×10). Rain falls on a catchment off the map. Minutes later, under clear sky, the flood comes down the dry wadi the AI chose as its siege road, and the timeline shows the flood curve. *Alexander's camp swept away in Gedrosia, 325 BC; Big Thompson 1976.*
4. **Marching dust** (×6). Dust rises with feet × speed × dryness and drifts downwind. A big army blinds its own archers, and a wind shift turns the plume into their faces. *Cannae 216 BC.*
5. **Lockstep bridge** (×4). Gusts start a light bridge swaying, and a column marching in step pumps the sway until the cables part. The AI's counter, "break step", is real but slow. *Angers 1850, 226 soldiers killed; Broughton 1831.*
6. **Thirst and the one well** (×6, IB 95). Heat raises each regiment's thirst, and each well serves a fixed number of men per minute. Drain or silt the others, and 10,000 men coil around one wellhead, right where your flood lands. *Hattin 1187.*
7. **The living ford** (×3). Each ford has a depth limit per unit type (siege axles about 0.5 m, cavalry about 1.2 m). Traffic scours it deeper and an upstream crest arrives late, so siege is stranded on the wrong bank mid-crossing. *Trebia 218 BC.*
8. **Tide-window causeway** (×4, IB 36). A causeway or tidal flat is passable only near low water. Crossing time grows with army size, so the sea cuts off the tail, and the preview shows exactly which regiment. *Maldon 991; Moel-y-Don 1282; Morecambe Bay.*
9. **Windthrow domino** (×6). Rain loosens shallow roots, then a gale lays a hillside of trees across the road like dominoes. The result is a natural barrier of felled trees that only axes clear. *Teutoburg AD 9; the Great Storm of 1987 (15 million trees).*
10. **Foehn, the snow-eater** (×6). Moist wind over a ridge drops rain or snow on one side and becomes a warm, dry gale on the other. On the lee side, snow vanishes, ice rots, mud dries and fuel crisps: one ridge, two weathers. *Spearfish 1943, +27 °C in two minutes.*
11. **Raise the water table** (×4). Groundwater rises under firm-looking ground, and nothing shows until marching feet pump it up. The rear regiments then wade through what the vanguard walked on. It also works as a catalyst: rain turns to mud at once, and a tremor liquefies ground without rain. *Passchendaele 1917.*
12. **Sabkha crust** (×4, IB 126). A salt crust holds the scouts, but each regiment weakens it, and rain dissolves it into brine mud; heat re-seals it. The vanguard crosses, and the third regiment's siege tower lurches through. *Qattara Depression; Rann of Kutch.*
13. **Vineyard and orchard rows.** Rows are quick to move along and nearly blocked across. The direction the player sets combs a broad attack into single-file streams aimed at the killing ground. It needs only a direction-dependent movement cost per cell. *Hougoumont 1815.*
14. **Sun in their eyes** (×5). Break the clouds at the tick you choose. Low sun blinds archers facing it, and wet fields, snow or a millpond double the glare. Rain first, then sun, is the Crécy double. *Crécy 1346.*
15. **Fog fratricide** (×3, IB 110). In fog, regiments only recognise each other up close. One that appears from an unexpected direction is attacked as the enemy, and cries of "treason!" spread panic. *Barnet 1471.*
16. **Wind setdown and seiche** (×5, IB 88). A long gale piles a lake up at one end and bares the near shallows as a false ford. Calm releases it, and the lake walks back over the column. *Lake Erie seiche at Buffalo, 1844; Drews & Han's wind-setdown model.*
17. **Black line** (×2). A strip burnt from the first tick is the only fuel-free ground when fire threatens, so the AI funnels the whole army through the lane you drew. *Mann Gulch 1949; prescribed burns.*
18. **Breath of the horde.** In deep, still cold, breath and horse sweat freeze into ice fog. Its density grows with soldiers per hectare, so a big army marches blind in a cloud it made itself. *Ice fog at Fairbanks and Yakutsk.*
19. **Siege-killer family** (×8). Softening ground tilts towers until they topple, sometimes across your ditch as a bridge. Glazed slopes send wagons sliding back into the queue, and frozen ruts snap axles. *Rhodes 305 BC (the Helepolis bogged down); Hannibal's descent from the Alps.*
20. **Camel lines** (×5, IB 65). Picketed camels throw a scent plume downwind, and horses inside it balk and bolt. An all-cavalry army has to bet on the wind, and so does your own horse. *Thymbra 547 BC.*
21. **Quaking bog mat.** A floating peat mat fails under ground pressure, not total weight. Open-order infantry cross, while hooves, wheels and packed columns punch through. Each tear crowds the rest onto less mat. *Solway Moss 1542.*
22. **Burn scar to debris flow** (×2, IB 43). After a fire, ash and water-repellent soil make the next light rain run off as grey slurry down the burned slope onto the road. Fire at 0:40, rain at 2:10. *Montecito 2018.*
23. **Quake lake** (×2). A landslide blocks a gorge. The river below runs dry and lures the AI onto the bed, while the lake above drowns fords. Later the dam bursts, on a gauge the preview shows: two disasters from one cast. *Gohna 1893–94; Attabad 2010.*
24. **Attacker's eyes** (×8, with IB 13 and IB 17). The AI routes on what its scouts saw. Change the ground behind a ridge or under smoke, and it marches toward a bridge that isn't there. The overlay shows it as a dashed ghost, with a "!" where the AI finds out. *The Berezina, 1812.*
25. **Water behind them.** Rain far upstream raises the ford behind the attacker. Its retreat check fails, so the rear turns back and the vanguard fights alone. This is the model for morale from real threats. *Aspern-Essling 1809.*

## Other obstacles armies face

These aren't terrain pieces or powers; they're what goes wrong for real armies. In the game, the god can only reach them through the world.

- **Body:**
  - thirst (wells, heat)
  - soak and chill after fording (Trebia 218 BC)
  - stamina drained by slope × ground × heat × load (Agincourt)
  - heat inside a packed block (Hattin)
  - cold, and running out of firewood (the Berezina)
- **Baggage and ammunition:**
  - pavises, ladders and spare arrows trail behind in the wagons, so a mired baggage train leaves archers with one quiver (Crécy)
  - stragglers clog the road
  - one broken axle corks a causeway (Teutoburg)
- **Crowd:**
  - crush when the front stalls and the rear can't see it (Agincourt)
  - "faster is slower" at gaps when urgency rises
  - stop-and-go waves that deliver a dense lump onto ice
  - files spilling off a causeway (Stirling Bridge)
- **Command:**
  - orders drowned by rapids, wind or thunder (IB 8)
  - friendly fire in fog (Barnet)
  - halting at nightfall
  - "break step" on bridges
- **Greed:** granaries, cellars and villages pull regiments out of formation (Badajoz 1812; IB 50).
- **What the battle leaves behind:**
  - heaps of fallen at chokepoints
  - wrecked engines in the ford
  - wreckage jamming a river until it jumps its bed
- **Disease:** real but slow, so only between rounds or levels (Harfleur 1415).

## Unit-type hunters

| Unit | Through the world |
|---|---|
| Cavalry | Warren ground, camel lines, snow crust, grikes under snow, vineyard rows set across the charge, wild horse bands, polished ice trails |
| Siege | Leaning towers on softening ground, glazed slopes, frost-heave hummocks, ford depth limits, switchbacks and ledge paths, swelling clay, brittle frames in deep cold |
| Ranged | Low sun and glare, marching dust, soaking drizzle, low smoke pall, gap winds, the spent arrows they leave you |
| Infantry | The press, chill after fording, heat in a packed block, lockstep bridges, thirst |

## Realism coverage

| Setting | Best new ideas |
|---|---|
| Rivers and fords | Living ford, braided bars, meander cutoff, perched river, backwater confluence, cut bank, kolk |
| Wetlands and soils | Soil types with traffic memory, quaking bog mat, bog burst, reed beds, tidal quicksand, marsh gas |
| Mountains | Cold-air drainage, scree apron, cornice lure, switchbacks, ledge path, gap wind, freeze-thaw cliffs |
| Forest and crops | Vineyard rows, windthrow, crown fire, standing rye, coppice and maquis, old-growth canopy, bamboo |
| Desert | Distant cloudburst, sabkha, wells, mirage, slip-face dunes, stony bait lane, day-night swing |
| Cold and thaw | Mud-season clock, snow crust, road icing, lake leads and floes, pingo, whiteout |
| Coast | Tide windows, fast-flooding tidal flats, tidal bores, storm surge and set-down, sea fog, sand that rain makes firmer |
| Karst, caves, volcanic | Polje, swallow holes, artesian caps, sand boils, geysers, lahar, ashfall |
| Weather | Drifting storm cells, drizzle, sleet, wet snow, lake-effect bands, cold fronts, gust fronts, foehn, katabatic wind, inversions |
| Light | Low sun, night and firelight, moonshaft, dark day, snow glare |

Still thin:

- tropical forest (only the monsoon jungle chapter, bamboo and the canopy)
- towns and streets (one thatched village)
- steppe and savanna (grass fire, migration and the winter steppe chapter)
- humidity as its own variable (dew, drizzle, sultry calm); recommended: fold it into wetness and temperature

## Catalog: terrain pieces and map features

Top 25 entries aren't repeated below.

### Rivers and fords

- **Braided bars.** At low flow, a braided river can be forded almost anywhere. A small rise reconnects the side channels and strands regiments on gravel islands they took for the far bank. *Hydaspes 326 BC.*
- **Water line** (×3). Dig ditches across a low field, then flood it knee-deep. The water hides every ditch, and cavalry and wheels drop into 2 m drains. *Dutch Water Line 1672.*
- **Perched river and crevasse.** A river runs above its plain between natural levees. Breach a levee upstream and the river floods the low ground behind a column strung along the levee road. *Yellow River; Huayuankou 1938 (handle the tone carefully).*
- **Backwater confluence.** The main river floods far away and the tributary can't drain into it. The creek crossed dry an hour ago is now 2 m deep and flowing backwards. It comes free from the water sim.
- **Cut bank lip.** The outside of a river bend is a 3 m undercut bank over deep water. Archers take it as high ground, and their weight drops it into the pool.
- **Slumping ford.** Every regiment that crosses crumbles the soaked exit ramp a little more, until the far bank is a mud wall.
- **Stirling window.** A single crossing leads into a river loop. The preview plots the fraction of the army across, and an upstream release timed at 50% cuts the army in two. *Stirling Bridge 1297.*

### Wetlands and soils

- **Soil types** (clay, sand, chalk, loess, loam). The same rain makes different mud on each; this is part of the traffic field. Sand turns firmer when wet, so rain on a beach helps the attacker's cavalry: a turnabout.
- **Tidal quicksand flats** (×2). Moving men stay on top, but anything that stops sinks, so congestion is the killer, and the tide sets the deadline. *Mont-Saint-Michel on the Bayeux Tapestry; King John's baggage in the Wash, 1216.*
- **Salt-marsh creek maze** (×2). The flood tide fills branching creeks from the sea inward and shatters the marsh into islands. *Moel-y-Don 1282.*
- **Fen reed bed** (×2). Reeds 3 m tall block sight and split formations into files. They're fireproof while green and a firewall once dried. *Hereward at Ely, 1071.*
- **Marsh gas vents.** Churned peat releases methane, and a fire arrow skips blue flame across the bog so horses bolt. Keep it a small ignition bridge, not a bomb.
- **Mangrove belt** (wildcard). Prop roots absorb storm surge and tide, so attackers can only use creeks that flood twice a day.

### Mountains

- **Scree apron.** Loose stone that each footfall destabilises. Scouts cross safely; the 3,000th man starts the slide.
- **Cornice lure** (×3). A wind-built overhang looks like a solid crest. Archers line up on it, and it isn't there. It can chain into an avalanche.
- **Switchback stack.** The column folds into tiers on a zigzag road, so one boulder bounces through five tiers of the same army. *Napoleon on the Great St Bernard, 1800.*
- **Ledge path.** It allows single file only, siege must be dismantled, and the edge crumbles with wear. *Hannibal cracking the rock with fire and vinegar (Livy 21.37).*
- **Gap-wind nozzle** (×3). A notch in a ridge speeds any wind into a jet. Arrows shot into it fall short, and fire upwind of it becomes a blowtorch. *The Bora at the Frigidus, 394.*
- **False crest.** A convex fold hides the ground beyond, and the AI commits based on what it can see. Needs the belief map. *Wellington's reverse slopes.*
- **Gully debris flow.** A distant storm sends mud and rock down the one gully the road follows.

### Forest, crops and vegetation

- **Crown-fire pine plantation** (×2). Resin and needle litter lift fire into the treetops, which throw embers 100–300 m downwind, across rivers. A belt of broadleaf trees is the real firebreak. *Peshtigo 1871.*
- **Rice paddy terraces** (×2, IB 96). Units funnel onto the narrow earth banks between paddies. One bank gives way and six terraces empty downhill in sequence. *Siege of Oshi 1590.*
- **Standing rye** (×3). Head-high grain hides 800 pikes until 20 m away. Trampled lanes funnel the regiments behind, and a storm flattens the crop. *Quatre Bras 1815.*
- **Coppice and maquis.** Dense coppice breaks a regiment into lost clumps. Maquis (Mediterranean scrub) keeps sight open but burns violently. *The Wilderness 1864.*
- **Bamboo grove** (×2). Burning stems burst with loud bangs, so fire becomes a stampede trigger. Bamboo also regrows over cut paths.
- **Old-growth canopy.** Old oaks keep the forest floor dry, so your archers under them keep shooting through a downpour. *Teutoburg.*
- **Strip-field harvest.** Unreaped strips of grain burn like fuses between safe stubble, funnelling the attacker into corridors.
- **Leaves on the line.** Wet leaves on sloping lanes turn glassy and clog culverts.

### Deserts

- **Slip-face dune** (×2, IB 118). Crowd weight on the steep lee slope starts a sand avalanche, and booming dunes spook horses. Over a long battle, the wind walks the dune onto a road. *Badain Jaran.*
- **Stony bait lane.** One strip of firm stony desert (hamada) runs through soft sand. Everything funnels onto it, and it can end in sabkha.
- **Qanat line** (×2). A buried water channel shows on the surface as a line of shaft mounds. Water sent into its source well fountains from the shafts, and the soaked roof caves in under siege.

### Cold and thaw

- **Lake ice leads and floes** (×3). Wind opens cracks of open water and pushes up pressure ridges, and a loaded floe drifts off with its regiment. *Charles X's march across the Belts, 1658; Lake Erie 2009.*
- **Road icing.** A seep in frost spills over the road and freezes in layers (aufeis). A trebuchet wagon slides back down a hill of glass.
- **Pingo.** A knoll with an ice core. The enemy's archers claim it, until heat slumps it into a crater pond.
- **Frozen waterfall** (wildcard). An ice ladder for light infantry; warm it and the curtain collapses into a field of ice blocks.

### Coasts

- **Fast-flooding tidal flats** (×2). The tide fills the gullies first, behind the troops, stranding them on shrinking islands. *Morecambe Bay.*
- **Sand vs shingle beach.** Damp sand is the best road on the map, while dry sand and shingle are the worst, and the tide eats the good band.
- **Hollow headland.** Sea caves undercut the cliff path during the battle, and blowholes boom as the warning.
- **Derelict hulk** (×3, with fire rafts). A wreck refloats at high water and drifts, burning, into the pontoon bridge. *The Antwerp hellburners, 1585 (fire only, no gunpowder).*

### Karst, caves and volcanic ground

- **Polje** (×3, IB 6). A flat karst basin that floods from below with no visible inflow. *Lake Cerknica.*
- **Swallow hole and resurgence** (×3, IB 6). Water poured into a hole far away bursts from a spring mid-battlefield after a known delay.
- **Limestone pavement** (×2). Bare slabs cut by knee-deep fissures (grikes) where horses break legs, and fresh snow hides them. *The Burren.*
- **Slot canyon.** A 4 m-wide shortcut that wagons can't enter. Rain on the wide catchment above turns it into a wall of floodwater. *Antelope Canyon 1997.*
- **Bentonite badlands** (×2). A maze of gullies where a drizzle turns the clay to "gumbo": glue on the flats, ice on the slopes.
- **Artesian cap.** A pressurised aquifer under thin clay. The first heavy load cracks it into a fountain and a boiling sand pit.
- **Sand boils.** Seepage under a dam or levee bubbles up on the dry side and washes it out from below. Engineers ring the boils with sandbags. *Teton Dam 1976.*
- **Geyser basin** (×2, IB 102). Eruptions run on a readable clock, steam drifts as fog, and the mineral crust has a hidden load limit.
- **Pitted ground** (×3, with warrens). Hidden pipes or burrows open up under rain, and charging horses fall. *Bannockburn's pits, 1314.*
- **Mud volcano** (wildcard ×2). A slow, unstoppable tongue of mud that you can only wall in and steer.

### Human landscape and props

- **Polder with windpumps** (×3, IB 35). Calm stops the sails and the field seeps back to marsh; in a gale, the brake catches fire.
- **Holloway** (×3). A sunken lane that hides a drop, channels runoff into a torrent, and fills with fog and cold first. *The Ohain lane at Waterloo.*
- **Hay ricks** (×2). Hay that is soaked and then warmed catches fire on its own minutes later, so the enemy's rain lights your fire.
- **Thatched village.** Lanes force regiments into files, fire runs from roof to roof, and cellars pull regiments out of formation.
- **Lime kiln.** Wet quicklime boils into steam and sets the yard alight, so their rain starts your fire. *Sandwich 1217.*
- **Timber yard and log drive** (×4, IB 98). Logs roll, float and jam against bridge piers until the bridge goes. *Morgarten 1315.*
- **Fog bell.** In thick fog, lost regiments steer toward the tolling bell, across whatever lies in between.
- **Rope ferry** (wildcard). It carries one regiment per trip, until a surge snaps the cable.

### Wildlife

- **Startled flight** (IB 55). A rookery or goose-filled reed bed bursts into the sky when a regiment passes, revealing its position and heading. *Sun Tzu, chapter 9.*
- **Migration** (×2, IB 19). A great herd crosses on a schedule and swerves toward fresh grass after a burn or rain.
- **Warren ground** (×3, IB 112). Burrows break horses' legs at the gallop, not at the walk.
- **Wild horse band** (×2). Mares break ranks to join the wild band, and a lancer wing dissolves into the hills.
- **Animal exodus** (IB 55). Wildlife flees before a queued hazard. This is the warning system, and it can be faked.
- **Poached ford** (×2). In heat, cattle stand in the ford and trample its banks into mire.
- **Beaver colony** (IB 67). Its ponds spread across the approach between PvP rounds.
- **Goat ledge** (×3). Goats on a cliff kick scree onto the road, and a startle sends the whole slope down.
- **Rooting boar.** Boar loosen turf so rain makes mud twice as fast, and squealing pigs panic war elephants. *Megara, 3rd century BC.*

## Catalog: map weather and climate (level-set)

- **Day clock.** Sun angle, light and shade; most of this list needs it.
- **Freeze-thaw clock** (×4). Ground is frozen at dawn and bottomless by 10:00 as the topsoil thaws over still-frozen ground (the Russian rasputitsa, or mud season). A cloudy night stops the refreeze. *The Mongols turning back short of Novgorod, 1238.*
- **Daily wind clock** (×3). Sea and valley breezes flip at set times, swinging every smoke plume and fire on the map. *Salamis 480 BC.*
- **Morning mist.** Dawn fog in hollows burns away from the sunlit edges at a predictable minute.
- **Shadow line.** Ridges and woods keep ice and snow hard on their shaded side while sunlit stretches soften, so terrain pieces now matter for the shade they cast.
- **Sea fog.** Sea fog (haar) rides the onshore breeze inland and pools in hollows.
- **Monsoon onset** (×3). A humidity clock bursts at a known tick. After that, all rain runs off and fords close one by one.
- **Desert day-night swing.** 40 °C at noon, near freezing at night, and dawn dew softening the salt crust.
- **Whiteout** (×2). Overcast sky over snow erases shadows. Regiments drift in arcs, and the AI only avoids hazards it can see.
- **Night, moon and firelight.** Light falls with the moon phase, cloud and fog, and troops in front of fires become silhouettes.
- **Sky tells.** Every queued weather power first shows its real warning signs (a halo before a front, towering cloud before a storm). The AI reads them, and PvP gods can fake them. IB 27, the forecast strip, is a close cousin.
- **The long approach.** The level states how the attacker arrived (forced march, a night under arms, a desert crossing), which sets its starting thirst, stamina, soak and ammo. *Hastings 1066; Gaugamela 331 BC.*

## Catalog: god powers by field

### Water

- **Kolk in the ford.** A whirlpool digs a hidden hole mid-ford while the approaches stay shallow, and the front rank steps in over its head. *Dutch "wielen" scour ponds.*
- **Meander cutoff** (×2). At flood stage, breach the neck of a river loop. The peninsula becomes an island and the bridge spans dead water. *Vicksburg 1876.*
- **Grown ice jam** (IB 104). Supercool the rapids into an ice dam. It floods upstream first, then releases a surge of floes downstream.
- **Fuse-plug levee** (×2, IB 53). A weak section of levee washes out when the river passes a mark you set, so other powers, or the rival's rain, light the fuse.
- **Half-across tripwire** (IB 18). Arm a sluice on a world condition such as "ford load above 40 t", with range checked at the release site, so the crest lands mid-crossing. The agent's rule: trigger only on world fields, never on "when unit X arrives". *Han Xin's sandbag dam on the Wei River, 204 BC.*
- **Open the sluice.** A small pulse runs down a millrace at walking pace: the teaching version of a dam break.
- **Wave stacking** (wildcard). Release several ponds at staggered ticks so their crests merge at one bridge.

### Ground

- **Unzipping fault.** A rupture runs along a mapped fault at a readable speed. It leaves a 1–3 m step that shears roads and bridges and cuts the column. *Kaikōura 2016.*
- **Tilt the block.** A block of land rotates slowly; still water slides off one side and the low edge floods. *New Madrid 1811–12.*
- **Head-cutting gully.** The head of a gully eats upslope three times faster on ground the army has trampled, chasing the siege train uphill in the rain.
- **Swelling clay flat** (×2). Wet clay heaves the road into axle-snapping ridges, and dry clay cracks into polygons that lame horses.
- **Creeping hillside.** A soaked slope beside the road creeps and tilts it until tall engines pass their tipping angle.
- **Quick clay.** Flush the salt out of a marine clay terrace. It holds until total footfall passes a threshold, then the whole field slides away. *Rissa 1978.*
- **Frost wedge** (×4). Rain, freeze and thaw cycles fill a crack meter on a cliff, arch or bridge pier until it sheds rock onto the road.

### Vegetation

- **Root-mat causeway.** Sow binding grass over mud, and a firm path knits in minutes, then tears under traffic. The vanguard crosses and the rear sinks; if the attacker reroutes onto your road, it tears under them.
- **Creeping vine front.** Vines spread a few metres a minute and reach the road as the column does: growth that legally creeps into the 100 m buffer.
- **Thorn belt.** Blackthorn or bamboo grows across a gap on the timeline. Cutting speed depends on the width of the face, not on headcount. *Tofrek 1885 (thorn-bush zariba).*
- **Strangler roots.** Roots pry a stone pier apart (rot for stone), or bind a riverbank against scour.
- **Green bloom.** Algae make stone fords slick, and duckweed hides a pond as a lawn the AI paths across.
- **Windfall mast.** Ripen an orchard and herds gather on the road for the fallen fruit; fermented windfall makes them spook easily.
- **Burr drift.** Seeds ride the wind, water or herd fur and sprout where the flow slows, inside the buffer. It's a way to deliver other plants.
- **Standing harvest.** Ripen fields head-high to hide troops, block sight and tempt foragers.

### Fire

- **Grass flash fire.** A thin, fast front that burns out in under a minute. Horses scatter and black ground is left behind.
- **Uphill chimney run.** Fire speeds up with slope, faster still in narrow ravines, and outruns siege on the switchbacks. *Mann Gulch 1949.*
- **Smoulder pall.** Wet fire lays its smoke low, so the enemy god's rain on your fire blinds their own crossbowmen.
- **Ember spotting** (×2, IB 26). Embers land 100–400 m downwind, so the river the enemy trusted as a firebreak burns on their side. *Black Saturday 2009.*
- **Firestorm** (×3). A big fire draws air inward, pulling smoke, fog and side fires toward itself, so it can close a lane by pulling two flank fires together. *Hamburg 1943.*

### Air and pressure

- **Drifting storm cell** (×2, IB 1). Rain falls in a swath that drifts with the wind. You cast it upwind, and any wind shift re-aims it.
- **Gust front** (×4). A storm off the map collapses, and its cold outflow sweeps the plain as a visible arc. Regiments brace and halt wherever they are, including on ice.
- **Virga downburst.** Rain evaporating in hot air plunges as a ring of cold gusts that fans fire and rocks siege towers.
- **Cold front passage.** A squall, a 90° wind shift and a 10–15 °C drop sweep the map. Fires swing direction, and mud freezes into ruts by night. *The Great Blue Norther, 1911.*
- **Falling glass** (IB 34). A pressure drop pulls drifting weather toward a chosen spot. It's a strong multiplier; show the pressure lines.
- **Thunderhead life cycle** (×2). Seed a cloud over hot ground. It grows on the heat, rains while drifting, and collapses into a gust front in the mid-afternoon.
- **Acoustic shadow** (×5, IB 8). Wind and temperature bend horn and drum signals away from part of the field, so regiments there act on stale orders. *Iuka 1862.*

### Temperature, snow and ice

- **Shepherd clouds.** Drifting cloud shadows cross a sun-baked plain. Overheated regiments follow the shade, and you herd them onto chosen ground.
- **Hot spring beneath** (×4, with warm seep). Warm a lake from below, and its ice rots from underneath while the top looks solid; steam wisps are the warning. A warm seep draws the exact line where the ice will fail.
- **Snow crust.** A sunny thaw followed by a hard night crusts the snow. Men on foot cross, while hooves and close order break through.
- **Polished track.** The column packs its own trail, and a midday thaw then a hard freeze turn it to glass between deep drifts. *Smooth-shod French horses on icy roads in the 1812 retreat.*
- **Weight slab** (×2). Wind or frost builds a hidden weak layer in the snowpack, and the column's own weight releases it. *White Friday, Dolomites 1916.*
- **Heavy wet snow.** Sticky snow snaps branches into deadfall and packs to glaze under boots.
- **Lake-effect snow band** (×4). Cold wind over your unfrozen pond paints a narrow wall of snow downwind. Turn the wind and the band sweeps across their march.
- **Sleet and refreeze.** Slush now, frozen ruts later. It's double-edged, since a hard frost can also rescue siege from mud.
- **Soot on the snowfield.** Darken a snowfield and it rots by noon. *Dust-on-snow melt in the Rockies.*
- **Ice shove.** Sun and an onshore wind drive lake ice ashore as walls across the shore road. *Mille Lacs 2013.*
- **Floe ferry.** Thaw a seam across river ice; under load, the slab breaks off and drifts away with a regiment aboard.
- **Frost-heave hummocks** (×2). Wet ground plus hard frost lifts knee-high hummocks, leaving no level road and no firing bed for engines.

### Light and sky

- **Moonless night.** Thicken cloud at night. Regiments lose sight of their banners and of hazard warnings, and walk onto prepared ground. *Epipolae 413 BC.*
- **Moonshaft.** A moonlit patch drifts like a searchlight, and the AI bends around lit ground into the bog.
- **Strobe storm.** Dry lightning lights the field every few seconds, revealing night columns. Strikes hit only trees and fuel.
- **Snow glare.** Glare builds up over snow or flooded meadows, and dazzled regiments drift toward shade. *Xenophon, Anabasis IV.5.*
- **Dark day** (×2). A high smoke or ash pall turns noon amber, then black, and the AI lights torches in the dry grass. *New England, 1780.*
- **Mirage** (×5, IB 120). Heat over a flat pan shows a phantom lake, and the AI detours around it into the real bog beside it. *Monge explaining mirages to Napoleon's army in Egypt, 1798.*

### Animals, through the world

- **Buffalo trace.** Seed salt and grass in a line. A herd walks it into a trail, and the attacker's pathing follows the trail into the marsh.
- **Green flush.** Green a distant meadow and the migration bends toward it, crossing the road for minutes.
- **Salt lick** (×2). It parks a herd exactly where you want, which fixes the starting point of a later stampede.
- **Landscape of fear.** A wolf den's musk drifts on the wind. Herds slide crosswind away from it like a curtain you steer with wind shifts. *Yellowstone elk avoiding wolf zones.*
- **Phantom ambush.** A gust flushes rooks from an empty wood, and the AI halts for an ambush that isn't there.
- **Gadfly hatch, fly belt, locusts** (×3). These depend on the swarm question; see Pillar calls.

### Catalysts (cheap, weak alone, unlock combos)

- **Seed the clouds** (×2). The next rain cast into the charged sky is doubled and arrives sooner. In PvP it doubles as a bluff: a grey sky that may never rain.
- **Charge the air** (×2). The storm's next strike lands on the glowing oak instead of at random, felling the tree on the exact tick.
- **Banked ember** (×5, IB 16). A fire smoulders underground until wind, drying or boots feed it air.
- **Stress the fault** (wildcard). Filling a placed reservoir triggers a tremor by itself, so rain becomes a quake clock. *Koyna 1967.*

Raise the water table, the inversion lid, salt lick, warm seep and weight slab also work as catalysts.

### Delayed and triggered

- **Lagging crest** (×3). Rain falls before the battle, and the river keeps rising after the sky clears. The preview needs a river gauge.
- **Root-sodden windthrow.** Soak a forest's soil now, and any later gust over the threshold drops the trees.
- **Dusk inversion.** Your hillside fire vents all afternoon, then at dusk the lid caps the valley and the smoke pools over their camp.
- **Dormant seedbank** (wildcard). Invisible seed bursts into thicket only where rain and warmth overlap for two minutes, so you plant a recipe rather than a place.

The half-across tripwire, fuse-plug levee, soot on the snowfield and banked ember belong here too. The agent's design note: arm triggers on world fields (load, water level, temperature), never on a unit arriving. That settles the brief's open question about event-pinned casts.

### PvP counters

- **Föhn gap.** Your gap turns the rival's rain into a hot, dry wind on their own valley. Its strength is capped by the rain they spent, so cost parity is built in.
- **Frost hollow (counter).** It freezes the rival's mud trap firm, so your army arrives early and their trap fizzles.
- **Weed-choked river.** Weed slows the river, so the rival's timed flood arrives late and flat, perhaps on their own baggage. Priced per metre of river.
- **Instruments of foresight.** A river gauge, seismoscope or weathercock plots one field's future at one spot, including hidden casts. The reading is exact but local, visible to the rival next round, and can be faked. *Zhang Heng's seismoscope, AD 132.*
- **Beacon Hill.** Your AI garrisons the beacon, and that regiment's 100 m bubble makes any hidden cast there fizzle.
- **Frost heave.** Too slow to aim at hidden pieces, so it only breaks the rival's older, visible walls and piers. It punishes static plans by design.
- **Peat sponge.** A bog that soaks up the rival's first flood surge; its capacity is priced by volume.

### Helping your own army

- **Rain-shadow ridge.** Steer a storm over a ridge so it rains on the attacker's slope while your archers shoot from dry grass. *Crécy.*
- **Katabatic spill.** Chill a hollow behind your line, and the cold wind draining downslope becomes a tailwind for your archers.
- **Drifting shade.** Cloud shadow over your line keeps it cool on heat levels, and the AI counterattacks when the sun-baked enemy is spent.
- **Timber raft.** Felled trees jam into a walkable raft for your retreat, which rolls apart under pursuing heavy troops.
- **Foehn corridor.** A warm wind dries one lane through snow or mud from your camp to their flank, and your cavalry sortie down it.
- **Backburn line.** A slow fire lit into the wind leaves firm black ground: a firebreak, and an open killing field.
- **Clean-water rill.** A seep crosses your lines first; downstream, 10,000 boots churn it undrinkable.

### Set-pieces (huge, rare, map-dependent)

- **Glacial outburst flood** (IB 56). A lake dammed by a glacier fills on a gauge until the ice dam floats and fails (a jökulhlaup). The flood leaves house-sized ice blocks as a maze. *Grímsvötn 1996.*
- **River avulsion** (×3, IB 48). Breach a river that runs above its plain, and the whole river moves for good. Very double-edged.
- **Storm surge and set-down** (×2). An offshore gale piles the sea over the dykes; the reverse wind exposes the seabed as a lure. *St Elizabeth's flood, 1421.*
- **Lahar** (×3, IB 92). Rain on ash slopes sends a slurry kilometres down the valley. It sets like concrete and becomes a road afterwards. *Armero 1985; Pinatubo.*
- **Ashfall.** A volcanic plume drifts downwind; wet ash collapses bridges and roofs and buries pasture. It replaces pyroclastic flow.
- **Supercell over the headwaters.** One storm cell brings a flash flood, a downburst that flattens forest, and lightning fires.
- **Tsunami drawback.** The sea withdraws, the AI takes the dry harbour floor, then the wave arrives. *Lisbon 1755.*

## Teaching ladder (early campaign)

One concept per power, in order. One agent suggested that each power later "grows up" into its full version, which is worth keeping.

1. **Spill** teaches that water flows downhill; it grows into spring and rain.
2. **Spark** teaches the 100 m rule: the first spark fizzles, and the player relights it at the far hedge two minutes earlier. It grows into wildfire.
3. **Gust** teaches that wind carries things; it grows into wind shift.
4. **Wet line** teaches that wet stops fire, and shaping; it grows into rain.
5. **Loosen a plank** teaches weight per unit; it grows into rot.
6. **Open the sluice** teaches travel time (scrub until the crest meets the column); it grows into dam break.
7. **Chill a pond** teaches that crowds break what scouts cross; it grows into cold snap.
8. **Nudge a log** teaches three-step chains; it grows into beaver dams and log jams.

## Campaign chapter seeds

Each biome teaches one skill.

| Chapter | Skill it teaches |
|---|---|
| Desert wadi | Remote cause, late arrival |
| Alpine pass | Altitude bands: one storm is snow, ice and rain down a single column |
| Polder delta | Controlled flooding, with the dike tops as funnels |
| Karst highland | Hidden plumbing: which hole feeds which spring |
| Boreal thaw | The day-night freeze cycle ("no clouds means frost, and frost means a road") |
| Volcanic valley | Multi-step chains: ash plus rain makes lahars |
| Monsoon jungle | Aiming a free weather clock (the 2 p.m. storm) |
| Winter steppe | Wind as a conveyor: drifts hide gullies |

## Wildcards

- **Brocken spectre.** Low sun throws giant, haloed shadows of your ridge troops onto valley fog (touches the omen question).
- **Fata Morgana** (×4). A mirage lifts and doubles distant shapes, making your small army loom or showing a phantom host (touches the omen question).
- **Mountain-wave rotor.** Strong wind over a ridge parks a fixed strip of chaotic gusts downwind.
- **Snow rollers.** A snowball grows as it rolls and ends as a snow wall.
- **Hungry mycelium.** A fungus that eats only fresh-cut timber, so the attacker's brushwood bundles and pontoons run on a timer.
- **Beer flood.** A quake bursts brewery vats down a town street, and the puddles then lure looters. *London 1814.*
- **Divine wind.** The attacker's fleet ties up at anchor, and a typhoon makes the ships smash into each other. *Hakata Bay 1281.*
- **The attacker's own beef herd.** Stampede their dinner through their rear.
- **Sinking coast.** A quake drops a coastline a metre, and later tides drown a forest into a ghost marsh. *Cascadia 1700.*
