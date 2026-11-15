"""Generates the Design Gate B grey-box vertical-slice map, settled water, starting plan and level JSON.

Creates:
- data/maps/b1-river-crossing/map.json, height.bin, water.bin (840 x 540 m, 1.5 m cells;
  north stone bridge, central frozen pond, southern ford, eastern defensive bluff, prevailing ENE wind)
- data/plans/b1-river-crossing.json (outnumbered garrison on the eastern bluff vs mixed attacker assault
  with cavalry, infantry, ranged, siege engines + crew, starting wooden bridge and hedgerow)
- data/levels/chapter-0/b1-river-crossing.json (LevelData v0 with objective bluff, hand powers, and
  pending Lightning / Wind Shift slots ready for M5 item 7)

Run from the repository root:
    python tools/maps/make_design_gate_b.py
"""
import json
import os
import struct
import subprocess
import sys

CELL = 1500
COLUMNS, ROWS = 560, 360  # 840 x 540 m (201,600 cells)
CENTRE_X = 260            # x = 390 m
HALF_BED = 6              # 18 m flat bed
BANK = 4                  # 6 m sloping bank on each side -> 30 m bank-to-bank (x = 375..405 m)
BED_MM = 1500

# Crossing bands along Y (rows):
# - North stone bridge around row 260 (y = 390 m), static deck 2.2 m above floor
# - Mid-north frozen pond (rows 196..212, y = 294..318 m) over a -1800 mm lagoon pool
# - South-central ford (rows 80..100, y = 120..150 m) widened to 36 m, -550 mm shallow bed
POND_ROWS = (196, 212)
POND_MM = 1800
FORD_ROWS = (80, 100)
FORD_HALF = 12
FORD_MM = 550

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def bed_depth(y):
    def ramp(lo, hi, depth, width=4):
        if lo <= y <= hi:
            return depth
        d = min(abs(y - lo), abs(y - hi))
        return depth if d == 0 else depth + (BED_MM - depth) * min(d, width) // width

    if FORD_ROWS[0] - 4 <= y <= FORD_ROWS[1] + 4:
        return ramp(*FORD_ROWS, FORD_MM)
    if POND_ROWS[0] - 4 <= y <= POND_ROWS[1] + 4:
        return ramp(*POND_ROWS, POND_MM)
    return BED_MM


def eastern_bluff_mm(x_m, y_m):
    """Gentle 2.8 m defensive plateau on the east camp bluff (x = 500..600 m, y = 200..340 m)."""
    dx = 0.0
    if x_m < 500.0:
        dx = 500.0 - x_m
    elif x_m > 600.0:
        dx = x_m - 600.0

    dy = 0.0
    if y_m < 200.0:
        dy = 200.0 - y_m
    elif y_m > 340.0:
        dy = y_m - 340.0

    dist = (dx * dx + dy * dy) ** 0.5
    if dist >= 55.0:
        return 0
    # Cosine easing -> max slope ~80 permille (< 120 permille), walkable for foot, horse and wheeled.
    t = 1.0 - dist / 55.0
    return int(round(2800 * (t * t * (3.0 - 2.0 * t))))


def height_at(x, y):
    valley = y * CELL // 2000  # 0.5 permille slope north -> south
    d = abs(x - CENTRE_X)
    if FORD_ROWS[0] <= y <= FORD_ROWS[1] and d < FORD_HALF:
        sink = FORD_MM * min(2, FORD_HALF - d) // 2
    else:
        depth = bed_depth(y)
        if d < HALF_BED:
            sink = depth
        elif d < HALF_BED + BANK:
            sink = depth * (HALF_BED + BANK - d) // BANK
        else:
            sink = 0
    x_m = (x + 0.5) * CELL / 1000.0
    y_m = (y + 0.5) * CELL / 1000.0
    return valley - sink + eastern_bluff_mm(x_m, y_m)


def write_json(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(obj, f, indent=2)
        f.write('\n')


def main():
    width_mm = COLUMNS * CELL
    depth_mm = ROWS * CELL
    river_min = (CENTRE_X - HALF_BED - BANK) * CELL
    river_max = (CENTRE_X + HALF_BED + BANK) * CELL

    heights = bytearray()
    for y in range(ROWS):
        for x in range(COLUMNS):
            heights += struct.pack('<i', height_at(x, y))

    map_dir = os.path.join(ROOT, 'data', 'maps', 'b1-river-crossing')
    os.makedirs(map_dir, exist_ok=True)
    with open(os.path.join(map_dir, 'height.bin'), 'wb') as f:
        f.write(heights)

    map_json = {
        "$comment": (
            "Design Gate B grey-box map (tools/maps/make_design_gate_b.py): 840 x 540 m river valley with a "
            "northern stone bridge (y = 390 m), a central frozen pond (y = 294..318 m, 110 mm ice), a southern "
            "wadeable ford (y = 120..150 m), an eastern defensive bluff and prevailing ENE wind."
        ),
        "version": 0,
        "id": "b1-river-crossing",
        "widthMm": width_mm,
        "depthMm": depth_mm,
        "cellSizeMm": CELL,
        "deployment": [
            {
                "side": "attacker",
                "minXMm": 60000,
                "minYMm": 40000,
                "maxXMm": 265000,
                "maxYMm": 500000,
            },
            {
                "side": "defender",
                "minXMm": 520000,
                "minYMm": 40000,
                "maxXMm": 780000,
                "maxYMm": 500000,
            },
        ],
        "water": {
            "sources": [
                {
                    "minXMm": river_min,
                    "minYMm": depth_mm - CELL,
                    "maxXMm": river_max,
                    "maxYMm": depth_mm,
                    "litresPerSecond": 18000,
                }
            ],
            "sinks": [
                {
                    "minXMm": river_min,
                    "minYMm": 0,
                    "maxXMm": river_max,
                    "maxYMm": CELL,
                }
            ],
        },
        "wind": {
            "$comment": "Prevailing ENE wind (~4.7 m/s) boosting eastern defenders' down-valley reach slightly less than attackers shooting uphill.",
            "xMmPerS": 4500,
            "yMmPerS": 1500,
        },
        "stoneBridges": [
            {
                "$comment": "Northern masonry crossing; immune to Rot, high deck above settled flow.",
                "points": [[369000, 390000], [411000, 390000]],
                "widthMm": 12000,
                "deckHeightMm": 2200,
            }
        ],
        "frozenPonds": [
            {
                "$comment": "Central ice sheet over deepened river pool (150 kg/m2 limit: carries crossing troops with visible strain unless overloaded or broken).",
                "minXMm": 370500,
                "minYMm": POND_ROWS[0] * CELL,
                "maxXMm": 409500,
                "maxYMm": POND_ROWS[1] * CELL,
                "thicknessMm": 150,
            }
        ],
    }
    write_json(os.path.join(map_dir, 'map.json'), map_json)

    plan_json = {
        "$comment": "Starting plan for Design Gate B level b1-river-crossing: far stronger attacker wave overwhelms the outnumbered bluff garrison unless the player uses terrain + casts.",
        "version": 0,
        "map": "b1-river-crossing",
        "army": [
            {
                "id": "atk_cav_north",
                "side": "attacker",
                "unitType": "cavalry",
                "soldiers": 80,
                "xMm": 215000,
                "yMm": 380000,
                "facingCentiDegrees": 0,
            },
            {
                "id": "atk_inf_north",
                "side": "attacker",
                "unitType": "infantry",
                "soldiers": 160,
                "xMm": 240000,
                "yMm": 306000,
                "facingCentiDegrees": 0,
            },
            {
                "id": "atk_inf_mid",
                "side": "attacker",
                "unitType": "infantry",
                "soldiers": 160,
                "xMm": 250000,
                "yMm": 225000,
                "facingCentiDegrees": 0,
            },
            {
                "id": "atk_inf_south",
                "side": "attacker",
                "unitType": "infantry",
                "soldiers": 160,
                "xMm": 250000,
                "yMm": 135000,
                "facingCentiDegrees": 0,
            },
            {
                "id": "atk_ranged_south",
                "side": "attacker",
                "unitType": "ranged",
                "soldiers": 100,
                "xMm": 210000,
                "yMm": 135000,
                "facingCentiDegrees": 0,
            },
            {
                "id": "atk_siege",
                "side": "attacker",
                "unitType": "siege",
                "soldiers": 3,
                "xMm": 200000,
                "yMm": 225000,
                "facingCentiDegrees": 0,
            },
            {
                "id": "def_inf_bluff",
                "side": "defender",
                "unitType": "infantry",
                "soldiers": 50,
                "xMm": 535000,
                "yMm": 270000,
                "facingCentiDegrees": 18000,
            },
            {
                "id": "def_ranged_bluff",
                "side": "defender",
                "unitType": "ranged",
                "soldiers": 50,
                "xMm": 565000,
                "yMm": 270000,
                "facingCentiDegrees": 18000,
            },
            {
                "id": "def_spademen",
                "side": "defender",
                "unitType": "spademen",
                "soldiers": 50,
                "xMm": 530000,
                "yMm": 225000,
                "facingCentiDegrees": 18000,
            },
        ],
        "terrain": [
            {
                "id": "wood_bridge_mid",
                "piece": "wooden-bridge",
                "points": [[372000, 225000], [408000, 225000]],
                "widthMm": 4500,
            },
            {
                "id": "hedge_north_flank",
                "piece": "hedgerow",
                "points": [[418000, 324000], [418000, 366000]],
            },
        ],
        "casts": [],
    }
    write_json(os.path.join(ROOT, 'data', 'plans', 'b1-river-crossing.json'), plan_json)

    level_json = {
        "$comment": (
            "Design Gate B grey-box campaign level (tools/maps/make_design_gate_b.py). Attackers try to "
            "capture the eastern bluff objective; player must stop them until sunset (3:45) or rout 700 permille "
            "of the attacking force. Lightning is active in hand; Wind Shift remains in pendingPowers until wind power rules land."
        ),
        "version": 0,
        "id": "b1-river-crossing",
        "chapter": "chapter-0",
        "title": "Crossing at Oakhaven Reach (Gate B)",
        "map": "b1-river-crossing",
        "plan": "b1-river-crossing.json",
        "seed": 1,
        "allowedPieces": [
            "channel",
            "ridge",
            "hollow",
            "boulder",
            "hedgerow",
            "drystone-wall",
            "wooden-bridge",
            "footbridge",
        ],
        "hand": [
            {
                "power": "rain",
                "charges": 2,
                "note": "Swell the north river to flood the southern ford and slow cavalry.",
            },
            {
                "power": "rot",
                "charges": 2,
                "note": "Weaken the central wooden bridge under heavy infantry or siege weight.",
            },
            {
                "power": "lightning",
                "charges": 1,
                "note": "Strike the bluff-side approach or ignite the dry hedgerow (30 m fixed radius).",
            },
        ],
        "pendingPowers": [
            {
                "power": "wind-shift",
                "charges": 1,
                "note": "Pending wind power rules: shift crosswind plume over the ford/bluff.",
            },
        ],
        "outcome": {
            "variant": "objective",
            "sunsetTick": 4500,
            "armyRoutPermille": 700,
            "objectiveMinSoldiers": 60,
            "objectiveHoldTicks": 240,
            "objectivePolygonMm": [
                [500000, 200000],
                [600000, 200000],
                [600000, 340000],
                [500000, 340000],
            ],
        },
        "sideQuests": [
            {
                "id": "collapse_wood_bridge",
                "description": "Rot or overload the central wooden bridge before the infantry clears it.",
            },
            {
                "id": "hold_the_bluff",
                "description": "Keep enemy soldiers off the eastern bluff for the entire defense.",
            },
        ],
        "teachingFlags": [
            "grey-box-gate-b",
            "lightning-enabled",
        ],
    }
    write_json(os.path.join(ROOT, 'data', 'levels', 'chapter-0', 'b1-river-crossing.json'), level_json)

    print("Wrote map, plan and level JSON for b1-river-crossing. Settling water (600 s)...")
    cmd = [
        "dotnet", "run", "-c", "Release",
        "--project", os.path.join(ROOT, "sim", "tools", "GodGame.Sim.Cli"),
        "--", "settle", "--map", map_dir, "--seconds", "600",
    ]
    res = subprocess.run(cmd, cwd=ROOT, check=False)
    if res.returncode != 0:
        sys.exit(res.returncode)
    print("Settled water.bin written cleanly.")


if __name__ == '__main__':
    main()
