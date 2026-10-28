// Builds the V2 kit's export sheets in the Figma file "Callbreak Game UI" (Vk5MVgtZaL2vXaAona2Tqk):
// section "Unity Export" on page "V2 · Design System", one frame on pure black and its copy on pure
// white, every kit piece at its export size with no gaps between rows that matter (24 units apart).
// Run it with the Figma MCP's use_figma tool; it returns the JSON that kit-sheet.json holds. Then
// export both frames at 2x (download_assets, png, scale 2) and run kit.py on the two PNGs.
//
// Each piece is a fresh instance of a V2 component variant (or a copy of a component's layer):
// labels, icons and carets are hidden, and drop shadows are removed (kit.py renders them as the
// separate shadow sprites the game draws under each face); inner shadows stay.
const page = await figma.getNodeByIdAsync('42:21');
await figma.setCurrentPageAsync(page);
const old = page.children.find(c => c.name === 'Unity Export');
if (old) old.remove();
const items = [
  {id: 'panel_large', src: '47:5', w: 240, h: 760},
  {id: 'panel_plain', src: '47:20', w: 200, h: 440},
  {id: 'card', src: '47:31', w: 128, h: 256},
  {id: 'card_light', src: '47:33', w: 128, h: 128},
  {id: 'card_selected', src: '47:32', w: 128, h: 256},
  {id: 'outline', src: '47:32', w: 128, h: 128, fillsNone: true, fx: 'none'},
  {id: 'drawer', src: '47:36', w: 267, h: 1080},
  {id: 'popover', src: '47:44', w: 200, h: 200, hide: ['Pointer']},
  {id: 'pointer', cloneOf: '47:45', w: 48, h: 23, pad: 2},
  {id: 'inset', src: '47:35', w: 96, h: 96},
  {id: 'icon_l', src: '47:165', w: 96, h: 96, hide: ['Icon', 'Badge']},
  {id: 'primary', src: '47:50', w: 360, h: 112, hide: ['Icon', 'Label']},
  {id: 'secondary', src: '47:136', w: 220, h: 88, hide: ['Icon', 'Label', 'Chevron']},
  {id: 'secondary_box', src: '47:150', w: 220, h: 88, hide: ['Icon', 'Label', 'Chevron']},
  {id: 'disc', src: '47:186', w: 160, h: 160, hide: ['Icon']},
  {id: 'disc_selected', src: '47:191', w: 160, h: 160, hide: ['Icon']},
  {id: 'info', src: '47:170', w: 64, h: 64, hide: ['Badge'], swap: {'Icon#47:81': '43:89'}},
  {id: 'plus', cloneOf: '49:196', w: 52, h: 52},
  {id: 'hero', src: '47:87', w: 600, h: 104, hide: ['Icon', 'Label']},
  {id: 'toggle_on', src: '48:57', w: 96, h: 54},
  {id: 'toggle_off', src: '48:59', w: 96, h: 54},
  {id: 'radio_on', src: '48:66', w: 48, h: 48},
  {id: 'radio_off', src: '48:68', w: 48, h: 48},
  {id: 'check', src: '48:70', w: 48, h: 48, pad: 3},
  {id: 'field', src: '48:73', w: 128, h: 80, hide: ['Value', 'Icon', 'Action']},
  {id: 'dropdown', src: '48:88', w: 240, h: 64, hide: ['Value'], alignEnd: true},
  {id: 'code_empty', src: '48:124', w: 120, h: 124},
  {id: 'code_focus', src: '48:128', w: 120, h: 124, hide: ['Caret']},
  {id: 'code_error', src: '81:585', w: 120, h: 124, hide: ['Char']},
  {id: 'stake', src: '48:102', w: 200, h: 84, hide: ['Art/Coin', 'Amount']},
  {id: 'stake_selected', src: '48:109', w: 200, h: 84, hide: ['Art/Coin', 'Amount']},
  {id: 'ready', src: '49:133', w: 36, h: 36},
  {id: 'dot', src: '49:160', w: 22, h: 22},
  {id: 'number_disc', src: '49:158', w: 72, h: 72, hide: ['Number']},
  {id: 'spinner', cloneOf: '49:140', child: 'Arc', w: 40, h: 40, fullArc: true},
  {id: 'segment_track', src: '48:97', w: 200, h: 68, hide: ['Segment']},
  {id: 'segment_selected', src: '48:92', w: 161, h: 60, hide: ['Label']},
  {id: 'slider_track', cloneOf: '48:62', child: 'Track', w: 64, h: 14},
  {id: 'slider_fill', cloneOf: '48:62', child: 'Fill', w: 64, h: 14},
  {id: 'slider_knob', cloneOf: '48:62', child: 'Knob', w: 44, h: 44, fx: 'none'},
  {id: 'room_code', src: '48:132', w: 200, h: 92, hide: ['Code', 'Button/Icon']},
  {id: 'name_plate', src: '49:114', w: 140, h: 40, hide: ['Icon', 'Name']},
  {id: 'status_pill', src: '49:119', w: 195, h: 44, hide: ['Label']},
  {id: 'tag_pill', src: '49:126', w: 212, h: 42, hide: ['Label']},
  {id: 'timer_pill', src: '49:128', w: 169, h: 60, hide: ['Icon', 'Time']},
  {id: 'turn_ring', cloneOf: '49:143', child: 'Progress', w: 112, h: 112, fullArc: true},
  {id: 'bid_chip', src: '48:117', w: 84, h: 84, hide: ['Number']},
  {id: 'bid_chip_selected', src: '48:119', w: 84, h: 84, hide: ['Number']},
];
const section = figma.createSection();
section.name = 'Unity Export';
const sheet = figma.createFrame();
sheet.name = 'Export · Kit · Black';
sheet.fills = [{type: 'SOLID', color: {r: 0, g: 0, b: 0}}];
sheet.clipsContent = true;
const sheetW = 1400, gap = 24;
let x = gap, y = gap, rowH = 0;
const boxes = [];
for (const it of items) {
  let node;
  if (it.src) {
    const comp = await figma.getNodeByIdAsync(it.src);
    node = comp.createInstance();
    if (it.swap) node.setProperties(it.swap);
    if (it.hide) for (const c of node.children) if (it.hide.includes(c.name)) c.visible = false;
    // An auto-layout row closes up round a hidden layer: keep what stays at the row's end.
    if (it.alignEnd) node.primaryAxisAlignItems = 'MAX';
    node.resize(it.w, it.h);
  } else {
    const base = await figma.getNodeByIdAsync(it.cloneOf);
    const srcNode = it.child ? base.children.find(c => c.name === it.child) : base;
    node = srcNode.clone();
    // A copied layer keeps its stretch constraints, which the sheet's own resize would apply.
    node.constraints = {horizontal: 'MIN', vertical: 'MIN'};
    // The game fills its rings radially (Image Filled), so the arc becomes a whole ring.
    if (it.fullArc) node.arcData = {startingAngle: 0, endingAngle: 2 * Math.PI, innerRadius: srcNode.arcData.innerRadius};
    if (node.width !== it.w || node.height !== it.h) node.resizeWithoutConstraints(it.w, it.h);
  }
  if (it.fillsNone) node.fills = [];
  const fx = it.fx || 'inner';
  if ('effects' in node) node.effects = fx === 'none' ? [] : node.effects.filter(e => e.type === 'INNER_SHADOW');
  const pad = it.pad || 0;
  const cw = it.w + 2 * pad, ch = it.h + 2 * pad;
  if (x + cw + gap > sheetW) { x = gap; y += rowH + gap; rowH = 0; }
  sheet.appendChild(node);
  node.x = x + pad; node.y = y + pad;
  node.name = it.id;
  boxes.push([it.id, x, y, cw, ch]);
  x += cw + gap; rowH = Math.max(rowH, ch);
}
sheet.resizeWithoutConstraints(sheetW, y + rowH + gap);
section.appendChild(sheet);
sheet.x = 0; sheet.y = 0;
const white = sheet.clone();
white.name = 'Export · Kit · White';
white.fills = [{type: 'SOLID', color: {r: 1, g: 1, b: 1}}];
section.appendChild(white);
white.x = sheetW + 100; white.y = 0;
section.resizeWithoutConstraints(2 * sheetW + 100, sheet.height);
section.x = 5400; section.y = 656;
return JSON.stringify({black: sheet.id, white: white.id, size: [sheet.width, sheet.height], boxes});
