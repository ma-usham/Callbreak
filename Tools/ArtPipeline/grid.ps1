# Crops a region, enlarges it and draws a labelled pixel grid in SOURCE coordinates,
# so element bounds can be read off the reference to within a pixel or two.
param(
    [Parameter(Mandatory)] [string] $Src,
    [Parameter(Mandatory)] [string] $Out,
    [Parameter(Mandatory)] [int] $X,
    [Parameter(Mandatory)] [int] $Y,
    [Parameter(Mandatory)] [int] $W,
    [Parameter(Mandatory)] [int] $H,
    [double] $Scale = 4,
    [int] $Minor = 10,
    [int] $Major = 50
)
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Bitmap]::FromFile($Src)
$ow = [int]($W * $Scale); $oh = [int]($H * $Scale)
$bmp = New-Object System.Drawing.Bitmap $ow, $oh
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($img, (New-Object System.Drawing.Rectangle 0, 0, $ow, $oh), (New-Object System.Drawing.Rectangle $X, $Y, $W, $H), [System.Drawing.GraphicsUnit]::Pixel)
$minorPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, 0, 160, 255)), 1
$majorPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(200, 0, 90, 255)), 1
$font = New-Object System.Drawing.Font 'Consolas', 9
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 60, 200))
$back = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(190, 255, 255, 255))
for ($sx = [Math]::Ceiling($X / $Minor) * $Minor; $sx -lt $X + $W; $sx += $Minor) {
    $px = [int](($sx - $X) * $Scale)
    if ($sx % $Major -eq 0) {
        $g.DrawLine($majorPen, $px, 0, $px, $oh)
        $g.FillRectangle($back, $px + 1, 0, 30, 13); $g.DrawString("$sx", $font, $brush, $px + 1, 0)
    } else { $g.DrawLine($minorPen, $px, 0, $px, $oh) }
}
for ($sy = [Math]::Ceiling($Y / $Minor) * $Minor; $sy -lt $Y + $H; $sy += $Minor) {
    $py = [int](($sy - $Y) * $Scale)
    if ($sy % $Major -eq 0) {
        $g.DrawLine($majorPen, 0, $py, $ow, $py)
        $g.FillRectangle($back, 0, $py + 1, 30, 13); $g.DrawString("$sy", $font, $brush, 0, $py + 1)
    } else { $g.DrawLine($minorPen, 0, $py, $ow, $py) }
}
$g.Dispose(); $img.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
"$Out  ${ow}x${oh}"
