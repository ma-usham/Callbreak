using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using static AudioGen.Dsp;

namespace AudioGen;

/// <summary>What the generator measures on every file it wrote, since nobody listens during a build.</summary>
public sealed record Stats(
    double Seconds, int Channels, int Rate, double PeakDb, double RmsDb, double Lufs, double Dc, int Clipped,
    double CentroidHz, double HighPct, double FirstAbs, double LastAbs,
    double SeamStep, double StepP999, double SeamCurve, double CurveP999);

public static class Analysis
{
    /// <summary>
    /// Integrated loudness per ITU-R BS.1770-4: K-weighting, 400 ms blocks at 75% overlap, a -70 LUFS
    /// absolute gate and a -10 LU relative gate. NaN for files shorter than one block.
    /// </summary>
    public static double Lufs(params double[][] ch)
    {
        int n = ch[0].Length, block = Samples(0.4), hop = Samples(0.1);
        if (n < block) return double.NaN;
        var prefix = new double[n + 1];
        foreach (var c in ch)
        {
            var shelf = Biquad.HighShelf(1500, 0.7071, 4.0);
            var hp = Biquad.HighPass(38, 0.5);
            double acc = 0;
            for (int i = 0; i < n; i++)
            {
                double v = hp.Run(shelf.Run(c[i]));
                acc += v * v;
                prefix[i + 1] += acc;
            }
        }
        var blocks = new List<double>();
        for (int s = 0; s + block <= n; s += hop) blocks.Add((prefix[s + block] - prefix[s]) / block);
        static double L(double meanSquare) => -0.691 + 10 * Math.Log10(Math.Max(meanSquare, 1e-20));
        var loud = blocks.Where(b => L(b) > -70).ToList();
        if (loud.Count == 0) return double.NegativeInfinity;
        double gate = L(loud.Average()) - 10;
        return L(loud.Where(b => L(b) > gate).Average());
    }

    /// <summary>Measures a WAV as it sits on disk (after 16-bit rounding).</summary>
    public static Stats Measure(string path)
    {
        var (ch, raw, rate) = Wav.Read(path);
        int n = ch[0].Length;
        double peak = 0, sumSq = 0, dc = 0, first = 0, last = 0;
        int clipped = 0;
        double seamStep = 0, stepP = 0, seamCurve = 0, curveP = 0;
        for (int k = 0; k < ch.Length; k++)
        {
            double mean = 0;
            var stepHist = new int[65536];
            var curveHist = new int[131072];
            for (int i = 0; i < n; i++)
            {
                double v = ch[k][i];
                peak = Math.Max(peak, Math.Abs(v));
                sumSq += v * v;
                mean += v;
                if (raw[k][i] >= 32767 || raw[k][i] <= -32767) clipped++;
                if (i > 0) stepHist[Math.Abs(raw[k][i] - raw[k][i - 1])]++;
                if (i > 1) curveHist[Math.Abs(raw[k][i] - 2 * raw[k][i - 1] + raw[k][i - 2])]++;
            }
            dc = Math.Max(dc, Math.Abs(mean / n));
            first = Math.Max(first, Math.Abs(ch[k][0]));
            last = Math.Max(last, Math.Abs(ch[k][n - 1]));
            // The loop seam: the jump and the change of slope from the last samples into the first ones,
            // against what is ordinary elsewhere in the file (99.9th percentile).
            seamStep = Math.Max(seamStep, Math.Abs(raw[k][0] - raw[k][n - 1]) / 32767.0);
            seamCurve = Math.Max(seamCurve, Math.Max(Math.Abs(raw[k][0] - 2 * raw[k][n - 1] + raw[k][n - 2]),
                                                     Math.Abs(raw[k][1] - 2 * raw[k][0] + raw[k][n - 1])) / 32767.0);
            stepP = Math.Max(stepP, Percentile(stepHist, n - 1, 0.999) / 32767.0);
            curveP = Math.Max(curveP, Percentile(curveHist, n - 2, 0.999) / 32767.0);
        }

        var mono = new double[n];
        foreach (var c in ch) for (int i = 0; i < n; i++) mono[i] += c[i] / ch.Length;
        var (centroid, high) = Spectrum(mono);
        return new Stats((double)n / rate, ch.Length, rate, ToDb(peak), ToDb(Math.Sqrt(sumSq / (n * ch.Length))), Lufs(ch),
                         dc, clipped, centroid, high, first, last, seamStep, stepP, seamCurve, curveP);
    }

    static double Percentile(int[] hist, int count, double p)
    {
        long want = (long)Math.Ceiling(count * p), seen = 0;
        for (int v = 0; v < hist.Length; v++)
        {
            seen += hist[v];
            if (seen >= want) return v;
        }
        return hist.Length - 1;
    }

    /// <summary>Averaged power spectrum: the spectral centroid (brightness) and the share of energy above 6 kHz (harshness).</summary>
    static (double centroidHz, double highPct) Spectrum(double[] x)
    {
        int size = 4096;
        while (size > 256 && size > x.Length) size >>= 1;
        int hop = size / 2, frames = 0;
        var power = new double[size / 2 + 1];
        var re = new double[size];
        var im = new double[size];
        for (int s = 0; s == 0 || s + size <= x.Length; s += hop)
        {
            for (int i = 0; i < size; i++)
            {
                double w = 0.5 - 0.5 * Math.Cos(TwoPi * i / size);
                re[i] = s + i < x.Length ? x[s + i] * w : 0;
                im[i] = 0;
            }
            Fft(re, im);
            for (int b = 0; b <= size / 2; b++) power[b] += re[b] * re[b] + im[b] * im[b];
            frames++;
        }
        double total = 0, weighted = 0, high = 0;
        for (int b = 1; b <= size / 2; b++)
        {
            double f = b * Fs / size;
            if (f < 20) continue;
            total += power[b];
            weighted += f * power[b];
            if (f > 6000) high += power[b];
        }
        return total <= 0 ? (0, 0) : (weighted / total, 100 * high / total);
    }

    public static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = -TwoPi / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (int j = 0; j < len / 2; j++)
                {
                    int a = i + j, b = a + len / 2;
                    double xr = re[b] * cr - im[b] * ci, xi = re[b] * ci + im[b] * cr;
                    re[b] = re[a] - xr; im[b] = im[a] - xi;
                    re[a] += xr; im[a] += xi;
                    double ncr = cr * wr - ci * wi;
                    ci = cr * wi + ci * wr;
                    cr = ncr;
                }
            }
        }
    }

    // ------------------------------------------------------------------ pictures

    /// <summary>
    /// Writes a picture of a sound for review without ears: the waveform on top, then a spectrogram
    /// (50 Hz to 16 kHz on a log scale, 80 dB of range) with lines at 100 Hz, 1 kHz and 10 kHz and time
    /// ticks along the bottom (every 0.1 s for clips up to 4 s, every 10 s for longer ones).
    /// </summary>
    public static void Picture(string path, double[] x, int width)
    {
        const int waveH = 72, specH = 320, tickH = 8;
        int height = waveH + specH + tickH, n = x.Length;
        int win = n < 30000 ? 512 : n < 400000 ? 2048 : 4096;
        var rgb = new byte[width * height * 3];
        void Px(int px, int py, byte r, byte g, byte b)
        {
            if (px < 0 || py < 0 || px >= width || py >= height) return;
            int o = (py * width + px) * 3;
            rgb[o] = r; rgb[o + 1] = g; rgb[o + 2] = b;
        }

        // Waveform: min/max per column.
        double peak = Math.Max(1e-9, Peak(x));
        for (int col = 0; col < width; col++)
        {
            int a = (int)((long)col * n / width), b = Math.Max(a + 1, (int)((long)(col + 1) * n / width));
            double lo = 0, hi = 0;
            for (int i = a; i < b && i < n; i++) { lo = Math.Min(lo, x[i]); hi = Math.Max(hi, x[i]); }
            int y0 = (int)((1 - hi / peak) * 0.5 * (waveH - 1)), y1 = (int)((1 - lo / peak) * 0.5 * (waveH - 1));
            for (int py = 0; py < waveH; py++)
            {
                bool on = py >= y0 && py <= y1;
                Px(col, py, (byte)(on ? 210 : 24), (byte)(on ? 220 : 24), (byte)(on ? 230 : 30));
            }
        }

        // Spectrogram.
        var db = new double[width][];
        var re = new double[win];
        var im = new double[win];
        double max = -300;
        const double fLo = 50, fHi = 16000;
        for (int col = 0; col < width; col++)
        {
            long centre = (long)((col + 0.5) * n / width);
            for (int i = 0; i < win; i++)
            {
                long s = centre - win / 2 + i;
                double w = 0.5 - 0.5 * Math.Cos(TwoPi * i / win);
                re[i] = s >= 0 && s < n ? x[s] * w : 0;
                im[i] = 0;
            }
            Fft(re, im);
            var column = db[col] = new double[specH];
            for (int row = 0; row < specH; row++)
            {
                double f0 = fLo * Math.Pow(fHi / fLo, (double)(specH - 1 - row) / specH);
                double f1 = fLo * Math.Pow(fHi / fLo, (double)(specH - row) / specH);
                int b0 = Math.Max(1, (int)Math.Round(f0 * win / Fs)), b1 = Math.Max(b0, (int)Math.Round(f1 * win / Fs));
                double p = 0;
                for (int b = b0; b <= b1 && b < win / 2; b++) p = Math.Max(p, re[b] * re[b] + im[b] * im[b]);
                column[row] = 10 * Math.Log10(p + 1e-30);
                max = Math.Max(max, column[row]);
            }
        }
        (double at, byte r, byte g, byte b)[] ramp =
        {
            (0.00, 0, 0, 6), (0.25, 70, 12, 110), (0.50, 185, 55, 85), (0.75, 249, 142, 10), (1.00, 252, 255, 164),
        };
        for (int col = 0; col < width; col++)
        {
            for (int row = 0; row < specH; row++)
            {
                double v = Math.Clamp((db[col][row] - max + 80) / 80, 0, 1);
                int k = 0;
                while (k < ramp.Length - 2 && v > ramp[k + 1].at) k++;
                double u = (v - ramp[k].at) / (ramp[k + 1].at - ramp[k].at);
                Px(col, waveH + row,
                   (byte)(ramp[k].r + u * (ramp[k + 1].r - ramp[k].r)),
                   (byte)(ramp[k].g + u * (ramp[k + 1].g - ramp[k].g)),
                   (byte)(ramp[k].b + u * (ramp[k + 1].b - ramp[k].b)));
            }
        }
        foreach (double f in new[] { 100.0, 1000.0, 10000.0 })
        {
            int row = specH - 1 - (int)(specH * Math.Log(f / fLo) / Math.Log(fHi / fLo));
            for (int col = 0; col < width; col += 6) Px(col, waveH + row, 120, 120, 120);
        }
        double seconds = n / Fs, tick = seconds <= 4 ? 0.1 : 10;
        for (int py = waveH + specH; py < height; py++)
            for (int col = 0; col < width; col++) Px(col, py, 24, 24, 30);
        for (double t = 0; t <= seconds + 1e-9; t += tick)
        {
            int col = Math.Min(width - 1, (int)(t / seconds * width));
            for (int py = waveH + specH; py < height; py++) Px(col, py, 230, 230, 230);
        }
        WritePng(path, rgb, width, height);
    }

    static void WritePng(string path, byte[] rgb, int w, int h)
    {
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), w);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), h);
        header[8] = 8;  // bits per channel
        header[9] = 2;  // RGB
        Chunk(fs, "IHDR", header);
        using var packed = new MemoryStream();
        using (var z = new ZLibStream(packed, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (int y = 0; y < h; y++)
            {
                z.WriteByte(0);
                z.Write(rgb, y * w * 3, w * 3);
            }
        }
        Chunk(fs, "IDAT", packed.ToArray());
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        s.Write(word);
        byte[] name = Encoding.ASCII.GetBytes(type);
        s.Write(name);
        s.Write(data);
        uint crc = 0xFFFFFFFF;
        foreach (byte b in name) crc = Crc(crc, b);
        foreach (byte b in data) crc = Crc(crc, b);
        BinaryPrimitives.WriteUInt32BigEndian(word, crc ^ 0xFFFFFFFF);
        s.Write(word);
    }

    static uint Crc(uint crc, byte b)
    {
        crc ^= b;
        for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        return crc;
    }
}
