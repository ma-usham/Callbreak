# Final checks for the ShopBackgroundsB plates: composites (Back + ShopTables' Table_HimalayanIvory +
# Front) and the numbers quoted in the report.
. "$PSScriptRoot\bg.ps1"
$fin = "$Area\final"; $chk = "$Area\check"
$table = "$Repo\ArtSource\Working\ShopTables\final\Table_HimalayanIvory.png"
$mask = [Img]::Load("$Repo\ArtSource\Working\ShopTables\final\TableMask.png")

function BoxStats($im, $x, $y, $w, $h) {
    $l = $im.Luma(); $s = 0; $n = 0; $dark = 0
    for ($yy = $y; $yy -lt $y + $h; $yy++) { for ($xx = $x; $xx -lt $x + $w; $xx++) { $v = $l[$yy * $im.W + $xx]; $s += $v; $n++; if ($v -lt 120) { $dark++ } } }
    "luma {0,5:F1}, dark {1,4:P0}" -f ($s / $n), ($dark / $n)
}
function SceneStats([Img] $p) {
    $sl = 0.0; $ss = 0.0; $n = 0
    for ($y = 157; $y -lt 1098; $y += 2) { for ($x = 279; $x -lt 1951; $x += 2) { $i = $y * 2230 + $x; if ($mask.A[$i] -gt 127) { continue }
        $mx = [Math]::Max($p.R[$i], [Math]::Max($p.G[$i], $p.B[$i])); $mn = [Math]::Min($p.R[$i], [Math]::Min($p.G[$i], $p.B[$i]))
        $sl += 0.299 * $p.R[$i] + 0.587 * $p.G[$i] + 0.114 * $p.B[$i]; if ($mx -gt 0) { $ss += ($mx - $mn) / $mx }; $n++ } }
    "scenery outside the table (16:9): luma {0:F1}, sat {1:F3}" -f ($sl / $n), ($ss / $n)
}
$hud = [ordered]@{ '16:9 ROUND' = @(932,175,366,36); '16:9 Scores/Settings' = @(1714,266,194,22); '21:9 Scores/Settings' = @(1977,265,218,25); '4:3 ROUND' = @(932,18,366,36); '4:3 Scores/Settings' = @(1714,108,196,22) }

"BG_Table (reference): " + (SceneStats ([Img]::Load("$Repo\Assets\_Project\Art\Backgrounds\BG_Table.png")))
foreach ($name in 'PokharaLakeside', 'MonasteryGarden') {
    $back = [Img]::Load("$fin\BG_${name}_Back.png")
    "$name Back: " + (SceneStats $back)
    foreach ($k in $hud.Keys) { $r = $hud[$k]; "  HUD {0,-22} {1}" -f $k, (BoxStats $back $r[0] $r[1] $r[2] $r[3]) }
    $front = [Img]::Load("$fin\BG_${name}_Front.png")
    $over = 0; $surf = 0; $hand = 0; $handN = 0
    for ($y = 441; $y -lt 1044; $y += 2) { for ($x = 334; $x -lt 1895; $x += 2) { $u = ($x - 1114.3) / 780.2; $v = ($y - 742.2) / 301.4; if ($u * $u + $v * $v -le 1) { $surf++; if ($front.A[$y * 2230 + $x] -gt 20) { $over++ } } } }
    for ($y = 900; $y -lt 1255; $y += 2) { for ($x = 799; $x -lt 1429; $x += 2) { $handN++; if ($front.A[$y * 2230 + $x] -gt 20) { $hand++ } } }
    "  Front: covers {0:P2} of the ivory playing surface, {1:P2} of the bottom centre (x 799-1429, y 900+)" -f ($over / $surf), ($hand / $handN)
    "  Front: left cluster {0}, right cluster {1} (x, y, w, h)" -f ($front.AlphaBox(20, 0, 1115) -join ','), ($front.AlphaBox(20, 1115, 2230) -join ',')
    $c = $back.Clone(); $c.Over([Img]::Load($table), 0, 0); $c.Over($front, 0, 0)
    $c.Save24("$chk\${name}_check_full.png")
    $c.Crop($ViewX, $ViewY, $ViewW, $ViewH).Save24("$chk\${name}_check_16x9.png")
    $c.Crop(17, $ViewY, 2195, $ViewH).Save24("$chk\${name}_check_21x9.png")
    $c.Crop($ViewX, 0, $ViewW, $PlateH).Save24("$chk\${name}_check_4x3.png")
}
# HimalayanVista reference numbers
$hv = [Img]::Load("$Repo\ArtSource\Working\ShopTables\final\BG_HimalayanVista_Back.png")
foreach ($k in $hud.Keys) { $r = $hud[$k]; "HimalayanVista Back HUD {0,-22} {1}" -f $k, (BoxStats $hv $r[0] $r[1] $r[2] $r[3]) }
