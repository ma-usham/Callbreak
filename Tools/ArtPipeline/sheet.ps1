# Lays images out in one row on a flat background so they can be reviewed in a single view.
param(
    [Parameter(Mandatory)] [string[]] $Images,
    [Parameter(Mandatory)] [string] $Out,
    [string] $Background = 'Magenta',
    [int] $Pad = 10,
    [int] $MaxHeight = 0
)
Add-Type -AssemblyName System.Drawing
$imgs = @($Images | ForEach-Object { [System.Drawing.Image]::FromFile($_) })
try {
    $sizes = @($imgs | ForEach-Object {
        $s = 1.0
        if ($MaxHeight -gt 0 -and $_.Height -gt $MaxHeight) { $s = $MaxHeight / $_.Height }
        [pscustomobject]@{ W = [int][Math]::Round($_.Width * $s); H = [int][Math]::Round($_.Height * $s) }
    })
    $w = [int](($sizes | Measure-Object W -Sum).Sum + $Pad * ($imgs.Count + 1))
    $h = [int](($sizes | Measure-Object H -Maximum).Maximum + 2 * $Pad)
    $sheet = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($sheet)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::FromName($Background))
    $x = $Pad
    for ($i = 0; $i -lt $imgs.Count; $i++) {
        $g.DrawImage($imgs[$i], $x, $Pad, $sizes[$i].W, $sizes[$i].H)
        $x += $sizes[$i].W + $Pad
    }
    $g.Dispose()
    $sheet.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $sheet.Dispose()
    "$Out  ${w}x${h}"
} finally { $imgs | ForEach-Object { $_.Dispose() } }
