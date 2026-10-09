using System.Diagnostics;
using System.Globalization;

namespace AudioGen;

/// <summary>
/// Renders every sound effect and both music loops, writes them into the Unity project (with a copy
/// under ArtSource/Audio), then measures each file from disk and fails if any check is off.
/// See README.md.
/// </summary>
static class Program
{
    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        string root = null, only = null, pictures = null, stems = null;
        bool checkOnly = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--root" when i + 1 < args.Length: root = args[++i]; break;
                case "--only" when i + 1 < args.Length: only = args[++i]; break;
                case "--pictures" when i + 1 < args.Length: pictures = args[++i]; break;
                case "--stems" when i + 1 < args.Length: stems = args[++i]; break;
                case "--check": checkOnly = true; break;
                case "-h" or "--help": Usage(); return 0;
                default: Console.Error.WriteLine($"Unknown or incomplete option: {args[i]}"); Usage(); return 2;
            }
        }
        root ??= FindRoot();
        if (root == null)
        {
            Console.Error.WriteLine("Cannot find the Unity project (a folder with Assets and ProjectSettings); pass --root <path>.");
            return 2;
        }

        // A cue with a generated source (ArtSource/Audio/Sources/sources.json) is made from it instead. A cue
        // that has only a source and no synthesized version (SFX_CoinCollect_01) is made from it too, listed
        // after the other effects.
        var recorded = Recorded.Load(root);
        var synthesized = Sfx.All().Concat(Music.All(stems == null ? null : (name, ch) => WriteStem(stems, name, ch))).ToList();
        var jobs = synthesized.Select(j => recorded.TryGetValue(j.Name, out Job source) ? source : j)
            .Concat(recorded.Values.Where(r => synthesized.All(j => j.Name != r.Name)))
            .OrderBy(j => j.Music)
            .Where(j => only == null || j.Name.Contains(only, StringComparison.OrdinalIgnoreCase)).ToList();
        if (jobs.Count == 0) { Console.Error.WriteLine($"No cue matches '{only}'."); return 2; }
        if (pictures != null) Directory.CreateDirectory(pictures);

        string MainPath(Job j) => Path.Combine(root, "Assets", "_Project", "Audio", j.Music ? "Music" : "SFX", j.Name + ".wav");
        string CopyPath(Job j) => Path.Combine(root, "ArtSource", "Audio", j.Music ? "Music" : "SFX", j.Name + ".wav");

        if (!checkOnly)
        {
            foreach (var job in jobs)
            {
                var clock = Stopwatch.StartNew();
                byte[] bytes = Wav.Encode(job.Render(), job.Music);
                bool wrote = Wav.WriteIfChanged(MainPath(job), bytes);
                Wav.WriteIfChanged(CopyPath(job), bytes);
                string from = recorded.ContainsKey(job.Name) ? "from its source" : "synthesized";
                Console.WriteLine($"{(wrote ? "wrote    " : "unchanged")} {job.Name}.wav  ({from}, {clock.Elapsed.TotalSeconds:0.0} s)");
            }
            Console.WriteLine();
        }

        Console.WriteLine("Measured from the files on disk (16-bit). LUFS = integrated loudness (needs 0.4 s); loud = the loudest");
        Console.WriteLine("100 ms on the same scale; dc = mean sample value; centroid = spectral centre of gravity;");
        Console.WriteLine("energy % = share of energy below 150 Hz, 150-500, 500-2k, 2k-6k and above 6 kHz; phone = dB of energy");
        Console.WriteLine("lost through a phone speaker (4th-order high-pass at 400 Hz); mono = dB lost when left and right are");
        Console.WriteLine("summed to one speaker; start/end = first and last sample; seam and kink = the jump and the change of");
        Console.WriteLine("slope across the loop point, each over the file's 99.9th percentile (<= 1 is ordinary).");
        Console.WriteLine();
        Console.WriteLine(Header + "   start     end  seam  kink  result");
        int failed = 0;
        foreach (var job in jobs)
        {
            if (!File.Exists(MainPath(job))) { Console.WriteLine($"{job.Name,-22} MISSING"); failed++; continue; }
            var s = Analysis.Measure(MainPath(job));
            var bad = Check(job, s);
            if (!File.Exists(CopyPath(job)) || !File.ReadAllBytes(CopyPath(job)).AsSpan().SequenceEqual(File.ReadAllBytes(MainPath(job))))
                bad.Add("ArtSource copy differs");
            string seam = job.Music ? (s.SeamStep / Math.Max(s.StepP999, 1e-9)).ToString("0.00") : "-";
            string kink = job.Music ? (s.SeamCurve / Math.Max(s.CurveP999, 1e-9)).ToString("0.00") : "-";
            Console.WriteLine($"{Row(job.Name, s)} {s.FirstAbs,7:0.0000} {s.LastAbs,7:0.0000} {seam,5} {kink,5}  {(bad.Count == 0 ? "ok" : "FAIL: " + string.Join(", ", bad))}");
            if (bad.Count > 0) failed++;
            if (pictures != null)
            {
                var (ch, _, _) = Wav.Read(MainPath(job));
                var mono = new double[ch[0].Length];
                foreach (var c in ch) for (int i = 0; i < mono.Length; i++) mono[i] += c[i] / ch.Length;
                Analysis.Picture(Path.Combine(pictures, job.Name + ".png"), mono, job.Music ? 1800 : 900);
                // A loop also gets a close-up of every ten seconds, where single notes and strokes can be read.
                int part = Dsp.Samples(10);
                for (int at = 0; job.Music && at < mono.Length; at += part)
                    Analysis.Picture(Path.Combine(pictures, $"{job.Name}_{at / Dsp.Rate:000}s.png"),
                                     mono.AsSpan(at, Math.Min(part, mono.Length - at)).ToArray(), 1500);
            }
        }
        Console.WriteLine();
        Console.WriteLine(failed == 0 ? $"All {jobs.Count} files pass." : $"{failed} of {jobs.Count} files FAILED.");
        return failed == 0 ? 0 : 1;
    }

    const string Header = "file                   ch     sec  peak dB  rms dB   LUFS   loud  dc dB clip centroid  energy %: <150  -500   -2k   -6k   >6k  phone   mono";

    static string Row(string name, Stats s)
    {
        string lufs = double.IsNaN(s.Lufs) ? "-" : s.Lufs.ToString("0.0");
        string bands = string.Join(" ", s.Bands.Select(b => b.ToString("0.0").PadLeft(5)));
        return $"{name,-22} {s.Channels,2} {s.Seconds,7:0.000} {s.PeakDb,8:0.00} {s.RmsDb,7:0.0} {lufs,6} {s.LoudestDb,6:0.0} {Dsp.ToDb(s.Dc),6:0} {s.Clipped,4} {s.CentroidHz,8:0}           {bands} {s.PhoneDb,6:0.0} {(s.Channels > 1 ? s.MonoDb.ToString("0.0") : "-"),6}";
    }

    /// <summary>--stems: one WAV per part of a loop (as balanced, before the master), with its measurements.</summary>
    static void WriteStem(string dir, string name, double[][] ch)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name + ".wav");
        double peak = Dsp.Peak(ch);
        if (peak > 0.98) ch = ch.Select(c => c.Select(v => v * 0.98 / peak).ToArray()).ToArray();
        File.WriteAllBytes(path, Wav.Encode(ch, false));
        Console.WriteLine($"  stem {Row(name, Analysis.Measure(path))}{(peak > 0.98 ? $"  (file scaled by {Dsp.ToDb(0.98 / peak):0.0} dB to fit)" : "")}");
    }

    /// <summary>The objective checks: format, level, no clipping, no DC, clean start and end, and for loops a seam like any other sample.</summary>
    static List<string> Check(Job job, Stats s)
    {
        var bad = new List<string>();
        if (s.Rate != Dsp.Rate) bad.Add($"rate {s.Rate}");
        if (s.Clipped > 0) bad.Add($"{s.Clipped} clipped samples");
        if (job.Music)
        {
            if (s.Channels != 2) bad.Add("not stereo");
            if (Math.Abs(s.Lufs - Music.TargetLufs) > 0.5) bad.Add("loudness off target");
            if (s.PeakDb > Music.CeilingDb + 0.05) bad.Add("peak over the ceiling");
            if (s.Dc > 0.001) bad.Add("DC offset");
            if (s.SeamStep > s.StepP999) bad.Add("step at the loop seam");
            if (s.SeamCurve > s.CurveP999) bad.Add("kink at the loop seam");
            if (s.MonoDb < -3) bad.Add("loses too much in mono");
        }
        else
        {
            if (s.Channels != 1) bad.Add("not mono");
            if (Math.Abs(s.PeakDb - Sfx.PeakDb) > 0.1) bad.Add("peak off target");
            if (s.Dc > 0.003) bad.Add("DC offset");
            if (s.FirstAbs > 0.01) bad.Add("does not start at zero");
            if (s.LastAbs > 1.5 / 32767) bad.Add("does not end at zero");
            if ((long)Math.Round(s.Seconds * s.Rate) % Sfx.BlockSamples != 0) bad.Add("length is not whole ADPCM blocks");
        }
        return bad;
    }

    static string FindRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Assets")) && Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings")))
                    return dir.FullName;
            }
        }
        return null;
    }

    static void Usage()
    {
        Console.WriteLine("""
            audiogen: renders Callbreak's sound effects and music loops.
              dotnet run --project Tools/AudioGen -c Release [-- options]
            options:
              --only <text>      only the cues whose file name contains <text> (e.g. CardPlay, MUS_)
              --check            measure the files already on disk; render nothing
              --pictures <dir>   also write a waveform + spectrogram PNG per file into <dir> (loops: plus one per 10 s)
              --stems <dir>      also write each loop's separate parts (flute, madal, tanpura, drone, room) into <dir>
              --root <path>      the Unity project folder (default: found from the current folder)
            """);
    }
}
