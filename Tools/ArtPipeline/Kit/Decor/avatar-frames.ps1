# Draws the two pieces the name screen's character picker adds to a chosen portrait (ADR 043), as
# the owner's mockup (Assets/ArtRefrences/game-screens/22-name-entry.png) shows them, measured on it:
#   UI_AvatarRing_Chosen.png  a thin dark-crimson ring that hugs a framed portrait's own gold ring
#                             (its inner edge at 94 % of the radius, where Avatar_Framed_* end);
#   UI_Check_Badge_Gold.png   a dark-brown disc in a gold rim with a gold tick, sitting on the ring.
# Both are plain shapes, drawn here at four times their size and resampled down (like the kit's
# white avatar disc, which is drawn too), so their edges are clean at any size.
#   avatar-frames.ps1 [-RingSize 320] [-BadgeSize 112] [-Install]
param(
    [int] $RingSize = 320,
    [int] $BadgeSize = 112,
    [switch] $Install
)
. (Join-Path $PSScriptRoot 'decor.ps1')
$fitDir = "$DecorWork\fit"; New-Item -ItemType Directory -Force $fitDir | Out-Null
function C([string] $hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
function New-Canvas([int] $n) {
    $b = New-Object System.Drawing.Bitmap $n, $n, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    return @($b, $g)
}
function Ring($g, [double] $c, [double] $outer, [double] $inner, $color) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse([float]($c - $outer), [float]($c - $outer), [float](2 * $outer), [float](2 * $outer))
    $path.AddEllipse([float]($c - $inner), [float]($c - $inner), [float](2 * $inner), [float](2 * $inner))
    $path.FillMode = [System.Drawing.Drawing2D.FillMode]::Alternate
    $g.FillPath((New-Object System.Drawing.SolidBrush $color), $path)
    $path.Dispose()
}
function Save-Down($big, [int] $size, [string] $name) {
    $small = [DecorTools]::Resize($big, $size, $size)
    $path = Join-Path $fitDir $name; [DecorTools]::Save($small, $path); $small.Dispose()
    return $path
}

# --- the crimson ring: mockup (98,4,0) at its edges and (114,12,8) along its middle
$n = $RingSize * 4; $c = $n / 2.0; $outer = $n / 2.0; $inner = $outer * 0.94; $w = $outer - $inner
$canvas = New-Canvas $n; $b = $canvas[0]; $g = $canvas[1]
Ring $g $c $outer $inner (C '#5E0403')
Ring $g $c ($outer - $w * 0.18) ($inner + $w * 0.18) (C '#6E0806')
Ring $g $c ($outer - $w * 0.38) ($inner + $w * 0.38) (C '#7C0E0A')
$g.Dispose()
$ringPath = Save-Down $b $RingSize 'UI_AvatarRing_Chosen.png'; $b.Dispose()

# --- the gold check badge
$n = $BadgeSize * 4; $c = $n / 2.0
$canvas = New-Canvas $n; $b = $canvas[0]; $g = $canvas[1]
$rect = New-Object System.Drawing.RectangleF 0, 0, $n, $n
$rim = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (C '#F7DC8E'), (C '#B07A2A'), ([float]90)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#6E4A1A')), 0, 0, $n, $n)
$edge = $n * 0.018
$g.FillEllipse($rim, [float]$edge, [float]$edge, [float]($n - 2 * $edge), [float]($n - 2 * $edge))
$discR = $n * 0.40
$disc = New-Object System.Drawing.Drawing2D.GraphicsPath
$disc.AddEllipse([float]($c - $discR), [float]($c - $discR), [float](2 * $discR), [float](2 * $discR))
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#8A6224')), [float]($c - $discR - $n * 0.012), [float]($c - $discR - $n * 0.012), [float](2 * $discR + $n * 0.024), [float](2 * $discR + $n * 0.024))
$brown = New-Object System.Drawing.Drawing2D.PathGradientBrush $disc
$brown.CenterColor = C '#7A4C22'
$brown.SurroundColors = [System.Drawing.Color[]]@((C '#4A2A0E'))
$brown.CenterPoint = New-Object System.Drawing.PointF ([float]($c - $discR * 0.25)), ([float]($c - $discR * 0.3))
$g.FillPath($brown, $disc)
$tick = New-Object System.Drawing.Drawing2D.GraphicsPath
$tick.AddLines([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF ([float]($n * 0.305)), ([float]($n * 0.515))),
    (New-Object System.Drawing.PointF ([float]($n * 0.445)), ([float]($n * 0.655))),
    (New-Object System.Drawing.PointF ([float]($n * 0.705)), ([float]($n * 0.375)))))
$gold = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (C '#FFF0BE'), (C '#D8A64E'), ([float]90)
$pen = New-Object System.Drawing.Pen $gold, ([float]($n * 0.115))
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Flat
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Flat
$g.DrawPath($pen, $tick)
$g.Dispose()
$badgePath = Save-Down $b $BadgeSize 'UI_Check_Badge_Gold.png'; $b.Dispose()

"ring   $ringPath  ($RingSize x $RingSize, inner edge at 94 %)"
"badge  $badgePath  ($BadgeSize x $BadgeSize)"
if ($Install) {
    $exp = "$DecorRepo\ArtSource\Exports\Decor"; New-Item -ItemType Directory -Force $exp | Out-Null
    Copy-Item $ringPath "$DecorArt\UI\Avatars\UI_AvatarRing_Chosen.png" -Force
    Copy-Item $badgePath "$DecorArt\UI\Controls\UI_Check_Badge_Gold.png" -Force
    Copy-Item $ringPath, $badgePath $exp -Force
    "installed UI/Avatars/UI_AvatarRing_Chosen.png and UI/Controls/UI_Check_Badge_Gold.png  (+ ArtSource/Exports/Decor)"
}
