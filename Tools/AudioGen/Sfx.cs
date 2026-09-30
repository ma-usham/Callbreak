using static AudioGen.Dsp;
using static AudioGen.Scale;
using static AudioGen.Voices;

namespace AudioGen;

/// <summary>One output file: its name (no extension), whether it is a music loop, and how to render it.</summary>
public sealed record Job(string Name, bool Music, Func<double[][]> Render);

/// <summary>
/// The sound effects of GDD §14: mono, peak-normalised to -3 dBFS, each seeded from its own file name.
/// Card sounds are built from paper crackle and soft impacts; every tonal cue is wood, marimba or a
/// soft bar chime in the game's D pentatonic. Nothing metallic-bright, nothing that rings like a coin.
/// </summary>
public static class Sfx
{
    public const double PeakDb = -3.0;

    public static IEnumerable<Job> All()
    {
        yield return Cue("SFX_CardShuffle_01", 0.80, 0.06, CardShuffle);
        for (int v = 1; v <= 3; v++) { int k = v; yield return Cue($"SFX_CardDeal_{k:00}", 0.15, 0.03, (r, n) => CardDeal(r, n, k)); }
        for (int v = 1; v <= 4; v++) { int k = v; yield return Cue($"SFX_CardPlay_{k:00}", 0.20, 0.04, (r, n) => CardPlay(r, n, k)); }
        yield return Cue("SFX_TrickCollect_01", 0.40, 0.06, TrickCollect);
        yield return Cue("SFX_CallConfirm_01", 0.25, 0.05, CallConfirm);
        yield return Cue("SFX_YourTurn_01", 0.60, 0.10, YourTurn);
        yield return Cue("SFX_TimerTick_01", 0.08, 0.025, TimerTick);
        yield return Cue("SFX_CallMet_01", 0.90, 0.20, CallMet);
        yield return Cue("SFX_CallMissed_01", 0.70, 0.15, CallMissed);
        yield return Cue("SFX_RoundEnd_01", 1.20, 0.25, RoundEnd);
        yield return Cue("SFX_Victory_01", 3.00, 0.30, Victory);
        yield return Cue("SFX_UiTap_01", 0.08, 0.02, UiTap);
        yield return Cue("SFX_UiBack_01", 0.10, 0.025, UiBack);
        yield return Cue("SFX_Toggle_01", 0.12, 0.025, Toggle);
    }

    static Job Cue(string name, double seconds, double fadeOut, Func<Rng, int, double[]> render) => new(name, false, () =>
    {
        var x = render(new Rng(name), Samples(seconds));
        Biquad.HighPass(40).Run(x);      // no DC, no sub-bass a phone cannot play anyway
        FadeIn(x, 0.0003);
        FadeOut(x, fadeOut);             // ends on an exact zero
        Gain(Db(PeakDb) / Peak(x), x);
        return new[] { x };
    });

    // ------------------------------------------------------------------ cards

    /// <summary>Round start: a riffle (two halves flicking together), the bridge cascade, the deck tapped square.</summary>
    static double[] CardShuffle(Rng r, int n)
    {
        var y = new double[n];
        double dur = n / Fs;

        // Bending the halves: a faint creak of paper.
        Add(y, Paper(r, 0.08, t => Ramp(t, 0.02) * (1 - Smoothstep((t - 0.03) / 0.05)), 250, 0.25, 1200, 5000), 0, 0.10);

        // The riffle: about fifty card edges snapping past the thumbs, mostly alternating halves
        // (two slightly different colours), quickest in the middle.
        const double t0 = 0.05, tEnd = 0.56;
        double tf = t0;
        bool side = false;
        while (tf < tEnd)
        {
            double u = (tf - t0) / (tEnd - t0);
            double e = Ramp(tf - t0, 0.04) * (1 - 0.6 * Smoothstep((u - 0.85) / 0.15)) * r.Range(0.6, 1.0);
            if (r.Next() < 0.8) side = !side;
            Burst(y, tf, 0.0007, 0.50 * e, r, side ? 3300 : 2500, 1.1);
            Blip(y, tf + 0.0003, side ? 2200 : 1750, 1.0, 1, 0.0016, 0.10 * e, 0.0002);
            tf += 0.0092 + 0.0045 * Math.Pow(2 * u - 1, 2) + r.Range(-0.0015, 0.0015);
        }
        Add(y, Paper(r, dur, t => Ramp(t - t0, 0.05) * (1 - Smoothstep((t - tEnd + 0.05) / 0.08)), 1400, 0.3, 1500, 6000), 0, 0.22);

        // The bridge: the interleaved deck cascades flat, a quick falling "frrp".
        var bridge = Paper(r, 0.24, t => Ramp(t, 0.03) * Math.Exp(-Math.Max(0, t - 0.05) / 0.06), 3000, 0.45, 900, 5500);
        Sweep(bridge, 0.9, 0.6, t => 4200 * Math.Pow(1800.0 / 4200, Math.Min(1, t / 0.16)));
        Add(y, bridge, Samples(0.54), 0.45);

        // Squaring the deck on the table.
        Thud(y, 0.715, 170, 0.022, 0.30, r, 0.8, 1300);
        Add(y, Paper(r, 0.05, t => Math.Exp(-t / 0.012), 800, 0.2, 1800, 6000), Samples(0.717), 0.12);
        return Reverb.Room(y, 0.12);
    }

    /// <summary>One card sliding off the deck and away: friction crackle speeding up, a puff of air, the edge flicking clear.</summary>
    static double[] CardDeal(Rng r, int n, int v)
    {
        var y = new double[n];
        double slide = 0.070 + 0.012 * v + r.Range(-0.006, 0.006);
        double centre = 2600 + 300 * v + r.Range(-150, 150);
        var s = Paper(r, slide + 0.03, t => Ramp(t, 0.012) * Math.Pow(Math.Sin(Math.PI * Math.Min(1, t / slide)), 0.6),
                      1500, 0.35, 1400, 7000);
        Sweep(s, 1.2, 0.55, t => centre * (0.8 + 0.4 * Math.Min(1, t / slide)));
        Add(y, s, 0, 0.6);
        Burst(y, slide * 0.4, 0.03, 0.10, r, 900, 0.8, Band.BandPass, 0.012);
        Burst(y, slide, 0.0012, 0.35, r, 3400 + 200 * v, 1.2);
        return Reverb.Room(y, 0.08);
    }

    /// <summary>A card snapped onto the felt: a paper flick, then the flat slap, the table's low knock and a tiny settle.</summary>
    static double[] CardPlay(Rng r, int n, int v)
    {
        var y = new double[n];
        Burst(y, 0, 0.0018, 0.45, r, 3200 + 250 * v, 1.0);
        Burst(y, 0.0004, 0.0025, 0.25, r, 1300, 0.9);

        double land = 0.012 + 0.004 * v + r.Range(-0.002, 0.002);
        Burst(y, land, 0.006, 0.9, r, 1700, 0.7, Band.LowPass, 0.0006);                         // the slap of air
        Blip(y, land, 185 * (1 + 0.06 * (v - 2.5)), 1.4, 0.006, 0.028, 0.7, 0.0008);            // felt-covered wood
        Blip(y, land + 0.0005, 560 + 40 * v, 1.1, 0.004, 0.014, 0.22, 0.0006);                  // the table top
        Add(y, Paper(r, 0.08, t => Ramp(t, 0.004) * Math.Exp(-t / (0.018 + 0.004 * v)), 900, 0.25, 1800, 6500),
            Samples(land + 0.004), 0.22);
        return Reverb.Room(y, 0.10);
    }

    /// <summary>Four cards swept together to the winner: a soft "shhk" that tightens, edges bumping, the stack settling.</summary>
    static double[] TrickCollect(Rng r, int n)
    {
        var y = new double[n];
        var sweep = Paper(r, 0.34, t => Ramp(t, 0.05) * (1 - Smoothstep((t - 0.2) / 0.12)), 900, 0.5, 700, 5500);
        Sweep(sweep, 0.8, 0.6, t => 1500 + 1600 * Smoothstep(t / 0.3));
        Add(y, sweep, 0, 0.55);
        foreach (var (t, g) in new[] { (0.13, 0.30), (0.19, 0.38), (0.235, 0.33) })
            Burst(y, t + r.Range(-0.006, 0.006), 0.0012, g, r, 2700 + r.Range(-300, 300), 1.1);
        Thud(y, 0.285, 200, 0.018, 0.40, r, 0.9, 1500);
        return Reverb.Room(y, 0.10);
    }

    /// <summary>Blends a moving band-pass into a noise buffer so it reads as motion (mix = how much of the band).</summary>
    static void Sweep(double[] x, double q, double mix, Func<double, double> centre)
    {
        var sv = new Svf(centre(0), q);
        for (int i = 0; i < x.Length; i++)
        {
            if ((i & 15) == 0) sv.Set(centre(i / Fs), q);
            sv.Run(x[i]);
            x[i] = mix * sv.Band + (1 - mix) * x[i];
        }
    }

    // ------------------------------------------------------------------ calls and turns

    /// <summary>A call is placed: one soft wood block on high Sa.</summary>
    static double[] CallConfirm(Rng r, int n)
    {
        var y = new double[n];
        Strike(y, 0, Hz("S'"), WoodBlock, 0.9, 2500, 0.001);
        Burst(y, 0, 0.0008, 0.10, r, 3500, 0.7, Band.LowPass);
        return Reverb.Room(y, 0.12);
    }

    /// <summary>Your turn: two marimba notes rising a fourth, Pa to high Sa.</summary>
    static double[] YourTurn(Rng r, int n)
    {
        var y = new double[n];
        Strike(y, 0.00, Hz("P"), Marimba, 0.75, 3000, 0.002);
        Strike(y, 0.14, Hz("S'"), Marimba, 1.00, 3000, 0.002);
        return Reverb.Room(y, 0.15, 0.5, 0.75);
    }

    /// <summary>Last five seconds: a very soft wooden tick.</summary>
    static double[] TimerTick(Rng r, int n)
    {
        var y = new double[n];
        var tick = new[] { new Mode(1.0, 1.0, 0.012), new Mode(2.7, 0.25, 0.005), new Mode(0.55, 0.3, 0.010) };
        Strike(y, 0, Hz("D'"), tick, 1.0, 2500, 0.0008);
        Burst(y, 0, 0.0006, 0.05, r, 3000, 0.7, Band.LowPass);
        return Reverb.Room(y, 0.05);
    }

    /// <summary>Call met: four soft bar chimes rising Pa Sa Ga Pa.</summary>
    static double[] CallMet(Rng r, int n)
    {
        var y = new double[n];
        string[] notes = { "P", "S'", "G'", "P'" };
        for (int i = 0; i < notes.Length; i++)
            Strike(y, 0.095 * i, Hz(notes[i]), Vibes, 0.7 + 0.1 * i, 2800, 0.0015, r.Range(-3, 3));
        return Reverb.Room(y, 0.20, 0.6, 0.80, 0.5);
    }

    /// <summary>Call missed: two felt-muted low notes falling Sa to the Dha below. A sigh, not a buzzer.</summary>
    static double[] CallMissed(Rng r, int n)
    {
        var y = new double[n];
        Strike(y, 0.00, Hz("S"), TongueDrum, 0.85, 1400, 0.004);
        Strike(y, 0.16, Hz("D."), TongueDrum, 0.75, 1300, 0.004);
        Burst(y, 0.00, 0.010, 0.08, r, 400, 0.7, Band.LowPass, 0.002);
        Burst(y, 0.16, 0.010, 0.07, r, 380, 0.7, Band.LowPass, 0.002);
        Biquad.LowPass(2500).Run(y);
        return Reverb.Room(y, 0.15, 0.5, 0.75);
    }

    /// <summary>Round results: a chime phrase settling Pa Ga Re Sa, the last note doubled an octave lower.</summary>
    static double[] RoundEnd(Rng r, int n)
    {
        var y = new double[n];
        var bar = Retime(Vibes, 1.2);
        (string note, double t, double v)[] phrase =
        {
            ("P'", 0.00, 0.75), ("G'", 0.13, 0.80), ("R'", 0.26, 0.80), ("S'", 0.42, 1.00), ("S", 0.42, 0.45),
        };
        foreach (var (note, t, v) in phrase) Strike(y, t, Hz(note), bar, v, 2600, 0.0015, r.Range(-3, 3));
        return Reverb.Room(y, 0.20, 0.6, 0.80, 0.5);
    }

    /// <summary>Victory: a madal roll landing on "Dha ... Dhin", then a short bansuri phrase rising to Ga and resting on high Sa.</summary>
    static double[] Victory(Rng r, int n)
    {
        var y = new double[n];

        (double t, Stroke s, double v)[] roll =
        {
            (0.000, Stroke.Ghe, 0.80), (0.000, Stroke.Ta, 0.80), (0.110, Stroke.Ti, 0.45), (0.190, Stroke.Ta, 0.50),
            (0.260, Stroke.Ti, 0.50), (0.320, Stroke.Ta, 0.60), (0.375, Stroke.Ti, 0.55), (0.425, Stroke.Ta, 0.70),
            (0.470, Stroke.Ti, 0.60), (0.515, Stroke.Ta, 0.80), (0.600, Stroke.Ghe, 0.90), (0.600, Stroke.Ta, 0.90),
        };
        foreach (var (t, s, v) in roll) Add(y, Madal(s, v, r), Samples(t), 0.8);
        Add(y, Madal(Stroke.Ghe, 1.0, r, bend: 0.22), Samples(0.70), 0.8);
        Add(y, Madal(Stroke.Tin, 1.0, r), Samples(0.70), 0.8);

        // A quiet Sa-Pa drone swells in under the flute.
        double[] drone = { Hz("S."), Hz("P."), Hz("S") };
        for (int i = Samples(0.62); i < n; i++)
        {
            double t = i / Fs, e = 0.05 * Ramp(t - 0.62, 0.4);
            foreach (double f in drone) y[i] += e * (Math.Sin(TwoPi * f * t) + 0.3 * Math.Sin(TwoPi * 2 * f * t) + 0.1 * Math.Sin(TwoPi * 3 * f * t));
        }

        var phrase = new List<FluteNote>
        {
            new(Hz("P"), 0.15, 0.75, KanFreq: Hz("D")),
            new(Hz("D"), 0.13, 0.80),
            new(Hz("S'"), 0.32, 0.85),
            new(Hz("R'"), 0.14, 0.85),
            new(Hz("G'"), 0.46, 0.95),
            new(Hz("R'"), 0.16, 0.85, Glide: true),
            new(Hz("S'"), 0.55, 0.90, Glide: true),
        };
        Add(y, Flute(phrase, r), Samples(0.80), 0.55);

        // A restrained sparkle on the last note (the GDD's trophy has "a restrained warm sparkle").
        var bar = Retime(Vibes, 1.4);
        Strike(y, 2.16, Hz("S''"), bar, 0.10, 2400, 0.002);
        Strike(y, 2.16, Hz("P'"), bar, 0.07, 2400, 0.002);
        return Reverb.Room(y, 0.22, 0.8, 0.80, 0.45, 0.010);
    }

    // ------------------------------------------------------------------ interface

    /// <summary>Button press: a small rounded wooden "tok" on high Pa.</summary>
    static double[] UiTap(Rng r, int n)
    {
        var y = new double[n];
        double f = Hz("P'");
        Blip(y, 0, f, 1.18, 0.004, 0.016, 1.0, 0.0006);
        Blip(y, 0, 2 * f, 1.10, 0.003, 0.008, 0.12, 0.0006);
        Burst(y, 0, 0.0006, 0.05, r, 4000, 0.7, Band.LowPass);
        return y;
    }

    /// <summary>Back / close: the same family a fifth lower, leaning downward.</summary>
    static double[] UiBack(Rng r, int n)
    {
        var y = new double[n];
        double f = Hz("S'");
        Blip(y, 0, f, 1.08, 0.006, 0.022, 1.0, 0.0008, -0.10, 0.04);
        Blip(y, 0, 2 * f, 1.05, 0.004, 0.010, 0.10, 0.0008, -0.10, 0.04);
        Burst(y, 0, 0.0008, 0.04, r, 3000, 0.7, Band.LowPass);
        return y;
    }

    /// <summary>Switch flipped: two tiny wooden ticks, "tk-tok".</summary>
    static double[] Toggle(Rng r, int n)
    {
        var y = new double[n];
        var tick = new[] { new Mode(1.0, 1.0, 0.007), new Mode(2.7, 0.2, 0.003) };
        Strike(y, 0.000, Hz("R''"), tick, 0.7, 3000, 0.0005);
        Strike(y, 0.036, Hz("D'"), Retime(tick, 1.5), 1.0, 2800, 0.0006);
        return y;
    }
}
