"""Centres the knob of the Settings switches (the owner, 3 Nov 2026: "the toggle middle switch isnt in the center").

Usage: python -P Tools/ArtPipeline/NewUI/centre_toggle_knobs.py

UI_Toggle_On.png and UI_Toggle_Off.png (190 x 108, cut by cut_plates.py from Figma's Toggle set, 167:71 and 167:73)
drew the knob 4 px above the track's middle and, switched on, 6 px short of the right end. This draws each again:
the track from its half without the knob (the track is the same both ways), then the knob, as large and as white as
before, in the middle of the track's height and as far from the end it sits at as from the top and the bottom.
Each file keeps its .meta. (Figma's Toggle set still draws the old knob: cutting the plates again brings it back.)
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
CONTROLS = os.path.join(ROOT, "Assets", "_Project", "Art", "UI", "Controls")

# The knob: its diameter and its white, brightest at its middle (measured on the cut sprites).
KNOB = 80.0
KNOB_MIDDLE = np.array([254.0, 254.0, 254.0])
KNOB_EDGE = np.array([240.0, 235.0, 231.0])
SUPERSAMPLE = 4


def track(pixels, knob_left):
    """The track alone: the half without the knob, mirrored onto the half with it."""
    h, w, _ = pixels.shape
    out = pixels.copy()
    middle = (w - 1) / 2.0
    for x in range(w):
        mirror = int(round(2 * middle - x))
        has_knob = (x < middle) if knob_left else (x > middle)
        if has_knob:
            out[:, x] = pixels[:, mirror]

    # The columns either side of the middle: the track runs straight there, so the nearest clean column.
    clean = int(middle) + (2 if knob_left else -2)
    for x in (int(middle), int(middle) + 1):
        out[:, x] = pixels[:, clean]
    return out


def knob(size, centre):
    """The knob's colour and coverage at each pixel, drawn at SUPERSAMPLE times and averaged down."""
    h, w = size
    s = SUPERSAMPLE
    ys, xs = np.mgrid[0:h * s, 0:w * s]
    cx, cy = centre
    r = np.hypot((xs + 0.5) / s - cx, (ys + 0.5) / s - cy)
    inside = (r <= KNOB / 2.0).astype(float)
    t = np.clip(r / (KNOB / 2.0), 0.0, 1.0)[..., None]
    colour = KNOB_MIDDLE + (KNOB_EDGE - KNOB_MIDDLE) * t * t
    cover = inside.reshape(h, s, w, s).mean(axis=(1, 3))
    rgb = (colour * inside[..., None]).reshape(h, s, w, s, 3).sum(axis=(1, 3))
    rgb = np.where(cover[..., None] > 0, rgb / np.maximum(cover[..., None] * s * s, 1e-6), 0.0)
    return rgb, cover


def centre(path, knob_left):
    pixels = np.asarray(Image.open(path).convert("RGBA"), dtype=np.float64)
    h, w, _ = pixels.shape
    out = track(pixels, knob_left)
    margin = (h - KNOB) / 2.0
    cx = margin + KNOB / 2.0 if knob_left else w - margin - KNOB / 2.0
    rgb, cover = knob((h, w), (cx, h / 2.0))
    out[..., :3] = out[..., :3] * (1.0 - cover[..., None]) + rgb * cover[..., None]
    out[..., 3] = np.maximum(out[..., 3], cover * 255.0)
    Image.fromarray(np.clip(out + 0.5, 0, 255).astype(np.uint8), "RGBA").save(path, optimize=True)
    print(f"{os.path.basename(path)}: knob centred at ({cx:.1f}, {h / 2.0:.1f}), {margin:.1f} px from the edges")


if __name__ == "__main__":
    centre(os.path.join(CONTROLS, "UI_Toggle_On.png"), knob_left=False)
    centre(os.path.join(CONTROLS, "UI_Toggle_Off.png"), knob_left=True)
