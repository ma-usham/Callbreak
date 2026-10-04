# Rebuilds every ShopBackgroundsB final from the ChatGPT downloads (chatgpt/) and the attachments
# (attach/). Intermediate files go to work/ (not kept in the repo). Usage:
#   & ArtSource\Working\ShopBackgroundsB\scripts\build-finals.ps1 [-OutDir <folder>]   (default final/)
param([string] $OutDir = '')
. "$PSScriptRoot\bg.ps1"
$s = $Area; $c = "$s\chatgpt"; $a = "$s\attach"; $w = "$s\work"
if (-not $OutDir) { $OutDir = "$s\final" }
New-Item -ItemType Directory -Force $w, $OutDir | Out-Null

# Pokhara Lakeside: plate, upper-right fix, blue grade
& "$PSScriptRoot\assemble-back.ps1" -Centre "$c\Pokhara_NoTable_a1.png" -Left "$c\Pokhara_Left_a2.png" -LeftCanvas "$a\Pokhara_Left_input2.png" -Right "$c\Pokhara_Right_a1.png" -RightCanvas "$a\Pokhara_Right_input2.png" -Top "$c\Pokhara_Top_a1.png" -TopCanvas "$a\Pokhara_Top_input2.png" -Out "$w\Pokhara_assembled.png" -TopBand 60
& "$PSScriptRoot\pokhara-topright.ps1" -Plate "$w\Pokhara_assembled.png" -Out "$w\Pokhara_fixed.png"
$p = [Img]::Load("$w\Pokhara_fixed.png"); $p.ScaleSatHue(195, 225, 25, 0.70); $p.Save24("$OutDir\BG_PokharaLakeside_Back.png")

# Monastery Garden plate
& "$PSScriptRoot\assemble-back.ps1" -Centre "$c\Monastery_NoTable_a1.png" -Left "$c\Monastery_Left_a1.png" -LeftCanvas "$a\Monastery_Left_input.png" -Right "$c\Monastery_Right_a1.png" -RightCanvas "$a\Monastery_Right_input.png" -Top "$c\Monastery_Top_a1.png" -TopCanvas "$a\Monastery_Top_input.png" -Out "$w\Monastery_assembled.png" -TopBand 60
[Img]::Load("$w\Monastery_assembled.png").Save24("$OutDir\BG_MonasteryGarden_Back.png")

# Front layers
& "$PSScriptRoot\fit-front.ps1" -Src "$c\Pokhara_Front_a1.png" -Out "$OutDir\BG_PokharaLakeside_Front.png"
& "$PSScriptRoot\fit-front.ps1" -Src "$c\Monastery_Front_a1.png" -Out "$OutDir\BG_MonasteryGarden_Front.png"

# Tile pictures (590 x 670) with their colour grades
& "$PSScriptRoot\make-thumb.ps1" -Src "$c\Thumb_Pokhara_a1.png" -Out "$w\Thumb_PokharaLakeside.png"
& "$PSScriptRoot\make-thumb.ps1" -Src "$c\Thumb_Monastery_a1.png" -Out "$w\Thumb_MonasteryGarden.png"
& "$PSScriptRoot\make-thumb.ps1" -Src "$c\Thumb_HimalayanVista_a1.png" -Out "$w\Thumb_HimalayanVista.png"
$t = [Img]::Load("$w\Thumb_PokharaLakeside.png"); $t.ScaleSatHue(195, 225, 25, 0.70); $t.ScaleSatLuma(0.75); $t.Save24("$OutDir\Thumb_PokharaLakeside.png")
$t = [Img]::Load("$w\Thumb_MonasteryGarden.png"); $t.ScaleSatLuma(0.70); $t.Save24("$OutDir\Thumb_MonasteryGarden.png")
$t = [Img]::Load("$w\Thumb_HimalayanVista.png"); $t.ScaleSatLuma(0.75); $t.Save24("$OutDir\Thumb_HimalayanVista.png")
Get-ChildItem $OutDir -Filter *.png | ForEach-Object { "{0,-32} {1,10:N0} bytes" -f $_.Name, $_.Length }
