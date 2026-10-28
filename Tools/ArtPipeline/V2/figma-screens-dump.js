// Reads the V2 screen frames of the Figma file "Callbreak Game UI" (Vk5MVgtZaL2vXaAona2Tqk), page
// "V2 · Screens", for screens.py. Read-only. Run it with the Figma MCP's use_figma, with `ids` set
// to the frames to read (a handful at a time: the result is returned as text), and merge the
// results into screens-v2.json.
//
// For each frame, one row per layer: [path, type initial, x, y, w, h, extra], boxes in the frame's
// canvas units (1920 x 1080). Text layers carry their text (t), font (f, "Serif" for Source Serif 4),
// size (s), colour (c, "#RRGGBB" or "#RRGGBB@opacity"), alignment (a: L, C, R), letter spacing (ls)
// and the box round their rendered glyphs (ink). Instances carry their component and properties;
// shapes their fill. The insides of instances are read for their text and nested instances only;
// lattice ornaments, card faces, scene backgrounds and the table states' copy of 08 Gameplay are
// skipped.
const ids = ['63:9273'];
const hex = c => '#' + [c.r, c.g, c.b].map(v => Math.round(v * 255).toString(16).padStart(2, '0')).join('').toUpperCase();
const r = v => Math.round(v * 10) / 10;
const out = {};
for (const id of ids) {
  const f = await figma.getNodeByIdAsync(id);
  const fx = f.absoluteTransform[0][2], fy = f.absoluteTransform[1][2];
  const box = b => b ? [r(b.x - fx), r(b.y - fy), r(b.width), r(b.height)] : null;
  const rows = [];
  const walk = (n, path, depth, inInst) => {
    const p = path ? path + '/' + n.name : n.name;
    const comp = n.type === 'INSTANCE' && n.mainComponent ? (n.mainComponent.parent && n.mainComponent.parent.type === 'COMPONENT_SET' ? n.mainComponent.parent.name + ' / ' + n.mainComponent.name : n.mainComponent.name) : null;
    if (comp && (/^(Motif\/|Card\/Face|Card\/Pip|Background|Card\/Court)/.test(comp) || /08 Gameplay$/.test(comp))) return;
    if (n.name === 'Ornament' || n.name === 'Index') return;
    const row = [p, n.type[0], ...box(n.absoluteBoundingBox)];
    const x = {};
    if (n.visible === false) x.hid = 1;
    if (n.type === 'TEXT') {
      x.t = n.characters.length > 60 ? n.characters.slice(0, 60) + '…' : n.characters;
      const fn = n.fontName;
      x.f = fn === figma.mixed ? 'mixed' : fn.family.replace('Source Serif 4', 'Serif') + ' ' + fn.style;
      x.s = n.fontSize === figma.mixed ? 'mixed' : r(n.fontSize);
      const fill = Array.isArray(n.fills) ? n.fills.find(q => q.visible !== false) : null;
      x.c = fill ? (fill.type === 'SOLID' ? hex(fill.color) + ((fill.opacity ?? 1) < 1 ? '@' + r(fill.opacity) : '') : fill.type) : null;
      x.a = n.textAlignHorizontal[0];
      const ls = n.letterSpacing;
      if (ls !== figma.mixed && ls.value) x.ls = ls.unit === 'PERCENT' ? r(ls.value) + '%' : r(ls.value) + 'px';
      x.ink = box(n.absoluteRenderBounds);
      if (n.opacity < 1) x.op = r(n.opacity);
    } else if (comp) {
      x.comp = comp.replace(/(Style|State|Size|Type|Shape)=/g, '');
      const props = {};
      for (const [k, v] of Object.entries(n.componentProperties || {})) {
        const kk = k.split('#')[0];
        if (!['Style', 'State', 'Size', 'Type', 'Shape', 'Badge', 'Show Badge', 'Ornament', 'Illustrated', 'Suit', 'Rank'].includes(kk)) props[kk] = v.value;
      }
      if (Object.keys(props).length) x.pr = props;
      if (n.opacity < 1) x.op = r(n.opacity);
      if (Math.abs(n.rotation) > 0.01) x.rot = r(n.rotation);
    } else if (n.type === 'RECTANGLE' || n.type === 'VECTOR' || n.type === 'ELLIPSE') {
      const fill = Array.isArray(n.fills) ? n.fills.find(q => q.visible !== false) : null;
      if (fill && fill.type === 'SOLID') x.fill = hex(fill.color) + ((fill.opacity ?? 1) < 1 ? '@' + r(fill.opacity) : '');
      if (n.opacity < 1) x.op = r(n.opacity);
    }
    if (Object.keys(x).length) row.push(x);
    if (!inInst || n.type === 'TEXT' || comp) rows.push(row);
    if ('children' in n && depth < 4 && !(comp && comp.startsWith('Card /'))) for (const c of n.children) walk(c, p, depth + 1, inInst || n.type === 'INSTANCE');
  };
  for (const c of f.children) walk(c, '', 0, false);
  out[f.name] = rows;
}
return JSON.stringify(out);
