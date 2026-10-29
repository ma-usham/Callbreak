"""Cuts the kit sprites the V1 rebuild redrew out of Figma's "Unity Export · Plates" sheet (ADR 058).

Usage: python -P Tools/ArtPipeline/NewUI/cut_plates.py <layout.json> <sheet-on-black.png> <sheet-on-white.png> [out-dir]
The layout is plates.py's, and the sheet figma-plates.js's: each plate fills its sprite's pixel size at
the sprite's scale, without its drop shadow (the game draws that as a separate sprite). Figma's
screenshots have no alpha, so the sheet is shot on black and on white and the alpha is recovered from
the difference. A "compose" sprite was drawn wide: it is cut to its two ends (as far as their ornaments
reach) and a plain 16-pixel middle, and given that border. Writes over Assets/_Project/Art/<path> (or
into out-dir, to look first) and, for a sprite whose border changed, its .meta and its ArtSource/Kit
entry (without out-dir). Every sprite keeps its GUID and pixels per unit.
"""
import glob
import json
import math
import os
import re
import sys

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ART = os.path.join(ROOT, "Assets", "_Project", "Art")
MIDDLE = 16


def ends(cell):
    """How far a wide plate's ornaments reach from each side: the columns that differ from its middle."""
    a = cell.astype(np.float32)
    centre = a[:, a.shape[1] // 2]
    diff = np.abs(a - centre[:, None, :]).mean(axis=(0, 2))
    plain = diff <= 2.5
    mid = a.shape[1] // 2
    left = mid - int(np.argmin(plain[:mid][::-1])) if not plain[:mid].all() else 0
    right = int(np.argmin(plain[mid:])) if not plain[mid:].all() else 0
    right = a.shape[1] - (mid + right)
    return left, right


def compose(cell):
    left, right = ends(cell)
    border = int(math.ceil(max(left, right) / 2.0) * 2) + 8
    mid = cell.shape[1] // 2
    out = np.concatenate([cell[:, :border], cell[:, mid - MIDDLE // 2: mid + MIDDLE // 2], cell[:, -border:]], axis=1)
    return out, [border, 0, border, 0]


def shadow_of(face, spec):
    """The face's drop shadow as the kit draws it: its silhouette, padded, blurred, in a dark brown."""
    px, py = spec["pad"]
    silhouette = Image.new("L", (face.shape[1] + 2 * px, face.shape[0] + 2 * py), 0)
    silhouette.paste(Image.fromarray(face[..., 3]), (px, py))
    alpha = np.asarray(silhouette.filter(ImageFilter.GaussianBlur(spec["blur"])), dtype=np.float32)
    alpha = np.clip(alpha * 255.0 / max(alpha.max(), 1.0), 0, 255).astype(np.uint8)
    out = np.zeros(alpha.shape + (4,), dtype=np.uint8)
    out[..., 0], out[..., 1], out[..., 2], out[..., 3] = 42, 26, 18, alpha
    return out


def set_border(sprite_path, nine):
    """nine is ArtSource/Kit's [left, top, right, bottom]; the .meta keeps x left, y bottom, z right, w top."""
    l, t, r, b = nine
    meta = sprite_path + ".meta"
    text = open(meta, encoding="utf-8").read()
    text = re.sub(r"spriteBorder: \{x: [\d.]+, y: [\d.]+, z: [\d.]+, w: [\d.]+\}", f"spriteBorder: {{x: {l}, y: {b}, z: {r}, w: {t}}}", text)
    open(meta, "w", encoding="utf-8", newline="").write(text)
    relative = os.path.relpath(sprite_path, ART).replace("\\", "/")
    for kit in glob.glob(os.path.join(ROOT, "ArtSource", "Kit", "*.json")):
        raw = open(kit, encoding="utf-8").read()
        pattern = r'(\{ "path": "' + re.escape(relative) + r'", "ppu": [\d.]+, "nine": )\[[^\]]*\]'
        if re.search(pattern, raw):
            open(kit, "w", encoding="utf-8", newline="").write(re.sub(pattern, r"\g<1>" + json.dumps(nine), raw))


def main(layout_path, black_path, white_path, out_dir=None):
    layout = json.load(open(layout_path, encoding="utf-8"))
    black = np.asarray(Image.open(black_path).convert("RGB"), dtype=np.float32) / 255.0
    white = np.asarray(Image.open(white_path).convert("RGB"), dtype=np.float32) / 255.0
    alpha = np.clip(1.0 - (white - black).mean(axis=2), 0.0, 1.0)
    colour = np.where(alpha[..., None] > 1e-3, black / np.maximum(alpha[..., None], 1e-3), 0.0)
    rgba = (np.dstack([np.clip(colour, 0, 1), alpha]) * 255 + 0.5).astype(np.uint8)
    for c in layout["cells"]:
        cell = rgba[c["y"]:c["y"] + c["ch"], c["x"]:c["x"] + c["cw"]]
        nine = c.get("nine")
        if c.get("compose"):
            cell, nine = compose(cell)
        out = os.path.join(out_dir or ART, c["path"])
        os.makedirs(os.path.dirname(out), exist_ok=True)
        Image.fromarray(cell, "RGBA").save(out, optimize=True)
        if nine and not out_dir:
            set_border(out, nine)
        print("wrote", c["path"], f"{cell.shape[1]}x{cell.shape[0]}", f"border {nine}" if nine else "")
        if c.get("shadow"):
            spec = c["shadow"]
            shadow = shadow_of(cell, spec)
            shadow_out = os.path.join(out_dir or ART, spec["path"])
            Image.fromarray(shadow, "RGBA").save(shadow_out, optimize=True)
            shadow_nine = [nine[0] + spec["pad"][0], 0, nine[2] + spec["pad"][0], 0]
            if not out_dir:
                set_border(shadow_out, shadow_nine)
            print("wrote", spec["path"], f"{shadow.shape[1]}x{shadow.shape[0]}", f"border {shadow_nine}")


if __name__ == "__main__":
    main(*sys.argv[1:])
