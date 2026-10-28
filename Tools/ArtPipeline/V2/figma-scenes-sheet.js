// Builds the V2 scenes' and tables' export sheets in the Figma file "Callbreak Game UI"
// (Vk5MVgtZaL2vXaAona2Tqk): section "Unity Export · Scenes" on page "V2 · Design System".
//
// The V2 scenes and tables are drawn at 1920 x 1080. The game draws them in a 2560 x 1440 frame, so
// that 21:9 phones and 4:3 tablets are covered too (the owner, 28 Oct 2026: widen them, continuing
// only the sky, the ground and the felt). Each picture is a detached copy of its component, at the
// middle of a 2560 x 1440 frame, widened by rule:
// - a rectangle or ellipse across the whole picture (sky, mist, lake, ground, felt, vignette) is
//   stretched to the frame, its gradient held where it was, so its far ends carry its end colours;
// - anything reaching the left or right edge (ridges, hills, pines, a shore, a garland) goes on in a
//   mirrored copy, clipped to the new margin, so it meets itself without a seam;
// - an opaque shape that reaches the picture's foot, across it or from a side, is carried down by a
//   band of its own bottom colour below it, as wide as the shape is at its bottom;
// - a see-through texture over the whole picture (the walnut's grain) is scaled to cover the frame.
// The components themselves are left as they are. Rows: the scenes' backs, the tables, then the
// scenes' fronts (and vignettes) on black and on white. Export each picture at 1x (Figma caps an
// export at 4096 px a side); scenes.py cuts them.
const page = await figma.getNodeByIdAsync('42:21');
await figma.setCurrentPageAsync(page);
const old = page.children.find(c => c.name === 'Unity Export · Scenes');
if (old) old.remove();

const W = 1920, H = 1080, FW = 2560, FH = 1440, DX = (FW - W) / 2, DY = (FH - H) / 2;
const scenes = (await figma.getNodeByIdAsync('52:206')).children;
const tables = (await figma.getNodeByIdAsync('52:239')).children;

function mul(t, a) {
  return [
    [t[0][0] * a[0][0], t[0][1] * a[1][1], t[0][0] * a[0][2] + t[0][1] * a[1][2] + t[0][2]],
    [t[1][0] * a[0][0], t[1][1] * a[1][1], t[1][0] * a[0][2] + t[1][1] * a[1][2] + t[1][2]],
  ];
}

// Stretches a shape to new bounds (its parent's space) and keeps its gradients where they were.
function stretch(node, x, y, w, h) {
  const a = [[w / node.width, 0, (x - node.x) / node.width], [0, h / node.height, (y - node.y) / node.height]];
  const fills = node.fills.map(p => p.type.startsWith('GRADIENT') ? Object.assign({}, p, { gradientTransform: mul(p.gradientTransform, a) }) : p);
  node.resizeWithoutConstraints(w, h);
  node.x = x; node.y = y;
  node.fills = fills;
}

function leafBounds(node, root) {
  const r = root.absoluteTransform, x0 = r[0][2], y0 = r[1][2];
  let minX = 1e9, minY = 1e9, maxX = -1e9, maxY = -1e9;
  const walk = (n) => {
    if (n.visible === false) return;
    if ('children' in n && n.children.length) { n.children.forEach(walk); return; }
    const b = n.absoluteBoundingBox;
    if (!b) return;
    minX = Math.min(minX, b.x - x0); minY = Math.min(minY, b.y - y0);
    maxX = Math.max(maxX, b.x + b.width - x0); maxY = Math.max(maxY, b.y + b.height - y0);
  };
  walk(node);
  return { minX, minY, maxX, maxY };
}

function opaque(node) {
  if (node.opacity < 1 || !Array.isArray(node.fills) || node.fills.length === 0) return false;
  return node.fills.every(p => p.visible === false || (p.opacity ?? 1) >= 1 && (p.type === 'SOLID' || (p.gradientStops || []).every(s => s.color.a >= 1)));
}

// Where a vector's outline crosses the line y = H, left and right, in picture space: its straight
// segments, cut there (the outlines near the foot are straight).
function footSpan(node, root, H) {
  const t = node.absoluteTransform, r = root.absoluteTransform;
  const at = v => ({ x: t[0][0] * v.x + t[0][1] * v.y + t[0][2] - r[0][2], y: t[1][0] * v.x + t[1][1] * v.y + t[1][2] - r[1][2] });
  const { vertices, segments } = node.vectorNetwork;
  const xs = [];
  for (const s of segments) {
    const a = at(vertices[s.start]), b = at(vertices[s.end]);
    if ((a.y - H) * (b.y - H) > 0 || a.y === b.y) continue;
    xs.push(a.x + (H - a.y) * (b.x - a.x) / (b.y - a.y));
  }
  for (const v of vertices) { const p = at(v); if (Math.abs(p.y - H) < 0.5) xs.push(p.x); }
  return xs.length ? { minX: Math.min(...xs), maxX: Math.max(...xs) } : null;
}

function bottomColour(node) {
  const p = node.fills.find(f => f.visible !== false);
  if (p.type === 'SOLID') return p.color;
  const s = p.gradientStops[p.gradientStops.length - 1].color;
  return { r: s.r, g: s.g, b: s.b };
}

// A mirrored copy of a layer, clipped to the margin on one side (parent space, whose origin is the picture's).
function mirror(node, side) {
  const parent = node.parent;
  const clip = figma.createFrame();
  clip.name = node.name + ' (continued ' + side + ')';
  clip.fills = [];
  clip.clipsContent = true;
  parent.insertChild(parent.children.indexOf(node) + 1, clip);
  clip.resizeWithoutConstraints(DX, FH);
  clip.x = side === 'left' ? -DX : W; clip.y = -DY;
  const copy = node.clone();
  clip.appendChild(copy);
  const m = side === 'left' ? 0 : W;
  const t = node.relativeTransform;
  copy.relativeTransform = [[-t[0][0], -t[0][1], 2 * m - t[0][2] - clip.x], [t[1][0], t[1][1], t[1][2] - clip.y]];
}

function widen(container, root) {
  for (const node of [...container.children]) {
    if (node.visible === false) continue;
    const full = node.width >= W - 20 && node.height >= 40;
    if ((node.type === 'RECTANGLE' || node.type === 'ELLIPSE') && full) {
      const top = node.y <= 0 ? -DY : node.y;
      const bottom = node.y + node.height >= H ? H + DY : node.y + node.height;
      stretch(node, -DX, top, FW, bottom - top);
      continue;
    }
    if (node.type === 'VECTOR' && node.opacity < 1 && node.width >= W - 20 && node.height >= H - 100) {
      node.resize(node.width * FW / W, node.height * FH / H);
      node.x = node.x * FW / W - DX; node.y = node.y * FH / H - DY;
      continue;
    }
    const b = leafBounds(node, root);
    // A shape may run a little past the foot (the frame clipped it): it is carried down from its own bottom.
    const foot = node.type === 'VECTOR' && b.maxY >= H && b.maxY < H + DY && opaque(node) && (full || b.minX <= 2 || b.maxX >= W - 2)
      ? footSpan(node, root, b.maxY - 0.5) : null;
    if (foot) {
      // Carried down, as wide as the shape is at its bottom, out to the margin on the side(s) it reaches.
      const left = b.minX <= 2 ? -DX : foot.minX, right = b.maxX >= W - 2 ? W + DX : foot.maxX;
      const top = Math.floor(b.maxY) - 1;
      const band = figma.createRectangle();
      band.name = node.name + ' (carried down)';
      band.fills = [{ type: 'SOLID', color: bottomColour(node) }];
      container.insertChild(container.children.indexOf(node), band);
      band.resizeWithoutConstraints(right - left, H + DY - top);
      band.x = left; band.y = top;
    }
    if (b.minX <= 2) mirror(node, 'left');
    if (b.maxX >= W - 2) mirror(node, 'right');
  }
}

async function picture(variant, keep) {
  const frame = figma.createFrame();
  frame.resizeWithoutConstraints(FW, FH);
  frame.clipsContent = true;
  frame.fills = [];
  const inst = variant.createInstance();
  frame.appendChild(inst);
  inst.x = DX; inst.y = DY;
  const root = inst.detachInstance();
  root.clipsContent = false;
  for (const c of [...root.children]) if (!keep.includes(c.name)) c.remove();
  for (const c of root.children) if (c.type === 'FRAME') c.clipsContent = false;
  if (keep.includes('Felt')) widen(root, root);
  else for (const c of [...root.children]) (c.type === 'FRAME' ? widen(c, root) : widen({ children: [c] }, root));
  return frame;
}

const section = figma.createSection();
section.name = 'Unity Export · Scenes';
const rows = [];
function row(name, frames, fill) {
  const r = figma.createFrame();
  r.name = name;
  r.fills = fill ? [{ type: 'SOLID', color: fill }] : [];
  r.clipsContent = true;
  r.resizeWithoutConstraints(FW * frames.length, FH);
  frames.forEach((f, i) => { r.appendChild(f); f.x = i * FW; f.y = 0; });
  section.appendChild(r);
  r.x = 0; r.y = rows.length * (FH + 200);
  rows.push(r);
  return r;
}
const names = scenes.map(s => s.name.replace('Scene=', ''));
const backs = []; for (const s of scenes) backs.push(await picture(s, ['Back']));
row('Export · Scenes · Back', backs, { r: 0, g: 0, b: 0 });
const tabs = []; for (const t of tables) tabs.push(await picture(t, ['Felt', 'Grain', 'Trick Ring', 'Emblem', 'Vignette']));
row('Export · Tables', tabs, { r: 0, g: 0, b: 0 });
const fronts = []; for (const s of scenes) fronts.push(await picture(s, ['Front', 'Vignette']));
row('Export · Scenes · Front · Black', fronts, { r: 0, g: 0, b: 0 });
const white = rows[2].clone();
white.name = 'Export · Scenes · Front · White';
white.fills = [{ type: 'SOLID', color: { r: 1, g: 1, b: 1 } }];
section.appendChild(white);
white.x = 0; white.y = 3 * (FH + 200);
rows.push(white);
section.resizeWithoutConstraints(FW * 3, 4 * (FH + 200));
section.x = 5400; section.y = 5300;
return JSON.stringify({
  section: section.id,
  rows: rows.map(r => ({ name: r.name, id: r.id, pictures: r.children.map(p => p.id) })),
  scenes: names,
  tables: tables.map(t => t.name.replace('Style=', '')),
});
