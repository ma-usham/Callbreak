# Prepares what is attached to the Decor requests: one enlarged tight crop per painted piece and
# two contact sheets of the line ornaments (brief: small uniform things go on a sheet).
# Output: ArtSource/Working/Kit/Decor/attach/. Rectangles are mockup pixels.
. (Join-Path $PSScriptRoot 'decor.ps1')
$a = "$DecorWork\attach"; New-Item -ItemType Directory -Force $a | Out-Null

# --- one crop per painted piece: screen, x, y, w, h, scale
$crops = [ordered]@{
    'crop_09_medal'      = @('09', 772, 186, 128, 112, 6)
    'crop_10_trophy'     = @('10', 650, 0, 400, 146, 4)
    'crop_10_sparkle'    = @('10', 662, 60, 54, 54, 10)
    'crop_10_crown'      = @('10', 652, 396, 70, 54, 10)
    'crop_05_arc'        = @('05', 728, 208, 214, 190, 5)
    'crop_12_cards'      = @('12', 996, 368, 318, 336, 3.5)
    'crop_08_logo'       = @('08', 170, 22, 360, 104, 4)
    'crop_16_seal'       = @('16', 772, 204, 128, 112, 6)
    'crop_01_mountains'  = @('01', 368, 600, 460, 100, 3)
    'crop_14_avatar'     = @('14', 470, 250, 236, 236, 3)
}
foreach ($k in $crops.Keys) {
    $c = $crops[$k]
    Save-MockCrop -Screen $c[0] -Rect $c[1], $c[2], $c[3], $c[4] -Out "$a\$k.png" -Scale $c[5]
}

# --- contact sheets: rows of crops on the panel ivory, nothing else (no labels: ChatGPT copies them)
function New-Sheet([int] $w, [int] $h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#F7EDE3'))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    @{ Bmp = $bmp; G = $g }
}
function Add-Crop($sheet, [string] $screen, [double[]] $rect, [double] $scale, [int] $x, [int] $y, [switch] $Rotate) {
    $m = [DecorTools]::Load((Get-Mockup $screen))
    $w = [int][Math]::Round($rect[2] * $scale); $h = [int][Math]::Round($rect[3] * $scale)
    $c = [DecorTools]::Resample($m, $rect[0], $rect[1], $rect[2], $rect[3], $w, $h); $m.Dispose()
    if ($Rotate) { $c.RotateFlip([System.Drawing.RotateFlipType]::Rotate90FlipNone) }
    $sheet.G.DrawImage($c, $x, $y, $c.Width, $c.Height)
    $size = "{0}x{1}" -f $c.Width, $c.Height; $c.Dispose()
    "  {0} {1} at {2},{3} size {4}" -f $screen, ($rect -join ','), $x, $y, $size
}

# Sheet A: the long line-and-diamond dividers, one per row, all 1400 px wide.
$s = New-Sheet 1536 1024
"sheet_dividers.png"
Add-Crop $s '01' @(662, 196, 351, 30) (1400 / 351) 68 70                 # title divider (fades out at both ends)
Add-Crop $s '04' @(452, 632, 768, 30) (1400 / 768) 68 300                # lobby divider (pale, even line)
Add-Crop $s '14' @(444, 579, 784, 30) (1400 / 784) 68 470                # profile divider (gaps beside the diamond)
Add-Crop $s '14' @(738, 253, 30, 302) (1400 / 302) 68 640 -Rotate        # profile vertical divider, turned to lie flat
$s.G.Dispose(); $s.Bmp.Save("$a\sheet_dividers.png", [System.Drawing.Imaging.ImageFormat]::Png); $s.Bmp.Dispose()

# Sheet B: the short ornaments.
$s = New-Sheet 1536 1024
"sheet_ornaments.png"
Add-Crop $s '09' @(568, 226, 212, 36) 5 90 80                            # medal line (left one: line, diamond at the medal end)
Add-Crop $s '09' @(596, 840, 126, 34) 5 90 330                           # footer arrow (left one)
Add-Crop $s '08' @(638, 22, 96, 33) 5 90 560                             # round ornament (left one, on sky)
Add-Crop $s '12' @(589, 733, 56, 56) 5 1130 80                           # pill ornament (woven gold diamond)
Add-Crop $s '10' @(652, 396, 70, 54) 5 1100 420                          # crown
$s.G.Dispose(); $s.Bmp.Save("$a\sheet_ornaments.png", [System.Drawing.Imaging.ImageFormat]::Png); $s.Bmp.Dispose()
"done"
