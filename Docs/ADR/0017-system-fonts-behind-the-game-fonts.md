# ADR 017 - The device's fonts stand behind the game's Latin-only fonts

**Status:** proposed.

**Context.** The game's two typefaces (Inter, Source Serif 4) are baked into static TextMeshPro
atlases with Basic Latin and a little punctuation: that keeps every label sharp and the atlases
small. The name rules (GDD §11 screen 14) allow letters of any script, and a Nepali player will
type a Devanagari name. Such a name, or one with an accent, came out as a row of empty boxes on
every screen that shows it. The typefaces also lack the spade, diamond and club symbols (Inter has
only the heart).

**Decision.** At startup `SystemFontFallbacks` creates dynamic font assets from the device's own
fonts - a wide Latin family and a Devanagari family per platform - and adds them to TextMeshPro's
global fallback list. No font file is shipped. Suit symbols in table text are inline sprites, not
font glyphs.

**Consequences.** Names show as letters on Windows (verified: Segoe UI and Nirmala UI) and should
on Android (Roboto, Noto Sans Devanagari) and iOS (Helvetica Neue, Kohinoor Devanagari), which
has not been run. A device without those families still shows boxes. TextMeshPro does not shape
complex scripts, so Devanagari conjuncts are not formed: a name is readable, a Nepali translation
of the UI would not be good enough (GDD §15 risk; the Nepali strings stay empty until that is
solved with a shaping plugin or UI Toolkit text). The fallback glyphs are regular weight whatever
the label's weight is.
