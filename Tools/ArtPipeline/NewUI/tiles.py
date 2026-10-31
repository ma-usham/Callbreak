"""The shop's theme tiles: each theme's background over its cloth, as one picture (themes.py, 30 Oct 2026).

Usage: python -P Tools/ArtPipeline/NewUI/tiles.py [Theme ...]
Reads what build.py writes (Art/Backgrounds/BG_<Name>.png, Art/Tables/Table_<Name>.png, Art/Tables/Frame_<Name>.png)
and writes Art/Shop/Themes/Tile_<Theme>.png, 492 x 444 with corners rounded 21 px round (transparent outside):
  - the top 492 x 277: the background's 1920 x 1080 picture (no margin), 492 wide;
  - a gold seam (#D2A957) from 276 to 283.5 px down;
  - below it: the cloth's top-left corner as the game shows it round a 1920 x 1080 screen (its base, and its
    border tiled round the screen as Unity's 9-slice tiling does, see border()), 0.45 px to a canvas unit, from
    the cloth's top-left: canvas units 0..1093 across and 0..357 down.
The pictures carry build.py's grain.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import cloths  # noqa: E402

ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
ART = os.path.join(ROOT, "Assets", "_Project", "Art")

FRAME_W, FRAME_H = 2230, 1255           # the backdrop frame (build.py)
CANVAS_PER_FRAME = 1920 / 1672
BLEED_X = (FRAME_W * CANVAS_PER_FRAME - 1920) / 2
BLEED_Y = (FRAME_H * CANVAS_PER_FRAME - 1080) / 2
PICTURE_PPU = 1 / CANVAS_PER_FRAME      # a backdrop picture's pixels per canvas unit
BORDER_PPU = 2                          # a border's (Frame_<Name>.png, drawn at twice the canvas size)
CORNER, TILE = cloths.FRAME_BORDER, 48  # its fixed corners, and the run its edges repeat

# theme -> (background, cloth, the cloth's border or None); the shop's first three, then themes.py's.
THEMES = {
    "DurbarSquare": ("DurbarSquare", "GreenDhaka", "Dhaka"),
    "PokharaLakeside": ("PokharaLakeside", "IndigoDhaka", "Dhaka"),
    "MonasteryGarden": ("MonasteryGarden", "DuskHimalaya", None),
}
for _name in ("HimalayanSunrise", "LaliguransForest", "GoldenTerraces", "TiharLights", "PrayerFlagPass", "MithilaGarden", "PhewaMoonlight"):
    THEMES[_name] = (_name, _name, _name)

SIZE = (492, 444)
RADIUS = 21
SEAM = (276, 283.5, (0xD2, 0xA9, 0x57))
CLOTH_PPU = 0.45
SS = 4                                   # drawn four times over, then averaged down


def premultiplied(path):
    rgba = np.asarray(Image.open(path).convert("RGBA"), dtype=np.float32) / 255
    rgba[..., :3] *= rgba[..., 3:]
    return rgba


def border(frame, w, h):
    """The border round a w x h screen (canvas units) as Unity draws a Tiled 9-slice Image without its centre:
    the 168-unit corners as they are, each edge's middle 48 units repeated from the left (top and bottom edges)
    or from the bottom (the sides), the last repeat cut short. frame: premultiplied RGBA at BORDER_PPU.
    Returns premultiplied RGBA at BORDER_PPU."""
    k = BORDER_PPU
    b, t, n = CORNER * k, TILE * k, frame.shape[0]
    W, H = int(round(w * k)), int(round(h * k))
    out = np.zeros((H, W, 4), np.float32)
    out[:b, :b], out[:b, W - b:] = frame[:b, :b], frame[:b, n - b:]
    out[H - b:, :b], out[H - b:, W - b:] = frame[n - b:, :b], frame[n - b:, n - b:]
    for x in range(b, W - b, t):
        s = min(t, W - b - x)
        out[:b, x:x + s] = frame[:b, b:b + s]
        out[H - b:, x:x + s] = frame[n - b:, b:b + s]
    for y1 in range(H - b, b, -t):
        s = min(t, y1 - b)
        out[y1 - s:y1, :b] = frame[b + t - s:b + t, :b]
        out[y1 - s:y1, W - b:] = frame[b + t - s:b + t, n - b:]
    return out


def resample(img, size, box):
    """img (H x W x C float) cut to box (x0, y0, x1, y1 in its pixels) and resampled to size, per channel."""
    chans = [np.asarray(Image.fromarray(img[..., c]).resize(size, Image.LANCZOS, box=box)) for c in range(img.shape[2])]
    return np.clip(np.dstack(chans), 0, None)


def picture(path, x0, y0, x1, y1, ppu):
    """The canvas-unit rect x0..x1, y0..y1 of a backdrop picture (2230 x 1255, the screen plus its margin), at ppu."""
    img = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32) / 255
    k = PICTURE_PPU
    box = ((x0 + BLEED_X) * k, (y0 + BLEED_Y) * k, (x1 + BLEED_X) * k, (y1 + BLEED_Y) * k)
    return resample(img, (int(round((x1 - x0) * ppu)), int(round((y1 - y0) * ppu))), box)


def cloth_on_screen(table, frame, w, h, ppu, x0=None, y0=None, x1=None, y1=None):
    """A cloth as the game shows it on a w x h screen (canvas units, centred on the 1920 x 1080 picture): its
    base, its border (or None) tiled round the screen. Optionally only the screen's rect x0..x1, y0..y1 (units
    from the screen's top-left). RGB float at ppu."""
    x0, y0 = x0 or 0, y0 or 0
    x1, y1 = x1 or w, y1 or h
    ox, oy = 960 - w / 2, 540 - h / 2
    base = picture(table, ox + x0, oy + y0, ox + x1, oy + y1, ppu)
    if frame is None:
        return base
    k = BORDER_PPU
    over = resample(border(premultiplied(frame), w, h), base.shape[1::-1], (x0 * k, y0 * k, x1 * k, y1 * k))
    return base * (1 - over[..., 3:]) + over[..., :3]


def tile(theme, art=ART):
    bg, cloth, frame = THEMES[theme]
    W, H = SIZE[0] * SS, SIZE[1] * SS
    seam0, seam1 = int(SEAM[0] * SS), int(SEAM[1] * SS)
    out = np.zeros((H, W, 3), np.float32)
    k = W / 1920
    out[:seam1] = picture(os.path.join(art, "Backgrounds", f"BG_{bg}.png"), 0, 0, 1920, seam1 / k, k)
    out[seam0:seam1] = np.array(SEAM[2], np.float32) / 255
    k = CLOTH_PPU * SS
    table = os.path.join(art, "Tables", f"Table_{cloth}.png")
    border_png = os.path.join(art, "Tables", f"Frame_{frame}.png") if frame else None
    out[seam1:] = cloth_on_screen(table, border_png, 1920, 1080, k, 0, 0, W / k, (H - seam1) / k)
    rgb = out.reshape(SIZE[1], SS, SIZE[0], SS, 3).mean(axis=(1, 3))
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, W - 1, H - 1), RADIUS * SS, fill=255)
    alpha = np.asarray(mask, np.float32).reshape(SIZE[1], SS, SIZE[0], SS).mean(axis=(1, 3))
    rgba = np.dstack([np.clip(rgb * 255 + 0.5, 0, 255), alpha + 0.5]).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def build(themes=None, art=ART):
    """Writes the tiles of the given themes (all by default) from the pictures under art."""
    folder = os.path.join(art, "Shop", "Themes")
    os.makedirs(folder, exist_ok=True)
    for theme in THEMES if themes is None else themes:
        out = os.path.join(folder, f"Tile_{theme}.png")
        tile(theme, art).save(out, optimize=True)
        print("wrote", os.path.relpath(out, ROOT))


if __name__ == "__main__":
    build(sys.argv[1:] or None)
