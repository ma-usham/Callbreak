namespace AudioGen;

/// <summary>Constants and small helpers shared by every voice. Everything runs at 44.1 kHz in doubles.</summary>
public static class Dsp
{
    public const int Rate = 44100;
    public const double Fs = Rate;
    public const double TwoPi = 2 * Math.PI;

    public static int Samples(double seconds) => (int)Math.Round(seconds * Fs);
    public static double Db(double db) => Math.Pow(10, db / 20);
    public static double ToDb(double gain) => 20 * Math.Log10(Math.Max(Math.Abs(gain), 1e-12));
    public static double Cents(double cents) => Math.Pow(2, cents / 1200);
    public static double Smoothstep(double x) => x <= 0 ? 0 : x >= 1 ? 1 : x * x * (3 - 2 * x);

    /// <summary>0 → 1 raised-cosine ramp over <paramref name="dur"/> seconds.</summary>
    public static double Ramp(double t, double dur) =>
        t >= dur ? 1 : t <= 0 ? 0 : 0.5 - 0.5 * Math.Cos(Math.PI * t / dur);

    /// <summary>Coefficient for a one-pole smoother with time constant <paramref name="tau"/> seconds.</summary>
    public static double Coef(double tau) => 1 - Math.Exp(-1 / (tau * Fs));

    /// <summary>Equal-power pan: -1 = left, 0 = centre, +1 = right.</summary>
    public static (double l, double r) Pan(double p)
    {
        double a = (Math.Clamp(p, -1, 1) + 1) * Math.PI / 4;
        return (Math.Cos(a), Math.Sin(a));
    }

    public static void Add(double[] dst, double[] src, int at, double gain = 1)
    {
        for (int i = Math.Max(0, -at); i < src.Length && at + i < dst.Length; i++) dst[at + i] += src[i] * gain;
    }

    /// <summary>Mixes <paramref name="src"/> into a loop buffer; anything past the end wraps onto the head.</summary>
    public static void AddWrapped(double[] dst, double[] src, long at, double gain = 1)
    {
        int n = dst.Length;
        long s = ((at % n) + n) % n;
        for (int i = 0; i < src.Length; i++) dst[(int)((s + i) % n)] += src[i] * gain;
    }

    public static void FadeIn(double[] x, double seconds)
    {
        int m = Math.Min(x.Length, Math.Max(1, Samples(seconds)));
        for (int i = 0; i < m; i++) x[i] *= 0.5 - 0.5 * Math.Cos(Math.PI * i / m);
    }

    /// <summary>Raised-cosine fade that reaches exactly zero on the last sample.</summary>
    public static void FadeOut(double[] x, double seconds)
    {
        int m = Math.Min(x.Length, Math.Max(2, Samples(seconds)));
        int s = x.Length - m;
        for (int i = 0; i < m; i++) x[s + i] *= 0.5 + 0.5 * Math.Cos(Math.PI * i / (m - 1));
    }

    public static double Peak(params double[][] chans)
    {
        double p = 0;
        foreach (var c in chans) foreach (var v in c) p = Math.Max(p, Math.Abs(v));
        return p;
    }

    public static void Scale(double gain, params double[][] chans)
    {
        foreach (var c in chans) for (int i = 0; i < c.Length; i++) c[i] *= gain;
    }

    /// <summary>
    /// Runs a stateful per-sample process over a periodic signal twice and keeps the second lap, so the
    /// filter state at the loop point is the steady state and the seam is as smooth as any other sample.
    /// </summary>
    public static double[] Circular(double[] x, Func<double, double> process)
    {
        int n = x.Length;
        var y = new double[n];
        for (int i = 0; i < n; i++) process(x[i]);
        for (int i = 0; i < n; i++) y[i] = process(x[i]);
        return y;
    }
}

/// <summary>Deterministic SplitMix64, seeded from cue names, so every run renders the same bytes.</summary>
public sealed class Rng
{
    ulong _s;

    public Rng(ulong seed) => _s = seed;
    public Rng(string name) => _s = Hash(name);

    public static ulong Hash(string s)
    {
        ulong h = 14695981039346656037UL;
        foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
        return h;
    }

    public ulong NextU64()
    {
        ulong z = _s += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform in [0, 1).</summary>
    public double Next() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);
    public double Range(double a, double b) => a + (b - a) * Next();
    /// <summary>Uniform white noise in [-1, 1).</summary>
    public double Noise() => 2 * Next() - 1;
    public Rng Fork(string tag) => new(NextU64() ^ Hash(tag));
}

/// <summary>RBJ-cookbook biquad, transposed direct form II.</summary>
public sealed class Biquad
{
    readonly double _b0, _b1, _b2, _a1, _a2;
    double _z1, _z2;

    Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
    {
        _b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0; _a1 = a1 / a0; _a2 = a2 / a0;
    }

    static (double cos, double alpha) W(double f, double q)
    {
        double w = Dsp.TwoPi * Math.Min(f, 0.45 * Dsp.Fs) / Dsp.Fs;
        return (Math.Cos(w), Math.Sin(w) / (2 * q));
    }

    public static Biquad LowPass(double f, double q = 0.7071)
    {
        var (c, a) = W(f, q);
        return new((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + a, -2 * c, 1 - a);
    }

    public static Biquad HighPass(double f, double q = 0.7071)
    {
        var (c, a) = W(f, q);
        return new((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a);
    }

    /// <summary>Band-pass with 0 dB gain at the centre.</summary>
    public static Biquad BandPass(double f, double q)
    {
        var (c, a) = W(f, q);
        return new(a, 0, -a, 1 + a, -2 * c, 1 - a);
    }

    public static Biquad Peak(double f, double q, double db)
    {
        var (c, a) = W(f, q);
        double A = Math.Pow(10, db / 40);
        return new(1 + a * A, -2 * c, 1 - a * A, 1 + a / A, -2 * c, 1 - a / A);
    }

    public static Biquad HighShelf(double f, double q, double db)
    {
        var (c, a) = W(f, q);
        double A = Math.Pow(10, db / 40), s = 2 * Math.Sqrt(A) * a;
        return new(A * ((A + 1) + (A - 1) * c + s), -2 * A * ((A - 1) + (A + 1) * c), A * ((A + 1) + (A - 1) * c - s),
                   (A + 1) - (A - 1) * c + s, 2 * ((A - 1) - (A + 1) * c), (A + 1) - (A - 1) * c - s);
    }

    public static Biquad LowShelf(double f, double q, double db)
    {
        var (c, a) = W(f, q);
        double A = Math.Pow(10, db / 40), s = 2 * Math.Sqrt(A) * a;
        return new(A * ((A + 1) - (A - 1) * c + s), 2 * A * ((A - 1) - (A + 1) * c), A * ((A + 1) - (A - 1) * c - s),
                   (A + 1) + (A - 1) * c + s, -2 * ((A - 1) + (A + 1) * c), (A + 1) + (A - 1) * c - s);
    }

    public double Run(double x)
    {
        double y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return y;
    }

    public void Run(double[] x)
    {
        for (int i = 0; i < x.Length; i++) x[i] = Run(x[i]);
    }
}

/// <summary>Topology-preserving state-variable filter (Zavalishin/Simper); stable while its cutoff moves.</summary>
public sealed class Svf
{
    double _a1, _a2, _a3, _k, _ic1, _ic2;
    public double Lp, Bp, Hp;

    public Svf(double f, double q) => Set(f, q);

    public void Set(double f, double q)
    {
        double g = Math.Tan(Math.PI * Math.Clamp(f, 10, 0.45 * Dsp.Fs) / Dsp.Fs);
        _k = 1 / q;
        _a1 = 1 / (1 + g * (g + _k));
        _a2 = g * _a1;
        _a3 = g * _a2;
    }

    public void Run(double x)
    {
        double v3 = x - _ic2;
        double v1 = _a1 * _ic1 + _a2 * v3;
        double v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
        _ic1 = 2 * v1 - _ic1;
        _ic2 = 2 * v2 - _ic2;
        Lp = v2; Bp = v1; Hp = x - _k * v1 - v2;
    }

    /// <summary>Band-pass output scaled to 0 dB at the centre.</summary>
    public double Band => _k * Bp;
}

/// <summary>
/// Freeverb topology (Jezar, public domain): eight damped feedback combs in parallel, then four
/// all-passes in series, per channel; the right channel's delays are 23 samples longer for width.
/// <paramref name="size"/> scales every delay (1 = the original medium hall, 0.35 = a small wooden room).
/// </summary>
public sealed class Reverb
{
    sealed class Comb
    {
        readonly double[] _b; int _i; double _store;
        readonly double _fb, _damp;
        public Comb(int n, double fb, double damp) { _b = new double[n]; _fb = fb; _damp = damp; }
        public double Run(double x)
        {
            double o = _b[_i];
            _store = o * (1 - _damp) + _store * _damp;
            _b[_i] = x + _store * _fb;
            if (++_i == _b.Length) _i = 0;
            return o;
        }
    }

    sealed class Allpass
    {
        readonly double[] _b; int _i;
        public Allpass(int n) => _b = new double[n];
        public double Run(double x)
        {
            double bo = _b[_i];
            _b[_i] = x + bo * 0.5;
            if (++_i == _b.Length) _i = 0;
            return bo - x;
        }
    }

    static readonly int[] CombTuning = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    static readonly int[] AllpassTuning = { 556, 441, 341, 225 };
    readonly Comb[] _cl, _cr;
    readonly Allpass[] _al, _ar;

    public Reverb(double size, double feedback, double damp)
    {
        _cl = CombTuning.Select(n => new Comb(Math.Max(8, (int)(n * size)), feedback, damp)).ToArray();
        _cr = CombTuning.Select(n => new Comb(Math.Max(8, (int)((n + 23) * size)), feedback, damp)).ToArray();
        _al = AllpassTuning.Select(n => new Allpass(Math.Max(4, (int)(n * size)))).ToArray();
        _ar = AllpassTuning.Select(n => new Allpass(Math.Max(4, (int)((n + 23) * size)))).ToArray();
    }

    public (double l, double r) Run(double x)
    {
        x *= 0.015;
        double l = 0, r = 0;
        foreach (var c in _cl) l += c.Run(x);
        foreach (var c in _cr) r += c.Run(x);
        foreach (var a in _al) l = a.Run(l);
        foreach (var a in _ar) r = a.Run(r);
        return (l, r);
    }

    /// <summary>Mono room for the effects: dry plus <paramref name="wet"/> of a small damped room, same length.</summary>
    public static double[] Room(double[] dry, double wet, double size = 0.35, double feedback = 0.72, double damp = 0.45,
                                double preDelay = 0.004)
    {
        var rv = new Reverb(size, feedback, damp);
        var hp = Biquad.HighPass(180);
        int pd = Dsp.Samples(preDelay);
        var y = (double[])dry.Clone();
        for (int i = 0; i < dry.Length; i++)
        {
            var (l, r) = rv.Run(hp.Run(i >= pd ? dry[i - pd] : 0));
            y[i] += wet * 0.5 * (l + r);
        }
        return y;
    }
}
