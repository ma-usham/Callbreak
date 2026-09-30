// Collects every place a family icon is drawn on a built screen, from ArtSource/Screens/*/screen.json:
// the node, its rectangle on the mockup, the round button it sits on and the cut sprite it shows today.
// Writes uses.json next to this file; icontool's "place" and "map" commands read it.
//   node Tools/ArtPipeline/Kit/Icons/uses.js
const fs = require("fs"), path = require("path");
const here = __dirname, repo = path.resolve(here, "../../../..");
const root = path.join(repo, "ArtSource", "Screens");

// Cut sprite (as a screen names it) -> family icon.
const rename = {
  Icon_Trump: "Icon_Suit_Spade", Icon_Rule_Trump: "Icon_Suit_Spade", Icon_SearchSpade: "Icon_Suit_Spade",
};
// The cut sprite a family icon replaces, for a node that already shows the family icon.
const kitOld = { Icon_Back: "UI/Common/Icon_Back.png", Icon_Home: "UI/Common/Icon_Home.png", Icon_Scorecard: "UI/Common/Icon_Scorecard.png", Icon_Close: "UI/Common/Icon_Close.png" };
const screenOld = {
  "Icon_Suit_Spade@Table": "Screens/Table/Icon_Trump.png", "Icon_Suit_Spade@HowToPlay": "Screens/HowToPlay/Icon_Rule_Trump.png",
  "Icon_Suit_Spade@Matchmaking": "Screens/Matchmaking/Icon_SearchSpade.png",
  "Icon_Sound@MatchMenu": "Screens/Settings/Icon_Sound.png", "Icon_Copy@PrivateLobby": "Screens/JoinRoom/Icon_Copy.png",
};
function originalOf(icon, screenName) {
  return kitOld[icon] || screenOld[`${icon}@${screenName}`] || `Screens/${screenName}/${icon}.png`;
}
// Uses whose surroundings would disturb the fit: the spade of the search ring sits inside the crimson arc.
const clip = { "Matchmaking.SearchSpade": [779, 246, 112, 112] };
// Approved main-menu icons and their white copies are not part of this family.
const notOurs = /Icon_(Settings|Shop|Friends|Offline|Chevron|Chevron_Light|Spade_Gold)(\.png)?$/;

function strip(s) { return s.replace(/^\s*\/\/.*$/gm, "").replace(/([^:"])\/\/.*$/gm, "$1").replace(/,(\s*[\]}])/g, "$1"); }

const uses = [];
for (const dir of fs.readdirSync(root).sort()) {
  const file = path.join(root, dir, "screen.json");
  if (!fs.existsSync(file) || dir.startsWith("CommonKit")) continue;      // the kit screens are the old cuts themselves
  const spec = JSON.parse(strip(fs.readFileSync(file, "utf8")));
  const byId = Object.fromEntries(spec.nodes.map(n => [n.id, n]));
  const outDir = spec.outDir || `Screens/${spec.name}`;
  const cutHere = new Set(spec.nodes.filter(n => n.out).map(n => n.out));
  for (const n of spec.nodes) {
    const ref = n.use || n.out || "";
    if (!/Icon_/.test(ref) || notOurs.test(ref)) continue;
    const base = path.basename(ref, ".png");
    const swapped = !n.out && ref.startsWith("UI/Icons/");                // the screen already shows the family icon
    const icon = rename[base] || base;
    const old = swapped ? originalOf(icon, spec.name)
      : n.out ? `${outDir}/${n.out}.png`
      : ref.includes("/") ? ref
      : cutHere.has(base) ? `${outDir}/${base}.png` : ref;
    const grow = n.shape ? 3 : 5;                                         // tight sprite rects need more room than loose ones
    const r = n.rect;
    const parent = n.parent ? byId[n.parent] : null;
    uses.push({
      icon,
      screenName: spec.name, screen: spec.mockup.slice(0, 2), node: n.id,
      rect: [Math.floor(r[0] - grow), Math.floor(r[1] - grow), Math.ceil(r[2] + 2 * grow) + 1, Math.ceil(r[3] + 2 * grow) + 1],
      disc: clip[spec.name + "." + n.id] || (parent && parent.shape === "ellipse" ? parent.rect : undefined),
      old, swapped: swapped ? true : undefined, hidden: n.hidden === true ? true : undefined, nodeRect: r,
    });
  }
}
fs.writeFileSync(path.join(here, "uses.json"), JSON.stringify({ uses }, null, 1).replace(/\n\s+(-?[\d.]+,?)(?=\n)/g, " $1").replace(/\[ /g, "[").replace(/\n\s+\]/g, "]") + "\n");
const byIcon = {};
for (const u of uses) (byIcon[u.icon] = byIcon[u.icon] || []).push(`${u.screen} ${u.screenName}.${u.node}`);
for (const k of Object.keys(byIcon).sort()) console.log(k.padEnd(24), byIcon[k].join("; "));
console.log(`${uses.length} uses -> uses.json`);
