# audiogen: Callbreak's sound effects and music

Every sound the game plays (GDD §14) is made here, under the file names the game knows. Since
1 Oct 2026 all of them come from **generated sources**: the effects from ElevenLabs, the two music
loops from Gemini (see [Generated sources](#generated-sources)). The synthesizer below still renders
any cue the sources do not cover, and is what every cue used before. A composer or a foley session
can replace any sound later **under the same file name** and nothing else changes.

```bash
dotnet run --project Tools/AudioGen -c Release            # render everything, then measure it
dotnet run --project Tools/AudioGen -c Release -- --check # only measure what is on disk
```

| Option | What it does |
| --- | --- |
| `--only <text>` | Only the cues whose file name contains the text (`CardPlay`, `MUS_`, ...) |
| `--check` | Measure the files already on disk; render nothing |
| `--pictures <dir>` | Also write a waveform + spectrogram PNG per file (loops: plus one per 10 s) |
| `--stems <dir>` | Also write each loop's separate parts (flute, madal, tanpura, drone, room) |
| `--root <path>` | The Unity project folder (default: found from the current folder) |

It writes `Assets/_Project/Audio/SFX/*.wav` and `Assets/_Project/Audio/Music/*.wav` and an identical
copy under `ArtSource/Audio/`. Rendering takes about 15 seconds. It is deterministic (every cue is
seeded from its own file name; the same machine and .NET runtime give the same bytes), and a file
whose bytes did not change is not rewritten, so an unchanged run gives Unity nothing to re-import.

**After the files change**, run **Callbreak > Build > Audio Library** in the Editor
(`Callbreak.Editor.AudioLibraryBuilder.Build()`): it applies the import settings and lists every
clip in `Assets/_Project/Config/Audio/AudioCueLibrary.asset`, which is what the game's audio
service reads.

Format: 44.1 kHz, 16-bit PCM. Effects are mono and peak at -3 dBFS. Music is stereo at -14 LUFS
(integrated, ITU-R BS.1770) with peaks under -1 dBFS. Because every effect peaks at the same level,
how loud each one is in the game is set by its volume in the cue library, not by the file.

Build output goes to `/obj/AudioGen` (already ignored). Note that `.gitignore` ignores `*.csproj`,
so `AudioGen.csproj` has to be added with `git add -f`.

## Generated sources

`ArtSource/Audio/Sources/sources.json` lists, for each cue it covers, the source file and how to cut
it, and keeps every prompt. A cue listed there is made from its source (`Recorded.cs`) instead of
being synthesized, and goes through the same checks.

- **Effects** (`ElevenLabs/<Cue>/take<N>.wav`): ElevenLabs Sound Effects, 48 kHz WAV, prompt
  influence 60%. Each generation gave four takes and two to four were downloaded; `source` names the
  one in use, so another take is a one-word change. An effect is folded to mono and resampled to
  44.1 kHz, starts 3 ms before its first sound, stops after its last (at most `max` seconds), fades
  in over 1 ms and out over `fadeOut` to an exact zero, and is peak-normalised and padded to whole
  ADPCM blocks. `drive` rounds the transient (tanh), as the synthesized card sounds did, where a take
  was too quiet for its old in-game loudness at full volume.
- **Music** (`Gemini/MUS_*.mp3`, decoded to `.wav` beside it by Unity's own MP3 import): Gemini
  "Create music", instrumental. A loop is cut between `loop` [start, end], chosen as a whole number
  of bars at the track's tempo with `node Tools/AudioGen/find-loop.js <wav> <from> <to> <crossfade>
  <beat guess> <beats per bar>` (menu: 125 BPM, 28 bars; table: 80 BPM, 13 bars). The end is aligned
  to the sample by waveform correlation, and the last `crossfade` seconds fade into the music just
  before the start, so the seam is like any other sample. Then -14 LUFS, peaks under -1 dBFS.

The cue volumes in `AudioLibraryBuilder` were recomputed from the "loud" column so that each cue
keeps the loudness it had in the game with the synthesized sounds (within 0.5 dB).

Licences: generated sound is only as free to use as the generator's plan allows. Check that the
ElevenLabs plan the effects were made on includes a commercial licence, and Google's terms for
Gemini music, before the game is published.

## What you should hear

Everything tonal is in **G, just intonation**, on the pentatonic Sa Re Ga Pa Dha (G A B D E): the
pitch of the common G-medium bansuri, and high enough that a phone speaker can play it. A sting
that fires over the music is therefore always in key with it.

### Cards

| File | Length | Plays when | It should sound like |
| --- | --- | --- | --- |
| `SFX_CardShuffle_01` | 0.80 s | Round start | A quick riffle: about fifty soft paper ticks ("brrrrt"), a short falling "frrp" as the two halves drop together, then a soft tap as the deck is squared on the table. Papery, not hissy; no pitch. |
| `SFX_CardDeal_01..03` | 0.15 s | Each card dealt (the game varies pitch ±3%) | A short "fft" of one card sliding off the deck, ending in a light flick. Three takes of slightly different length and colour. |
| `SFX_CardPlay_01..04` | 0.20 s | A card lands on the table | "Fwip-pat": a tiny rising flick of paper, then a soft flat pat on felt over wood and a whisper of the card settling. No ring, no click. |
| `SFX_TrickCollect_01` | 0.40 s | The trick sweeps to its winner | A soft sliding "shhk" that tightens as four cards bunch up, three tiny edge bumps, a soft pat as the stack comes to rest. |

### Calls, turns and results

| File | Length | Plays when | It should sound like |
| --- | --- | --- | --- |
| `SFX_CallConfirm_01` | 0.25 s | A call is placed | One soft wood-block "tok" on D5. |
| `SFX_YourTurn_01` | 0.60 s | Your turn begins | Two marimba notes rising a fourth, D5 to G5, soft mallets, a little room. Friendly, not urgent. |
| `SFX_TimerTick_01` | 0.08 s | Each of the last 5 seconds | A tiny dry wooden "tk" near 1 kHz. |
| `SFX_CallMet_01` | 0.90 s | Round results, call met | Four soft bar chimes rising G4 B4 D5 G5 and ringing together as a warm major chord. |
| `SFX_CallMissed_01` | 0.70 s | Round results, call missed | Two felt-muted low notes falling G4 to E4, like a tongue drum under a cloth. A sigh, not a buzzer. |
| `SFX_RoundEnd_01` | 1.20 s | Round results appear | A chime phrase stepping down D5 B4 A4 G4, the last note doubled an octave lower, ringing out. |
| `SFX_Victory_01` | 3.00 s | Match won | A madal roll that speeds up into two big strokes ("Dha ... Dhin", the bass head bending upward on the last), then a bansuri runs up D E G A B, leans back through A and rests on G with vibrato, over a quiet drone and one faint chime. |

### Interface

| File | Length | Plays when | It should sound like |
| --- | --- | --- | --- |
| `SFX_UiTap_01` | 0.08 s | Any button | A small rounded wooden "tok" on G5. |
| `SFX_UiBack_01` | 0.10 s | Back / close | The same, a fourth lower (D5) and leaning downward. |
| `SFX_Toggle_01` | 0.12 s | A switch flips | Two tiny wooden ticks, "tk-tok", the second lower. |

### Music

Both loops are a bansuri over a tanpura and a soft sustained drone with a light madal, in the manner
of the hill tunes (Pahadi): phrases that dip to the Dha and Pa below Sa and come home.

| File | Length | Form |
| --- | --- | --- |
| `MUS_Menu_Theme` | 80.0 s = 32 bars of 4/4 at 96 BPM | Bars 1-4: a slow opening, the flute gliding up to G after 1.25 s. 5-12: the tune (B. D B A, G A B A ...) and its answer, which dips to E and D below G. 13-20: a higher middle part that climbs to B5. 21-28: the tune again, closing on a long G. 29-30: a quiet echo. 31-32: drone and drum only, with a fill into the repeat. The madal plays a light eight-beat groove ("Dha . ta ti, ta ka Dhin ta"); the tanpura is plucked every 1.25 s (D, G, G, low G). |
| `MUS_Table_Loop` | 84.0 s = 28 bars of 4/4 at 80 BPM | The drone and a slow tanpura (every 1.5 s) carry it. The flute says seven short things, softer and breathier than in the menu, with two to four bars of room between them. The madal is a slow pulse: a soft ringing "tin" on beat 1, a closed tick on beat 3, a soft bass stroke every other bar. The game plays this one 6 dB down. |

Both loops repeat without a seam (see below). They do not start from silence: the first samples
carry what rings over from the last bar, so always fade a loop in (the audio service does).

## How it is checked

Nobody listens during a build, so the run ends by measuring every file as it sits on disk and
exits non-zero if a check fails.

| Column | Meaning | Check |
| --- | --- | --- |
| `peak dB` | Highest sample | Effects: -3.0 ± 0.1 dBFS. Music: under -1 dBFS. |
| `LUFS` | Integrated loudness (files of 0.4 s or more) | Music: -14 ± 0.5. |
| `loud` | The loudest 100 ms, on the LUFS scale | How loud a short cue feels; the library's volumes are set from it. |
| `dc dB` | Mean sample value | Below -50 dBFS (effects), -60 dBFS (music). |
| `clip` | Samples at full scale | 0. |
| `centroid`, `energy %` | Where the sound sits: spectral centre, and the share of energy below 150 Hz, 150-500, 500-2k, 2k-6k, above 6 kHz | For judging warmth and harshness. |
| `phone` | dB of energy lost through a phone speaker (modelled as a 4th-order high-pass at 400 Hz) | For judging what survives on a phone. |
| `start`, `end` | First and last sample | Effects start within 0.01 of zero and end on zero. |
| `sec` | Length | Effects are a whole number of 64-sample blocks (see below). |
| `seam`, `kink` | The jump and the change of slope from the last sample to the first, over the file's own 99.9th percentile | Music: both at most 1, i.e. the loop point is an ordinary sample. |

`--pictures` is the closest thing to listening: open a PNG and read the waveform on top and the
spectrogram below (50 Hz to 16 kHz, log scale, dotted lines at 100 Hz, 1 kHz and 10 kHz).

Two things were also measured inside the Editor, on what Unity actually decodes:

- **ADPCM resamples a clip whose length is not a multiple of 64 samples.** It does not pad it: a
  3528-sample tick came back 3584 samples long and 27 cents flat. That is why every effect here is
  a whole number of 64-sample blocks (the lengths in the tables are rounded, by at most 0.7 ms).
  With that, the decoded clips line up with the sources sample for sample, 34-46 dB above the
  codec's noise on the tonal cues and 23-29 dB on the noise-like card sounds.
- **The loops stay seamless through Vorbis.** Decoded, both keep their exact sample count, with no
  offset, no extra error in the first or last milliseconds, and a jump across the loop point
  smaller than an ordinary step between two samples.

## How it is made

| File | Holds |
| --- | --- |
| `Dsp.cs` | Random numbers, biquad and state-variable filters, the room reverb (Freeverb topology), fades |
| `Voices.cs` | The instruments: struck bars and blocks (modal), paper friction and impacts, the bansuri, the madal, the tanpura |
| `Scale.cs` | Sargam to Hz |
| `Sfx.cs` | One method per sound effect |
| `Music.cs` | The two scores (flute in sargam, madal as 16-step bars), the drone, the mix and the master |
| `Analysis.cs` | Loudness (BS.1770), the spectrum figures, the pictures |
| `Wav.cs`, `Program.cs` | File format, the command line, the checks |

- **Cards** are paper: sparse micro-clicks (what makes paper crackle rather than hiss) over a hiss
  floor, shaped by moving band-pass filters, plus soft impacts that are damped within a cycle or two.
- **Bansuri**: a phrase is rendered as one breath. One oscillator follows a pitch curve with finger
  changes, glides (meend), grace notes (kan), a scoop into the first note and vibrato that arrives
  late on held notes; its tone is mostly fundamental; two breath noises (one on the pitch, one of
  plain air) and a chiff at each attack do the rest.
- **Madal**: the treble head rings on G4 with the near-harmonic modes of a loaded skin (Ta on the
  rim, Tin open, Ti damped); the bass head is a falling tone two octaves lower that can bend upward.
- **Tanpura**: harmonics whose band of emphasis sweeps upward after each pluck (the jawari buzz).
- **Seamless loops**: the loop is a whole number of bars; every note is mixed into a circular
  buffer, so whatever rings past the last bar lands on the first; the drone's partials complete
  whole cycles per loop; the reverb, filters and limiter run twice round and keep the second lap.
- **Mix**: stems are balanced by measured loudness, the room is set relative to the dry mix, and
  the master is normalised to -14 LUFS under a look-ahead limiter.

To change a tune, edit the score strings at the top of `Music.cs`: one bar between `|`, durations in
eighth notes, `-n` a rest (a breath), `~` a glide into the note, `^` a grace note from above; the
generator refuses a bar that does not add up to eight.

## Replacing a sound with a real one

Keep the file name and folder, deliver 44.1 kHz WAV, effects peaking near -3 dBFS and music near
-14 LUFS, then run **Callbreak > Build > Audio Library**. More takes of a cue are picked up by
number (`SFX_CardPlay_05.wav`). Trim or pad an effect shorter than a second to a multiple of 64
samples, or Unity will retune it slightly. A music loop must be an exact number of bars with its
tail folded onto its head. Once a file is no longer generated, remove its line from `Sfx.cs` or
`Music.cs`, or the next run of this tool will write the synthetic version back over it.

## What is synthetic about it

These are models, not recordings, and nobody has auditioned them by ear yet: they were shaped from
measurements and spectrograms. The flute has no player, so its phrasing is even and its breath is
tidy. The madal and tanpura are recognisable but plainer than the instruments. The card sounds are
shaped noise; they read as cards, not as a particular deck on a particular table. There is no
sarangi, which the GDD names for the menu theme.

Worth replacing first, in this order: the four card sounds (a phone recording of a real deck on
felt will beat them), the menu theme's flute (a played bansuri), then the victory sting. The wood,
marimba and chime cues and the interface ticks are the kind of sound synthesis does well.
