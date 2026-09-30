# Brief: rebuild menu and result screens from their mockups (visual pass)

Shared brief for the screen agents. Your own screen list and view contracts are in the message
that sent you here.

## Goal
Each screen must look **exactly** like its reference mockup (`Assets/ArtRefrences/game-screens/NN-*.png`,
1672 × 941). The owner compares screenshots side by side and rejects "close". Screen 01
(Play with Friends) is done with this pipeline and is the model to copy:
`ArtSource/Screens/PlayWithFriends/screen.json`, `Assets/_Project/Scripts/Presentation/Screens/PlayWithFriends/PlayWithFriendsView.cs`,
and its results in `ArtSource/Screens/PlayWithFriends/check/`.

## Read first (in this order)
1. `Tools/ArtTool/README.md` - the whole pipeline: measuring, `screen.json`, labels, views, widgets, plates, build and compare. Follow it exactly.
2. `AGENTS.md` - standing rules.
3. The GDD's screen specs for your screens (GDD §11), UTF-8 text at
   `C:\Users\maush\AppData\Local\Temp\claude\D--UnityProjects-Callbreak\309c890b-f66a-465e-97b7-580a0890364b\scratchpad\docs\Callbreak_GameDesign_TechArchitecture_v1.1.txt`
   (screens 00-16 are around lines 816-1400; art direction §12 around 1396-1550). They tell you what is dynamic.

## Per screen, the loop
1. Look at the mockup (Read the PNG; zoom with `Tools/art grid`). List every element and decide:
   separate node (anything dynamic or stateful, every word) or baked into its parent (static decoration).
2. Measure (`fitrect`, `profile`, `inkbox`, grids) and write `ArtSource/Screens/<Name>/screen.json`.
3. `Tools/art job <Name>` → check `ArtSource/Screens/<Name>/check/report.txt` (reconstruction error ≲ 1.0) and `diff-x6.png`. Fix rects/shapes until clean.
4. Write `<Name>View` (+ a customizer and any screen-specific widget).
5. `node Tools/Unity/compile-check.js`, then `Tools/Unity/screen.sh <Name> --compile`.
6. Read `check/capture-report.txt` (labels: dx, dy within ±1 px; w, h within ~1 px) and LOOK at `check/capture-split.png`, `check/capture-split-v.png`, and the 21:9 / 4:3 captures in `Temp/Captures/`. Iterate until nothing visible differs except what the rules below allow.

## Rules
- Files you own: `ArtSource/Screens/<Name>/**`, `ArtSource/Working/<Name>/**` (except `chatgpt/`, which the ChatGPT designer writes),
  `Assets/_Project/Art/Screens/<Name>/**`, `Assets/_Project/Art/Backgrounds/BG_<Name>.png`,
  `Assets/_Project/Scripts/Presentation/Screens/<Name>/**`, `Assets/_Project/Scripts/Editor/Screens/<Name>Customizer.cs`,
  and new widget files you create under `Assets/_Project/Scripts/Presentation/Widgets/` (give them specific names; check the folder first and reuse what exists).
  arttool appends your rows to `ArtSource/handoff.csv` itself.
- Shared files (`Tools/ArtTool/*`, `UiBuildKit.cs`, `ScreenLayoutBuilder.cs`, `ScreenLayout.cs`, the framework, `ScreenCaptureTool.cs`) are used by other agents at the same time.
  Change one only if you are truly blocked, and then only additively (a new optional field or branch; existing behaviour unchanged). Afterwards run
  `node Tools/Unity/compile-check.js` and `Tools/Unity/screen.sh PlayWithFriends` and confirm its report is unchanged, and list the change in your report.
- Never leave C# in `Assets/` that does not compile: other agents share the Editor. Run the compile check before `--compile`.
- `Tools/Unity/screen.sh` takes a lock on the shared Unity Editor; waiting is normal. Do not run any other `unity` command that changes the project, do not enter Play mode, and do not open scenes.
- No git commands that change anything (no add/commit/checkout/stash/reset). The orchestrator commits.
- Views: `public sealed class <Name>View : UIScreen`, `[SerializeField] private` fields only, an event per intent, setters for everything a presenter renders, no logic and no services. Presenters come in a later task; write the view so a presenter can drive everything the GDD lists for the screen (texts, states, enabled flags).
- Every label that shows fixed copy gets a string-table `key` (`ui.<screen_snake>.<name>`), and its `text` is the mockup's exact English wording.
  Dynamic labels (names, codes, numbers, scores) get no key; their mockup text is just the design-time sample.
- Where the GDD's copy differs from the mockup's wording, keep the mockup's layout, use the GDD's copy only if the GDD explicitly calls it a copy update,
  and list the conflict in your report. Never invent new visual design; if the GDD needs a state the mockup does not show (an empty seat, a disabled button),
  derive it from existing art in the most obvious way (e.g. dimmed avatar, the same button at 50% opacity) and list it.
- ChatGPT plates: a separate designer is making clean plates in ChatGPT. When `ArtSource/Prompts/<Name>_Plate.md` exists with an `Accepted:` line,
  set `"plate": { "generated": "ArtSource/Working/<Name>/chatgpt/<that file>" }`, re-run the job and the screen, and compare with and without; keep the better one and say which.

## Report (your final message)
Per screen: the files you created, the arttool reconstruction error, the capture-vs-mockup error and the worst label deltas, what still differs visibly
from the mockup and why, the view's public API (events and setters), any shared file you changed, and anything not verified. Honesty over polish.
