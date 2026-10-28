"""Cuts the V2 scenes and tables into the game's backgrounds, tables and shop pictures.

The pictures come from the 'Unity Export · Scenes' section (figma-scenes-sheet.js): each scene and
table widened to 2560 x 1440, exported one picture at a time at 1x (Figma caps an export at 4096 px
a side), named as scenes-sheet.json lists them. A scene's back is opaque; its front (and vignette) is
exported on black and on white and its alpha recovered as kit.py recovers the kit's, then laid on the
back as Figma lays it (in sRGB values), so each scene is one opaque picture.

Writes, for each scene and table, in the backdrop frame (2230 x 1255, the 1920 x 1080 screen plus
margins for 21:9 and 4:3; TableBackdropBuilder):
- Art/Backgrounds/BG_<Name>.png: the scene, behind the menus;
- Art/Tables/Table_<Name>.png: the table, filling the screen at the table;
- Art/Shop/Backgrounds/Thumb_<Name>.png and Art/Shop/Tables/Preview_<Name>.png: the shop's pictures,
  cut from the middle of the 1920 x 1080 picture at their files' sizes, as V2's shop tiles crop them;
  the table previews with V2's rounded corners (16 on a 316 px tall tile).

    python Tools/ArtPipeline/V2/scenes.py --folder <exports>
"""
import argparse
import json
import os

import numpy as np
from PIL import Image

from kit import ART, HERE, recover_alpha, rounded_rect_mask

FRAME = (2230, 1255)
THUMB = (590, 670)
PREVIEW = (603, 633)
PREVIEW_RADIUS = 16.0 / 316.0     # of the preview's height
SCREEN = (1920, 1080)             # the V2 picture inside the 2560 x 1440 export


def load(path):
    return np.asarray(Image.open(path).convert('RGBA'))


def opaque(rgb):
    return Image.fromarray(np.clip(np.round(rgb * 255.0), 0, 255).astype(np.uint8), 'RGB')


def middle(image, size):
    """The size's proportions cut from the middle of the export's 1920 x 1080 picture, at that size."""
    w, h = image.size
    height = SCREEN[1]
    width = height * size[0] / size[1]
    box = ((w - width) / 2.0, (h - height) / 2.0, (w + width) / 2.0, (h + height) / 2.0)
    return image.resize(size, Image.LANCZOS, box=box)


def save(image, *parts):
    path = os.path.join(ART, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path, optimize=True)
    return path


def main():
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('--folder', required=True, help='the exports: back_<i>, table_<i>, front_black_<i>, front_white_<i>.png')
    args = parser.parse_args()
    sheet = json.load(open(os.path.join(HERE, 'scenes-sheet.json'), encoding='utf-8'))

    written = []
    for i, scene in enumerate(sheet['scenes']):
        name = scene.replace(' ', '')
        back = load(os.path.join(args.folder, f'back_{i}.png'))[..., :3] / 255.0
        front = recover_alpha(load(os.path.join(args.folder, f'front_black_{i}.png')),
                              load(os.path.join(args.folder, f'front_white_{i}.png')))
        a = front[..., 3:4]
        picture = opaque(back * (1.0 - a) + front[..., :3] * a)
        written.append(save(picture.resize(FRAME, Image.LANCZOS), 'Backgrounds', f'BG_{name}.png'))
        written.append(save(middle(picture, THUMB), 'Shop', 'Backgrounds', f'Thumb_{name}.png'))

    for i, table in enumerate(sheet['tables']):
        name = table.replace(' ', '')
        picture = opaque(load(os.path.join(args.folder, f'table_{i}.png'))[..., :3] / 255.0)
        written.append(save(picture.resize(FRAME, Image.LANCZOS), 'Tables', f'Table_{name}.png'))
        preview = np.asarray(middle(picture, PREVIEW)).astype(np.float64) / 255.0
        w, h = PREVIEW
        r = PREVIEW_RADIUS * h
        mask = rounded_rect_mask(w, h, (r, r, r, r), 0, 1.0)
        rgba = np.dstack([preview, mask])
        data = np.clip(np.round(rgba * 255.0), 0, 255).astype(np.uint8)
        written.append(save(Image.fromarray(data, 'RGBA'), 'Shop', 'Tables', f'Preview_{name}.png'))

    for path in written:
        print(os.path.relpath(path, os.path.join(ART, '..', '..', '..')).replace('\\', '/'))


if __name__ == '__main__':
    main()
