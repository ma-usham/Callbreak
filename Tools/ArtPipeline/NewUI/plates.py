"""Lays out Figma's "Unity Export · Plates" sheet from plates.json (ADR 058).

Usage: python -P Tools/ArtPipeline/NewUI/plates.py <layout.json>
Each kit sprite in plates.json gets a cell: its pixel size (or the "size" it is given), and the scale
its plate is drawn at, its pixels per unit / 100 (a 200-ppu sprite has two pixels per Figma unit). A
"compose" sprite's cell is drawn wider (cut_plates.py keeps its two ends and a plain middle). Prints
the cells as figma-plates.js takes them: [node, x, y, cell width, cell height, scale, fit].
"""
import json
import os
import re
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, "..", "..", "..", "Assets", "_Project", "Art")
SHEET_WIDTH, GAP = 3200, 24


def main(out_path):
    spec = json.load(open(os.path.join(HERE, "plates.json"), encoding="utf-8"))
    cells = []
    for s in spec["sprites"]:
        path = os.path.join(ART, s["path"])
        w, h = s.get("size") or Image.open(path).size
        meta = open(path + ".meta", encoding="utf-8").read()
        scale = float(re.search(r"spritePixelsToUnits: ([\d.]+)", meta).group(1)) / 100.0
        cell = dict(s, w=w, h=h, scale=scale, cw=max(2 * w, 640) if s.get("compose") else w, ch=h)
        cells.append(cell)
    x = y = shelf = 0
    for c in sorted(cells, key=lambda c: -c["ch"]):
        if x + c["cw"] > SHEET_WIDTH:
            x, y, shelf = 0, y + shelf + GAP, 0
        c["x"], c["y"] = x, y
        x += c["cw"] + GAP
        shelf = max(shelf, c["ch"])
    layout = {"width": SHEET_WIDTH, "height": y + shelf, "cells": cells}
    json.dump(layout, open(out_path, "w", encoding="utf-8"), indent=1)
    print("sheet", layout["width"], layout["height"])
    print(json.dumps([[c["node"], c["x"], c["y"], c["cw"], c["ch"], round(c["scale"], 4), c.get("fit", "")] for c in cells], separators=(",", ":")))


if __name__ == "__main__":
    main(sys.argv[1])
