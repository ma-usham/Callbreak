"""Cuts the game's logos and the Cloths tab icon out of Figma's "Unity Export · NewUI" sheet (ADR 058).

Usage: python -P Tools/ArtPipeline/NewUI/cut_sheet.py <sheet-on-black.png> <sheet-on-white.png>
The sheet (on the V1 Rebuild · Design System page) holds the logo the owner chose on 30 Oct 2026, Art/Logo ·
Temple Crown (Logo_Callbreak) and Art/Logo Compact · Temple Crown (the two Logo_Callbreak_NoFlag), and the
folded-cloth icon of the shop's drawer, each fitted into the ink box of the game sprite it replaces, at
that sprite's pixel size. Figma's screenshots have no alpha, so the sheet is shot on black and on white and the alpha
is recovered from the difference.
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
UI = os.path.join(ROOT, "Assets", "_Project", "Art", "UI")

# sheet cell (x, y, width, height) -> the sprite it becomes
CELLS = [
    ((0, 0, 1602, 636), "Ornaments/Logo_Callbreak.png"),
    ((0, 676, 1602, 366), "Common/Logo_Callbreak_NoFlag.png"),
    ((0, 1082, 768, 190), "Decor/Logo_Callbreak_NoFlag.png"),
    ((808, 1082, 218, 144), "Icons/Icon_TableOval_Ivory.png"),
    ((1066, 1082, 218, 144), "Icons/Icon_TableOval_Brown.png"),
]


def main(black_path, white_path):
    black = np.asarray(Image.open(black_path).convert("RGB"), dtype=np.float32) / 255.0
    white = np.asarray(Image.open(white_path).convert("RGB"), dtype=np.float32) / 255.0
    alpha = np.clip(1.0 - (white - black).mean(axis=2), 0.0, 1.0)
    colour = np.where(alpha[..., None] > 1e-3, black / np.maximum(alpha[..., None], 1e-3), 0.0)
    rgba = (np.dstack([np.clip(colour, 0, 1), alpha]) * 255 + 0.5).astype(np.uint8)
    for (x, y, w, h), sprite in CELLS:
        out = os.path.join(UI, sprite)
        Image.fromarray(rgba[y:y + h, x:x + w], "RGBA").save(out, optimize=True)
        print("wrote", os.path.relpath(out, ROOT))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
