using static AudioGen.Dsp;

namespace AudioGen;

/// <summary>One mode of a struck resonator: frequency ratio to f0, relative amplitude, decay time constant (s).</summary>
public readonly record struct Mode(double Ratio, double Amp, double Tau);

public enum Band { LowPass, BandPass, HighPass }

public enum Stroke { Ta, Tin, Ti, Ghe, Ka }

/// <summary>A note inside a bansuri phrase. Glide = meend into this note; KanFreq = grace note flicked in from above.</summary>
public sealed record FluteNote(double Freq, double Dur, double Vel = 0.8, bool Glide = false, double KanFreq = 0, double FromFreq = 0);

/// <summary>The instruments. Every voice writes into a plain mono buffer; placement and mixing happen later.</summary>
public static class Voices
{
    // ------------------------------------------------------------------ struck and plucked resonators

    /// <summary>
    /// Adds a struck modal resonator (decaying sines at f0 × ratio). The mallet is a soft low-pass on the
    /// modes (<paramref name="malletHz"/>) plus a raised-cosine attack, so nothing starts with a click.
    /// </summary>
    public static void Strike(double[] buf, double at, double f0, Mode[] modes, double gain, double malletHz,
                              double attack, double detuneCents = 0)
    {
        int s0 = Samples(at);
        int attN = Math.Max(1, Samples(attack));
        foreach (var m in modes)
        {
            double f = f0 * m.Ratio * Cents(detuneCents);
            if (f >= 0.42 * Fs) continue;
            double a = gain * m.Amp / Math.Sqrt(1 + Math.Pow(f / malletHz, 4));
            int len = Math.Min(buf.Length - s0, (int)(m.Tau * 9.2 * Fs)); // until -80 dB
            double w = TwoPi * f / Fs, c = Math.Cos(w), s = Math.Sin(w), re = 1, im = 0;
            double dec = Math.Exp(-1 / (m.Tau * Fs)), env = 1;
            for (int i = 0; i < len; i++)
            {
                double att = i < attN ? 0.5 - 0.5 * Math.Cos(Math.PI * i / attN) : 1;
                buf[s0 + i] += a * env * att * im;
                double nre = re * c - im * s;
                im = re * s + im * c;
                re = nre;
                env *= dec;
            }
        }
    }

    public static Mode[] Retime(Mode[] modes, double tauScale) =>
        modes.Select(m => m with { Tau = m.Tau * tauScale }).ToArray();

    /// <summary>A damped sine whose pitch falls from f0·from to f0 (time constant <paramref name="glide"/>): taps and thuds.</summary>
    public static void Blip(double[] buf, double at, double f0, double from, double glide, double tau, double gain,
                            double attack, double drift = 0, double driftTime = 0.05)
    {
        int s0 = Samples(at);
        int len = Math.Min(buf.Length - s0, (int)(tau * 9.2 * Fs));
        double ph = 0;
        for (int i = 0; i < len; i++)
        {
            double t = i / Fs;
            double f = f0 * (1 + (from - 1) * Math.Exp(-t / glide)) * (1 + drift * Smoothstep(t / driftTime));
            ph += TwoPi * f / Fs;
            buf[s0 + i] += gain * Math.Exp(-t / tau) * Ramp(t, attack) * Math.Sin(ph);
        }
    }

    /// <summary>
    /// Exponentially decaying noise burst through a second-order filter. The level is compensated for the
    /// filter's bandwidth, so <paramref name="gain"/> reads roughly as a peak amplitude whatever the band.
    /// </summary>
    public static void Burst(double[] buf, double at, double tau, double gain, Rng r, double f, double q,
                             Band band = Band.BandPass, double attack = 0.0003)
    {
        int s0 = Samples(at);
        int len = Math.Min(buf.Length - s0, (int)(tau * 9.2 * Fs) + 64);
        double nyq = Fs / 2;
        var (flt, bw) = band switch
        {
            Band.LowPass => (Biquad.LowPass(f, q), f),
            Band.HighPass => (Biquad.HighPass(f, q), nyq - f),
            _ => (Biquad.BandPass(f, q), f / q),
        };
        double g = gain * Math.Sqrt(nyq / Math.Clamp(bw, 50, nyq));
        for (int i = 0; i < len; i++)
        {
            double t = i / Fs;
            buf[s0 + i] += g * flt.Run(r.Noise() * Math.Exp(-t / tau) * Ramp(t, attack));
        }
    }

    // ------------------------------------------------------------------ cards

    /// <summary>
    /// Paper friction: Poisson micro-clicks (the crackle that makes paper sound like paper rather than
    /// steam) plus a smooth hiss floor, both following env(t), band-limited to lo..hi Hz.
    /// </summary>
    public static double[] Paper(Rng r, double seconds, Func<double, double> env, double grainsPerSec, double hiss,
                                 double lo, double hi)
    {
        int n = Samples(seconds);
        var x = new double[n];
        for (int i = 0; i < n; i++)
        {
            double e = env(i / Fs);
            if (e <= 0) continue;
            x[i] += hiss * e * r.Noise();
            if (r.Next() < grainsPerSec * e / Fs)
            {
                double a = e * (0.2 + 0.8 * r.Next() * r.Next());
                int len = 6 + (int)(r.Next() * 34);
                for (int j = 0; j < len && i + j < n; j++) x[i + j] += a * r.Noise() * Math.Exp(-3.0 * j / len);
            }
        }
        var h1 = Biquad.HighPass(lo); var h2 = Biquad.HighPass(lo);
        var l1 = Biquad.LowPass(hi); var l2 = Biquad.LowPass(hi);
        double comp = Math.Sqrt(Fs / 2 / Math.Max(200, hi - lo));
        for (int i = 0; i < n; i++) x[i] = comp * l2.Run(l1.Run(h2.Run(h1.Run(x[i]))));
        return x;
    }

    /// <summary>
    /// A card or the deck meeting felt over wood: mostly a soft puff of low-mid air, with a body tone and a
    /// table-top knock that are damped within a cycle or two, so it pats rather than rings.
    /// </summary>
    public static void Pat(double[] buf, double at, double f0, double tau, double gain, Rng r, double airHz = 1600)
    {
        Burst(buf, at, tau * 0.7, gain, r, airHz, 0.7, Band.LowPass, 0.0005);
        Blip(buf, at, f0, 1.6, 0.005, tau * 0.8, gain * 0.35, 0.0008);
        Blip(buf, at + 0.0004, f0 * 2.9, 1.15, 0.004, tau * 0.7, gain * 0.25, 0.0006);
    }

    // ------------------------------------------------------------------ bells and bars

    /// <summary>Soft vibraphone-like bar (tuned 1 : 4 : 10 with a faint octave): warm chimes.</summary>
    public static readonly Mode[] Vibes =
    {
        new(1.00, 1.00, 0.50), new(3.98, 0.12, 0.12), new(2.00, 0.05, 0.30), new(9.85, 0.02, 0.03),
    };

    /// <summary>Rosewood marimba bar with a touch of bell octave.</summary>
    public static readonly Mode[] Marimba =
    {
        new(1.00, 1.00, 0.32), new(3.93, 0.14, 0.055), new(9.30, 0.03, 0.015), new(2.00, 0.05, 0.10),
    };

    /// <summary>Hollow wood block: free-bar modes (1 : 2.7 : 5) plus the cavity's lower resonance.</summary>
    public static readonly Mode[] WoodBlock =
    {
        new(1.00, 1.00, 0.055), new(2.71, 0.30, 0.020), new(4.95, 0.10, 0.008), new(0.52, 0.22, 0.030),
    };

    /// <summary>Felt-muted tongue drum: harmonic, rounded, short.</summary>
    public static readonly Mode[] TongueDrum =
    {
        new(1.00, 1.00, 0.22), new(2.00, 0.42, 0.11), new(3.00, 0.14, 0.06), new(4.10, 0.04, 0.02),
    };

    // ------------------------------------------------------------------ bansuri

    /// <summary>
    /// A bansuri-like phrase rendered as one breath: a continuous oscillator follows a pitch curve (finger
    /// changes, meend glides, kan grace notes, a scoop into the first note, delayed vibrato on held notes)
    /// under a breath envelope (soft attack, articulation dips, release). Tone is mostly fundamental with a
    /// little 2nd/3rd harmonic (more when low and loud) plus two breath noises: a narrow band that tracks the
    /// pitch (the airy core) and a wide air band that also carries the attack chiff.
    /// </summary>
    public static double[] Flute(IReadOnlyList<FluteNote> notes, Rng r, double release = 0.32, double breathiness = 1)
    {
        int count = notes.Count;
        var start = new double[count + 1];
        for (int k = 0; k < count; k++) start[k + 1] = start[k] + notes[k].Dur;
        double total = start[count];
        int n = Samples(total + release);
        var y = new double[n];

        var core = new Svf(notes[0].Freq, 7);
        var air = new Svf(2800, 0.6);
        var lp = Biquad.LowPass(7000, 0.6);
        var hp = Biquad.HighPass(150, 0.6);

        const double centsToLog = 0.000577622650466621; // ln 2 / 1200
        double phase = 0, vib = r.Next() * TwoPi, vibRate = r.Range(4.9, 5.4), amp = 0;
        double upFirst = Coef(0.04), up = Coef(0.018), down = Coef(0.035), rel = Coef(0.075);
        // Breath is never perfectly steady: slow random wander of pitch (a few cents) and of level (a few %).
        double wander = 0, flutter = 0, wanderC = Coef(0.05), flutterC = Coef(0.02);
        int cur = 0;
        double noteCents = r.Range(-4, 4);
        for (int i = 0; i < n; i++)
        {
            double t = i / Fs;
            while (cur + 1 < count && t >= start[cur + 1]) { cur++; noteCents = r.Range(-4, 4); }
            var nt = notes[cur];
            double tn = t - start[cur];
            bool released = t >= total;

            // Pitch (log-frequency).
            double target = Math.Log(nt.Freq) + noteCents * centsToLog;
            double logF = target;
            if (cur > 0)
            {
                double from = Math.Log(notes[cur - 1].Freq);
                double tt = nt.Glide ? Math.Min(0.24, 0.5 * nt.Dur) : 0.022;
                if (tn < tt) logF = from + (target - from) * Smoothstep(tn / tt);
            }
            else if (nt.Glide && nt.FromFreq > 0)
            {
                double from = Math.Log(nt.FromFreq), tt = Math.Min(0.3, 0.5 * nt.Dur);
                if (tn < tt) logF = from + (target - from) * Smoothstep(tn / tt);
            }
            if (nt.KanFreq > 0)
            {
                const double kanLen = 0.045, kanSlide = 0.018;
                double kan = Math.Log(nt.KanFreq);
                if (tn < kanLen) logF = kan;
                else if (tn < kanLen + kanSlide) logF = kan + (target - kan) * Smoothstep((tn - kanLen) / kanSlide);
            }
            if (cur == 0 && !nt.Glide) logF -= (1 - Smoothstep(tn / 0.08)) * 40 * centsToLog;
            double vibAmt = nt.Dur >= 0.45 ? Smoothstep((tn - 0.22) / 0.45) : 0;
            vib += TwoPi * vibRate / Fs;
            wander += (r.Noise() - wander) * wanderC;
            flutter += (r.Noise() - flutter) * flutterC;
            logF += (vibAmt * 15 * Math.Sin(vib) + 180 * wander) * centsToLog;
            double f = Math.Exp(logF);

            // Breath (amplitude).
            double want = released ? 0 : nt.Vel * (1 + 0.07 * Math.Sin(Math.PI * Math.Min(1, tn / nt.Dur)));
            amp += (want - amp) * (want > amp ? (cur == 0 ? upFirst : up) : released ? rel : down);
            double dip = cur > 0 && !nt.Glide ? 1 - 0.3 * Math.Exp(-Math.Pow((tn - 0.008) / 0.014, 2)) : 1;
            double a = amp * dip * (1 + 0.04 * vibAmt * Math.Sin(vib - 0.7) + 1.6 * flutter);

            // Tone.
            phase += TwoPi * f / Fs;
            if (phase > TwoPi) phase -= TwoPi;
            double br = Math.Sqrt(Math.Max(a, 0)) * Math.Clamp(440 / f, 0.5, 1.5);
            double tone = Math.Sin(phase) + br * (0.17 * Math.Sin(2 * phase + 0.4) + 0.06 * Math.Sin(3 * phase + 1.3)
                                                  + 0.022 * Math.Sin(4 * phase + 2.1) + 0.011 * Math.Sin(5 * phase + 0.7)
                                                  + 0.006 * Math.Sin(6 * phase + 1.9));

            // Breath noise and chiff.
            if ((i & 15) == 0) core.Set(f, 7);
            core.Run(r.Noise());
            air.Run(r.Noise());
            double chiff = cur == 0 ? Math.Exp(-tn / 0.028) * Ramp(tn, 0.003) : nt.Glide ? 0 : 0.3 * Math.Exp(-tn / 0.012);
            double s = a * tone + breathiness * (a * 2.2 * core.Band + (0.16 * a + 0.9 * chiff * nt.Vel) * air.Band);
            y[i] = lp.Run(hp.Run(s));
        }
        FadeOut(y, 0.04);
        return y;
    }

    // ------------------------------------------------------------------ madal

    /// <summary>
    /// One madal stroke, tuned to the scale: the treble head rings on Sa, the bass head two octaves below.
    /// Treble head: the near-harmonic modes of a loaded skin plus a finger click. Ta (rim) leans on the 2nd
    /// and 3rd modes, Tin (open) on the fundamental, Ti is the same skin damped. Bass head (Ghe open, Ka
    /// closed slap): a falling-pitch tone with an optional upward palm bend (gamak) and a soft low thump.
    /// </summary>
    public static double[] Madal(Stroke s, double vel, Rng r, double bend = 0)
    {
        double treble = Scale.Sa, bass = Scale.Sa / 4;
        double v = Math.Clamp(vel, 0.05, 1.2);
        var y = new double[Samples(s switch { Stroke.Tin => 1.1, Stroke.Ghe => 0.9, Stroke.Ta => 0.7, _ => 0.3 })];
        if (s is Stroke.Ta or Stroke.Tin or Stroke.Ti)
        {
            double tau = s switch { Stroke.Tin => 0.40, Stroke.Ta => 0.20, _ => 0.03 };
            double a1 = s == Stroke.Tin ? 1.00 : 0.40, a2 = s == Stroke.Tin ? 0.45 : 1.00, a3 = s == Stroke.Tin ? 0.20 : 0.55;
            var modes = new[]
            {
                new Mode(1.00, a1, tau), new Mode(2.00, a2, tau * 0.6), new Mode(3.00, a3, tau * 0.4),
                new Mode(4.00, 0.14, tau * 0.25), new Mode(1.52, 0.16, tau * 0.18), new Mode(2.27, 0.10, tau * 0.12),
            };
            Strike(y, 0, treble, modes, 0.5 * v, 2000 + 3000 * v, 0.0006, r.Range(-5, 5));
            double click = s switch { Stroke.Ti => 0.45, Stroke.Ta => 0.32, _ => 0.18 };
            Burst(y, 0, s == Stroke.Ti ? 0.005 : 0.0025, click * v * v, r, 3000, 0.9);
        }
        else
        {
            double tau = s == Stroke.Ghe ? 0.30 : 0.05;
            int len = Math.Min(y.Length, (int)(tau * 9.2 * Fs));
            double ph = 0;
            for (int i = 0; i < len; i++)
            {
                double t = i / Fs;
                double f = bass * (1 + 0.45 * Math.Exp(-t / 0.014)) * (1 + bend * Smoothstep((t - 0.03) / 0.16));
                ph += TwoPi * f / Fs;
                y[i] += 0.55 * v * Math.Exp(-t / tau) * Ramp(t, 0.0015)
                        * (Math.Sin(ph) + 0.50 * Math.Exp(-t / 0.10) * Math.Sin(1.59 * ph) + 0.25 * Math.Exp(-t / 0.06) * Math.Sin(2.14 * ph));
            }
            Burst(y, 0, s == Stroke.Ghe ? 0.007 : 0.012, (s == Stroke.Ghe ? 0.30 : 0.5) * v, r, 800, 0.7, Band.LowPass, 0.001);
        }
        FadeOut(y, 0.02);
        return y;
    }

    // ------------------------------------------------------------------ tanpura

    /// <summary>
    /// Tanpura-like pluck: additive harmonics whose "jawari" band of emphasis sweeps up the harmonic series
    /// over the first seconds after the pluck, giving the drone its slow shimmering buzz. As on the real
    /// instrument, most of the sound is in the harmonics, not the fundamental.
    /// </summary>
    public static double[] Tanpura(double f0, double vel, Rng r, double seconds = 4.5)
    {
        int n = Samples(seconds);
        var y = new double[n];
        int harmonics = Math.Min(40, (int)(4500 / f0));
        const int block = 32;
        for (int h = 1; h <= harmonics; h++)
        {
            double fh = f0 * h * Cents(r.Range(-0.4, 0.4));
            double baseAmp = 1 / Math.Pow(h, 0.8), tau = 2.4 / (1 + 0.07 * h);
            double w = TwoPi * fh / Fs, c = Math.Cos(w), s = Math.Sin(w);
            double ph = r.Next() * TwoPi, re = Math.Cos(ph), im = Math.Sin(ph);
            int hh = h;
            double Env(double t)
            {
                double centre = 2 + 16 * (1 - Math.Exp(-t / 1.1));
                double jaw = 1 + 3.0 * Math.Exp(-Math.Pow((hh - centre) / 3.5, 2)) * (1 - Math.Exp(-t / 0.05));
                return baseAmp * jaw * Math.Exp(-t / tau) * Ramp(t, 0.005);
            }
            double e0 = Env(0);
            for (int b = 0; b < n; b += block)
            {
                double e1 = Env((b + block) / Fs);
                int m = Math.Min(block, n - b);
                for (int j = 0; j < m; j++)
                {
                    y[b + j] += (e0 + (e1 - e0) * j / block) * im;
                    double nre = re * c - im * s;
                    im = re * s + im * c;
                    re = nre;
                }
                e0 = e1;
            }
        }
        var lp = Biquad.LowPass(4500, 0.6);
        var hp = Biquad.HighPass(130);
        for (int i = 0; i < n; i++) y[i] = 0.25 * vel * lp.Run(hp.Run(y[i]));
        FadeOut(y, 0.4);
        return y;
    }
}
