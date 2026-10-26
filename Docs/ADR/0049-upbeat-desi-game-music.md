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
- **The old tracks are gone** (the owner, 26 Oct 2026): their Gemini sources
  (`ArtSource/Audio/Sources/Gemini/MUS_Menu_Theme.*`, `MUS_Table_Loop.*`), their prompts and their
  loop points are deleted. They are in git history before this change.

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

## Addendum, 27 Oct 2026: one music for the menu and the table

The owner gave the game a single track for both places: "use single music for both. remove the
requirement of two different music." They first sent one Gemini track, then swapped it for another
before it was done: **"The Quiet Round"** (Gemini, downloaded as `The_Quiet_Round.mp3`).

- **The loop:** `MUS_Game_Theme`, 19.05-150.90 s of the 181.6 s track.
  - That's 131.8 s, 178 beats at about 81 BPM, with the 12 s soft intro and the fade from 155 s left
    out.
  - Its beat is soft, so whole-bar loops matched poorly (texture 0.40). The loop was chosen by
    harmony and timbre with `find-loop.py` (texture 0.71, about as good as the old table loop's 0.72).
  - AudioGen: rhythm match 0.54, seam 0.01, kink 0.01, -14 LUFS, peaks at -1.1 dBFS.
- **The library:** `AudioLibraryBuilder` takes the one `MUS_...` clip in the Music folder and gives it
  to every `MusicTrack`. The menu and the table each keep their volume (0.7), and a second music file
  is reported as a problem. Import settings are as before: Streaming, Vorbis 0.5, stereo.
- **Playing on:** the code still says where the player is (`MusicTrack.Menu`, `MusicTrack.Table`).
  - When the next place's clip is the one already playing, `AudioService` keeps it on its source.
  - Only the level moves: the table's 6 dB drop (and the rise back on the menu) glides over the
    usual 1.5 s instead of the song starting again.
- **Removed:** the two loops of 26 Oct (`MUS_Menu_Theme`, `MUS_Table_Loop`), their Gemini sources and
  prompts, and their `sources.json` entries. Git history has them.
- **AudioGen** no longer renders its synthesized menu and table loops (`Music.cs`). A full run would
  otherwise have put them back beside the one track.
- **Tests:** `AudioServiceMusicTests` (EditMode, engine) check that the menu and the table share one
  loop, and that going to the table, and back, keeps the song on its source with no second copy.
- **Phone speakers:** the track is dark and mellow. 54% of its energy is below 150 Hz and 40% at
  150-500 Hz, so a phone's own speaker loses about 9.2 dB of it, more than the old loops' 3.7 and
  7.2 dB. If it's too quiet there, the library volumes can go from 0.7 to 1.0 (+3.1 dB).
- **Not verified on a phone,** and nobody has heard it in the game yet. The owner gets a 20 s clip
  across the loop point to listen to.
