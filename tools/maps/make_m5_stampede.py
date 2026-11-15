"""Writes data/maps/m5-stampede: grey-box map (heightfield + zones + road soil only; no plan).

Layout (480 x 360 m, 1.5 m cells):
- Road: runs east-west along y = 60..84 m (24 m wide packed road soil across x = 0..480 m).
- Herd grazing meadow: level terrace around x = 240 m, y = 124 m (40 m north of the road edge y = 84 m).
- Dry grove plateau: gentle 1.8 m rise centered at x = 240 m, y = 224 m (100 m north beyond the herd meadow).
- Attacker zone: west approach (x = 0..75 m, y = 0..160 m); Defender zone: east road exit (x = 390..480 m, y = 0..180 m).

Run from the repository root: python tools/maps/make_m5_stampede.py
"""
import json
import math
import os
import struct

CELL = 1500
COLUMNS, ROWS = 320, 240  # 480 x 360 m
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def smoothstep(edge0, edge1, x):
    t = max(0.0, min(1.0, (x - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


def height_mm(x_cell, y_cell):
    x_m = (x_cell + 0.5) * CELL / 1000.0
    y_m = (y_cell + 0.5) * CELL / 1000.0

    # Gentle north-to-south drainage slope (~4 permille overall) outside roadbed.
    base_mm = y_m * 4.0

    # Subtle crowned roadbed along y = 60..84 m (flat across crown, slight shallow ditches).
    road_dist = abs(y_m - 72.0)
    if road_dist <= 12.0:
        road_crown = 180.0 * (1.0 - (road_dist / 12.0) ** 2)
    else:
        ditch_t = smoothstep(12.0, 22.0, road_dist)
        road_crown = -140.0 * (1.0 - ditch_t) * math.exp(-((road_dist - 15.0) ** 2) / 18.0)

    # Slight grazing terrace around (240 m, 124 m), 40 m beyond road edge.
    meadow_r = math.hypot(x_m - 240.0, y_m - 124.0)
    meadow_mm = 200.0 * (1.0 - smoothstep(18.0, 55.0, meadow_r))

    # Gentle dry grove hillock around (240 m, 224 m), 100 m beyond the grazing meadow.
    grove_r = math.hypot(x_m - 240.0, y_m - 224.0)
    grove_mm = 1800.0 * (1.0 - smoothstep(16.0, 62.0, grove_r))

    return int(round(base_mm + road_crown + meadow_mm + grove_mm))


def main():
    width = COLUMNS * CELL
    depth = ROWS * CELL

    heights = bytearray()
    for y in range(ROWS):
        for x in range(COLUMNS):
            heights += struct.pack("<i", height_mm(x, y))

    map_dir = os.path.join(ROOT, "data", "maps", "m5-stampede")
    os.makedirs(map_dir, exist_ok=True)
    map_doc = {
        "$comment": (
            "M5 Stampede grey-box map (heightfield + zones only, written by tools/maps/make_m5_stampede.py): "
            "packed east-west road at y = 60..84 m (24 m wide), grazing meadow centered around (240, 124) m "
            "(40 m beside the road edge), and dry grove rise centered at (240, 224) m (100 m beyond the herd)."
        ),
        "version": 0,
        "id": "m5-stampede",
        "widthMm": width,
        "depthMm": depth,
        "cellSizeMm": CELL,
        "deployment": [
            {
                "side": "attacker",
                "minXMm": 0,
                "minYMm": 0,
                "maxXMm": 75000,
                "maxYMm": 160000,
            },
            {
                "side": "defender",
                "minXMm": width - 90000,
                "minYMm": 0,
                "maxXMm": width,
                "maxYMm": 180000,
            },
        ],
        "soils": [
            {
                "soil": "road",
                "minXMm": 0,
                "minYMm": 60000,
                "maxXMm": width,
                "maxYMm": 84000,
            }
        ],
    }

    with open(os.path.join(map_dir, "map.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(map_doc, f, indent=2)
        f.write("\n")
    with open(os.path.join(map_dir, "height.bin"), "wb") as f:
        f.write(heights)
    print(f"make_m5_stampede: wrote {map_dir} ({COLUMNS}x{ROWS} cells, {width // 1000}x{depth // 1000} m)")


if __name__ == "__main__":
    main()
