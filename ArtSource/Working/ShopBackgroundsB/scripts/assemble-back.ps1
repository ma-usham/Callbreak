# Assembles a 2230 x 1255 scenery plate (BG_<Name>_Back.png):
#  - the 1672 x 941 centre picture pasted 1:1 at (279, 157) (the 16:9 gameplay view);
#  - the left, right and top margins from ChatGPT "repaint the strip" outputs (strip-canvas.ps1):
#    each output is aligned to its canvas (similarity fit), colour-matched to the centre on the
#    overlap (low-frequency fit) and joined along a minimum-difference seam inside the centre's edge;
#  - the bottom margin (always under the table and the Front layer) continued from the rows above,
#    and the four corners (never on screen: 21:9 shows only the view's full height, 4:3 only its
#    full width) mirrored.
param(
    [Parameter(Mandatory)] [string] $Centre,
    [Parameter(Mandatory)] [string] $Left,  [Parameter(Mandatory)] [string] $LeftCanvas,
    [Parameter(Mandatory)] [string] $Right, [Parameter(Mandatory)] [string] $RightCanvas,
    [Parameter(Mandatory)] [string] $Top,   [Parameter(Mandatory)] [string] $TopCanvas,
    [Parameter(Mandatory)] [string] $Out,
    [int] $Band = 240,
    [int] $TopBand = 170,
    [int] $Feather = 6,
    [switch] $NoColor
)
. "$PSScriptRoot\bg.ps1"
$b = [Img]::Load($Centre)
$P = New-Object Img $PlateW, $PlateH
[Array]::Clear($P.A, 0, $P.A.Length)   # transparent until filled
$P.Paste($b, $ViewX, $ViewY)

function Get-Aligned([string] $outPath, [string] $canvasPath, [int[]] $roi) {
    $o = [Img]::Load($outPath); $c = [Img]::Load($canvasPath)
    $f = [Img]::FitSimilarity($c, $o, 0.95, 1.05, -30, 30, -30, 30, $roi)
    "  {0}: s {1:F4} tx {2:F2} ty {3:F2} ncc {4:F3}" -f (Split-Path $outPath -Leaf), $f[0], $f[1], $f[2], $f[3] | Write-Host
    [Img]::ClearCache()
    # no edge extension: where the fitted output does not cover its canvas the layer stays empty and
    # is filled below from the rows above (a reflected band made a visible kaleidoscope)
    return [Img]::Warp($o, 1672, 941, $f[0], $f[1], $f[2], 0)
}

function Add-Strip([Img] $aligned, [int] $px, [int] $py, [bool] $vertical, [int] $b0, [int] $b1, [int] $r0, [int] $r1, [bool] $srcLow, [int[]] $overlap) {
    $layer = New-Object Img $PlateW, $PlateH
    [Array]::Clear($layer.A, 0, $layer.A.Length)
    $layer.Paste($aligned, $px, $py)
    if (-not $NoColor) {
        $mask = [Masks]::Rect($PlateW, $PlateH, $overlap[0], $overlap[1], $overlap[2], $overlap[3], 0)
        $mask = [Masks]::Mul($mask, [Masks]::Threshold([Masks]::FromAlpha($layer), 0.99))
        $mask = [Masks]::Mul($mask, [Masks]::Threshold([Masks]::FromAlpha($P), 0.99))
        $k = [Img]::FitColorSmooth($layer, $P, $mask, 12)
        $sane = ($k[0] -gt 0.8 -and $k[0] -lt 1.25 -and $k[2] -gt 0.8 -and $k[2] -lt 1.25 -and $k[4] -gt 0.8 -and $k[4] -lt 1.25)
        "    colour fit (gain, offset) R {0:F3} {1:F1}  G {2:F3} {3:F1}  B {4:F3} {5:F1}  {6}" -f $k[0], $k[1], $k[2], $k[3], $k[4], $k[5], $(if ($sane) { 'applied' } else { 'not applied (out of range)' }) | Write-Host
        if ($sane) { $layer.ApplyColor($k) }
    }
    $seam = [Seams]::Join($P, $layer, $vertical, $b0, $b1, $r0, $r1, $srcLow, $Feather)
    $mn = ($seam | Measure-Object -Minimum -Maximum)
    "    seam between {0} and {1}" -f $mn.Minimum, $mn.Maximum | Write-Host
}

$x1 = $ViewX + $ViewW; $y1 = $ViewY + $ViewH       # 1951, 1098
"left"
$L = Get-Aligned $Left $LeftCanvas @(300, 0, 1360, 360)
Add-Strip $L 0 $ViewY $true $ViewX ($ViewX + $Band) $ViewY $y1 $true @($ViewX, $ViewY, 500, $ViewH)
"right"
$R = Get-Aligned $Right $RightCanvas @(0, 0, 1360, 360)
Add-Strip $R ($x1 - 1393) $ViewY $true ($x1 - $Band) $x1 $ViewY $y1 $false @(($x1 - 500), $ViewY, 500, $ViewH)
"top"
$T = Get-Aligned $Top $TopCanvas @(0, 170, 1672, 360)
Add-Strip $T $ViewX 0 $false $ViewY ($ViewY + $TopBand) $ViewX $x1 $true @($ViewX, $ViewY, $ViewW, 300)
# columns a strip did not reach at the plate's outer edges: mirror the strip's last columns
[Seams]::MirrorFillEmptyEnds($P, $ViewY, $y1, $true)
[Seams]::MirrorFillEmptyEnds($P, $ViewY, $y1, $false)
# top corners (never on screen): the top margin mirrored sideways
[Seams]::MirrorFill($P, 0, 0, $ViewX, $ViewY, 0)
[Seams]::MirrorFill($P, $x1, 0, $PlateW - $x1, $ViewY, 1)
# bottom margin (under the table and the Front layer; its corners never on screen): the rows above
# moved down, joined on a seam only where the table's apron covers the view (x 300..1925), so
# nothing visible in the view or the 21:9 side margins changes.
$down = [Seams]::Shifted($P, 0, ($PlateH - $y1))
$null = [Seams]::Join($P, $down, $false, ($y1 - 60), $y1, 300, 1925, $false, 4)
# side margins: where a strip did not reach the view's bottom row, continue it on a seam above the gap
$null = [Seams]::Join($P, $down, $false, ($y1 - 100), ($y1 - 20), 0, 300, $false, 4)
$null = [Seams]::Join($P, $down, $false, ($y1 - 100), ($y1 - 20), 1925, $PlateW, $false, 4)
[Seams]::FillEmpty($P, $down)
$P.FillHoles()
$P.Save($Out)
"saved $Out  " + $P.Stats()
