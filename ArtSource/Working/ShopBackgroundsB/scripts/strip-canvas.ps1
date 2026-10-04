# Builds a 1672 x 941 "repaint the strip" canvas: the centre picture at 1:1, shifted so that a strip
# of Width px is free along one side, and that strip filled with a blurred mirror of the picture's own
# edge (a placeholder ChatGPT repaints; a magenta strip made it re-frame the whole picture).
param(
    [Parameter(Mandatory)] [string] $Src,      # the 1672 x 941 centre picture
    [Parameter(Mandatory)] [ValidateSet('Left', 'Right', 'Top', 'Bottom')] [string] $Side,
    [Parameter(Mandatory)] [string] $Out,
    [int] $Width = 0,                          # strip width; default 279 (sides) / 157 (top, bottom)
    [double] $Sigma = 14
)
. "$PSScriptRoot\bg.ps1"
$b = [Img]::Load($Src)
if ($Width -le 0) { $Width = if ($Side -in 'Left', 'Right') { 279 } else { 157 } }
$W = 1672; $H = 941; $c = New-Object Img $W, $H
$sideNo = @{ Left = 0; Right = 1; Top = 2; Bottom = 3 }[$Side]
switch ($Side) {
    'Left'   { $c.Paste($b.Crop(0, 0, $W - $Width, $H), $Width, 0); $mask = [Masks]::Rect($W, $H, 0, 0, $Width, $H, 0) }
    'Right'  { $c.Paste($b.Crop($Width, 0, $W - $Width, $H), 0, 0); $mask = [Masks]::Rect($W, $H, $W - $Width, 0, $Width, $H, 0) }
    'Top'    { $c.Paste($b.Crop(0, 0, $W, $H - $Width), 0, $Width); $mask = [Masks]::Rect($W, $H, 0, 0, $W, $Width, 0) }
    'Bottom' { $c.Paste($b.Crop(0, $Width, $W, $H - $Width), 0, 0); $mask = [Masks]::Rect($W, $H, 0, $H - $Width, $W, $Width, 0) }
}
$m = $c.Clone(); $m.MirrorStrip($sideNo, $Width)
$bl = $m.Blur($Sigma)
$c.LerpTo($bl, $mask)      # the picture stays sharp; only the strip is the blurred mirror
$c.Save($Out)
"$Out  ($Side strip $Width px, blurred mirror sigma $Sigma)"
