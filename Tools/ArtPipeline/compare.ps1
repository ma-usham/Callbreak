# Stacks the same region of two images (reference on top, candidate below), enlarged, for a
# close side-by-side check. Both images are scaled to the reference's size first.
param(
    [Parameter(Mandatory)] [string] $Reference,
    [Parameter(Mandatory)] [string] $Candidate,
    [Parameter(Mandatory)] [string] $Out,
    [Parameter(Mandatory)] [int[]] $Region,     # x, y, w, h in reference pixels
    [double] $Scale = 2
)
Add-Type -AssemblyName System.Drawing
$a = [System.Drawing.Bitmap]::FromFile($Reference); $b = [System.Drawing.Bitmap]::FromFile($Candidate)
$x = $Region[0]; $y = $Region[1]; $w = $Region[2]; $h = $Region[3]
$fx = $b.Width / $a.Width; $fy = $b.Height / $a.Height
$ow = [int]($w * $Scale); $oh = [int]($h * $Scale)
$bmp = New-Object System.Drawing.Bitmap $ow, ($oh * 2 + 6)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::Magenta)
$g.DrawImage($a, (New-Object System.Drawing.Rectangle 0, 0, $ow, $oh), (New-Object System.Drawing.Rectangle $x, $y, $w, $h), [System.Drawing.GraphicsUnit]::Pixel)
$g.DrawImage($b, (New-Object System.Drawing.RectangleF 0, ($oh + 6), $ow, $oh), (New-Object System.Drawing.RectangleF ($x * $fx), ($y * $fy), ($w * $fx), ($h * $fy)), [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose(); $a.Dispose(); $b.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
$Out
