"""Writes the game's scenery, cloths and shop pictures from the vector scenes (ADR 058).

Usage: python -P Tools/ArtPipeline/NewUI/build.py [work-dir [Name ...]]
Needs Python 3 with numpy and Pillow, and Microsoft Edge (render.py). The work dir keeps the SVGs and
renders (default: a temp folder); names after it (DurbarSquare, GreenDhaka, Frame for the border...) rebuild only those
pictures. Writes, under Assets/_Project/Art:
  Backgrounds/BG_<Name>.png        the shop's backgrounds, shown behind the menus (shop_scenes.durbar,
                                   the default since 30 Oct 2026, .pokhara and .monastery)
  Tables/Table_<Name>.png          the cloths, filling the screen at the table (cloths.dhaka, .dusk)
  Tables/Frame_Dhaka.png           the dhaka cloths' border, tiled round the screen (cloths.frame), 2x
  Shop/Backgrounds/Thumb_<Name>.png  the Background shop's tiles, cropped as Figma's ScenePicture frames
  Shop/Tables/Preview_<Name>.png   the Cloth shop's tiles, cropped as Figma's ClothPicture frames
The backgrounds and cloths cover the backdrop frame: 2230 x 1255 pixels for the 1920 x 1080 screen plus
its margins for 21:9 and 4:3 (TableBackdropBuilder.FrameWidth/Height), so each scene is drawn with that
bleed. The vector scenes carry Figma's grain (a NOISE effect); this adds the same, seeded.
"""
import os
import sys
import tempfile

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import cloths  # noqa: E402
import scenes as S  # noqa: E402
import shop_scenes  # noqa: E402
from render import render  # noqa: E402

ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
ART = os.path.join(ROOT, "Assets", "_Project", "Art")

FRAME_W, FRAME_H = 2230, 1255           # TableBackdropBuilder.FrameWidth / FrameHeight
CANVAS_PER_FRAME = 1920 / 1672          # a frame pixel is a mockup pixel
BLEED_X = (FRAME_W * CANVAS_PER_FRAME - 1920) / 2
BLEED_Y = (FRAME_H * CANVAS_PER_FRAME - 1080) / 2

# id art name -> (svg maker, folder, grain density)
BACKGROUNDS = {"DurbarSquare": shop_scenes.durbar, "PokharaLakeside": shop_scenes.pokhara, "MonasteryGarden": shop_scenes.monastery}
CLOTHS = {"GreenDhaka": lambda: cloths.dhaka("green"), "IndigoDhaka": lambda: cloths.dhaka("indigo"), "DuskHimalaya": cloths.dusk}

# The shop tiles' crops of the 1920 x 1080 picture, as Figma's ScenePicture / ClothPicture frames show them
# (x, y, width; Durbar Square's, which Figma's shop has no tile for, on its right-hand temples); the height
# follows from the game's picture size.
THUMBS = {"DurbarSquare": (1240, 220, 680), "PokharaLakeside": (1080, 180, 720), "MonasteryGarden": (1140, 220, 720)}
THUMB_SIZE = (590, 670)
PREVIEWS = {"GreenDhaka": (579, 141, 762), "IndigoDhaka": (579, 141, 762), "DuskHimalaya": (579, 141, 762)}
PREVIEW_SIZE = (603, 633)
PREVIEW_RADIUS = 36


def write_svg(path, text):
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(text)
    return path


def grain(path, density, seed):
    """Figma's monotone NOISE: a sprinkle of slightly darker pixels."""
    image = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)
    rng = np.random.default_rng(seed)
    shade = (rng.random(image.shape[:2]) < density) * rng.uniform(0.0, 0.07, image.shape[:2])
    image *= (1.0 - shade)[..., None]
    Image.fromarray(np.clip(image + 0.5, 0, 255).astype(np.uint8), "RGB").save(path, optimize=True)


def crop(src, box, size, out, radius=0):
    x, y, w = box
    h = w * size[1] / size[0]
    image = Image.open(src).convert("RGB").crop((round(x), round(y), round(x + w), round(y + h))).resize(size, Image.LANCZOS)
    if radius:
        mask = Image.new("L", (size[0] * 4, size[1] * 4), 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, size[0] * 4 - 1, size[1] * 4 - 1), radius * 4, fill=255)
        image = image.convert("RGBA")
        image.putalpha(mask.resize(size, Image.LANCZOS))
    image.save(out, optimize=True)


def main():
    work = sys.argv[1] if len(sys.argv) > 1 else tempfile.mkdtemp(prefix="newui-art-")
    only = set(sys.argv[2:])
    os.makedirs(work, exist_ok=True)
    pictures = [(name, fn, os.path.join(ART, "Backgrounds", f"BG_{name}.png")) for name, fn in BACKGROUNDS.items()]
    pictures += [(name, fn, os.path.join(ART, "Tables", f"Table_{name}.png")) for name, fn in CLOTHS.items()]
    # Each keeps its own grain seed, whichever are rebuilt.
    pictures = [(i, name, fn, out) for i, (name, fn, out) in enumerate(pictures) if not only or name in only]

    # The whole pictures, with the frame's bleed.
    S.set_bleed(BLEED_X, BLEED_Y)
    for i, name, fn, out in pictures:
        svg = write_svg(os.path.join(work, f"{name}_frame.svg"), fn())
        render(svg, out, FRAME_W, FRAME_H)
        grain(out, 0.4, 58 + i)
        print("wrote", os.path.relpath(out, ROOT))

    # The shop's crops, from the 1920 x 1080 picture alone.
    S.set_bleed(0, 0)
    for i, name, fn, _ in pictures:
        svg = write_svg(os.path.join(work, f"{name}.svg"), fn())
        flat = os.path.join(work, f"{name}.png")
        render(svg, flat, 1920, 1080)
        grain(flat, 0.4, 58 + i)
        if name in THUMBS:
            out = os.path.join(ART, "Shop", "Backgrounds", f"Thumb_{name}.png")
            crop(flat, THUMBS[name], THUMB_SIZE, out)
        else:
            out = os.path.join(ART, "Shop", "Tables", f"Preview_{name}.png")
            crop(flat, PREVIEWS[name], PREVIEW_SIZE, out, PREVIEW_RADIUS)
        print("wrote", os.path.relpath(out, ROOT))

    # The dhaka cloths' border, at twice the canvas size.
    if only and "Frame" not in only:
        return
    svg = write_svg(os.path.join(work, "frame.svg"), cloths.frame())
    out = os.path.join(ART, "Tables", "Frame_Dhaka.png")
    render(svg, out, cloths.FRAME * 2, cloths.FRAME * 2, transparent=True)
    print("wrote", os.path.relpath(out, ROOT))


if __name__ == "__main__":
    main()
