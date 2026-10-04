using System;
using ArtTool;

namespace ShopUi
{
    internal static partial class Program
    {
        private const string UsageExtra = @"  lumalpha --in png --out f --color #hex [--lo 0.0] [--hi 1.0]
          a light glow drawn on black: alpha from its brightness (lo..hi linear), one colour
  bounds   --in png [--thr 0.5]                 box of the pixels whose alpha is above thr
  trimto   --in png --out f [--thr 0.5] [--pad 2]   crop to that box plus a margin
";

        private static int Extra(string cmd, Opts o)
        {
            switch (cmd)
            {
                case "lumalpha": return LumAlpha(o);
                case "bounds": return Bounds(o);
                case "trimto": return TrimTo(o);
                default:
                    if (IconCommand(cmd, o, out int rc)) return rc;
                    if (ColorCommand(cmd, o, out int rc2)) return rc2;
                    if (ShapeCommand(cmd, o, out int rc3)) return rc3;
                    if (FitFrameCommand(cmd, o, out int rc4)) return rc4;
                    if (NineViewCommand(cmd, o, out int rc5)) return rc5;
                    if (FitLayerCommand(cmd, o, out int rc9)) return rc9;
                    if (GlowCommand(cmd, o, out int rc6)) return rc6;
                    if (SceneCommand(cmd, o, out int rc7)) return rc7;
                    if (ShadowCommand(cmd, o, out int rc8)) return rc8;
                    Console.Error.WriteLine("unknown command " + cmd);
                    return 1;
            }
        }

        /// <summary>Box of the pixels with alpha above a threshold, and the alpha profile across its edges.</summary>
        private static int Bounds(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            float thr = (float)o.Num("thr", 0.5);
            int x0 = m.W, y0 = m.H, x1 = -1, y1 = -1;
            for (int y = 0; y < m.H; y++)
            for (int x = 0; x < m.W; x++)
            {
                if (m.A[y * m.W + x] <= thr) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }

            Console.WriteLine($"{o.Need("in")}  {m.W}x{m.H}  alpha>{thr}: x {x0}..{x1} y {y0}..{y1}  ({x1 - x0 + 1}x{y1 - y0 + 1})");
            return 0;
        }

        /// <summary>Crop to the box of alpha above a threshold, plus a margin; everything outside it is dropped.</summary>
        private static int TrimTo(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            float thr = (float)o.Num("thr", 0.5);
            int pad = (int)o.Num("pad", 2);
            int x0 = m.W, y0 = m.H, x1 = -1, y1 = -1;
            for (int y = 0; y < m.H; y++)
            for (int x = 0; x < m.W; x++)
            {
                if (m.A[y * m.W + x] <= thr) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }

            x0 = Math.Max(0, x0 - pad); y0 = Math.Max(0, y0 - pad);
            x1 = Math.Min(m.W - 1, x1 + pad); y1 = Math.Min(m.H - 1, y1 + pad);
            Img c = m.Crop(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            c.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {c.W}x{c.H}  (from {x0},{y0})");
            return 0;
        }

        private static int LumAlpha(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            var c = Img.ParseHex(o.Need("color"));
            float lo = (float)o.Num("lo", 0), hi = (float)o.Num("hi", 1);
            var n = new Img(m.W, m.H, 0f);
            for (int i = 0; i < m.Count; i++)
            {
                float l = m.Lum(i);
                n.R[i] = c.r; n.G[i] = c.g; n.B[i] = c.b;
                n.A[i] = Math.Clamp((l - lo) / Math.Max(1e-6f, hi - lo), 0f, 1f);
            }

            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {n.W}x{n.H}");
            return 0;
        }
    }
}
