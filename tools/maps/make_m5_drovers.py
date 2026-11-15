"""Writes data/maps/m5-drovers: grey-box map (heightfield + zones + road soil only; no plan).

Layout (450 x 240 m, 1.5 m cells):
- Central packed road: east-west along y = 102..138 m (36 m wide across x = 0..450 m) with open verges north and south,
  giving room for a herd standing squarely on the road around x = 225 m, y = 120 m.
- Attacker zone: west edge (x = 0..60 m, y = 45..195 m); Defender zone: east edge (x = 390..450 m, y = 45..195 m).

Run from the repository root: python tools/maps/make_m5_drovers.py
"""
import json
import math
import os
import struct

CELL = 1500
COLUMNS, ROWS = 300, 160  # 450 x 240 m
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def height_mm(x_cell, y_cell):
    x_m = (x_cell + 0.5) * CELL / 1000.0
    y_m = (y_cell + 0.5) * CELL / 1000.0

    # Mild east-west longitudinal grade (~2 permille) + gentle flanking rises toward north/south edges.
    along_mm = x_m * 2.0
    road_offset = abs(y_m - 120.0)
    if road_offset <= 18.0:
        cross_mm = 160.0 * (1.0 - (road_offset / 18.0) ** 2)
    else:
        flank_t = min(1.0, (road_offset - 18.0) / 80.0)
        cross_mm = 700.0 * (flank_t * flank_t)
    return int(round(along_mm + cross_mm))


def main():
    width = COLUMNS * CELL
    depth = ROWS * CELL

    heights = bytearray()
    for y in range(ROWS):
        for x in range(COLUMNS):
            heights += struct.pack("<i", height_mm(x, y))

    map_dir = os.path.join(ROOT, "data", "maps", "m5-drovers")
    os.makedirs(map_dir, exist_ok=True)
    map_doc = {
        "$comment": (
            "M5 Drovers grey-box map (heightfield + zones only, written by tools/maps/make_m5_drovers.py): "
            "36 m wide central road along y = 102..138 m for placing a herd directly on the road near (225, 120) m "
            "with clear flank margins for droving."
        ),
        "version": 0,
        "id": "m5-drovers",
        "widthMm": width,
        "depthMm": depth,
        "cellSizeMm": CELL,
        "deployment": [
            {
                "side": "attacker",
                "minXMm": 0,
                "minYMm": 45000,
                "maxXMm": 60000,
                "maxYMm": depth - 45000,
            },
            {
                "side": "defender",
                "minXMm": width - 60000,
                "minYMm": 45000,
                "maxXMm": width,
                "maxYMm": depth - 45000,
            },
        ],
        "soils": [
            {
                "soil": "road",
                "minXMm": 0,
                "minYMm": 102000,
                "maxXMm": width,
                "maxYMm": 138000,
            }
        ],
    }

    with open(os.path.join(map_dir, "map.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(map_doc, f, indent=2)
        f.write("\n")
    with open(os.path.join(map_dir, "height.bin"), "wb") as f:
        f.write(heights)
    print(f"make_m5_drovers: wrote {map_dir} ({COLUMNS}x{ROWS} cells, {width // 1000}x{depth // 1000} m)")


if __name__ == "__main__":
    main()
