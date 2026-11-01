# Unit brainstorm (2026-10-09)

Output of a 10-agent brainstorm on new unit types: what each looks like, what it does and what it costs. Each agent took one angle (defender specialists, attacker answers, heavy and mass, light movers, ranged and wind, siege and builders, command and morale, beasts, toy box, economy). The 80 raw ideas merged into 54 units; "xN" means N agents raised the idea independently. These are candidates, not decisions; `claude/game-brief.md` wins where they conflict. Interactive version: https://claude.ai/artifact/8uSdU3shv3GeXJZyNyA1Ao

**Cost baseline (a brainstorm assumption, not a decision):** prices are in bodies, the PvP army currency, where a regiment of 100 plain line infantry costs 100 bodies. Where agents priced the same idea differently, the merged figure is a recommendation.

## Shortlist

| Unit | Why |
|---|---|
| **Throwback Pikes** (both) | A charge becomes the defender's launcher, and it anchors the counter loop against every launcher in the roster. |
| **Testudo Cohort** (attacker) | The defender's archers become a lever on the load system without a single cast inside the 100 m bubble. |
| **Stilt Slingers** (both) | Four of the ten agents invented it independently. The god's flood becomes the defender's strongpoint, and gales, ice and stampedes all answer it. |
| **Hill Wheel** (both) | Terrain shaping becomes aiming, which is the god fantasy in one object, and it costs one simulated body. |
| **Hounds** (attacker) | Three agents found it. The defender now wins by hiding, so the attacker needs a finisher, and rain and rivers are the god's answer. |
| **Straw Legion** (defender) | It answers the reveal rule (every failed round shows the attacker more of the defence) with one fresh doubt per round, at almost no simulation cost. |
| **Rally Drum** (defender) | Fleeing off the map counts as wiped, so where broken regiments run is one of the defender's biggest problems. This makes it a placement decision the weather can ruin. |
| **Crossing Marshals** (attacker) | The best attacker answer: it blunts the game's signature load trap without deleting it, moving the danger to the bank and charging sunset clock for it. |
| **Remount String** (attacker) | The attacker buys his own stampede, and it runs into his own column. It costs almost nothing to build. |

Runners-up: Kite Watchers, Pilum Throwers, Leadfoots, Wagon Fort, Field Kitchen.

## Rule questions these ideas raise

Several of the strongest ideas lean on rules the brief hasn't settled. Each needs a decision before the unit can be designed properly.

1. **Settled 9 Oct: units may change the world.** Firebrands, Spademen, Sappers, Fascine Carriers, Wheel-Breakers, the Trebuchet and Drovers are all legal. Working rules: fixed regiment rules the preview shows as markers on each regiment's lane; a set number of charges per battle; fire only in dry fuel, wind away from their own side, never within 30 m of any soldier; every result clearable and gap-checked; herding at walking pace only, never starting or steering a stampede. Recorded in the brief.
2. **Settled 10 Oct: specialists only.** World-shaping belongs to specific unit types, each with its own action, charges and seat lock; line regiments can't buy a kit. Specialists lose unspent charges when they fall, so both AIs treat them as priority targets. Still open: whether fire also costs energy, and which specialists make the first roster (recommended seven: Spademen, Wheel-Breakers, Fascine Carriers, Firebrands, Sappers, Pontoon Train, Drovers).
3. **Add a fifth class, Support?** Twelve ideas don't fight (marshals, drums, kites, menders, kitchens, wagons, drovers, the herald, decoys). Recommendation: one Support class whose regiments don't count toward the hold and don't project the 100 m no-cast bubble, so a 12-man drum can't hold a round or shield a flank from casts.
4. **Can any unit heed warnings?** The Mule Train halts at the first creak, which the 2026-10-09 rule rules out. Recommendation: no exceptions; keep the mules' ammunition and ledge roles and drop the balking.
5. **Hidden from whom?** Forest Hiders and the Straw Legion fool the enemy AI. The economy agent also hid them from the human opponent across rounds, which breaks 'earlier placements are visible'. Recommendation: hide from the AI in battle only.
6. **Rules that act on the opponent's units?** The Herald freezes enemy regiments by custom, and the Sellsword bribe lets the opponent make your regiment walk away. Neither comes from the world. Recommendation: park the Herald; keep the bribe only if PvP needs more cross-seat play.

**World-shaping specialists (settled 2026-10-10: only specialist unit types shape the world).** Spademen (digs ditches and banks), Fascine Carriers (fills ditches, mud and fords), Firebrands (lights fires), Sappers (undermines stone), Wheel-Breakers (wrecks stalled engines), Pontoon Train (lays a floating bridge), Drovers (herds the god's herds), Smudgers (plants smoke pots), Linkmen (lifts fog with heat), Snowshoe Pathbreakers (packs a snow road), Rope-fixers (fixes climbing ropes), Forest Hiders (fells trees across a track), Field Kitchen (heats and smokes a camp). Recommended first roster: Spademen, Fascine Carriers, Firebrands, Sappers, Wheel-Breakers, Pontoon Train, Drovers.

## Units

### Shock and anchors

#### 1. Throwback Pikes ★ (x2)

- **Seat / type:** Both (best for the defender) · Infantry
- **Cost:** 130 bodies for 100 soldiers. A hard counter to shock, but only from one direction.
- **Looks:** A square pike block. At rest the pikes stand upright like a bristle brush; braced, they all drop forward. A team pennon on every pike tip makes the block a coloured pincushion; a spiked outline at strategic zoom.
- **Does:** Braces toward a charge or a herd. Cavalry, sledges, yachts or a stampede hitting the braced front are launched back along the pike facing, so riders who just forded land back in the river. Braced, it is an immovable cork at a bridge exit: the column behind jams on the span and its load keeps climbing. Mud sinks the pike butts and ice makes the block skid, halving the brace; the flanks are ordinary infantry.
- **Strong vs:** Cavalry, stampedes, wheels, yachts, columns at chokepoints. **Weak vs:** Ranged, flank attacks, Shove Lines, mud, ice, fire.
- **Why it's fun:** The landing preview shows the lancers coming down in the ford they just crossed.
- **Build risk:** Low: a brace state and an overridden launch heading.
- *From:* Heavy and mass, Toy box

#### 2. Leadfoots (x2)

- **Seat / type:** Both · Infantry
- **Cost:** 150 bodies for 50 soldiers. Immunity to the game's main source of disruption needs a steep price and a slow unit. The Cork variant costs 180 for 60.
- **Looks:** Squat tin soldiers, each fixed to a heavy round base painted in team colour, with great helms and maces. A tray of coloured coins at strategic zoom; round prints in mud and snow. Cork variant: door-sized pavises planted edge to edge.
- **Does:** Can't be launched or pushed. Charges stop dead, stampedes split around them, crowd pressure can't force them over an edge, and water sweeps them only once it tops their helms. Planted in a gap (the Cork variant), the horde's push turns sideways into its own flank files at a ford or bridge. The price is mass: three soldiers' weight each, so ice and weak bridges fail under them first, and they sink with a plonk instead of bobbing.
- **Strong vs:** Cavalry, stampedes, launch traps at edges, holding a bridgehead. **Weak vs:** Ice, weak bridges, mud, ranged.
- **Why it's fun:** A stampede breaks round ten lead men who don't look up, then the ice beneath them starts to sing.
- **Build risk:** Low: flags on the launch and push rules. The Cork needs a pressure cap so it delays and never walls.
- *From:* Toy box, Defender specialists; extends IB 62 (armour weight as a dial), pushed to the extreme

#### 3. Testudo Cohort ★

- **Seat / type:** Attacker · Infantry
- **Cost:** 130 bodies for 80 soldiers. Nearly immune to the defender's main killer, so it pays a premium per soldier and comes in small numbers.
- **Looks:** Tall curved shields with big team-colour faces. Closed up, a shell of shields on stubby legs; at strategic zoom a tight dark square that swells back to a rectangle when the shell opens.
- **Does:** Under volleys with no melee in reach it closes the shell: half spacing, about 90% arrow cover, half speed. The same mass on a quarter of the footprint triples the pressure on ice, bog and mine roofs. Defender archers who time a volley for mid-lake squeeze it through the ice; fog or a headwind that stops the volleys keeps the shell open, and the cohort crosses.
- **Strong vs:** Ranged, tower garrisons. **Weak vs:** Cavalry (the tight bunch is launched together), thin ice, mine workings, mud, flanks.
- **Why it's fun:** The archers don't kill the tortoise; they squeeze it into a fist that punches through the ice.
- **Build risk:** Low: one regiment state that changes spacing and cover; ice is already checked per patch.
- *From:* Heavy and mass

#### 4. Shove Line (x2)

- **Seat / type:** Both · Infantry (new melee mode)
- **Cost:** 120 bodies for 100 soldiers. Weak on open ground and deadly only at edges. The Bank Warden version costs 150 for 50.
- **Looks:** Door-sized pavises with a big painted open hand on a team-colour face, bodies leaning in; or bargemen holding 5 m punt poles level. A thick bar with a chevron on its facing side.
- **Does:** Barely strikes. It pushes, with about three times normal crowd pressure, picking the enemy closest to a lethal edge (from the existing distance-to-lethal field). It engages only near edges: fords, bridges, causeways, cliff paths, broken ice. Mud, ice and snow cut the push to a third; downhill pushes hardest. Capped at one shove per front-rank man every few seconds.
- **Strong vs:** Regiments on riverbanks, bridges, causeways, cliff tops. **Weak vs:** Ranged, cavalry on the flank, mud, fighting uphill, open ground; a freeze removes the edge.
- **Why it's fun:** The god builds the edge; the shove line does the last metre.
- **Build risk:** Low to medium: reuses crowd pressure and the distance-to-lethal field, but needs caps so every riverbank doesn't become a mill.
- *From:* Heavy and mass, Defender specialists

#### 5. Ploughshare Cataphracts

- **Seat / type:** Both · Cavalry
- **Cost:** 160 bodies for 40 riders. One charge down a dike can clear hundreds, so the count stays small.
- **Looks:** Fully barded horses wearing a wedge-shaped wooden plough front like a cowcatcher, painted in team colour. A narrow chevron at strategic zoom.
- **Does:** Their charge launches soldiers sideways, off whichever side of the wedge they hit. On open ground that only scatters; along a dike, causeway, bridge or ice edge it sprays files into the water. They prefer charge lanes that run parallel to a lethal edge. At four times a soldier's mass per rider they can break the bridge they are sweeping, and they need firm ground for momentum.
- **Strong vs:** Columns on narrow ground, levies, lure pursuers. **Weak vs:** Throwback Pikes, mud, snow, ice, weak bridges, open plains.
- **Why it's fun:** A snowplough of horses runs the length of a dike and the column sprays off both sides into the flood.
- **Build risk:** Low: the existing launch with its heading turned 90 degrees.
- *From:* Heavy and mass

#### 6. Levy Rabble (x2)

- **Seat / type:** Attacker · Infantry
- **Cost:** 50 bodies for 100 soldiers. Half price because they barely fight; each further levy regiment costs 10 more, so the rabble can't become a free swarm.
- **Looks:** Unarmoured peasants in oversized team-colour smocks with pitchforks and flails, a few huge crude banners and one mounted sergeant with a cowbell. A ragged blob among clean rectangles.
- **Does:** Fights poorly. In front, it springs ice, bridge and mine traps early and churns mud fastest. Behind another regiment, it presses that regiment forward, through shield walls and also over riverbanks. Lowest morale in the game: one mass launch routs it back through its own column, and if its sergeant is launched the mob stalls and the column behind crushes it.
- **Strong vs:** Untuned load traps, arrows (it soaks them), shield walls. **Weak vs:** Launches, morale shock, any lethal edge near its own front, traps tuned to let peasants through.
- **Why it's fun:** You send the rabble first to find the trap, and the trap was built to let them through.
- **Build risk:** Trivial: regiment parameters and an attach-and-press rule.
- *From:* Heavy and mass, Economy

#### 7. War Drums (x3)

- **Seat / type:** Attacker · Support
- **Cost:** 80 bodies for 12 musicians. Speeds up five or six regiments without fighting. Bands don't stack.
- **Looks:** A kettle drum on a handcart and drummers in tall crests, or pipers whose drones stick up like antlers. Pulse rings on every regiment in earshot at strategic zoom.
- **Does:** Regiments in earshot march in step: about 15 to 25% faster, half the panic contagion, and they keep formation in fog. In step, their load counts extra on bridges and ice on beat ticks (resonance), and their footfall shakes loose snow slopes, scree and mine roofs sooner. Earshot stretches downwind; rain and thunder shrink it and break the step, which slows the column but stops the resonance.
- **Strong vs:** The sunset clock, long approaches, panic cascades, fog. **Weak vs:** Light bridges, ice, snow slopes, mine workings, storms.
- **Why it's fun:** Sometimes the god's best move is perfect weather, so the band plays the column off the bridge.
- **Build risk:** Low: an aura flag and a load multiplier. Based on real bridge collapses under marching troops (Broughton 1831, Angers 1850).
- *From:* Heavy and mass, Attacker answers, Command and morale; extends IB 28 (marching feet shake the earth)

#### 8. Buffalo Riders

- **Seat / type:** Both (best for the attacker) · Cavalry
- **Cost:** 170 bodies for 50 riders. Heavy cavalry price plus a premium for ignoring mud.
- **Looks:** Huge sweeping horns, a low massive body and a rider sitting far back with a war club, with team-colour horn tassels. Wide heavy dots that pick up a mud sheen as they march.
- **Does:** Mud and shallow water barely slow them, and they wade 1.6 m, so rain and swamp, the defender's cheapest tools, don't stop this charge. Above a heat threshold the regiment leaves the line to wallow in the nearest water or mud, churning that bank to mire. Heavy on ice and bridges. They ignore thunder and fear only fire.
- **Strong vs:** Swamp, rain, flooded meadows, braced infantry. **Weak vs:** A heat wave, sun after rain, ice, bridges, fire.
- **Why it's fun:** The god answers a mud-proof charge with a hot afternoon, and the heavy cavalry goes and sits in the pond.
- **Build risk:** Low: one temperature threshold and a go-to-water target.
- *From:* Beasts

#### 9. Camel Lancers

- **Seat / type:** Both · Cavalry
- **Cost:** 170 bodies for 60 riders. Cavalry price plus a premium for the scent plume.
- **Looks:** A tall humped silhouette with the rider high above any infantry line and a long tasselled lance. The preview draws a tan scent plume drifting downwind of them.
- **Does:** They carry a scent plume that rides the wind; horses inside it, on either side, balk and then bolt homeward. Camels shrug off heat but splay in mud, refuse ice and slow in the cold. Their charge launches fewer soldiers than horses, but the riders strike over shields.
- **Strong vs:** Horse cavalry, remounts and draft oxen downwind; desert maps. **Weak vs:** Rain, a wind shift that swings the plume onto your own horses, spears, winter.
- **Why it's fun:** Buying camels is a bet on the wind, and the other god controls the wind.
- **Build risk:** Low: one more channel in the smoke field.
- *From:* Beasts; extends Camel lines, terrain and weather brainstorm #20

### Water, ice and snow movers

#### 10. Stilt Slingers ★ (x4)

- **Seat / type:** Both (best for the defender) · Ranged
- **Cost:** 120 bodies for 60 soldiers. Flood and swamp mobility plus portable height, paid for with fragility on dry ground.
- **Looks:** Twice everyone's height on striped stilts, a long team-colour smock and a sling whirling overhead (or a short bow and reed hat). Tall pins with long shadows that stay crisp over the flood tint.
- **Does:** They wade 1.5 to 1.8 m of water and deep mud at near full speed, so the flood that strands the column is their ground. They carry their own high ground: a fixed height bonus to range, and they shoot over hedges, front ranks and low fog. Stilts are point loads (triple weight on ice), and anything that would launch them topples them like felled timber instead; toppled in deep water, they are swept away. In a gale they step down and plod.
- **Strong vs:** Infantry and siege stranded in floods or mud, swamps, hedgerows. **Weak vs:** Cavalry on dry ground, stampedes, gales, ice, forest; swamp under the water; 20 cm more flood.
- **Why it's fun:** The flood that stalls the horde becomes a heron pond full of archers nobody can reach.
- **Build risk:** Low: per-class tables for lethal water depth, mud and ice, plus a height input.
- *From:* Defender specialists, Light movers, Toy box, Attacker answers

#### 11. Coracles and Puffers (x3)

- **Seat / type:** Both · Infantry (amphibious)
- **Cost:** 120 bodies for 60 soldiers. Mediocre fighters whose whole value depends on there being water.
- **Looks:** Coracles: round hide boats worn on the back like turtle shells, the biggest patch of team colour in the game, spinning bowls on the water. Puffers: a huge round inflated goatskin on each back, a ball on legs; in water only the ball and a head show.
- **Does:** Deep water isn't lethal to them. They cross it while the current carries them downstream and the wind adds drift, so every flood or gust moves where they land, and the preview shows it. Rapids, weirs, spillways and surge fronts still kill, and new ice locks them in, stunned. Puffer skins shrink visibly with each arrow hit. They cross and land, never wait afloat, which stops a regiment camping on the lake until sunset.
- **Strong vs:** River lines, flood moats, broken bridges, launch traps into water. **Weak vs:** Archers on the bank, cold snaps, dam-break waves, cavalry on land.
- **Why it's fun:** Your flood moat becomes their highway; or open the dam and the whole regiment bobs off toward the weir.
- **Build risk:** Medium: a per-class lethality field plus drift along the flow.
- *From:* Defender specialists, Light movers, Toy box

#### 12. Fen Skaters

- **Seat / type:** Defender · Infantry
- **Cost:** 100 bodies for 40 soldiers. Light fighters worth little without a cold snap.
- **Looks:** A low forward crouch, bone skates, a light spear across the back and long team-colour scarves streaming behind. Dots trailing short streaks.
- **Does:** On ice they move at cavalry speed, always in open order with triple spacing (a fixed formation rule, not a reaction to cracks), so their load per patch is about a quarter of a line block's. They raid regiments stalled on the shore or the ice, then pull back across ice that will break under their pursuers. Snow on the ice, slush or a thaw drops them to walking pace.
- **Strong vs:** Armies held up at frozen rivers, archers on the far bank. **Weak vs:** A thaw, rain, snow on the ice, archers in the open.
- **Why it's fun:** One cold snap turns the river into a road only your forty can use.
- **Build risk:** Low: a speed table and a spacing rule.
- *From:* Defender specialists

#### 13. Ice Yachts (x2)

- **Seat / type:** Both · New: wind-driven vehicle
- **Cost:** 130 bodies for 10 yachts, 30 crew. Shock to match a cavalry regiment, but only on ice and only with wind.
- **Looks:** A big triangular team-colour sail on three runners with the crew crouched aboard. The sail is the strategic-zoom read and doubles as a wind gauge.
- **Does:** Ice and packed snow only. Speed comes from the wind along the heading, up to about three times cavalry; they can't sail within 45 degrees of upwind, and a calm strands them. A hit launches like a charge and skids the crew forward. Braking distance grows with speed, so a hole opening ahead slides them in. Light and spread out: ten yachts load the ice like thirty soldiers.
- **Strong vs:** Fleeing regiments and ranged on frozen lakes. **Weak vs:** Calm, headwind, thaw, snow on the ice, Throwback Pikes, shorelines.
- **Why it's fun:** A wind shift after they commit swings the whole run into the reeds, or through their own column.
- **Build risk:** Low to medium: a handful of bodies with a wind lookup and a skid model.
- *From:* Light movers, Toy box

#### 14. Ski Runners (x2)

- **Seat / type:** Both (snow maps) · Ranged
- **Cost:** 130 bodies for 50 soldiers. Uncatchable on snow serves the defender's win condition directly, so they are dear per head.
- **Looks:** Long skis, a pole held crosswise, javelins or a short bow, a team-colour hood or cape over a white smock. Long files trailing twin lines.
- **Does:** The deeper the snow, the faster they go, and faster still downhill; on bare ground they shoulder their skis and plod. They glide on top, leaving no packed trail and putting little load on ice and loaded slopes, so they can shoot down from an avalanche slope that the regiment chasing them then releases. They shoot, then glide off when melee closes. Falling snow hides them one fog step.
- **Strong vs:** Regiments wading or fleeing through snow. **Weak vs:** Thaw, forest, packed roads, wind-scoured ridges.
- **Why it's fun:** They glide to safety while their pursuers set off the slab.
- **Build risk:** Low: a per-class snow cost and a pressure flag on the slope check.
- *From:* Light movers, Ranged and wind

#### 15. Snowshoe Pathbreakers

- **Seat / type:** Attacker · Infantry
- **Cost:** 100 bodies for 100 soldiers. Weaker fighters than line infantry; a second regiment adds little.
- **Looks:** Big round racquet snowshoes, bundles of beater poles and long team-colour scarves. A pale packed ribbon behind them at strategic zoom.
- **Does:** They walk deep snow at near full speed and pack a trail three times wider and harder, writing to the snow field so the cavalry and siege behind keep their pace. The general sends them to the head of the column when snow on the route passes a set depth. Fresh snow or drifts erase the trail, a thaw turns it to slush, a cold snap glazes it, and the trail shows the defender exactly where 8,000 soldiers will walk.
- **Strong vs:** Deep snow, drifted passes. **Weak vs:** Blizzards, thaw, ambush.
- **Why it's fun:** Your blizzard erases the road while the siege train is halfway along it.
- **Build risk:** Low: a per-class strength and width on the existing trail-breaking.
- **World action:** Packs a snow road.
- *From:* Light movers; extends the snow field's trail-breaking seed

#### 16. Reindeer Sledges

- **Seat / type:** Both (winter maps) · New: sledge
- **Cost:** 130 bodies for 20 sledges, 60 archers. Fast archers that tend to survive.
- **Looks:** Antlered reindeer pulling a boat-shaped sledge with a team-colour hull, three archers aboard and harness bells. Long dots leaving twin runner lines.
- **Does:** Deep snow barely slows them. Their low ground pressure crosses snow crust and bog mats that hooves punch through, but their full weight counts on ice. The archers shoot on the move and never melee. Bare ground and slush drag the runners to a crawl, and the reindeer tire above a set temperature.
- **Strong vs:** Defender: the last regiment slides away across a snowfield. Attacker: hunts across it. **Weak vs:** Heat wave, soot on the snow, thaw, roads.
- **Why it's fun:** The god's heat wave strands the sledges in slush in the middle of a chase.
- **Build risk:** Medium: needs a ground-pressure check, shared with the snow-crust and bog-mat ideas.
- *From:* Beasts

#### 17. Tobogganeers

- **Seat / type:** Both (snow maps) · Infantry
- **Cost:** 120 bodies for 80 soldiers. One big charge, on snow maps only, that leaves the regiment scattered.
- **Looks:** A big curved team-colour shield worn on the back like a turtle shell; sledding, they sit in it, legs out and arms up. Streaks down white slopes.
- **Does:** They gather on a snow crest above the enemy and go over together, but only if the predicted stopping point is safe at that moment. They slide the fall line at about three times cavalry speed and launch the front ranks they hit, and themselves. Powder slows them, crust and ice speed them up, trees stop them. Gathering loads the slope; past its strength it fails, and they ride the avalanche down with everyone below.
- **Strong vs:** Infantry and ranged below snow slopes. **Weak vs:** Throwback Pikes, Leadfoots, trees, thaw, flat maps.
- **Why it's fun:** Add snow and they stop short; freeze a crust and all eighty overshoot onto the thin lake ice.
- **Build risk:** Medium: a sliding state per soldier with friction from the snow and ice fields.
- *From:* Toy box

#### 18. Pole-vaulters

- **Seat / type:** Both · Infantry
- **Cost:** 110 bodies for 50 soldiers. A fast hunter or escaper, but only in ditch country.
- **Looks:** An upright pole twice their height, striped in team colour with a tassel at the tip. Dots hopping over water lines in dotted arcs.
- **Does:** At a run they vault ditches, dug channels and streams up to about 6 m, plus low hedges and walls, using a jumpable-gap mask and the existing arc code. Jump length reads the take-off cell: mud shortens it, ice makes them slip, a tailwind stretches it. They judge a gap only by its width (mud is a warning, not lethal), so rain on the run-up drops the last vaulters short into the channel. Floods widen gaps beyond reach.
- **Strong vs:** Polders, channels, hedgerow mazes. **Weak vs:** Rain, wide water, forest (poles snag), cavalry.
- **Why it's fun:** A file of tin soldiers bounds over your canal one by one, until your shower makes the last ones plop in short.
- **Build risk:** Low: one mask rebuilt per terrain edit, plus the existing arc code.
- *From:* Light movers

#### 19. Rope-fixers

- **Seat / type:** Both · Infantry (climbers)
- **Cost:** 140 bodies for 50 soldiers. Poor fighters, but a route only they can use, plus a way to break pursuit, buys minutes.
- **Looks:** A coil of rope and a grapnel pole held high, team-colour jerkins. Their fixed ropes hang as team-colour ladders, a coloured stitch across the cliff at strategic zoom.
- **Does:** They climb a cliff and fix a rope: a one-file lane with a load limit of about 20 soldiers. A toggle in the army layer decides what happens next. Haul it up, and pursuers must walk the long way round; leave it as bait, and the attacker crowds onto it and snaps it. Wet or glazed rock closes the climb, gusts pause it, and fire burns the ropes.
- **Strong vs:** Walled passes, pursuers. **Weak vs:** Volleys while on the cliff face, freezing rain, fire at the cliff foot.
- **Why it's fun:** 300 attackers queue on a creaking ladder your climbers left hanging.
- **Build risk:** Low to medium: ropes reuse the bridge-segment load code, and every clifftop must stay reachable on foot.
- **World action:** Fixes climbing ropes.
- *From:* Light movers

#### 20. Teeterboard Tumblers

- **Seat / type:** Both (mainly for the attacker) · Infantry
- **Cost:** 130 bodies for 60 soldiers. They bypass walls and channels, the defender's main purchases, so they are dear and slow to cross.
- **Looks:** Lean acrobats in striped team-colour tunics carrying planks in pairs. A fan of dotted arcs over an obstacle at strategic zoom.
- **Does:** At a wall, hedge, channel or moat up to about 20 m wide, they set the boards down: two jump and one flies, aimed at a landing spot chosen for the current wind. One soldier every 3 s, so the far side fills in a trickle. They land on their feet, immune to launch stun and morale shock. Crosswinds push the arcs off course, and they won't launch blind into fog or smoke.
- **Strong vs:** Walls, hedges, channels, launch traps. **Weak vs:** A garrison waiting at the landing zone, fog, crosswinds, towers.
- **Why it's fun:** A gust timed after they commit pushes the red edge of the landing preview onto the moat.
- **Build risk:** Medium: a launch queue per board, aimed per regiment.
- *From:* Toy box

### Ranged, sight and wind

#### 21. Kite Watchers

- **Seat / type:** Both · Support (observer)
- **Cost:** 60 bodies for 20 crew. You pay for sight, not damage, and sight doesn't stack.
- **Looks:** A windlass crew under a huge team-colour kite. At strategic zoom a kite diamond on a line tethered to the crew dot, so you can see exactly where the eye is.
- **Does:** Kite height follows the wind at the crew: grounded in a calm, snapped and tumbling in a gale. The eye hangs downwind, further out in a stronger wind, and sees over ridges and through thicker fog and smoke than ground eyes can. Friendly arcing regiments within 200 m may volley at what it sees, with an accuracy penalty. The kite isn't soldiers, so it never extends the no-cast bubble.
- **Strong vs:** Reverse slopes, smoke screens, fog banks. **Weak vs:** Calm, wind shifts (the eye can swing back over your own lines), raids on the crew.
- **Why it's fun:** The wind decides where your army can see, and you can watch the eye drift.
- **Build risk:** Low: one sight origin per crew and a downwind offset.
- *From:* Ranged and wind

#### 22. Wind-Chaser Longbows

- **Seat / type:** Both · Ranged
- **Cost:** 130 bodies for 100 soldiers. The best reach in the game, but only when the wind serves.
- **Looks:** A bow taller than the man. The officer's pole carries a long team-colour streamer that blows with the wind; their range fan is drawn stretched downwind.
- **Does:** Light flight arrows: the longest range and the steepest wind curve in the table. Each decision tick the regiment scores eight nearby firing spots by reach (wind along the shot plus height) and moves if one beats its current spot by 15%, with a minimum time in place. The defender's tailwind on a ridge draws them onto it, and the attacker's cheap wind shift can pull them off it, down toward the ford.
- **Strong vs:** Columns crossing open ground downwind. **Weak vs:** Headwind, rain, fog without a kite, cavalry.
- **Why it's fun:** Your wind cast doubles as their marching order, and the rival god can steal it.
- **Build risk:** Low: eight table lookups per regiment per decision.
- *From:* Ranged and wind; extends IB 232 (banners as wind gauges)

#### 23. Pavise Crossbowmen

- **Seat / type:** Both · Ranged
- **Cost:** 140 bodies for 100 soldiers. The best cover and steady range, paid for in speed, weight and blindness.
- **Looks:** A door-sized team-colour pavise planted in front of each man with the crossbow resting on top: the biggest area of team colour in the roster, a solid coloured bar in front of the dots.
- **Does:** Planting takes 8 s and sharply cuts frontal hits. Bolts barely feel the wind, so a gale in the enemy's faces is their best day: longbows fall short, bolts don't. Direct fire only, so they need their own sight, can't use kites and can't shoot over friends. The heaviest foot unit. If they retreat before repacking, the pavises stay standing as cover anyone can use, the attacker included.
- **Strong vs:** Archer duels in a headwind, holding a bridgehead. **Weak vs:** Fog, smoke, cavalry on the flank, thin ice, fast retreats.
- **Why it's fun:** A wall of colour that outshoots longbows on a stormy day, until the fog rolls in.
- **Build risk:** Low: abandoned pavises reuse the hedgerow cover piece.
- *From:* Ranged and wind

#### 24. Slingers

- **Seat / type:** Both · Ranged
- **Cost:** 80 bodies for 100 soldiers. Fragile and ammunition-limited; double frontage stops them being massed at a chokepoint.
- **Looks:** Bareheaded and unarmoured with a fat ammunition pouch, shooting with a big overhead loop. A loose, wide cloud of dots instead of a block.
- **Does:** No bowstring, so rain never affects them, and a downpour that slackens bows is their best day. They need swing room: open order only, and no shooting from forest or a packed crowd. Six volleys per pouch, refilled on stony cells (gravel bars, scree, roads, boulders); snow or standing water buries the stones. The lightest ground pressure in the roster.
- **Strong vs:** Archers in rain, light troops, thin ice. **Weak vs:** Cavalry, forest, snow maps.
- **Why it's fun:** The god's rainstorm finally hurts only the enemy's archers.
- **Build risk:** Low: an ammunition counter and a stony-ground mask.
- *From:* Ranged and wind; extends IB 158 (wet bowstrings, dry canopies)

#### 25. Pilum Throwers

- **Seat / type:** Both · Ranged (thrown)
- **Cost:** 110 bodies for 100 soldiers. Line infantry plus two volleys of debuff.
- **Looks:** A long iron-shanked javelin cocked overhead and a small shield. Struck regiments' shields bristle with bent shafts, and the regiment gets a hatched heavy tint.
- **Does:** Short range, nearly wind-proof, two volleys, then they fight as light infantry. Each volley that hits adds burden to the target: 10% more mass per volley (capped at 25%) and a slower march, and the load check counts it, so the enemy is heavier when it reaches the ice. A burdened regiment drops its shields once no enemy archers are in range, which clears the burden but costs it more to arrows. Mud and snow spoil the run-up.
- **Strong vs:** Shield infantry heading for ice or a bridge. **Weak vs:** Archers (outranged), cavalry, mud, snow.
- **Why it's fun:** You weigh the enemy down before they reach the ice.
- **Build risk:** Low: the same per-regiment mass hook as IB 33 (soaked wool).
- *From:* Ranged and wind

#### 26. Smudgers (x2)

- **Seat / type:** Both · Infantry (smoke pots)
- **Cost:** 80 bodies for 30 soldiers. Weak in melee with a fixed number of pots; a timed answer, not demolition.
- **Looks:** Smoking clay pots on chains or a carrying pole, straw bundles and face cloths. Grey dots trailing puffs; the preview draws each pot's plume.
- **Does:** They plant pots where the wind field will carry smoke over a target. Attacker: a tower, wall or ridge archers coming into range of the column. Defender: its own regiments retreating under fire. Smoke blinds garrisons and blocks direct fire. Each pot is a fixed smoke source for 60 to 90 s in the existing field: a calm pools it in hollows, a strong wind thins it, and a wind shift rolls it onto your own archers.
- **Strong vs:** Towers, walls, ridge archers; crossbows and slings; hunters who need sight. **Weak vs:** Wind shifts, calm, kites, sallies, cavalry.
- **Why it's fun:** There is one smoke screen, and whoever owns the wind owns it.
- **Build risk:** Low: point sources in the smoke field.
- **World action:** Plants smoke pots.
- *From:* Attacker answers, Ranged and wind

#### 27. Linkmen

- **Seat / type:** Both · Infantry
- **Cost:** 120 bodies for 30 soldiers. Poor fighters, but fog is the defender's cheapest delay.
- **Looks:** Iron fire baskets on tall poles. A warm halo cutting a clear lane through the fog shading at strategic zoom.
- **Does:** Each basket is a small heat source in the temperature field. Heat lifts fog, so the regiment cuts a lane about 30 m wide that fills in again behind it, and the column inside keeps its sight and cohesion. The heat is real, so it also thins the ice under the column it leads. Heavy rain puts the baskets out, and a launched bearer spills his basket, lighting dry fuel where he lands.
- **Strong vs:** Fog banks, fog pooled under an inversion. **Weak vs:** Rain with fog, frozen crossings, a stampede through dry grass, their own glow (archers can see them).
- **Why it's fun:** The lanterns that lead the column out of the fog also melt the lake under it.
- **Build risk:** Low: heat sources in an existing field.
- **World action:** Lifts fog with heat.
- *From:* Attacker answers

### Siege and builders

#### 28. Hill Wheel ★

- **Seat / type:** Both · Siege
- **Cost:** 150 bodies for 1 wheel, 12 crew. One release per battle and it can hit its own side, but a good lane launches a hundred or more.
- **Looks:** A wheel taller than a horse, its rim painted in team-colour segments like a toy hoop, with twelve crew on push-poles. A coloured disc at strategic zoom, and a dashed roll line once released.
- **Does:** The crew push it uphill on roads or firm slopes to a crest and let it go when the predicted roll line crosses an enemy regiment and no friendly one. After that nobody controls it: it follows the fall line and launches everyone in its path sideways, friend or foe. Ice speeds and straightens it, mud and trees stop it, snow fattens it, deep water floats it away; it smashes one hedge or palisade section, then stops.
- **Strong vs:** Columns on slopes, hedges, palisades. **Weak vs:** Flat maps, mud, boulders, spread-out regiments, Leadfoots, riders who reach the crew.
- **Why it's fun:** The god aims the hill: an icy strip or a channel bends the roll line onto the column, or the rival god bends it into your reserve.
- **Build risk:** Low: one rolling body on the heightfield, with a cheap, exact preview.
- *From:* Toy box

#### 29. Skittle Mangonel

- **Seat / type:** Both · Siege
- **Cost:** 120 bodies for 3 engines, 30 crew. It scatters and stuns, and rarely kills unless the lane ends in water.
- **Looks:** A squat torsion engine with its arm laid flat and a pyramid of big team-striped balls on an ammunition cart. An engine icon with a dashed roll lane.
- **Does:** Fires a stone low, so it bounces and rolls along the long axis of the densest enemy regiment. The ground steers it: ice skids it twice as far, mud and snow swallow it, slopes curve it downhill, hedges and ridges stop it. Each soldier it hits is launched and slows it, and it stops after about fifteen (no chain reaction). Spent stones stay as small blockers that roughen the road.
- **Strong vs:** Packed columns on roads, crowds on ice next to open water. **Weak vs:** Rain, mud, snow, loose formations, hedgerows.
- **Why it's fun:** The god's freeze turns a field into a bowling lane and the column goes into the river like skittles.
- **Build risk:** Low: a few rolling bodies that lose speed on each contact.
- *From:* Siege and builders

#### 30. Trebuchet (x2)

- **Seat / type:** Attacker · Siege
- **Cost:** 130 bodies for 1 engine, 30 crew. The long-range answer to stone, slow to fire. The staged version costs 400 over three rounds.
- **Looks:** A towering A-frame with a sling arm and a team-colour counterweight box; it travels as flat timber wagons. A big T glyph with a range ring the defender can see.
- **Does:** Travels as a kit of light wagons that cross bridges the assembled engine would break, assembles on firm level ground and never moves again. Targets stone walls and towers first, then anything that holds still (garrisons, wagon forts, earthworks). Stones launch soldiers and stay as boulder props; misses into a river build a weir that backs the water up. Soaked ground settles the base and scatters its shots. PvP variant: bought in stages (frame, then arm, then counterweight), one per round, so the defender gets a round of dread.
- **Strong vs:** Walls, towers, wagon forts, clumped last stands. **Weak vs:** Rain and mud at its base, smoke, crosswinds, cavalry raids, spread-out deployments.
- **Why it's fun:** Its misses build a stone causeway, and the god then floods it.
- **Build risk:** Medium: riverbed edits must feed the water simulation mid-battle.
- *From:* Siege and builders, Economy

#### 31. Belfry (x2)

- **Seat / type:** Attacker · Siege
- **Cost:** 160 bodies for 2 towers, 40 pushers. The only way over stone without breaking it, and it can topple. The regiment that rides it is bought separately.
- **Looks:** The tallest object on the field: a wheeled tower with a drawbridge and patchwork hides that darken when wet. A tall pin with a tilt needle at strategic zoom.
- **Does:** Pushed by road to the nearest manned wall, where it docks and lets an assault regiment onto the wall top. Tilt is one number: slope, plus uneven sinking in mud, plus wind on its height. Past the limit it topples like a felled tree, launching its riders along the fall (deadly into a moat), and stays as a long prop that can serve as a load-limited footbridge across a channel. On a muddy or icy downhill it can roll back through its own column. Wet hides won't burn but weigh more.
- **Strong vs:** Walls, towers, moats. **Weak vs:** Crosswinds, slopes, mud, ice, weak bridges, fire in a drought.
- **Why it's fun:** The whole field watches one tower lean, lean further, and drop sixty men into the moat.
- **Build risk:** Medium: the tilt rule is new; the topple reuses the felled-tree code.
- *From:* Heavy and mass, Siege and builders

#### 32. Wagon Fort (x2)

- **Seat / type:** Defender · Siege
- **Cost:** 210 bodies for 12 wagons, 60 crew. A wall plus a ranged regiment that cavalry alone can't wipe out.
- **Looks:** Tall plank-sided wagons with loopholes and team-colour shields hung along the sides. A train of blocks that snaps into a ring at strategic zoom.
- **Does:** Travels by road. With a stronger enemy near and firm level ground, it circles into wall segments in about 20 s, leaving a gap toward its retreat edge; the crossbow crew shoot with garrison cover, and one battered regiment may shelter inside. It limbers up and falls back once clear, and its heavy wagons churn the wet road behind it into bog for the pursuit. Mud stops it circling, water in the ring breaks it, wagons burn in a drought, and they can't cross ice.
- **Strong vs:** Cavalry, levies, hunters, Shove Lines. **Weak vs:** Trebuchets, belfries, sappers, fire, hollows that flood, the god's own rain.
- **Why it's fun:** At sunset the last sixty defenders circle the wagons while the horde breaks on painted planks.
- **Build risk:** Low to medium: the ring reuses the wall and garrison code.
- *From:* Heavy and mass, Siege and builders

#### 33. Pontoon Train

- **Seat / type:** Both · Siege
- **Cost:** 120 bodies for 8 boats, 40 crew. Opens a route but never fights.
- **Looks:** Boat-shaped wagons with team-colour hulls and planks stacked on top. A dotted line growing across the river at strategic zoom.
- **Does:** Lays a floating bridge (about 80 m in 45 s) where the flow on its route is weakest. The deck rides the water: a flood strains the cables and a surge snaps them, swinging the bridge downstream like a gate; a drain or an upstream dam grounds the boats, twisting the deck and cutting its load limit; a freeze locks it stronger until the thaw. Defender keepers cut it loose after their last regiment crosses.
- **Strong vs:** Rivers, broken bridges, flooded fords. **Weak vs:** Floods and drains, fire, raids on the crew while laying.
- **Why it's fun:** The god has two opposite levers, raising or lowering the river, and either can break the bridge with an army on it.
- **Build risk:** Medium: a bridge that appears mid-battle needs an incremental flow-field patch.
- **World action:** Lays a floating bridge. Recommended for the first roster.
- *From:* Siege and builders; extends IB 70 (engineers' bridging)

#### 34. Sappers

- **Seat / type:** Attacker · Siege
- **Cost:** 120 bodies for 60 soldiers. Slow and single-target, but it brings down any stone piece.
- **Looks:** Stocky diggers with picks, a timber sap-head shed and a growing heap of team-colour spoil sacks. A dotted line creeping underground at strategic zoom.
- **Does:** Dig a straight gallery (about 1 m every 3 s) to a stone wall or tower, then burn the props so it falls. The gallery stays as old mine workings with a low load limit, so a later column or the attacker's own trebuchet breaks into it. Frozen ground stops the digging; rain, a spring or a flood fills the gallery and collapses it short of the target; a tremor collapses it too.
- **Strong vs:** Stone walls and towers. **Weak vs:** Water, frost, tremors, raids on the sap-head, defenders who move away.
- **Why it's fun:** The mine that brought down your wall becomes the hole that swallows their siege train ten minutes later.
- **Build risk:** Low: a progress counter and a line of load-limited cells.
- **World action:** Undermines stone. Recommended for the first roster.
- *From:* Siege and builders

#### 35. Fascine Carriers

- **Seat / type:** Attacker · Infantry (builders)
- **Cost:** 100 bodies for 100 soldiers. Line-infantry price so they can't serve as cheap bodies to soak damage.
- **Looks:** Walking haystacks: each soldier under a huge brushwood bundle tied with team-colour cord. A fuzzy straw-coloured block.
- **Does:** Ahead of the siege train they drop bundles into ditches, channels, mud and shallow fords; each bundle firms a cell and raises its bed. A filled stream becomes a brush weir that washes out in a surge. Dry bundles are fuel: with fire beside them they drop the lot and run, leaving a burning line across their own road. Rain fireproofs the bundles but adds their weight on ice and bridges.
- **Strong vs:** Ditches, moats, mud, channels, Spademen. **Weak vs:** Fire, dry wind, cavalry, their own wet weight on ice.
- **Why it's fun:** A grass fire upwind turns a column of walking haystacks into a burning roadblock in front of their own trebuchet.
- **Build risk:** Low: cell edits to firmness and bed height.
- **World action:** Fills ditches, mud and fords. Recommended for the first roster.
- *From:* Siege and builders

#### 36. Spademen (x2)

- **Seat / type:** Defender · Infantry (builders)
- **Cost:** 110 bodies for 100 soldiers. Poor fighters, but they shape ground inside the 100 m buffer, where the god can't.
- **Looks:** Round spades on their backs, wicker baskets and team-colour aprons. A regiment inside a growing dark brown ring or stripe.
- **Does:** Dig a ditch and bank in front of their position, with forced gaps (the no-trap check runs on the line as it grows). The bank gives arrow cover, breaks charges and holds water like a levee; rain turns the ditch into a moat and cold into an ice strip; frozen ground can't be dug. PvP variant (Sod-cutters): the works grow each round the regiment stays put, from a scrape to a ditch and bank to a palisade that burns, and moving or removing it forfeits the growth. A flood that tops the bank drowns them in their own fort.
- **Strong vs:** Cavalry, siege, levies; it makes the god's floods safe for the defender. **Weak vs:** Fascines, trebuchets, water over the bank, tremors, frost, fire on the palisade.
- **Why it's fun:** The god finally floods the whole valley while the defenders sit dry on an island they dug themselves.
- **Build risk:** Medium: mid-battle terrain edits must update water and navigation incrementally.
- **World action:** Digs ditches and banks. Recommended for the first roster.
- *From:* Siege and builders, Economy; extends IB 31 (plantings mature between rounds), applied to earthworks

#### 37. Wheel-Breakers

- **Seat / type:** Defender · Infantry
- **Cost:** 90 bodies for 30 soldiers. One job and almost no combat value.
- **Looks:** A small crew with oversized mallets on their shoulders, coils of rope and team-colour leather aprons. A small wedge at strategic zoom.
- **Does:** They target only siege engines that have stood still for 10 s (bogged in mud, stuck in a drift, waiting at a broken bridge), drive off the crew, smash the wheels and run. The engine becomes a wreck stamped into the navigation grid like a fallen tree: it plugs a narrow road, and on a bridge it stays as dead load. Fire burns the wreck and engineers clear it in a fixed time, so it never becomes a permanent lock.
- **Strong vs:** Siege stuck in mud, snow or at broken bridges. **Weak vs:** Cavalry escorts, archers, engines that keep moving, open country.
- **Why it's fun:** The attacker's biggest engine becomes the cork in his own road.
- **Build risk:** Low: reuses the fallen-tree stamping.
- **World action:** Wrecks stalled engines. Recommended for the first roster.
- *From:* Defender specialists

#### 38. Firebrands (x2)

- **Seat / type:** Attacker (fire-pot archers: both seats) · Infantry or Ranged
- **Cost:** 150 bodies for 40 soldiers. Every fire they set is one the god can steer. The fire-pot archer version costs 130 for 80 plus 3 energy, so the body-rich attacker can't spam fire.
- **Looks:** Torches held high with big orange flames and a pitch pot on a yoke; or a glowing fire-pot on a pole in the middle of an archer block. Orange-tipped dots, and a fuse icon on each target in the preview.
- **Does:** Two jobs: a backfire that burns a strip of fuel ahead of a fire coming from upwind, and burning gaps in hedgerows, palisades, bridges and empty wooden towers. Strict rules keep it from being a god power: dry fuel only, the wind blowing away from their own side, no soldier of either side within 30 m (so a garrisoned tower is off-limits), a fixed number of torches or pots, and each one shown on the timeline. Rain douses them.
- **Strong vs:** Hedges, palisades, wooden towers and bridges, wildfire, defenders hiding in woods. **Weak vs:** Rain, snow, stone, a wind shift after lighting, herds near the target.
- **Why it's fun:** One gust turns their own torch line into a fire that chases the column back down the road.
- **Build risk:** Low to build, high to balance: unit-set fire is the biggest world change any unit makes.
- **World action:** Lights fires. Recommended for the first roster.
- *From:* Attacker answers, Ranged and wind

### Hunters and hiders

#### 39. Hounds ★ (x3)

- **Seat / type:** Attacker · New: tracker
- **Cost:** 150 bodies for 30 handlers, 30 hounds. Weak in a fight, but it turns a defender's hold into a wipe. The agents priced it from 50 to 200.
- **Looks:** Long-eared toy hounds in team-colour coats straining on leads ahead of handlers with curled horns. A fast arrowhead of dots and a dotted scent line to the quarry.
- **Does:** They find defenders by scent, not sight: a defender regiment within about 400 m upwind across dry ground (one wind and one wetness sample per pair, no new field) or, in a richer version, a drifting scent field. They pin the quarry (slower, staggered, losing morale), bay to call nearby cavalry, expose hiders and decoys within 60 m, and handlers lead stunned men off as prisoners. Rain washes the trail, a river breaks it, fire sends them home, and a herd nearby overrides everything (they can spook it).
- **Strong vs:** Hidden remnants, woods, fog, scattered survivors, decoys. **Weak vs:** Rain, rivers, wind shifts, formed infantry, herds, archers who shoot the handlers.
- **Why it's fun:** The god's last shower becomes a getaway car, and you watch the pack lose the trail at the stream.
- **Build risk:** Low to medium with sampled scent; medium with a full scent field.
- *From:* Attacker answers, Beasts, Economy; extends IB 65 (scent carried on the wind)

#### 40. Forest Hiders (x3)

- **Seat / type:** Defender · Ranged or Infantry
- **Cost:** 130 bodies for 50 soldiers. Hiding is insurance for the hold, so it is dear. At most two per army.
- **Looks:** Moss-green hooded cloaks with team-colour linings and longbows or short bows. The owner sees a dashed outline; the opponent sees nothing until they are spotted.
- **Does:** In dense vegetation, fog or smoke they go to ground: enemy regiments can't target or path to them beyond about 30 to 40 m. Forest is cheap ground for them and closed to cavalry and siege, so they shoot from the edge, melt back, and may fell trees across a track behind them. Stance is set at deployment: lie low, or ambush once. They count toward the hold, so they are sunset insurance. Fire flushes them, fresh snow shows their tracks, hounds smell them.
- **Strong vs:** Cavalry, siege, hunters without hounds, stalled columns. **Weak vs:** Fire and embers, snow, hounds, sweeps through the trees.
- **Why it's fun:** The horde marches past a quiet wood at sunset, and the wood is still yours.
- **Build risk:** Low if they hide from the enemy AI only; medium if they also hide from the human opponent.
- **World action:** Fells trees across a track.
- **Rule question:** Hiding from the human opponent across rounds breaks 'earlier placements are visible'; see the rule questions.
- *From:* Defender specialists, Light movers, Economy

#### 41. Hobilars

- **Seat / type:** Defender · Infantry with mounts
- **Cost:** 150 bodies for 80 soldiers and ponies. Infantry fighting value plus escape speed; cheaper than cavalry because it can't charge.
- **Looks:** Stocky shaggy ponies and riders with short spears and round shields. Dismounted, the ponies stand tethered in a straight picket row under team-colour blankets: a block plus a separate thin line.
- **Does:** They ride to position and dismount to fight, with the picket waiting 80 to 150 m behind. When the defender AI calls a fall-back, they run to the ponies, mount and outrun infantry. Smoke, thunder or fire near the picket bolts the ponies homeward riderless and strands the regiment on foot. Ponies are light, so they cross ice and bridges that heavy horse breaks.
- **Strong vs:** Infantry pursuit, hounds (they reach a stream first), long days. **Weak vs:** Cavalry that catches them mounting; the attacker's few casts spent on smoke or a thunderclap by the picket.
- **Why it's fun:** A whole round hinges on one row of tethered ponies the attacker found a cheap way to scare.
- **Build risk:** Low to medium: a two-part regiment and a mount state.
- *From:* Beasts

#### 42. Straw Legion ★

- **Seat / type:** Defender · New: decoy
- **Cost:** 30 bodies for 1 regiment-sized decoy. Worth most in the round it is placed; dead weight once exposed, which gives the defender a reason to pay to remove it.
- **Looks:** Straw men on stakes in team-colour tabards with painted shields, or hobby-horse cavalry. At mid and far zoom they look identical to the type they copy; straw tufts show only up close. The owner sees a straw-hatched outline.
- **Does:** Stands still. The attacker AI treats it as the real thing until one of its regiments comes within 40 m (20 m in fog) or rain soaks it and it slumps; then it is exposed for good. Straw burns. It carries into the next round unidentified only if nothing touched it.
- **Strong vs:** The attacker AI's targeting and the human's counter-picks. **Weak vs:** Rain, hounds, cavalry sweeps, scouts.
- **Why it's fun:** The attacker builds a whole round-3 plan to crush a flank of scarecrows.
- **Build risk:** Low: static instances with an apparent type per seat.
- **Rule question:** Does a decoy block casts like a real regiment? See the rule questions.
- *From:* Economy; extends IB 97 (bluff sigils and decoy placements)

#### 43. Drovers (x3)

- **Seat / type:** Both · Support (herd handlers)
- **Cost:** 80 bodies for 20 drovers, 20 dogs. No fighting value; it exists to answer herds.
- **Looks:** Shepherds with tall crooks and wide hats, a collie in a team-colour neckerchief at each heel, and a crook-and-bell standard. Dots inside a herd-coloured halo; the preview draws their bark ring.
- **Does:** A weak fear source the god's herds read like mild heat, capped below the stampede threshold, so drovers move herds at walking pace but can never start a stampede. Attacker: drive a herd ahead of the vanguard as a minesweeper (it breaks thin ice and tramples snow), then off the road before contact. Defender: keep a herd between them and the enemy. Fire or thunder scatters the dogs first.
- **Strong vs:** Roaming herds, herd roadblocks, stampede traps. **Weak vs:** Fire set upwind, any fighting unit, herd pens, maps without herds.
- **Why it's fun:** The defender's herd trap meets the attacker's dogs, and the god has to place the fire so the dogs break before the cattle.
- **Build risk:** Low: one more input to the herd's fear gradient.
- **World action:** Herds the god's herds. Recommended for the first roster.
- *From:* Defender specialists, Attacker answers, Beasts

### Command and support

#### 44. Rally Drum ★

- **Seat / type:** Defender · Support
- **Cost:** 70 bodies for 12 drummers. It saves whole regiments from being wiped by fleeing off the map, but never fights and, once revealed, shows hunters where survivors gather. At most two per army.
- **Looks:** A barrel-sized toy drum on a two-wheeled cart with a team-colour skin and drummers in tall hats. A drum icon with an earshot ellipse stretched downwind.
- **Does:** Placed on a rally point with one fallback point. Routed regiments inside earshot run to the drum instead of off the map and re-form after 20 s; survivors of wiped regiments join the nearest regiment there. Earshot follows the wind field; heavy rain and each thunderclap shrink it. If attackers come within 60 m, the drummers move to the fallback.
- **Strong vs:** Panic cascades, stampedes, morale-shock traps. **Weak vs:** Thunder, rain, an upwind spot, cavalry once revealed, rally routes over ice or bridges that the converging routers overload.
- **Why it's fun:** Deciding where your army runs when it breaks becomes a second plan.
- **Build risk:** Low: one flow-field target per drum.
- *From:* Command and morale

#### 45. Crossing Marshals ★

- **Seat / type:** Attacker · Support
- **Cost:** 200 bodies for 24 marshals. They can save a thousand men but add no fighting strength and burn sunset clock.
- **Looks:** Tall striped poles topped by a big round team-colour paddle. Gate glyphs at both ends of a crossing and a queue blob at strategic zoom.
- **Does:** They reach the first bridge or ice sheet ahead of the column and meter it: one regiment on the span at a time, in open file. It is a fixed rule that never reads strain, so 'regiments ignore warnings' still holds. Peak load drops about threefold, and so does throughput. If the marshals fall mid-crossing, the gate lapses and the whole queue surges onto the ice at once.
- **Strong vs:** Thin ice, rotted bridges, lure pursuers (metered too). **Weak vs:** The queue itself (a dense static crowd on a floodplain, a scoured bank or under archers), mine workings they don't mark, losing the marshals.
- **Why it's fun:** The ice trap doesn't vanish; it moves to the riverbank, where 3,000 soldiers are queueing politely.
- **Build risk:** Low: an occupancy counter per crossing and the existing wait state.
- *From:* Attacker answers

#### 46. Remount String ★

- **Seat / type:** Attacker · New: army herd
- **Cost:** 60 bodies for 120 horses, 8 herders. Never fights, but triples how many charges a cavalry wing makes over a long day.
- **Looks:** A loose mob of saddled, riderless horses under bright team-colour blankets, four herders with long poles and a tall horsetail standard. A ragged, hollow, team-tinted blob, clearly apart from the god's tan herds.
- **Does:** Grazes 100 to 200 m behind a cavalry wing. Tired cavalry ride back and swap mounts, which restores their charge (one stamina number per regiment). If fear crosses its threshold (fire, lightning, thunder, dense smoke), the whole string bolts homeward down the attacker's own road, through the infantry and the siege queue behind it.
- **Strong vs:** The late hunt, mud and snow that drain stamina. **Weak vs:** Fire, lightning on a tree near the pen, a thunderclap, smoke blown over it.
- **Why it's fun:** The attacker paid for his own stampede, and the god only has to light a fire 100 m upwind.
- **Build risk:** Low: reuses the herd agents and the retreat flow field.
- *From:* Beasts

#### 47. Carroccio

- **Seat / type:** Both · Support (moves like siege)
- **Cost:** 150 bodies for 20 crew. Steadies a whole army but hangs its nerve on one cart that must keep to roads.
- **Looks:** An ox-drawn platform with a mast flying a giant banner and a bell, the tallest thing on the field. A big banner icon, and a bright rim on every regiment that can see it.
- **Does:** Regiments that can see the mast (400 m; fog, smoke and hills block it) resist panic contagion and recover morale faster. If the cart is lost (bogged and abandoned, through ice or a bridge, swept away or captured), every regiment that could see it takes a morale shock at the same moment.
- **Strong vs:** Panic cascades, morale-shock traps. **Weak vs:** Smoke, fog, weak bridges or ice on its road, mud, raids.
- **Why it's fun:** Every round asks where the cart goes, and one splash breaks an army's heart.
- **Build risk:** Low: a sight test against the fog field, which ranged units need anyway.
- *From:* Command and morale; extends IB 219 (banners carry the morale)

#### 48. Provost Line

- **Seat / type:** Attacker · Cavalry
- **Cost:** 90 bodies for 30 riders. Protects thousands from panic cascades, but only where they can see and the ground is wide.
- **Looks:** Riders in tall shakos holding long striped staves level like a fence. A dashed line of dots across the rear at strategic zoom.
- **Does:** They hold a line 150 m behind the assault. Routers reaching it are held for 15 s and sent back with low morale, so panic doesn't wash back through the column. But routers stop wherever they meet the line: on a bridge, ford or causeway they pile up against the reserve still pushing forward, and the outer files are pressed into the water. Routers they can't see in fog or smoke slip past.
- **Strong vs:** Morale-shock traps in open ground. **Weak vs:** Chokepoints, fog, smoke, mud.
- **Why it's fun:** Break the enemy on the bridge, and their own provosts turn the rout into a crush.
- **Build risk:** Low: a line segment that switches routers to a held state.
- *From:* Command and morale; extends IB 23 (rout backwash crush)

#### 49. Bank Menders

- **Seat / type:** Both (best for the attacker) · Support
- **Cost:** 45 bodies for 10 menders. One post can save a regiment from one flood, but only on one strip of one bank.
- **Looks:** Waders, team-colour blanket rolls and long boat hooks held upright like a row of question marks. A hook icon with a catch strip drawn downstream.
- **Does:** Posted at a crossing, they move to the bank downstream where swept bodies wash ashore and haul out their own side's swept soldiers within 6 m, stunned, with the drowning timer cleared. They can't reach under ice, in fog, or above a flow-speed cap (the rope snaps), and they back away from rising water like anyone else.
- **Strong vs:** Floods, ford surges, bridges collapsing into slow water. **Weak vs:** Broken ice, fast flood crests, fog, floods at a different crossing.
- **Why it's fun:** The attacker fishes his friends out with boat hooks, so you flood the next ford instead.
- **Build risk:** Low: one spatial-hash query along a bank strip per post.
- *From:* Command and morale

#### 50. Field Kitchen

- **Seat / type:** Both · Support
- **Cost:** 40 bodies for 6 cooks. Recovery is cheap, but it packs your army into one predictable, heated spot.
- **Looks:** A wagon with a giant cauldron and a stovepipe puffing smoke rings, and aproned cooks with ladles. A cauldron icon with a warm halo and a smoke streak downwind.
- **Does:** When its army halts (waiting out rain or a high river), it parks on a flat dry cell beside water and lights up: a 20 m warm patch in the temperature field and a smoke plume. Regiments within 60 m shed soak and chill and recover morale twice as fast, so halted regiments crowd round it. Rain puts it out and fire burns it.
- **Strong vs:** Cold snaps, soaked troops (IB 33), long halts; it lets a defender use cold powers without freezing its own men. **Weak vs:** Lakeshores (it softens the ice its crowd stands on), wind turning its smoke onto its own archers, dry grass, rain.
- **Why it's fun:** The enemy's lunch melts the lake under their own reserve.
- **Build risk:** Low: heat and smoke sources in existing fields.
- **World action:** Heats and smokes a camp.
- *From:* Command and morale

#### 51. Sellswords (x2)

- **Seat / type:** Both (pay-wagon version: attacker) · Infantry
- **Cost:** 75 bodies for 100 soldiers. The cheapest real bodies in the game, with a single point of failure. A pay wagon costs 60; a bribe costs 100.
- **Looks:** Slashed doublets in team colour with a gold stripe, floppy plumed hats, coin purses and a coin banner; team colour hatched with gold. The pay wagon is an iron chest heaped with oversized coins.
- **Does:** Solid line infantry with a contract. They won't wade above the knee, so a shallow flood stops them. Two variants. Pay wagon: if the wagon bogs, sinks or is swept away, or they are out of its range for 30 s, they march home through their own column, and spilled coins pull nearby regiments into a scooping mob. Bribe: once revealed, the opponent may pay to pick a tick when they furl their banners and walk off the map; the owner can pay wages to raise the price.
- **Strong vs:** Body-starved budgets, soaking arrows, early rounds. **Weak vs:** Shallow floods, ice and weak bridges on the baggage road, a rich opponent, being your last line.
- **Why it's fun:** You sink one wagon and watch 1,500 sellswords shrug and walk home.
- **Build risk:** Low: a distance check or a scheduled event, plus the existing rout movement.
- *From:* Command and morale, Economy

#### 52. Recruiting Wagon

- **Seat / type:** Both · New: economy
- **Cost:** 150 bodies for 1 wagon, 20 crew. Attacker 150, paying 60 a round; defender 60, paying 25. Bought in round 1 it pays four times; in round 3 it is a loss.
- **Looks:** A gaudy wagon with a bass drum on top, ribbons, a team-colour canopy and a coin barrel. A wagon glyph with a coin icon and a dashed line to its village.
- **Does:** Drives under siege rules to a village on the enemy half chosen at deployment. Parked there when a round ends, it raises the owner's body budget for the next round (for the attacker, only when the round ends in a hold). It is visible from its second round, so its road becomes a known target.
- **Strong vs:** Passive opponents. **Weak vs:** Bridges tuned to its weight, muddy roads, cavalry raids.
- **Why it's fun:** A drum wagon trundles over your rival's bridge carrying next round's army in its barrel.
- **Build risk:** Low: a siege-like unit and one check at the end of the round.
- *From:* Economy

#### 53. Mule Train

- **Seat / type:** Both · Support (carrier)
- **Cost:** 40 bodies for 30 mules, 10 muleteers. Multiplies archers' value and can't fight.
- **Looks:** A string of mules under big boxy team-colour panniers, led by muleteers in wide hats. Always a dotted line, never a block.
- **Does:** Carries arrows: ranged regiments within 40 m refill, and without it they get one quiver per battle. Climbs ledge paths no wagon can. Its signature: mules heed the warnings soldiers ignore. At the first strain stage (a creaking bridge, cracking ice, knee-deep rising water, drifting smoke) the train halts and won't move until it clears, and everything behind it on that road queues.
- **Strong vs:** Feeding archers in towers and on hills; never falls through ice. **Weak vs:** A god who strains a bridge to the first stage corks the attacker's road with his own mules; cavalry raids.
- **Why it's fun:** The stubborn mule is a living warning light, and a plug the enemy god can pull.
- **Build risk:** Low to build.
- **Rule question:** Conflicts with the 2026-10-09 rule that regiments react only to what is lethal right now.
- *From:* Beasts

#### 54. Herald

- **Seat / type:** Defender · Support
- **Cost:** 90 bodies for 3 heralds. Halting the enemy front for 40 s is worth more than a regiment; one parley per round stops spamming.
- **Looks:** A white flag on a pole taller than any banner, a long trumpet and white tabards with team-colour trim. Halted attackers get a white tint at strategic zoom.
- **Does:** Walks the guaranteed route at a set time. Attacker regiments that can see the flag halt for 40 s of parley wherever they stand. Fog and smoke hide it, and a limp flag in a calm is seen at half range. If any attacker becomes a casualty during the parley, the attackers are enraged (immune to panic, faster) for 60 s.
- **Strong vs:** Racing sunset, timing a flood crest. **Weak vs:** Fog, smoke, calm, mud on the herald's route, spread-out columns.
- **Why it's fun:** You hold 3,000 men still on a frozen lake while the flood crest arrives, if you can afford their fury afterwards.
- **Build risk:** Low to build; high design risk.
- **Rule question:** A rule acting on the enemy AI rather than physics; see the rule questions.
- *From:* Command and morale

## Cross-cutting rules from the brainstorm

- **Homeward bolt for army animals.** Army animals fear what the god's herds fear (heat, thunder, dense smoke, camel scent) but bolt homeward along their own army's retreat route, launching whoever is on that road, and calm at their deployment zone instead of leaving the map, so one thunderclap can't wipe a defender regiment. Blows land on riders and handlers; animals trot off when their handlers fall. This makes every animal unit a risk its buyer takes and a target the god can reach indirectly (Remount String, Hobilars, Camel Lancers).
- **One table per movement class.** Stilts, coracles, puffers, buffalo, skaters, sledges and skis all reuse one mechanism: each movement class has its own lethal water depth, mud, snow and ice costs, and ground pressure. Each class adds a family of flow fields and a distance-to-lethal field, computed only when a regiment of that class is on the map, so cap the number of classes (perhaps eight).
- **The counter loop.** Launchers (charges, the Hill Wheel, Tobogganeers, Ice Yachts, Ploughshare Cataphracts) beat dense blocks; anchors (Throwback Pikes, Leadfoots) beat launchers; load, mud and ranged beat anchors; and launchers punish the light units that dodge load. Heavies pay in load, lights pay in fragility, and the world sets the prices on each map.
- **Unit-set fire.** Dry fuel only, the wind blowing away from the unit's own side, no soldier of either side within 30 m, a fixed number of torches or pots per battle, each shown on the timeline. Rain douses the source.
- **Small units and the no-cast bubble.** A 3-man herald, a 12-man drum or a decoy would otherwise be a cheap shield against the rival god's casts. Treat support regiments under about 25 soldiers, kites and decoys like stragglers: they don't block casts.

## Pricing principles (economy agent)

- **Stop swarms with a banner fee.** Every regiment pays a flat fee of about 20 bodies plus its per-soldier price; regiments need at least 40 soldiers (25 for specialists); chaff gets 10 bodies dearer for each extra regiment of the same type. Ten regiments of 40 then cost about 25% more than four of 100.
- **Let the world tax both ends.** Floods, crowd pressure, stampedes and panic punish swarms; load (ice, bridges, mud, roads) punishes heavy elites. Price each near its fighting value and let each map tax it.
- **Price for the round it is usually bought,** and show its value curve on the card: economy units pay off early, staged engines late, decoys in the round they are placed, earthworks reward staying put.
- **Bought levels, not earned veterancy.** Losses reset every round, so free veterancy would snowball whichever seat is already holding. Each regiment can buy two levels (drill at about 40% of its price, kit at about 60%); levels stay hidden like new placements, which gives the defender cheap fresh surprises and costs the simulation nothing.
- **Cap soldiers per seat** (for example 8,000 attacker, 1,500 defender) so late budgets go into quality and round 5 stays within the performance budget.
- **Counters arrive a round late.** Hard counters should be about 1.5 to 2 times as efficient against their target and poor elsewhere, so over-countering is punishable. Doubt-makers (decoys, Sellswords, hidden levels) should be cheap enough for one fresh doubt per round, each with a crisp exposure rule.
- **Commitment costs.** Removing a unit costs about 30% of its price; units that never move (trebuchets, earthworks) sell about 15% under their raw value, because the enemy knows where they are for several rounds.

| Unit kind | Price (bodies) |
|---|---|
| Chaff infantry | 50 to 70 per 100 |
| Line infantry | 100 per 100 |
| Specialist infantry | 110 to 130 per 100 |
| Ranged | 120 to 140 per 100 |
| Cavalry | 100 to 120 per 50 riders |
| Pursuit and stealth | 130 to 160 per 50 to 60 |
| Siege | 120 to 400 per engine, crew included |
| Decoys | 30 flat |
| Economy units | 150 flat (attacker), scaled down for the defender |

## Projectile weather sort (ranged agent)

Each projectile has a different best day, so weather sorts the ranged roster.

| Projectile | Headwind | Rain | Fog or smoke | Snow or mud | Best day |
|---|---|---|---|---|---|
| Flight arrow (Wind-Chasers) | Worst | Strings slacken | Can arc on kite sight | Fine | Dry tailwind, kite up |
| Bolt (Pavise) | Barely felt | Mild | Blind | Too heavy for ice | A gale in the enemy's faces |
| Sling shot | Moderate | None | Blind | Snow buries the stones | A downpour on gravel |
| Javelin (Pilum) | None | None | Blind, short range | No run-up | Firm ground just before their ice |
| Fire arrow | Bad | Pot doused | Needs sight | Fuel won't catch | Hot, dry, steady wind away from friends |
| Ski bow | Bad | None | Snowfall hides them | Thrives | Fresh snowfall |
