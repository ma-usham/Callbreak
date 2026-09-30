# Builds the patch mask for BG_Menu_Clean: white where the main-menu mockup has UI (grown a
# little to take in shadows and glows), black where the painting is untouched, feathered.
param(
    [Parameter(Mandatory)] [string] $Out,
    [double] $Grow = 10,
    [int] $Feather = 5
)
$sp = $PSScriptRoot
. "$sp\art.ps1"

$bmp = New-Object System.Drawing.Bitmap 1672, 941, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Black)
$white = [System.Drawing.Brushes]::White

function Circle($cx, $cy, $r) { $rr = $r + $Grow; $g.FillEllipse($white, [single]($cx - $rr), [single]($cy - $rr), [single](2 * $rr), [single](2 * $rr)) }
function Box($x, $y, $w, $h) { $g.FillRectangle($white, [single]($x - $Grow), [single]($y - $Grow), [single]($w + 2 * $Grow), [single]($h + 2 * $Grow)) }
function Pill($x, $y, $w, $h) {
    $x -= $Grow; $y -= $Grow; $w += 2 * $Grow; $h += 2 * $Grow
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc([single]$x, [single]$y, [single]$h, [single]$h, 90, 180)
    $path.AddArc([single]($x + $w - $h), [single]$y, [single]$h, [single]$h, 270, 180)
    $path.CloseFigure(); $g.FillPath($white, $path); $path.Dispose()
}
function Card($cx, $cy, $w, $h, $tilt) {
    $state = $g.Save()
    $g.TranslateTransform([single]$cx, [single]$cy); $g.RotateTransform([single]$tilt)
    $g.FillRectangle($white, [single](-$w / 2 - $Grow), [single](-$h / 2 - $Grow), [single]($w + 2 * $Grow), [single]($h + 2 * $Grow))
    $g.Restore($state)
}

# Header
Circle 127.5 100 54            # avatar ring and its shadow
Box 203 58 125 68              # Namaste, Player
Circle 1402 99 48              # settings button and its shadow
Circle 1547.5 99 48            # shop button and its shadow
Box 1472 63 5 70               # divider

# Logo
Box 798 32 90 120              # flag
Box 483 160 705 166            # lettering, subtitle and rules

# Card fan: centre, size, clockwise tilt
Card 680.8 492.4 167.7 234.8 -23.2
Card 788.3 459.1 167.7 234.8 -6.9
Card 909.1 483.5 167.7 234.8 14.1
Card 1008.3 525 167.7 234.8 25.9

# Buttons and footer. The three buttons and the shadowed gap between them go as one block,
# otherwise the primary button's drop shadow survives as a dark line.
Pill 494 566 683.5 140
Pill 489 737.5 335 94
Pill 850 737.5 331 94
Box 520 700 630 50             # gap between the primary and secondary buttons
Box 520 864 630 26             # tagline and its two rules

$g.Dispose()
$blurred = [ArtTools]::Blur($bmp, $Feather); $bmp.Dispose()
[ArtTools]::Save($blurred, $Out); $blurred.Dispose()
$Out
