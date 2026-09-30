# Dot-source to load the Decor designer's helpers:  . Tools\ArtPipeline\Kit\Decor\decor.ps1
# Gives [DecorTools] (DecorTools.cs in this folder) plus a few path shortcuts.
Add-Type -AssemblyName System.Drawing
if (-not ('DecorTools' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'DecorTools.cs') -ReferencedAssemblies System.Drawing
}
$DecorRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$DecorWork = Join-Path $DecorRepo 'ArtSource\Working\Kit\Decor'
$DecorArt = Join-Path $DecorRepo 'Assets\_Project\Art'

# Full path of a mockup by its two-digit number, e.g. Get-Mockup 09
function Get-Mockup([string] $Number) {
    (Get-ChildItem (Join-Path $DecorRepo 'Assets\ArtRefrences\game-screens') -Filter "$Number-*.png" | Select-Object -First 1).FullName
}

# Crops a mockup rectangle (mockup pixels, may be fractional) enlarged by -Scale, for attachments
# and for checks. Returns the output path.
function Save-MockCrop {
    param([Parameter(Mandatory)] [string] $Screen, [Parameter(Mandatory)] [double[]] $Rect, [Parameter(Mandatory)] [string] $Out, [double] $Scale = 1)
    $m = [DecorTools]::Load((Get-Mockup $Screen))
    $w = [int][Math]::Round($Rect[2] * $Scale); $h = [int][Math]::Round($Rect[3] * $Scale)
    $c = [DecorTools]::Resample($m, $Rect[0], $Rect[1], $Rect[2], $Rect[3], $w, $h); $m.Dispose()
    New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
    [DecorTools]::Save($c, $Out); $c.Dispose()
    "{0}  {1}x{2}" -f $Out, $w, $h
}
