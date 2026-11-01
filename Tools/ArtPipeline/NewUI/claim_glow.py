"""Draws the glow a reward ready to collect wears (the owner, 2 Nov 2026: "show some effect if its not been
collected so it will be easy to identify we should click or claim it"; ADR 069).

Usage: python -P Tools/ArtPipeline/NewUI/claim_glow.py [out-dir]
Needs Python 3 with numpy and Pillow. Writes, under Assets/_Project/Art/UI/Decor (or out-dir):
  Deco_ClaimGlow_Disc.png   the warm gold halo round the main menu's Daily Rewards disc (106 units across),
                            clear inside the disc, which ClaimHighlight breathes
  Deco_ClaimRim_Disc.png    the steady gold rim on that disc's edge, the gold of the kit's selected disc
                            (UI_ButtonDisc_Selected), light at the top and deeper at the foot
  Deco_ClaimGlow_Tile.png   the same halo round a Daily Rewards day tile, a 9-slice (border 84 px on every
                            side) for any tile's size; the tile's own gold TodayFrame is its rim
Everything is drawn at 2 px per canvas unit (pixels per unit 200, as the kit), from signed distances to the
disc's and the tile's edges, four samples a pixel each way. Each halo is clear well inside the edge and
reaches full strength under the rim (so a halo grown by ClaimHighlight's swell still meets it), then fades
out over HALO_REACH units; the tiles stand 14 units apart, so a tile's halo is all but gone at the next one.
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
DECOR = os.path.join(ROOT, "Assets", "_Project", "Art", "UI", "Decor")

PX = 2.0                   # pixels per canvas unit
SAMPLES = 4                # samples per pixel each way

AMBER = (240, 178, 58)     # the halo's warm gold
RIM_TOP, RIM_MID, RIM_FOOT = (247, 226, 163), (228, 190, 108), (178, 131, 56)

# The disc: the main menu's header disc, 106 units across.
DISC_RADIUS = 53.0
DISC_RIM = (-2.75, 1.5)    # the rim from 2.75 units inside the disc's edge to 1.5 outside
DISC_REACH = 28.0          # the halo fades out over this many units beyond the rim

# A day tile: its corners' radius (17.7 mockup px), and its gold TodayFrame from 1.6 units inside its edge to
# 2.4 outside.
TILE_RADIUS = 20.3
TILE_RIM_OUT = 2.4
TILE_REACH = 16.0
TILE_BORDER = 42.0         # the 9-slice border, units: the halo's reach and a corner
TILE_CORE = 2.0            # the stretched middle, units


def grid(half_w, half_h):
    """Sample points in units from the centre, y down, SAMPLES per pixel each way."""
    n_x, n_y = int(round(2 * half_w * PX)), int(round(2 * half_h * PX))
    step = 1.0 / (PX * SAMPLES)
    xs = (np.arange(n_x * SAMPLES) + 0.5) * step - half_w
    ys = (np.arange(n_y * SAMPLES) + 0.5) * step - half_h
    return np.meshgrid(xs, ys), (n_x, n_y)


def down(a, size):
    """Averages the samples into pixels."""
    n_x, n_y = size
    return a.reshape(n_y, SAMPLES, n_x, SAMPLES, *a.shape[2:]).mean(axis=(1, 3))


def halo(d, rim_out, reach):
    """The halo's opacity at signed distance d (units, + outside the edge): clear well inside, full under the
    rim, fading out (a soft square curve) over `reach` units beyond it."""
    inside = np.clip((d + 6.0) / 3.0, 0.0, 1.0)
    out = np.clip(1.0 - (d - rim_out) / reach, 0.0, 1.0) ** 2
    return np.where(d <= rim_out, inside, out)


def rgba(colour, alpha):
    a = np.clip(alpha, 0.0, 1.0)
    img = np.zeros(alpha.shape + (4,))
    img[..., :3] = np.array(colour, dtype=float) / 255.0 if np.ndim(colour) == 1 else colour
    img[..., 3] = a
    return img


def save(img, path):
    out = np.clip(np.round(img * 255.0), 0, 255).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(path)
    print("wrote", path, out.shape[1], "x", out.shape[0])


def disc_glow(out_dir):
    half = DISC_RADIUS + DISC_RIM[1] + DISC_REACH + 2.0
    (x, y), size = grid(half, half)
    d = np.hypot(x, y) - DISC_RADIUS
    a = down(halo(d, DISC_RIM[1], DISC_REACH), size)
    save(rgba(AMBER, a), os.path.join(out_dir, "Deco_ClaimGlow_Disc.png"))


def disc_rim(out_dir):
    half = DISC_RADIUS + DISC_RIM[1] + 1.0
    (x, y), size = grid(half, half)
    d = np.hypot(x, y) - DISC_RADIUS
    inner, outer = DISC_RIM
    cover = ((d >= inner) & (d <= outer)).astype(float)
    # Light at the top, deeper at the foot.
    t = np.clip((y / (DISC_RADIUS + outer) + 1.0) / 2.0, 0.0, 1.0)[..., None]
    top, mid, foot = (np.array(c, dtype=float) / 255.0 for c in (RIM_TOP, RIM_MID, RIM_FOOT))
    colour = np.where(t < 0.5, top + (mid - top) * (t / 0.5), mid + (foot - mid) * ((t - 0.5) / 0.5))
    # Each pixel's colour from its covered samples only, so the edges keep the rim's gold.
    alpha = down(cover, size)
    tint = down(colour * cover[..., None], size) / np.maximum(alpha[..., None], 1e-6)
    save(rgba(tint, alpha), os.path.join(out_dir, "Deco_ClaimRim_Disc.png"))


def tile_glow(out_dir):
    # A rounded rectangle whose straight edges are TILE_CORE long, inside TILE_BORDER of halo and corner.
    half = TILE_BORDER + TILE_CORE / 2.0
    (x, y), size = grid(half, half)
    edge = half - (TILE_REACH + TILE_RIM_OUT + 1.6)  # the tile's edge, so the halo ends inside the sprite
    qx, qy = np.abs(x) - (edge - TILE_RADIUS), np.abs(y) - (edge - TILE_RADIUS)
    d = np.hypot(np.maximum(qx, 0.0), np.maximum(qy, 0.0)) + np.minimum(np.maximum(qx, qy), 0.0) - TILE_RADIUS
    a = down(halo(d, TILE_RIM_OUT, TILE_REACH), size)
    save(rgba(AMBER, a), os.path.join(out_dir, "Deco_ClaimGlow_Tile.png"))
    print("  9-slice border", int(TILE_BORDER * PX), "px; the tile's edge sits", round((half - edge) * PX, 1), "px in")


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else DECOR
    os.makedirs(out_dir, exist_ok=True)
    disc_glow(out_dir)
    disc_rim(out_dir)
    tile_glow(out_dir)


if __name__ == "__main__":
    main()
