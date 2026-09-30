# Turns the ChatGPT extractions for the main menu into game-ready exports: cleans the alpha,
# trims, applies the colour correction measured against the mockup, resizes to @2x of the size
# each part has on the 1920 x 1080 canvas, and builds the soft shadows. Writes ArtSource/Exports
# and copies the same files into Assets/_Project/Art.
param(
    [Parameter(Mandatory)] [string] $FitDir,      # holds layout.csv and colors.csv
    [Parameter(Mandatory)] [string] $Plate,       # patched clean plate at mockup size
    [hashtable] $Pick = @{}
)
$sp = $PSScriptRoot
. "$sp\art.ps1"
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
$src = "$repo\ArtSource\Working\MainMenu\chatgpt"
$exports = "$repo\ArtSource\Exports"
$assets = "$repo\Assets\_Project\Art"
$at2x = 2 * 1920 / 1672        # mockup pixels -> @2x export pixels
$shadowColor = [System.Drawing.ColorTranslator]::FromHtml('#2A1A12')

$parts = @{}; $shadows = @{}
foreach ($line in Get-Content "$FitDir\layout.csv") {
    $f = $line.Split(',')
    if ($f[0] -eq 'part') { $parts[$f[1]] = @{ W = [double]$f[4]; H = [double]$f[5] } }
    else { $shadows[$f[1]] = @{ Blur = [double]$f[2]; Dy = [double]$f[3]; Opacity = [double]$f[4] } }
}
$colors = @{}
foreach ($line in Get-Content "$FitDir\colors.csv") { $f = $line.Split(','); $colors[$f[0]] = [double[]]($f[1..6] + 0) }

function Even([double] $v) { $n = [int][Math]::Round($v); if ($n % 2) { $n + 1 } else { $n } }
function SrcOf($name) { $s = if ($Pick.ContainsKey($name)) { $Pick[$name] } else { '_a1' }; "$src\${name}${s}.png" }
$rows = New-Object System.Collections.Generic.List[string]

# Saves one export in both places and records it for handoff.csv.
function Save-Export($bmp, [string] $exportPath, [string] $assetPath, [string] $type, [string] $border, [string] $prompt, [string] $notes) {
    foreach ($p in "$exports\$exportPath", "$assets\$assetPath") { New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null; [ArtTools]::Save($bmp, $p) }
    $rows.Add(('{0},{1},{2}x{3},{4},00,{5},review,{6}' -f $exportPath.Replace('\', '/'), $type, $bmp.Width, $bmp.Height, $border, $prompt, $notes))
    "{0,-44} {1}x{2}" -f $exportPath, $bmp.Width, $bmp.Height
}

# A cut-out, colour-corrected and resized to its @2x size. Returns the bitmap.
function Make-Sprite([string] $asset, [double] $mockW, [double] $mockH) {
    $c = Get-Cutout (SrcOf $asset)
    if ($colors.ContainsKey($asset)) { $k = [ArtTools]::ApplyColor($c, $colors[$asset]); $c.Dispose(); $c = $k }
    $r = [ArtTools]::Resize($c, (Even ($mockW * $at2x)), (Even ($mockH * $at2x))); $c.Dispose(); $r
}
function Make-Shadow($sprite, [double] $blurMock) { [ArtTools]::Shadow($sprite, [int][Math]::Round($blurMock * $at2x), $shadowColor, 1.0) }

# Background: the patched plate, scaled to the canvas.
$patched = [ArtTools]::Load($Plate); $bg = [ArtTools]::Resize($patched, 1920, 1080); $patched.Dispose()
Save-Export $bg 'Backgrounds\BG_Menu_Clean.png' 'Backgrounds\BG_Menu_Clean.png' 'bg' '' 'BG-1 a2' 'patched over the original outside the UI; 16:9 only until BG-2 extends it to 2560x1440'
$bg.Dispose()

$logo = Make-Sprite 'Logo_Callbreak' $parts.Logo.W $parts.Logo.H
Save-Export $logo 'Decor\Logo_Callbreak.png' 'UI\Ornaments\Logo_Callbreak.png' 'decor' '' 'UI-6' 'colour matched to the mockup'; $logo.Dispose()

$fan = Make-Sprite 'MenuFan' $parts.Fan.W $parts.Fan.H
Save-Export $fan 'Decor\Deco_MenuFan.png' 'UI\Ornaments\Deco_MenuFan.png' 'decor' '' 'fan as drawn' 'whole fan as one sprite: the mockup aces match no deck and each has its own layout'
$s = Make-Shadow $fan $shadows.Fan_Shadow.Blur
Save-Export $s 'UI\UI_Shadow_MenuFan.png' 'UI\Ornaments\UI_Shadow_MenuFan.png' 'ui' '' 'rebuilt' ('blurred silhouette; offset y {0} mockup px, opacity {1}' -f $shadows.Fan_Shadow.Dy, $shadows.Fan_Shadow.Opacity)
$fan.Dispose(); $s.Dispose()

$primary = Make-Sprite 'UI_ButtonPrimary_Normal' $parts.Primary.W $parts.Primary.H
$cap = [int]($primary.Height * 0.62)
Save-Export $primary 'UI\UI_ButtonPrimary_Normal.png' 'UI\Buttons\UI_ButtonPrimary_Normal.png' 'ui' "$cap;0;$cap;0" 'UI-3' 'colour matched to the mockup'
$s = Make-Shadow $primary $shadows.Primary_Shadow.Blur; $pad = ($s.Width - $primary.Width) / 2
Save-Export $s 'UI\UI_Shadow_Primary.png' 'UI\Buttons\UI_Shadow_Primary.png' 'ui' "$($cap + $pad);0;$($cap + $pad);0" 'rebuilt' ('blurred silhouette; offset y {0} mockup px, opacity {1}' -f $shadows.Primary_Shadow.Dy, $shadows.Primary_Shadow.Opacity)
$primary.Dispose(); $s.Dispose()

$secondary = Make-Sprite 'UI_ButtonSecondary_Normal' $parts.Friends.W $parts.Friends.H
$cap = [int]($secondary.Height * 0.55)
Save-Export $secondary 'UI\UI_ButtonSecondary_Normal.png' 'UI\Buttons\UI_ButtonSecondary_Normal.png' 'ui' "$cap;0;$cap;0" 'UI-4' 'colour matched to the mockup'
$s = Make-Shadow $secondary $shadows.Friends_Shadow.Blur; $pad = ($s.Width - $secondary.Width) / 2
Save-Export $s 'UI\UI_Shadow_Secondary.png' 'UI\Buttons\UI_Shadow_Secondary.png' 'ui' "$($cap + $pad);0;$($cap + $pad);0" 'rebuilt' ('blurred silhouette; offset y {0} mockup px, opacity {1}' -f $shadows.Friends_Shadow.Dy, $shadows.Friends_Shadow.Opacity)
$secondary.Dispose(); $s.Dispose()

$circle = Make-Sprite 'UI_ButtonCircle_Normal' 92 92
Save-Export $circle 'UI\UI_ButtonCircle_Normal.png' 'UI\Buttons\UI_ButtonCircle_Normal.png' 'ui' '' 'UI-5' 'made exactly round'
$s = Make-Shadow $circle $shadows.Settings_Shadow.Blur
Save-Export $s 'UI\UI_Shadow_Circle.png' 'UI\Buttons\UI_Shadow_Circle.png' 'ui' '' 'rebuilt' ('blurred silhouette; offset y {0} mockup px, opacity {1}; ring: y {2}, {3}' -f $shadows.Settings_Shadow.Dy, $shadows.Settings_Shadow.Opacity, $shadows.Ring_Shadow.Dy, $shadows.Ring_Shadow.Opacity)
$circle.Dispose(); $s.Dispose()

$ring = [ArtTools]::Disc((Even (104 * $at2x)), [System.Drawing.Color]::FromArgb(255, 252, 251, 249))
Save-Export $ring 'UI\UI_AvatarRing.png' 'UI\Avatars\UI_AvatarRing.png' 'ui' '' 'rebuilt' 'white disc behind the avatar; the mockup ring is plain white, not the ivory button'; $ring.Dispose()

$avatar = Make-Sprite 'Avatar_Default' $parts.Avatar.W $parts.Avatar.W
Save-Export $avatar 'Decor\Avatar_Default.png' 'UI\Avatars\Avatar_Default.png' 'avatar' '' 'IC-2' ''; $avatar.Dispose()

$spade = Make-Sprite 'Icon_Spade_Gold' $parts.Spade.W $parts.Spade.H
Save-Export $spade 'Icons\Icon_Spade_Gold.png' 'UI\Icons\Icon_Spade_Gold.png' 'icon' '' 'UI-7' ''; $spade.Dispose()

$icons = [ordered]@{ Icon_Settings = 'SettingsIcon'; Icon_Shop = 'ShopIcon'; Icon_Friends = 'FriendsIcon'; Icon_Offline = 'OfflineIcon'; Icon_Chevron = 'FriendsChevron' }
foreach ($asset in $icons.Keys) {
    $p = $parts[$icons[$asset]]
    $i = Make-Sprite $asset $p.W $p.H
    Save-Export $i "Icons\$asset.png" "UI\Icons\$asset.png" 'icon' '' 'extracted as image' 'ink moved to the mockup near-black; SVG (IC-1) drifted from the shape'; $i.Dispose()
}

$csv = "$repo\ArtSource\handoff.csv"
@('file,type,size_px,nine_slice_ltrb,used_on,prompt,status,notes') + $rows | Set-Content $csv -Encoding utf8
"handoff.csv: $($rows.Count) rows"
