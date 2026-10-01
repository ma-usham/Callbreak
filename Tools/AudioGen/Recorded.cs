using System.Text.Json;
using static AudioGen.Dsp;

namespace AudioGen;

/// <summary>
/// Cues made from generated sources instead of synthesis: the sound effects made with ElevenLabs and the
/// two music loops made with Gemini. ArtSource/Audio/Sources/sources.json names, for each cue it covers,
/// the source file and how to cut it, and keeps the prompts. A source replaces the synthesized cue of
/// the same name and goes through the same checks:
/// <list type="bullet">
/// <item>An effect is folded to mono and resampled to 44.1 kHz, starts 3 ms before its first sound and
/// stops after its last (at most <c>max</c> seconds), optionally has its transient rounded (<c>drive</c>,
/// as the synthesized card sounds), fades in over 1 ms and out to an exact zero, is peak-normalised to -3 dBFS and padded with silence to whole ADPCM blocks.</item>
/// <item>A music track becomes a loop. With <c>loop</c> [start, end] in seconds (whole bars at the
/// track's tempo, found by Tools/AudioGen's README method) those are the loop points; otherwise the
/// loop is cut from its steady part (<c>from</c>..<c>to</c>): it starts on a strong beat and ends where
/// the rhythm of the last seconds best matches the rhythm leading into the start. The end is then
/// aligned to the sample by waveform correlation (at most 10 ms), and the last <c>crossfade</c> seconds
/// fade into the music that comes just before the start, so the last sample flows into the first like
/// any other. Then a 30 Hz high-pass and -14 LUFS with peaks under -1 dBFS, both run round the loop
/// (as the synthesized loops are).</item>
/// </list>
/// </summary>
public static class Recorded
{
    const double OnsetHop = 0.01;          // the rhythm analysis works in 10 ms frames
    const double MatchWindow = 3.0;        // seconds of rhythm compared before the loop start and the loop end
    const double EndSearch = 6.0;          // the loop end is looked for in the last seconds of the steady part

    sealed record Effect(string Source, double Max, double FadeOut, double Drive);

    sealed record Track(string Source, double From, double To, double Crossfade, double[] Loop);

    /// <summary>The jobs for every cue the manifest covers, by cue name; empty when there is no manifest.</summary>
    public static Dictionary<string, Job> Load(string root)
    {
        var jobs = new Dictionary<string, Job>();
        string manifest = Path.Combine(root, "ArtSource", "Audio", "Sources", "sources.json");
        if (!File.Exists(manifest)) return jobs;

        string dir = Path.GetDirectoryName(manifest);
        using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
        foreach (var e in doc.RootElement.GetProperty("sfx").EnumerateObject())
        {
            var effect = new Effect(Path.Combine(dir, e.Value.GetProperty("source").GetString()),
                                    e.Value.GetProperty("max").GetDouble(), e.Value.GetProperty("fadeOut").GetDouble(),
                                    e.Value.TryGetProperty("drive", out JsonElement drive) ? drive.GetDouble() : 0);
            jobs[e.Name] = new Job(e.Name, false, () => RenderEffect(effect));
        }
        foreach (var e in doc.RootElement.GetProperty("music").EnumerateObject())
        {
            double[] loop = e.Value.TryGetProperty("loop", out JsonElement points) ? points.EnumerateArray().Select(p => p.GetDouble()).ToArray() : null;
            var track = new Track(Path.Combine(dir, e.Value.GetProperty("source").GetString()), e.Value.GetProperty("from").GetDouble(),
                                  e.Value.GetProperty("to").GetDouble(), e.Value.GetProperty("crossfade").GetDouble(), loop);
            string name = e.Name;
            jobs[name] = new Job(name, true, () => RenderLoop(name, track));
        }
        return jobs;
    }

    static double[][] RenderEffect(Effect e)
    {
        var (ch, _, rate) = Wav.Read(e.Source);
        var x = new double[ch[0].Length];
        foreach (var c in ch) for (int i = 0; i < x.Length; i++) x[i] += c[i] / ch.Length;
        x = Resample(x, rate, Rate);
        Biquad.HighPass(40).Run(x);      // no DC, no sub-bass

        double peak = Peak(x);
        var level = Envelope(x, Samples(0.005));
        int first = Array.FindIndex(x, v => Math.Abs(v) > 0.05 * peak);
        int last = Array.FindLastIndex(level, v => v > 0.003 * peak);
        int start = Math.Max(0, first - Samples(0.003));
        int end = Math.Min(Math.Min(x.Length, last + Samples(0.01)), start + Samples(e.Max));

        var y = x[start..end];
        if (e.Drive > 0)
        {
            // As the synthesized card sounds do: the top of the transient rounded (tanh), so a short sound
            // carries more body at the same peak.
            double top = Peak(y);
            for (int i = 0; i < y.Length; i++) y[i] = Math.Tanh(e.Drive * y[i] / top);
        }
        FadeIn(y, 0.001);
        FadeOut(y, Math.Min(e.FadeOut, 0.4 * y.Length / Fs));    // ends on an exact zero
        Gain(Db(Sfx.PeakDb) / Peak(y), y);
        Array.Resize(ref y, (y.Length + Sfx.BlockSamples - 1) / Sfx.BlockSamples * Sfx.BlockSamples);
        return new[] { y };
    }

    static double[][] RenderLoop(string name, Track t)
    {
        var (ch, _, rate) = Wav.Read(t.Source);
        if (ch.Length == 1) ch = new[] { ch[0], (double[])ch[0].Clone() };
        if (rate != Rate) ch = ch.Select(c => Resample(c, rate, Rate)).ToArray();
        int n = ch[0].Length;
        var mono = new double[n];
        for (int i = 0; i < n; i++) mono[i] = 0.5 * (ch[0][i] + ch[1][i]);

        int hop = Samples(OnsetHop), fade = Samples(t.Crossfade), w = (int)Math.Round(MatchWindow / OnsetHop);
        var onset = Onsets(mono, hop);

        int a, b;
        double rhythm;
        if (t.Loop != null)
        {
            a = Samples(t.Loop[0]);
            b = Samples(t.Loop[1]);
            rhythm = Correlation(onset, a / hop - w, b / hop - w, w);
        }
        else
        {
            // The start: the strongest beat within a second of where the steady part, less the crossfade, begins.
            int a0 = (Samples(t.From) + fade) / hop;
            int aF = Enumerable.Range(a0, (int)Math.Round(1.0 / OnsetHop)).MaxBy(f => onset[f]);

            // The end: in the last seconds of the steady part, where the rhythm leading in matches the start's.
            int bLast = Samples(t.To) / hop, bFirst = bLast - (int)Math.Round(EndSearch / OnsetHop);
            rhythm = double.MinValue;
            int bF = bLast;
            for (int f = bFirst; f <= bLast; f++)
            {
                double r = Correlation(onset, aF - w, f - w, w);
                if (r > rhythm) { rhythm = r; bF = f; }
            }

            a = aF * hop;
            b = bF * hop;
        }

        // To the sample: the end shifted by up to one frame either way to line the waveforms up.
        int span = Samples(0.05);
        double wave = double.MinValue;
        int shift = 0;
        for (int d = -hop; d <= hop; d++)
        {
            double r = Correlation(mono, a - span, b + d - span, 2 * span);
            if (r > wave) { wave = r; shift = d; }
        }
        b += shift;
        int length = b - a;

        var y = new double[2][];
        for (int c = 0; c < 2; c++)
        {
            y[c] = ch[c][a..b];
            for (int j = 0; j < fade; j++)
            {
                double p = (j + 0.5) / fade * Math.PI / 2;
                y[c][length - fade + j] = ch[c][b - fade + j] * Math.Cos(p) + ch[c][a - fade + j] * Math.Sin(p);
            }
            y[c] = Circular(y[c], Biquad.HighPass(30).Run);
        }

        // Loudness to the target, with a look-ahead limiter round the loop if the peaks would pass the ceiling.
        for (int pass = 0; pass < 4; pass++)
        {
            Gain(Db(Music.TargetLufs - Analysis.Lufs(y)), y);
            if (Peak(y) <= Db(Music.CeilingDb - 0.1)) break;
            Limit(y, Db(Music.CeilingDb - 0.1));
        }

        Console.WriteLine($"          {name}: loop {a / Fs:0.00}-{b / Fs:0.00} s of {n / Fs:0.00} ({length / Fs:0.00} s), " +
                          $"rhythm match {rhythm:0.00}, waveform match {wave:0.00}, crossfade {t.Crossfade:0.0} s");
        return y;
    }

    /// <summary>
    /// Windowed-sinc resampling (Blackman window, 32 zero crossings a side, cutoff at 95% of the lower
    /// Nyquist): clean enough that nothing above the new Nyquist folds back into the audible band.
    /// </summary>
    public static double[] Resample(double[] x, int from, int to)
    {
        if (from == to) return (double[])x.Clone();
        double ratio = (double)to / from, cutoff = 0.95 * 0.5 * Math.Min(1.0, ratio);   // cycles per input sample
        int half = (int)Math.Ceiling(32 * Math.Max(1.0, 1.0 / ratio));
        var y = new double[(int)Math.Floor(x.Length * ratio)];
        for (int i = 0; i < y.Length; i++)
        {
            double at = i / ratio, sum = 0;
            int centre = (int)Math.Floor(at);
            for (int k = Math.Max(0, centre - half + 1); k <= Math.Min(x.Length - 1, centre + half); k++)
            {
                double d = at - k, u = 2 * cutoff * d;
                double sinc = Math.Abs(u) < 1e-9 ? 1 : Math.Sin(Math.PI * u) / (Math.PI * u);
                double z = d / half;
                double window = Math.Abs(z) >= 1 ? 0 : 0.42 + 0.5 * Math.Cos(Math.PI * z) + 0.08 * Math.Cos(2 * Math.PI * z);
                sum += x[k] * 2 * cutoff * sinc * window;
            }
            y[i] = sum;
        }
        return y;
    }

    /// <summary>Moving average of |x| over <paramref name="width"/> samples, centred.</summary>
    static double[] Envelope(double[] x, int width)
    {
        var prefix = new double[x.Length + 1];
        for (int i = 0; i < x.Length; i++) prefix[i + 1] = prefix[i] + Math.Abs(x[i]);
        var e = new double[x.Length];
        for (int i = 0; i < x.Length; i++)
        {
            int lo = Math.Max(0, i - width / 2), hi = Math.Min(x.Length, i + width / 2 + 1);
            e[i] = (prefix[hi] - prefix[lo]) / (hi - lo);
        }
        return e;
    }

    /// <summary>Onset strength per frame: the rise in log energy from one frame to the next (rises only).</summary>
    static double[] Onsets(double[] x, int hop)
    {
        int frames = x.Length / hop, size = 2 * hop;
        var energy = new double[frames];
        for (int f = 0; f < frames; f++)
        {
            double e = 0;
            for (int i = f * hop; i < Math.Min(x.Length, f * hop + size); i++) e += x[i] * x[i];
            energy[f] = Math.Log(e + 1e-9);
        }
        var onset = new double[frames];
        for (int f = 1; f < frames; f++) onset[f] = Math.Max(0, energy[f] - energy[f - 1]);
        return onset;
    }

    /// <summary>Normalised correlation of two equally long stretches of one signal.</summary>
    static double Correlation(double[] x, int p, int q, int length)
    {
        double xy = 0, xx = 0, yy = 0;
        for (int i = 0; i < length; i++)
        {
            double u = x[p + i], v = x[q + i];
            xy += u * v; xx += u * u; yy += v * v;
        }
        return xy / Math.Sqrt(Math.Max(xx * yy, 1e-30));
    }

    /// <summary>
    /// Look-ahead peak limiter (5 ms ahead, 80 ms release) on a loop: the gain needed in the coming 5 ms
    /// is reached at once and released slowly, run twice round the loop so the seam is like any sample.
    /// </summary>
    static void Limit(double[][] y, double ceiling)
    {
        int n = y[0].Length, ahead = Samples(0.005);
        var need = new double[n];
        for (int i = 0; i < n; i++)
        {
            double p = 0;
            foreach (var c in y) p = Math.Max(p, Math.Abs(c[i]));
            need[i] = p > ceiling ? ceiling / p : 1;
        }
        var lowest = new double[n];
        var window = new LinkedList<int>();      // indices of a rising run of minima over the next 5 ms
        for (int i = n + ahead - 1; i >= 0; i--)
        {
            int k = i % n;
            while (window.Count > 0 && need[window.Last.Value % n] >= need[k]) window.RemoveLast();
            window.AddLast(i);
            while (window.First.Value > i + ahead) window.RemoveFirst();
            if (i < n) lowest[i] = need[window.First.Value % n];
        }
        double release = Coef(0.08), gain = 1;
        var applied = new double[n];
        for (int lap = 0; lap < 2; lap++)
        {
            for (int i = 0; i < n; i++)
            {
                gain = lowest[i] < gain ? lowest[i] : gain + (lowest[i] - gain) * release;
                if (lap == 1) applied[i] = gain;
            }
        }
        foreach (var c in y) for (int i = 0; i < n; i++) c[i] *= applied[i];
    }
}
