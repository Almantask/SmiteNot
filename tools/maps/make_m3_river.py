"""Writes data/maps/m3-river and data/plans/m3-river-two-thousand.json: the M3 debug map for watching water.

A small map (360 x 240 m, 1.5 m cells) so the river fills in minutes: a channel 18 m wide with 1.2 m banks at
200 permille runs north to south down a 0.5 permille valley, fed by 25 m3/s at the north edge and drained at the south.
A ford at y = 112 to 128 m spreads the river 51 m wide over a bed 0.6 m below the land (low enough that it never dams the river onto the plain); a pool at y = 30 to 60 m drops it to 3 m, deep enough
to close to every class. Attackers start in the west, defenders in the east.

Run from the repository root: python tools/maps/make_m3_river.py
"""
import json
import os
import struct

CELL = 1500
COLUMNS, ROWS = 240, 160
CENTRE = COLUMNS // 2
HALF_BED = 6      # cells: an 18 m bed
BANK = 4          # cells of bank on each side: 1.2 m over 6 m
BANK_MM = 1200
FORD = (75, 85)   # rows
POOL = (20, 40)   # rows
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def bed_depth(y):
    """How far the channel bed lies below the banks at row y, in mm."""
    def ramp(lo, hi, depth, width=4):
        if lo <= y <= hi:
            return depth
        d = min(abs(y - lo), abs(y - hi))
        return depth if d == 0 else (depth + (BANK_MM - depth) * min(d, width) // width)
    if FORD[0] - 4 <= y <= FORD[1] + 4:
        return ramp(*FORD, FORD_MM)
    if POOL[0] - 6 <= y <= POOL[1] + 6:
        return ramp(*POOL, 3000, 6)
    return BANK_MM


FORD_HALF = 17    # cells: the ford spreads the river 51 m wide, so the flow over it is shallow and slow enough to wade
FORD_MM = 600


def height(x, y):
    valley = y * CELL // 2000  # 0.5 permille: higher in the north
    d = abs(x - CENTRE)
    if FORD[0] <= y <= FORD[1] and d < FORD_HALF:
        # Its edges slope over two cells (200 permille), so even the siege train can drive down into it.
        return valley - FORD_MM * min(2, FORD_HALF - d) // 2
    depth = bed_depth(y)
    if d < HALF_BED:
        sink = depth
    elif d < HALF_BED + BANK:
        # The bank slopes from the bed up to the land; a deep pool keeps a steeper inner wall.
        sink = depth * (HALF_BED + BANK - d) // BANK
    else:
        sink = 0
    return valley - sink


heights = bytearray()
for y in range(ROWS):
    for x in range(COLUMNS):
        heights += struct.pack('<i', height(x, y))

width, depth = COLUMNS * CELL, ROWS * CELL
river_min = (CENTRE - HALF_BED - BANK) * CELL
river_max = (CENTRE + HALF_BED + BANK) * CELL
mapdir = os.path.join(ROOT, 'data', 'maps', 'm3-river')
os.makedirs(mapdir, exist_ok=True)
map_json = {
    "$comment": "M3 debug map for watching water (written by tools/maps/make_m3_river.py): 360 x 240 m, an 18 m channel with 1.2 m banks running north to south down a 0.5 permille valley, 25 m3/s in at the north edge and out at the south; a ford at y = 112 to 128 m where the river spreads 51 m wide over a bed 0.6 m below the land and a 3 m deep pool at y = 30 to 60 m. Attacker zone in the west, defender zone in the east.",
    "version": 0,
    "id": "m3-river",
    "widthMm": width,
    "depthMm": depth,
    "cellSizeMm": CELL,
    "deployment": [
        {"side": "attacker", "minXMm": 0, "minYMm": 0, "maxXMm": 45000, "maxYMm": depth},
        {"side": "defender", "minXMm": width - 45000, "minYMm": 0, "maxXMm": width, "maxYMm": depth},
    ],
    "water": {
        "sources": [{"minXMm": river_min, "minYMm": depth - CELL, "maxXMm": river_max, "maxYMm": depth,
                     "litresPerSecond": 25000}],
        "sinks": [{"minXMm": river_min, "minYMm": 0, "maxXMm": river_max, "maxYMm": CELL}],
    },
}
with open(os.path.join(mapdir, 'map.json'), 'w', encoding='utf-8', newline='\n') as f:
    json.dump(map_json, f, indent=2)
    f.write('\n')
with open(os.path.join(mapdir, 'height.bin'), 'wb') as f:
    f.write(heights)

army = []
types = ['cavalry'] * 4 + ['infantry'] * 8 + ['siege'] * 2
for i, unit in enumerate(types):
    column, row = divmod(i, 7)
    army.append({"id": "a%03d" % i, "side": "attacker", "unitType": unit, "soldiers": 50 if unit == 'siege' else 100,
                 "xMm": 30000 - column * 18000 // 2, "yMm": 20000 + row * 32000, "facingCentiDegrees": 0})
for i in range(4):
    army.append({"id": "d%03d" % i, "side": "defender", "unitType": "infantry", "soldiers": 100,
                 "xMm": width - 25000, "yMm": 40000 + i * 50000, "facingCentiDegrees": 18000})
plan = {
    "$comment": "M3 debug plan on m3-river (written by tools/maps/make_m3_river.py): 1,700 attackers (4 cavalry, 8 infantry and 2 siege regiments) march east across the river to 400 defenders, who hold.",
    "version": 0,
    "map": "m3-river",
    "army": army,
    "terrain": [],
    "casts": [],
}
with open(os.path.join(ROOT, 'data', 'plans', 'm3-river-two-thousand.json'), 'w', encoding='utf-8', newline='\n') as f:
    json.dump(plan, f, indent=2)
    f.write('\n')
print('wrote m3-river', COLUMNS, 'x', ROWS)
