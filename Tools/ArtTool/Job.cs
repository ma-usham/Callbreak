using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArtTool
{
    /// <summary>
    /// Rebuilds one mockup as layers: a clean background plate, then every UI element back to
    /// front, each cut from the mockup with its children erased, its edges separated from what is
    /// behind it, and its drop shadow extracted as its own sprite. Re-composing the layers over the
    /// plate must give back the mockup (text aside), and the job reports how closely it does.
    /// Writes the sprites, the plate, layout.json for the Unity screen builder, a composite and a
    /// difference image, and handoff.csv rows.
    /// </summary>
    internal sealed class Job
    {
        public const double MockW = 1672, MockH = 941;
        public const double CanvasPerMock = 1920.0 / 1672.0;
        public const double PpuAt1x = 100.0 / CanvasPerMock;      // a 1× mockup-pixel sprite shows at its mockup size on the 1080p canvas

        private readonly string _repo;
        private readonly string _specPath;
        private readonly JsonObject _spec;
        private readonly string _name;
        private readonly string _mockupFile;
        private readonly List<Node> _nodes = new List<Node>();
        private readonly Dictionary<string, Node> _byId = new Dictionary<string, Node>();
        private readonly StringBuilder _log = new StringBuilder();
        private Img _m;

        private string ScreenDir => Path.Combine(_repo, "ArtSource", "Screens", _name);
        private string AssetsArt => Path.Combine(_repo, "Assets", "_Project", "Art");
        private string OutDir => _spec["outDir"]?.ToString() ?? $"Screens/{_name}";
        private string AssetsDir => Path.Combine(AssetsArt, OutDir.Replace('/', Path.DirectorySeparatorChar));
        private string ExportsDir => Path.Combine(_repo, "ArtSource", "Exports", OutDir.Replace('/', Path.DirectorySeparatorChar));
        private string AssetPathFor(string name) => $"Assets/_Project/Art/{OutDir}/{name}.png";

        /// <summary>"screen" (default): this screen's own plate; "shared": the shared menu background; "none": no background (kit jobs).</summary>
        private string BackgroundMode => _spec["background"]?.ToString() ?? "screen";

        public const string SharedMenuBackground = "Assets/_Project/Art/Backgrounds/BG_Menu_Wide.png";
        private string ScreenNumber => _mockupFile.Length >= 2 ? _mockupFile.Substring(0, 2) : "";

        public Job(string repo, string specPath)
        {
            _repo = repo;
            _specPath = specPath;
            _spec = JsonNode.Parse(File.ReadAllText(specPath), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }).AsObject();
            _name = (string)_spec["name"];
            _mockupFile = (string)_spec["mockup"];
        }

        private sealed class Node
        {
            public JsonObject Raw;
            public string Id, Kind, ParentId, Shape, Out, Use, Ink;
            public double X, Y, W, H, Radius, ShadowPad, Grow;
            public int Scale = 1, Pad = 3;
            public bool Hole = true, Solid;
            public float Lo = 0.03f, Hi = 0.09f;
            public JsonObject Make;
            public List<double[]> Erase = new List<double[]>();
            public Node Parent;
            public readonly List<Node> Children = new List<Node>();

            // Phase A
            public Box Region;
            public float[] Footprint;          // over Region
            public float[] Alpha;              // mattes, over Region
            public (float r, float g, float b) InkColor;
            public bool HasInk;

            // Label measurements (mockup px)
            public double[] InkBox;
            public double CapTop, Baseline;
            public string ColorHex, ColorTopHex, ColorBottomHex;

            // Outputs
            public string SpritePath, ShadowPath;
            public double[] SpriteRect, ShadowRect;
            public double SpritePpu, ShadowPpu;
            public double Error = double.NaN;

            public bool IsLabel => Kind == "label";
            public bool IsGroup => Kind == "group";
            public bool IsCut => !string.IsNullOrEmpty(Out);
            public bool IsGeometric => Shape == "rect" || Shape == "roundrect" || Shape == "pill" || Shape == "ellipse";
            public bool IsMatte => Shape == "ink" || Shape == "diff";
            public Shape Geometry => new Shape(Shape, X, Y, W, H, Radius);
        }

        public int Run(bool writeAssets)
        {
            string mockupPath = Path.Combine(_repo, "Assets", "ArtRefrences", "game-screens", _mockupFile);
            _m = Img.Load(mockupPath);
            Parse();
            foreach (Node n in _nodes) Discover(n);

            Img plate = BuildPlate();
            Img composite = plate.Clone();
            var labelMask = new float[_m.Count];
            foreach (Node n in _nodes)
            {
                if (n.IsLabel)
                {
                    StampMask(labelMask, n.Region, n.Footprint);
                    continue;
                }

                if (n.IsCut) CutNode(n, composite, writeAssets);
                else if (!string.IsNullOrEmpty(n.Use)) PlaceUsed(n, composite, writeAssets);
                else if (n.Make != null) MakeNode(n, composite, writeAssets);
            }

            Directory.CreateDirectory(Path.Combine(ScreenDir, "check"));
            string platePath = Path.Combine(AssetsArt, "Backgrounds", $"BG_{_name}.png");
            if (writeAssets && BackgroundMode == "screen")
            {
                Img wide = Extend(plate, ExtendX, ExtendY, _spec["plate"]?["generatedWide"]?.ToString());
                wide.Save(platePath);
                wide.Save(Path.Combine(_repo, "ArtSource", "Exports", "Backgrounds", $"BG_{_name}.png"));
                Handoff($"Backgrounds/BG_{_name}.png", "bg", wide.W, wide.H, _spec["plate"]?["generated"] != null ? "BG-1 + patch" : "inpainted from mockup",
                    "reference pixels outside the UI; the UI area filled; margins for 21:9 and 4:3");
            }

            Verify(composite, labelMask);
            WriteLayout(BackgroundMode == "screen" ? platePath : null);
            File.WriteAllText(Path.Combine(ScreenDir, "check", "report.txt"), _log.ToString());
            Console.Write(_log.ToString());
            return 0;
        }

        // ------------------------------------------------------------------ parsing

        private void Parse()
        {
            foreach (JsonNode jn in _spec["nodes"].AsArray())
            {
                JsonObject o = jn.AsObject();
                var n = new Node { Raw = o };
                n.Id = Str(o, "id");
                n.Kind = Str(o, "kind") ?? "sprite";
                n.ParentId = Str(o, "parent");
                n.Out = Str(o, "out");
                n.Use = Str(o, "use");
                n.Ink = Str(o, "ink") ?? "dark";
                n.Make = o["make"] as JsonObject;
                double[] r = Arr(o, "rect");
                if (r != null) { n.X = r[0]; n.Y = r[1]; n.W = r[2]; n.H = r[3]; }
                n.Shape = Str(o, "shape") ?? (n.IsLabel || n.IsGroup ? "none" : "rect");
                n.Radius = Num(o, "radius", 0);
                // Static elements keep their soft shadow in whatever is behind them (the plate or
                // their parent), so only their own footprint is erased. Elements that move, appear
                // or disappear at runtime ask for "shadow": pad, which erases the shadow too and
                // exports it as its own sprite.
                n.ShadowPad = Num(o, "shadow", 0);
                n.Grow = Num(o, "grow", n.ShadowPad > 0 ? n.ShadowPad : 1.5);
                n.Scale = (int)Num(o, "scale", 1);
                n.Pad = (int)Num(o, "pad", n.IsLabel ? 3 : 2);
                n.Hole = o["hole"] == null || (bool)o["hole"];
                n.Solid = o["solid"] != null && (bool)o["solid"];
                n.Lo = (float)Num(o, "lo", 0.03);
                n.Hi = (float)Num(o, "hi", 0.09);
                if (o["erase"] is JsonArray ea)
                {
                    foreach (JsonNode e in ea) n.Erase.Add(e.AsArray().Select(v => (double)v).ToArray());
                }

                if (string.IsNullOrEmpty(n.Id)) throw new InvalidDataException("every node needs an id");
                if (_byId.ContainsKey(n.Id)) throw new InvalidDataException($"duplicate id {n.Id}");
                _byId[n.Id] = n;
                _nodes.Add(n);
            }

            foreach (Node n in _nodes)
            {
                if (string.IsNullOrEmpty(n.ParentId)) continue;
                if (!_byId.TryGetValue(n.ParentId, out Node p)) throw new InvalidDataException($"{n.Id}: unknown parent {n.ParentId}");
                n.Parent = p;
                p.Children.Add(n);
            }
        }

        private static string Str(JsonObject o, string key) => o[key] is JsonValue v ? v.ToString() : null;

        private static double Num(JsonObject o, string key, double fallback) => o[key] is JsonValue v ? (double)v : fallback;

        private static double[] Arr(JsonObject o, string key) => o[key] is JsonArray a ? a.Select(v => (double)v).ToArray() : null;

        // ------------------------------------------------------------------ phase A: footprints

        private void Discover(Node n)
        {
            if (n.IsGroup || n.W <= 0 || n.H <= 0) return;
            if (n.IsLabel || n.IsMatte)
            {
                const int ring = 4;
                n.Region = Box.Around(n.X, n.Y, n.W, n.H, ring + n.Pad).Clamp(_m.W, _m.H);
                Img mr = _m.Crop(n.Region.X, n.Region.Y, n.Region.W, n.Region.H);
                float[] inside = RectMask(n.Region, n.X, n.Y, n.W, n.H);
                Img bg = mr.Clone();
                Inpaint.Fill(bg, inside, null, grain: false, relax: 240);

                if (n.Shape == "diff")
                {
                    n.Alpha = Matte.DiffAlpha(mr, bg, n.Lo, n.Hi, n.Solid);
                    for (int i = 0; i < n.Alpha.Length; i++) n.Alpha[i] *= inside[i];
                }
                else
                {
                    bool dark = n.Ink != "light";
                    n.InkColor = n.Ink.StartsWith("#") ? Img.ParseHex(n.Ink) : Matte.InkColor(Masked(mr, inside), bg, dark);
                    n.HasInk = true;
                    n.Alpha = Matte.InkCoverage(mr, bg, n.InkColor);
                    for (int i = 0; i < n.Alpha.Length; i++) n.Alpha[i] *= inside[i];
                }

                var hard = new float[n.Alpha.Length];
                for (int i = 0; i < hard.Length; i++) hard[i] = n.Alpha[i] > 0.08f ? 1f : 0f;
                n.Footprint = Inpaint.Dilate(hard, n.Region.W, n.Region.H, n.Pad);
                if (n.IsLabel) MeasureLabel(n, mr);
                return;
            }

            if (n.IsGeometric || !string.IsNullOrEmpty(n.Use) || n.Make != null || n.Kind != "sprite")
            {
                Shape s = n.IsGeometric ? n.Geometry : new Shape("rect", n.X, n.Y, n.W, n.H, 0);
                n.Region = Box.Around(n.X, n.Y, n.W, n.H, n.Grow + 2).Clamp(_m.W, _m.H);
                n.Footprint = new float[n.Region.W * n.Region.H];
                for (int y = 0; y < n.Region.H; y++)
                {
                    for (int x = 0; x < n.Region.W; x++)
                    {
                        n.Footprint[y * n.Region.W + x] = s.Coverage(n.Region.X + x + 0.5, n.Region.Y + y + 0.5, 1, n.Grow);
                    }
                }
            }
        }

        private static Img Masked(Img m, float[] inside)
        {
            Img c = m.Clone();
            for (int i = 0; i < c.Count; i++) if (inside[i] < 0.5f) { c.R[i] = float.NaN; }
            // NaN rows are skipped by InkColor through the luminance test failing.
            return c;
        }

        private float[] RectMask(Box region, double x, double y, double w, double h)
        {
            var mask = new float[region.W * region.H];
            for (int j = 0; j < region.H; j++)
            {
                double py = region.Y + j + 0.5;
                for (int i = 0; i < region.W; i++)
                {
                    double px = region.X + i + 0.5;
                    double cx = Math.Clamp(Math.Min(px - x, x + w - px) + 0.5, 0, 1);
                    double cy = Math.Clamp(Math.Min(py - y, y + h - py) + 0.5, 0, 1);
                    mask[j * region.W + i] = (float)(cx * cy);
                }
            }

            return mask;
        }

        private void MeasureLabel(Node n, Img mr)
        {
            int w = n.Region.W, h = n.Region.H;
            var colMax = new float[w];
            var rowSum = new float[h];
            var rowMax = new float[h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float c = n.Alpha[y * w + x];
                    colMax[x] = Math.Max(colMax[x], c);
                    rowMax[y] = Math.Max(rowMax[y], c);
                    rowSum[y] += c;
                }
            }

            const float on = 0.15f;
            int x0 = Array.FindIndex(colMax, v => v >= on), x1 = Array.FindLastIndex(colMax, v => v >= on);
            int y0 = Array.FindIndex(rowMax, v => v >= on), y1 = Array.FindLastIndex(rowMax, v => v >= on);
            if (x0 < 0 || y0 < 0)
            {
                _log.AppendLine($"WARN {n.Id}: no ink found in its rect");
                return;
            }

            double left = n.Region.X + x0 + (1 - colMax[x0]);
            double right = n.Region.X + x1 + colMax[x1];
            double top = n.Region.Y + y0 + (1 - rowMax[y0]);
            double bottom = n.Region.Y + y1 + rowMax[y1];
            n.InkBox = new[] { Round2(left), Round2(top), Round2(right), Round2(bottom) };

            float peak = rowSum.Max();
            int capRow = y0, baseRow = y1;
            for (int y = y0; y <= y1; y++) if (rowSum[y] >= peak * 0.30f) { capRow = y; break; }
            for (int y = y1; y >= y0; y--) if (rowSum[y] >= peak * 0.45f) { baseRow = y; break; }
            n.CapTop = Round2(n.Region.Y + capRow);
            n.Baseline = Round2(n.Region.Y + baseRow + 1);

            // Colour: the core of the strokes, and separately the top and bottom thirds of the
            // ink box, to catch the vertical gradients the serif titles carry.
            (double r, double g, double b, int k) all = (0, 0, 0, 0), topC = (0, 0, 0, 0), botC = (0, 0, 0, 0);
            double third = (y1 - y0 + 1) / 3.0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * w + x;
                    if (n.Alpha[i] < 0.9f) continue;
                    all = (all.r + mr.R[i], all.g + mr.G[i], all.b + mr.B[i], all.k + 1);
                    if (y < y0 + third) topC = (topC.r + mr.R[i], topC.g + mr.G[i], topC.b + mr.B[i], topC.k + 1);
                    else if (y > y1 - third) botC = (botC.r + mr.R[i], botC.g + mr.G[i], botC.b + mr.B[i], botC.k + 1);
                }
            }

            if (all.k == 0)
            {
                n.ColorHex = Img.Hex(n.InkColor.r, n.InkColor.g, n.InkColor.b);
                return;
            }

            n.ColorHex = Img.Hex((float)(all.r / all.k), (float)(all.g / all.k), (float)(all.b / all.k));
            if (topC.k > 4 && botC.k > 4)
            {
                string t = Img.Hex((float)(topC.r / topC.k), (float)(topC.g / topC.k), (float)(topC.b / topC.k));
                string b = Img.Hex((float)(botC.r / botC.k), (float)(botC.g / botC.k), (float)(botC.b / botC.k));
                if (HexDistance(t, b) >= 14)
                {
                    n.ColorTopHex = t;
                    n.ColorBottomHex = b;
                }
            }
        }

        private static int HexDistance(string a, string b)
        {
            int d = 0;
            for (int c = 0; c < 3; c++)
            {
                d = Math.Max(d, Math.Abs(Convert.ToInt32(a.Substring(1 + c * 2, 2), 16) - Convert.ToInt32(b.Substring(1 + c * 2, 2), 16)));
            }

            return d;
        }

        private static double Round2(double v) => Math.Round(v, 2);

        // ------------------------------------------------------------------ phase B: layers

        /// <summary>The nodes whose footprint is erased from <paramref name="host"/> (null = the plate).</summary>
        private IEnumerable<Node> ErasedFrom(Node host)
        {
            IEnumerable<Node> starts = host == null ? _nodes.Where(n => n.Parent == null) : host.Children;
            var stack = new Stack<Node>(starts.Reverse());
            while (stack.Count > 0)
            {
                Node n = stack.Pop();
                if (n.Hole && n.Footprint != null) yield return n;
                if (n.IsCut) continue;                     // a cut node erases its own subtree from itself
                foreach (Node c in Enumerable.Reverse(n.Children)) stack.Push(c);
            }
        }

        private Img BuildPlate()
        {
            var hole = new float[_m.Count];
            foreach (Node n in ErasedFrom(null)) StampMask(hole, n.Region, n.Footprint);

            Img plate = _m.Clone();
            string generated = _spec["plate"]?["generated"]?.ToString();
            if (!string.IsNullOrEmpty(generated))
            {
                Img g = Img.Load(Path.Combine(_repo, generated));
                if (g.W != _m.W || g.H != _m.H) g = g.Resize(_m.W, _m.H);
                MatchColors(g, _m, hole);
                int feather = (int)(_spec["plate"]?["feather"] is JsonValue fv ? (double)fv : 4);
                float[] soft = Inpaint.Blur(Inpaint.Dilate(hole, _m.W, _m.H, feather), _m.W, _m.H, Math.Max(1, feather / 2));
                for (int i = 0; i < _m.Count; i++)
                {
                    float a = Math.Max(hole[i], soft[i]);
                    plate.R[i] = _m.R[i] * (1 - a) + g.R[i] * a;
                    plate.G[i] = _m.G[i] * (1 - a) + g.G[i] * a;
                    plate.B[i] = _m.B[i] * (1 - a) + g.B[i] * a;
                }

                _log.AppendLine($"plate: generated image blended into the UI area ({generated})");
            }
            else
            {
                Inpaint.Fill(plate, hole, null, grain: true, relax: 400);
                _log.AppendLine("plate: UI area inpainted from the surroundings");
            }

            int holes = hole.Count(v => v > 0.5f);
            _log.AppendLine($"plate: {holes} px replaced ({100.0 * holes / _m.Count:0.0}% of the screen)");
            return plate;
        }

        /// <summary>
        /// Margins that take the 1672 × 941 design area to 2560 × 1440 on the canvas (GDD §12: the
        /// full design area always shows; wider phones and tablets see more of the background).
        /// </summary>
        public const int ExtendX = 279, ExtendY = 157;

        /// <summary>
        /// Surrounds the plate with margins for other aspect ratios. A generated wide plate
        /// (Designer Handbook BG-2) is used when there is one, with the reference-derived centre
        /// kept; otherwise the margins mirror the plate's edges and fade into a growing blur, which
        /// reads as out-of-focus scenery.
        /// </summary>
        private Img Extend(Img core, int extendX, int extendY, string wide) => ExtendPlate(core, extendX, extendY, wide == null ? null : Path.Combine(_repo, wide), _log);

        public static Img ExtendPlate(Img core, int ExtendX, int ExtendY, string wide, StringBuilder _log)
        {
            int w = core.W + 2 * ExtendX, h = core.H + 2 * ExtendY;
            Img ext;
            if (!string.IsNullOrEmpty(wide) && File.Exists(wide))
            {
                ext = Img.Load(wide);
                if (ext.W != w || ext.H != h) ext = ext.Resize(w, h);
                _log.AppendLine($"plate: margins from {wide}");
            }
            else
            {
                ext = new Img(w, h);
                Img[] blurred = { core, BlurImg(core, 3), BlurImg(core, 8), BlurImg(core, 18) };
                for (int y = 0; y < h; y++)
                {
                    int sy = Reflect(y - ExtendY, core.H);
                    double dy = Math.Max(0, Math.Max(ExtendY - y, y - (ExtendY + core.H - 1)));
                    for (int x = 0; x < w; x++)
                    {
                        int sx = Reflect(x - ExtendX, core.W);
                        double dx = Math.Max(0, Math.Max(ExtendX - x, x - (ExtendX + core.W - 1)));
                        double d = Math.Sqrt(dx * dx + dy * dy);
                        double t = Math.Min(3, d / 60.0);
                        int lo = (int)Math.Floor(t), hi = Math.Min(3, lo + 1);
                        float f = (float)(t - lo);
                        int s = sy * core.W + sx, i = y * w + x;
                        ext.R[i] = blurred[lo].R[s] * (1 - f) + blurred[hi].R[s] * f;
                        ext.G[i] = blurred[lo].G[s] * (1 - f) + blurred[hi].G[s] * f;
                        ext.B[i] = blurred[lo].B[s] * (1 - f) + blurred[hi].B[s] * f;
                    }
                }

                _log.AppendLine("plate: margins mirrored and blurred (no generated wide plate yet)");
            }

            // The centre is always the reference-derived plate, feathered into the margins.
            for (int y = 0; y < core.H; y++)
            {
                for (int x = 0; x < core.W; x++)
                {
                    int edge = Math.Min(Math.Min(x, core.W - 1 - x), Math.Min(y, core.H - 1 - y));
                    float a = edge >= 6 ? 1f : (edge + 1) / 7f;
                    int s = y * core.W + x, i = (y + ExtendY) * w + x + ExtendX;
                    ext.R[i] = ext.R[i] * (1 - a) + core.R[s] * a;
                    ext.G[i] = ext.G[i] * (1 - a) + core.G[s] * a;
                    ext.B[i] = ext.B[i] * (1 - a) + core.B[s] * a;
                }
            }

            return ext;
        }

        private static int Reflect(int v, int n)
        {
            if (n <= 1) return 0;
            int period = 2 * (n - 1);
            v = ((v % period) + period) % period;
            return v < n ? v : period - v;
        }

        private static Img BlurImg(Img src, int r)
        {
            var o = new Img(src.W, src.H);
            Array.Copy(Inpaint.Blur(src.R, src.W, src.H, r), o.R, src.Count);
            Array.Copy(Inpaint.Blur(src.G, src.W, src.H, r), o.G, src.Count);
            Array.Copy(Inpaint.Blur(src.B, src.W, src.H, r), o.B, src.Count);
            return o;
        }

        /// <summary>Per-channel gain and offset that bring a generated plate's colours to the reference outside the UI.</summary>
        private static void MatchColors(Img g, Img m, float[] hole)
        {
            for (int c = 0; c < 3; c++)
            {
                float[] gc = c == 0 ? g.R : c == 1 ? g.G : g.B;
                float[] mc = c == 0 ? m.R : c == 1 ? m.G : m.B;
                double sx = 0, sy = 0, sxx = 0, sxy = 0; long k = 0;
                for (int i = 0; i < gc.Length; i += 3)
                {
                    if (hole[i] > 0.01f) continue;
                    sx += gc[i]; sy += mc[i]; sxx += gc[i] * gc[i]; sxy += gc[i] * mc[i]; k++;
                }

                if (k < 100) continue;
                double den = k * sxx - sx * sx;
                if (Math.Abs(den) < 1e-9) continue;
                double gain = (k * sxy - sx * sy) / den, off = (sy - gain * sx) / k;
                gain = Math.Clamp(gain, 0.8, 1.25);
                for (int i = 0; i < gc.Length; i++) gc[i] = (float)Math.Clamp(gc[i] * gain + off, 0, 1);
            }
        }

        private void CutNode(Node n, Img composite, bool writeAssets)
        {
            Box box = (n.IsMatte ? n.Region : Box.Around(n.X, n.Y, n.W, n.H, Math.Max(n.ShadowPad, 1) + 2)).Clamp(_m.W, _m.H);
            if (n.IsMatte && n.ShadowPad > 0) box = Box.Around(n.X, n.Y, n.W, n.H, n.ShadowPad + 2).Clamp(_m.W, _m.H);
            Img own = _m.Crop(box.X, box.Y, box.W, box.H);
            Img behind = composite.Crop(box.X, box.Y, box.W, box.H);

            // The element's alpha over the box.
            var alpha = new float[box.W * box.H];
            if (n.IsGeometric)
            {
                Shape s = n.Geometry;
                for (int y = 0; y < box.H; y++)
                    for (int x = 0; x < box.W; x++)
                        alpha[y * box.W + x] = s.Coverage(box.X + x + 0.5, box.Y + y + 0.5);
            }
            else
            {
                CopyRegion(n.Alpha, n.Region, alpha, box);
            }

            // Erase children (and extra erase rects) from the element's own pixels, filling only
            // from the element's own interior.
            var childHole = new float[alpha.Length];
            foreach (Node c in ErasedFrom(n)) StampRegion(childHole, box, c.Region, c.Footprint);
            foreach (double[] e in n.Erase)
            {
                float[] rm = RectMask(box, e[0], e[1], e[2], e[3]);
                for (int i = 0; i < rm.Length; i++) childHole[i] = Math.Max(childHole[i], rm[i]);
            }

            if (childHole.Any(v => v > 0.001f))
            {
                var valid = new float[alpha.Length];
                for (int i = 0; i < valid.Length; i++) valid[i] = alpha[i] >= 0.99f && childHole[i] < 0.001f ? 1f : 0f;
                Inpaint.Fill(own, childHole, valid, grain: true, relax: 300, seed: n.Id.GetHashCode());
            }

            // Colour at the edges without the background's contribution.
            var color = new Img(box.W, box.H);
            if (n.HasInk && n.Shape == "ink")
            {
                for (int i = 0; i < color.Count; i++) { color.R[i] = n.InkColor.r; color.G[i] = n.InkColor.g; color.B[i] = n.InkColor.b; }
            }
            else
            {
                Matte.Decontaminate(own, behind, alpha, color);
            }

            Array.Copy(alpha, color.A, alpha.Length);

            // The shadow, solved against what is behind the element.
            Img shadowImg = null;
            if (n.ShadowPad > 0)
            {
                Img orig = _m.Crop(box.X, box.Y, box.W, box.H);
                (float[] sa, (float r, float g, float b) sc) = Matte.Shadow(orig, behind, alpha);
                Taper(sa, box.W, box.H, 3);
                shadowImg = new Img(box.W, box.H);
                for (int i = 0; i < sa.Length; i++) { shadowImg.R[i] = sc.r; shadowImg.G[i] = sc.g; shadowImg.B[i] = sc.b; shadowImg.A[i] = sa[i]; }
                composite.Over(shadowImg, box.X, box.Y);
            }

            composite.Over(color, box.X, box.Y);

            // Export the element trimmed to its own bounds.
            Box spriteBox = n.IsGeometric
                ? Box.Around(n.X, n.Y, n.W, n.H, 0).Clamp(_m.W, _m.H)
                : AlphaBounds(alpha, box, 0.004f);
            Img sprite = color.Crop(spriteBox.X - box.X, spriteBox.Y - box.Y, spriteBox.W, spriteBox.H);
            Matte.BleedColor(sprite);
            if (n.Scale > 1)
            {
                Img up = sprite.Clone();
                for (int i = 0; i < up.Count; i++) up.A[i] = 1f;
                up = up.Resize(spriteBox.W * n.Scale, spriteBox.H * n.Scale);
                if (n.IsGeometric)
                {
                    Shape s = n.Geometry;
                    double px = 1.0 / n.Scale;
                    for (int y = 0; y < up.H; y++)
                        for (int x = 0; x < up.W; x++)
                            up.A[y * up.W + x] = s.Coverage(spriteBox.X + (x + 0.5) * px, spriteBox.Y + (y + 0.5) * px, px);
                }
                else
                {
                    Img a = new Img(sprite.W, sprite.H);
                    for (int i = 0; i < a.Count; i++) { a.R[i] = sprite.A[i]; a.G[i] = sprite.A[i]; a.B[i] = sprite.A[i]; }
                    Img aUp = a.Resize(up.W, up.H);
                    for (int i = 0; i < up.Count; i++) up.A[i] = Math.Clamp(aUp.R[i] > 0 ? Img.ToSrgb01(aUp.R[i]) : 0f, 0f, 1f);
                }

                sprite = up;
            }

            n.SpriteRect = new double[] { spriteBox.X, spriteBox.Y, spriteBox.W, spriteBox.H };
            n.SpritePpu = Math.Round(PpuAt1x * n.Scale, 4);
            n.SpritePath = AssetPathFor(n.Out);
            if (writeAssets)
            {
                SaveBoth(sprite, n.Out);
                Handoff($"{OutDir}/{n.Out}.png", n.IsMatte && n.Shape == "ink" ? "icon" : "ui", sprite.W, sprite.H,
                    "cut from mockup", n.IsGeometric ? $"{n.Shape}; children erased" : $"{n.Shape} matte");
            }

            if (shadowImg != null)
            {
                Box sb = AlphaBounds(shadowImg.A, box, 0.004f);
                if (!sb.IsEmpty)
                {
                    Img sh = shadowImg.Crop(sb.X - box.X, sb.Y - box.Y, sb.W, sb.H);
                    n.ShadowRect = new double[] { sb.X, sb.Y, sb.W, sb.H };
                    n.ShadowPpu = Math.Round(PpuAt1x, 4);
                    n.ShadowPath = AssetPathFor(n.Out + "_Shadow");
                    if (writeAssets)
                    {
                        SaveBoth(sh, n.Out + "_Shadow");
                        Handoff($"{OutDir}/{n.Out}_Shadow.png", "ui", sh.W, sh.H, "extracted", "drop shadow solved against the clean background");
                    }
                }
            }
        }

        private void PlaceUsed(Node n, Img composite, bool writeAssets)
        {
            string path = ResolveUse(n.Use);
            n.SpritePath = ToAssetPath(path);
            n.SpritePpu = 0;

            // A matte node that reuses a sprite takes the rectangle its own ink occupies here, so
            // a trimmed icon lands exactly where this copy of it was drawn.
            Box place = n.IsMatte && n.Alpha != null ? AlphaBounds(n.Alpha, n.Region, 0.004f) : Box.Around(n.X, n.Y, n.W, n.H, 0);
            n.SpriteRect = n.IsMatte && n.Alpha != null
                ? new double[] { place.X, place.Y, place.W, place.H }
                : new[] { n.X, n.Y, n.W, n.H };

            if (n.ShadowPad > 0 && n.IsGeometric) ExtractShadow(n, composite, writeAssets, n.Out ?? n.Id);
            if (!File.Exists(path))
            {
                _log.AppendLine($"WARN {n.Id}: use '{n.Use}' not found at {path}");
                return;
            }

            int w = Math.Max(1, (int)Math.Round(n.SpriteRect[2])), h = Math.Max(1, (int)Math.Round(n.SpriteRect[3]));
            Img s = Img.Load(path).Resize(w, h);
            composite.Over(s, (int)Math.Round(n.SpriteRect[0]), (int)Math.Round(n.SpriteRect[1]));
        }

        /// <summary>The drop shadow of a geometric element that is drawn from another sprite.</summary>
        private void ExtractShadow(Node n, Img composite, bool writeAssets, string name)
        {
            Box box = Box.Around(n.X, n.Y, n.W, n.H, n.ShadowPad + 2).Clamp(_m.W, _m.H);
            Img behind = composite.Crop(box.X, box.Y, box.W, box.H);
            Img orig = _m.Crop(box.X, box.Y, box.W, box.H);
            var alpha = new float[box.W * box.H];
            Shape s = n.Geometry;
            for (int y = 0; y < box.H; y++)
                for (int x = 0; x < box.W; x++)
                    alpha[y * box.W + x] = s.Coverage(box.X + x + 0.5, box.Y + y + 0.5);
            (float[] sa, (float r, float g, float b) sc) = Matte.Shadow(orig, behind, alpha);
            Taper(sa, box.W, box.H, 3);
            var shadowImg = new Img(box.W, box.H);
            for (int i = 0; i < sa.Length; i++) { shadowImg.R[i] = sc.r; shadowImg.G[i] = sc.g; shadowImg.B[i] = sc.b; shadowImg.A[i] = sa[i]; }
            composite.Over(shadowImg, box.X, box.Y);
            Box sb = AlphaBounds(sa, box, 0.004f);
            if (sb.IsEmpty) return;
            Img sh = shadowImg.Crop(sb.X - box.X, sb.Y - box.Y, sb.W, sb.H);
            n.ShadowRect = new double[] { sb.X, sb.Y, sb.W, sb.H };
            n.ShadowPpu = Math.Round(PpuAt1x, 4);
            n.ShadowPath = AssetPathFor(name + "_Shadow");
            if (writeAssets)
            {
                SaveBoth(sh, name + "_Shadow");
                Handoff($"{OutDir}/{name}_Shadow.png", "ui", sh.W, sh.H, "extracted", "drop shadow solved against the clean background");
            }
        }

        private string ResolveUse(string use)
        {
            string local = Path.Combine(AssetsDir, use.EndsWith(".png") ? use : use + ".png");
            if (File.Exists(local)) return local;
            return Path.Combine(AssetsArt, use.Replace('/', Path.DirectorySeparatorChar));
        }

        private string ToAssetPath(string full) => Path.GetRelativePath(_repo, full).Replace('\\', '/');

        private void MakeNode(Node n, Img composite, bool writeAssets)
        {
            string name = n.Make["out"]?.ToString() ?? n.Id;
            Img hi = Vector.Make(n.Make, n.W, n.H, 2 * CanvasPerMock);
            n.SpritePath = AssetPathFor(name);
            n.SpriteRect = new[] { n.X, n.Y, n.W, n.H };
            n.SpritePpu = 200;
            if (writeAssets)
            {
                SaveBoth(hi, name);
                Handoff($"{OutDir}/{name}.png", "ui", hi.W, hi.H, "rebuilt", Vector.Describe(n.Make));
            }

            Img lo = hi.Resize(Math.Max(1, (int)Math.Round(n.W)), Math.Max(1, (int)Math.Round(n.H)));
            composite.Over(lo, (int)Math.Round(n.X), (int)Math.Round(n.Y));
        }

        private void SaveBoth(Img img, string name)
        {
            img.Save(Path.Combine(AssetsDir, name + ".png"));
            img.Save(Path.Combine(ExportsDir, name + ".png"));
        }

        // ------------------------------------------------------------------ verification

        private void Verify(Img composite, float[] labelMask)
        {
            Img check = composite.Clone();
            for (int i = 0; i < _m.Count; i++)
            {
                float a = labelMask[i];
                if (a <= 0f) continue;
                check.R[i] = check.R[i] * (1 - a) + _m.R[i] * a;
                check.G[i] = check.G[i] * (1 - a) + _m.G[i] * a;
                check.B[i] = check.B[i] * (1 - a) + _m.B[i] * a;
            }

            string dir = Path.Combine(ScreenDir, "check");
            check.Save(Path.Combine(dir, "composite.png"));
            var diff = new Img(_m.W, _m.H);
            double total = 0;
            for (int i = 0; i < _m.Count; i++)
            {
                float d = (Math.Abs(Img.ToSrgb01(check.R[i]) - Img.ToSrgb01(_m.R[i])) +
                           Math.Abs(Img.ToSrgb01(check.G[i]) - Img.ToSrgb01(_m.G[i])) +
                           Math.Abs(Img.ToSrgb01(check.B[i]) - Img.ToSrgb01(_m.B[i]))) / 3f;
                total += d;
                float v = Img.ToLinear(Math.Min(1f, d * 6f));
                diff.R[i] = v; diff.G[i] = v; diff.B[i] = v;
            }

            diff.Save(Path.Combine(dir, "diff-x6.png"));
            _log.AppendLine($"reconstruction error (mean abs, 0-255, text excluded): {total / _m.Count * 255:0.00}");
            foreach (Node n in _nodes)
            {
                if (n.IsLabel || n.IsGroup || n.W <= 0) continue;
                Box b = Box.Around(n.X, n.Y, n.W, n.H, Math.Max(2, n.ShadowPad)).Clamp(_m.W, _m.H);
                double e = 0; int k = 0;
                for (int y = b.Y; y < b.Bottom; y++)
                {
                    for (int x = b.X; x < b.Right; x++)
                    {
                        int i = y * _m.W + x;
                        e += (Math.Abs(Img.ToSrgb01(check.R[i]) - Img.ToSrgb01(_m.R[i])) +
                              Math.Abs(Img.ToSrgb01(check.G[i]) - Img.ToSrgb01(_m.G[i])) +
                              Math.Abs(Img.ToSrgb01(check.B[i]) - Img.ToSrgb01(_m.B[i]))) / 3.0;
                        k++;
                    }
                }

                n.Error = k > 0 ? e / k * 255 : 0;
                string what = n.IsCut ? "cut" : !string.IsNullOrEmpty(n.Use) ? "use" : n.Make != null ? "make" : "erase";
                _log.AppendLine($"  {n.Id,-28} {what,-5} error {n.Error,6:0.00}");
            }

            foreach (Node n in _nodes.Where(x => x.IsLabel && x.InkBox != null))
            {
                double[] b = n.InkBox;
                _log.AppendLine(FormattableString.Invariant(
                    $"  label {n.Id,-22} ink {b[0]:0.0},{b[1]:0.0} -> {b[2]:0.0},{b[3]:0.0}  cap {n.CapTop:0.0} base {n.Baseline:0.0}  {n.ColorHex}{(n.ColorTopHex != null ? $" grad {n.ColorTopHex}->{n.ColorBottomHex}" : "")}"));
            }
        }

        // ------------------------------------------------------------------ layout.json

        private void WriteLayout(string platePath)
        {
            var root = new JsonObject
            {
                ["name"] = _name,
                ["mockup"] = _mockupFile,
                ["mockW"] = MockW,
                ["mockH"] = MockH,
                ["background"] = BackgroundMode == "none" ? "" : BackgroundMode == "shared" ? SharedMenuBackground : (platePath != null ? ToAssetPath(platePath) : $"Assets/_Project/Art/Backgrounds/BG_{_name}.png"),
                ["backgroundRect"] = ToJson(new double[] { -ExtendX, -ExtendY, MockW + 2 * ExtendX, MockH + 2 * ExtendY }),
                ["backgroundPpu"] = BackgroundMode == "shared" ? 0 : Math.Round(PpuAt1x, 4),
            };
            var arr = new JsonArray();
            foreach (Node n in _nodes)
            {
                var o = JsonNode.Parse(n.Raw.ToJsonString()).AsObject();
                o["kind"] = n.Kind;
                if (n.SpritePath != null)
                {
                    o["sprite"] = n.SpritePath;
                    o["spriteRect"] = ToJson(n.SpriteRect);
                    o["spritePpu"] = n.SpritePpu;
                }

                if (n.ShadowPath != null)
                {
                    o["shadowSprite"] = n.ShadowPath;
                    o["shadowRect"] = ToJson(n.ShadowRect);
                    o["shadowPpu"] = n.ShadowPpu;
                }

                if (n.IsLabel && n.InkBox != null)
                {
                    o["inkBox"] = ToJson(n.InkBox);
                    o["capTop"] = n.CapTop;
                    o["baseline"] = n.Baseline;
                    o["color"] = n.ColorHex;
                    if (n.ColorTopHex != null)
                    {
                        o["colorTop"] = n.ColorTopHex;
                        o["colorBottom"] = n.ColorBottomHex;
                    }
                }

                if (n.IsMatte && n.HasInk && !n.IsLabel) o["color"] = Img.Hex(n.InkColor.r, n.InkColor.g, n.InkColor.b);
                if (!double.IsNaN(n.Error)) o["checkError"] = Math.Round(n.Error, 2);
                arr.Add(o);
            }

            root["nodes"] = arr;
            File.WriteAllText(Path.Combine(ScreenDir, "layout.json"), root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private static JsonArray ToJson(double[] v)
        {
            var a = new JsonArray();
            foreach (double d in v) a.Add(Math.Round(d, 2));
            return a;
        }

        // ------------------------------------------------------------------ helpers

        private void StampMask(float[] full, Box region, float[] local)
        {
            if (local == null) return;
            for (int y = 0; y < region.H; y++)
            {
                int fy = region.Y + y;
                if (fy < 0 || fy >= _m.H) continue;
                for (int x = 0; x < region.W; x++)
                {
                    int fx = region.X + x;
                    if (fx < 0 || fx >= _m.W) continue;
                    int i = fy * _m.W + fx;
                    full[i] = Math.Max(full[i], local[y * region.W + x]);
                }
            }
        }

        private static void StampRegion(float[] target, Box targetBox, Box region, float[] local)
        {
            if (local == null) return;
            for (int y = 0; y < region.H; y++)
            {
                int ty = region.Y + y - targetBox.Y;
                if (ty < 0 || ty >= targetBox.H) continue;
                for (int x = 0; x < region.W; x++)
                {
                    int tx = region.X + x - targetBox.X;
                    if (tx < 0 || tx >= targetBox.W) continue;
                    int i = ty * targetBox.W + tx;
                    target[i] = Math.Max(target[i], local[y * region.W + x]);
                }
            }
        }

        private static void CopyRegion(float[] src, Box srcBox, float[] dst, Box dstBox)
        {
            if (src == null) return;
            for (int y = 0; y < dstBox.H; y++)
            {
                int sy = dstBox.Y + y - srcBox.Y;
                if (sy < 0 || sy >= srcBox.H) continue;
                for (int x = 0; x < dstBox.W; x++)
                {
                    int sx = dstBox.X + x - srcBox.X;
                    if (sx < 0 || sx >= srcBox.W) continue;
                    dst[y * dstBox.W + x] = src[sy * srcBox.W + sx];
                }
            }
        }

        private static Box AlphaBounds(float[] a, Box box, float threshold)
        {
            int minX = box.W, minY = box.H, maxX = -1, maxY = -1;
            for (int y = 0; y < box.H; y++)
            {
                for (int x = 0; x < box.W; x++)
                {
                    if (a[y * box.W + x] <= threshold) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            return maxX < 0 ? new Box(box.X, box.Y, 0, 0) : new Box(box.X + minX, box.Y + minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>Fades a soft layer to nothing over the last pixels of its box so it never ends in a visible edge.</summary>
        private static void Taper(float[] a, int w, int h, int width)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int d = Math.Min(Math.Min(x, w - 1 - x), Math.Min(y, h - 1 - y));
                    if (d < width) a[y * w + x] *= (float)(d + 0.5) / width;
                }
            }
        }

        private void Handoff(string file, string type, int w, int h, string prompt, string notes)
        {
            string csv = Path.Combine(_repo, "ArtSource", "handoff.csv");
            var lines = File.Exists(csv) ? File.ReadAllLines(csv).ToList() : new List<string> { "file,type,size_px,nine_slice_ltrb,used_on,prompt,status,notes" };
            string row = string.Join(",", file, type, $"{w}x{h}", "", ScreenNumber, prompt, "review", notes.Replace(',', ';'));
            int at = lines.FindIndex(l => l.StartsWith(file + ",", StringComparison.Ordinal));
            if (at >= 0) lines[at] = row;
            else lines.Add(row);
            File.WriteAllLines(csv, lines, new UTF8Encoding(true));
        }
    }
}
