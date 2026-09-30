# ADR 010 - Screens laid out on their mockups, art shared

**Status:** accepted (owner, 30 Sep 2026: "all the panels have the same background, they can be
reused; reusable UI can be reused with 9-slice").

**Context.** The 17 mockups were generated one by one, so the scenery, panel ornaments and button
details differ slightly between screens. Rebuilding each screen from its own pixels (a plate and
panels per screen) matched each mockup almost exactly but repeated the same art many times. The
owner chose reuse.

**Decision.**
- Every menu-style screen (01-06, 09-12, 14-16) sits on one shared background, the main menu's
  scenery extended to 2560 × 1440 so 20:9 phones and 4:3 tablets see more scenery instead of a
  zoom. The table screens share one table background.
- Panels, primary and secondary buttons, header discs, the avatar, the title divider, the logos
  and the common icons come from one shared kit (`Assets/_Project/Art/UI/Common` plus the main
  menu's buttons), 9-sliced where their size changes, with shared shadow sprites.
- Each screen is still laid out on its own mockup: every element at its measured position and
  size, every label fitted to the ink box measured there (`Tools/ArtTool`, `Tools/Unity/screen.sh`).
  Only what is unique to a screen is cut from its mockup.
- Screens slide only their content during transitions, so the shared background stays still.

**Consequences.** Consistent art and far fewer assets. A screen differs from its mockup where
the mockup drew its own variant of a shared element (a button without the gold rim, different
panel ornaments, the scenery composition); each screen's report lists these. The per-screen
ChatGPT plates made before this decision are kept under `ArtSource/Working/*/chatgpt/` but unused.
