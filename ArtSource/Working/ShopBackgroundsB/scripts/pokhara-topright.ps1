# Pokhara Lakeside, upper-right fix: replaces the willow and dark hills behind the HUD's "Scores" /
# "Settings" with ChatGPT's bright cloud bank (chatgpt/Pokhara_TopRight_a2.png, a blurred-guide edit
# of the plate window x 558..2230, y 0..941). The edit is aligned to the window with a local
# displacement field (it came back with shifts up to 10 px), colour-matched on the unedited part, and
# joined to the window along two minimum-difference seams (below and left of the edited area).
param(
    [Parameter(Mandatory)] [string] $Plate,      # assembled Back plate (assemble-back.ps1 output)
    [Parameter(Mandatory)] [string] $Out
)
. "$PSScriptRoot\bg.ps1"
$wx = 558
$p = [Img]::Load($Plate)
$old = $p.Crop($wx, 0, 1672, 941)
$o = [Img]::Load("$Area\chatgpt\Pokhara_TopRight_a2.png")
# displacement field measured outside the edited area
$m = New-Object 'float[]' (1672 * 941)
for ($y = 0; $y -lt 941; $y++) { for ($x = 0; $x -lt 1672; $x++) { if ((($x -ge 1040 -and $y -le 360) -or ($x -ge 1210 -and $y -le 470))) { $m[$y * 1672 + $x] = 1 } } }
$r = [Img]::FlowField($old, $o, 96, 48, 14, 0.55, $m, -4, 0, 1.0)
$info = $r[2]
"flow: valid tiles {0} of {1}, median ncc {2:F3}" -f $info[0], $info[1], $info[2]
$new = [Img]::WarpField($o, $r[0], $r[1], 1672, 941)
[Img]::ClearCache()
$k = [Img]::FitColorSmooth($new, $old, [Masks]::Rect(1672, 941, 0, 0, 1000, 941, 0), 12)
"colour fit: R {0:F3} {1:F1}  G {2:F3} {3:F1}  B {4:F3} {5:F1}" -f $k[0], $k[1], $k[2], $k[3], $k[4], $k[5]
$new.ApplyColor($k)
$d = $old.Clone()
$s1 = [Seams]::Join($d, $new, $false, 600, 720, 880, 1672, $true, 6)
$s2 = [Seams]::Join($d, $old, $true, 880, 1100, 0, 720, $true, 6)
"seams: horizontal y {0}..{1}, vertical x {2}..{3}" -f ($s1 | Measure-Object -Minimum).Minimum, ($s1 | Measure-Object -Maximum).Maximum, ($s2 | Measure-Object -Minimum).Minimum, ($s2 | Measure-Object -Maximum).Maximum
$p.Paste($d, $wx, 0)
$p.Save($Out)
"saved $Out"
