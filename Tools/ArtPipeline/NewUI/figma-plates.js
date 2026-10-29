// Builds Figma's "Unity Export · Plates" sheet on "V1 Rebuild · Design System" (file Vk5MVgtZaL2vXaAona2Tqk),
// for the Figma MCP's use_figma: paste plates.py's printed cells into CELLS and run it. Each cell holds an
// instance of the plate drawn at the game sprite's size and scale ([node, x, y, width, height, scale, fit]):
// resized to the sprite's size in Figma units (its corners stay pinned) and then scaled by the sprite's
// pixels per Figma unit, or, with fit "uniform", scaled whole into it. Drop shadows are taken off: the
// game draws them as separate sprites. The sheet is then shot on black and on white (cut_plates.py).
const CELLS = [/* plates.py output */];
const page = await figma.getNodeByIdAsync('164:2');
await figma.setCurrentPageAsync(page);
const old = page.findOne(n => n.name === 'Unity Export · Plates');
if (old) old.remove();
const width = Math.max(...CELLS.map(c => c[1] + c[3]));
const height = Math.max(...CELLS.map(c => c[2] + c[4]));
const sheet = figma.createFrame();
sheet.name = 'Unity Export · Plates';
sheet.resize(width, height);
sheet.x = 1800; sheet.y = 7400;
sheet.fills = [{ type: 'SOLID', color: { r: 0, g: 0, b: 0 } }];
sheet.clipsContent = true;
page.appendChild(sheet);
for (const [id, x, y, w, h, s, fit] of CELLS) {
  const cell = figma.createFrame();
  cell.name = `${id} ${w}x${h}`; cell.resize(w, h); cell.x = x; cell.y = y; cell.fills = []; cell.clipsContent = true;
  sheet.appendChild(cell);
  const inst = (await figma.getNodeByIdAsync(id)).createInstance();
  cell.appendChild(inst);
  if (fit === 'uniform') {
    inst.rescale(Math.min(w / inst.width, h / inst.height));
  } else {
    inst.resize(w / s, h / s);
    if (Math.abs(s - 1) > 1e-6) inst.rescale(s);
  }
  inst.x = (w - inst.width) / 2; inst.y = (h - inst.height) / 2;
  const strip = n => { if ('effects' in n && n.effects && n.effects.some(e => e.type === 'DROP_SHADOW')) n.effects = n.effects.filter(e => e.type !== 'DROP_SHADOW'); };
  strip(inst);
  inst.findAll(() => true).forEach(strip);
}
return sheet.id;
