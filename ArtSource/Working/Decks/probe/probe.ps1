# Exploratory probes (not part of the export): averaged edge profiles of a card.
param([string] $Sheet, [int] $X0, [int] $X1, [int] $Y0, [int] $Y1, [string] $Dir = 'h')
Add-Type -AssemblyName System.Drawing
$b = [System.Drawing.Bitmap]::FromFile($Sheet)
try {
    if ($Dir -eq 'h') {
        # average over rows Y0..Y1, list x from X0..X1
        for ($x = $X0; $x -le $X1; $x++) {
            $r = 0; $g = 0; $bl = 0; $n = 0
            for ($y = $Y0; $y -le $Y1; $y++) { $c = $b.GetPixel($x, $y); $r += $c.R; $g += $c.G; $bl += $c.B; $n++ }
            "x {0,4}  {1,6:F1} {2,6:F1} {3,6:F1}" -f $x, ($r / $n), ($g / $n), ($bl / $n)
        }
    } else {
        for ($y = $Y0; $y -le $Y1; $y++) {
            $r = 0; $g = 0; $bl = 0; $n = 0
            for ($x = $X0; $x -le $X1; $x++) { $c = $b.GetPixel($x, $y); $r += $c.R; $g += $c.G; $bl += $c.B; $n++ }
            "y {0,4}  {1,6:F1} {2,6:F1} {3,6:F1}" -f $y, ($r / $n), ($g / $n), ($bl / $n)
        }
    }
} finally { $b.Dispose() }
