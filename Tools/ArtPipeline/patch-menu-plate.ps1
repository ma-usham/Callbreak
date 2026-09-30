# Patch technique for BG_Menu_Clean (Designer Handbook section 8): keeps the mockup's own pixels
# wherever there was no UI and lets ChatGPT's clean plate show only through the UI-shaped holes.
param(
    [string] $Generated,    # ChatGPT's clean plate, same size as the mockup
    [string] $OutDir
)
$sp = $PSScriptRoot
. "$sp\art.ps1"
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
if (-not $Generated) { $Generated = "$repo\ArtSource\Working\MainMenu\chatgpt\BG_Menu_Clean_a2.png" }
if (-not $OutDir) { $OutDir = "$repo\ArtSource\Working\MainMenu\fit" }
New-Item -ItemType Directory -Force $OutDir | Out-Null

& "$sp\make-menu-mask.ps1" -Out "$OutDir\menu-mask.png" -Grow 14 -Feather 7 | Out-Null
$original = [ArtTools]::Load("$repo\Assets\ArtRefrences\game-screens\00-main-menu.png")
$clean = [ArtTools]::Load($Generated)
$mask = [ArtTools]::Load("$OutDir\menu-mask.png")
"difference from the mockup outside the UI (0-255 per channel): {0:N2}" -f [ArtTools]::MeanDifference($original, $clean, $mask, 8)
$patched = [ArtTools]::Patch($original, $clean, $mask)
[ArtTools]::Save($patched, "$OutDir\BG_Menu_Clean_patched_1672.png")
$original.Dispose(); $clean.Dispose(); $mask.Dispose(); $patched.Dispose()
"$OutDir\BG_Menu_Clean_patched_1672.png"
