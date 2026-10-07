// Writes ArtSource/Screens/NamePrompt/layout.json, the name screen at the start (ADR 043), from the
// owner's mockup Assets/ArtRefrences/game-screens/22-name-entry.png. That mockup is screen 14
// (Profile) with a character grid where the stats were: its title, panel, name field, Save button
// and note are screen 14's pixel for pixel, so their measured nodes are taken from Profile's layout.
// The rest was measured on 22-name-entry.png: the big avatar (241 px with its crimson ring, centre
// 588,373), the header "Choose your character" (ink 889..1148 x 229..256), its ornaments (diamonds
// at 847.5 and 1191.5, y 241.5) and the grid (centres 165 apart).
// The owner asked for one change to the mockup: the name field at the top of the panel, so the
// on-screen keyboard never hides it. The caption, field and the line under it move up, the divider
// follows, and the avatar section moves down under it, its grid a little smaller (128 instead of
// 150) so that Save stays where the mockup has it.
//   node Tools/ArtPipeline/name-entry-layout.js
const fs = require('fs');
const path = require('path');
const repo = path.resolve(__dirname, '..', '..');
const profile = JSON.parse(fs.readFileSync(path.join(repo, 'ArtSource/Screens/Profile/layout.json'), 'utf8'));
const node = id => JSON.parse(JSON.stringify(profile.nodes.find(n => n.id === id)));

// Moves a node down by dy mockup pixels (every vertical measure it carries).
function shift(n, dy) {
  for (const key of ['rect', 'spriteRect']) if (n[key]) n[key][1] += dy;
  if (n.inkBox) { n.inkBox[1] += dy; n.inkBox[3] += dy; }
  for (const key of ['capTop', 'baseline']) if (typeof n[key] === 'number') n[key] += dy;
  return n;
}

const nodes = [];
for (const id of ['Title', 'TitleLineLeft', 'TitleDiamond', 'TitleLineRight', 'Subtitle']) nodes.push(node(id));

// The panel ends a little higher: the note no longer sits under Save.
const panel = node('Panel');
panel.rect[3] = panel.spriteRect[3] = 657;
nodes.push(panel);

// The name block at the top of the panel: caption, field, and the line under it (the note or why a
// name cannot be used), then the divider.
nodes.push(shift(node('DisplayNameCaption'), 220 - 615));
const field = shift(node('NameField'), 220 - 615);
nodes.push(field);
// FieldText keeps the mockup's 'Player' for fitting its size; the customizer empties it.
nodes.push(shift(node('FieldText'), 220 - 615));
const message = shift(node('NameMessage'), 318 - 822);
nodes.push(message);
nodes.push(shift(node('NameDivider'), 352 - 586.6));

// The avatar section, 380 down: the header and its ornaments (the title divider's line and diamond,
// lighter), the big avatar's name and tagline, and the vertical divider. The avatars themselves and
// the grid are built by NamePromptCustomizer.
const top = 380 - 229;
const ornament = (id, file, rect, mirror) => {
  const n = { id, parent: 'Panel', rect, use: `UI/Decor/${file}`, kind: 'sprite', sprite: `Assets/_Project/Art/UI/Decor/${file}`, spriteRect: rect.slice(), spritePpu: 0, opacity: 0.55 };
  if (mirror) n.mirror = true;
  return n;
};
nodes.push({
  id: 'CharacterHeader', kind: 'label', parent: 'Panel', text: 'Choose your character', font: 'SerifBold',
  rect: [889, 229 + top, 259, 28], key: 'ui.name_prompt.choose_character',
  inkBox: [889, 229 + top, 1148, 256 + top], capTop: 230 + top, baseline: 249 + top, color: '#3E2210',
});
nodes.push(ornament('HeaderLineLeft', 'Deco_TitleDivider_Line.png', [795, 239.8 + top, 44, 3.4], true));
nodes.push(ornament('HeaderDiamondLeft', 'Deco_TitleDivider_Diamond.png', [841.15, 235.15 + top, 12.7, 12.7], false));
nodes.push(ornament('HeaderDiamondRight', 'Deco_TitleDivider_Diamond.png', [1185.15, 235.15 + top, 12.7, 12.7], false));
nodes.push(ornament('HeaderLineRight', 'Deco_TitleDivider_Line.png', [1199, 239.8 + top, 46, 3.4], false));

const name = shift(node('PlayerName'), 596 - 481.29);
name.text = 'Player';
nodes.push(name);
nodes.push(shift(node('Tagline'), 652 - 538.4));
const divider = node('StatsDivider');
divider.id = 'CharacterDivider';
divider.rect[1] = divider.spriteRect[1] = 390;
nodes.push(divider);

// Save where the mockup has it.
nodes.push(node('SaveButton'), node('SaveLabel'));

const layout = {
  name: 'NamePrompt',
  mockup: '22-name-entry.png',
  mockW: profile.mockW,
  mockH: profile.mockH,
  background: profile.background,
  backgroundRect: profile.backgroundRect,
  backgroundPpu: profile.backgroundPpu,
  nodes,
};
const dir = path.join(repo, 'ArtSource/Screens/NamePrompt');
fs.mkdirSync(dir, { recursive: true });
fs.writeFileSync(path.join(dir, 'layout.json'), JSON.stringify(layout, null, 2) + '\n');
console.log(`wrote ${path.relative(repo, path.join(dir, 'layout.json'))} (${nodes.length} nodes)`);
