# Reports representative colours in regions of an image: the darkest and brightest few percent
# (the ink of text drawn on a lighter or darker ground) and the median (the ground).
param(
    [Parameter(Mandatory)] [string] $Src,
    [Parameter(Mandatory)] [string[]] $Regions   # name,x,y,w,h
)
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Bitmap]::FromFile($Src)
function Avg($pixels) {
    $r = ($pixels | Measure-Object R -Average).Average
    $g = ($pixels | Measure-Object G -Average).Average
    $b = ($pixels | Measure-Object B -Average).Average
    '#{0:X2}{1:X2}{2:X2}' -f [int]$r, [int]$g, [int]$b
}
try {
    foreach ($reg in $Regions) {
        $p = $reg.Split(','); $name = $p[0]; $x = [int]$p[1]; $y = [int]$p[2]; $w = [int]$p[3]; $h = [int]$p[4]
        $px = New-Object System.Collections.Generic.List[object]
        for ($j = $y; $j -lt $y + $h; $j++) { for ($i = $x; $i -lt $x + $w; $i++) {
            $c = $img.GetPixel($i, $j)
            $px.Add([pscustomobject]@{ R = $c.R; G = $c.G; B = $c.B; L = 0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B })
        } }
        $sorted = $px | Sort-Object L
        $n = $sorted.Count; $k = [Math]::Max(1, [int]($n * 0.03))
        '{0,-22} dark {1}  median {2}  bright {3}  (n={4})' -f $name, (Avg ($sorted | Select-Object -First $k)), (Avg ($sorted | Select-Object -Skip ([int]($n / 2) - 1) -First 3)), (Avg ($sorted | Select-Object -Last $k)), $n
    }
} finally { $img.Dispose() }
