# Measures each main-menu label on the reference (scaled to 1080p) and on a Unity capture, and
# prints how far the capture is off: left edge, width, cap height and baseline, in 1080p pixels.
param(
    [string] $Capture
)
$sp = $PSScriptRoot
. "$sp\art.ps1"
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
if (-not $Capture) { $Capture = "$repo\Temp\Captures\MainMenu_1920x1080.png" }
$mock = [ArtTools]::Load("$repo\Assets\ArtRefrences\game-screens\00-main-menu.png")
$ref = [ArtTools]::Resize($mock, 1920, 1080); $mock.Dispose()
$cap = [ArtTools]::Load($Capture)

# name, region x, y, w, h (1080p pixels), dark ink?, threshold
$labels = @(
    @('PlayOnline',   790, 680, 510, 100, $false, 70),
    @('Friends',      672, 868, 230, 60,  $true,  90),
    @('Offline',      1105, 868, 193, 60, $true,  90),
    @('Tagline',      676, 985, 560, 40,  $true,  35),
    @('Greeting',     232, 60, 170, 44,   $true,  35),
    @('PlayerName',   232, 102, 160, 56,  $true,  90)
)
'{0,-11} {1,-5} {2,8} {3,8} {4,8} {5,8} {6,8}' -f 'label', 'image', 'left', 'width', 'capH', 'capTop', 'baseline'
foreach ($l in $labels) {
    $a = [ArtTools]::InkBox($ref, $l[1], $l[2], $l[3], $l[4], $l[5], $l[6])
    $b = [ArtTools]::InkBox($cap, $l[1], $l[2], $l[3], $l[4], $l[5], $l[6])
    foreach ($pair in @(@('ref', $a), @('ours', $b))) {
        $v = $pair[1]
        if ($null -eq $v) { '{0,-11} {1,-5} no ink found' -f $l[0], $pair[0]; continue }
        '{0,-11} {1,-5} {2,8:N1} {3,8:N1} {4,8:N1} {5,8:N1} {6,8:N1}' -f $l[0], $pair[0], $v[0], ($v[1] - $v[0] + 1), ($v[3] - $v[2] + 1), $v[2], $v[3]
    }
    if ($a -and $b) {
        '{0,-11} {1,-5} {2,8:N1} {3,8:N3} {4,8:N3} {5,8:N1} {6,8:N1}   (left diff, width ratio, capH ratio, capTop diff, baseline diff: ref vs ours)' -f $l[0], 'fix', ($a[0] - $b[0]), (($a[1] - $a[0] + 1) / ($b[1] - $b[0] + 1)), (($a[3] - $a[2] + 1) / ($b[3] - $b[2] + 1)), ($a[2] - $b[2]), ($a[3] - $b[3])
    }
}
$ref.Dispose(); $cap.Dispose()
