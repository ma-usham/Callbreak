using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using ArtTool;

namespace DailyRewards
{
    /// <summary>
    /// drt: image helpers for the DailyRewards art batch. Coordinates are pixels of the image named;
    /// colours are #RRGGBB (sRGB); all blending happens in linear light (ArtTool.Img).
    /// </summary>
    internal static partial class Program
    {
        private const string Usage = @"drt <command> [options]
  refsheet --out f --size W,H --bg #hex --items ""png@x,y,w,h[@ink=#hex];..."" [--boxes ""x,y,w,h;...""] [--captions ""text@cx,y;...""] [--capsize 18]
          a reference sheet: each png fitted (aspect kept, centred) into its box, optionally as one flat ink;
          dashed boxes (empty cells to fill) and grey captions
" + UsageTiles;

        private static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                Console.WriteLine(Usage);
                return 0;
            }

            var o = new Opts(args.Skip(1).ToArray());
            try
            {
                switch (args[0])
                {
                    case "refsheet": return RefSheet(o);
                    default:
                        if (TileCommand(args[0], o, out int rc)) return rc;
                        Console.Error.WriteLine($"unknown command {args[0]}");
                        return 2;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("error: " + e.Message);
                Console.Error.WriteLine(e.StackTrace);
                return 1;
            }
        }

        private static int RefSheet(Opts o)
        {
            int[] size = o.Ints("size", 2, new[] { 1536, 1024 });
            var bg = Img.ParseHex(o.Get("bg", "#FFFFFF"));
            var sheet = new Img(size[0], size[1]);
            Array.Fill(sheet.R, bg.r);
            Array.Fill(sheet.G, bg.g);
            Array.Fill(sheet.B, bg.b);
            foreach (string part in o.Need("items").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = part.Split('@');
                Img src = Img.Load(p[0].Trim());
                double[] r = Nums(p[1]);
                string ink = p.Skip(2).FirstOrDefault(s => s.StartsWith("ink="))?.Substring(4);
                double k = Math.Min(r[2] / src.W, r[3] / src.H);
                int w = Math.Max(1, (int)Math.Round(src.W * k)), h = Math.Max(1, (int)Math.Round(src.H * k));
                Img s = src.Resize(w, h);
                if (ink != null)
                {
                    var c = Img.ParseHex(ink);
                    Array.Fill(s.R, c.r);
                    Array.Fill(s.G, c.g);
                    Array.Fill(s.B, c.b);
                }

                sheet.Over(s, (int)Math.Round(r[0] + (r[2] - w) / 2), (int)Math.Round(r[1] + (r[3] - h) / 2));
            }

            using Bitmap bmp = sheet.ToBitmap();
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                if (o.Has("boxes"))
                {
                    using var pen = new Pen(Color.FromArgb(255, 170, 170, 170), 2f) { DashStyle = DashStyle.Dash };
                    foreach (string b in o.Get("boxes").Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        double[] r = Nums(b);
                        g.DrawRectangle(pen, (float)r[0], (float)r[1], (float)r[2], (float)r[3]);
                    }
                }

                if (o.Has("captions"))
                {
                    using var font = new Font("Arial", (float)o.Num("capsize", 18), GraphicsUnit.Pixel);
                    using var brush = new SolidBrush(Color.FromArgb(255, 110, 110, 110));
                    var fmt = new StringFormat { Alignment = StringAlignment.Center };
                    foreach (string c in o.Get("captions").Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        int at = c.LastIndexOf('@');
                        double[] xy = Nums(c.Substring(at + 1));
                        g.DrawString(c.Substring(0, at), font, brush, (float)xy[0], (float)xy[1], fmt);
                    }
                }
            }

            string outPath = o.Need("out");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"{outPath}  {size[0]}x{size[1]}");
            return 0;
        }

        internal static double[] Nums(string s) => s.Split(',').Select(v => double.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToArray();
    }

    /// <summary>--key value options; flags without a value are "true".</summary>
    internal sealed class Opts
    {
        private readonly Dictionary<string, string> _d = new();

        public Opts(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--")) continue;
                string k = args[i].Substring(2);
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) _d[k] = args[++i];
                else _d[k] = "true";
            }
        }

        public bool Has(string k) => _d.ContainsKey(k);
        public string Get(string k, string d = null) => _d.TryGetValue(k, out string v) ? v : d;
        public string Need(string k) => _d.TryGetValue(k, out string v) ? v : throw new ArgumentException($"--{k} is required");
        public double Num(string k, double d) => _d.TryGetValue(k, out string v) ? double.Parse(v, CultureInfo.InvariantCulture) : d;

        public double[] Nums(string k, int n, double[] d = null)
        {
            if (!_d.TryGetValue(k, out string v)) return d;
            double[] r = Program.Nums(v);
            if (n > 0 && r.Length != n) throw new ArgumentException($"--{k} needs {n} numbers");
            return r;
        }

        public int[] Ints(string k, int n, int[] d = null) => Nums(k, n)?.Select(v => (int)Math.Round(v)).ToArray() ?? d;
    }
}
