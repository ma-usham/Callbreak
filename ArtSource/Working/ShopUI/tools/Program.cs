using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ArtTool;

namespace ShopUi
{
    /// <summary>
    /// shopui: helpers for the ShopUI art batch that kitcontrols (Tools/ArtPipeline/Kit/Controls)
    /// does not have. Images are ArtTool.Img (linear light, straight alpha); rects are pixels.
    /// </summary>
    internal static partial class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                Console.WriteLine(Usage);
                return 0;
            }

            try
            {
                var o = new Opts(args.Skip(1).ToArray());
                switch (args[0])
                {
                    case "splicey": return SpliceY(o);
                    default: return Extra(args[0], o);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("error: " + e.Message);
                return 1;
            }
        }

        private const string Usage = @"shopui <command> [options]
  splicey --in png --rect x,y,w,h --cuts ""y0-y1;..."" --out f [--blend 6]
          crop a region and take out bands of rows (cross-faded), e.g. a tall panel shown short
" + UsageExtra + UsageIcons + UsageColors + UsageShapes + UsageFitFrame + UsageNineView + UsageGlow + UsageScene + UsageShadow + UsageFitLayer;

        // ------------------------------------------------------------------------------------
        private static int SpliceY(Opts o)
        {
            Img src = Img.Load(o.Need("in"));
            int[] r = o.Ints("rect", 4, null);
            Img c = src.Crop(r[0], r[1], r[2], r[3]);
            int blend = (int)o.Num("blend", 6);
            var cuts = o.Need("cuts").Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Split('-').Select(v => int.Parse(v.Trim()) - r[1]).ToArray())
                .OrderByDescending(a => a[0]).ToList();
            foreach (int[] cut in cuts)
            {
                int a = cut[0], b = cut[1], gone = b - a;
                var n = new Img(c.W, c.H - gone);
                for (int y = 0; y < n.H; y++)
                {
                    int above = Math.Min(y, c.H - 1), below = Math.Min(y + gone, c.H - 1);
                    float t = Math.Clamp((y - (a - blend / 2f)) / blend, 0f, 1f);
                    for (int x = 0; x < c.W; x++)
                    {
                        int d = y * n.W + x, ia = above * c.W + x, ib = below * c.W + x;
                        n.R[d] = c.R[ia] + (c.R[ib] - c.R[ia]) * t;
                        n.G[d] = c.G[ia] + (c.G[ib] - c.G[ia]) * t;
                        n.B[d] = c.B[ia] + (c.B[ib] - c.B[ia]) * t;
                        n.A[d] = c.A[ia] + (c.A[ib] - c.A[ia]) * t;
                    }
                }

                c = n;
            }

            c.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {c.W}x{c.H}");
            return 0;
        }
    }

    /// <summary>--key value options; flags without a value are "true".</summary>
    internal sealed class Opts
    {
        private readonly Dictionary<string, string> _d = new();
        public readonly List<string> Pos = new();

        public Opts(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    string k = args[i].Substring(2);
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) _d[k] = args[++i];
                    else _d[k] = "true";
                }
                else Pos.Add(args[i]);
            }
        }

        public bool Has(string k) => _d.ContainsKey(k);
        public string Get(string k, string def = null) => _d.TryGetValue(k, out string v) ? v : def;
        public string Need(string k) => _d.TryGetValue(k, out string v) ? v : throw new ArgumentException("missing --" + k);
        public double Num(string k, double def) => _d.TryGetValue(k, out string v) ? double.Parse(v, CultureInfo.InvariantCulture) : def;

        public double[] Nums(string k, int n, double[] def)
        {
            if (!_d.TryGetValue(k, out string v)) return def ?? throw new ArgumentException("missing --" + k);
            double[] a = v.Split(',').Select(s => double.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
            if (n > 0 && a.Length != n) throw new ArgumentException($"--{k} needs {n} numbers");
            return a;
        }

        public int[] Ints(string k, int n, int[] def)
        {
            double[] d = Nums(k, n, def?.Select(x => (double)x).ToArray());
            return d.Select(x => (int)Math.Round(x)).ToArray();
        }
    }
}
