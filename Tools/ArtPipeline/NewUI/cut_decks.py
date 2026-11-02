"""Cuts the game's card decks out of Figma's "Card Decks" export sheets (the owner, 2 Nov 2026: "in figma we have
new card design us that, give classic card as default and set other to be purcahsed from shop"; ADR 068).

Usage: python -P Tools/ArtPipeline/NewUI/cut_decks.py <sheets-dir> [art-dir] [--with vintage]
The sheets are the frames of section "Unity Export · Card Decks" on page "Card Decks" 338:2, one per deck, each
shot at 1x (get_screenshot, maxDimension 6848, contentsOnly) on a black and on a white fill, since Figma's
screenshots have no alpha and it is recovered from the two:
  <key>_black.png, <key>_white.png   13 x 5 cells of 512 x 712, 16 apart: the spades, hearts, clubs and diamonds
                                     from the ace down to the two, then the back alone in the fifth row
Every deck has its own faces (its paper, frame lines, indices, pips and courts all differ), so each card is
written whole, as the Figma card is, at the game's size: scaled down in premultiplied linear light and stored
with straight alpha (the rounded corners are see-through), each file keeping its .meta (GUID, import
settings: uncompressed, mip maps, no atlas; ADR 048). Round the card, EDGE see-through pixels: a card turned on
the table then has its straight edges smoothed by the texture's filtering, where an edge that is the picture's
own edge is drawn in steps (the owner, 2 Nov 2026: "the card that are tilted has rough edges"; ADR 073). The
table draws the picture that much larger, so the card keeps its size (TableCardBuilder.ArtEdge):
  Art/Cards/<Deck>/Card_<Suit>_<Rank>.png   the 52 whole cards, 278 x 386 in a 282 x 390 picture
  Art/Cards/<Deck>/Back_<Deck>.png          the back, the same
The Figma sheets are the source; re-exporting is the four sheets shot again and this script.
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
CARDS = os.path.join(ROOT, "Assets", "_Project", "Art", "Cards")
# Sheet key -> the deck's folder (its id is the folder in kebab case: CosmeticCatalog.Decks). Vintage's sheet
# (key "vintage", folder "Vintage") is shot too, but the deck waits to be downloaded only by those who buy it:
# its art alone would add about 16 MB to every phone's download (ADR 068). Pass --with vintage to cut it.
DECKS = {"classic": "Classic", "royal": "KathmanduRoyal", "mithila": "MithilaInk"}
LATER = {"vintage": "Vintage"}
SUITS = ["Spades", "Hearts", "Clubs", "Diamonds"]
RANKS = ["A", "K", "Q", "J", "10", "9", "8", "7", "6", "5", "4", "3", "2"]
CELL_W, CELL_H, GAP = 512, 712, 16
# The game's card: the old 386 px height (ADR 048, 061), at Figma's 512 x 712 shape, and the see-through
# pixels round it (ADR 073).
SIZE = (278, 386)
EDGE = 2


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
    # Fully see-through pixels carry no colour, so they compress to nothing.
    pixels = np.clip(np.dstack([rgb, alpha]) * 255.0 + 0.5, 0, 255).astype(np.uint8)
    pixels[pixels[..., 3] == 0] = 0
    return pixels


def framed(pixels):
    """The card with EDGE see-through pixels round it."""
    return np.pad(pixels, ((EDGE, EDGE), (EDGE, EDGE), (0, 0)))


def save(path, pixels):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(pixels, "RGBA").save(path, optimize=True)


def cells(colour, alpha):
    """(name, colour, alpha) for each card and the back, in sheet order."""
    for r, suit in enumerate(SUITS + ["Back"]):
        for c, rank in enumerate(RANKS if suit != "Back" else [None]):
            x, y = c * (CELL_W + GAP), r * (CELL_H + GAP)
            yield (f"Card_{suit}_{rank}" if rank else None), colour[y:y + CELL_H, x:x + CELL_W], alpha[y:y + CELL_H, x:x + CELL_W]


def main(sheets_dir, art_dir=CARDS, *later):
    decks = dict(DECKS)
    decks.update({key: LATER[key] for key in later})
    for key, folder in decks.items():
        colour, alpha = recover(os.path.join(sheets_dir, f"{key}_black.png"), os.path.join(sheets_dir, f"{key}_white.png"))
        for name, c, a in cells(colour, alpha):
            name = name or f"Back_{folder}"
            save(os.path.join(art_dir, folder, name + ".png"), framed(to8(*shrink(c, a, SIZE))))
        print(f"{folder}: 52 cards and the back")


if __name__ == "__main__":
    args = sys.argv[1:]
    later = []
    while "--with" in args:
        i = args.index("--with")
        later.append(args[i + 1])
        del args[i:i + 2]
    main(*args, *later)
