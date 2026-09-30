# Builds the 2560 x 1440 menu background from a ChatGPT outpaint (BG-2): the outpaint supplies the
# margins, the approved clean plate goes back on top of the centre (handbook: "put your original
# centre back on top").
#   wide-menu.ps1 -Attempt a2 [-Feather 48] [-Dx 0 -Dy 0] [-Install]
# -Dx/-Dy shift the upscaled outpaint (pixels at 2560 scale) when its re-drawn centre sits off the
# original; without them the shift is measured on the scenery next to the seam.
# -Install copies the result to Assets/_Project/Art/Backgrounds/BG_Menu_Wide.png and ArtSource/Exports.
param(
    [Parameter(Mandatory)] [string] $Attempt,
    [int] $Feather = 48,
    [int] $Dx = [int]::MinValue,
    [int] $Dy = [int]::MinValue,
    [double] $Sharpen = 0.5,
    [switch] $Install
)
. (Join-Path $PSScriptRoot 'decor.ps1')
$W = 2560; $H = 1440; $cx = 320; $cy = 180
$plate = [DecorTools]::Load("$DecorArt\Backgrounds\BG_Menu_Clean.png")            # 1920 x 1080
$gen = [DecorTools]::Load("$DecorRepo\ArtSource\Working\Menu\chatgpt\BG_Menu_Wide_$Attempt.png")
$up = [DecorTools]::Resize($gen, $W, $H); $gen.Dispose()
$canvas = [DecorTools]::Place($plate, $W, $H, $cx, $cy)

if ($Dx -eq [int]::MinValue) {
    # Measure on textured tiles inside the centre (temple side and mountain side).
    $votes = @()
    foreach ($t in @(@(330, 430, 420, 560), @(760, 560, 300, 420), @(1680, 480, 540, 560), @(340, 1010, 360, 230), @(1900, 1010, 330, 230))) {
        $line = ([DecorTools]::ShiftGrid($canvas, $up, $t[0], $t[1], $t[2], $t[3], 1, 1, 6) -split "`n")[1] -split "`t"
        $votes += , @([int]$line[1], [int]$line[2], [double]$line[3], [double]$line[4])
        "tile {0}: best shift {1},{2}  err {3} -> {4}" -f ($t -join ','), $line[1], $line[2], $line[3], $line[4]
    }
    # 'up' matches the canvas when sampled at +shift, so move it by -shift.
    $Dx = - [int][Math]::Round((($votes | ForEach-Object { $_[0] }) | Sort-Object)[[int]($votes.Count / 2)])
    $Dy = - [int][Math]::Round((($votes | ForEach-Object { $_[1] }) | Sort-Object)[[int]($votes.Count / 2)])
}
"moving the outpaint by $Dx, $Dy px"
if ($Dx -ne 0 -or $Dy -ne 0) {
    $moved = [DecorTools]::Resample($up, - $Dx, - $Dy, $W, $H, $W, $H); $up.Dispose(); $up = $moved
}
if ($Sharpen -gt 0) { $s = [DecorTools]::Sharpen($up, 2, $Sharpen); $up.Dispose(); $up = $s }

# Colour: bring the outpaint onto the plate, fitted on the centre.
$upCentre = [DecorTools]::Crop($up, $cx, $cy, 1920, 1080)
# Offset only, measured on the plain haze (sky, mist, misty ground) of the centre: the scenery is
# re-drawn, so fitting on it (or fitting a gain) would follow the re-drawing, not the colour cast.
$mask = New-Object System.Drawing.Bitmap 1920, 1080, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$mg = [System.Drawing.Graphics]::FromImage($mask); $mg.Clear([System.Drawing.Color]::Transparent)
$haze = @(@(500, 20, 900, 230), @(760, 300, 420, 480), @(700, 930, 520, 130))
foreach ($r in $haze) { $mg.FillRectangle([System.Drawing.Brushes]::White, $r[0], $r[1], $r[2], $r[3]) }
$mg.Dispose()
$k = [DecorTools]::FitOffset($plate, $upCentre, $mask, 128)
"colour offset per r g b (measured on haze): " + (($k[1], $k[3], $k[5] | ForEach-Object { '{0:F2}' -f $_ }) -join ' ')
foreach ($r in $haze) { "  haze {0,-18} before: {1}" -f ($r -join ','), [DecorTools]::Compare($plate, $upCentre, $null, 0, $r[0], $r[1], $r[2], $r[3]) }
$fit = [DecorTools]::ApplyColor($up, $k); $up.Dispose(); $upCentre.Dispose()
$fitCentre = [DecorTools]::Crop($fit, $cx, $cy, 1920, 1080)
foreach ($r in $haze) { "  haze {0,-18} after : {1}" -f ($r -join ','), [DecorTools]::Compare($plate, $fitCentre, $null, 0, $r[0], $r[1], $r[2], $r[3]) }
# Thin strips just inside each edge of the centre: what the seam will look like.
foreach ($s in @(@('top edge', 300, 0, 1320, 24), @('bottom edge', 300, 1056, 1320, 24), @('left edge', 0, 100, 24, 880), @('right edge', 1896, 100, 24, 880))) {
    "  seam {0,-12}: {1}" -f $s[0], [DecorTools]::Compare($plate, $fitCentre, $null, 0, $s[1], $s[2], $s[3], $s[4])
}
$fitCentre.Dispose(); $mask.Dispose()

$out = [DecorTools]::FeatherPaste($fit, $plate, $cx, $cy, $Feather)
$dir = "$DecorRepo\ArtSource\Working\Menu\fit"; New-Item -ItemType Directory -Force $dir | Out-Null
$path = "$dir\BG_Menu_Wide_$Attempt.png"
$rgb = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($rgb); $g.DrawImage($out, 0, 0, $W, $H); $g.Dispose()
$rgb.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
"$path  ${W}x${H}"

# Checks: the centre must be the plate, and the seams must not show.
$outCentre = [DecorTools]::Crop($out, $cx + $Feather, $cy + $Feather, 1920 - 2 * $Feather, 1080 - 2 * $Feather)
$plateInner = [DecorTools]::Crop($plate, $Feather, $Feather, 1920 - 2 * $Feather, 1080 - 2 * $Feather)
"centre inside the feather vs plate: " + [DecorTools]::Compare($plateInner, $outCentre, $null, 0, 0, 0, $plateInner.Width, $plateInner.Height)
$outCentre.Dispose(); $plateInner.Dispose()
$small = [DecorTools]::Resize($out, 1280, 720); [DecorTools]::Save($small, "$dir\check_${Attempt}_whole.png"); $small.Dispose()
$hp = [DecorTools]::HighPass($out, 30, 5); $hps = [DecorTools]::Resize($hp, 1280, 720); [DecorTools]::Save($hps, "$dir\check_${Attempt}_highpass.png"); $hp.Dispose(); $hps.Dispose()
foreach ($c in @(@('tl', 0, 0, 900, 620), @('tr', 1660, 0, 900, 620), @('bl', 0, 820, 900, 620), @('br', 1660, 820, 900, 620), @('top', 830, 0, 900, 420), @('bottom', 830, 1020, 900, 420), @('left', 0, 420, 700, 620), @('right', 1860, 420, 700, 620))) {
    $crop = [DecorTools]::Crop($out, $c[1], $c[2], $c[3], $c[4]); [DecorTools]::Save($crop, "$dir\check_${Attempt}_$($c[0]).png"); $crop.Dispose()
}
if ($Install) {
    Copy-Item $path "$DecorArt\Backgrounds\BG_Menu_Wide.png" -Force
    New-Item -ItemType Directory -Force "$DecorRepo\ArtSource\Exports\Backgrounds" | Out-Null
    Copy-Item $path "$DecorRepo\ArtSource\Exports\Backgrounds\BG_Menu_Wide.png" -Force
    "installed to Assets/_Project/Art/Backgrounds/BG_Menu_Wide.png and ArtSource/Exports/Backgrounds"
}
$out.Dispose(); $fit.Dispose(); $plate.Dispose(); $canvas.Dispose(); $rgb.Dispose()
