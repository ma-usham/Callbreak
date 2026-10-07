# Cuts the owner's five portraits (Assets/ArtRefrences/cartoon-adults, 6 Oct 2026) into the avatar
# presets (ADR 041). Each portrait sits in a gold ring of its own; the game frames every avatar in
# its own ring already (UI_AvatarRing and the seat rings), so the disc is cut just inside the gold:
# the ring is fitted to its outer edge (rays from the centre; a hat that breaks out of it is left
# out of the fit), its width is measured where the cream ground meets it, and the cut is $Inset px
# inside that. Inside an exact, anti-aliased circle the disc is made fully opaque (the sources are
# 98-99 % opaque) and resampled to $Size x $Size: the Profile screen shows it at up to ~240 px, the
# seats far smaller (the textures are mipmapped).
# Presets keep their files (26 screen layouts and the kit records name them): 01 -> Avatar_Default
# (preset 0, what a screen shows before it knows the player's), 02..05 -> Avatar_01..04.
# The same five also come framed (ADR 043): the whole portrait with its own gold ring, cut at the
# ring's outer edge, for the name screen at the start, which shows them as the owner's mockup draws
# them: Avatar_Framed_00..04, $FramedSize x $FramedSize.
# No grey silhouette shows anywhere any more (ADR 041), so the Quick Match screen's searching seat,
# UI_Seat_Searching, keeps only its dashed circle: everything inside the dashes is cleared.
#   portraits.ps1 [-Size 512] [-FramedSize 384] [-Inset 3] [-Install]
param(
    [int] $Size = 512,
    [int] $FramedSize = 384,
    [double] $Inset = 3,
    [switch] $Install
)
. (Join-Path $PSScriptRoot 'decor.ps1')
if (-not ('PortraitTools' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;

// Works on BGRA pixels as DecorTools.Read gives them (the two Add-Types cannot see each other).
public static class PortraitTools
{
    // The gold ring: cx, cy, outer radius, width (outer edge to the cream ground inside).
    public static double[] Ring(byte[] px, int w, int h, int rays)
    {
        double cx0 = w / 2.0, cy0 = h / 2.0, rMax = Math.Min(w, h) / 2.0;
        var edge = new List<double[]>();
        for (int k = 0; k < rays; k++)
        {
            double a = 2 * Math.PI * k / rays, dx = Math.Cos(a), dy = Math.Sin(a);
            for (double r = rMax; r > rMax * 0.5; r -= 0.25)
            {
                int x = (int)Math.Floor(cx0 + dx * r), y = (int)Math.Floor(cy0 + dy * r);
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                if (px[(y * w + x) * 4 + 3] >= 128) { edge.Add(new[] { cx0 + dx * r, cy0 + dy * r }); break; }
            }
        }

        double[] c = Fit(edge);
        for (int pass = 0; pass < 3; pass++)
        {
            double[] fit = c;
            List<double[]> kept = edge.FindAll(p => Math.Abs(Dist(p, fit) - fit[2]) <= 4.0);
            c = Fit(kept);
        }

        var widths = new List<double>();
        for (int k = 0; k < rays; k++)
        {
            double a = 2 * Math.PI * k / rays, dx = Math.Cos(a), dy = Math.Sin(a);
            for (double d = 1; d < 90; d += 0.25)
            {
                double r = c[2] - d;
                int x = (int)Math.Floor(c[0] + dx * r), y = (int)Math.Floor(c[1] + dy * r);
                int i = (y * w + x) * 4;
                if (IsCream(px[i + 2], px[i + 1], px[i])) { widths.Add(d); break; }
            }
        }

        if (widths.Count == 0) throw new InvalidOperationException("no cream ground found inside the ring");
        widths.Sort();
        return new[] { c[0], c[1], c[2], widths[widths.Count / 2], edge.Count, widths.Count };
    }

    // Every pixel fully opaque (the cut's circle sets the edge afterwards).
    public static void Opaque(byte[] px)
    {
        for (int i = 3; i < px.Length; i += 4) px[i] = 255;
    }

    // Fully opaque within r of (cx, cy) only: the portrait and its ring; the ring's own soft outer
    // edge and the transparent corners stay as drawn.
    public static void OpaqueWithin(byte[] px, int w, int h, double cx, double cy, double r)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                if (dx * dx + dy * dy < r * r) px[(y * w + x) * 4 + 3] = 255;
            }
        }
    }

    // Clears every pixel closer than r to the centre (what lies inside a ring); returns how far out
    // anything that was cleared reached.
    public static double ClearInside(byte[] px, int w, int h, double r)
    {
        double cx = w / 2.0, cy = h / 2.0, reach = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double d = Math.Sqrt((x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy));
                int i = (y * w + x) * 4;
                if (d >= r || px[i + 3] == 0) continue;
                if (d > reach) reach = d;
                px[i] = px[i + 1] = px[i + 2] = px[i + 3] = 0;
            }
        }
        return reach;
    }

    // The innermost opaque pixel at least r from the centre: where a ring begins.
    public static double InnerEdge(byte[] px, int w, int h, double r)
    {
        double cx = w / 2.0, cy = h / 2.0, edge = double.MaxValue;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double d = Math.Sqrt((x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy));
                if (d >= r && d < edge && px[(y * w + x) * 4 + 3] >= 128) edge = d;
            }
        }
        return edge;
    }

    // The portraits' ground: light and warm, unlike the gold ring and its dark outline.
    static bool IsCream(int r, int g, int b) { return r >= 235 && g >= 215 && b >= 180 && r - b <= 70; }

    static double Dist(double[] p, double[] c) { return Math.Sqrt((p[0] - c[0]) * (p[0] - c[0]) + (p[1] - c[1]) * (p[1] - c[1])); }

    // Least-squares circle (Kasa) through the points, in coordinates about their mean.
    static double[] Fit(List<double[]> pts)
    {
        double mx = 0, my = 0;
        foreach (double[] p in pts) { mx += p[0]; my += p[1]; }
        mx /= pts.Count; my /= pts.Count;
        double sxx = 0, syy = 0, sxy = 0, sxz = 0, syz = 0, sz = 0;
        foreach (double[] p in pts)
        {
            double x = p[0] - mx, y = p[1] - my, z = x * x + y * y;
            sxx += x * x; syy += y * y; sxy += x * y; sxz += x * z; syz += y * z; sz += z;
        }
        // Centred, so the sums of x and y are 0: solve [sxx sxy; sxy syy] [D E] = -[sxz syz], F = -sz / n.
        double det = sxx * syy - sxy * sxy;
        double D = (-sxz * syy + syz * sxy) / det;
        double E = (-syz * sxx + sxz * sxy) / det;
        double F = -sz / pts.Count;
        double cx = -D / 2, cy = -E / 2;
        return new[] { cx + mx, cy + my, Math.Sqrt(cx * cx + cy * cy - F) };
    }
}
'@
}

$src = Join-Path $DecorRepo 'Assets\ArtRefrences\cartoon-adults'
$fitDir = "$DecorWork\fit"; New-Item -ItemType Directory -Force $fitDir | Out-Null
$sources = @('01-dhaka-topi', '02-crimson-braid', '03-himalayan-cap', '04-teal-shawl', '05-indigo-jacket')
$names = @('Avatar_Default', 'Avatar_01', 'Avatar_02', 'Avatar_03', 'Avatar_04')
$made = @()
$framed = @()
for ($n = 0; $n -lt 5; $n++) {
    $raw = [DecorTools]::Load("$src\$($sources[$n]).png")
    $px = [DecorTools]::Read($raw)
    $ring = [PortraitTools]::Ring($px, $raw.Width, $raw.Height, 360)
    $cx = $ring[0]; $cy = $ring[1]; $outer = $ring[2]; $width = $ring[3]
    $cut = $outer - $width - $Inset

    # Framed: the ring kept, cut round at its outer edge (a hat that breaks out of it is cut too).
    $fpx = [DecorTools]::Read($raw)
    [PortraitTools]::OpaqueWithin($fpx, $raw.Width, $raw.Height, $cx, $cy, $outer - 1)
    $fsrc = [DecorTools]::Write($fpx, $raw.Width, $raw.Height)
    $fr = $outer + 2
    $fsq = [DecorTools]::Resample($fsrc, $cx - $fr, $cy - $fr, 2 * $fr, 2 * $fr, $FramedSize, $FramedSize); $fsrc.Dispose()
    $fmask = [DecorTools]::CircleMask($fsq, $FramedSize / 2.0, $FramedSize / 2.0, ($FramedSize / 2.0) * ($outer + 0.5) / $fr, 0, [System.Drawing.Color]::Black); $fsq.Dispose()
    $fpath = (Join-Path $fitDir ('Avatar_Framed_{0:00}.png' -f $n)); [DecorTools]::Save($fmask, $fpath); $fmask.Dispose()
    $framed += $fpath

    [PortraitTools]::Opaque($px)
    $opaque = [DecorTools]::Write($px, $raw.Width, $raw.Height); $raw.Dispose()
    $sq = [DecorTools]::Resample($opaque, $cx - $cut, $cy - $cut, 2 * $cut, 2 * $cut, $Size, $Size); $opaque.Dispose()
    $final = [DecorTools]::CircleMask($sq, $Size / 2.0, $Size / 2.0, $Size / 2.0, 0, [System.Drawing.Color]::Black); $sq.Dispose()
    $path = "$fitDir\$($names[$n]).png"; [DecorTools]::Save($final, $path); $final.Dispose()
    "{0,-15} <- {1,-17} ring centre {2:F1},{3:F1} r {4:F1} ({5} of 360 rays fit), gold {6:F2} px wide ({7} rays); cut r {8:F1} -> {9} px" -f $names[$n], $sources[$n], $cx, $cy, $outer, $ring[4], $width, $ring[5], $cut, $Size
    $made += $path
}

# Review sheet: each preset at the Profile screen's size and at a seat's, on the panel ivory and on
# the table's dark seat colour, inside the white disc every screen puts behind an avatar.
$big = 240; $small = 92; $pad = 24; $cell = $big + $pad
$sheet = New-Object System.Drawing.Bitmap ($pad + 5 * $cell), ($pad + 2 * ($big + $pad))
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#F7EDE3'))
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#2B2622'))), 0, ($pad + $big + $pad / 2), $sheet.Width, ($big + $pad))
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 252, 251, 249))
for ($i = 0; $i -lt 5; $i++) {
    $img = [System.Drawing.Image]::FromFile($made[$i])
    $x = $pad + $i * $cell
    $g.FillEllipse($white, $x - 6, $pad - 6, $big + 12, $big + 12)
    $g.DrawImage($img, $x, $pad, $big, $big)
    $y = $pad + $big + $pad + ($big - $small) / 2
    $g.FillEllipse($white, $x + ($big - $small) / 2 - 3, $y - 3, $small + 6, $small + 6)
    $g.DrawImage($img, $x + ($big - $small) / 2, $y, $small, $small)
    $img.Dispose()
}
$g.Dispose(); $sheet.Save("$fitDir\Portraits_check.png"); $sheet.Dispose()
"check    $fitDir\Portraits_check.png (presets 0-4 at 240 and 92 px, on ivory and on dark)"

# The searching seat: the dashes stay, the silhouette inside them goes. Read from the kit's export
# (the original), so running this again changes nothing.
$searching = [DecorTools]::Load("$DecorRepo\ArtSource\Exports\UI\UI_Seat_Searching.png")
$spx = [DecorTools]::Read($searching)
$dashes = [PortraitTools]::InnerEdge($spx, $searching.Width, $searching.Height, $searching.Width * 0.45)
$reach = [PortraitTools]::ClearInside($spx, $searching.Width, $searching.Height, $dashes - 2)
$clean = [DecorTools]::Write($spx, $searching.Width, $searching.Height)
$seatPath = "$fitDir\UI_Seat_Searching.png"; [DecorTools]::Save($clean, $seatPath)
"UI_Seat_Searching {0}x{1}: dashes from r {2:F1}; cleared inside r {3:F1} (the silhouette reached r {4:F1})" -f $searching.Width, $searching.Height, $dashes, ($dashes - 2), $reach
$clean.Dispose(); $searching.Dispose()

if ($Install) {
    $dst = "$DecorArt\UI\Avatars"; $exp = "$DecorRepo\ArtSource\Exports\Decor"; New-Item -ItemType Directory -Force $exp | Out-Null
    foreach ($p in $made + $framed) { Copy-Item $p (Join-Path $dst (Split-Path $p -Leaf)) -Force; Copy-Item $p (Join-Path $exp (Split-Path $p -Leaf)) -Force }
    "installed UI/Avatars/Avatar_Default.png, Avatar_01..04.png and Avatar_Framed_00..04.png  (+ ArtSource/Exports/Decor)"
    Copy-Item $seatPath "$DecorArt\UI\Controls\UI_Seat_Searching.png" -Force
    "installed UI/Controls/UI_Seat_Searching.png (the kit's export keeps the original)"
}
