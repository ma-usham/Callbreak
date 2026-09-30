# Crops rectangles out of one source image. Each region: name,x,y,w,h[,scale]
param(
    [Parameter(Mandatory)] [string] $Src,
    [Parameter(Mandatory)] [string] $OutDir,
    [Parameter(Mandatory)] [string[]] $Regions
)
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutDir | Out-Null
$img = [System.Drawing.Bitmap]::FromFile($Src)
try {
    foreach ($r in $Regions) {
        $p = $r.Split(',')
        $name = $p[0]; $x = [int]$p[1]; $y = [int]$p[2]; $w = [int]$p[3]; $h = [int]$p[4]
        $scale = 1.0; if ($p.Length -gt 5) { $scale = [double]$p[5] }
        $ow = [int][Math]::Round($w * $scale); $oh = [int][Math]::Round($h * $scale)
        $bmp = New-Object System.Drawing.Bitmap($ow, $oh, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $dst = New-Object System.Drawing.Rectangle(0, 0, $ow, $oh)
        $srcRect = New-Object System.Drawing.Rectangle($x, $y, $w, $h)
        $g.DrawImage($img, $dst, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()
        $out = Join-Path $OutDir "$name.png"
        $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        "{0}  {1}x{2}" -f $out, $ow, $oh
    }
} finally { $img.Dispose() }
