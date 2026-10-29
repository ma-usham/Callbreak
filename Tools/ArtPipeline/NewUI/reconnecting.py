"""Writes the "Reconnecting..." cover's Dhaka Ring (the owner's pick, 30 Oct 2026): the ring that turns
and the seal that stays.

Usage: python -P Tools/ArtPipeline/NewUI/reconnecting.py [work-dir]
Needs Python 3 with numpy and Pillow, and Microsoft Edge (render.py). Writes, under Assets/_Project/Art/UI/Table:
  UI_Reconnecting_Ring.png   twelve stitched gold diamonds round the seal, a bright one at the top with
                             its glow, the eleven others fading clockwise from it (its trail, so it runs
                             anticlockwise)
  UI_Reconnecting_Seal.png   the ivory seal in its gold rim, with the crimson spade and its drop shadow
Figma: "Reconnecting A · Dhaka Ring" 238:3 on "V1 Rebuild · Options". Its pieces are drawn in mockup
pixels (the loading screen's ring is 150 of them across). Figma's generator rotated each diamond about its
corner, which put the ring 2.9 px right of and 9.8 px above the seal; here the ring is centred on the seal,
as the generator meant, so that it turns round it without a wobble.
The table blends its UI in linear light, so a translucent pixel looks lighter there than in Figma's sRGB
blend: each one's opacity is set so that over the cover (the green cloth under its 45% dim) it looks as
Figma draws it, which keeps the trail fading as it does there.
"""
import math
import os
import sys
import tempfile

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from render import render  # noqa: E402

ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "UI", "Table")

PX = 2 * 1920 / 1672                    # sprite pixels per mockup pixel: two per canvas unit
RING_PX, SEAL_PX = 576, 424             # the sprites' sizes; TableCustomizer shows them at PX
GROUND = (64, 105, 76)                  # the green dhaka cloth's middle, before the cover's dim
DIM = 0.45                              # TableCustomizer.ReconnectingDim, black

GOLD = '<stop offset="0" stop-color="#F7DE8E"/><stop offset="0.45" stop-color="#D9A646"/><stop offset="1" stop-color="#9C6A1E"/>'
SPADE = ("M31 0 C42.16 14.88 62 26.04 62 42.16 C62 54.56 50.84 62 40.92 58.28 C37.2 57.04 34.72 54.56 33.48 52.08 "
         "C34.72 59.52 38.44 65.72 44.64 69.44 L17.36 69.44 C23.56 65.72 27.28 59.52 28.52 52.08 "
         "C27.28 54.56 24.8 57.04 21.08 58.28 C11.16 62 0 54.56 0 42.16 C0 26.04 19.84 14.88 31 0 Z")


def svg(half, body, defs):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{2 * half:.3f}" height="{2 * half:.3f}" '
            f'viewBox="{-half:.3f} {-half:.3f} {2 * half:.3f} {2 * half:.3f}"><defs>{defs}</defs>{body}</svg>')


def ring():
    """The bright diamond (20 px, 96 from the centre) and eleven 14 px ones at 92, every 30 degrees."""
    defs = (f'<linearGradient id="gold" x1="0" y1="0" x2="0" y2="1">{GOLD}</linearGradient>'
            '<filter id="glow" x="-1" y="-1" width="3" height="3"><feDropShadow dx="0" dy="0" stdDeviation="5" '
            'flood-color="#FFD966" flood-opacity="0.9"/></filter>')
    body = []
    for i, opacity in enumerate([1, 0.9, 0.85, 0.8, 0.75, 0.7, 0.65, 0.6, 0.55, 0.5, 0.45, 0.4]):
        side, radius = (20, 96) if i == 0 else (14, 92)
        a = math.radians(30 * i)
        x, y = radius * math.sin(a), -radius * math.cos(a)
        # Figma's diamonds are squares turned 45 degrees, their gold running from the upper left edge.
        paint = 'fill="#FFE9A8" filter="url(#glow)"' if i == 0 else f'fill="url(#gold)" opacity="{opacity}"'
        body.append(f'<rect x="{-side / 2}" y="{-side / 2}" width="{side}" height="{side}" '
                    f'transform="translate({x:.4f} {y:.4f}) rotate(-45)" {paint}/>')
    return svg(RING_PX / PX / 2, "".join(body), defs)


def seal():
    """The 120 px ivory disc, its 6 px gold rim inside, its shadow, and the 62 px spade."""
    defs = ('<linearGradient id="rim" gradientUnits="userSpaceOnUse" x1="0" y1="-60" x2="0" y2="60">' + GOLD + '</linearGradient>'
            '<linearGradient id="spade" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#C2272F"/>'
            '<stop offset="0.55" stop-color="#9A1822"/><stop offset="1" stop-color="#6E0E16"/></linearGradient>'
            '<filter id="shadow" x="-1" y="-1" width="3" height="3"><feDropShadow dx="0" dy="4" stdDeviation="9" '
            'flood-color="#000" flood-opacity="0.5"/></filter>')
    body = ('<g filter="url(#shadow)"><circle r="60" fill="#FBF2DF"/></g>'
            '<circle r="57" fill="none" stroke="url(#rim)" stroke-width="6"/>'
            f'<path transform="translate(-31 -39)" d="{SPADE}" fill="url(#spade)"/>')
    return svg(SEAL_PX / PX / 2, body, defs)


def to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_light(path):
    """Each translucent pixel's opacity, so that blended in linear light over the cover's ground it
    looks as Figma's sRGB blend does."""
    rgba = np.asarray(Image.open(path), dtype=np.float64) / 255.0
    colour, alpha = rgba[..., :3], rgba[..., 3]
    ground = np.array(GROUND) / 255.0
    ground_linear = to_linear(ground) * (1.0 - DIM)
    ground_srgb = np.where(ground_linear <= 0.0031308, ground_linear * 12.92, 1.055 * ground_linear ** (1 / 2.4) - 0.055)
    weights = np.array([0.2126, 0.7152, 0.0722])
    seen = to_linear(alpha[..., None] * colour + (1.0 - alpha[..., None]) * ground_srgb) @ weights
    own, under = to_linear(colour) @ weights, ground_linear @ weights
    reach = own - under
    steady = np.abs(reach) > 0.01
    fitted = np.where(steady, (seen - under) / np.where(steady, reach, 1.0), alpha)
    rgba[..., 3] = np.where((alpha > 0) & (alpha < 1), np.clip(fitted, 0.0, 1.0), alpha)
    Image.fromarray(np.round(rgba * 255.0).astype(np.uint8), "RGBA").save(path, optimize=True)


def main():
    work = sys.argv[1] if len(sys.argv) > 1 else tempfile.mkdtemp(prefix="newui-reconnecting-")
    os.makedirs(work, exist_ok=True)
    for name, maker, size in (("Ring", ring, RING_PX), ("Seal", seal, SEAL_PX)):
        source = os.path.join(work, f"{name}.svg")
        with open(source, "w", encoding="utf-8") as fh:
            fh.write(maker())
        out = os.path.join(OUT, f"UI_Reconnecting_{name}.png")
        render(source, out, size, size, transparent=True)
        linear_light(out)
        print("wrote", os.path.relpath(out, ROOT))


if __name__ == "__main__":
    main()
