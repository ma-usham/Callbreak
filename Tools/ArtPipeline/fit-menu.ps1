# Re-composes the main menu from its extracted parts over the clean plate, fits the soft drop
# shadows and the parts that have no shadow of their own, and reports how close the result is
# to the mockup. Writes composite.png, composite-diff-x3.png and layout.csv.
param(
    [Parameter(Mandatory)] [string] $Plate,
    [Parameter(Mandatory)] [string] $OutDir,
    [hashtable] $Pick = @{}          # asset name -> attempt suffix, e.g. @{ UI_ButtonCircle_Normal = '_a2' }
)
$sp = $PSScriptRoot
. "$sp\art.ps1"
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
$ref = [ArtTools]::Load("$repo\Assets\ArtRefrences\game-screens\00-main-menu.png")
$src = "$repo\ArtSource\Working\MainMenu\chatgpt"
New-Item -ItemType Directory -Force $OutDir | Out-Null
$shadowColor = [System.Drawing.ColorTranslator]::FromHtml('#2A1A12')

$scene = New-Object Scene
$scene.Plate = [ArtTools]::Load($Plate)

function SrcOf($name) { $s = if ($Pick.ContainsKey($name)) { $Pick[$name] } else { '_a1' }; "$src\${name}${s}.png" }
function Fit2x($bmp, [double] $mockWidth) {
    $w = [int][Math]::Max(16, [Math]::Round($mockWidth * 2)); $h = [int][Math]::Round($bmp.Height * $w / $bmp.Width)
    $r = [ArtTools]::Resize($bmp, $w, $h); $bmp.Dispose(); $r
}
function Cut($name, [double] $mockWidth) { Fit2x (Get-Cutout (SrcOf $name)) $mockWidth }
function RoiOf($r) { New-Object System.Drawing.RectangleF $r[0], $r[1], $r[2], $r[3] }

$circle = Cut 'UI_ButtonCircle_Normal' 92
$secondary = Cut 'UI_ButtonSecondary_Normal' 335
$primary = Cut 'UI_ButtonPrimary_Normal' 684
$chevron = Cut 'Icon_Chevron' 15
$ring = [ArtTools]::Disc(208, [System.Drawing.Color]::FromArgb(255, 252, 251, 249))
$fan = Cut 'MenuFan' 580

# Shadow layers share their owner's image, blurred. They are created empty here and filled in by
# Set-Shadow, so their settings can be searched.
$shadows = @{}
function Add-ShadowLayer($name, $owner, $cut) {
    $l = $scene.Add($name, $cut, 0, 0, 10, 0); $l.Visible = $false
    $shadows[$name] = @{ Owner = $owner; Cut = $cut }
}
function Set-Shadow($name, [double] $blur, [double] $dy, [double] $opacity) {
    $s = $shadows[$name]; $o = $scene.Get($s.Owner); $l = $scene.Get($name)
    $img = [ArtTools]::Shadow($s.Cut, [int][Math]::Round($blur * 2), $shadowColor, $opacity)
    $l.Image = $img; $l.Aspect = $img.Width / $img.Height
    $l.Width = $o.Width * $img.Width / $s.Cut.Width
    $l.CenterX = $o.CenterX; $l.CenterY = $o.CenterY + $dy; $l.Rotation = $o.Rotation; $l.Visible = $true
    $s.Blur = $blur; $s.Dy = $dy; $s.Opacity = $opacity
}

# Back to front. A shadow is added just before its owner.
[void]$scene.Add('Logo', (Cut 'Logo_Callbreak' 695), 834.75, 180.25, 697.5, 0)
Add-ShadowLayer 'Fan_Shadow' 'Fan' $fan
[void]$scene.Add('Fan', $fan, 847, 480.5, 580, 0)
Add-ShadowLayer 'Primary_Shadow' 'Primary' $primary
[void]$scene.Add('Primary', $primary, 835.75, 636.25, 684.5, 0)
[void]$scene.Add('Spade', (Cut 'Icon_Spade_Gold' 70), 641.25, 634.75, 72, 0)
Add-ShadowLayer 'Friends_Shadow' 'Friends' $secondary
[void]$scene.Add('Friends', $secondary, 656.5, 783.25, 335, 0)
[void]$scene.Add('FriendsIcon', (Cut 'Icon_Friends' 59), 544, 781.25, 59, 0)
[void]$scene.Add('FriendsChevron', $chevron, 796, 783.5, 15, 0)
Add-ShadowLayer 'Offline_Shadow' 'Offline' $secondary
[void]$scene.Add('Offline', $secondary, 1015.5, 783.25, 331, 0)
[void]$scene.Add('OfflineIcon', (Cut 'Icon_Offline' 48), 924.5, 782, 50, 0)
[void]$scene.Add('OfflineChevron', $chevron, 1140.75, 783.75, 15.5, 0)
Add-ShadowLayer 'Ring_Shadow' 'Ring' $ring
[void]$scene.Add('Ring', $ring, 127.5, 98, 104, 0)
[void]$scene.Add('Avatar', (Cut 'Avatar_Default' 96), 127.25, 98, 96.5, 0)
Add-ShadowLayer 'Settings_Shadow' 'SettingsCircle' $circle
[void]$scene.Add('SettingsCircle', $circle, 1402, 97, 92, 0)
[void]$scene.Add('SettingsIcon', (Cut 'Icon_Settings' 47), 1401.1, 96.85, 46.75, 0)
Add-ShadowLayer 'Shop_Shadow' 'ShopCircle' $circle
[void]$scene.Add('ShopCircle', $circle, 1547.5, 97, 92, 0)
[void]$scene.Add('ShopIcon', (Cut 'Icon_Shop' 47), 1548.75, 96, 46.5, 0)

$whole = RoiOf @(0, 0, 1672, 941)
"no shadows, measured rects: whole-screen error {0:N3}" -f $scene.Error($ref, $whole)

# Grid-searches one shadow family: every layer in $names gets the same settings.
function Search-Shadow([string[]] $names, $rois, [double[]] $blurs, [double[]] $dys, [double[]] $opacities) {
    $best = $null
    foreach ($b in $blurs) { foreach ($o in $opacities) { foreach ($d in $dys) {
        foreach ($n in $names) { Set-Shadow $n $b $d $o }
        $e = 0; foreach ($r in $rois) { $e += $scene.Error($ref, (RoiOf $r)) }
        if ($null -eq $best -or $e -lt $best.E) { $best = @{ E = $e; B = $b; D = $d; O = $o } }
    } } }
    foreach ($n in $names) { Set-Shadow $n $best.B $best.D $best.O }
    "{0,-28} blur {1,4:N1}  dy {2,4:N1}  opacity {3:N2}   roi error {4:N3}" -f ($names -join '+'), $best.B, $best.D, $best.O, $best.E
}

# The fan's place is only roughly known, so it is fitted before its shadow is searched.
[void]$scene.Fit($ref, 'Fan', (RoiOf @(540, 318, 620, 246)), 'xyw', 8)
"fan placed: cx {0:N2}  cy {1:N2}  w {2:N2}" -f $scene.Get('Fan').CenterX, $scene.Get('Fan').CenterY, $scene.Get('Fan').Width

Search-Shadow @('Primary_Shadow') @(, @(470, 552, 735, 185)) @(6, 9, 12, 16, 20) @(4, 7, 10, 14, 18) @(0.2, 0.3, 0.4, 0.5, 0.6, 0.75)
Search-Shadow @('Friends_Shadow', 'Offline_Shadow') @(@(475, 727, 365, 125), @(836, 727, 361, 125)) @(3, 5, 7, 10) @(2, 4, 6, 8) @(0.1, 0.15, 0.2, 0.3, 0.4)
Search-Shadow @('Settings_Shadow', 'Shop_Shadow') @(@(1340, 35, 124, 135), @(1485, 35, 124, 135)) @(3, 5, 7, 10) @(2, 4, 6, 8) @(0.1, 0.15, 0.2, 0.3, 0.4)
Search-Shadow @('Ring_Shadow') @(, @(60, 30, 136, 140)) @(3, 5, 7, 10) @(2, 4, 6, 8) @(0.1, 0.15, 0.2, 0.3, 0.4)
Search-Shadow @('Fan_Shadow') @(, @(540, 318, 620, 246)) @(3, 6, 9, 13) @(0, 3, 6) @(0.05, 0.1, 0.2, 0.3)
"with shadows: whole-screen error {0:N3}" -f $scene.Error($ref, $whole)

# Parts with crisp edges and no large shadow of their own are fitted; cards also by tilt.
$fits = [ordered]@{
    Logo = @(470, 25, 730, 310, 'xyw'); Spade = @(596, 586, 90, 98, 'xyw'); Avatar = @(70, 41, 116, 116, 'xyw')
    SettingsIcon = @(1372, 66, 60, 62, 'xyw'); ShopIcon = @(1518, 68, 60, 58, 'xyw')
    FriendsIcon = @(506, 752, 76, 58, 'xyw'); OfflineIcon = @(892, 752, 64, 60, 'xyw')
    FriendsChevron = @(784, 768, 26, 33, 'xyw'); OfflineChevron = @(1128, 768, 27, 33, 'xyw')
    Fan = @(540, 318, 620, 246, 'xyw')
}
foreach ($pass in 1, 2) {
    foreach ($name in $fits.Keys) {
        $f = $fits[$name]
        [void]$scene.Fit($ref, $name, (RoiOf $f[0..3]), $f[4], 2)
        $sh = "${name}_Shadow"; if ($shadows.ContainsKey($sh)) { Set-Shadow $sh $shadows[$sh].Blur $shadows[$sh].Dy $shadows[$sh].Opacity }
    }
}
"after fitting parts: whole-screen error {0:N3}" -f $scene.Error($ref, $whole)

# Colour drift: measure each extraction against the reference in place and correct it.
$assetOf = [ordered]@{
    Logo = 'Logo_Callbreak'; Fan = 'MenuFan'; Primary = 'UI_ButtonPrimary_Normal'; Spade = 'Icon_Spade_Gold'
    Friends = 'UI_ButtonSecondary_Normal'; SettingsCircle = 'UI_ButtonCircle_Normal'; Avatar = 'Avatar_Default'
    SettingsIcon = 'Icon_Settings'; ShopIcon = 'Icon_Shop'; FriendsIcon = 'Icon_Friends'; OfflineIcon = 'Icon_Offline'
    FriendsChevron = 'Icon_Chevron'
}
# Silhouette icons are too uniform to regress; their ink is moved to the reference's ink instead
# (asked for as #231C19, measured on the mockup as near-black).
$inkFrom = 0x23, 0x1C, 0x19; $inkTo = 0x0C, 0x0C, 0x0D
$inkK = foreach ($c in 0..2) { $gain = (255.0 - $inkTo[$c]) / (255.0 - $inkFrom[$c]); $gain; ($inkTo[$c] - $inkFrom[$c] * $gain) }
$colorRows = foreach ($name in $assetOf.Keys) {
    $l = $scene.Get($name)
    $roi = RoiOf @(($l.CenterX - $l.Width / 2 - 2), ($l.CenterY - $l.Height / 2 - 2), ($l.Width + 4), ($l.Height + 4))
    $k = if ($assetOf[$name] -like 'Icon_*' -and $assetOf[$name] -ne 'Icon_Spade_Gold') { [double[]]($inkK + 0) } else { $scene.ColorFit($ref, $name, $roi) }
    $old = $l.Image; $new = [ArtTools]::ApplyColor($old, $k)
    foreach ($other in $scene.Layers) { if ([object]::ReferenceEquals($other.Image, $old)) { $other.Image = $new } }
    "{0,-26} R x{1:N3} {2,6:N1}   G x{3:N3} {4,6:N1}   B x{5:N3} {6,6:N1}   ({7:N0} px)" -f $assetOf[$name], $k[0], $k[1], $k[2], $k[3], $k[4], $k[5], $k[6] | Write-Host
    '{0},{1:F4},{2:F2},{3:F4},{4:F2},{5:F4},{6:F2}' -f $assetOf[$name], $k[0], $k[1], $k[2], $k[3], $k[4], $k[5]
}
$colorRows | Set-Content "$OutDir\colors.csv"
"after colour correction: whole-screen error {0:N3}" -f $scene.Error($ref, $whole)
""
foreach ($l in $scene.Layers) {
    if ($l.Name -like '*_Shadow') { continue }
    "{0,-15} cx {1,8:N2}  cy {2,7:N2}  w {3,7:N2}  h {4,7:N2}  rot {5,6:N2}" -f $l.Name, $l.CenterX, $l.CenterY, $l.Width, $l.Height, $l.Rotation
}

$render = $scene.Render($whole, 1)
$flat = $render.Clone((New-Object System.Drawing.Rectangle 0, 0, 1672, 941), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
[ArtTools]::Save($flat, "$OutDir\composite.png")
$diff = [ArtTools]::Difference($ref, $flat, 3); [ArtTools]::Save($diff, "$OutDir\composite-diff-x3.png")
$rows = foreach ($l in $scene.Layers) {
    if ($l.Name -like '*_Shadow') { $s = $shadows[$l.Name]; 'shadow,{0},{1:F1},{2:F1},{3:F2}' -f $l.Name, $s.Blur, $s.Dy, $s.Opacity }
    else { 'part,{0},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2}' -f $l.Name, $l.CenterX, $l.CenterY, $l.Width, $l.Height, $l.Rotation }
}
$rows | Set-Content "$OutDir\layout.csv"
$render.Dispose(); $flat.Dispose(); $diff.Dispose(); $ref.Dispose()
