"""Writes data/maps/m5-charge: grey-box map (heightfield + zones only; no plan).

Layout (420 x 240 m, 1.5 m cells):
- Clean open grassland ground across the whole map with subtle broad crowning (< 15 permille slope),
  room for parallel lanes holding one close-order infantry block (e.g. y ~ 75 m) and one open-order block
  (e.g. y ~ 165 m) facing oncoming cavalry.
- Attacker zone: west (x = 0..90 m, y = 0..240 m); Defender zone: east (x = 330..420 m, y = 0..240 m).

Run from the repository root: python tools/maps/make_m5_charge.py
"""
import json
import math
import os
import struct

CELL = 1500
COLUMNS, ROWS = 280, 160  # 420 x 240 m
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def height_mm(x_cell, y_cell):
    x_m = (x_cell + 0.5) * CELL / 1000.0
    y_m = (y_cell + 0.5) * CELL / 1000.0
    # Smooth open prairie rise in the middle (~0.6 m peak, max slope ~6 permille).
    nx = (x_m - 210.0) / 140.0
    ny = (y_m - 120.0) / 120.0
    r2 = nx * nx + ny * ny
    return int(round(600.0 * math.exp(-r2)))


def main():
    width = COLUMNS * CELL
    depth = ROWS * CELL

    heights = bytearray()
    for y in range(ROWS):
        for x in range(COLUMNS):
            heights += struct.pack("<i", height_mm(x, y))

    map_dir = os.path.join(ROOT, "data", "maps", "m5-charge")
    os.makedirs(map_dir, exist_ok=True)
    map_doc = {
        "$comment": (
            "M5 Charge grey-box map (heightfield + zones only, written by tools/maps/make_m5_charge.py): "
            "420 x 240 m open ground with full-depth attacker (west) and defender (east) deployment zones "
            "for testing close-order vs open-order infantry blocks under charge impact."
        ),
        "version": 0,
        "id": "m5-charge",
        "widthMm": width,
        "depthMm": depth,
        "cellSizeMm": CELL,
        "deployment": [
            {
                "side": "attacker",
                "minXMm": 0,
                "minYMm": 0,
                "maxXMm": 90000,
                "maxYMm": depth,
            },
            {
                "side": "defender",
                "minXMm": width - 90000,
                "minYMm": 0,
                "maxXMm": width,
                "maxYMm": depth,
            },
        ],
    }

    with open(os.path.join(map_dir, "map.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(map_doc, f, indent=2)
        f.write("\n")
    with open(os.path.join(map_dir, "height.bin"), "wb") as f:
        f.write(heights)
    print(f"make_m5_charge: wrote {map_dir} ({COLUMNS}x{ROWS} cells, {width // 1000}x{depth // 1000} m)")


if __name__ == "__main__":
    main()
