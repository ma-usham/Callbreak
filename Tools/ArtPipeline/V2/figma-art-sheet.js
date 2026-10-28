// Builds the V2 art's export sheets in the Figma file "Callbreak Game UI" (Vk5MVgtZaL2vXaAona2Tqk):
// section "Unity Export · Art" on page "V2 · Design System", one frame on pure black and its copy on
// pure white. On them: every V2 icon the game uses, the decor art, the six portraits, each portrait
// framed in the avatar's gold ring, and the avatar's rings. Run it with the Figma MCP's use_figma
// tool; it returns the JSON that art-sheet.json holds. Export both frames at 3x and run art.py.
const page = await figma.getNodeByIdAsync('42:21');
await figma.setCurrentPageAsync(page);
const old = page.children.find(c => c.name === 'Unity Export · Art');
if (old) old.remove();

const icons = {
  'arrow-left': '43:7', 'chevron-right': '43:11', 'close': '43:23', 'plus': '43:27', 'check': '43:31',
  'settings': '43:35', 'home': '43:39', 'shop': '43:44', 'scorecard': '43:49', 'sound': '43:55',
  'friends': '43:69', 'monitor': '43:85', 'info': '43:89', 'book': '43:94', 'lock': '43:99', 'clock': '43:104',
  'copy': '43:109', 'edit': '43:114', 'play': '43:122', 'leave': '43:127', 'enter': '43:132', 'emote': '43:136',
  'gift': '43:142', 'no-ads': '43:149', 'cards': '43:154', 'mountains': '43:158', 'table': '43:163',
  'lightning': '43:167', 'trophy': '43:174', 'crown': '43:179', 'star': '43:183', 'chart': '43:187',
  'target': '43:191', 'spade': '43:195', 'heart': '43:199', 'club': '43:203', 'diamond': '43:207',
};
const portraits = { 'Topi Youth': '53:61', 'Braid': '53:64', 'Elder': '53:63', 'Shawl': '53:66', 'Hoodie': '53:65', 'Red Kurta': '53:62' };

const items = [];
for (const [name, id] of Object.entries(icons)) items.push({ id: 'icon_' + name, src: id, scale: 2 });
items.push(
  { id: 'logo', src: '44:59', scale: 1 },
  { id: 'coin', src: '44:14', scale: 2 },
  { id: 'coin_pile', src: '44:19', scale: 1.5 },
  { id: 'trophy', src: '44:83', scale: 1 },
  { id: 'laurel', src: '44:93', scale: 1 },
  { id: 'ribbon', src: '44:103', scale: 1 },
  { id: 'seal', src: '44:111', scale: 1 },
  { id: 'mountain_line', src: '44:116', scale: 1 },
  { id: 'divider', src: '44:7', scale: 1 },
  { id: 'ring', src: '49:73', scale: 1, hideIn: { Photo: ['Placeholder'] } },
  { id: 'ring_chosen', src: '49:78', scale: 1, hide: ['Photo'] },
);
for (const [name, id] of Object.entries(portraits)) items.push({ id: 'portrait_' + name, src: id, scale: 1 });
for (const [name, id] of Object.entries(portraits)) items.push({ id: 'framed_' + name, src: '49:73', scale: 1, photo: id, hideIn: { Photo: ['Placeholder'] } });

const section = figma.createSection();
section.name = 'Unity Export · Art';
const sheet = figma.createFrame();
sheet.name = 'Export · Art · Black';
sheet.fills = [{ type: 'SOLID', color: { r: 0, g: 0, b: 0 } }];
sheet.clipsContent = true;
const sheetW = 1600, gap = 24;
let x = gap, y = gap, rowH = 0;
const boxes = [];
for (const it of items) {
  const comp = await figma.getNodeByIdAsync(it.src);
  const node = comp.createInstance();
  if (it.hide) for (const c of node.children) if (it.hide.includes(c.name)) c.visible = false;
  if (it.hideIn) for (const [parent, names] of Object.entries(it.hideIn)) {
    const p = node.children.find(c => c.name === parent);
    for (const c of p.children) if (names.includes(c.name)) c.visible = false;
  }
  if (it.photo) {
    // The screens show a portrait in an avatar by giving its Photo disc the portrait's image paint.
    const portrait = await figma.getNodeByIdAsync(it.photo);
    node.children.find(c => c.name === 'Photo').fills = portrait.fills;
  }
  // Drop shadows are the screens' own business (the game draws them as separate sprites).
  node.effects = node.effects.filter(e => e.type === 'INNER_SHADOW');
  if (it.scale !== 1) node.rescale(it.scale);
  const w = Math.ceil(node.width), h = Math.ceil(node.height);
  if (x + w + gap > sheetW) { x = gap; y += rowH + gap; rowH = 0; }
  sheet.appendChild(node);
  node.x = x; node.y = y;
  node.name = it.id;
  boxes.push([it.id, x, y, w, h]);
  x += w + gap; rowH = Math.max(rowH, h);
}
sheet.resizeWithoutConstraints(sheetW, y + rowH + gap);
section.appendChild(sheet);
sheet.x = 0; sheet.y = 0;
const white = sheet.clone();
white.name = 'Export · Art · White';
white.fills = [{ type: 'SOLID', color: { r: 1, g: 1, b: 1 } }];
section.appendChild(white);
white.x = sheetW + 100; white.y = 0;
section.resizeWithoutConstraints(2 * sheetW + 100, sheet.height);
section.x = 5400; section.y = 3400;
return JSON.stringify({ black: sheet.id, white: white.id, size: [sheet.width, sheet.height], boxes });
