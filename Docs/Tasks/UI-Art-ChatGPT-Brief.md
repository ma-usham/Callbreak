# Brief: generate the UI kit with ChatGPT (designers)

Shared brief for the designer agents. Your own asset family and worklist are in the message that
sent you here.

## Why
The owner looked at the running game and said: the UI on the screens is only screenshots and
crops of the mockups; use ChatGPT (open in his Brave browser) to generate the same UI as the
reference art, give it the reference and ask for each UI part, and reuse a part wherever it can
be reused. So every UI piece that is currently cut out of a mockup gets a clean ChatGPT
extraction: no baked-in text, no background pixels, no inpainting smudges, twice the resolution.
The layout, the text and the views stay as they are; only the art files change.

## Read first
1. `AGENTS.md` (standing rules).
2. The Designer Handbook, UTF-8 text at
   `C:\Users\maush\AppData\Local\Temp\claude\D--UnityProjects-Callbreak\309c890b-f66a-465e-97b7-580a0890364b\scratchpad\docs\Callbreak_Designer_Handbook_v1.0.txt`,
   lines 286-830: the eight-step extraction loop, "Talking to ChatGPT" (P-0 project instructions,
   anatomy of a request, its limits), the prompt library (BG-1..4, UI-1..7, IC-1/2, F-1..6), sizes and
   9-slice, naming, folders, the prompt log template and the master asset list. Follow it.
3. `ArtSource/Prompts/MainMenu_Batch.md`: the prompts that produced the approved main menu pieces.
4. `Tools/ArtPipeline/README.md` (the PowerShell/C# helpers from the main menu: `crop.ps1`,
   `sheet.ps1`, `sample.ps1`, `compare.ps1`, `art.ps1` with `Get-Cutout`) and `Tools/ArtTool/README.md`
   §0-1 (`Tools/art crop|grid|probe|bounds|fitrect|inkbox` for measuring the mockups; how screens
   reference kit sprites with `"use"`).
5. The mockups: `Assets/ArtRefrences/game-screens/NN-*.png` (1672 x 941). Each built screen's
   `ArtSource/Screens/<Name>/screen.json` lists its elements with their rectangles in mockup pixels:
   use those rectangles for your crops.

## ChatGPT: how to drive it
- Browser: the owner's **Brave**, through the Claude in Chrome tools (`mcp__claude-in-chrome__*`; load
  them with one ToolSearch call: `select:mcp__claude-in-chrome__list_connected_browsers,mcp__claude-in-chrome__select_browser,mcp__claude-in-chrome__tabs_context_mcp,mcp__claude-in-chrome__tabs_create_mcp,mcp__claude-in-chrome__navigate,mcp__claude-in-chrome__computer,mcp__claude-in-chrome__find,mcp__claude-in-chrome__read_page,mcp__claude-in-chrome__get_page_text,mcp__claude-in-chrome__file_upload,mcp__claude-in-chrome__javascript_tool`).
  Several browsers are connected: list them and select Brave. Do not use the built-in Browser pane
  (it cannot attach files).
- **One chat, yours alone, for everything you do.** Open one new tab, go to the ChatGPT project
  "CardGame" (`https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/project`) and start
  ONE new chat there. **Your first message is just `hi`. Wait for ChatGPT's reply. Only then start
  working** (the owner asked for exactly this). Never open a second chat: not per screen, not per
  asset, not after an error. If the chat stops answering (rate limit, HTTP 429, "too many
  requests"), wait a few minutes and try again, waiting longer each time; if it is still dead after
  about 30 minutes, stop and report instead of opening another chat. Write the chat's URL into your
  prompt log as soon as it has one.
- Other agents drive other tabs of the same browser at the same time. Work only in your own tab
  (keep its tabId), never close, navigate or click in a tab you did not open, and prefer reading the
  page (`find`, `read_page`, `get_page_text`, `javascript_tool`) over screenshots.
- After `hi`, send the handbook's P-0 project instructions once, adapted to your family, with the
  reference screens you will need attached, and ask it to reply "Ready".
- Attach files with `file_upload` on the "Attach files" input (re-`find` the ref every time; it
  goes stale). Put multi-line prompts in with a synthetic `paste` event on `.ProseMirror`, then
  click Send. Always attach the full screen plus a tight, enlarged crop of the element; the crop is
  what ChatGPT copies.
- One asset per request, except for small uniform things (icons, chips, dots): there, attach a
  contact sheet of the crops on a grid and ask for the same grid redrawn clean, then split it. That
  keeps a family consistent and saves requests. Pace yourself: a few requests, then check results.
- Leave the chat's model and thinking setting as the owner has it.
- Downloads: open the image, click the download arrow in the lightbox. Files land in
  `C:\Users\maush\Downloads` as `ChatGPT Image <date>, <time>.png`. Because other agents download
  too, take the download lock first: `mkdir ArtSource/Working/_download.lock` (retry until it
  succeeds), click download, move the newest new file into your working folder, then
  `rmdir ArtSource/Working/_download.lock`. Never hold it longer than a minute.
- Never delete or archive chats, never change account settings, never send anything but these art
  requests. If ChatGPT shows a dialog you do not understand, stop and report.

## What a finished asset is (handbook §11)
- **Empty**: no letters, no numbers, no icon on a button, no shadow baked in (shadows are separate
  shared sprites). **Transparent** outside the shape with clean alpha (check it on black and on
  white; if ChatGPT cannot give real transparency, ask for a flat pure-magenta background and key
  it out). **Front-on and symmetric**: for anything symmetric keep the better half and mirror it.
- **Size @2x** = mockup pixels x 2.3, rounded up to even numbers; never upscale ChatGPT's output by
  more than 2x (crop tighter and ask again instead). 9-slice pieces are delivered at a convenient
  size with a plain, even middle; measure the borders (left, top, right, bottom in sprite pixels).
- **Colours match the mockup**, not ChatGPT's taste: measure your result against the crop
  (`Tools/art probe`, `sample.ps1`) and correct the drift (ChatGPT tends to return crimson more
  saturated and ivory cooler). Reference values: crimson #861018, ivory #FFFBF5, gold #B8914F,
  icon charcoal #231C19 (the mockups' icons are near-black).
- **One file per distinct element.** If the same element appears on several screens (a toggle, a
  row highlight, a header divider), make it once and list every screen that uses it.
- Do not spend ChatGPT requests on hairlines (rules, separators and dividers thinner than about
  8 mockup pixels without ornament): leave those to the screen engineers as drawn shapes, and say
  so in your map.

## Where things go
| What | Where |
| --- | --- |
| Attachments you prepare | `ArtSource/Working/Kit/<Family>/attach/` |
| ChatGPT originals | `ArtSource/Working/Kit/<Family>/chatgpt/<Asset>_a1.png`, `_a2.png`, ... (keep every attempt) |
| Your scripts | `Tools/ArtPipeline/Kit/<Family>/` (your own; shared scripts only additively) |
| Final sprites for the game | `Assets/_Project/Art/UI/<Folder>/<Name>.png` (folder given in your message), with a copy in `ArtSource/Exports/<UI|Icons|Decor>/` |
| Import settings | `ArtSource/Kit/<Family>.json`: `{ "sprites": [ { "path": "UI/Panels/UI_Panel_Large.png", "ppu": 200, "nine": [L, T, R, B] } ] }` (path under `Assets/_Project/Art`; ppu 200 = an @2x export shown at mockup size; omit `nine` if it never stretches). Apply with `Tools/Unity/ueval.sh 'return Callbreak.Editor.KitImporter.Apply();'` |
| Swap map for the screen engineers | `ArtSource/Kit/<Family>.map.csv`, header `old_sprite,screens,new_sprite,mock_rect_note,nine_ltrb,status`: one row per existing cut sprite you replace (old path under `Assets/_Project/Art`), which screens use it, the kit sprite to `use` instead, anything they must know to place it, `status` = ready / pending / keep-as-drawn |
| Handoff rows | append to `ArtSource/handoff.csv` (`file,type,size_px,nine_slice_ltrb,used_on,prompt,status,notes`; keep its UTF-8 BOM and line endings; append whole lines in one write) |
| Prompt log | `ArtSource/Prompts/<Family>_Kit.md`: chat URL, every prompt as sent, every attempt with its verdict, the accepted file and its export size (handbook template) |

Kit folders already holding approved ChatGPT work (do not regenerate or overwrite): `UI/Buttons/*`,
`UI/Icons/Icon_{Settings,Shop,Friends,Offline,Chevron,Spade_Gold}.png`, `UI/Avatars/*`,
`UI/Ornaments/*`, `Backgrounds/BG_Menu_Clean.png`, the three card decks in `Art/Cards/`.

## Check every asset before you call it done
1. Scale the final sprite to its mockup rectangle, composite it over the mockup crop and look at
   both side by side at 3x (`sheet.ps1` / `Tools/art crop --scale 3`). Shape, ornament, rim and colour
   must read as the same element; anything a player would notice is a miss.
2. Alpha on black and on white: no halo, no grey fringe, no stray pixels.
3. For a 9-slice piece: stretch it to 3x width and height with your borders; nothing may smear.
4. At most five attempts per asset (handbook); then keep the best, mark it `pending` with what is
   wrong, and move on.

## Rules
- You do not edit screens, prefabs, `screen.json`, views or code outside `Tools/ArtPipeline/Kit/<Family>/`.
  The swap into the screens is a later step done by the screen engineers from your map.
- No git commands that change anything. Do not enter Play mode, do not open scenes; the only Unity
  command you run is the `KitImporter.Apply()` line above (it takes the shared Editor lock).
- Do not spawn sub-agents.
- If you run long, finish the asset you are on, write the log and the map, and report what is left.

## Report (final message)
The chat URL; per asset: file, size, 9-slice, attempts, what still differs from the mockup; the
map file; shared scripts you touched; anything not verified; suggestions.
