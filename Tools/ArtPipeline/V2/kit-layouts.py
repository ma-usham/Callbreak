"""Points the screen layouts (ArtSource/Screens/*/layout.json) at the V2 kit.

kit.py replaces the kit's sprites in place, so most layout nodes need no change. What changes:
- The V2 shadows carry their own colour and strength and are rendered for a V2 face, so every
  node over a V2 shadow takes that shadow's drop, spread (for shadows sliced on all sides) and full
  opacity from ArtSource/Kit/V2.json.
- Cards and card-like panels move from UI/Common/UI_Panel_Card_Shadow.png (which the playing cards
  keep) to the V2 card shadow; the inset table body casts none in V2, nor does the room-code pill.
- V2 draws three secondary buttons as its box button (Settings' How to Play and About, Match
  Results' Main Menu): they take the box sprite and its shadow.
- 9-slice scales tuned for the old ornate corners go: the V2 corners show at their own size.

Only the lines that change are rewritten, so the rest of each file stays byte for byte.

    python Tools/ArtPipeline/V2/kit-layouts.py
"""
import glob
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
ART_PREFIX = 'Assets/_Project/Art/'
SX, SY = 1920 / 1672, 1080 / 941

CARD_SHADOW_OLD = 'UI/Common/UI_Panel_Card_Shadow.png'
CARD_SHADOW = 'UI/Panels/UI_Panel_Card_Shadow.png'
BOX = 'UI/Table/UI_ButtonSecondary_Box_Normal.png'
BOX_SHADOW = 'UI/Table/UI_ButtonSecondary_Box_Shadow.png'
NO_SHADOW_FACES = {'UI/Table/UI_TableBody.png', 'UI/Controls/UI_Pill_RoomCode.png'}
CARD_FACES = {
    'UI/Panels/UI_Panel_Card.png', 'UI/Panels/UI_Panel_Card_Light.png',
    'UI/Panels/UI_DayTile_Normal.png', 'UI/Panels/UI_DayTile_Bonus.png',
    'UI/Panels/UI_RewardBlock_Normal.png', 'UI/Panels/UI_RewardBlock_Selected.png',
}
BOX_NODES = {('Settings', 'HowToPlayButton'), ('Settings', 'AboutButton'), ('MatchResults', 'MainMenuButton')}
SHADOW_KEYS = ('shadowUse', 'shadowSpread', 'shadowDrop', 'shadowOpacity')

KEY = re.compile(r'^(\s*)"(\w+)": (.*?)(,?)\r?$')


def number(v):
    return str(int(v)) if float(v).is_integer() else f'{v:.2f}'.rstrip('0')


class Node:
    """One node object of a layout file, edited line by line."""

    def __init__(self, lines, start, end):
        self.lines, self.start, self.end = lines, start, end

    def find(self, key):
        for i in range(self.start + 1, self.end):
            m = KEY.match(self.lines[i])
            if m and m.group(2) == key and len(m.group(1)) == 6:
                return i, m
        return None, None

    def get(self, key):
        _, m = self.find(key)
        return json.loads(m.group(3)) if m else None

    def set(self, key, value, after):
        text = json.dumps(value, ensure_ascii=False) if isinstance(value, str) else number(value)
        i, m = self.find(key)
        if m:
            self.lines[i] = f'{m.group(1)}"{key}": {text}{m.group(4)}'
            return
        a, am = self.find(after)
        comma = am.group(4)
        if not comma:
            self.lines[a] = self.lines[a] + ','
        self.lines.insert(a + 1, f'      "{key}": {text}{comma}')
        self.end += 1

    def delete(self, key):
        i, m = self.find(key)
        if not m:
            return
        del self.lines[i]
        self.end -= 1
        if not m.group(4):
            # it was the last key: the one before it loses its comma
            j = i - 1
            self.lines[j] = self.lines[j][:-1] if self.lines[j].endswith(',') else self.lines[j]


def nodes_of(lines):
    start = None
    for i, line in enumerate(lines):
        if line == '    {':
            start = i
        elif start is not None and line.startswith('    }'):
            yield start, i
            start = None


def main():
    manifest = json.load(open(os.path.join(REPO, 'ArtSource', 'Kit', 'V2.json'), encoding='utf-8'))['sprites']
    faces = {s['path'] for s in manifest if 'shadow' not in s}
    shadows = {s['path']: s['shadow'] for s in manifest if 'shadow' in s}
    for path in sorted(glob.glob(os.path.join(REPO, 'ArtSource', 'Screens', '*', 'layout.json'))):
        screen = os.path.basename(os.path.dirname(path))
        raw = open(path, encoding='utf-8', newline='').read()
        nl = '\r\n' if '\r\n' in raw else '\n'
        lines = raw.split(nl)
        parsed = {n['id']: n for n in json.loads(raw)['nodes']}
        changed = 0
        # walk from the end, so inserted and deleted lines don't move the nodes still to edit
        for start, end in reversed(list(nodes_of(lines))):
            node = Node(lines, start, end)
            before = lines[start:end + 1]
            node_id = node.get('id')
            if (screen, node_id) in BOX_NODES:
                node.set('use', BOX, 'id')
                node.set('sprite', ART_PREFIX + BOX, 'use')
                node.set('shadowUse', BOX_SHADOW, 'use')
            face = node.get('use') or (node.get('sprite') or '').replace(ART_PREFIX, '')
            if face in faces:
                node.delete('sliceScale')
            shadow = node.get('shadowUse')
            if shadow and (face in NO_SHADOW_FACES):
                for key in SHADOW_KEYS:
                    node.delete(key)
                shadow = None
            elif shadow == CARD_SHADOW_OLD and face in CARD_FACES:
                node.set('shadowUse', CARD_SHADOW, 'id')
                shadow = CARD_SHADOW
            elif shadow == CARD_SHADOW_OLD and face == BOX:
                node.set('shadowUse', BOX_SHADOW, 'id')
                shadow = BOX_SHADOW
            if shadow in shadows:
                info = shadows[shadow]
                if info['slice'] in ('both', 'vertical'):
                    # sliced: the shadow reaches its own spread beyond any face
                    node.set('shadowSpread', round(info['spread'] / SX, 2), 'shadowUse')
                elif info['slice'] == 'none':
                    # scaled: the spread grows with the face, which may not be the one the shadow
                    # was rendered for (the round shadow serves discs, rings, knobs and chips)
                    rect = parsed[node_id].get('spriteRect') or parsed[node_id]['rect']
                    node.set('shadowSpread', round(rect[2] * info['spread'] / info['face'][0], 2), 'shadowUse')
                else:
                    # sliced across and scaled to the height: the builder spreads it like its face
                    node.delete('shadowSpread')
                if info['drop']:
                    node.set('shadowDrop', round(info['drop'] / SY, 2), 'shadowUse')
                else:
                    node.delete('shadowDrop')
                node.delete('shadowOpacity')
            if lines[start:node.end + 1] != before:
                changed += 1
        if changed:
            open(path, 'w', encoding='utf-8', newline='').write(nl.join(lines))
            json.loads(nl.join(lines))
            print(f'{screen}: {changed} nodes')


if __name__ == '__main__':
    main()
