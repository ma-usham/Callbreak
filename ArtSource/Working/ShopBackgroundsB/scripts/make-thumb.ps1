# Shop tile picture: ChatGPT's 7:8 painting, centre-cropped to the tile aspect (590 x 670, mockup 20's
# tile picture of about 256 x 291 mockup px at x2.3; same size as ShopBackgroundsA's thumbs) and
# resized with Lanczos-3. Prints brightness/saturation next to mockup 20's three tile pictures.
param(
    [Parameter(Mandatory)] [string] $Src,
    [Parameter(Mandatory)] [string] $Out,
    [int] $W = 590, [int] $H = 670,
    [double] $ShiftY = 0.5              # 0 = keep the top, 0.5 = centre, 1 = keep the bottom
)
. "$PSScriptRoot\bg.ps1"
$t = [Img]::Load($Src)
$aspect = $W / $H
if ($t.W / $t.H -gt $aspect) { $cw = [int][Math]::Round($t.H * $aspect); $ch = $t.H } else { $cw = $t.W; $ch = [int][Math]::Round($t.W / $aspect) }
$cx = [int][Math]::Round(($t.W - $cw) / 2); $cy = [int][Math]::Round(($t.H - $ch) * $ShiftY)
$r = $t.Crop($cx, $cy, $cw, $ch).Resize($W, $H)
$r.Save($Out)
"{0}: crop {1},{2} {3}x{4} of {5}x{6} -> {7}x{8}" -f (Split-Path $Out -Leaf), $cx, $cy, $cw, $ch, $t.W, $t.H, $W, $H

function Stat([Img] $im) {
    $sl = 0.0; $ss = 0.0; $n = $im.W * $im.H
    for ($i = 0; $i -lt $n; $i++) {
        $mx = [Math]::Max($im.R[$i], [Math]::Max($im.G[$i], $im.B[$i])); $mn = [Math]::Min($im.R[$i], [Math]::Min($im.G[$i], $im.B[$i]))
        $sl += 0.299 * $im.R[$i] + 0.587 * $im.G[$i] + 0.114 * $im.B[$i]
        if ($mx -gt 0) { $ss += ($mx - $mn) / $mx }
    }
    "luma {0,5:F1}  sat {1:F3}" -f ($sl / $n), ($ss / $n)
}
$m = [Img]::Load("$Repo\Assets\ArtRefrences\game-screens\20-backgrounds.png")
"  mockup Kathmandu Courtyard  " + (Stat ($m.Crop(394, 243, 254, 285)))
"  mockup Village Terrace      " + (Stat ($m.Crop(703, 243, 247, 285)))
"  mockup Vintage Home         " + (Stat ($m.Crop(999, 244, 245, 284)))
"  this thumb                  " + (Stat $r)
