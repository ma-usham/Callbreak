using System.Globalization;
using static AudioGen.Dsp;
using static AudioGen.Voices;

namespace AudioGen;

/// <summary>
/// The two music loops. Both are in G (Sa = G4, see <see cref="Scale"/>), in the Sa Re Ga Pa Dha pentatonic
/// with the phrasing of the hill tunes (Pahadi): melodies that dip to the Dha and Pa below Sa and come home.
///
/// How the loops are made seamless: the loop is a whole number of bars; every note, drum and pluck is
/// mixed into a circular buffer, so whatever rings past the last bar lands on the first; the drone's
/// partials and slow modulations complete whole cycles per loop; and every stateful stage (reverb,
/// filters, limiter) runs twice round the loop and keeps the second lap. The last sample therefore flows
/// into the first exactly as any sample flows into the next.
/// </summary>
public static class Music
{
    public const double TargetLufs = -14.0;
    public const double CeilingDb = -1.0;

    /// <summary>stemSink, when given, receives each part of a loop as it is balanced (name, stereo channels).</summary>
    public static IEnumerable<Job> All(Action<string, double[][]> stemSink = null)
    {
        yield return new Job("MUS_Menu_Theme", true, () => Menu(stemSink));
        yield return new Job("MUS_Table_Loop", true, () => Table(stemSink));
    }

    // ------------------------------------------------------------------ scores

    // Flute scores: one bar between bars "|", durations in eighth notes (8 per bar), "-n" = rest (a breath;
    // it ends the phrase), "~" = meend (glide into the note), "^" = kan (grace note from the note above).

    const string MenuScore = """
        -4 ~S4        | R2 G2 ~R3 -1    | S2 D.2 ~P.4     | D.2 S3 -3    |
        ^G3 P1 G2 R2  | S2 R1 G1 R3 -1  | G2 P2 D2 P2     | G6 -2        |
        ^G3 P1 G2 R2  | S2 D.2 ~P.3 -1  | D.1 S2 R1 G2 R2 | ~S6 -2       |
        P2 D1 S'1 D2 P2    | G2 P2 ~D3 -1    | ^S'3 R'1 S'2 D2 | P6 -2   |
        P2 D1 S'1 R'2 G'2  | ~R'4 S'2 D1 -1  | P2 G1 P1 D2 P2  | G6 -2   |
        ^G3 P1 G2 R2  | S2 R1 G1 R3 -1  | G2 P2 D2 S'2    | ~P6 -2       |
        ^G3 P1 G2 R2  | S2 D.2 ~P.3 -1  | D.1 S2 R1 G2 R2 | ~S8          |
        -4 G2 R2      | ~S6 -2          | -8              | -8
        """;

    const string TableScore = """
        -8            | -8              | ~S4 R2 G2       | ~R6 -2       |
        -8            | -8              | G2 P2 ~D4       | P6 -2        |
        D2 S'2 ~D4    | P2 G2 R3 -1     | ~G6 -2          | -8           |
        -8            | -8              | S2 D.2 ~P.4     | D.2 ~S4 -2   |
        ^G3 P1 G2 R2  | S2 R2 ~G3 -1    | R6 -2           | -8           |
        -8            | -8              | P.2 D.2 S2 R2   | ~S6 -2       |
        -4 G2 R2      | ~S6 -2          | -8              | -8
        """;

    // Madal bars, 16 steps each. Treble head: T/t = Ta, N/n = Tin (ringing), i = Ti (closed).
    // Bass head: G/g = Ghe, B/b = Ghe with an upward palm bend, k = Ka (closed). Capitals are accents.
    static readonly (string treble, string bass) Groove = ("T...t.i.t...N.t.", "G.........k.B...");
    static readonly (string treble, string bass) Fill = ("T...t.i.t.i.tTtN", "G.........k.G.g.");
    static readonly (string treble, string bass) Sparse = ("........n.......", "g...............");
    static readonly (string treble, string bass) SparseB = ("........n...t...", "g...............");

    // ------------------------------------------------------------------ menu theme

    /// <summary>
    /// 96 BPM, 32 bars of 4/4 = 80 s. An alaap-like opening (4 bars), the tune (A, A'), a higher middle
    /// part (B, 8 bars), the tune again with a final cadence, and a quiet tail that hands back to the
    /// opening. Bansuri over a tanpura cycle and a soft sustained drone, with a light kaharwa-style madal.
    /// </summary>
    static double[][] Menu(Action<string, double[][]> stemSink)
    {
        const int bars = 32;
        const double barSec = 2.5;
        int n = Samples(bars * barSec);
        var r = new Rng("MUS_Menu_Theme");

        var flute = new Stem(n);
        foreach (var p in ParseFlute(MenuScore, barSec / 8, 0.80, bars, r.Fork("score")))
            flute.Add(Flute(p.Notes, r.Fork("flute")), p.Start + r.Range(-0.008, 0.008), 1, 0.12);

        var madal = new Stem(n);
        for (int b = 0; b < bars; b++)
        {
            var pattern = b == 0 ? Sparse : b == 1 ? SparseB : b % 4 == 3 ? Fill : Groove;
            Drums(madal, r, b * barSec, barSec, pattern, 1.0);
        }

        // Tanpura: Pa, Sa, Sa, low Sa, evenly on beats 1 and 3; one cycle every two bars.
        var tanpura = new Stem(n);
        TanpuraCycles(tanpura, r, n, 2 * barSec, new[] { 0.0, 1.25, 2.5, 3.75 }, 0.9);

        var drone = new Stem(n);
        Drone(drone, r, new[] { (Scale.Hz("S.."), 0.7, 10), (Scale.Hz("P.."), 0.8, 12), (Scale.Hz("S."), 1.0, 16) });

        return Master("Menu", stemSink, n, -12, 0.84, 0.35,
            ("flute", flute, 0.0, 1.0), ("madal", madal, -8.5, 0.35), ("tanpura", tanpura, -10.0, 0.7), ("drone", drone, -12.0, 0.3));
    }

    // ------------------------------------------------------------------ table loop

    /// <summary>
    /// 80 BPM, 28 bars of 4/4 = 84 s. The drone carries it; the bansuri says something short every few
    /// bars and leaves room; the madal is a slow, quiet pulse. Made to sit under the card sounds.
    /// </summary>
    static double[][] Table(Action<string, double[][]> stemSink)
    {
        const int bars = 28;
        const double barSec = 3.0;
        int n = Samples(bars * barSec);
        var r = new Rng("MUS_Table_Loop");

        var flute = new Stem(n);
        foreach (var p in ParseFlute(TableScore, barSec / 8, 0.68, bars, r.Fork("score")))
            flute.Add(Flute(p.Notes, r.Fork("flute"), 0.4, 1.25), p.Start + r.Range(-0.010, 0.010), 1, 0.10);

        var madal = new Stem(n);
        for (int b = 0; b < bars; b++)
        {
            var pattern = (b % 4) switch
            {
                0 => ("n.......i.......", "g..............."),
                1 => ("n.......i.......", "................"),
                2 => ("n.......i...t...", "g..............."),
                _ => ("n...............", "................"),
            };
            Drums(madal, r, b * barSec, barSec, pattern, 0.8);
        }

        var tanpura = new Stem(n);
        TanpuraCycles(tanpura, r, n, 2 * barSec, new[] { 0.0, 1.5, 3.0, 4.5 }, 0.75);

        var drone = new Stem(n);
        Drone(drone, r, new[] { (Scale.Hz("S.."), 0.8, 7), (Scale.Hz("P.."), 0.8, 10), (Scale.Hz("S."), 1.0, 14) });

        return Master("Table", stemSink, n, -10, 0.86, 0.40,
            ("flute", flute, 0.0, 1.0), ("drone", drone, -6.0, 0.35), ("tanpura", tanpura, -9.0, 0.7), ("madal", madal, -13.0, 0.5));
    }

    // ------------------------------------------------------------------ parts

    /// <summary>A stereo stem the length of the loop; everything added to it wraps round the end.</summary>
    sealed class Stem
    {
        public readonly double[] L, R;
        public Stem(int n) { L = new double[n]; R = new double[n]; }

        public void Add(double[] mono, double atSeconds, double gain, double pan)
        {
            var (gl, gr) = Pan(pan);
            long at = (long)Math.Round(atSeconds * Fs);
            AddWrapped(L, mono, at, gain * gl);
            AddWrapped(R, mono, at, gain * gr);
        }
    }

    sealed record Phrase(double Start, List<FluteNote> Notes);

    /// <summary>Turns a score into phrases (one breath each) and checks every bar adds up to eight eighths.</summary>
    static List<Phrase> ParseFlute(string score, double eighth, double velocity, int expectedBars, Rng r)
    {
        var phrases = new List<Phrase>();
        List<FluteNote> cur = null;
        double pos = 0;
        var bars = score.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (bars.Length != expectedBars) throw new InvalidOperationException($"Score has {bars.Length} bars, expected {expectedBars}");
        for (int b = 0; b < bars.Length; b++)
        {
            double sum = 0;
            foreach (string tok in bars[b].Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int i = 0;
                bool glide = false, kan = false;
                for (; tok[i] == '~' || tok[i] == '^'; i++) { if (tok[i] == '~') glide = true; else kan = true; }
                if (tok[i] == '-')
                {
                    double rest = double.Parse(tok[(i + 1)..], CultureInfo.InvariantCulture);
                    cur = null; pos += rest; sum += rest;
                    continue;
                }
                char swara = tok[i++];
                int oct = 0;
                for (; i < tok.Length && (tok[i] == '.' || tok[i] == '\''); i++) oct += tok[i] == '\'' ? 1 : -1;
                double dur = double.Parse(tok[i..], CultureInfo.InvariantCulture);
                double freq = Scale.Hz(swara, oct);
                double vel = velocity * (1 + 0.06 * Math.Log2(freq / Scale.Sa)) * r.Range(0.95, 1.05);
                double kanFreq = 0, fromFreq = 0;
                if (kan) { var (s, o) = Scale.Step(swara, oct, +1); kanFreq = Scale.Hz(s, o); }
                if (cur == null)
                {
                    cur = new List<FluteNote>();
                    phrases.Add(new Phrase(pos * eighth, cur));
                    if (glide) { var (s, o) = Scale.Step(swara, oct, -1); fromFreq = Scale.Hz(s, o); }
                }
                cur.Add(new FluteNote(freq, dur * eighth, vel, glide, kanFreq, fromFreq));
                pos += dur; sum += dur;
            }
            if (Math.Abs(sum - 8) > 1e-9) throw new InvalidOperationException($"Flute bar {b + 1} adds up to {sum} eighths, expected 8");
        }
        foreach (var p in phrases) p.Notes[^1] = p.Notes[^1] with { Vel = p.Notes[^1].Vel * 0.92 };
        return phrases;
    }

    /// <summary>One bar of madal from a 16-step pattern, with a little human looseness in time and touch.</summary>
    static void Drums(Stem stem, Rng r, double barStart, double barSec, (string treble, string bass) pattern, double level)
    {
        double step = barSec / 16;
        for (int s = 0; s < 16; s++)
        {
            foreach (char c in new[] { pattern.treble[s], pattern.bass[s] })
            {
                if (c == '.') continue;
                var (stroke, vel, bend) = c switch
                {
                    'T' => (Stroke.Ta, 0.85, 0.0), 't' => (Stroke.Ta, 0.50, 0.0),
                    'N' => (Stroke.Tin, 0.85, 0.0), 'n' => (Stroke.Tin, 0.50, 0.0),
                    'I' => (Stroke.Ti, 0.70, 0.0), 'i' => (Stroke.Ti, 0.45, 0.0),
                    'G' => (Stroke.Ghe, 0.85, 0.0), 'g' => (Stroke.Ghe, 0.50, 0.0),
                    'B' => (Stroke.Ghe, 0.80, 0.125), 'b' => (Stroke.Ghe, 0.50, 0.125),   // a whole tone up, Sa to Re
                    'K' => (Stroke.Ka, 0.70, 0.0), 'k' => (Stroke.Ka, 0.45, 0.0),
                    _ => throw new InvalidOperationException($"Unknown madal stroke '{c}'"),
                };
                bool bass = stroke is Stroke.Ghe or Stroke.Ka;
                double at = barStart + s * step + r.Range(-0.004, 0.004);
                stem.Add(Madal(stroke, vel * r.Range(0.92, 1.08), r, bend), at, level, bass ? 0.05 : 0.25);
            }
        }
    }

    /// <summary>The tanpura's four strings (Pa, Sa, Sa, low Sa) plucked at the given offsets, every cycle.</summary>
    static void TanpuraCycles(Stem stem, Rng r, int n, double cycleSec, double[] offsets, double level)
    {
        double[] strings = { Scale.Hz("P.."), Scale.Hz("S."), Scale.Hz("S."), Scale.Hz("S..") };
        double[] pans = { -0.35, -0.20, -0.05, -0.20 };
        // Three takes of each string, rotated, so no two cycles in a row are the same recording.
        var takes = new double[strings.Length][][];
        for (int s = 0; s < strings.Length; s++)
        {
            takes[s] = new double[3][];
            for (int v = 0; v < 3; v++) takes[s][v] = Tanpura(strings[s] * Cents(s == 2 ? 3 : 0), 1, r);
        }
        int cycles = (int)Math.Round(n / Fs / cycleSec);
        for (int c = 0; c < cycles; c++)
            for (int s = 0; s < strings.Length; s++)
                stem.Add(takes[s][(c + s) % 3], c * cycleSec + offsets[s] + r.Range(-0.012, 0.012), level * r.Range(0.85, 1.0), pans[s]);
    }

    /// <summary>
    /// A soft sustained bed of Sa and Pa. Every partial completes a whole number of cycles per loop (its
    /// frequency moves by at most 1/(2 x loop length) Hz), so the drone is exactly periodic. Each partial
    /// swells and fades at its own slow rate, so the colour never sits still. Width comes from a quiet
    /// side signal a few cycles per loop away (L = mid + side, R = mid - side), which cancels in mono:
    /// a phone's single speaker hears the same steady drone. Partials below about 400 Hz are held back,
    /// so the bed lives in its harmonics (a soft reed-organ colour that small speakers can play) rather
    /// than in bass they cannot.
    /// </summary>
    static void Drone(Stem stem, Rng r, (double hz, double gain, int breaths)[] voices)
    {
        int n = stem.L.Length;
        foreach (var (hz, gain, breaths) in voices)
        {
            long cycles = (long)Math.Round(hz * n / Fs);
            for (int h = 1; h <= 10; h++)
            {
                double fh = cycles * h * Fs / n;
                if (fh > 2000) break;
                double a = gain / Math.Pow(h, 1.1) * Math.Min(1, fh / 400) / (1 + Math.Pow(fh / 1400, 2));
                long mid = cycles * h, side = mid + 1 + h % 3, lfo = breaths + h - 1;
                double pm = r.Next() * TwoPi, ps = r.Next() * TwoPi, pl = r.Next() * TwoPi;
                for (int i = 0; i < n; i++)
                {
                    double swell = 1 + 0.25 * Math.Sin(TwoPi * (lfo * i % n) / n + pl);
                    double m = a * swell * Math.Sin(TwoPi * (mid * i % n) / n + pm);
                    double s = 0.4 * a * Math.Sin(TwoPi * (side * i % n) / n + ps);
                    stem.L[i] += m + s;
                    stem.R[i] += m - s;
                }
            }
        }
    }

    // ------------------------------------------------------------------ mix and master

    /// <summary>
    /// Balances the stems by measured loudness (each stem's LU relative to the lead), adds a shared room,
    /// then masters to the target loudness under a look-ahead limiter. "send" is how much of the stem
    /// feeds the room; roomLu is the room's loudness relative to the dry mix.
    /// </summary>
    static double[][] Master(string loop, Action<string, double[][]> stemSink, int n, double roomLu, double roomFeedback,
                             double roomDamp, params (string name, Stem stem, double lu, double send)[] stems)
    {
        var L = new double[n];
        var R = new double[n];
        var send = new double[n];
        foreach (var (name, stem, lu, amount) in stems)
        {
            Gain(Db(-20 + lu - Analysis.Lufs(stem.L, stem.R)), stem.L, stem.R);
            for (int i = 0; i < n; i++)
            {
                L[i] += stem.L[i];
                R[i] += stem.R[i];
                send[i] += amount * 0.5 * (stem.L[i] + stem.R[i]);
            }
            stemSink?.Invoke($"{loop}_{name}", new[] { stem.L, stem.R });
        }

        // The room: 20 ms pre-delay, lows kept out of it, two laps so its tail crosses the loop point.
        var room = new Reverb(1.0, roomFeedback, roomDamp);
        var hp = Biquad.HighPass(250);
        int pre = Samples(0.020);
        var wetL = new double[n];
        var wetR = new double[n];
        for (int lap = 0; lap < 2; lap++)
        {
            for (int i = 0; i < n; i++)
            {
                var (l, rr) = room.Run(hp.Run(send[(i - pre + n) % n]));
                wetL[i] = l;
                wetR[i] = rr;
            }
        }
        Gain(Db(Analysis.Lufs(L, R) + roomLu - Analysis.Lufs(wetL, wetR)), wetL, wetR);
        stemSink?.Invoke($"{loop}_room", new[] { wetL, wetR });
        for (int i = 0; i < n; i++)
        {
            L[i] += wetL[i];
            R[i] += wetR[i];
        }

        // Master: clear the sub-bass, open the top a little (soft sources are dull without it), then loudness,
        // then the limiter; a second pass trims what the limiter took.
        var hpL = Biquad.HighPass(30);
        var hpR = Biquad.HighPass(30);
        var airL = Biquad.HighShelf(3000, 0.7, 3.0);
        var airR = Biquad.HighShelf(3000, 0.7, 3.0);
        L = Circular(L, v => airL.Run(hpL.Run(v)));
        R = Circular(R, v => airR.Run(hpR.Run(v)));
        for (int pass = 0; pass < 2; pass++)
        {
            Gain(Db(TargetLufs - Analysis.Lufs(L, R)), L, R);
            Limit(L, R, Db(CeilingDb));
        }
        return new[] { L, R };
    }

    /// <summary>
    /// Look-ahead peak limiter for a periodic signal. The gain curve is worked out round the loop (indices
    /// wrap), so it treats the loop point like any other moment.
    /// </summary>
    static void Limit(double[] L, double[] R, double ceiling)
    {
        int n = L.Length, look = Samples(0.0015);
        var need = new double[n];
        Array.Fill(need, 1.0);
        bool any = false;
        for (int i = 0; i < n; i++)
        {
            double p = Math.Max(Math.Abs(L[i]), Math.Abs(R[i]));
            if (p <= ceiling) continue;
            any = true;
            double g = ceiling / p;
            for (int j = 0; j <= look; j++)
            {
                int k = (i - j + n) % n;
                if (g < need[k]) need[k] = g;
            }
        }
        if (!any) return;

        // Averaging the held minimum over the look-ahead window turns each step into a ramp that has fully
        // arrived by the peak; the release then lets the gain drift back up slowly.
        var ramp = new double[n];
        double sum = 0;
        for (int j = 0; j <= look; j++) sum += need[(n - j) % n];
        for (int i = 0; i < n; i++)
        {
            ramp[i] = sum / (look + 1);
            sum += need[(i + 1) % n] - need[(i - look + n) % n];
        }
        double rel = Coef(0.08), gain = 1;
        for (int lap = 0; lap < 2; lap++)
        {
            for (int i = 0; i < n; i++)
            {
                gain = Math.Min(ramp[i], gain + (1 - gain) * rel);
                if (lap == 1) { L[i] *= gain; R[i] *= gain; }
            }
        }
    }
}
