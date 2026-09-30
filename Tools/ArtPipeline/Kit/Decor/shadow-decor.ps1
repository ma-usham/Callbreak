# Builds the soft drop shadow of a finished Decor sprite as its own sprite (shadows are separate
# sprites in the kit): the sprite's silhouette, blurred, in one dark colour. The blur, opacity
# and drop are searched against the mockup (DecorTools.ShadowSearch).
#   shadow-decor.ps1 -Name Deco_AceOverKing -Screen 12 -Rect 1007,390.5,277.5,282.3 [-Install]
# Prints the values for the screen's node: shadowUse, shadowSpread, shadowDrop, shadowOpacity.
param(
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [string] $Screen,
    [Parameter(Mandatory)] [double[]] $Rect,       # where the sprite sits on the mockup (x,y,w,h)
    [string] $OutFolder = 'UI/Decor',
    [string] $Color = '#2A1A12',
    [int] $Margin = 30,
    [switch] $Install
)
. (Join-Path $PSScriptRoot 'decor.ps1')
$fitDir = "$DecorWork\fit"
$sprite = [DecorTools]::Load("$fitDir\$Name.png")
$mock = [DecorTools]::Load((Get-Mockup $Screen))
$col = [System.Drawing.ColorTranslator]::FromHtml($Color)
$p = [DecorTools]::ShadowSearch($mock, $sprite, $Rect[0], $Rect[1], $Rect[2], $Rect[3], $col, $Margin)
"search   blur {0} px, opacity {1:F2}, drop {2} px, shift right {3} px   error {4:F2} -> {5:F2}" -f $p[0], $p[1], $p[2], $p[3], $p[4], $p[5]
$perMock = $sprite.Width / $Rect[2]                      # export px per mockup px
$blur = [int][Math]::Round($p[0] * $perMock)
$pad = $blur * 3
$shadow = [DecorTools]::ShadowSprite($sprite, $blur, $pad, $col)
$out = "$fitDir\${Name}_Shadow.png"; [DecorTools]::Save($shadow, $out)
"sprite   {0}x{1} (pad {2} px = spread {3:F1} mockup px)" -f $shadow.Width, $shadow.Height, $pad, ($pad / $perMock)
"node     ""shadowUse"": ""$OutFolder/${Name}_Shadow.png"", ""shadowSpread"": {0:F1}, ""shadowDrop"": {1}, ""shadowOpacity"": {2:F2}{3}" -f ($pad / $perMock), $p[2], $p[1], $(if ($p[3] -ne 0) { "   (the mockup's shadow also sits $($p[3]) px to the right)" } else { '' })

# check: ground + shadow + sprite against the mockup, at 2x
$rx = [int][Math]::Floor($Rect[0] - $Margin); $ry = [int][Math]::Floor($Rect[1] - $Margin)
$rw = [int][Math]::Ceiling($Rect[2] + 2 * $Margin); $rh = [int][Math]::Ceiling($Rect[3] + 2 * $Margin)
$z = 2
$mc = [DecorTools]::Crop($mock, $rx, $ry, $rw, $rh); $mBig = [DecorTools]::Resize($mc, $rw * $z, $rh * $z)
$g0 = $mc.GetPixel(1, 1)
$canvas = [DecorTools]::Flat($rw * $z, $rh * $z, $g0)
$shOp = [DecorTools]::ScaleAlpha($shadow, $p[1])
$spread = $pad / $perMock
$shW = [int][Math]::Round(($Rect[2] + 2 * $spread) * $z); $shH = [int][Math]::Round(($Rect[3] + 2 * $spread) * $z)
$shBig = [DecorTools]::Resize($shOp, $shW, $shH)
$c1 = [DecorTools]::Over($canvas, $shBig, [int][Math]::Round(($Rect[0] - $spread - $rx + $p[3]) * $z), [int][Math]::Round(($Rect[1] - $spread - $ry + $p[2]) * $z))
$spBig = [DecorTools]::Resize($sprite, [int][Math]::Round($Rect[2] * $z), [int][Math]::Round($Rect[3] * $z))
$c2 = [DecorTools]::Over($c1, $spBig, [int][Math]::Round(($Rect[0] - $rx) * $z), [int][Math]::Round(($Rect[1] - $ry) * $z))
$sheet = New-Object System.Drawing.Bitmap ($rw * $z * 2 + 24), ($rh * $z + 16)
$g = [System.Drawing.Graphics]::FromImage($sheet); $g.Clear([System.Drawing.Color]::FromArgb(255, 90, 90, 90))
$g.DrawImage($mBig, 8, 8, $rw * $z, $rh * $z); $g.DrawImage($c2, 16 + $rw * $z, 8, $rw * $z, $rh * $z); $g.Dispose()
$sheet.Save("$fitDir\${Name}_Shadow_check.png"); $sheet.Dispose()
"check    $fitDir\${Name}_Shadow_check.png (mockup | ground + shadow + sprite, at 2x)"
if ($Install) {
    $dst = Join-Path $DecorArt ($OutFolder -replace '/', '\'); New-Item -ItemType Directory -Force $dst | Out-Null
    Copy-Item $out (Join-Path $dst "${Name}_Shadow.png") -Force
    $exp = "$DecorRepo\ArtSource\Exports\Decor"; New-Item -ItemType Directory -Force $exp | Out-Null
    Copy-Item $out (Join-Path $exp "${Name}_Shadow.png") -Force
    "installed $OutFolder/${Name}_Shadow.png  (+ ArtSource/Exports/Decor)"
}
$sprite.Dispose(); $mock.Dispose(); $shadow.Dispose()
