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
    [Parameter(Mandatory)] [double[]] $Rect,          # guess: the element's visible pixels on the mockup (x,y,w,h)
    [string] $OutFolder = 'UI/Decor',
    [string] $Plate,                                  # UI-free plate aligned to the mockup (for elements on scenery)
    [int[]] $SourceCrop,                              # x,y,w,h inside the ChatGPT image (one cell of a sheet)
    [ValidateSet('auto', 'alpha', 'color')] [string] $Key = 'auto',
    [string] $KeyColor,                               # '#RRGGBB'; default = the image's border colour
    [double] $KeyLo = 10, [double] $KeyHi = 60,
    [switch] $KeyEverywhere,                          # also remove the ground colour inside the object
    [int] $SpeckArea = 40,
    [string] $Recolor,                                # '#RRGGBB': flat single-colour ornament
    [switch] $NoColorFit,
    [switch] $OffsetOnly,
    [switch] $FreeAspect,
    [switch] $MirrorLeft,
    [switch] $FlipH,
    [double] $Opacity = 1,
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
if ($Key -eq 'alpha' -or ($Key -eq 'auto' -and $hasAlpha)) {
    $cut = [DecorTools]::AlphaLevels($raw, 12, 250)
    "cut-out  real alpha (levels 12..250)"
}
else {
    $kc = if ($KeyColor) { [System.Drawing.ColorTranslator]::FromHtml($KeyColor) } else { [DecorTools]::BorderColor($raw) }
    $cut = [DecorTools]::KeyColor($raw, $kc, $KeyLo, $KeyHi, (-not $KeyEverywhere))
    "cut-out  keyed #{0:X2}{1:X2}{2:X2} (lo {3}, hi {4}, {5})" -f $kc.R, $kc.G, $kc.B, $KeyLo, $KeyHi, $(if ($KeyEverywhere) { 'everywhere' } else { 'connected to the border' })
}
$raw.Dispose()
if ($SpeckArea -gt 0) { $d = [DecorTools]::DropSpecks($cut, 24, $SpeckArea); $cut.Dispose(); $cut = $d }
$b = [DecorTools]::AlphaBounds($cut, 8)
$trim = [DecorTools]::Crop($cut, $b[0], $b[1], $b[2], $b[3]); $cut.Dispose()
if ($FlipH) { $f = [DecorTools]::FlipH($trim); $trim.Dispose(); $trim = $f }
if ($MirrorLeft) { $f = [DecorTools]::MirrorLeftHalf($trim); $trim.Dispose(); $trim = $f }
if ($Recolor) { $f = [DecorTools]::Recolor($trim, [System.Drawing.ColorTranslator]::FromHtml($Recolor)); $trim.Dispose(); $trim = $f }
if ($Opacity -lt 1) { $f = [DecorTools]::ScaleAlpha($trim, $Opacity); $trim.Dispose(); $trim = $f }
"trimmed  at {0},{1}  {2}" -f $b[0], $b[1], [DecorTools]::Stats($trim)

# 3. where it sits on the mockup
$mock = [DecorTools]::Load((Get-Mockup $Screen))
$bg = $null
if ($Plate) {
    $bg = [DecorTools]::Load($Plate)
    if ($bg.Width -ne $mock.Width -or $bg.Height -ne $mock.Height) { $r = [DecorTools]::Resize($bg, $mock.Width, $mock.Height); $bg.Dispose(); $bg = $r }
}
if ($NoFit) { $fit = @($Rect[0], $Rect[1], $Rect[2], $Rect[3], 0, 0) }
else { $fit = [DecorTools]::FitOnMockup($mock, $bg, $trim, $Rect[0], $Rect[1], $Rect[2], $Rect[3], [bool]$FreeAspect, $Margin) }
"fitted   x {0:F2} y {1:F2} w {2:F2} h {3:F2}   error {4:F2} -> {5:F2}   (guess {6})" -f $fit[0], $fit[1], $fit[2], $fit[3], $fit[4], $fit[5], ($Rect -join ',')

# 4. colour
if (-not $NoColorFit -and -not $Recolor) {
    $k = [DecorTools]::FitSpriteColor($mock, $trim, $fit[0], $fit[1], $fit[2], $fit[3], [bool]$OffsetOnly)
    "colour   gain/offset r {0:F3} {1:F1}  g {2:F3} {3:F1}  b {4:F3} {5:F1}  on {6} px" -f $k[0], $k[1], $k[2], $k[3], $k[4], $k[5], $k[6]
    if ($k[6] -ge 40) { $f = [DecorTools]::ApplyColor($trim, $k[0..5]); $trim.Dispose(); $trim = $f } else { "colour   too few opaque pixels, left as is" }
}
$errFinal = [DecorTools]::CompositeError($mock, $bg, $trim, $fit[0], $fit[1], $fit[2], $fit[3], $Margin)

# 5. export size: mockup pixels x 2.3, rounded up to even
function Even([double] $v) { $n = [int][Math]::Ceiling($v - 1e-6); if ($n % 2) { $n + 1 } else { $n } }
$W = Even ($fit[2] * $ExportScale); $H = Even ($fit[3] * $ExportScale)
$up = $W / $trim.Width
$final = [DecorTools]::Resize($trim, $W, $H)
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
