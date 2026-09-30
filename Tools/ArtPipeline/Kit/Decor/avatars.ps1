# Splits the 2 x 2 avatar sheet (ChatGPT, IC-2 variants) into Avatar_01..04: each disc gets an
# exact, anti-aliased circular edge (the cut-out's own edge is jagged), the ivory one gets its
# thin outline re-drawn at that edge, and the silhouette is brought to the same size and place
# as on the approved Avatar_Default. Output 222 x 222 like Avatar_Default.
#   avatars.ps1 -Src <sheet png> [-Install]
param(
    [Parameter(Mandatory)] [string] $Src,
    [int] $Size = 222,
    [switch] $NoNormalise,
    [switch] $Install
)
. (Join-Path $PSScriptRoot 'decor.ps1')
$fitDir = "$DecorWork\fit"; New-Item -ItemType Directory -Force $fitDir | Out-Null
$names = @('Avatar_01', 'Avatar_02', 'Avatar_03', 'Avatar_04')
$what = @('warm grey', 'bronze', 'crimson', 'ivory')

$def = [DecorTools]::Load("$DecorArt\UI\Avatars\Avatar_Default.png")
$dbox = [DecorTools]::SilhouetteBox($def, 30)
"Avatar_Default  silhouette box {0:F3},{1:F3} .. {2:F3},{3:F3}  (w {4:F3}, h {5:F3})" -f $dbox[0], $dbox[1], $dbox[2], $dbox[3], ($dbox[2] - $dbox[0]), ($dbox[3] - $dbox[1])

$raw = [DecorTools]::Load($Src)
$cut = [DecorTools]::AlphaLevels($raw, 12, 250); $raw.Dispose()
$blobs = [DecorTools]::Blobs($cut, 40, 3, 20000)
if ($blobs.Count -ne 4) { throw "expected 4 discs on the sheet, found $($blobs.Count)" }
# order: top row left to right, then bottom row left to right
$blobs = @($blobs | Sort-Object { [int]($_[1] / 300) }, { $_[0] })
$made = @()
for ($n = 0; $n -lt 4; $n++) {
    $b = $blobs[$n]
    $cx = $b[0] + $b[2] / 2.0; $cy = $b[1] + $b[3] / 2.0; $rim = [Math]::Min($b[2], $b[3]) / 2.0
    # how far inside ChatGPT's rim the disc is fully opaque (the cut-out's own edge is jagged)
    $k = 0; while ($k -lt 14 -and [DecorTools]::MinAlphaOnCircle($cut, $cx, $cy, $rim - $k) -lt 255) { $k++ }
    $ringDrawn = 0; $ringC = [System.Drawing.Color]::Black
    if ($n -eq 3) {
        # the ivory disc's thin outline: the darkest colour found on the circles from the rim
        # inwards, and how far in it reaches (it may lie inside the jagged zone)
        $lums = @(); $cols = @()
        for ($j = 0; $j -le 16; $j++) {
            $c = [DecorTools]::ColorOnCircle($cut, $cx, $cy, $rim - $j)
            $cols += , $c; $lums += (0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B)
        }
        $inner = $lums[16]; $darkest = ($lums | Measure-Object -Minimum).Minimum
        $ringC = $cols[[array]::IndexOf($lums, $darkest)]
        $last = 0
        for ($j = 0; $j -le 16; $j++) { if ($lums[$j] -lt ($inner + $darkest) / 2) { $last = $j } }
        $ringDrawn = $last + 1
        if ($k -lt $ringDrawn) { $k = $ringDrawn }      # scale only the plain disc; the outline is re-drawn
    }
    $clean = $rim - $k - 0.5
    $discC = [DecorTools]::ColorOnCircle($cut, $cx, $cy, $clean - 4)
    # fill what lies outside the clean disc with the disc colour, so any square can be resampled
    $crop = [DecorTools]::Crop($cut, $b[0] - 40, $b[1] - 40, $b[2] + 80, $b[3] + 80)
    $ox = $cx - ($b[0] - 40); $oy = $cy - ($b[1] - 40)
    $inside = [DecorTools]::CircleMask($crop, $ox, $oy, $clean, 0, [System.Drawing.Color]::Black); $crop.Dispose()
    $solid = [DecorTools]::Over([DecorTools]::Flat($inside.Width, $inside.Height, $discC), $inside, 0, 0); $inside.Dispose()
    $sq = [DecorTools]::Resample($solid, $ox - $clean, $oy - $clean, 2 * $clean, 2 * $clean, 600, 600)
    $box = [DecorTools]::SilhouetteBox($sq, 30); $sq.Dispose()
    $scale = 1.0; $dx = 0.0; $dy = 0.0
    if (-not $NoNormalise) {
        # same silhouette height and centre as the default avatar
        $scale = ($dbox[3] - $dbox[1]) / ($box[3] - $box[1])
        $dx = (($dbox[0] + $dbox[2]) / 2 - 0.5) - (($box[0] + $box[2]) / 2 - 0.5) * $scale
        $dy = (($dbox[1] + $dbox[3]) / 2 - 0.5) - (($box[1] + $box[3]) / 2 - 0.5) * $scale
    }
    $side = 2 * $clean / $scale
    $sx = $ox - $side / 2 - $dx * $side; $sy = $oy - $side / 2 - $dy * $side
    $out = [DecorTools]::Resample($solid, $sx, $sy, $side, $side, $Size, $Size); $solid.Dispose()
    $ringOut = if ($n -eq 3) { [Math]::Max(1.6, $ringDrawn * ($Size / 2.0) / $rim) } else { 0 }
    $final = [DecorTools]::CircleMask($out, $Size / 2.0, $Size / 2.0, $Size / 2.0, $ringOut, $ringC); $out.Dispose()
    $path = "$fitDir\$($names[$n]).png"; [DecorTools]::Save($final, $path)
    $fb = [DecorTools]::SilhouetteBox($final, 30)
    "{0} ({1})  rim r {2:F1} px, opaque from {3} px inside, disc #{4:X2}{5:X2}{6:X2}{7}  silhouette x{8:F3} moved {9:F3},{10:F3} -> box {11:F3},{12:F3} .. {13:F3},{14:F3}  {15}" -f $names[$n], $what[$n], $rim, $k, $discC.R, $discC.G, $discC.B,
        $(if ($n -eq 3) { ", outline #{0:X2}{1:X2}{2:X2} {3} px -> {4:F1} px" -f $ringC.R, $ringC.G, $ringC.B, $ringDrawn, $ringOut } else { '' }),
        $scale, $dx, $dy, $fb[0], $fb[1], $fb[2], $fb[3], [DecorTools]::Stats($final)
    $made += $path
    $final.Dispose()
}
$cut.Dispose()

# review sheet: the default and the four variants, on the panel ivory and on the table's dark seat colour
$all = @("$DecorArt\UI\Avatars\Avatar_Default.png") + $made
$z = 2; $pad = 16
$sheet = New-Object System.Drawing.Bitmap (($Size * $z + $pad) * 5 + $pad), (($Size * $z + $pad) * 2 + $pad)
$g = [System.Drawing.Graphics]::FromImage($sheet); $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#F7EDE3'))
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#2B2622'))), 0, ($Size * $z + $pad + $pad / 2), $sheet.Width, ($Size * $z + $pad))
for ($i = 0; $i -lt 5; $i++) {
    $img = [System.Drawing.Image]::FromFile($all[$i])
    $g.DrawImage($img, ($pad + $i * ($Size * $z + $pad)), $pad, $Size * $z, $Size * $z)
    $g.DrawImage($img, ($pad + $i * ($Size * $z + $pad)), ($pad * 2 + $Size * $z), $Size * $z, $Size * $z)
    $img.Dispose()
}
$g.Dispose(); $sheet.Save("$fitDir\Avatars_check.png"); $sheet.Dispose()
"check    $fitDir\Avatars_check.png (Avatar_Default, 01..04; on ivory and on dark; 2x)"
$def.Dispose()
if ($Install) {
    $dst = "$DecorArt\UI\Avatars"; $exp = "$DecorRepo\ArtSource\Exports\Decor"; New-Item -ItemType Directory -Force $exp | Out-Null
    foreach ($p in $made) { Copy-Item $p (Join-Path $dst (Split-Path $p -Leaf)) -Force; Copy-Item $p (Join-Path $exp (Split-Path $p -Leaf)) -Force }
    "installed UI/Avatars/Avatar_01..04.png  (+ ArtSource/Exports/Decor)"
}
