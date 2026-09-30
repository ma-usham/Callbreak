# Cuts the three approved card decks into game-ready sprites: 52 faces per deck from the suit
# sheets in Assets/ArtRefrences/card-designs/full-decks, and one back per deck from the Card
# Collection mockup (game-screens/15-card-styles.png). Re-runnable end to end.
#
#   & Tools\ArtPipeline\export-decks.ps1             # measure, cut, check, review sheets
#   & Tools\ArtPipeline\export-decks.ps1 -Install    # the same, then copy into Assets/ if QA passes
#
# Method and numbers: ArtSource/Prompts/Decks_Batch.md. Do not pipe into Select-Object -First.
param(
    [int] $Width = 228,          # output size, px (even); see Decks_Batch.md for why
    [int] $Height = 358,
    [double] $Radius = 14,       # output corner radius, px
    [string[]] $Decks = @('classic-crimson', 'himalayan-ivory', 'kathmandu-gold'),
    [switch] $Install,           # copy the exported PNGs into Assets/_Project/Art/Cards (QA must pass)
    [switch] $NoReview           # skip the review sheets
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Add-Type -AssemblyName System.Drawing
if (-not ('DeckTools' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'DeckTools.cs') -ReferencedAssemblies System.Drawing
}
$inv = [System.Globalization.CultureInfo]::InvariantCulture
function F([double] $v, [string] $fmt = '0.000') { $v.ToString($fmt, $inv) }

$deckName = @{ 'classic-crimson' = 'ClassicCrimson'; 'himalayan-ivory' = 'HimalayanIvory'; 'kathmandu-gold' = 'KathmanduGold' }
$suitName = [ordered]@{ spades = 'Spades'; hearts = 'Hearts'; diamonds = 'Diamonds'; clubs = 'Clubs' }
$ranks = @('A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K')

$fullDecks = Join-Path $repo 'Assets\ArtRefrences\card-designs\full-decks'
$mockup = Join-Path $repo 'Assets\ArtRefrences\game-screens\15-card-styles.png'
$exportRoot = Join-Path $repo 'ArtSource\Exports\Cards'
$work = Join-Path $repo 'ArtSource\Working\Decks'
$reviewDir = Join-Path $work 'review'
$verifyDir = Join-Path $repo 'Docs\Verification\Decks'
$installRoot = Join-Path $repo 'Assets\_Project\Art\Cards'
$handoff = Join-Path $repo 'ArtSource\handoff.csv'
New-Item -ItemType Directory -Force $work, $reviewDir | Out-Null

$manifest = Get-Content (Join-Path $fullDecks 'manifest.json') -Raw | ConvertFrom-Json

function Median([double[]] $v) {
    $a = @($v | Where-Object { -not [double]::IsNaN($_) } | Sort-Object)
    if ($a.Count -eq 0) { return [double]::NaN }
    if ($a.Count % 2) { return $a[($a.Count - 1) / 2] }
    return ($a[$a.Count / 2 - 1] + $a[$a.Count / 2]) / 2
}

# Builds the cut from a measurement: outer edges, first fully covered pixel on each side, and the
# outline's corner radii: the fitted radius when the fit is good (rms <= 1 px), held within 2 px of
# the deck's median, else the median; plus 0.25 px. A radius a little too large only widens the
# band that gets the rim colour; one too small would let shadow pixels count as card.
function New-Cut($name, $cm, [double] $rMed) {
    $s = New-Object CutSpec
    $s.Name = $name
    $s.L = $cm.L; $s.T = $cm.T; $s.R = $cm.R; $s.B = $cm.B
    $s.CL = $cm.Sides[0].FirstCard; $s.CT = $cm.Sides[1].FirstCard; $s.CR = $cm.Sides[2].FirstCard; $s.CB = $cm.Sides[3].FirstCard
    for ($c = 0; $c -lt 4; $c++) {
        $r = $cm.CornerR[$c]
        if ([double]::IsNaN($r) -or -not ($cm.CornerRms[$c] -le 1.0)) { $r = $rMed }
        $s.Rt[$c] = [Math]::Min([Math]::Max($r, $rMed - 2.0), $rMed + 2.0) + 0.25
    }
    $s
}

# ------------------------------------------------------------------------------ 1. measure faces
Write-Host "Measuring faces..."
$faces = New-Object System.Collections.Generic.List[object]
$sheets = @{}
foreach ($d in $Decks) {
    foreach ($s in $suitName.Keys) {
        $path = Join-Path $fullDecks "$d\$s.png"
        $im = [Img]::Load($path)
        $sheets["$d/$s"] = $im
        $bg = [DeckTools]::SheetBg($im, 885, 945)
        $cards = @($manifest.cards | Where-Object { $_.theme -eq $d -and $_.suit -eq $s })
        if ($cards.Count -ne 13) { throw "$d/$s : manifest lists $($cards.Count) cards" }
        [int[][]] $boxes = @($cards | ForEach-Object { , [int[]]@($_.region.x, $_.region.y, $_.region.width, $_.region.height) })
        $fr = [DeckTools]::Frames($im, $bg, $boxes, 4, 8, 14)
        for ($i = 0; $i -lt 13; $i++) {
            $f = $fr[$i]
            if ($null -eq $f) { throw "$d/$s $($cards[$i].rank): no card frame found" }
            $cm = [DeckTools]::MeasureCard($im, [int[]]$f[0..3], $f[4], $f[5], $f[6], $f[7])
            [DeckTools]::MeasureCorners($im, $cm, 13.5)
            $faces.Add([pscustomobject]@{
                deck = $d; suit = $s; rank = $cards[$i].rank; row = [int]($i -ge 7); col = $(if ($i -lt 7) { $i } else { $i - 7 })
                m = $cm; bg = $bg
                L = $cm.L; T = $cm.T; R = $cm.R; B = $cm.B; W = $cm.Width; H = $cm.Height
                tilt = ($cm.Sides | ForEach-Object { [Math]::Abs($_.EdgeB - $_.EdgeA) } | Measure-Object -Maximum).Maximum
                rTL = $cm.CornerR[0]; rTR = $cm.CornerR[1]; rBR = $cm.CornerR[2]; rBL = $cm.CornerR[3]
                fallback = @(0..3 | Where-Object { [double]::IsNaN($cm.CornerR[$_]) -or -not ($cm.CornerRms[$_] -le 1.0) }).Count
            })
        }
    }
}
foreach ($x in $faces) { foreach ($v in 'L', 'T', 'R', 'B') { if ([double]::IsNaN($x.$v)) { throw "$($x.deck)/$($x.suit) $($x.rank): edge $v not found" } } }

# ------------------------------------------------------------------------------ 2. regularity
# Cards in one sheet row share their top and bottom edges; columns share a regular pitch. A card
# whose top or bottom is off its row's median by more than 0.75 px, or whose two half-side
# measurements disagree by more than 1.5 px, is reported (and would need a look).
$warnings = New-Object System.Collections.Generic.List[string]
foreach ($g in ($faces | Group-Object deck, suit, row)) {
    $mT = Median @($g.Group | ForEach-Object T); $mB = Median @($g.Group | ForEach-Object B)
    foreach ($x in $g.Group) {
        if ([Math]::Abs($x.T - $mT) -gt 0.75) { $warnings.Add("$($x.deck)/$($x.suit) $($x.rank): top $(F $x.T 0.00) vs row $(F $mT 0.00)") }
        if ([Math]::Abs($x.B - $mB) -gt 0.75) { $warnings.Add("$($x.deck)/$($x.suit) $($x.rank): bottom $(F $x.B 0.00) vs row $(F $mB 0.00)") }
        if ($x.tilt -gt 1.5) { $warnings.Add("$($x.deck)/$($x.suit) $($x.rank): halves of a side differ by $(F $x.tilt 0.00) px") }
    }
}
$rMed = Median @($faces | ForEach-Object { $_.rTL; $_.rTR; $_.rBR; $_.rBL })
$faces | Select-Object deck, suit, rank, row, col, L, T, R, B, W, H, tilt, rTL, rTR, rBR, rBL |
    Export-Csv -NoTypeInformation -Path (Join-Path $work 'measure-faces.csv')

# ------------------------------------------------------------------------------ 3. measure backs
Write-Host "Measuring backs..."
$mock = [Img]::Load($mockup)
$panel = [DeckTools]::SheetBg($mock, 540, 548)    # strip of the option panels below the backs
$backBoxes = @{ 'classic-crimson' = @(425, 232, 220, 310); 'himalayan-ivory' = @(727, 232, 220, 310); 'kathmandu-gold' = @(1028, 232, 220, 310) }
$backs = @{}
foreach ($d in $Decks) {
    $b = $backBoxes[$d]
    $f = [DeckTools]::RoughFrame($mock, $panel, $b[0], $b[1], $b[2], $b[3], 8)
    $cm = [DeckTools]::MeasureCard($mock, $f, 24, 24, 24, 24)
    [DeckTools]::MeasureCorners($mock, $cm, 14.5)
    $backs[$d] = [pscustomobject]@{ deck = $d; m = $cm; L = $cm.L; T = $cm.T; R = $cm.R; B = $cm.B; W = $cm.Width; H = $cm.Height
        rTL = $cm.CornerR[0]; rTR = $cm.CornerR[1]; rBR = $cm.CornerR[2]; rBL = $cm.CornerR[3] }
}
$backs.Values | Select-Object deck, L, T, R, B, W, H, rTL, rTR, rBR, rBL | Export-Csv -NoTypeInformation -Path (Join-Path $work 'measure-backs.csv')

# ------------------------------------------------------------------------------ 4. cut
Write-Host "Cutting $Width x $Height, corner radius $Radius..."
$mask = [Cut]::RoundRectMask($Width, $Height, $Radius, 32)
$exported = @{}    # deck -> list of @{ file; img; label; notes }
foreach ($d in $Decks) {
    $dn = $deckName[$d]
    $outDir = Join-Path $exportRoot $dn
    New-Item -ItemType Directory -Force $outDir | Out-Null     # names are fixed: files are overwritten
    $list = New-Object System.Collections.Generic.List[object]
    $rDeck = Median @($faces | Where-Object deck -eq $d | ForEach-Object { $_.rTL; $_.rTR; $_.rBR; $_.rBL })
    foreach ($x in ($faces | Where-Object deck -eq $d)) {
        $name = "Card_$($suitName[$x.suit])_$($x.rank)"
        $cut = New-Cut $name $x.m $rDeck
        $img = [Cut]::CutCard($sheets["$d/$($x.suit)"], $cut, $Width, $Height, $mask, 6)
        $fid = [Cut]::Fidelity($sheets["$d/$($x.suit)"], $cut, $img, 3)
        $file = Join-Path $outDir "$name.png"
        $img.SavePng($file, $true)
        $list.Add([pscustomobject]@{ file = $file; img = $img; label = $name.Substring(5); bg = $x.bg; suit = $x.suit; rank = $x.rank; fid = $fid
            notes = "from full-decks/$d/$($x.suit).png; card $(F $x.W 0.0)x$(F $x.H 0.0) px on the sheet; scale $(F ($Width / $x.W))/$(F ($Height / $x.H))" })
    }
    $bk = $backs[$d]
    $rB = Median @($bk.rTL, $bk.rTR, $bk.rBR, $bk.rBL)
    $cut = New-Cut "Back_$dn" $bk.m $rB
    $img = [Cut]::CutCard($mock, $cut, $Width, $Height, $mask, 6)
    $fid = [Cut]::Fidelity($mock, $cut, $img, 3)
    $file = Join-Path $outDir "Back_$dn.png"
    $img.SavePng($file, $true)
    $list.Add([pscustomobject]@{ file = $file; img = $img; label = 'Back'; bg = $panel; suit = ''; rank = ''; fid = $fid
        notes = "from game-screens/15-card-styles.png; back $(F $bk.W 0.0)x$(F $bk.H 0.0) px on the mockup; scale $(F ($Width / $bk.W))/$(F ($Height / $bk.H)) (non-uniform)" })
    $exported[$d] = $list
}

# ------------------------------------------------------------------------------ 5. checks
Write-Host "Checking..."
$qa = New-Object System.Collections.Generic.List[string]
$failed = 0
foreach ($d in $Decks) {
    $dn = $deckName[$d]
    $list = $exported[$d]
    $names = @($list | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.file) })
    $faceNames = @($names | Where-Object { $_ -like 'Card_*' })
    $want = foreach ($s in $suitName.Values) { foreach ($r in $ranks) { "Card_${s}_$r" } }
    $missing = @($want | Where-Object { $faceNames -notcontains $_ })
    $unique = @($faceNames | Sort-Object -Unique).Count
    $line = "$dn : $($faceNames.Count) faces ($unique unique), back: $(@($names | Where-Object { $_ -eq "Back_$dn" }).Count)"
    if ($missing.Count -or $unique -ne 52 -or $faceNames.Count -ne 52) { $line += "  FAIL missing: $($missing -join ' ')"; $failed++ }
    $qa.Add($line)
    $shadowTotal = 0
    foreach ($e in $list) {
        $stats = New-Object int[] 3
        $errs = [Qa]::Check($e.file, $Width, $Height, $mask, $e.bg, $stats)
        $shadowTotal += $stats[2]
        if ($stats[2] -gt 0) { $qa.Add("  $([IO.Path]::GetFileName($e.file)): $($stats[2]) shadow-coloured rim pixels") }
        if ($errs.Count) { $failed++; $qa.Add("  FAIL $([IO.Path]::GetFileName($e.file)): $($errs -join '; ')") }
    }
    $qa.Add("  all $($list.Count) files $Width x $Height, 8-bit RGBA, corners clear, centre opaque, alpha identical to the shared mask: $(if ($failed) { 'see failures' } else { 'yes' }); suspected shadow rim pixels in total: $shadowTotal")
    $fr = @($list | ForEach-Object { $_.fid[0] }); $fg = @($list | ForEach-Object { $_.fid[1] }); $fb = @($list | ForEach-Object { $_.fid[2] }); $fa = @($list | ForEach-Object { $_.fid[3] })
    $qa.Add(('  colour vs source (interior): mean shift R {0} G {1} B {2} levels (worst card {3}); mean abs difference {4} (worst {5})' -f
        (F (($fr | Measure-Object -Average).Average) 0.00), (F (($fg | Measure-Object -Average).Average) 0.00), (F (($fb | Measure-Object -Average).Average) 0.00),
        (F (@($fr + $fg + $fb | ForEach-Object { [Math]::Abs($_) }) | Measure-Object -Maximum).Maximum 0.00),
        (F (($fa | Measure-Object -Average).Average) 0.00), (F (($fa | Measure-Object -Maximum).Maximum) 0.00)))
}
# Index box: rank + suit ink below the corner ornament, on every face (see IndexBox in DeckTools).
$idx = @()
foreach ($d in $Decks) {
    foreach ($e in ($exported[$d] | Where-Object { $_.suit })) {
        $b = [IndexBox]::Find($e.img, ($d -eq 'kathmandu-gold'), 18, 20, 100, 135, 60)
        if ($null -eq $b) { continue }
        $idx += [pscustomobject]@{ deck = $d; x0 = [Math]::Min($b[0], $b[4]); y0 = $b[1]; x1 = [Math]::Max($b[2], $b[6]); y1 = $b[7]; rankH = $b[3] - $b[1] + 1 }
    }
}
$nIdx = $idx.Count
$ix0 = ($idx.x0 | Measure-Object -Minimum).Minimum; $iy0 = ($idx.y0 | Measure-Object -Minimum).Minimum
$ix1 = ($idx.x1 | Measure-Object -Maximum).Maximum; $iy1 = ($idx.y1 | Measure-Object -Maximum).Maximum
$qa.Add(('index box, union of {0} faces: x {1}..{2} y {3}..{4} px = x {5} y {6} w {7} h {8} of the card; median box x {9}..{10} y {11}..{12}; rank glyph {13}..{14} px tall (median {15})' -f
    $nIdx, $ix0, $ix1, $iy0, $iy1, (F ($ix0 / $Width)), (F ($iy0 / $Height)), (F (($ix1 - $ix0 + 1) / $Width)), (F (($iy1 - $iy0 + 1) / $Height)),
    (Median @($idx.x0)), (Median @($idx.x1)), (Median @($idx.y0)), (Median @($idx.y1)),
    ($idx.rankH | Measure-Object -Minimum).Minimum, ($idx.rankH | Measure-Object -Maximum).Maximum, (Median @($idx.rankH))))
# Frame alignment: each face's frame profile on each side against the deck's median profile.
foreach ($d in $Decks) {
    $fc = @($exported[$d] | Where-Object { $_.suit })
    $parts = @()
    foreach ($side in 0..3) {
        $profiles = @($fc | ForEach-Object { , [FrameAlign]::Profile($_.img, $side, 40) })
        $ref = New-Object double[] 40
        for ($k = 0; $k -lt 40; $k++) { $ref[$k] = Median @($profiles | ForEach-Object { $_[$k] }) }
        $off = @($profiles | ForEach-Object { [FrameAlign]::Offset($_, $ref) })
        $abs = @($off | ForEach-Object { [Math]::Abs($_) } | Sort-Object)
        $parts += '{0} max {1} p90 {2}' -f @('left', 'top', 'right', 'bottom')[$side], (F $abs[-1] 0.00), (F $abs[[int][Math]::Floor(0.9 * ($abs.Count - 1))] 0.00)
    }
    $qa.Add("$($deckName[$d]) frame offsets vs deck median (px): $($parts -join '; ')")
}
# Round trip: a saved file reads back exactly as written.
$e = $exported[$Decks[0]][0]
$back = [Img]::Load($e.file); $diff = 0
for ($i = 0; $i -lt $Width * $Height; $i++) {
    if ($back.A[$i] -ne [Img]::Q($e.img.A[$i]) -or $back.R[$i] -ne [Img]::Q($e.img.R[$i]) -or $back.G[$i] -ne [Img]::Q($e.img.G[$i]) -or $back.B[$i] -ne [Img]::Q($e.img.B[$i])) { $diff++ }
}
$qa.Add("round trip $([IO.Path]::GetFileName($e.file)): $diff pixels differ; chunks: $([Png]::Chunks($e.file))")
if ($diff) { $failed++ }

# ------------------------------------------------------------------------------ 6. review sheets
if (-not $NoReview) {
    Write-Host "Review sheets..."
    New-Item -ItemType Directory -Force $verifyDir | Out-Null
    $dark = [System.Drawing.Color]::FromArgb(24, 24, 24); $light = [System.Drawing.Color]::White
    $table = [System.Drawing.ColorTranslator]::FromHtml('#E9D5BC')
    $handOrder = @('Spades A', 'Spades K', 'Spades J', 'Spades 9', 'Spades 4', 'Hearts A', 'Hearts J', 'Hearts 8', 'Diamonds K', 'Diamonds 10', 'Diamonds 5', 'Clubs Q', 'Clubs 7')
    $zoomPick = @('Spades A', 'Hearts 7', 'Diamonds 4', 'Clubs 8', 'Spades K', 'Hearts Q', 'Diamonds 10', 'Clubs J')
    foreach ($d in $Decks) {
        $dn = $deckName[$d]
        $list = $exported[$d]
        $byKey = @{}; foreach ($e in $list) { if ($e.suit) { $byKey["$($suitName[$e.suit]) $($e.rank)"] = $e } }
        $backE = $list | Where-Object label -eq 'Back'
        # Contact sheets: 4 suit rows of 13, then the back.
        $imgs = New-Object System.Collections.Generic.List[Img]; $labs = New-Object System.Collections.Generic.List[string]
        foreach ($s in $suitName.Values) { foreach ($r in $ranks) { $e = $byKey["$s $r"]; $imgs.Add($e.img); $labs.Add("$s $r") } }
        $imgs.Add($backE.img); $labs.Add("Back_$dn")
        foreach ($mode in 'dark', 'light') {
            $c = [Review]::Contact($imgs.ToArray(), $labs.ToArray(), 13, 12, $(if ($mode -eq 'dark') { $dark } else { $light }), $(if ($mode -eq 'dark') { $light } else { $dark }))
            $c.SavePng((Join-Path $reviewDir "${dn}_contact_$mode.png"))
            $c.Shrink(2).SavePng((Join-Path $reviewDir "${dn}_contact_${mode}_half.png"))
        }
        # Corner zooms (4x) on black and white.
        $zi = @($zoomPick | ForEach-Object { $byKey[$_].img }) + $backE.img
        $zl = @($zoomPick) + 'Back'
        ([Review]::CornerZooms([Img[]]$zi, [string[]]$zl, 20, 4)).SavePng((Join-Path $reviewDir "${dn}_corners_x4.png"))
        # Hand mock on the table colour: each card shows half its width.
        $hi = @($handOrder | ForEach-Object { $byKey[$_].img })
        ([Review]::Hand([Img[]]$hi, 0.5, $table, 40)).SavePng((Join-Path $reviewDir "${dn}_hand.png"))
    }
    # Docs get the half-size contact sheets, corner zooms and hands (the full-size sheets, ~7 MB
    # each, stay in ArtSource/Working/Decks/review).
    Get-ChildItem $reviewDir -Filter '*.png' | Where-Object { $_.Name -like '*_half.png' -or $_.Name -like '*_corners_x4.png' -or $_.Name -like '*_hand.png' } |
        Copy-Item -Destination $verifyDir -Force
}

# ------------------------------------------------------------------------------ 7. report + handoff
$report = New-Object System.Collections.Generic.List[string]
$report.Add("export-decks.ps1  $Width x $Height  corner radius $Radius  ($(Get-Date -Format s))")
$report.Add("face corner radius, median of all fitted corners (sheet px): $(F $rMed 0.00); corners whose fit was poor (rms > 1 px) and took the deck median: $(($faces | Measure-Object fallback -Sum).Sum) of $($faces.Count * 4)")
$aspectChange = @($faces | ForEach-Object { (($Width / $_.W) / ($Height / $_.H) - 1) * 100 })
$report.Add(('face scale: x {0}..{1}, y {2}..{3}; aspect change (x scale / y scale - 1) {4}%..{5}%' -f
    (F (($faces | ForEach-Object { $Width / $_.W } | Measure-Object -Minimum).Minimum)), (F (($faces | ForEach-Object { $Width / $_.W } | Measure-Object -Maximum).Maximum)),
    (F (($faces | ForEach-Object { $Height / $_.H } | Measure-Object -Minimum).Minimum)), (F (($faces | ForEach-Object { $Height / $_.H } | Measure-Object -Maximum).Maximum)),
    (F (($aspectChange | Measure-Object -Minimum).Minimum) 0.0), (F (($aspectChange | Measure-Object -Maximum).Maximum) 0.0)))
foreach ($g in ($faces | Group-Object deck, row)) {
    $x = $g.Group
    $report.Add(('{0,-22} W {1} ({2}..{3})  H {4} ({5}..{6})  aspect {7}' -f $g.Name, (F (Median @($x.W)) 0.00), (F (($x.W | Measure-Object -Minimum).Minimum) 0.00), (F (($x.W | Measure-Object -Maximum).Maximum) 0.00),
        (F (Median @($x.H)) 0.00), (F (($x.H | Measure-Object -Minimum).Minimum) 0.00), (F (($x.H | Measure-Object -Maximum).Maximum) 0.00), (F ((Median @($x.W)) / (Median @($x.H))) 0.0000)))
}
foreach ($d in $Decks) { $b = $backs[$d]; $report.Add(('back {0,-18} {1} x {2}  aspect {3}  corners {4} {5} {6} {7}' -f $d, (F $b.W 0.00), (F $b.H 0.00), (F ($b.W / $b.H) 0.0000), (F $b.rTL 0.0), (F $b.rTR 0.0), (F $b.rBR 0.0), (F $b.rBL 0.0))) }
$report.Add("regularity warnings: $($warnings.Count)")
foreach ($w in $warnings) { $report.Add("  $w") }
foreach ($q in $qa) { $report.Add($q) }
$report.Add("QA: $(if ($failed) { "$failed FAILURES" } else { 'passed' })")
$report | Set-Content -Encoding UTF8 (Join-Path $work 'report.txt')
if (-not $NoReview) { Copy-Item (Join-Path $work 'report.txt') (Join-Path $verifyDir 'report.txt') -Force }
$report | ForEach-Object { Write-Host $_ }

# handoff.csv: drop earlier card rows, append one per exported file (UTF-8 with BOM, CRLF).
$lines = [IO.File]::ReadAllLines($handoff, [Text.Encoding]::UTF8)
$keep = @($lines | Where-Object { $_ -notlike 'Cards/*' })
foreach ($d in $Decks) {
    foreach ($e in $exported[$d]) {
        $rel = 'Cards/' + $deckName[$d] + '/' + [IO.Path]::GetFileName($e.file)
        $keep += "$rel,card,${Width}x$Height,,all,cut from sheet,review,$($e.notes)"
    }
}
[IO.File]::WriteAllText($handoff, (($keep -join "`r`n") + "`r`n"), (New-Object Text.UTF8Encoding $true))

# ------------------------------------------------------------------------------ 8. install
if ($Install) {
    if ($failed) { throw "QA failed ($failed); not copying into Assets." }
    foreach ($d in $Decks) {
        $dst = Join-Path $installRoot $deckName[$d]
        New-Item -ItemType Directory -Force $dst | Out-Null
        foreach ($e in $exported[$d]) { Copy-Item $e.file (Join-Path $dst ([IO.Path]::GetFileName($e.file))) -Force }
        Write-Host "installed $($exported[$d].Count) files into $dst"
    }
}
