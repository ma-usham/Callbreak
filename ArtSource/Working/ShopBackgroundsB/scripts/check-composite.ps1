# Check composite: Back + ShopTables' Table_HimalayanIvory + Front, saved full size and as the 16:9,
# 21:9 and 4:3 crops (what those phones show), plus a contact-strip view of where the table meets
# the ground at both sides.
param(
    [Parameter(Mandatory)] [string] $Back,
    [Parameter(Mandatory)] [string] $Front,
    [Parameter(Mandatory)] [string] $Name,
    [string] $Table = 'D:\UnityProjects\Callbreak\ArtSource\Working\ShopTables\final\Table_HimalayanIvory.png'
)
. "$PSScriptRoot\bg.ps1"
$out = Join-Path $Area 'check'
$p = [Img]::Load($Back)
$p.Over([Img]::Load($Table), 0, 0)
if ($Front) { $p.Over([Img]::Load($Front), 0, 0) }
$p.Save("$out\${Name}_composite.png")
$p.Crop($ViewX, $ViewY, $ViewW, $ViewH).Save("$out\${Name}_composite_16x9.png")
$p.Crop(17, $ViewY, 2195, $ViewH).Resize(1464, 628).Save("$out\${Name}_composite_21x9_small.png")
$p.Crop($ViewX, 0, $ViewW, $PlateH).Resize(836, 628).Save("$out\${Name}_composite_4x3_small.png")
# where the table's sides meet the ground: left x 120..520, right x 1710..2110, y 560..1000
$s = New-Object Img 800, 440
$s.Paste($p.Crop(120, 560, 400, 440), 0, 0)
$s.Paste($p.Crop(1710, 560, 400, 440), 400, 0)
$s.Save("$out\${Name}_composite_table_sides.png")
"saved $out\${Name}_composite*.png"
