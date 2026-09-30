using System.Text;

namespace AudioGen;

/// <summary>16-bit PCM WAV at 44.1 kHz, written and read back without any library.</summary>
public static class Wav
{
    /// <summary>
    /// Writes channels (each in [-1, 1], equal lengths) as 16-bit PCM, rounded to nearest with no dither
    /// (Unity re-encodes to ADPCM or Vorbis, which buries anything dither would fix). A loop also gets a
    /// "smpl" chunk marking the whole file as one forward loop, for tools that read loop points.
    /// Returns how many samples had to be clamped (should always be 0).
    /// </summary>
    public static int Write(string path, double[][] ch, bool loop)
    {
        int n = ch[0].Length, c = ch.Length, clamped = 0;
        int dataBytes = n * c * 2;
        const int smplBytes = 36 + 24;
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(4 + (8 + 16) + (8 + dataBytes) + (loop ? 8 + smplBytes : 0));
        w.Write(Encoding.ASCII.GetBytes("WAVE"));

        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);                   // PCM
        w.Write((short)c);
        w.Write(Dsp.Rate);
        w.Write(Dsp.Rate * c * 2);           // bytes per second
        w.Write((short)(c * 2));             // block align
        w.Write((short)16);

        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataBytes);
        for (int i = 0; i < n; i++)
        {
            for (int k = 0; k < c; k++)
            {
                double s = Math.Round(ch[k][i] * 32767.0);
                if (s > 32767) { s = 32767; clamped++; }
                else if (s < -32768) { s = -32768; clamped++; }
                w.Write((short)s);
            }
        }

        if (loop)
        {
            w.Write(Encoding.ASCII.GetBytes("smpl"));
            w.Write(smplBytes);
            w.Write(0); w.Write(0);                    // manufacturer, product
            w.Write(1_000_000_000 / Dsp.Rate);         // sample period, ns
            w.Write(60); w.Write(0);                   // MIDI unity note, pitch fraction
            w.Write(0); w.Write(0);                    // SMPTE format, offset
            w.Write(1); w.Write(0);                    // one loop, no sampler data
            w.Write(0); w.Write(0);                    // cue id, type 0 = forward
            w.Write(0); w.Write(n - 1);                // first and last sample frame of the loop
            w.Write(0); w.Write(0);                    // fraction, play count 0 = forever
        }
        return clamped;
    }

    /// <summary>Reads a 16-bit PCM WAV back as doubles in [-1, 1) plus the raw sample values.</summary>
    public static (double[][] ch, short[][] raw, int rate) Read(string path)
    {
        using var r = new BinaryReader(File.OpenRead(path));
        if (new string(r.ReadChars(4)) != "RIFF") throw new InvalidDataException($"{path}: not RIFF");
        r.ReadInt32();
        if (new string(r.ReadChars(4)) != "WAVE") throw new InvalidDataException($"{path}: not WAVE");
        int channels = 0, rate = 0, bits = 0;
        while (r.BaseStream.Position + 8 <= r.BaseStream.Length)
        {
            string id = new string(r.ReadChars(4));
            int size = r.ReadInt32();
            long next = r.BaseStream.Position + size + (size & 1);
            if (id == "fmt ")
            {
                int format = r.ReadInt16();
                channels = r.ReadInt16();
                rate = r.ReadInt32();
                r.ReadInt32(); r.ReadInt16();
                bits = r.ReadInt16();
                if (format != 1 || bits != 16) throw new InvalidDataException($"{path}: expected 16-bit PCM, got format {format}, {bits} bits");
            }
            else if (id == "data")
            {
                if (channels == 0) throw new InvalidDataException($"{path}: data before fmt");
                int n = size / (2 * channels);
                var ch = new double[channels][];
                var raw = new short[channels][];
                for (int k = 0; k < channels; k++) { ch[k] = new double[n]; raw[k] = new short[n]; }
                for (int i = 0; i < n; i++)
                {
                    for (int k = 0; k < channels; k++)
                    {
                        short s = r.ReadInt16();
                        raw[k][i] = s;
                        ch[k][i] = s / 32767.0;
                    }
                }
                return (ch, raw, rate);
            }
            r.BaseStream.Position = next;
        }
        throw new InvalidDataException($"{path}: no data chunk");
    }
}
