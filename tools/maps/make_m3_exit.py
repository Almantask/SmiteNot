"""Writes data/maps/m3-exit and its plans: the M3 exit-criteria map (spec S1, section 13).

A 480 x 600 m valley whose sides fall 10 permille toward a river running north to south down the middle (0.5 permille
fall), an 18 m bed with 1.2 m banks, fed 10 m3/s at the north edge and drained at the south. A ford at y = 140 to 160 m
spreads the river 30 m wide over a bed 0.6 m below the banks: at base flow foot soldiers wade it, and when rain upstream
roughly multiplies the flow it sweeps them. Attackers start in the west, south of the middle, and march for the defenders
in the east, so the ford is their crossing; the rain falls 300 m upstream, far from both armies.

Plans:
- m3-exit-dry.json: the armies only.
- m3-exit-rain.json: the same, plus an 80 m storm on the river 300 m upstream of the ford at 0:50 (tick 1,000).
- m3-exit-moat.json: the rain plus a 1.5 m channel dug across the valley floor between the storm and the ford.

Run from the repository root: python tools/maps/make_m3_exit.py
"""
import json
import os
import struct

CELL = 1500
COLUMNS, ROWS = 320, 400
CENTRE = COLUMNS // 2
HALF_BED = 6
BANK = 4
BANK_MM = 1200
FORD = (93, 106)       # rows: y = 140 to 160 m
FORD_HALF = 10         # cells: 30 m wide
FORD_MM = 600
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def height(x, y):
    valley = y * CELL // 2000 + abs(x - CENTRE) * CELL // 100  # 0.5 permille along, 10 permille sides
    d = abs(x - CENTRE)
    if FORD[0] <= y <= FORD[1] and d < FORD_HALF:
        return valley - FORD_MM * min(2, FORD_HALF - d) // 2
    if d < HALF_BED:
        return valley - BANK_MM
    if d < HALF_BED + BANK:
        return valley - BANK_MM * (HALF_BED + BANK - d) // BANK
    return valley


heights = bytearray()
for y in range(ROWS):
    for x in range(COLUMNS):
        heights += struct.pack('<i', height(x, y))

width, depth = COLUMNS * CELL, ROWS * CELL
river_min = (CENTRE - HALF_BED - BANK) * CELL
river_max = (CENTRE + HALF_BED + BANK) * CELL
mapdir = os.path.join(ROOT, 'data', 'maps', 'm3-exit')
os.makedirs(mapdir, exist_ok=True)
with open(os.path.join(mapdir, 'map.json'), 'w', encoding='utf-8', newline='\n') as f:
    json.dump({
        "$comment": ("M3 exit-criteria map (spec S1, section 13; written by tools/maps/make_m3_exit.py): a 480 x 600 m "
                     "valley falling 10 permille to an 18 m river (1.2 m banks, 0.5 permille fall), 10 m3/s in at the north "
                     "edge and out at the south; a 30 m ford at y = 140 to 160 m over a bed 0.6 m below the banks."),
        "version": 0,
        "id": "m3-exit",
        "widthMm": width,
        "depthMm": depth,
        "cellSizeMm": CELL,
        "deployment": [
            {"side": "attacker", "minXMm": 0, "minYMm": 0, "maxXMm": 30000, "maxYMm": depth},
            {"side": "defender", "minXMm": width - 30000, "minYMm": 0, "maxXMm": width, "maxYMm": depth},
        ],
        "water": {
            "sources": [{"minXMm": river_min, "minYMm": depth - CELL, "maxXMm": river_max, "maxYMm": depth,
                         "litresPerSecond": 10000}],
            "sinks": [{"minXMm": river_min, "minYMm": 0, "maxXMm": river_max, "maxYMm": CELL}],
        },
    }, f, indent=2)
    f.write('\n')
with open(os.path.join(mapdir, 'height.bin'), 'wb') as f:
    f.write(heights)

army = []
for i in range(6):
    army.append({"id": "a%03d" % i, "side": "attacker", "unitType": "infantry", "soldiers": 100,
                 "xMm": 20000, "yMm": 100000 + i * 20000, "facingCentiDegrees": 0})
for i in range(2):
    army.append({"id": "d%03d" % i, "side": "defender", "unitType": "infantry", "soldiers": 100,
                 "xMm": width - 15000, "yMm": 130000 + i * 40000, "facingCentiDegrees": 18000})
storm = {"id": "storm", "power": "rain", "tick": 1000, "xMm": CENTRE * CELL, "yMm": 450000, "radiusMm": 80000}
moat = {"id": "moat", "piece": "channel", "points": [[165000, 330000], [300000, 330000]], "widthMm": 4500,
        "depthMm": 1500}


def plan(name, comment, terrain, casts):
    with open(os.path.join(ROOT, 'data', 'plans', name), 'w', encoding='utf-8', newline='\n') as f:
        json.dump({"$comment": comment, "version": 0, "map": "m3-exit", "army": army, "terrain": terrain,
                   "casts": casts}, f, indent=2)
        f.write('\n')


plan('m3-exit-dry.json', "M3 exit control (tools/maps/make_m3_exit.py): 600 attackers march for the ford, no god.", [], [])
plan('m3-exit-rain.json', "M3 exit (tools/maps/make_m3_exit.py): the same march, and an 80 m storm on the river 300 m upstream of the ford at 0:50.", [], [storm])
plan('m3-exit-moat.json', "M3 exit (tools/maps/make_m3_exit.py): the storm, and a 1.5 m channel dug across the valley floor between it and the ford.", [moat], [storm])
print('wrote m3-exit', COLUMNS, 'x', ROWS)
