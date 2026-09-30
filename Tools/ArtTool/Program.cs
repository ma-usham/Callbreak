using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ArtTool
{
    /// <summary>
    /// arttool - see README.md. All coordinates are mockup pixels (1672 × 941, top-left origin).
    /// </summary>
    internal static class Program
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
                string repo = FindRepo();
                var opt = Options(args.Skip(1).ToArray(), out List<string> pos);
                string Get(string k, string d = null) => opt.TryGetValue(k, out string v) ? v : d;
                switch (args[0])
                {
                    case "job":
                    {
                        string spec = pos.Count > 0 ? pos[0] : throw new ArgumentException("job needs a screen.json path or a screen name");
                        if (!File.Exists(spec)) spec = Path.Combine(repo, "ArtSource", "Screens", spec, "screen.json");
                        return new Job(repo, Path.GetFullPath(spec)).Run(writeAssets: !opt.ContainsKey("dry"));
                    }
                    case "crop":
                        Commands.Crop(Commands.MockupPath(repo, Get("screen")), Commands.Rect(Get("rect")), double.Parse(Get("scale", "3")), Get("out"));
                        return 0;
                    case "grid":
                        Commands.Grid(Commands.MockupPath(repo, Get("screen")), Commands.Rect(Get("rect")), double.Parse(Get("scale", "4")),
                            int.Parse(Get("minor", "10")), int.Parse(Get("major", "50")), Get("out"));
                        return 0;
                    case "probe":
                        Commands.Probe(Img.Load(Commands.MockupPath(repo, Get("screen"))), Commands.Rect(Get("rect")));
                        return 0;
                    case "bounds":
                        Commands.Bounds(Img.Load(Commands.MockupPath(repo, Get("screen"))), Commands.Rect(Get("rect")),
                            float.Parse(Get("threshold", "0.05")), int.Parse(Get("ring", "4")));
                        return 0;
                    case "profile":
                    {
                        Img img = Img.Load(Commands.MockupPath(repo, Get("screen")));
                        bool isRow = opt.ContainsKey("row");
                        int line = int.Parse(isRow ? Get("row") : Get("col"));
                        Commands.Profile(img, isRow, line, int.Parse(Get("from")), int.Parse(Get("to")), int.Parse(Get("edges", "4")));
                        return 0;
                    }
                    case "fitrect":
                        Commands.FitRect(Img.Load(Commands.MockupPath(repo, Get("screen"))), Commands.Rect(Get("rect")), int.Parse(Get("search", "6")));
                        return 0;
                    case "inkbox":
                    {
                        double[] b = InkMeasure.Box(Img.Load(Commands.MockupPath(repo, Get("screen"))), Commands.Rect(Get("rect")), Get("ink", "dark") != "light");
                        Console.WriteLine(b == null ? "no ink" : FormattableString.Invariant($"ink box left {b[0]:0.00} top {b[1]:0.00} right {b[2]:0.00} bottom {b[3]:0.00}  (w {b[2] - b[0]:0.00}, h {b[3] - b[1]:0.00})"));
                        return 0;
                    }
                    case "compare":
                    {
                        string name = Get("name");
                        string layout = Get("layout") ?? (name != null ? Path.Combine(repo, "ArtSource", "Screens", name, "layout.json") : null);
                        string outDir = Get("out") ?? (name != null ? Path.Combine(repo, "ArtSource", "Screens", name, "check") : Path.Combine(repo, "Temp", "Compare"));
                        return Commands.Compare(repo, Commands.MockupPath(repo, Get("screen")), Get("capture"), layout, outDir);
                    }
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

        private const string Usage = @"arttool <command> [options]
  job <screen.json | ScreenName> [--dry]      rebuild a mockup as plate + sprites + layout.json (see README)
  crop   --screen 02 --rect x,y,w,h [--scale 3] --out file.png
  grid   --screen 02 --rect x,y,w,h [--scale 4] [--minor 10] [--major 50] --out file.png
  probe  --screen 02 --rect x,y,w,h          colour statistics of a region
  bounds --screen 02 --rect x,y,w,h [--threshold 0.05]   tight bounds + corner radii of what stands out
  inkbox --screen 02 --rect x,y,w,h [--ink dark|light]   text ink box, sub-pixel
  profile --screen 02 (--row y | --col x) --from a --to b [--edges 4]   brightness line + strongest edges
  fitrect --screen 02 --rect x,y,w,h [--search 6]   exact edges + corner radius of a panel/button from a rough rect
  compare --screen 02 --capture Temp/Captures/X_1920x1080.png [--name CreateRoom | --layout path] [--out dir]";

        private static Dictionary<string, string> Options(string[] args, out List<string> positional)
        {
            var d = new Dictionary<string, string>();
            positional = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    string k = args[i].Substring(2);
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) d[k] = args[++i];
                    else d[k] = "true";
                }
                else positional.Add(args[i]);
            }

            return d;
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

            throw new DirectoryNotFoundException("run arttool from inside the Unity project");
        }
    }
}
