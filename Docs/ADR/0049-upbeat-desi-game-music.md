# ADR 049 - Upbeat desi game music for the menu and the table

**Status:** accepted (owner, 26 Oct 2026).

**Context.** The owner said the menu and table music "don't feel right". The game played two Gemini
loops made on 1 Oct 2026 from Nepali folk prompts (bansuri, madal, sarangi): 53.8 s for the menu
(125 BPM) and 39.0 s for the table (80 BPM), short enough that the repeat is easy to hear.

- A first set of six calm Nepali and Himalayan folk candidates (bansuri, sarangi, madal, tanpura,
  slow and gentle, as the session's brief suggested) was turned down: the music should be "like
  callbreak game music like other games", not like the current folk.
- Asked which style other Callbreak games are closest to, the owner chose **upbeat desi game
  music**: catchy casual-game loops with a South Asian flavour, as in Teen Patti, Callbreak and Ludo
  apps (tabla, dholak, madal over modern drums and bass, a flute or sitar hook), the table a lighter
  version of the menu so it does not tire.
- Six Gemini tracks were made in that style, three for each place; the owner picked one of each.
  ElevenLabs Music made four more in the owner's account, but the Free plan can't download music, so
  they were not in the choice.

## Decision

- **Menu: Gemini's "Table of Kings"**: a sitar riff and a flute tune over tabla and dholak with
  hip-hop drums, claps, round bass and synth plucks, about 104 BPM. Loop 28.85-144.20 s of the track:
  **115.4 s, 50 bars**.
- **Table: Gemini's "Copper Tiles"**: lighter, a relaxed tabla and madal groove over soft drums and
  warm bass, a flute and sitar motif that comes and goes, soft pads, about 95 BPM. Loop
  39.15-140.21 s: **101.1 s, 40 bars** (the track turns into a quieter outro at 143 s, so no longer
  loop is clean).
- **The same files and settings.** They replace the contents of `MUS_Menu_Theme.wav` and
  `MUS_Table_Loop.wav`, so the cue library, the GUIDs and the import settings are unchanged:
  Streaming, Vorbis quality 0.5, stereo, 44.1 kHz (`AudioLibraryBuilder`). Nothing in the code
  changes: the menu still starts `MusicTrack.Menu`, the table `MusicTrack.Table`, with the 1.5 s
  crossfade, the table 6 dB down, the Music switch and Volume as before, and silence under
  full-screen ads (ADR 035).
- **Loudness as before.** Both loops are -14 LUFS integrated with peaks at -1.1 dBFS (like the old
  ones), and the library keeps its volumes (0.7 each), so in the game the menu sits near -17 LUFS and
  the table near -23 LUFS against the effects, as they did.
- **Seamless loops** made by `Tools/AudioGen` from `ArtSource/Audio/Sources/sources.json`: whole
  bars, the end aligned by waveform, a 2 s crossfade into the music before the start. The loop points
  were found with the new `Tools/AudioGen/find-loop.py`, which compares harmony, timbre and onsets
  of the 6 s leading into each point (the beat-only `find-loop.js` is unreliable on these). Measured
  match of the music leading into the loop point with that leading into the start: menu rhythm 0.67,
  waveform 0.82; table 0.61 and 0.30 (old loops: 0.42 / 0.13 and 0.54 / 0.24). AudioGen's checks
  pass (seam 0.10 and 0.03, kink 0.02 and 0.01, no clipping).
- **How they were made.** Gemini "Create music" in the owner's Google account, Standard length
  (about 3 minutes), Instrumental, genre Custom, 26 Oct 2026; the prompts are in `sources.json`. The
  MP3s (192 kbps; Google's C2PA "Created by Google Generative AI" and SynthID marks, nothing personal)
  are kept with WAVs decoded by the new `Tools/AudioGen/decode-mp3.ps1` (Windows Media Foundation).
- **The old tracks stay** until the owner says they can go: their Gemini sources remain in
  `ArtSource/Audio/Sources/Gemini/MUS_*`, and each music entry in `sources.json` keeps the old loop
  under `previous`, so going back is moving those values up and running AudioGen.

## Consequences

- The menu repeats every 115 s and the table every 101 s, against 54 s and 39 s.
- **Phone speakers.** "Copper Tiles" has two thirds of its energy below 150 Hz. Through a phone
  speaker (modelled as a 400 Hz high-pass) it loses 7.2 dB, the old table loop 2.9 dB, so on a
  phone's own speaker the table music is about 4 dB quieter than before at the same settings (the
  menu loses 3.7 dB, as the old one did). If the owner finds it too quiet there, its library volume
  can go from 0.7 to 1.0 (+3.1 dB), which also makes it 3 dB louder on headphones.
- **Size.** The loops are 2.3 times as long, and the music Unity imports grows from 1.9 MiB to
  4.7 MiB (menu 1.11 to 2.51 MiB, table 0.78 to 2.19 MiB, Vorbis 0.5 as before), about 2.8 MiB more
  in the build. A lower Vorbis quality would win some of it back; that is the owner's call.
- **The loops stay seamless through Vorbis.** Decoded by Unity, both keep their exact sample count,
  the jump across the loop point is 0.07 (menu) and 0.02 (table) of an ordinary large step between
  two samples, and the codec error at the ends is no worse than in the middle.
- **Keys.** Both tracks sit around F and B♭ major, not the G of the synthesized sounds; the
  ElevenLabs effects are in mixed keys anyway (the call-met chime is near B♭).
- **Licences.** As for the effects (AudioGen's README): check Google's terms for Gemini music before
  the game is published.
- **Not verified on a phone.** Nobody has heard them in a build on a device yet; the owner listened
  to the loops and their loop points as files.
