"""Gives the menu screens' labels V2's type: font, size, colour, letter spacing and place.

The V2 screens (Figma "Callbreak Game UI", page "V2 · Screens") are read by figma-screens-dump.js
into screens-v2.json. Each layout label (ArtSource/Screens/<Name>/layout.json) is matched to its
V2 text layer: by id, or as the one text inside the V2 instance its parent is named after (a
button's label), or by the table below. For a matched label it writes:
- font: V2's family and weight, as the layout's font role;
- size: V2's size in canvas units (the builder then places the label at that size instead of
  fitting it to the old mockup's lettering);
- color: V2's colour (an old two-colour gradient goes);
- spacing: V2's letter spacing, as TextMeshPro's character spacing (1/100 em);
- inkBox: the box round V2's rendered glyphs, in mockup pixels; the builder puts the label's ink
  there, by its alignment's edge;
- measure: V2's text, where the layout's sample text differs, so the label is placed by the
  text V2 measured.

Icons and art pieces (icons-v2.json: each V2 icon instance's ink box) are matched the same way,
by id or as the icon or chevron inside the V2 instance their parent is named after. Each game
file holds the V2 art (art.py fitted it in), so the file's own ink is measured and the node's
rect is set so that ink lands on V2's: icons take V2's size and place.

    python Tools/ArtPipeline/V2/screens.py            # report what would change
    python Tools/ArtPipeline/V2/screens.py --apply    # write it into the layouts
"""
import argparse
import json
import os
import re

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
SCALE_X = 1920.0 / 1672.0
SCALE_Y = 1080.0 / 941.0

FRAMES = {
    '01 Play with Friends': 'PlayWithFriends',
    '02 Create Room': 'CreateRoom',
    '03 Join Room': 'JoinRoom',
    '04 Private Lobby': 'PrivateLobby',
    '05 Matchmaking': 'Matchmaking',
    '06 Offline Setup': 'OfflineSetup',
    '07 Bidding': 'BiddingPanel',
    '08 Gameplay': 'Table',
    '11 Settings': 'Settings',
    '13 Match Menu': 'MatchMenu',
    '12 How to Play': 'HowToPlay',
    '14 Profile': 'Profile',
    '15 Shop · Cards': 'Shop',
    '17 Scene Loading': 'SceneLoading',
    '20 Shop · Backgrounds': 'ShopBackgroundsLayout',
    '21 Shop · Tables': 'ShopTablesLayout',
    '22 Name Entry': 'NamePrompt',
    '22 Earn Coins': 'EarnCoins',
    '24 Play Online': 'PlayOnline',
    '26 Daily Rewards': 'DailyRewards',
}

ROLES = {
    'Inter Regular': 'SansRegular', 'Inter Medium': 'SansMedium', 'Inter Semi Bold': 'SansSemiBold',
    'Inter Bold': 'SansBold', 'Serif SemiBold': 'SerifSemiBold', 'Serif Bold': 'SerifBold',
}

# Layout label id -> V2 text path, where neither the id nor the parent says it. "#n" picks the
# n-th text with that path.
MATCH = {
    'CreateRoom': {'TurnLabel15': 'TurnTrack/Segment/Label#0', 'TurnLabel30': 'TurnTrack/Segment/Label#1',
                   'RoomNameText': 'RoomNameField/Value'},
    'PlayOnline': {'ChipLabel0': 'ChipNormal0/Amount', 'ChipLabel1': 'ChipSelected1/Amount'},
    # 07 Bidding's own number layers are garbled (34 and 12 pt, off the chips' centres): each chip's
    # number is the Bid Chip component's own, in its place.
    'BiddingPanel': {'ChipLabel%d' % i: 'Chip%d/Number' % i for i in range(8)},
}

# Labels that take V2's font and colour but keep their fitted size and place: the game's own
# composition of a V2 label ("Buy for", coin, price, where V2 has one "Buy for 350").
STYLE_ONLY = {'ShopTablesLayout': {'BuyLabel', 'BuyPrice'}}

# Icons that keep their place: the shop drawer, which ShopCustomizer builds smaller than V2 draws it.
KEEP_ICONS = {
    'Shop': {'CategoryIcon0', 'CategoryIcon1', 'CategoryIcon2'},
    'ShopBackgroundsLayout': {'CategoryIcon0', 'CategoryIcon1'},
    'ShopTablesLayout': {'CategoryIcon2'},
}

# Nodes V2 does not draw: the chevron on the crimson and primary buttons (V2's Button/Hero and
# Button/Primary have none). They stay in the prefab, hidden, as the views still name them.
HIDDEN = {
    'CreateRoom': {'CreateChevron'},
    'HowToPlay': {'GotItChevron'},
    'JoinRoom': {'JoinChevron'},
    'OfflineSetup': {'StartChevron'},
    'PlayOnline': {'PlayQuickChevron', 'PlayStakeChevron'},
    'PlayWithFriends': {'CreateChevron', 'JoinChevron'},
}

# Icons the passes cannot match by name: layout id -> the V2 icon instance it shows.
MATCH_ICONS = {
    'Matchmaking': {'ElapsedClock': 'ElapsedPill/Icon'},
    'PrivateLobby': {'CopyIcon': 'CodePill/Button/Icon/Icon'},
    'PlayOnline': {'StakeCoins': 'StakeDisc/Icon'},
}

# V2's boxes (canvas units: x, y, w, h) for nodes inside V2 instances the dump does not open:
# the volume slider's parts (11 Settings, "Slider": track 14 high, fill to the knob's centre, a
# 44 knob, centred on the track) and Private Lobby's copy button (04, the room code's crimson
# "Button/Icon", Size S).
RECTS = {
    'Settings': {
        'VolumeSlider': [493.3, 623.7, 415.2, 44],
        'VolumeTrack': [493.3, 638.7, 415, 14],
        'VolumeFill': [493.3, 638.7, 280, 14],
        'VolumeKnob': [751.3, 623.7, 44, 44],
    },
    'PrivateLobby': {'CopyButton': [937.8, 336.15, 64, 64]},
}

# Key changes V2 asks for beyond type and place.
SET = {
    # The copy button is V2's crimson disc with its shadow (pieces.py draws the disc).
    'PrivateLobby': {'CopyButton': {
        'use': 'UI/Buttons/UI_ButtonIcon_Crimson.png',
        'sprite': 'Assets/_Project/Art/UI/Buttons/UI_ButtonIcon_Crimson.png',
        'shadowUse': 'UI/Buttons/UI_Shadow_Circle.png', 'shadowSpread': 9.86, 'shadowDrop': 3.49},
        'CopyIcon': {'use': 'UI/Icons/Icon_Copy_Ivory.png', 'sprite': 'Assets/_Project/Art/UI/Icons/Icon_Copy_Ivory.png'}},
    # The mockup's chosen chip (4) shows its number in the Bid Chip's Selected colour; the chip
    # row (BiddingPanelCustomizer) takes the chosen and the other numbers' colours from these.
    'BiddingPanel': {'ChipLabel3': {'color': '#FFFCF7'}},
    # How to Play's two cards are the deck's own (HowToPlayCustomizer), not one picture.
    'HowToPlay': {'CardsArt': {'use': None, 'sprite': None, 'spriteRect': None, 'spritePpu': None, 'shadowUse': None,
                               'shadowSpread': None, 'shadowDrop': None, 'shadowOpacity': None}},
}

# Nodes V2 adds, each before (or after) a node: the glow behind each reward's coins on Earn Coins
# (22, "CoinGlow0..2"), and the shop panel's crimson edge (15, 20, 21: "Panel / Card, Selected").
ADD = {
    'EarnCoins': [('before', 'CoinStack%d' % i, {
        'id': 'CoinGlow%d' % i, 'parent': 'Block%d' % i, 'v2': box, 'kind': 'sprite',
        'use': 'UI/Decor/Deco_CoinGlow.png', 'sprite': 'Assets/_Project/Art/UI/Decor/Deco_CoinGlow.png', 'spritePpu': 0})
        for i, box in enumerate([[454, 399.5, 271, 271], [823.3, 383.5, 271, 271], [1187.6, 385.7, 271, 271]])],
    'Shop': [('after', 'Panel', {
        'id': 'PanelOutline', 'v2': [377.8, 228.2, 1122.8, 616.9], 'kind': 'sprite',
        'use': 'UI/Controls/UI_Outline_Selected.png', 'sprite': 'Assets/_Project/Art/UI/Controls/UI_Outline_Selected.png', 'spritePpu': 0})],
}

# The old mockups' lettering fixes, which V2's type replaces (ScreenExtras).
OLD_FITS = ('capFit', 'xFit', 'dilate', 'lineGradient', 'tracked', 'colorTop', 'colorBottom')


def texts(rows):
    """V2 text rows by path, in order (a path can repeat)."""
    out = {}
    for row in rows:
        if row[1] == 'T' and len(row) > 6:
            out.setdefault(row[0], []).append(row)
    return out


def find(label, by_path, overrides):
    lid, parent = label['id'], label.get('parent', '')
    want = overrides.get(lid)
    if want:
        path, _, n = want.partition('#')
        rows = by_path.get(path, [])
        return rows[int(n or 0)] if len(rows) > int(n or 0) else None
    if lid in by_path:
        return by_path[lid][0]
    if parent:
        inside = [r for p, rs in by_path.items() if p.startswith(parent + '/') for r in rs]
        if len(inside) == 1:
            return inside[0]
    return None


def to_mock(box):
    x, y, w, h = box
    return [round(x / SCALE_X, 2), round(y / SCALE_Y, 2), round((x + w) / SCALE_X, 2), round((y + h) / SCALE_Y, 2)]


def colour(c):
    """'#RRGGBB' or '#RRGGBB@0.8' -> '#RRGGBB' or '#RRGGBBCC'."""
    if not c or not c.startswith('#'):
        return None
    hexa, _, alpha = c.partition('@')
    return hexa if not alpha else hexa + '%02X' % round(float(alpha) * 255)


def spacing(ls):
    if not ls:
        return 0.0
    if ls.endswith('%'):
        return float(ls[:-1])
    return None     # pixels: none in the V2 menus


def plan(layout, rows, overrides, style_only=()):
    by_path = texts(rows)
    changes = []
    for node in layout['nodes']:
        if node.get('kind') != 'label':
            continue
        row = find(node, by_path, overrides)
        if row is None:
            changes.append((node['id'], None, None))
            continue
        x = row[6]
        role = ROLES.get(x['f'])
        new = {
            'font': role or node.get('font'),
            'size': x['s'],
            'color': colour(x['c']) or node.get('color'),
            'spacing': spacing(x.get('ls')),
            'inkBox': to_mock(x['ink']),
        }
        if x['t'] != node.get('text'):
            new['measure'] = x['t']
        if node['id'] in style_only:
            new = {'font': new['font'], 'color': new['color'], 'styleOnly': True}
        changes.append((node['id'], row[0], new))
    return changes


def find_icon(node, rows, overrides=None):
    """The V2 icon or art instance a sprite node shows: by id, or the icon (chevron) in its parent."""
    lid, parent = node['id'], node.get('parent')
    want = (overrides or {}).get(lid, lid)
    match = [r for r in rows if r[0] == want]
    if not match and parent:
        if lid.endswith('Chevron'):
            match = [r for r in rows if r[0] == parent + '/Chevron']
        elif 'Icon' in lid or lid.endswith('Coin'):
            match = [r for r in rows if r[0].startswith(parent + '/') and r[0].split('/')[1] in ('Icon', 'Art')]
    return match[0] if match else None


def file_ink(path):
    """The box round a sprite file's visible pixels, as fractions of its width and height."""
    alpha = np.asarray(Image.open(path).convert('RGBA'))[..., 3]
    ys, xs = np.where(alpha > 25)
    h, w = alpha.shape
    return xs.min() / w, ys.min() / h, (xs.max() + 1) / w, (ys.max() + 1) / h


def icon_rect(node, row):
    """The node's rect (mockup pixels) that puts its file's ink on the V2 ink box, keeping the file's proportions."""
    fx0, fy0, fx1, fy1 = file_ink(os.path.join(REPO, node['sprite']))
    ix, iy, iw, ih = row[6:10]
    old = node.get('spriteRect') or node['rect']
    ratio = (old[2] * SCALE_X) / (old[3] * SCALE_Y)          # the face's own proportions on screen
    # The face's width and height (canvas) from the ink's larger side, so the art is not distorted.
    if iw >= ih:
        w = iw / (fx1 - fx0)
        h = w / ratio
    else:
        h = ih / (fy1 - fy0)
        w = h * ratio
    x = ix + iw / 2 - (fx0 + fx1) / 2 * w
    y = iy + ih / 2 - (fy0 + fy1) / 2 * h
    return [round(float(v), 2) for v in (x / SCALE_X, y / SCALE_Y, w / SCALE_X, h / SCALE_Y)]


def plan_icons(layout, rows, keep, overrides=None):
    changes = []
    for node in layout['nodes']:
        if node.get('kind') == 'label' or not node.get('rect') or not node.get('sprite') or node['id'] in keep:
            continue
        row = find_icon(node, rows, overrides)
        if row is not None:
            changes.append((node['id'], row[0], icon_rect(node, row)))
    return changes


def ink_delta(old, new, align):
    """Canvas-unit move of the label's anchor (its alignment's edge, and its vertical centre)."""
    if not old:
        return None
    pick = {'left': 0, 'right': 2}.get(align)
    ox = old[pick] if pick is not None else (old[0] + old[2]) / 2
    nx = new[pick] if pick is not None else (new[0] + new[2]) / 2
    return round((nx - ox) * SCALE_X, 1), round(((new[1] + new[3]) - (old[1] + old[3])) / 2 * SCALE_Y, 1)


def value_text(value):
    """A value in the layouts' own style: arrays one item a line, at the nodes' indent."""
    if isinstance(value, list):
        return '[\n' + ',\n'.join('        ' + json.dumps(v) for v in value) + '\n      ]'
    return json.dumps(value, ensure_ascii=False)


def node_span(text, node_id):
    """Where a node's object lies in the file: from its '{' line to its closing '}'."""
    m = re.search(r'\n    \{\n      "id": %s,' % re.escape(json.dumps(node_id, ensure_ascii=False)), text)
    if not m:
        raise KeyError(node_id)
    start = m.start() + 1
    end = text.index('\n    }', start) + len('\n    }')
    return start, end


def set_keys(block, values):
    """Sets (or, with None, removes) keys of one node block, leaving every other line as it is."""
    lines = block.split('\n')            # '    {', key lines..., '    }'
    keys = []                            # (key, first line, last line)
    i = 1
    while i < len(lines) - 1:
        m = re.match(r'      "([^"]+)": (.*)$', lines[i])
        first = i
        if m and m.group(2) in ('[', '{'):          # an array or object, one item a line
            close = ']' if m.group(2) == '[' else '}'
            while not re.match(r'      \%s,?$' % close, lines[i]):
                i += 1
        keys.append((m.group(1), first, i))
        i += 1
    body = [lines[a:b + 1] for _, a, b in keys]
    names = [k for k, _, _ in keys]
    for key, value in values.items():
        if key in names:
            n = names.index(key)
            if value is None:
                del body[n], names[n]
            else:
                body[n] = ('      "%s": %s' % (key, value_text(value))).split('\n')
        elif value is not None:
            names.append(key)
            body.append(('      "%s": %s' % (key, value_text(value))).split('\n'))
    out = [lines[0]]
    for n, part in enumerate(body):
        part = list(part)
        part[-1] = part[-1].rstrip(',') + (',' if n < len(body) - 1 else '')
        out.extend(part)
    out.append(lines[-1])
    return '\n'.join(out)


def node_text(node):
    """A new node block in the layouts' own style."""
    lines = ['    {']
    keys = list(node.items())
    for n, (key, value) in enumerate(keys):
        lines.extend(('      "%s": %s%s' % (key, value_text(value), ',' if n < len(keys) - 1 else '')).split('\n'))
    lines.append('    }')
    return '\n'.join(lines)


def canvas_rect(box):
    """A V2 box (canvas units) as a layout rect (mockup pixels)."""
    x, y, w, h = box
    return [round(x / SCALE_X, 2), round(y / SCALE_Y, 2), round(w / SCALE_X, 2), round(h / SCALE_Y, 2)]


def add_nodes(text, additions):
    """Inserts V2's new nodes before or after the nodes they belong with; one already there is left as it is."""
    present = {n['id'] for n in json.loads(text)['nodes']}
    for where, anchor, node in additions:
        if node['id'] in present:
            continue
        node = dict(node)
        rect = canvas_rect(node.pop('v2'))
        node = {**{k: node[k] for k in ('id', 'parent') if k in node}, 'rect': rect,
                **{k: v for k, v in node.items() if k not in ('id', 'parent')}, 'spriteRect': rect}
        start, end = node_span(text, anchor)
        block = node_text(node)
        text = text[:start] + block + ',\n' + text[start:] if where == 'before' else text[:end] + ',\n' + block + text[end:]
    return text


def apply(name, changes, icons):
    path = os.path.join(REPO, 'ArtSource', 'Screens', name, 'layout.json')
    text = open(path, encoding='utf-8', newline='').read()
    eol = '\r\n' if '\r\n' in text else '\n'
    text = text.replace('\r\n', '\n')
    text = add_nodes(text, ADD.get(name, []))
    edits = {}
    for lid, _, new in changes:
        if not new:
            continue
        if new.get('styleOnly'):
            edits[lid] = {'font': new['font'], 'color': new['color']}
            continue
        edit = {key: new[key] for key in ('font', 'size', 'color', 'inkBox')}
        edit['spacing'] = new['spacing'] or None
        edit['measure'] = new.get('measure')
        edit.update({key: None for key in OLD_FITS})
        edits[lid] = edit
    layout = json.loads(text)
    nodes = {n['id']: n for n in layout['nodes']}
    for lid, _, rect in icons:
        edits[lid] = {'rect': rect, 'spriteRect': rect if 'spriteRect' in nodes[lid] else None}
    for lid in HIDDEN.get(name, ()):
        edits.setdefault(lid, {})['hidden'] = True
    for lid, box in RECTS.get(name, {}).items():
        rect = canvas_rect(box)
        edits.setdefault(lid, {}).update({'rect': rect, 'spriteRect': rect if 'spriteRect' in nodes[lid] else None})
    for lid, values in SET.get(name, {}).items():
        edits.setdefault(lid, {}).update(values)
    for lid, edit in edits.items():
        start, end = node_span(text, lid)
        text = text[:start] + set_keys(text[start:end], edit) + text[end:]
    json.loads(text)                     # still valid JSON
    with open(path, 'w', encoding='utf-8', newline='') as f:
        f.write(text.replace('\n', eol))


def main():
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('--apply', action='store_true', help='write the changes into the layouts')
    parser.add_argument('--screen', help='only this layout')
    args = parser.parse_args()
    v2 = json.load(open(os.path.join(HERE, 'screens-v2.json'), encoding='utf-8'))
    v2_icons = json.load(open(os.path.join(HERE, 'icons-v2.json'), encoding='utf-8'))
    for frame, name in FRAMES.items():
        if args.screen and args.screen != name:
            continue
        layout = json.load(open(os.path.join(REPO, 'ArtSource', 'Screens', name, 'layout.json'), encoding='utf-8'))
        nodes = {n['id']: n for n in layout['nodes']}
        changes = plan(layout, v2[frame], MATCH.get(name, {}), STYLE_ONLY.get(name, ()))
        print(f'## {name} ({frame})')
        for lid, path, new in changes:
            old = nodes[lid]
            if new is None:
                print(f'  {lid}: no V2 text')
                continue
            if new.get('styleOnly'):
                print(f"  {lid} <- {path}: font {new['font']} colour {new['color']} (style only)")
                continue
            moved = ink_delta(old.get('inkBox'), new['inkBox'], old.get('align', 'centre'))
            font = '' if new['font'] == old.get('font') else f" font {old.get('font')}->{new['font']}"
            col = '' if (new['color'] or '').upper() == (old.get('color') or '').upper() else f" colour {old.get('color')}->{new['color']}"
            meas = f" measure '{new['measure']}'" if 'measure' in new else ''
            print(f"  {lid} <- {path}: size {new['size']}{font}{col} move {moved}{meas}")
        icons = plan_icons(layout, v2_icons.get(frame, []), KEEP_ICONS.get(name, set()), MATCH_ICONS.get(name))
        for lid, path, rect in icons:
            old = nodes[lid].get('spriteRect') or nodes[lid]['rect']
            print(f"  {lid} <- {path}: rect {old} -> {rect}")
        if args.apply:
            apply(name, changes, icons)


if __name__ == '__main__':
    main()
