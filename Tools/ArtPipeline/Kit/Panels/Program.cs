using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ArtTool;

namespace KitPanels
{
    /// <summary>
    /// kitpanels - cleans ChatGPT extractions of panels and controls, builds 9-slice sprites from
    /// them and checks every sprite against its mockup. Rectangles are mockup pixels (1672 x 941,
    /// top-left origin); "unit" is sprite pixels per mockup pixel (2.3 for an @2x export).
    /// </summary>
    internal static partial class Program
    {
        private static string _repo;

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
                _repo = FindRepo();
                var o = new Opts(args.Skip(1).ToArray());
                switch (args[0])
                {
                    case "info": return Info(o);
                    case "cut": return Cut(o);
                    case "mirror": return Mirror(o);
                    case "resize": return Resize(o);
                    case "crop": return CropCmd(o);
                    case "shape": return ShapeCmd(o);
                    case "nine": return Nine(o);
                    case "check": return Check(o);
                    case "stretch": return Stretch(o);
                    case "stats": return Stats(o);
                    case "recolor": return Recolor(o);
                    case "gradmap": return GradMap(o);
                    case "extent": return Extent(o);
                    case "seams": return Seams(o);
                    case "line": return Line(o);
                    case "tile": return TileCmd(o);
                    case "defringe": return Defringe(o);
                    case "flatfill": return FlatFill(o);
                    case "rot180": return Rot180(o);
                    case "curve": return Curve(o);
                    case "compose": return Compose(o);
                    case "pad": return PadCmd(o);
                    case "export": return Export(o);
                    case "onbg": return OnBg(o);
                    case "sheet": return Sheet(o);
                    case "split": return Split(o);
                    case "flatten": return Flatten(o);
                    case "states": return States(o);
                    default:
                        Console.Error.WriteLine($"unknown command '{args[0]}'\n{Usage}");
                        return 2;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"error: {e.Message}\n{e.StackTrace}");
                return 1;
            }
        }

        private const string Usage = @"kitpanels <command> [options]
  info    <png>...                                   size, alpha make-up, content box, corner colour, corner radius
  cut     --in a --out b [--key auto|magenta|none] [--lo 12] [--hi 250] [--notrim]   clean alpha (or key out magenta), trim
  crop    --in a --out b --box x,y,w,h               crop in image pixels
  mirror  --in a --out b --axis h|v|hv [--from left|right|top|bottom]   copy the better half over the other
  resize  --in a --out b --w W --h H
  shape   --in a --out b --type roundrect|pill|ellipse [--radius r] [--inset i]   exact geometric alpha over bled colour
  nine    --in a --out b --border L,T,R,B [--mid 32,32]                       compact 9-slice: the middle squeezed to a few plain pixels
  flatten --in a --out b --border L,T,R,B [--mid 32,32]                       same size, the middle as the game will stretch it
  check   --sprite s --screen 02 --rect x,y,w,h [--nine L,T,R,B] [--unit 2.3] [--view x,y,w,h] [--zoom 3] --out f
  tile    --out f [--bg #808080] [--maxh 160] [--cols 0] <png>...   several images side by side, for looking at them
  defringe --in a --out b [--px 2]                   edge pixels take the colour just inside them (removes the magenta fringe)
  flatfill --in a --out b [--thr 8] [--grow 3] [--fill #hex]   plain fill everywhere except ornaments and outline
  rot180  --in a --out b [--from top|bottom]         point symmetry: one half becomes the other turned by 180 degrees
  curve   --in a --out b --anchors ""#src>#dst;...""   per-channel curve through the anchors (nearest offset outside them)
  pad     --in a --out b --w W --h H                 centre on a transparent canvas
  stretch --sprite s --nine L,T,R,B [--unit 2.3] --size W,H --out f [--zoom 1] [--bg #E9DDD0]
  stats   --sprite s --screen 02 --rect x,y,w,h [--nine ..] [--unit 2.3] --sample ""x,y,w,h;...""
  recolor --in a --out b --anchors ""#src>#dst;..."" [--mode affine|offset|gain]
  gradmap --in a --out b --channel b --stops ""178:#F6DFCB;239:#F7EEE3;255:#FCF8F2"" [--stops2 ... (colours at the bottom row)]
  extent  --in a [--thr 3] [--edge 12]             how far the corner ornaments reach (for choosing 9-slice borders)
  seams   --in a --nine L,T,R,B                      largest colour step across each 9-slice line (sRGB levels)
  line    --in a (--row y | --col x) --from a --to b   pixel colours and alpha along a line of any image
  export  --in a --name UI/Panels/UI_Panel_Large.png [--w W --h H]   bleed colour, save to Assets/_Project/Art + ArtSource/Exports/UI
  onbg    --in a --out f [--zoom 1]                  the sprite on black, white and magenta side by side
  sheet   --out f --items ""02:x,y,w,h:scale;..."" [--cols 4] [--gap 40] [--bg #FFFFFF]
  split   --in a --outdir d --names a,b,c [--min 200] [--grow 6]
  states  --in a --out b --tint #RRGGBB [--amount 0.5] | --darken 0.08 | --desaturate 0.6";

        // ------------------------------------------------------------------------------------
        private sealed class Opts
        {
            private readonly Dictionary<string, string> _d = new Dictionary<string, string>();
            public readonly List<string> Pos = new List<string>();

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
            public string Get(string k, string d = null) => _d.TryGetValue(k, out string v) ? v : d;
            public string Need(string k) => _d.TryGetValue(k, out string v) ? v : throw new ArgumentException($"--{k} is required");
            public double Num(string k, double d) => _d.TryGetValue(k, out string v) ? double.Parse(v, CultureInfo.InvariantCulture) : d;
            public int Int(string k, int d) => _d.TryGetValue(k, out string v) ? int.Parse(v, CultureInfo.InvariantCulture) : d;
            public double[] Nums(string k, int n, double[] d = null)
            {
                if (!_d.TryGetValue(k, out string v)) return d;
                double[] r = v.Split(',').Select(p => double.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
                if (n > 0 && r.Length != n) throw new ArgumentException($"--{k} needs {n} numbers");
                return r;
            }

            public int[] Ints(string k, int n, int[] d = null)
            {
                double[] r = Nums(k, n);
                return r == null ? d : r.Select(x => (int)Math.Round(x)).ToArray();
            }
        }

        private static string FindRepo()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, "Assets")) && Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings"))) return dir.FullName;
                    dir = dir.Parent;
                }
            }

            throw new DirectoryNotFoundException("run kitpanels from inside the Unity project");
        }

        private static string MockupPath(string screen)
        {
            if (File.Exists(screen)) return screen;
            string dir = Path.Combine(_repo, "Assets", "ArtRefrences", "game-screens");
            string hit = Directory.GetFiles(dir, "*.png").FirstOrDefault(f => Path.GetFileName(f).StartsWith(screen, StringComparison.OrdinalIgnoreCase));
            if (hit == null) throw new FileNotFoundException($"no mockup matches '{screen}' in {dir}");
            return hit;
        }

        private static byte S8(float linear) => Img.ToSrgbByte(linear);

        // ------------------------------------------------------------------------------------
        // info
        private static int Info(Opts o)
        {
            foreach (string p in o.Pos)
            {
                Img m = Img.Load(p);
                long clear = 0, solid = 0, semi = 0;
                for (int i = 0; i < m.Count; i++)
                {
                    if (m.A[i] <= 0f) clear++;
                    else if (m.A[i] >= 0.999f) solid++;
                    else semi++;
                }

                Box b = AlphaBox(m, 8f / 255f);
                Console.WriteLine($"{Path.GetFileName(p)}  {m.W}x{m.H}  clear {100.0 * clear / m.Count:0.0}%  solid {100.0 * solid / m.Count:0.0}%  semi {100.0 * semi / m.Count:0.0}%");
                Console.WriteLine($"  content box {b.X},{b.Y} {b.W}x{b.H}   corners: TL {Px(m, 2, 2)}  TR {Px(m, m.W - 3, 2)}  BL {Px(m, 2, m.H - 3)}  BR {Px(m, m.W - 3, m.H - 3)}");
                if (!b.IsEmpty)
                {
                    Console.WriteLine($"  corner radius (alpha 0.5 along the diagonals): TL {Radius(m, b, 0):0.0} TR {Radius(m, b, 1):0.0} BL {Radius(m, b, 2):0.0} BR {Radius(m, b, 3):0.0}");
                    Console.WriteLine($"  centre colour {Hex(m, b.X + b.W / 2, b.Y + b.H / 2)}  mean solid {MeanSolid(m)}");
                }
            }

            return 0;
        }

        private static string Px(Img m, int x, int y)
        {
            int i = y * m.W + x;
            return $"{Img.Hex(m.R[i], m.G[i], m.B[i])}/a{(int)Math.Round(m.A[i] * 255)}";
        }

        private static string Hex(Img m, int x, int y)
        {
            int i = Math.Clamp(y, 0, m.H - 1) * m.W + Math.Clamp(x, 0, m.W - 1);
            return Img.Hex(m.R[i], m.G[i], m.B[i]);
        }

        private static string MeanSolid(Img m)
        {
            double r = 0, g = 0, b = 0; long n = 0;
            for (int i = 0; i < m.Count; i++)
            {
                if (m.A[i] < 0.999f) continue;
                r += m.R[i]; g += m.G[i]; b += m.B[i]; n++;
            }

            return n == 0 ? "-" : Img.Hex((float)(r / n), (float)(g / n), (float)(b / n));
        }

        private static Box AlphaBox(Img m, float thr)
        {
            int minX = m.W, minY = m.H, maxX = -1, maxY = -1;
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    if (m.A[y * m.W + x] > thr)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            return maxX < 0 ? new Box(0, 0, 0, 0) : new Box(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>Corner radius from where alpha crosses one half along the corner's diagonal.</summary>
        private static double Radius(Img m, Box b, int corner)
        {
            int dx = corner == 0 || corner == 2 ? 1 : -1, dy = corner < 2 ? 1 : -1;
            int sx = dx > 0 ? b.X : b.Right - 1, sy = dy > 0 ? b.Y : b.Bottom - 1;
            double prev = 0;
            for (int t = 0; t < Math.Min(b.W, b.H) / 2; t++)
            {
                double a = m.A[(sy + dy * t) * m.W + sx + dx * t];
                if (a >= 0.5)
                {
                    double frac = a - prev > 1e-6 ? (0.5 - prev) / (a - prev) : 0;
                    return (t - 1 + frac + 0.5) / 0.2929;
                }

                prev = a;
            }

            return double.NaN;
        }

        // ------------------------------------------------------------------------------------
        // cut: alpha clean-up / magenta key, trim
        private static int Cut(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            string key = o.Get("key", "auto");
            if (key == "auto") key = LooksMagenta(m) ? "magenta" : "none";
            if (key == "magenta") KeyMagenta(m, (float)o.Num("soft", 1.0));
            float lo = (float)o.Num("lo", 12) / 255f, hi = (float)o.Num("hi", 250) / 255f;
            for (int i = 0; i < m.Count; i++)
            {
                float a = m.A[i];
                a = a <= lo ? 0f : (a >= hi ? 1f : (a - lo) / (hi - lo));
                m.A[i] = a;
                if (a == 0f) { m.R[i] = 0; m.G[i] = 0; m.B[i] = 0; }
            }

            if (o.Has("despeckle")) Despeckle(m, o.Int("despeckle", 40));
            Box b = AlphaBox(m, 8f / 255f);
            if (!o.Has("notrim") && !b.IsEmpty) m = m.Crop(b.X, b.Y, b.W, b.H);
            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  key={key}  content {b}  ->  {m.W}x{m.H}");
            return 0;
        }

        private static bool LooksMagenta(Img m)
        {
            int hits = 0;
            foreach ((int x, int y) in new[] { (2, 2), (m.W - 3, 2), (2, m.H - 3), (m.W - 3, m.H - 3) })
            {
                int i = y * m.W + x;
                if (m.A[i] > 0.9f && m.R[i] > 0.6f && m.B[i] > 0.6f && m.G[i] < 0.15f) hits++;
            }

            return hits >= 3;
        }

        /// <summary>
        /// Alpha from how far a pixel is pulled towards the flat magenta ground (magenta has high
        /// red and blue with no green; nothing in this kit does), then the ground is taken out of
        /// the edge pixels' colour.
        /// </summary>
        private static void KeyMagenta(Img m, float soft)
        {
            // The ground as ChatGPT drew it (it is rarely exactly #FF00FF): mean of the border.
            double kr = 0, kg = 0, kb = 0; int n = 0;
            for (int x = 0; x < m.W; x++)
            {
                foreach (int y in new[] { 0, 1, m.H - 2, m.H - 1 })
                {
                    int i = y * m.W + x;
                    kr += m.R[i]; kg += m.G[i]; kb += m.B[i]; n++;
                }
            }

            float Kr = (float)(kr / n), Kg = (float)(kg / n), Kb = (float)(kb / n);
            float sBg = Math.Min(Kr, Kb) - Kg;
            if (sBg < 0.2f) throw new InvalidOperationException($"the border is not magenta ({Img.Hex(Kr, Kg, Kb)})");
            for (int i = 0; i < m.Count; i++)
            {
                float s = Math.Min(m.R[i], m.B[i]) - m.G[i];
                float a = 1f - Math.Clamp(s / (sBg * soft), 0f, 1f);
                if (a <= 0.004f) { m.A[i] = 0f; m.R[i] = 0; m.G[i] = 0; m.B[i] = 0; continue; }
                if (a < 0.996f)
                {
                    m.R[i] = Math.Clamp((m.R[i] - (1 - a) * Kr) / a, 0f, 1f);
                    m.G[i] = Math.Clamp((m.G[i] - (1 - a) * Kg) / a, 0f, 1f);
                    m.B[i] = Math.Clamp((m.B[i] - (1 - a) * Kb) / a, 0f, 1f);
                }

                m.A[i] *= a;
            }
        }

        /// <summary>Removes connected blobs of visible pixels smaller than <paramref name="minArea"/>.</summary>
        private static void Despeckle(Img m, int minArea)
        {
            foreach ((Box box, List<int> px) in Components(m, 8f / 255f, 0))
            {
                if (px.Count >= minArea) continue;
                foreach (int i in px) { m.A[i] = 0; m.R[i] = 0; m.G[i] = 0; m.B[i] = 0; }
            }
        }

        private static List<(Box box, List<int> px)> Components(Img m, float thr, int grow)
        {
            int w = m.W, h = m.H;
            var on = new bool[w * h];
            for (int i = 0; i < on.Length; i++) on[i] = m.A[i] > thr;
            if (grow > 0)
            {
                var mask = new float[w * h];
                for (int i = 0; i < on.Length; i++) mask[i] = on[i] ? 1f : 0f;
                mask = Inpaint.Dilate(mask, w, h, grow);
                for (int i = 0; i < on.Length; i++) on[i] = mask[i] > 0.5f;
            }

            var seen = new bool[w * h];
            var result = new List<(Box, List<int>)>();
            var stack = new Stack<int>();
            for (int s = 0; s < on.Length; s++)
            {
                if (!on[s] || seen[s]) continue;
                var px = new List<int>();
                int minX = w, minY = h, maxX = -1, maxY = -1;
                stack.Push(s); seen[s] = true;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    px.Add(i);
                    int x = i % w, y = i / w;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    if (x > 0 && on[i - 1] && !seen[i - 1]) { seen[i - 1] = true; stack.Push(i - 1); }
                    if (x < w - 1 && on[i + 1] && !seen[i + 1]) { seen[i + 1] = true; stack.Push(i + 1); }
                    if (y > 0 && on[i - w] && !seen[i - w]) { seen[i - w] = true; stack.Push(i - w); }
                    if (y < h - 1 && on[i + w] && !seen[i + w]) { seen[i + w] = true; stack.Push(i + w); }
                }

                result.Add((new Box(minX, minY, maxX - minX + 1, maxY - minY + 1), px));
            }

            return result;
        }

        // ------------------------------------------------------------------------------------
        private static int CropCmd(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            int[] b = o.Ints("box", 4);
            m.Crop(b[0], b[1], b[2], b[3]).Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {b[2]}x{b[3]}");
            return 0;
        }

        private static int Mirror(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            string axis = o.Get("axis", "h"), from = o.Get("from", "");
            if (axis.Contains('h')) MirrorH(m, from != "right");
            if (axis.Contains('v')) MirrorV(m, from != "bottom");
            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  mirrored {axis}");
            return 0;
        }

        private static void MirrorH(Img m, bool fromLeft)
        {
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W / 2; x++)
                {
                    int a = y * m.W + x, b = y * m.W + (m.W - 1 - x);
                    int s = fromLeft ? a : b, t = fromLeft ? b : a;
                    m.R[t] = m.R[s]; m.G[t] = m.G[s]; m.B[t] = m.B[s]; m.A[t] = m.A[s];
                }
            }
        }

        private static void MirrorV(Img m, bool fromTop)
        {
            for (int y = 0; y < m.H / 2; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    int a = y * m.W + x, b = (m.H - 1 - y) * m.W + x;
                    int s = fromTop ? a : b, t = fromTop ? b : a;
                    m.R[t] = m.R[s]; m.G[t] = m.G[s]; m.B[t] = m.B[s]; m.A[t] = m.A[s];
                }
            }
        }

        private static int Resize(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            int w = o.Int("w", 0), h = o.Int("h", 0);
            if (w == 0 && h == 0) throw new ArgumentException("--w and/or --h");
            if (w == 0) w = (int)Math.Round(m.W * (double)h / m.H);
            if (h == 0) h = (int)Math.Round(m.H * (double)w / m.W);
            Console.WriteLine($"{o.Need("out")}  {m.W}x{m.H} -> {w}x{h}  (x{(double)w / m.W:0.000}, y{(double)h / m.H:0.000})");
            m.Resize(w, h).Save(o.Need("out"));
            return 0;
        }

        // ------------------------------------------------------------------------------------
        // shape: exact geometric alpha
        private static int ShapeCmd(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            string type = o.Get("type", "roundrect");
            double inset = o.Num("inset", 0), radius = o.Num("radius", 0);
            Matte.BleedColor(m, 0.5f);
            var shape = new Shape(type, inset, inset, m.W - 2 * inset, m.H - 2 * inset, radius);
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++) m.A[y * m.W + x] = shape.Coverage(x + 0.5, y + 0.5);
            }

            for (int i = 0; i < m.Count; i++) if (m.A[i] <= 0f) { m.R[i] = 0; m.G[i] = 0; m.B[i] = 0; }
            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {shape}");
            return 0;
        }

        // ------------------------------------------------------------------------------------
        // nine: compact 9-slice with a plain middle
        private static int Nine(Opts o)
        {
            Img f = Img.Load(o.Need("in"));
            int[] b = o.Ints("border", 4);
            int[] mid = o.Ints("mid", 2, new[] { 32, 32 });
            Img n = BuildNine(f, b[0], b[1], b[2], b[3], mid[0], mid[1]);
            n.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {f.W}x{f.H} -> {n.W}x{n.H}  border L{b[0]} T{b[1]} R{b[2]} B{b[3]}");
            return 0;
        }

        private static int Flatten(Opts o)
        {
            // Squeezes the middle to a few pixels and stretches it back: what is left between the
            // borders is exactly what the game will draw there.
            Img f = Img.Load(o.Need("in"));
            int[] b = o.Ints("border", 4);
            int[] mid = o.Ints("mid", 2, new[] { 32, 32 });
            Img n = BuildNine(f, b[0], b[1], b[2], b[3], mid[0], mid[1]);
            Img back = Render(n, b, 1, 1, f.W, f.H);
            back.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {back.W}x{back.H}  middle made plain inside L{b[0]} T{b[1]} R{b[2]} B{b[3]}");
            return 0;
        }

        /// <summary>
        /// Keeps the borders as they are and squeezes what lies between them down to a few pixels
        /// by averaging along the direction it will stretch: grain and stray marks in the middle
        /// average out, a smooth gradient survives and comes back when the game stretches the
        /// sprite. With no left/right (or top/bottom) border that direction is left alone.
        /// </summary>
        private static Img BuildNine(Img f, int L, int T, int R, int B, int midX, int midY)
        {
            Img a = L + R > 0 ? Squeeze(f, L, R, midX, true) : f;
            Img n = T + B > 0 ? Squeeze(a, T, B, midY, false) : a;
            Regularize(n, L + R > 0 ? L : 0, L + R > 0 ? midX : 0, T + B > 0 ? T : 0, T + B > 0 ? midY : 0);
            return n;
        }

        /// <summary>
        /// Makes the squeezed middle exactly plain along the direction it stretches: every middle
        /// column of a top/bottom strip becomes the strip's mean column, the centre keeps only its
        /// top-to-bottom course, and the side strips their cross-section plus that course. What
        /// is left of ChatGPT's faint unevenness would otherwise come back as soft blotches
        /// when the middle is stretched over a whole panel.
        /// </summary>
        private static void Regularize(Img n, int L, int midX, int T, int midY)
        {
            float[][] ch = { n.R, n.G, n.B, n.A };
            int w = n.W, h = n.H;
            if (midX > 0)
            {
                // Rows: all middle columns take the row's mean over the middle (premultiplied).
                for (int y = 0; y < h; y++)
                {
                    double r = 0, g = 0, b = 0, a = 0;
                    for (int x = L; x < L + midX; x++)
                    {
                        int i = y * w + x;
                        r += n.R[i] * n.A[i]; g += n.G[i] * n.A[i]; b += n.B[i] * n.A[i]; a += n.A[i];
                    }

                    float fa = (float)(a / midX);
                    float fr = a > 1e-9 ? (float)(r / a) : 0f, fg = a > 1e-9 ? (float)(g / a) : 0f, fb = a > 1e-9 ? (float)(b / a) : 0f;
                    for (int x = L; x < L + midX; x++)
                    {
                        int i = y * w + x;
                        n.R[i] = fr; n.G[i] = fg; n.B[i] = fb; n.A[i] = fa;
                    }
                }
            }

            if (midY > 0)
            {
                // The centre's top-to-bottom course (its middle columns are already identical).
                int cx = midX > 0 ? L + midX / 2 : w / 2;
                var course = new float[4][];
                var mean = new double[4];
                for (int c = 0; c < 4; c++)
                {
                    course[c] = new float[midY];
                    for (int k = 0; k < midY; k++) { course[c][k] = ch[c][(T + k) * w + cx]; mean[c] += course[c][k]; }
                    mean[c] /= midY;
                }

                // Side strips (and, without left/right borders, everything): each column keeps its
                // mean over the middle rows and follows the centre's course around that mean.
                for (int x = 0; x < w; x++)
                {
                    if (midX > 0 && x >= L && x < L + midX) continue;
                    for (int c = 0; c < 4; c++)
                    {
                        double m = 0;
                        for (int k = 0; k < midY; k++) m += ch[c][(T + k) * w + x];
                        m /= midY;
                        for (int k = 0; k < midY; k++)
                        {
                            // Alpha and colour of the rim stay as they are; only opaque fill follows the course.
                            float v = c == 3 ? (float)m : (float)(m + (course[c][k] - mean[c]));
                            ch[c][(T + k) * w + x] = Math.Clamp(v, 0f, 1f);
                        }
                    }
                }
            }
        }

        private static Img Squeeze(Img f, int lead, int trail, int mid, bool alongX)
        {
            int len = alongX ? f.W : f.H, other = alongX ? f.H : f.W;
            int srcMid = len - lead - trail;
            if (srcMid <= 0) throw new ArgumentException("the borders leave no middle");
            if (mid >= srcMid) return f;
            int outLen = lead + mid + trail;
            var n = alongX ? new Img(outLen, other, 0f) : new Img(other, outLen, 0f);
            float[][] src = { f.R, f.G, f.B, f.A }, dst = { n.R, n.G, n.B, n.A };
            double step = (double)srcMid / mid;
            for (int q = 0; q < other; q++)
            {
                for (int p = 0; p < outLen; p++)
                {
                    int t = alongX ? q * n.W + p : p * n.W + q;
                    if (p < lead || p >= lead + mid)
                    {
                        int sp = p < lead ? p : p - (lead + mid) + (len - trail);
                        int s = alongX ? q * f.W + sp : sp * f.W + q;
                        for (int c = 0; c < 4; c++) dst[c][t] = src[c][s];
                        continue;
                    }

                    // Area average of the source span this output pixel stands for, premultiplied.
                    double a0 = lead + (p - lead) * step, a1 = a0 + step;
                    double r = 0, g = 0, b = 0, al = 0, wsum = 0;
                    for (int sp = (int)Math.Floor(a0); sp < Math.Ceiling(a1); sp++)
                    {
                        double wgt = Math.Min(a1, sp + 1) - Math.Max(a0, sp);
                        if (wgt <= 0) continue;
                        int s = alongX ? q * f.W + sp : sp * f.W + q;
                        r += f.R[s] * f.A[s] * wgt; g += f.G[s] * f.A[s] * wgt; b += f.B[s] * f.A[s] * wgt; al += f.A[s] * wgt; wsum += wgt;
                    }

                    dst[3][t] = (float)(al / wsum);
                    dst[0][t] = al > 1e-9 ? (float)(r / al) : 0f; dst[1][t] = al > 1e-9 ? (float)(g / al) : 0f; dst[2][t] = al > 1e-9 ? (float)(b / al) : 0f;
                }
            }

            return n;
        }

        private static float Smooth(float t) => t <= 0f ? 0f : (t >= 1f ? 1f : t * t * (3f - 2f * t));

        // ------------------------------------------------------------------------------------
        // rendering a sprite into a rectangle (plain stretch or 9-slice)
        private static Img Render(Img sprite, int[] nine, double unit, double zoom, double rectW, double rectH)
        {
            int ow = Math.Max(1, (int)Math.Round(rectW * zoom)), oh = Math.Max(1, (int)Math.Round(rectH * zoom));
            using Bitmap src = sprite.ToBitmap();
            using var dst = new Bitmap(ow, oh, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                // Bilinear, as the game filters: bicubic overshoots on thin bright rims and would
                // show them whiter than they are.
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.Clear(Color.Transparent);
                if (nine == null)
                {
                    DrawPatch(g, src, 0, 0, sprite.W, sprite.H, 0, 0, ow, oh);
                }
                else
                {
                    double k = zoom / unit;
                    double l = nine[0] * k, t = nine[1] * k, r = nine[2] * k, b = nine[3] * k;
                    if (l + r > ow) { double s = ow / (l + r); l *= s; r *= s; }
                    if (t + b > oh) { double s = oh / (t + b); t *= s; b *= s; }
                    int il = (int)Math.Round(l), ir = (int)Math.Round(r), it = (int)Math.Round(t), ib = (int)Math.Round(b);
                    int[] dx = { 0, il, ow - ir, ow }, dy = { 0, it, oh - ib, oh };
                    int[] sx = { 0, nine[0], sprite.W - nine[2], sprite.W }, sy = { 0, nine[1], sprite.H - nine[3], sprite.H };
                    for (int j = 0; j < 3; j++)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            if (dx[i + 1] <= dx[i] || dy[j + 1] <= dy[j] || sx[i + 1] <= sx[i] || sy[j + 1] <= sy[j]) continue;
                            DrawPatch(g, src, sx[i], sy[j], sx[i + 1] - sx[i], sy[j + 1] - sy[j], dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j]);
                        }
                    }
                }
            }

            using Bitmap straight = dst.Clone(new Rectangle(0, 0, ow, oh), PixelFormat.Format32bppArgb);
            return Img.FromBitmap(straight);
        }

        private static void DrawPatch(Graphics g, Bitmap src, int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh)
        {
            using Bitmap patch = src.Clone(new Rectangle(sx, sy, sw, sh), PixelFormat.Format32bppArgb);
            using var attrs = new ImageAttributes();
            attrs.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(patch, new Rectangle(dx, dy, dw, dh), 0, 0, sw, sh, GraphicsUnit.Pixel, attrs);
        }

        private static Img MockupView(string screen, double[] view, double zoom)
        {
            using var img = new Bitmap(MockupPath(screen));
            int ow = (int)Math.Round(view[2] * zoom), oh = (int)Math.Round(view[3] * zoom);
            using var bmp = new Bitmap(ow, oh, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = zoom >= 1 ? InterpolationMode.HighQualityBicubic : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Magenta);
                g.DrawImage(img, new RectangleF(0, 0, ow, oh), new RectangleF((float)view[0], (float)view[1], (float)view[2], (float)view[3]), GraphicsUnit.Pixel);
            }

            return Img.FromBitmap(bmp);
        }

        private static Img Flat(int w, int h, string hex)
        {
            var c = Img.ParseHex(hex);
            var m = new Img(w, h);
            for (int i = 0; i < m.Count; i++) { m.R[i] = c.r; m.G[i] = c.g; m.B[i] = c.b; }
            return m;
        }

        private static Img Tiles(IList<Img> tiles, int gap, string gapHex = "#FF00FF")
        {
            int w = tiles.Sum(t => t.W) + gap * (tiles.Count + 1), h = tiles.Max(t => t.H) + 2 * gap;
            Img sheet = Flat(w, h, gapHex);
            int x = gap;
            foreach (Img t in tiles)
            {
                sheet.Paste(t, x, gap);
                x += t.W + gap;
            }

            return sheet;
        }

        // ------------------------------------------------------------------------------------
        // check: mockup | sprite over mockup | sprite on black | sprite on white
        private static int Check(Opts o)
        {
            Img sprite = Img.Load(o.Need("sprite"));
            string screen = o.Need("screen");
            double[] rect = o.Nums("rect", 4);
            int[] nine = o.Ints("nine", 4);
            double unit = o.Num("unit", 2.3);
            double pad = o.Num("pad", 10);
            double[] view = o.Nums("view", 4, new[] { Math.Floor(rect[0] - pad), Math.Floor(rect[1] - pad), Math.Ceiling(rect[2] + 2 * pad), Math.Ceiling(rect[3] + 2 * pad) });
            double zoom = o.Num("zoom", Math.Min(3.0, Math.Max(1.0, Math.Floor(1100.0 / view[2] * 2) / 2)));

            Img mock = MockupView(screen, view, zoom);
            Img drawn = Render(sprite, nine, unit, zoom, rect[2], rect[3]);
            int ox = (int)Math.Round((rect[0] - view[0]) * zoom), oy = (int)Math.Round((rect[1] - view[1]) * zoom);
            Img comp = mock.Clone();
            comp.Over(drawn, ox, oy);
            Img black = Flat(mock.W, mock.H, "#000000"); black.Over(drawn, ox, oy);
            Img white = Flat(mock.W, mock.H, "#FFFFFF"); white.Over(drawn, ox, oy);
            var tiles = new List<Img> { mock, comp };
            if (!o.Has("noalpha")) { tiles.Add(black); tiles.Add(white); }
            Img sheet = o.Has("stack") ? Stack(tiles, 6) : (tiles.Count == 4 && tiles[0].W * 4 > 2000 ? Stack(new List<Img> { Tiles(tiles.GetRange(0, 2), 6), Tiles(tiles.GetRange(2, 2), 6) }, 0) : Tiles(tiles, 6));
            sheet.Save(o.Need("out"));

            // How far the composite is from the mockup inside the rect (text included, so read it as a trend).
            double e = 0; long k = 0;
            for (int y = Math.Max(0, oy); y < Math.Min(mock.H, oy + drawn.H); y++)
            {
                for (int x = Math.Max(0, ox); x < Math.Min(mock.W, ox + drawn.W); x++)
                {
                    int i = y * mock.W + x;
                    e += (Math.Abs(Img.ToSrgb01(comp.R[i]) - Img.ToSrgb01(mock.R[i])) + Math.Abs(Img.ToSrgb01(comp.G[i]) - Img.ToSrgb01(mock.G[i])) + Math.Abs(Img.ToSrgb01(comp.B[i]) - Img.ToSrgb01(mock.B[i]))) / 3.0;
                    k++;
                }
            }

            Console.WriteLine($"{o.Need("out")}  {sheet.W}x{sheet.H}  zoom {zoom:0.##}  view {view[0]},{view[1]},{view[2]},{view[3]}  mean abs error in rect {(k > 0 ? e / k * 255 : 0):0.00}");
            return 0;
        }

        private static Img Stack(IList<Img> tiles, int gap)
        {
            int w = tiles.Max(t => t.W) + 2 * gap, h = tiles.Sum(t => t.H) + gap * (tiles.Count + 1);
            Img sheet = Flat(w, h, "#FF00FF");
            int y = gap;
            foreach (Img t in tiles)
            {
                sheet.Paste(t, gap, y);
                y += t.H + gap;
            }

            return sheet;
        }

        // stretch: a 9-slice sprite drawn at a given size (mockup px), on a plate colour, on black and on white
        private static int Stretch(Opts o)
        {
            Img sprite = Img.Load(o.Need("sprite"));
            int[] nine = o.Ints("nine", 4);
            double unit = o.Num("unit", 2.3), zoom = o.Num("zoom", 1);
            double[] size = o.Nums("size", 2);
            Img drawn = Render(sprite, nine, unit, zoom, size[0], size[1]);
            int pad = 12;
            var tiles = new List<Img>();
            foreach (string bg in new[] { o.Get("bg", "#E9DDD0"), "#000000", "#FFFFFF" })
            {
                Img t = Flat(drawn.W + 2 * pad, drawn.H + 2 * pad, bg);
                t.Over(drawn, pad, pad);
                tiles.Add(t);
                if (o.Has("one")) break;
            }

            Img sheet = o.Has("stack") ? Stack(tiles, 6) : Tiles(tiles, 6);
            sheet.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {sheet.W}x{sheet.H}  drawn {drawn.W}x{drawn.H}");
            return 0;
        }

        private static int OnBg(Opts o)
        {
            Img sprite = Img.Load(o.Need("in"));
            double zoom = o.Num("zoom", 1);
            Img drawn = zoom == 1 ? sprite : sprite.Resize((int)Math.Round(sprite.W * zoom), (int)Math.Round(sprite.H * zoom));
            int pad = 10;
            var tiles = new List<Img>();
            foreach (string bg in (o.Get("bgs", "#000000,#FFFFFF,#FF00FF")).Split(','))
            {
                Img t = Flat(drawn.W + 2 * pad, drawn.H + 2 * pad, bg);
                t.Over(drawn, pad, pad);
                tiles.Add(t);
            }

            Img sheet = o.Has("stack") ? Stack(tiles, 6, "#808080") : Tiles(tiles, 6, "#808080");
            sheet.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {sheet.W}x{sheet.H}");
            return 0;
        }

        private static Img Stack(IList<Img> tiles, int gap, string hex)
        {
            int w = tiles.Max(t => t.W) + 2 * gap, h = tiles.Sum(t => t.H) + gap * (tiles.Count + 1);
            Img sheet = Flat(w, h, hex);
            int y = gap;
            foreach (Img t in tiles)
            {
                sheet.Paste(t, gap, y);
                y += t.H + gap;
            }

            return sheet;
        }

        // ------------------------------------------------------------------------------------
        // stats: colours of the sprite (drawn at its rect) and of the mockup in sample rects
        private static int Stats(Opts o)
        {
            Img sprite = Img.Load(o.Need("sprite"));
            string screen = o.Need("screen");
            double[] rect = o.Nums("rect", 4);
            int[] nine = o.Ints("nine", 4);
            double unit = o.Num("unit", 2.3);
            Img mock = Img.Load(MockupPath(screen));
            // Draw at mockup resolution on an integer grid.
            int rx = (int)Math.Round(rect[0]), ry = (int)Math.Round(rect[1]);
            Img drawn = Render(sprite, nine, unit, 1, rect[2], rect[3]);
            Console.WriteLine($"sample (mockup px)            sprite median / mean          mockup median / mean          median diff (mockup - sprite)");
            foreach (string s in o.Need("sample").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = s.Split('@');
                double[] r = parts[0].Split(',').Select(p => double.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
                double pct = parts.Length > 1 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : 50;
                var sr = new List<(byte r, byte g, byte b)>();
                var mr = new List<(byte r, byte g, byte b)>();
                for (int y = (int)r[1]; y < (int)(r[1] + r[3]); y++)
                {
                    for (int x = (int)r[0]; x < (int)(r[0] + r[2]); x++)
                    {
                        if (x < 0 || y < 0 || x >= mock.W || y >= mock.H) continue;
                        int mi = y * mock.W + x;
                        mr.Add((S8(mock.R[mi]), S8(mock.G[mi]), S8(mock.B[mi])));
                        int dx = x - rx, dy = y - ry;
                        if (dx < 0 || dy < 0 || dx >= drawn.W || dy >= drawn.H) continue;
                        int di = dy * drawn.W + dx;
                        if (drawn.A[di] < 0.98f) continue;
                        sr.Add((S8(drawn.R[di]), S8(drawn.G[di]), S8(drawn.B[di])));
                    }
                }

                (int r, int g, int b) sm = Pct(sr, pct), mm = Pct(mr, pct);
                Console.WriteLine($"{parts[0],-24} p{pct,-3:0}  #{sm.r:X2}{sm.g:X2}{sm.b:X2} / {Mean(sr)}  (n={sr.Count,5})   #{mm.r:X2}{mm.g:X2}{mm.b:X2} / {Mean(mr)}  (n={mr.Count,5})   {mm.r - sm.r,4} {mm.g - sm.g,4} {mm.b - sm.b,4}");
            }

            return 0;
        }

        /// <summary>The colour at a brightness percentile (per channel, after sorting by luminance).</summary>
        private static (int r, int g, int b) Pct(List<(byte r, byte g, byte b)> px, double pct)
        {
            if (px.Count == 0) return (0, 0, 0);
            var sorted = px.OrderBy(p => 0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b).ToList();
            int n = sorted.Count, c = (int)Math.Clamp(Math.Round(pct / 100.0 * (n - 1)), 0, n - 1);
            int a = Math.Max(0, c - Math.Max(1, n / 40)), b = Math.Min(n - 1, c + Math.Max(1, n / 40));
            double r = 0, g = 0, bl = 0; int k = 0;
            for (int i = a; i <= b; i++) { r += sorted[i].r; g += sorted[i].g; bl += sorted[i].b; k++; }
            return ((int)Math.Round(r / k), (int)Math.Round(g / k), (int)Math.Round(bl / k));
        }

        private static string Mean(List<(byte r, byte g, byte b)> px)
        {
            if (px.Count == 0) return "-------";
            return $"#{(int)Math.Round(px.Average(p => (double)p.r)):X2}{(int)Math.Round(px.Average(p => (double)p.g)):X2}{(int)Math.Round(px.Average(p => (double)p.b)):X2}";
        }

        // recolor: per-channel gain/offset through anchor colours, in sRGB bytes
        private static int Recolor(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            var pairs = new List<(double[] s, double[] d)>();
            foreach (string a in o.Need("anchors").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = a.Split('>');
                pairs.Add((HexBytes(p[0]), HexBytes(p[1])));
            }

            string mode = o.Get("mode", pairs.Count >= 2 ? "affine" : "offset");
            var gain = new double[3];
            var off = new double[3];
            for (int c = 0; c < 3; c++)
            {
                if (mode == "offset")
                {
                    gain[c] = 1; off[c] = pairs.Average(p => p.d[c] - p.s[c]);
                }
                else if (mode == "gain")
                {
                    gain[c] = pairs.Average(p => p.d[c] / Math.Max(1, p.s[c])); off[c] = 0;
                }
                else
                {
                    double ms = pairs.Average(p => p.s[c]), md = pairs.Average(p => p.d[c]);
                    double num = pairs.Sum(p => (p.s[c] - ms) * (p.d[c] - md)), den = pairs.Sum(p => (p.s[c] - ms) * (p.s[c] - ms));
                    gain[c] = den > 1e-6 ? num / den : 1;
                    off[c] = md - gain[c] * ms;
                }
            }

            ApplyBytes(m, gain, off);
            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  mode {mode}  gain {gain[0]:0.000} {gain[1]:0.000} {gain[2]:0.000}  offset {off[0]:0.0} {off[1]:0.0} {off[2]:0.0}");
            return 0;
        }

        /// <summary>
        /// Gradient map: one channel of the extraction (the one that separates its tones best)
        /// picks a colour on a ramp measured on the mockup, e.g. ornament - fill - rim highlight.
        /// The result has exactly the mockup palette whatever hue ChatGPT drifted to. With
        /// --stops2 the ramp changes linearly from the top row (--stops) to the bottom row.
        /// </summary>
        private static int GradMap(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            string chName = o.Get("channel", "b");
            float[] ch = chName == "r" ? m.R : (chName == "g" ? m.G : m.B);
            (double v, double[] c)[] top = ParseStops(o.Need("stops"));
            (double v, double[] c)[] bottom = o.Has("stops2") ? ParseStops(o.Get("stops2")) : top;
            if (bottom.Length != top.Length) throw new ArgumentException("--stops and --stops2 need the same number of stops");
            double extra = o.Num("extrapolate", 0.5);
            for (int y = 0; y < m.H; y++)
            {
                double ty = m.H > 1 ? (double)y / (m.H - 1) : 0;
                for (int x = 0; x < m.W; x++)
                {
                    int i = y * m.W + x;
                    if (m.A[i] <= 0f) continue;
                    double v = Img.ToSrgb01(ch[i]) * 255.0;
                    int k = 0;
                    while (k < top.Length - 2 && v > top[k + 1].v) k++;
                    double t = (v - top[k].v) / (top[k + 1].v - top[k].v);
                    t = Math.Clamp(t, k == 0 ? -extra : 0, k == top.Length - 2 ? 1 : 1);
                    var outc = new double[3];
                    for (int c = 0; c < 3; c++)
                    {
                        double a = top[k].c[c] + (bottom[k].c[c] - top[k].c[c]) * ty;
                        double b = top[k + 1].c[c] + (bottom[k + 1].c[c] - top[k + 1].c[c]) * ty;
                        outc[c] = a + (b - a) * t;
                    }

                    m.R[i] = Img.ToLinear((float)Math.Clamp(outc[0] / 255.0, 0, 1));
                    m.G[i] = Img.ToLinear((float)Math.Clamp(outc[1] / 255.0, 0, 1));
                    m.B[i] = Img.ToLinear((float)Math.Clamp(outc[2] / 255.0, 0, 1));
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  gradient map on {chName}: {o.Need("stops")}{(o.Has("stops2") ? "  ->  " + o.Get("stops2") : "")}");
            return 0;
        }

        private static (double v, double[] c)[] ParseStops(string s)
        {
            return s.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p =>
            {
                string[] q = p.Split(':');
                return (double.Parse(q[0], CultureInfo.InvariantCulture), HexBytes(q[1]));
            }).OrderBy(p => p.Item1).ToArray();
        }

        /// <summary>
        /// How far anything that is not plain fill reaches in from each corner, ignoring the rim
        /// (the outer --edge pixels): the smallest 9-slice borders that keep the ornaments whole.
        /// A pixel is "not fill" when it differs from the median of its own row's middle fifth.
        /// </summary>
        private static int Extent(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            double thr = o.Num("thr", 3);
            int edge = o.Int("edge", 12);
            int w = m.W, h = m.H;
            var busy = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                var mid = new List<(byte r, byte g, byte b)>();
                for (int x = w * 2 / 5; x < w * 3 / 5; x++) { int i = y * w + x; mid.Add((S8(m.R[i]), S8(m.G[i]), S8(m.B[i]))); }
                (int r, int g, int b) f = Pct(mid, 50);
                for (int x = edge; x < w - edge; x++)
                {
                    int i = y * w + x;
                    if (m.A[i] < 0.99f) continue;
                    double d = Math.Max(Math.Abs(S8(m.R[i]) - f.r), Math.Max(Math.Abs(S8(m.G[i]) - f.g), Math.Abs(S8(m.B[i]) - f.b)));
                    busy[i] = d > thr;
                }
            }

            // Ignore isolated pixels: a busy pixel counts when at least 5 of its 3x3 neighbours are busy.
            bool Dense(int x, int y)
            {
                int k = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && yy >= 0 && xx < w && yy < h && busy[yy * w + xx]) k++;
                    }
                return k >= 5;
            }

            int[] reachX = new int[4], reachY = new int[4];   // TL, TR, BL, BR
            for (int y = edge; y < h - edge; y++)
            {
                for (int x = edge; x < w - edge; x++)
                {
                    if (!busy[y * w + x] || !Dense(x, y)) continue;
                    int c = (y < h / 2 ? 0 : 2) + (x < w / 2 ? 0 : 1);
                    int dx = x < w / 2 ? x + 1 : w - x, dy = y < h / 2 ? y + 1 : h - y;
                    reachX[c] = Math.Max(reachX[c], dx);
                    reachY[c] = Math.Max(reachY[c], dy);
                }
            }

            string[] names = { "top-left", "top-right", "bottom-left", "bottom-right" };
            for (int c = 0; c < 4; c++) Console.WriteLine($"{names[c],-13} reaches {reachX[c]} px in from the side, {reachY[c]} px in from the {(c < 2 ? "top" : "bottom")}");
            Console.WriteLine($"borders that keep them whole: L {Math.Max(reachX[0], reachX[2])} T {Math.Max(reachY[0], reachY[1])} R {Math.Max(reachX[1], reachX[3])} B {Math.Max(reachY[2], reachY[3])}  (image {w}x{h})");
            return 0;
        }

        /// <summary>
        /// The largest colour step between neighbouring pixels across each 9-slice line, compared
        /// with the largest step between neighbours just inside it: a seam shows as a step that
        /// is clearly larger across the line than beside it.
        /// </summary>
        private static int Seams(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            int[] b = o.Ints("nine", 4);
            int w = m.W, h = m.H;
            double Step(int i, int j)
            {
                if (m.A[i] < 0.99f || m.A[j] < 0.99f) return 0;
                return Math.Max(Math.Abs(S8(m.R[i]) - S8(m.R[j])), Math.Max(Math.Abs(S8(m.G[i]) - S8(m.G[j])), Math.Abs(S8(m.B[i]) - S8(m.B[j]))));
            }

            void Line(string name, bool vertical, int at)
            {
                if (at <= 1 || at >= (vertical ? w : h) - 1) return;
                double across = 0, beside = 0; int where = -1;
                int len = vertical ? h : w;
                for (int k = 0; k < len; k++)
                {
                    int i0 = vertical ? k * w + at - 1 : (at - 1) * w + k, i1 = vertical ? k * w + at : at * w + k;
                    int j0 = vertical ? k * w + at - 2 : (at - 2) * w + k;
                    double s = Step(i0, i1);
                    if (s > across) { across = s; where = k; }
                    beside = Math.Max(beside, Step(j0, i0));
                }

                Console.WriteLine($"  {name,-8} at {at,4}: largest step across {across:0} (at {where}), beside {beside:0}");
            }

            Console.WriteLine($"{Path.GetFileName(o.Need("in"))}  {w}x{h}");
            Line("left", true, b[0]);
            Line("right", true, w - b[2]);
            Line("top", false, b[1]);
            Line("bottom", false, h - b[3]);
            return 0;
        }

        private static int Line(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            bool row = o.Has("row");
            int at = o.Int(row ? "row" : "col", 0), from = o.Int("from", 0), to = o.Int("to", (row ? m.W : m.H) - 1);
            var sb = new StringBuilder();
            for (int k = Math.Max(0, from); k <= Math.Min((row ? m.W : m.H) - 1, to); k++)
            {
                int i = row ? at * m.W + k : k * m.W + at;
                sb.Append($"{k}:{Img.Hex(m.R[i], m.G[i], m.B[i]).Substring(1)}/{(int)Math.Round(m.A[i] * 255)} ");
            }

            Console.WriteLine(sb.ToString());
            return 0;
        }

        private static void ApplyBytes(Img m, double[] gain, double[] off)
        {
            float[][] ch = { m.R, m.G, m.B };
            for (int i = 0; i < m.Count; i++)
            {
                if (m.A[i] <= 0f) continue;
                for (int c = 0; c < 3; c++)
                {
                    double v = Img.ToSrgb01(ch[c][i]) * 255.0 * gain[c] + off[c];
                    ch[c][i] = Img.ToLinear((float)Math.Clamp(v / 255.0, 0, 1));
                }
            }
        }

        private static double[] HexBytes(string hex)
        {
            hex = hex.Trim().TrimStart('#');
            return new double[] { Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16) };
        }

        // states: a tinted / darkened / desaturated copy that lines up with the original to the pixel
        private static int States(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            float[][] ch = { m.R, m.G, m.B };
            if (o.Has("darken"))
            {
                double k = 1 - o.Num("darken", 0.08);
                ApplyBytes(m, new[] { k, k, k }, new double[] { 0, 0, 0 });
            }

            if (o.Has("desaturate"))
            {
                float k = (float)o.Num("desaturate", 0.6);
                for (int i = 0; i < m.Count; i++)
                {
                    float r = Img.ToSrgb01(m.R[i]), g = Img.ToSrgb01(m.G[i]), b = Img.ToSrgb01(m.B[i]);
                    float l = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                    m.R[i] = Img.ToLinear(r + (l - r) * k); m.G[i] = Img.ToLinear(g + (l - g) * k); m.B[i] = Img.ToLinear(b + (l - b) * k);
                }
            }

            m.Save(o.Need("out"));
            Console.WriteLine($"{o.Need("out")}  {m.W}x{m.H}");
            return 0;
        }

        // ------------------------------------------------------------------------------------
        // export
        private static int Export(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            string name = o.Need("name").Replace('\\', '/');
            int w = o.Int("w", m.W), h = o.Int("h", m.H);
            if (w != m.W || h != m.H)
            {
                Console.WriteLine($"  resize {m.W}x{m.H} -> {w}x{h}  (x{(double)w / m.W:0.000}, y{(double)h / m.H:0.000})");
                m = m.Resize(w, h);
            }

            if (m.W % 2 != 0 || m.H % 2 != 0) Console.WriteLine($"  WARNING: odd size {m.W}x{m.H}");
            for (int i = 0; i < m.Count; i++) if (m.A[i] < 1.5f / 255f) m.A[i] = 0f;
            Matte.BleedColor(m, 0.004f);
            string asset = Path.Combine(_repo, "Assets", "_Project", "Art", name);
            string export = Path.Combine(_repo, "ArtSource", "Exports", "UI", Path.GetFileName(name));
            m.Save(asset);
            m.Save(export);
            Console.WriteLine($"{name}  {m.W}x{m.H}  -> Assets/_Project/Art/{name} and ArtSource/Exports/UI/{Path.GetFileName(name)}");
            return 0;
        }

        // ------------------------------------------------------------------------------------
        // sheet: crops of mockups on a grid, for one request that covers several small elements
        private static int Sheet(Opts o)
        {
            var crops = new List<Img>();
            foreach (string item in o.Need("items").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = item.Split(':');
                double[] r = p[1].Split(',').Select(v => double.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToArray();
                double scale = p.Length > 2 ? double.Parse(p[2], CultureInfo.InvariantCulture) : 4;
                crops.Add(MockupView(p[0], r, scale));
            }

            int cols = o.Int("cols", 4), gap = o.Int("gap", 40);
            int cw = o.Int("cellw", crops.Max(c => c.W)), chh = o.Int("cellh", crops.Max(c => c.H));
            int rows = (crops.Count + cols - 1) / cols;
            Img sheet = Flat(cols * cw + (cols + 1) * gap, rows * chh + (rows + 1) * gap, o.Get("bg", "#FFFFFF"));
            var sb = new StringBuilder();
            for (int k = 0; k < crops.Count; k++)
            {
                int cx = gap + (k % cols) * (cw + gap), cy = gap + (k / cols) * (chh + gap);
                int x = cx + (cw - crops[k].W) / 2, y = cy + (chh - crops[k].H) / 2;
                sheet.Paste(crops[k], x, y);
                sb.AppendLine($"{k}: cell {cx},{cy} {cw}x{chh}  crop at {x},{y} {crops[k].W}x{crops[k].H}");
            }

            sheet.Save(o.Need("out"));
            File.WriteAllText(Path.ChangeExtension(o.Need("out"), ".cells.txt"), sb.ToString());
            Console.WriteLine($"{o.Need("out")}  {sheet.W}x{sheet.H}  {crops.Count} crops, {cols} columns");
            Console.Write(sb.ToString());
            return 0;
        }

        // split: separate the elements of a returned grid (already cut), left to right, top to bottom
        private static int Split(Opts o)
        {
            Img m = Img.Load(o.Need("in"));
            string[] names = o.Need("names").Split(',');
            int min = o.Int("min", 200), grow = o.Int("grow", 6);
            var comps = Components(m, 8f / 255f, grow).Where(c => c.px.Count >= min).Select(c => c.box).ToList();
            // Rows: cluster by vertical overlap, then order each row by x.
            comps.Sort((a, b) => a.Y.CompareTo(b.Y));
            var rows = new List<List<Box>>();
            foreach (Box b in comps)
            {
                List<Box> row = rows.FirstOrDefault(r => r.Any(q => Math.Min(q.Bottom, b.Bottom) - Math.Max(q.Y, b.Y) > 0.4 * Math.Min(q.H, b.H)));
                if (row == null) { row = new List<Box>(); rows.Add(row); }
                row.Add(b);
            }

            var ordered = rows.OrderBy(r => r.Min(q => q.Y)).SelectMany(r => r.OrderBy(q => q.X)).ToList();
            Console.WriteLine($"{ordered.Count} elements found ({names.Length} names)");
            Directory.CreateDirectory(o.Need("outdir"));
            for (int k = 0; k < ordered.Count; k++)
            {
                Box g = ordered[k];
                // The grown box is looser than the content; trim it again on the real alpha.
                Img part = m.Crop(g.X, g.Y, g.W, g.H);
                Box t = AlphaBox(part, 8f / 255f);
                part = part.Crop(t.X, t.Y, t.W, t.H);
                string name = k < names.Length ? names[k] : $"extra{k}";
                string path = Path.Combine(o.Need("outdir"), name + ".png");
                part.Save(path);
                Console.WriteLine($"  {name,-28} at {g.X + t.X},{g.Y + t.Y}  {part.W}x{part.H}");
            }

            return 0;
        }
    }
}
