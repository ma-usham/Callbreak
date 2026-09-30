# Dot-source this file to get ArtTools and a few shortcuts:  . "$sp\art.ps1"
Add-Type -AssemblyName System.Drawing
if (-not ('ArtTools' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'ArtTools.cs'), (Join-Path $PSScriptRoot 'SceneFit.cs') -ReferencedAssemblies System.Drawing
}

# Loads a ChatGPT cut-out with its alpha cleaned up and its transparent margins removed.
function Get-Cutout {
    param([Parameter(Mandatory)] [string] $Src, [int] $Lo = 12, [int] $Hi = 250)
    $raw = [ArtTools]::Load($Src)
    $lev = [ArtTools]::AlphaLevels($raw, $Lo, $Hi); $raw.Dispose()
    $b = [ArtTools]::AlphaBounds($lev, 8)
    $cut = [ArtTools]::Crop($lev, $b[0], $b[1], $b[2], $b[3]); $lev.Dispose()
    $cut
}

# Trims transparent margins, resizes to the export size (or to a height, keeping aspect) and saves.
function Export-Trimmed {
    param(
        [Parameter(Mandatory)] [string] $Src,
        [Parameter(Mandatory)] [string] $Out,
        [int] $Width = 0,
        [int] $Height = 0,
        [int] $AlphaThreshold = 8,
        [int] $PadTo = 0,          # square canvas size, e.g. 128 for icons
        [switch] $MirrorLeft
    )
    $bmp = [ArtTools]::Load($Src)
    $b = [ArtTools]::AlphaBounds($bmp, $AlphaThreshold)
    $cut = [ArtTools]::Crop($bmp, $b[0], $b[1], $b[2], $b[3]); $bmp.Dispose()
    if ($MirrorLeft) { $m = [ArtTools]::MirrorLeftHalf($cut); $cut.Dispose(); $cut = $m }
    if ($Width -gt 0 -and $Height -eq 0) { $Height = [int][Math]::Round($cut.Height * $Width / $cut.Width) }
    if ($Height -gt 0 -and $Width -eq 0) { $Width = [int][Math]::Round($cut.Width * $Height / $cut.Height) }
    if ($Width -gt 0) { $r = [ArtTools]::Resize($cut, $Width, $Height); $cut.Dispose(); $cut = $r }
    if ($PadTo -gt 0) { $p = [ArtTools]::Pad($cut, $PadTo, $PadTo); $cut.Dispose(); $cut = $p }
    New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
    [ArtTools]::Save($cut, $Out)
    $stats = [ArtTools]::Stats($cut); $cut.Dispose()
    "{0}  trimmed from {1},{2} {3}x{4}  ->  {5}" -f (Split-Path $Out -Leaf), $b[0], $b[1], $b[2], $b[3], $stats
}

function Get-ArtStats {
    param([Parameter(Mandatory)] [string[]] $Path)
    foreach ($p in $Path) {
        $bmp = [ArtTools]::Load($p)
        $b = [ArtTools]::AlphaBounds($bmp, 8)
        "{0,-34} {1}  content {2},{3} {4}x{5}" -f (Split-Path $p -Leaf), [ArtTools]::Stats($bmp), $b[0], $b[1], $b[2], $b[3]
        $bmp.Dispose()
    }
}

# Saves a copy composited over a flat colour, for looking at transparent art.
function Save-OnColor {
    param([Parameter(Mandatory)] [string] $Src, [Parameter(Mandatory)] [string] $Out, [string] $Color = '#F4E9DE', [int] $MaxWidth = 0)
    $bmp = [ArtTools]::Load($Src)
    if ($MaxWidth -gt 0 -and $bmp.Width -gt $MaxWidth) {
        $r = [ArtTools]::Resize($bmp, $MaxWidth, [int]($bmp.Height * $MaxWidth / $bmp.Width)); $bmp.Dispose(); $bmp = $r
    }
    $c = [ArtTools]::OnColor($bmp, [System.Drawing.ColorTranslator]::FromHtml($Color)); $bmp.Dispose()
    [ArtTools]::Save($c, $Out); $c.Dispose()
    $Out
}
