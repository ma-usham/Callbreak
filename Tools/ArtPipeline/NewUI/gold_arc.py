"""Writes the Gold Arc (the owner, 31 Oct 2026: "use golder arc for every loading screen") for the loading
screen and its black look: the arc that turns and the seal that stays.

Usage: python -P Tools/ArtPipeline/NewUI/gold_arc.py [work-dir [Loading|Dark ...]]
Needs Python 3 with numpy and Pillow, and Microsoft Edge (render.py). Names after the work dir write only
those sets. Writes, under Assets/_Project/Art/UI/Decor:
  Deco_LoadingRing.png, Deco_LoadingSeal.png   over the loading screen's background, in its ring's place
                                   between the logo and "Loading..." (the files the Dhaka Ring had there)
  Deco_LoadingRing_Dark.png, Deco_LoadingSeal_Dark.png   the same over the curtain's black look (ADR 042)
The ring: a faint cream track and the gold arc on it, from the top round past the bottom (243 degrees), with
its glow. The seal: the logo's crimson disc in its gold rim, with its shadow, and the cream spade.
Figma: "Reconnecting C · Gold Arc" 238:56383 on "V1 Rebuild · Options", drawn in mockup pixels like the
Dhaka Ring (dhaka_ring.py): the same scale and sprite sizes, so the Gold Arc stands as much smaller than the
Dhaka Ring as Figma draws it. As there, each translucent pixel's opacity is set for the game's linear-light
blend over what lies under it.
"""
import math
import os
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from dhaka_ring import GOLD, PX, RING_PX, SEAL_PX, SPADE, UI, ROOT, linear_light, svg  # noqa: E402
from render import render  # noqa: E402

SCALE = 0.75                            # the loading screen's size, as the Dhaka Ring's there

# Each set: its files, and the ground under it (sRGB) with the black dim over that.
SETS = {
    # Over Durbar Square's light middle, as the Dhaka Ring's loading pair was set.
    "Loading": (("Decor/Deco_LoadingRing.png", "Decor/Deco_LoadingSeal.png"), (245, 232, 214), 0.0),
    # Over the black look's black.
    "Dark": (("Decor/Deco_LoadingRing_Dark.png", "Decor/Deco_LoadingSeal_Dark.png"), (0, 0, 0), 0.0),
}

OUTER, INNER = 85.0, 76.5               # the 170 px ring, its inner radius 0.9 of its outer
START, END = -90.0, 153.0               # the arc's ends, degrees clockwise from the right (Figma's arcData)


def point(radius, degrees):
    a = math.radians(degrees)
    return radius * math.cos(a), radius * math.sin(a)


def band(start, end):
    """A ring's band from one angle clockwise to another (y runs down, as in Figma)."""
    large = 1 if (end - start) % 360 > 180 else 0
    (ox1, oy1), (ox2, oy2) = point(OUTER, start), point(OUTER, end)
    (ix1, iy1), (ix2, iy2) = point(INNER, end), point(INNER, start)
    return (f"M{ox1:.4f} {oy1:.4f} A{OUTER} {OUTER} 0 {large} 1 {ox2:.4f} {oy2:.4f} "
            f"L{ix1:.4f} {iy1:.4f} A{INNER} {INNER} 0 {large} 0 {ix2:.4f} {iy2:.4f} Z")


def ring():
    """The cream track at 18% all round, and the gold arc over it with its 10 px glow."""
    defs = (f'<linearGradient id="gold" gradientUnits="userSpaceOnUse" x1="0" y1="{-OUTER}" x2="0" y2="{OUTER}">'
            f'{GOLD}</linearGradient>'
            '<filter id="glow" x="-1" y="-1" width="3" height="3"><feDropShadow dx="0" dy="0" stdDeviation="5" '
            'flood-color="#FFCC59" flood-opacity="0.6"/></filter>')
    track = f'<path d="{band(0, 180)} {band(180, 360)}" fill="#FBF2DF" fill-opacity="0.18"/>'
    arc = f'<path d="{band(START, END)}" fill="url(#gold)" filter="url(#glow)"/>'
    return svg(RING_PX / PX / 2, track + arc, defs)


def seal():
    """The 116 px crimson disc, its 5 px gold rim inside, its shadow, and the 56 px cream spade 4.6 px above
    the middle."""
    defs = ('<linearGradient id="rim" gradientUnits="userSpaceOnUse" x1="0" y1="-58" x2="0" y2="58">' + GOLD + '</linearGradient>'
            '<linearGradient id="disc" gradientUnits="userSpaceOnUse" x1="0" y1="-58" x2="0" y2="58">'
            '<stop offset="0" stop-color="#C2272F"/><stop offset="0.55" stop-color="#9A1822"/>'
            '<stop offset="1" stop-color="#6E0E16"/></linearGradient>'
            '<filter id="shadow" x="-1" y="-1" width="3" height="3"><feDropShadow dx="0" dy="4" stdDeviation="9" '
            'flood-color="#000" flood-opacity="0.5"/></filter>')
    body = ('<g filter="url(#shadow)"><circle r="58" fill="url(#disc)"/></g>'
            '<circle r="55.5" fill="none" stroke="url(#rim)" stroke-width="5"/>'
            f'<path transform="translate(-28 -36) scale({56 / 62:.6f})" d="{SPADE}" fill="#FBF2DF"/>')
    return svg(SEAL_PX / PX / 2, body, defs)


def main():
    work = sys.argv[1] if len(sys.argv) > 1 else tempfile.mkdtemp(prefix="newui-gold-arc-")
    only = set(sys.argv[2:])
    os.makedirs(work, exist_ok=True)
    for name, maker in (("Ring", ring), ("Seal", seal)):
        with open(os.path.join(work, f"{name}.svg"), "w", encoding="utf-8") as fh:
            fh.write(maker())
    for place, (files, ground, dim) in SETS.items():
        if only and place not in only:
            continue
        for name, file, size in (("Ring", files[0], RING_PX), ("Seal", files[1], SEAL_PX)):
            out = os.path.join(UI, *file.split("/"))
            pixels = round(size * SCALE)
            render(os.path.join(work, f"{name}.svg"), out, pixels, pixels, transparent=True)
            linear_light(out, ground, dim)
            print("wrote", os.path.relpath(out, ROOT), pixels)


if __name__ == "__main__":
    main()
