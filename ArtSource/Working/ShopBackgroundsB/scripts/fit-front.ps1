# Front layer: ChatGPT's 1672 x 941 transparent foreground (framed like the plate at 75 %), alpha
# levelled (ChatGPT cut-outs: 250-254 inside, faint haze outside), upscaled x1.3337 to the plate frame
# (Lanczos-3, premultiplied), then each corner cluster scaled toward its own bottom corner so its reach
# matches BG_Table's own flowers (ShopTables' BG_HimalayanVista_Front) and stays off the playing
# surface and the hand area.
param(
    [Parameter(Mandatory)] [string] $Src,
    [Parameter(Mandatory)] [string] $Out,
    [double] $LeftScale = 0.82,
    [double] $RightScale = 0.85
)
. "$PSScriptRoot\bg.ps1"
$f = [Img]::Load($Src)
$f.AlphaLevels(12, 250)
$F = $f.Resize($PlateW, $PlateH)
$half = [int]($PlateW / 2)
# left cluster about (0, H), right cluster about (W, H): out(x) = in(c + (x - c) / k)
$kL = $LeftScale; $kR = $RightScale
$L = [Img]::WarpRGBA($F, $PlateW, $PlateH, 1 / $kL, 0, $PlateH * (1 - 1 / $kL), 0, $half)
$R = [Img]::WarpRGBA($F, $PlateW, $PlateH, 1 / $kR, $PlateW * (1 - 1 / $kR), $PlateH * (1 - 1 / $kR), $half, $PlateW)
# sources must come from the matching half only: blank the other half of each source sample
$o = New-Object Img $PlateW, $PlateH
[Array]::Clear($o.A, 0, $o.A.Length)
$o.Over($L, 0, 0); $o.Over($R, 0, 0)
$o.Save($Out)
"{0}: {1}" -f (Split-Path $Out -Leaf), $o.Stats()
"  left cluster box {0}; right cluster box {1}" -f ($o.AlphaBox(20, 0, $half) -join ','), ($o.AlphaBox(20, $half, $PlateW) -join ',')
"  reach in rows 957..1098: left to x {0}, right from x {1}" -f $o.ExtentInRows(20, 957, 1098, $true, $half), $o.ExtentInRows(20, 957, 1098, $false, $half)
$over = 0; $surf = 0
for ($y = 441; $y -lt 1044; $y += 2) { for ($x = 334; $x -lt 1895; $x += 2) { $u = ($x - 1114.3) / 780.2; $v = ($y - 742.2) / 301.4; if ($u * $u + $v * $v -le 1) { $surf++; if ($o.A[$y * $PlateW + $x] -gt 20) { $over++ } } } }
"  covers {0:P2} of the ivory playing surface (alpha > 20)" -f ($over / $surf)
