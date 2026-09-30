# Turns one ChatGPT extraction into a finished Decor sprite and checks it against the mockup.
#   fit-decor.ps1 -Name Deco_SpadeMedal -Src <chatgpt png> -Screen 09 -Rect 796,203,79,78 [-Install]
# Steps: cut out (real alpha, or key out the flat ground), trim, find where it sits on the mockup
# (FitOnMockup), match its colours to the mockup there, resize to @2x (mockup px x 2.3, even),
# write <Name>.png plus check sheets into ArtSource/Working/Kit/Decor/fit/.
# -Install also copies it to Assets/_Project/Art/<OutFolder>/ and ArtSource/Exports/Decor/.
param(
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [string] $Src,
    [Parameter(Mandatory)] [string] $Screen,
    [double[]] $Rect,                                 # guess: the element's visible pixels on the mockup (x,y,w,h)
    [double[]] $CanvasRect,                           # instead of -Rect: where the whole source canvas sits on the mockup
    [double] $GroundDead = 0.06,                      # -Key ground: differences below this share of the darkest tone are paper noise
    [int] $GroundBlur = 2,                            # -Key ground: blur radius applied before matting
    [double[]] $ToneOffset,                           # -Key ground: r,g,b added to the matted tones (ground of the drawing vs ground on the mockup)
    [string] $OutFolder = 'UI/Decor',
    [string] $Plate,                                  # UI-free plate aligned to the mockup (for elements on scenery)
    [string] $GroundColor,                            # '#RRGGBB': flat ground for the fit and the check when the ring around the element is not plain
    [int[]] $SourceCrop,                              # x,y,w,h inside the ChatGPT image (one cell of a sheet)
    [ValidateSet('auto', 'alpha', 'color', 'ground')] [string] $Key = 'auto',
    [string] $KeyColor,                               # '#RRGGBB'; default = the image's border colour
    [double] $KeyLo = 10, [double] $KeyHi = 60,
    [switch] $KeyEverywhere,                          # also remove the ground colour inside the object
    [int] $SpeckArea = 40,
    [switch] $NoTrim,                                 # keep the source canvas (e.g. padding that centres a rotating sprite)
    [int] $FixFringe = 0,                             # glow sprites: alpha below this takes the colour of the stronger pixels around it
    [int] $Soften = 0,                                # glow sprites: blur radius in cut-out pixels
    [double] $RotateBy = 0,                           # small tilt correction, degrees clockwise
    [string] $Recolor,                                # '#RRGGBB': flat single-colour ornament
    [switch] $NoColorFit,
    [switch] $OffsetOnly,
    [switch] $FreeAspect,
    [switch] $MirrorLeft,
    [switch] $FlipH,
    [ValidateSet(0, 90, 180, 270)] [int] $Rotate = 0,  # degrees clockwise, applied to the trimmed cut-out
    [double] $Opacity = 1,
    [double] $LineThickness = 0,                      # mockup px: thicken the hair line of a line ornament to this
    [double[]] $Diamond,                              # line ornament: the diamond's start and width along the line (and its size across), mockup px
    [int[]] $DiamondSrcCrop,                          # x,y,w,h in -Src of a larger diamond of the same family to use instead of an upscaled one
    [switch] $LineInk,                                # line ornament: flat ink and line strength measured on the mockup
    [switch] $LineVertical,
    [int] $MinColourPixels = 40,
    [int] $Margin = 6,
    [double] $ExportScale = 2.3,
    [switch] $NoFit,                                  # trust -Rect as the final rectangle
    [switch] $Install
)
. (Join-Path $PSScriptRoot 'decor.ps1')
$fitDir = "$DecorWork\fit"; New-Item -ItemType Directory -Force $fitDir | Out-Null

# 1. load, optional cell crop
$raw = [DecorTools]::Load($Src)
if ($SourceCrop) { $c = [DecorTools]::Crop($raw, $SourceCrop[0], $SourceCrop[1], $SourceCrop[2], $SourceCrop[3]); $raw.Dispose(); $raw = $c }
"source   {0}x{1}  {2}" -f $raw.Width, $raw.Height, [DecorTools]::Stats($raw)

# 2. cut out
$px = $raw.GetPixel(1, 1)
$hasAlpha = ($px.A -lt 10) -or ($raw.GetPixel($raw.Width - 2, $raw.Height - 2).A -lt 10)
$srcW = $raw.Width; $srcH = $raw.Height
if ($Key -eq 'ground') {
    $info = ''
    if ($GroundBlur -gt 0) { $bl = [DecorTools]::BlurRgb($raw, $GroundBlur); $raw.Dispose(); $raw = $bl }
    $cut = [DecorTools]::MatteOnGround($raw, $GroundDead, [ref]$info)
    if ($ToneOffset) { $f = [DecorTools]::ApplyColor($cut, [double[]]@(1, $ToneOffset[0], 1, $ToneOffset[1], 1, $ToneOffset[2])); $cut.Dispose(); $cut = $f }
    "cut-out  matte against the flat ground ($info, blur $GroundBlur, dead zone $GroundDead)"
}
elseif ($Key -eq 'alpha' -or ($Key -eq 'auto' -and $hasAlpha)) {
    $cut = [DecorTools]::AlphaLevels($raw, 12, 250)
    "cut-out  real alpha (levels 12..250)"
}
else {
    $kc = if ($KeyColor) { [System.Drawing.ColorTranslator]::FromHtml($KeyColor) } else { [DecorTools]::BorderColor($raw) }
    if ($KeyHi -le 0) { $KeyHi = [DecorTools]::AutoKeyHi($raw, $kc) }
    $cut = [DecorTools]::KeyColor($raw, $kc, $KeyLo, $KeyHi, (-not $KeyEverywhere))
    "cut-out  keyed #{0:X2}{1:X2}{2:X2} (lo {3}, hi {4}, {5})" -f $kc.R, $kc.G, $kc.B, $KeyLo, $KeyHi, $(if ($KeyEverywhere) { 'everywhere' } else { 'connected to the border' })
}
$raw.Dispose()
if ($SpeckArea -gt 0) { $d = [DecorTools]::DropSpecks($cut, 24, $SpeckArea); $cut.Dispose(); $cut = $d }
$b = if ($NoTrim) { @(0, 0, $cut.Width, $cut.Height) } else { [DecorTools]::AlphaBounds($cut, 8) }
$trim = [DecorTools]::Crop($cut, $b[0], $b[1], $b[2], $b[3]); $cut.Dispose()
if ($CanvasRect) {
    # The whole source canvas has a known place on the mockup: the trimmed sprite's follows.
    $sx = $CanvasRect[2] / $srcW; $sy = $CanvasRect[3] / $srcH
    $Rect = @(($CanvasRect[0] + $b[0] * $sx), ($CanvasRect[1] + $b[1] * $sy), ($b[2] * $sx), ($b[3] * $sy))
    $NoFit = $true
}
if (-not $Rect) { throw 'give -Rect or -CanvasRect' }
if ($FixFringe -gt 0) {
    # pad first so the blur of a following -Soften has room, then give faint pixels the glow's colour
    $f = [DecorTools]::FixFringe($trim, $FixFringe, [int][Math]::Max(6, $trim.Width / 12)); $trim.Dispose(); $trim = $f
}
if ($Soften -gt 0) {
    $pad = $Soften * 3
    $p = [DecorTools]::Place($trim, $trim.Width + 2 * $pad, $trim.Height + 2 * $pad, $pad, $pad); $trim.Dispose()
    $f = [DecorTools]::Soften($p, $Soften); $p.Dispose()
    $bb = [DecorTools]::AlphaBounds($f, 4)
    $trim = [DecorTools]::Crop($f, $bb[0], $bb[1], $bb[2], $bb[3]); $f.Dispose()
}
if ($RotateBy -ne 0) { $f = [DecorTools]::RotateSmall($trim, $RotateBy); $trim.Dispose(); $bb = [DecorTools]::AlphaBounds($f, 8); $trim = [DecorTools]::Crop($f, $bb[0], $bb[1], $bb[2], $bb[3]); $f.Dispose() }
if ($Rotate -ne 0) { $f = [DecorTools]::Rotate($trim, $Rotate); $trim.Dispose(); $trim = $f }
if ($FlipH) { $f = [DecorTools]::FlipH($trim); $trim.Dispose(); $trim = $f }
if ($MirrorLeft) { $f = [DecorTools]::MirrorLeftHalf($trim); $trim.Dispose(); $trim = $f }
if ($Recolor) { $f = [DecorTools]::Recolor($trim, [System.Drawing.ColorTranslator]::FromHtml($Recolor)); $trim.Dispose(); $trim = $f }
if ($Opacity -lt 1) { $f = [DecorTools]::ScaleAlpha($trim, $Opacity); $trim.Dispose(); $trim = $f }
$perMockFixed = 0
if ($Diamond) {
    # Line ornament laid out from mockup measurements: the diamond keeps its shape at its mockup
    # size, the line parts are stretched to the mockup's lengths (3-slice along the line).
    $span = [DecorTools]::DiamondSpan($trim, [bool]$LineVertical)
    $dc = $span[1] - $span[0] + 1
    $s = $Diamond[1] / $dc                                   # mockup px per cut-out px
    $axisStart = if ($LineVertical) { $Rect[1] } else { $Rect[0] }
    $axisLen = if ($LineVertical) { $Rect[3] } else { $Rect[2] }
    $nb = [int][Math]::Round(($Diamond[0] - $axisStart) / $s)
    $na = [int][Math]::Round(($axisStart + $axisLen - $Diamond[0] - $Diamond[1]) / $s)
    $f = [DecorTools]::StretchLines($trim, [bool]$LineVertical, $nb, $na); $trim.Dispose(); $trim = $f
    if ($LineVertical) { $am = $trim.Width * $s; $Rect = @(($Rect[0] + $Rect[2] / 2 - $am / 2), $Rect[1], $am, $Rect[3]) }
    else { $am = $trim.Height * $s; $Rect = @($Rect[0], ($Rect[1] + $Rect[3] / 2 - $am / 2), $Rect[2], $am) }
    $NoFit = $true; $perMockFixed = 1 / $s
    "layout   diamond {0} cut-out px = {1} mockup px; line parts {2} and {3} px; rect {4:F1},{5:F1},{6:F1},{7:F1}" -f $dc, $Diamond[1], $nb, $na, $Rect[0], $Rect[1], $Rect[2], $Rect[3]
}
if ($LineThickness -gt 0) {
    $perMock = if ($perMockFixed -gt 0) { $perMockFixed } elseif ($LineVertical) { $trim.Height / $Rect[3] } else { $trim.Width / $Rect[2] }
    $info = ''
    $f = [DecorTools]::ThickenLine($trim, $LineThickness * $perMock, [bool]$LineVertical, [ref]$info); $trim.Dispose(); $trim = $f
    "line     $info  ({0:F2} cut-out px per mockup px)" -f $perMock
}
"trimmed  at {0},{1}  {2}" -f $b[0], $b[1], [DecorTools]::Stats($trim)

# 3. where it sits on the mockup
$mock = [DecorTools]::Load((Get-Mockup $Screen))
$bg = $null
if ($GroundColor) { $bg = [DecorTools]::Flat($mock.Width, $mock.Height, [System.Drawing.ColorTranslator]::FromHtml($GroundColor)) }
elseif ($Plate) {
    $bg = [DecorTools]::Load($Plate)
    if ($bg.Width -ne $mock.Width -or $bg.Height -ne $mock.Height) { $r = [DecorTools]::Resize($bg, $mock.Width, $mock.Height); $bg.Dispose(); $bg = $r }
}
if ($LineInk) {
    if (-not $Diamond) { throw '-LineInk needs -Diamond' }
    # Flat ink measured on the mockup's diamond; line strength measured along the mockup's line.
    $prof = [DecorTools]::LineProfile($mock, $Rect[0], $Rect[1], $Rect[2], $Rect[3], $Diamond[0], $Diamond[1], [bool]$LineVertical)
    $f = [DecorTools]::ApplyLineProfile($trim, [bool]$LineVertical, $prof); $trim.Dispose(); $trim = $f
    $st = $prof[6..($prof.Length - 1)] | Sort-Object
    "ink      #{0:X2}{1:X2}{2:X2} on ground #{3:X2}{4:X2}{5:X2}; line strength along the mockup: min {6:F2}, median {7:F2}, max {8:F2}" -f [int]$prof[0], [int]$prof[1], [int]$prof[2], [int]$prof[3], [int]$prof[4], [int]$prof[5], $st[0], $st[[int]($st.Count / 2)], $st[-1]
    $NoColorFit = $true
}
if ($NoFit) { $fit = @($Rect[0], $Rect[1], $Rect[2], $Rect[3], 0, 0) }
else { $fit = [DecorTools]::FitOnMockup($mock, $bg, $trim, $Rect[0], $Rect[1], $Rect[2], $Rect[3], [bool]$FreeAspect, $Margin) }
"fitted   x {0:F2} y {1:F2} w {2:F2} h {3:F2}   error {4:F2} -> {5:F2}   (guess {6})" -f $fit[0], $fit[1], $fit[2], $fit[3], $fit[4], $fit[5], ($Rect -join ',')

# 4. colour
if (-not $NoColorFit -and -not $Recolor) {
    $k = [DecorTools]::FitSpriteColor($mock, $trim, $fit[0], $fit[1], $fit[2], $fit[3], [bool]$OffsetOnly)
    "colour   gain/offset r {0:F3} {1:F1}  g {2:F3} {3:F1}  b {4:F3} {5:F1}  on {6} px" -f $k[0], $k[1], $k[2], $k[3], $k[4], $k[5], $k[6]
    if ($k[6] -ge $MinColourPixels) { $f = [DecorTools]::ApplyColor($trim, $k[0..5]); $trim.Dispose(); $trim = $f } else { "colour   too few opaque pixels, left as is" }
}
$errFinal = [DecorTools]::CompositeError($mock, $bg, $trim, $fit[0], $fit[1], $fit[2], $fit[3], $Margin)

# 5. export size: mockup pixels x 2.3, rounded up to even
function Even([double] $v) { $n = [int][Math]::Ceiling($v - 1e-6); if ($n % 2) { $n + 1 } else { $n } }
$W = Even ($fit[2] * $ExportScale); $H = Even ($fit[3] * $ExportScale)
$up = $W / $trim.Width
$final = [DecorTools]::Resize($trim, $W, $H)
if ($Diamond) {
    $dscale = $Diamond[1] * $ExportScale / $dc
    "diamond  {0} cut-out px -> {1:F0} export px ({2:F2}x){3}" -f $dc, ($Diamond[1] * $ExportScale), $dscale, $(if ($dscale -gt 1.05 -and -not $DiamondSrcCrop) { '  (upscaled: consider -DiamondSrcCrop)' } else { '' })
}
if ($Diamond -and $DiamondSrcCrop) {
    # A sharper diamond from a larger ornament of the same sheet replaces the upscaled one.
    $rawD = [DecorTools]::Load($Src)
    $cD = [DecorTools]::Crop($rawD, $DiamondSrcCrop[0], $DiamondSrcCrop[1], $DiamondSrcCrop[2], $DiamondSrcCrop[3]); $rawD.Dispose()
    if ($cD.GetPixel(1, 1).A -lt 10) { $kD = [DecorTools]::AlphaLevels($cD, 12, 250) }
    else { $kcD = [DecorTools]::BorderColor($cD); $kD = [DecorTools]::KeyColor($cD, $kcD, $KeyLo, [DecorTools]::AutoKeyHi($cD, $kcD), $true) }
    $cD.Dispose()
    $bD = [DecorTools]::AlphaBounds($kD, 8); $dia = [DecorTools]::Crop($kD, $bD[0], $bD[1], $bD[2], $bD[3]); $kD.Dispose()
    if ($LineInk) { $f = [DecorTools]::Recolor($dia, [System.Drawing.Color]::FromArgb(255, [int]$prof[0], [int]$prof[1], [int]$prof[2])); $dia.Dispose(); $dia = $f }
    $alongPx = if ($LineVertical) { $H / $fit[3] } else { $W / $fit[2] }
    $acrossPx = if ($LineVertical) { $W / $fit[2] } else { $H / $fit[3] }
    $axis0 = if ($LineVertical) { $fit[1] } else { $fit[0] }
    $dh = if ($Diamond.Count -gt 2) { $Diamond[2] } else { $Diamond[1] }
    $f = [DecorTools]::ReplaceDiamond($final, [bool]$LineVertical, $dia, (($Diamond[0] + $Diamond[1] / 2 - $axis0) * $alongPx), ($Diamond[1] * $alongPx), ($dh * $acrossPx))
    $final.Dispose(); $final = $f
    "diamond  replaced by the {0}x{1} diamond from crop {2}" -f $dia.Width, $dia.Height, ($DiamondSrcCrop -join ',')
    $dia.Dispose()
}
$out = "$fitDir\$Name.png"; [DecorTools]::Save($final, $out)
"export   {0}x{1}  (ChatGPT cut-out {2}x{3}, scale {4:F2}x{5})   error vs mockup {6:F2}" -f $W, $H, $trim.Width, $trim.Height, $up, $(if ($up -gt 2) { '  ** more than 2x upscale **' } else { '' }), $errFinal
"         {0}" -f [DecorTools]::Stats($final)

# 6. check sheets
$scale = [int][Math]::Max(2, [Math]::Min(6, [Math]::Floor(620 / ($fit[2] + 2 * $Margin))))
$sheet = [DecorTools]::CheckSheet($mock, $bg, $final, $fit[0], $fit[1], $fit[2], $fit[3], $Margin, $scale, 4)
[DecorTools]::Save($sheet, "$fitDir\${Name}_check.png"); $sheet.Dispose()
$alpha = [DecorTools]::AlphaSheet($final, 760); [DecorTools]::Save($alpha, "$fitDir\${Name}_alpha.png"); $alpha.Dispose()
"checks   $fitDir\${Name}_check.png (mockup | sprite on ground | difference x4, at ${scale}x)  and  ${Name}_alpha.png"

if ($Install) {
    $dst = Join-Path $DecorArt ($OutFolder -replace '/', '\')
    New-Item -ItemType Directory -Force $dst | Out-Null
    Copy-Item $out (Join-Path $dst "$Name.png") -Force
    $exp = "$DecorRepo\ArtSource\Exports\Decor"; New-Item -ItemType Directory -Force $exp | Out-Null
    Copy-Item $out (Join-Path $exp "$Name.png") -Force
    "installed $OutFolder/$Name.png  (+ ArtSource/Exports/Decor)"
}
$final.Dispose(); $trim.Dispose(); $mock.Dispose(); if ($bg) { $bg.Dispose() }
