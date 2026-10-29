"""Cuts the game's cards out of Figma's card export sheets (the owner, 30 Oct 2026: "for now use the cards from
the figma that we created in new ui v1"; ADR 061).

Usage: python -P Tools/ArtPipeline/NewUI/cut_cards.py <sheets-dir> [art-dir exports-dir]
The sheets dir holds the four sheets on "V1 Rebuild · Cards", each shot at 1x on a black and on a white
fill (Figma's screenshots have no alpha, so it is recovered from the two):
  cards1_black/white.png   Unity Export · Cards 1: the ink of the spades and hearts (paper, outline and
                           frame lines hidden), then the blank card (only those) and the Card Back 177:15
  cards2_black/white.png   Unity Export · Cards 2: the ink of the clubs and diamonds
  whole1_black/white.png   Unity Export · Whole Cards 1: the whole spades and hearts
  whole2_black/white.png   Unity Export · Whole Cards 2: the whole clubs and diamonds
Every card is drawn at twice its 240 × 376 and scaled down in premultiplied linear light. Writes ADR 054's
layers at the game's sizes, each file keeping its .meta (GUID, import settings):
  Art/Cards/<Deck>/Base_<Deck>.png         the blank card, the same for every deck, 246 × 386
  Art/Cards/Faces/Card_<Suit>_<Rank>.png   the 52 shared faces, 246 × 386: each card's whole card less
                                           the base, premultiplied in linear light, its alpha the ink's
                                           coverage (raised only where the whole card needs it), so that
                                           over the base it gives the whole card back within a level
  Art/Cards/ClassicCrimson/Back_ClassicCrimson.png   the Card Back, 228 × 358
  ArtSource/Exports/Cards/<Deck>/Card_*.png, Base_*.png   the whole cards and the base, for review and
                                           as SharedCardFaceTests' reference; Classic Crimson's back too
Himalayan Ivory and Kathmandu Gold keep their own backs.
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
CARDS = os.path.join(ROOT, "Assets", "_Project", "Art", "Cards")
EXPORTS = os.path.join(ROOT, "ArtSource", "Exports", "Cards")
DECKS = ["ClassicCrimson", "HimalayanIvory", "KathmanduGold"]
RANKS = ["A", "K", "Q", "J", "10", "9", "8", "7", "6", "5", "4", "3", "2"]
CELL_W, CELL_H, GAP = 480, 752, 16
FACE = (246, 386)
BACK = (228, 358)
LAYOUT = {
    "cards1": [[f"Card_Spades_{r}" for r in RANKS], [f"Card_Hearts_{r}" for r in RANKS], ["Base", "Back"]],
    "cards2": [[f"Card_Clubs_{r}" for r in RANKS], [f"Card_Diamonds_{r}" for r in RANKS]],
    "whole1": [[f"Card_Spades_{r}" for r in RANKS], [f"Card_Hearts_{r}" for r in RANKS]],
    "whole2": [[f"Card_Clubs_{r}" for r in RANKS], [f"Card_Diamonds_{r}" for r in RANKS]],
}


def linear(s):
    return np.where(s <= 0.04045, s / 12.92, ((s + 0.055) / 1.055) ** 2.4)


def srgb(l):
    l = np.clip(l, 0.0, 1.0)
    return np.where(l <= 0.0031308, l * 12.92, 1.055 * np.power(l, 1 / 2.4) - 0.055)


def recover(black_path, white_path):
    """Straight colour and alpha from a sheet shot on black and on white."""
    black = np.asarray(Image.open(black_path).convert("RGB"), dtype=np.float64) / 255.0
    white = np.asarray(Image.open(white_path).convert("RGB"), dtype=np.float64) / 255.0
    alpha = np.clip(1.0 - (white - black).mean(axis=2), 0.0, 1.0)
    colour = np.where(alpha[..., None] > 1e-4, black / np.maximum(alpha[..., None], 1e-4), 0.0)
    return np.clip(colour, 0.0, 1.0), alpha


def shrink(colour, alpha, size):
    """Scales a card down in premultiplied linear light; returns its straight sRGB colour and alpha."""
    premul = linear(colour) * alpha[..., None]
    chans = [premul[..., i] for i in range(3)] + [alpha]
    out = [np.asarray(Image.fromarray(c.astype(np.float32), "F").resize(size, Image.LANCZOS), dtype=np.float64) for c in chans]
    a = np.clip(out[3], 0.0, 1.0)
    p = np.minimum(np.clip(np.dstack(out[:3]), 0.0, None), a[..., None])
    return srgb(np.where(a[..., None] > 1e-4, p / np.maximum(a[..., None], 1e-4), 0.0)), a


def to8(rgb, alpha):
    return np.clip(np.dstack([rgb, alpha]) * 255.0 + 0.5, 0, 255).astype(np.uint8)


def save(path, pixels):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(pixels, "RGBA").save(path, optimize=True)


def lift(whole, base, ink_alpha):
    """The face that gives the whole card back over the base as the GPU blends it (and as
    SharedCardFaceTests checks): linear(base) * (1 - a) + linear(face) per channel, premultiplied."""
    w, b = linear(whole[..., :3] / 255.0), linear(base[..., :3] / 255.0)
    inside = whole[..., 3] > 0
    a = np.round(ink_alpha * 255.0)
    # Where the whole card is darker than the base allows for this coverage, the ink covers more.
    need = 1.0 - np.min(np.where(b > 1e-6, w / np.maximum(b, 1e-6), 1.0), axis=2)
    a = np.clip(np.maximum(a, np.ceil(np.clip(need, 0, 1) * 255.0 - 1e-6)), 0, 255)
    a = np.where(inside, a, 0)
    under = b * (1.0 - a[..., None] / 255.0)
    p = np.clip(w - under, 0.0, 1.0)
    face = np.round(srgb(p) * 255.0)
    # Nudge each channel by a level where that lands closer to the whole card.
    best, err = face.copy(), np.full(face.shape, 1e9)
    for d in (-1, 0, 1):
        cand = np.clip(face + d, 0, 255)
        got = np.round(srgb(under + linear(cand / 255.0)) * 255.0)
        e = np.abs(got - whole[..., :3])
        better = e < err
        best, err = np.where(better, cand, best), np.where(better, e, err)
    best = np.where(inside[..., None], best, 0)
    worst = int(err[inside].max()) if inside.any() else 0
    return np.dstack([best, a]).astype(np.uint8), worst


def main(sheets_dir, art_dir=CARDS, exports_dir=EXPORTS):
    cells = {}
    for sheet, rows in LAYOUT.items():
        colour, alpha = recover(os.path.join(sheets_dir, f"{sheet}_black.png"), os.path.join(sheets_dir, f"{sheet}_white.png"))
        for r, row in enumerate(rows):
            for c, name in enumerate(row):
                x, y = c * (CELL_W + GAP), r * (CELL_H + GAP)
                cells[(sheet[:-1], name)] = (colour[y:y + CELL_H, x:x + CELL_W], alpha[y:y + CELL_H, x:x + CELL_W])

    base = to8(*shrink(*cells[("cards", "Base")], FACE))
    back = to8(*shrink(*cells[("cards", "Back")], BACK))
    for deck in DECKS:
        save(os.path.join(art_dir, deck, f"Base_{deck}.png"), base)
        save(os.path.join(exports_dir, deck, f"Base_{deck}.png"), base)
    save(os.path.join(art_dir, "ClassicCrimson", "Back_ClassicCrimson.png"), back)
    save(os.path.join(exports_dir, "ClassicCrimson", "Back_ClassicCrimson.png"), back)

    worst = 0
    for (kind, name), (colour, alpha) in sorted(cells.items()):
        if kind != "whole":
            continue
        whole = to8(*shrink(colour, alpha, FACE))
        _, ink_alpha = shrink(*cells[("cards", name)], FACE)
        face, off = lift(whole.astype(np.float64), base.astype(np.float64), ink_alpha)
        worst = max(worst, off)
        save(os.path.join(art_dir, "Faces", name + ".png"), face)
        for deck in DECKS:
            save(os.path.join(exports_dir, deck, name + ".png"), whole)
    print(f"wrote 52 faces, 3 bases, Classic Crimson's back; the faces give their whole cards back to within {worst} level(s)")


if __name__ == "__main__":
    main(*sys.argv[1:])
