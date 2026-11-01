# Unit spec: look, motion and rendering

What soldiers, mounts and crews look like on screen and how they move. Unit types and behaviour live in `claude/game-brief.md`; the simulation side lives in `claude/tech-notes.md`. This doc extends tech notes section 7 (rendering 10,000 soldiers); the brief wins if they disagree.

## Target: TABS-level detail, our own look

Decided 2026-10-09. Units match the level of detail of Totally Accurate Battle Simulator (Landfall): simple, chunky, stylised bodies that read at a glance, gear you can identify from the camera, and big, exaggerated physical consequences (but no wobble; see Motion). We don't copy its look (its body shape, faces, palette or proportions).

What "TABS-level detail" means here:

- **Few, simple parts.** Head, torso, upper and lower limbs, mitt hands. No fingers, no modelled faces beyond a simple mark (or none), no cloth simulation.
- **Flat colour, no fine texture.** Each part takes a colour from one small shared palette texture. No normal maps or photographic textures: detail comes from silhouette, colour blocks and lighting.
- **Gear carries identity.** Each type has one signature silhouette (below). Helmets, shields and weapons are separate simple shapes, big enough to read from the default camera.
- **Variety by mixing, not modelling.** Two to four variants each of helmet, body build and colour shade per type, chosen per soldier from the simulation seed, so a regiment reads as a crowd rather than clones.
- **Team colour on big areas** (tabard, shield face, plume) so sides read at every zoom.
- **Cosmetics never touch readability.** Star-unlocked cosmetics (brief, 2026-10-09) change paint patterns, banner emblems, trims, effects and sounds, but never a unit's silhouette, size or team-colour areas.

Tone: playful (decided 2026-10-09). Comedy comes from exaggerated consequences, never from luck or from mocking the player; no gore, and the fallen tumble, splash and vanish like toys.

Own look (open, see the brief): proportions, palette and materials distinct from TABS. Candidates that suit a god looking down on a battlefield: painted tabletop miniatures, carved and painted wooden figures, lacquered tin soldiers. Each fits the same detail budget.

## Per-type silhouettes

| Type | Signature read |
|---|---|
| Infantry | Big shield plus spear or sword. The most numerous type, so the cheapest mesh |
| Cavalry | Horse and rider as one silhouette; lance or plume lifts it above the infantry line |
| Ranged | Bow or crossbow held out, quiver, lighter build, no big shield |
| Siege | The engine itself (moving arm, wheels, ropes) plus a small crew sharing the infantry body |
| Herds | Same detail level and rig approach as horses, coloured apart from both armies |

One officer or standard-bearer per regiment carries a tall plume or banner, so regiments read as units and the AI's regiment-level decisions are visible.

## Motion: exaggerated physics, no wobble

Decided 2026-10-09: physical consequences are exaggerated, soldiers don't wobble, and performance comes first. TABS gets its feel by simulating every unit as a self-balancing physics ragdoll, which limits it to hundreds of units and makes its outcomes vary between runs. Here the exaggeration lives in the deterministic simulation as a few numbers per soldier, so it is cheap, identical on every machine, and what the player sees is what happened.

- **No wobble.** No idle sway and no spring motion. Soldiers play clean baked animations (vertex animation textures, tech notes section 7): walk, run, wade, brace, swing, shoot, fall, swept, flee, tumble, crumple. A seeded phase and stride per soldier keeps ranks out of lockstep at no cost.
- **Animation follows the ground.** The clip is picked from the fields the soldier stands in: heavy, slipping steps in mud; arms out on ice, wider as the load nears breaking; high steps in snow; wading with raised arms, then swept. It is only a choice of clip, so it costs nothing extra, and it keeps the warnings.
- **Teetering at the edge.** Soldiers never walk into lethal hazards (brief, 2026-10-09), so a soldier pressed against deep water or a cliff by the crowd behind him teeters, arms windmilling, before he goes over. It is the warning and the gag in one clip.
- **Big launches, decided by the simulation.** Cavalry charges, stampedes, falling trees, collapsing bridges and siege stones throw soldiers much further than reality would. A launched soldier becomes a ballistic body in the simulation (position, velocity, spin), follows a fixed-point arc with reduced gravity for extra hang time, and lands where the simulation says. The screen draws that arc with a tumbling spin and a crumple on landing. While planning, the preview shows each source's landing spread, red where the landing kills (brief, 2026-10-09).
- **Spectacle is not lethality.** Launch distance and damage are tuned separately. Most launched soldiers survive, stunned for a few seconds and out of formation; landing in deep water, off a cliff or into fire is what kills. Exaggeration breaks formations and morale without turning battles into a damage race.
- **No chain reactions.** A landing soldier knocks neighbours down (a short stagger) but never launches them, so one charge can't bowl through an army and small plan edits can't snowball into a different battle.
- **Weapon contact:** melee swings are timed to the simulation's contact events, so a blow lands on screen when it lands in the data.
- **Deaths:** a cartoon gag per cause of death (below); launched soldiers who die finish their simulated arc first. No runtime ragdolls.
- **Effects to match.** Splashes, dust and debris are sized up to the exaggeration and fade quickly. Anything that blocks movement (a fallen tree, a collapsed span) comes from the simulation, never from an effect, and bodies never pile up where the simulation has no obstacle.
- **Readable when zoomed out.** A charge or stampede scatters dots out of the regiment's shape, so launches read from strategic zoom. Close-up warnings also get a zoomed-out form: cracks drawn on loaded ice, a rising-water tint, a regiment tint.

**Cost:** an airborne soldier adds a few fixed-point numbers and one arc step per tick; rendering is the same instanced draw with a different clip and a rotation, so thousands can be in the air at once without a cap. The playback track records each launch (tick, position, velocity, spin), so scrubbing and replays redraw identical arcs.

**Tuning knobs:** launch speed multiplier per source; gravity scale for launched bodies (hang time); stun duration; landing damage per surface.

## Death gags (decided 2026-10-11)

Every death plays an exaggerated, funny gag chosen by the cause the simulation recorded, so the player can tell at a glance what killed whom. Toy-like and cartoon-styled: no blood, gore or suffering.

| Cause | Gag |
|---|---|
| Drowned | Arms wave above the surface, then the soldier sinks with a bubble; shield, helmet and spear bob up and drift off with the current |
| Swept by a flood or dam-break wave | Tumbles head over heels in the wave front; a few ride their shields like surfboards; dumped dazed on the bank, or sinks as drowned |
| Crushed (boulder, falling tree, collapse) | Pancake-flat, then peels up like paper and flutters away |
| Trampled (stampede, charge) | Pressed flat into the ground with hoof-prints across the back |
| Burned | Turns soot-black, blinks once, crumbles into a little pile of ash that the wind carries off |
| Fell through ice | Drops through with arms up as the slab tips; the helmet keeps spinning on the ice |
| Buried (avalanche, landslide) | Only a hand, a spear or a helmet plume sticks out of the snow or mud |
| Off a cliff | Windmills at the edge, then drops out of sight with a falling whistle and a puff of dust |
| Combat | Exaggerated throw-back; the weapon spins away |

Rules:

- **Presentation only.** The gag is picked from the death event (cause, tick, position, heading, seed), so the preview, the live battle, replays and the opponent's screen all show the same one.
- **Free gauges.** Floating gear follows the water-flow field and blowing ash follows the wind field, so mass deaths also show the current and the wind.
- **Built cheaply.** Baked clips plus shader parameters (flatten, char, sink) and a few instanced props; no per-soldier physics.
- **Remains.** Pancakes, soot figures and floating gear last a few seconds, then vanish (working assumption; open question in the brief).
- **Readable first.** A gag never hides a warning. At strategic zoom it shrinks to a splash, a puff or a fading mark; mass-casualty moments cap the number of floating props, and the Low preset thins them further.

## Level of detail

| Distance | What is drawn |
|---|---|
| Hero (near camera, a few hundred soldiers) | Full parts and gear, ground-driven clips, tumbling launches; about 1,000 to 2,500 triangles with gear |
| Mid | One merged mesh with gear baked in, about 300 to 600 triangles, animation textures |
| Far | Under about 100 triangles or impostor cards: team colour and type silhouette only |
| Strategic zoom | Shaded dots or regiment shapes, tinted by morale and state |

Triangle counts are starting budgets for the High preset. The Low preset targets integrated graphics at 20 fps (brief, 2026-10-11): it caps hero-detail soldiers at about a hundred near the camera and switches to mid, far and dot representations sooner, while keeping every silhouette, team colour and warning readable. Graphics settings move LOD distances and effect density only, never gameplay.

## Surface state

Shader-driven from the fields and per-instance values: wet sheen and darker cloth from wetness; mud climbing the legs with distance marched through mud; frost on helmets in the cold; scorch and soot near fire; snow on shoulders; dulled, dripping gear after being swept. What a soldier looks like is what it has been through.

## Out of scope

- Per-soldier rigid bodies or active balance in the simulation.
- Wobble: idle sway and spring-based secondary motion (dropped 2026-10-09).
- Runtime ragdolls (dropped 2026-10-09; launches and deaths use simulated arcs and baked clips).
- Facial animation, cloth simulation, dismemberment.
- Gore: none (playful tone, 2026-10-09). No blood, no dismemberment; cartoon remains vanish after a few seconds (see Death gags).
