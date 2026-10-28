# Prints the game's three decks from the vintage card pack's elements (ArtSource/Cards/Vintage):
# each deck keeps its own look (ADR 014: Classic Crimson, Himalayan Ivory, Kathmandu Gold), redrawn
# sharp at twice the export size, with the pack's index glyphs, pips, ornate aces and court figures
# printed on it in that deck's inks. Run it in a fresh PowerShell (Add-Type cannot reload types).
#
#   & Tools\ArtPipeline\compose-decks.ps1 -Sample    # a few cards of each deck, for review
#   & Tools\ArtPipeline\compose-decks.ps1            # all 156 faces, checks, review sheets, handoff rows
#   & Tools\ArtPipeline\compose-decks.ps1 -Install   # the same, then copy into Assets/ if QA passes
#
# The game draws the decks in layers (ADR 054): each deck's base (its frame on its paper) under one
# set of 52 faces shared by every deck, lifted off Classic Crimson's cards. -Install copies those
# (Art/Cards/Faces, Art/Cards/<Deck>/Base_<Deck>.png); the whole cards stay in ArtSource/Exports for
# review and as the check the layers are measured against.
# The backs stay as export-decks.ps1 cut them. Method and numbers: ArtSource/Prompts/Decks_Vintage.md.
# Do not pipe into Select-Object -First.
param(
    # Export size, px (the cards' 0.637). At 246 x 386 a deck's 52 faces fill one 2048 atlas page
    # and half of another, as ADR 014's 228 x 358 did; 256 x 402 needs two full pages (+8 MiB for
    # the three decks, ADR 050).
    [int] $Width = 246,
    [int] $Height = 386,
    [switch] $Install,           # copy the shared faces and the bases into Assets/_Project/Art/Cards (QA must pass)
    [switch] $Sample,            # only the sample cards, into ArtSource/Working/Decks/sample
    [string[]] $SampleCards = @('Spades A', 'Spades 5', 'Hearts 10', 'Clubs J', 'Diamonds Q', 'Hearts K')
)
$ErrorActionPreference = 'Stop'
$SampleCards = @($SampleCards | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() })   # powershell -File passes one string
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Add-Type -AssemblyName System.Drawing
if ('DeckTools' -as [type]) { throw 'DeckTools is already loaded in this session: run the script in a fresh PowerShell.' }
Add-Type -Path (Join-Path $PSScriptRoot 'DeckTools.cs'), (Join-Path $PSScriptRoot 'CardComposer.cs') -ReferencedAssemblies System.Drawing
function C([string] $hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }

$pack = Join-Path $repo 'ArtSource\Cards\Vintage'
$cardsRoot = Join-Path $repo 'Assets\_Project\Art\Cards'
$work = Join-Path $repo 'ArtSource\Working\Decks'
$sampleDir = Join-Path $work 'sample'
New-Item -ItemType Directory -Force $work, $sampleDir | Out-Null

$suits = [ordered]@{ Spades = 'Spade'; Hearts = 'Heart'; Diamonds = 'Diamond'; Clubs = 'Club' }
$packRank = [ordered]@{ A = 'A'; '2' = '02'; '3' = '03'; '4' = '04'; '5' = '05'; '6' = '06'; '7' = '07'; '8' = '08'; '9' = '09'; '10' = '10'; J = 'J'; Q = 'Q'; K = 'K' }

# The pack's inks (measured on its pips): near-black, and a deep red. The paper is measured per card.
$black = [double[]](16, 13, 12)
$red = [double[]](135, 8, 24)
$floor = 0.06

$split = @{}
function Split-Card([string] $name) {
    if (-not $split.ContainsKey($name)) {
        $im = [Img]::Load((Join-Path $pack "$name.png"))
        $split[$name] = [Ink]::Split($im, [Ink]::PaperOf($im), $black, $red, $floor)
    }
    $split[$name]
}

# Ink inside a box of a pack card, without the pieces that touch the box's sides (the frame's
# scrollwork, the pack's own index); with -OpenBottom a piece may touch the bottom (a court
# figure cut at the waist).
function Get-Piece([string] $name, [int] $x0, [int] $y0, [int] $x1, [int] $y1, [switch] $OpenBottom) {
    $layer = (Split-Card $name).Crop($x0, $y0, $x1 - $x0, $y1 - $y0)
    $w = $layer.W; $h = $layer.H; $open = [bool] $OpenBottom
    $layer.KeepComponents(0.3, 2, [Predicate[int[]]] {
        param($b)
        $b[4] -ge 12 -and $b[0] -gt 0 -and $b[1] -gt 0 -and $b[2] -lt $w -and ($open -or $b[3] -lt $h)
    }.GetNewClosure())
    $layer
}

# ------------------------------------------------------------------------------ 1. elements
Write-Host 'Lifting the elements from the pack...'
$e = New-Object PackElements
foreach ($s in $suits.Keys) {
    # The middle pip of the three: the one pip every number card repeats.
    $pip = (Get-Piece "$($suits[$s])_03" 150 250 362 462).Trim(0.02, 3)
    $e.Pip[$s] = $pip.ToWidth([Composer]::PipW)
    $e.SmallPip[$s] = $pip.ToWidth([Composer]::SmallPipW)
    $e.IndexPip[$s] = $pip.ToWidth([Composer]::IndexPipW)
    # The ace's ornament is fine filigree in many pieces, and the diamond's reaches x 75, next to
    # the frame's bar: a piece belongs to it when its centre lies in the middle of the card (the
    # bars, the corner rosettes and the pack's index all centre outside it).
    $ace = (Split-Card "$($suits[$s])_A").Crop(60, 100, 392, 520)
    $ace.KeepComponents(0.3, 2, [Predicate[int[]]] {
        param($b)
        $cx = ($b[0] + $b[2]) / 2 + 60; $cy = ($b[1] + $b[3]) / 2 + 100
        $b[4] -ge 12 -and $cx -ge 100 -and $cx -le 412 -and $cy -ge 110 -and $cy -le 600 -and $b[0] -gt 0 -and $b[2] -lt 392
    })
    $e.Ace[$s] = $ace.Trim(0.02, 3).ToWidth([Composer]::AceW)
}
# Index glyphs from the spades (black ink), all at one scale: the K's height sets it.
$glyphs = @{}
foreach ($r in $packRank.Keys) {
    $g = (Split-Card "Spade_$($packRank[$r])").Crop(10, 18, 90, 110)
    $gw = $g.W; $gh = $g.H
    # The glyph's strokes only: specks under 80 px (paper flecks, a frame's tip) go.
    $g.KeepComponents(0.3, 2, [Predicate[int[]]] { param($b) $b[4] -ge 80 -and $b[0] -gt 0 -and $b[1] -gt 0 -and $b[2] -lt $gw -and $b[3] -lt $gh }.GetNewClosure())
    $glyphs[$r] = $g.Trim(0.02, 2)
}
$glyphScale = [Composer]::IndexRankH / ($glyphs['K'].H - 4)
# The pack's glyphs are a light serif: a little bolder, they read like ADR 014's index at a fifth
# of the card's size (the trick, the opponents' view).
foreach ($r in $packRank.Keys) {
    $g = $glyphs[$r].Crop(-2, -2, $glyphs[$r].W + 4, $glyphs[$r].H + 4)
    $e.Rank[$r] = $g.Resize([Math]::Round($g.W * $glyphScale), [Math]::Round($g.H * $glyphScale)).Bolden([Composer]::IndexBolden)
}
# Courts: the figure from the crown to the waist (y 400, above the pack's half-figure turned
# head). The figure is one large piece that touches only the waist; the frame's scrollwork
# reaches into the box from the top and right in small pieces, and the large suit beside the
# crown (the deck's index shows the suit) sits on its own in the top left. All three go.
# Specks of scrollwork that float free of the box's sides go too: a piece is kept only inside the
# figure's own box.
foreach ($s in $suits.Keys) {
    foreach ($r in 'J', 'Q', 'K') {
        $layer = (Split-Card "$($suits[$s])_$r").Crop(80, 62, 356, 338)
        $w = $layer.W
        $probe = $layer.Crop(0, 0, $layer.W, $layer.H)
        $script:fig = $null
        $probe.KeepComponents(0.3, 0, [Predicate[int[]]] { param($b) if ($b[4] -ge 20000) { $script:fig = $b }; $true })
        if ($null -eq $script:fig) { throw "$s $r : no figure found" }
        $f = $script:fig
        $layer.KeepComponents(0.3, 2, [Predicate[int[]]] {
            param($b)
            $figure = $b[4] -ge 20000
            $inside = $b[0] -ge $f[0] - 2 -and $b[1] -ge $f[1] - 2 -and $b[2] -le $f[2] + 2 -and $b[3] -le $f[3] + 2
            $bigSuit = $b[4] -ge 2000 -and $b[0] -ge 10 -and $b[2] -le 125 -and $b[3] -le 155
            $figure -or ($b[4] -ge 12 -and $inside -and -not $bigSuit)
        }.GetNewClosure())
        $layer.Deepen([Composer]::CourtDeepen)
        $e.Court["$s $r"] = $layer.Resize([Math]::Round($layer.W * [Composer]::CourtScale), [Math]::Round($layer.H * [Composer]::CourtScale))
    }
}

# Himalayan Ivory's mountains: the faint wash of its ADR 014 ace, lifted once into
# ArtSource/Cards/HimalayanIvory (a 228 px card's wash; it is soft anyway), placed where it was.
$k = [Composer]::W / 228.0
$wm = @(
    @{ n = 'Top'; x = 65; y = 18 },
    @{ n = 'Bottom'; x = 40; y = 278 }
) | ForEach-Object {
    $layer = [Ink]::FromAlpha([Img]::Load((Join-Path $repo "ArtSource\Cards\HimalayanIvory\Mountains_$($_.n).png")))
    [pscustomobject]@{ layer = $layer.Resize([Math]::Round($layer.W * $k), [Math]::Round($layer.H * $k)); at = [int[]]([Math]::Round($_.x * $k), [Math]::Round($_.y * $k)) }
}

# ------------------------------------------------------------------------------ 2. styles
# Colours measured on ADR 014's decks (Docs/Verification/Decks), cleaned of the sheets' noise.
function New-Style($name, $band, $corner, $motif, $lines, $widths, $paper, $black, $blackLow, $red, $courtBlack, $courtRed, $seam) {
    $s = New-Object DeckStyle
    $s.Name = $name; $s.Band = C $band; $s.Corner = C $corner; $s.Motif = C $motif
    $s.Lines = [System.Drawing.Color[]] @($lines | ForEach-Object { C $_ }); $s.LineWidths = [float[]] $widths
    $s.Paper = C $paper; $s.Black = C $black; $s.BlackLow = C $blackLow; $s.Red = C $red
    $s.CourtBlack = C $courtBlack; $s.CourtRed = C $courtRed; $s.Seam = C $seam
    $s
}
$styles = [ordered]@{}
$styles['ClassicCrimson'] = New-Style 'Classic Crimson' '#6A0507' '#A50E10' '#E9A266' @('#FBF1D6', '#A8854E', '#F3E6CB') @(2.7, 5.2, 2.2) '#FBF5EA' '#141111' '#141111' '#B0101B' '#171212' '#A50F18' '#A8854E'
$styles['HimalayanIvory'] = New-Style 'Himalayan Ivory' '#CC9C68' '#EAD3B0' '#B98450' @('#F8F0DE', '#CDA478', '#F4E9D8') @(4.4, 4.6, 2.2) '#FCF6EC' '#141111' '#141111' '#AA0F18' '#211913' '#A3121A' '#C59A6A'
$styles['HimalayanIvory'].Watermarks = [InkLayer[]] @($wm | ForEach-Object layer)
$styles['HimalayanIvory'].WatermarkAt = [int[][]] @($wm | ForEach-Object { , $_.at })
$styles['HimalayanIvory'].WatermarkInk = C '#E2CFB4'
# Kathmandu Gold's spades and clubs are black like the other decks' (the owner, 26 Oct 2026: the
# bronze of ADR 014's sheets looked faded); the gold is in its frame.
$styles['KathmanduGold'] = New-Style 'Kathmandu Gold' '#100E0B' '#14110C' '#EBC05A' @('#5C4520', '#F2CB6A', '#B07A2A', '#E9D9AE') @(2.2, 4.0, 2.4, 2.2) '#FBF3E4' '#141111' '#141111' '#BC101B' '#171212' '#A8121B' '#C99A45'
$styles['KathmanduGold'].EdgeRim = C '#E3B254'
$styles['KathmanduGold'].EdgeRimWidth = 3.6

# One alpha for every file of every deck: the rounded corner at the export size.
$mask = [Cut]::RoundRectMask($Width, $Height, [Composer]::Radius * $Width / [Composer]::W, [Composer]::Radius * $Height / [Composer]::H, 32)

# ------------------------------------------------------------------------------ 3. sample
if ($Sample) {
    Write-Host 'Composing the sample...'
    $rows = New-Object System.Collections.Generic.List[Img]
    foreach ($d in $styles.Keys) {
        $imgs = New-Object System.Collections.Generic.List[Img]; $labs = New-Object System.Collections.Generic.List[string]
        foreach ($c in $SampleCards) {
            $suit, $rank = $c -split ' '
            $master = [Composer]::Face($styles[$d], $e, $suit, $rank)
            $face = [Composer]::Export($master, $Width, $Height, $mask)
            $face.SavePng((Join-Path $sampleDir "${d}_Card_${suit}_$rank.png"), $true)
            if ($c -eq $SampleCards[-1]) { $master.SavePng((Join-Path $sampleDir "${d}_Card_${suit}_${rank}_master.png")) }
            $imgs.Add($face); $labs.Add("$($styles[$d].Name): $c")
        }
        $rows.Add([Review]::Contact($imgs.ToArray(), $labs.ToArray(), $imgs.Count, 16, [System.Drawing.ColorTranslator]::FromHtml('#1E5A3C'), [System.Drawing.Color]::White))
    }
    $sheet = [Review]::Stack($rows.ToArray(), $true, 0, [System.Drawing.ColorTranslator]::FromHtml('#1E5A3C'))
    $sheet.SavePng((Join-Path $sampleDir 'sample_sheet.png'))
    Write-Host "sample: $sampleDir"
    return
}

# ------------------------------------------------------------------------------ 4. decks
Write-Host 'Composing the decks...'
$exportRoot = Join-Path $repo 'ArtSource\Exports\Cards'
$reviewDir = Join-Path $work 'review'
$verifyDir = Join-Path $repo 'Docs\Verification\Decks'
$handoff = Join-Path $repo 'ArtSource\handoff.csv'
New-Item -ItemType Directory -Force $reviewDir | Out-Null
$exported = @{}
foreach ($d in $styles.Keys) {
    $outDir = Join-Path $exportRoot $d
    New-Item -ItemType Directory -Force $outDir | Out-Null     # names are fixed: files are overwritten
    $list = New-Object System.Collections.Generic.List[object]
    foreach ($s in $suits.Keys) {
        foreach ($r in $packRank.Keys) {
            $face = [Composer]::Export([Composer]::Face($styles[$d], $e, $s, $r), $Width, $Height, $mask)
            $file = Join-Path $outDir "Card_${s}_$r.png"
            $face.SavePng($file, $true)
            $src = if ('J', 'Q', 'K' -contains $r) { "court $($suits[$s])_$r" } elseif ($r -eq 'A') { "ace $($suits[$s])_A" } else { "pips $($suits[$s])_03" }
            $list.Add([pscustomobject]@{ file = $file; img = $face; label = "$s $r"; notes = "composed: $d frame; $src; index Spade_$($packRank[$r])" })
        }
    }
    $exported[$d] = $list
}

# ------------------------------------------------------------------------------ 4b. layers
# Each deck's base, and the shared faces lifted off the reference deck's cards (ADR 054). Read back
# from the saved files, so the faces are measured against the 8-bit cards the game would show.
Write-Host 'Lifting the shared faces...'
$reference = 'ClassicCrimson'
$facesDir = Join-Path $exportRoot 'Faces'
New-Item -ItemType Directory -Force $facesDir | Out-Null
$bases = @{}
foreach ($d in $styles.Keys) {
    $file = Join-Path $exportRoot "$d\Base_$d.png"
    ([Composer]::Export([Composer]::Frame($styles[$d]), $Width, $Height, $mask)).SavePng($file, $true)
    $bases[$d] = [pscustomobject]@{ file = $file; img = [Img]::Load($file) }
}
$r = $styles[$reference]
$inks = [System.Drawing.Color[]] @($r.Black, $r.BlackLow, $r.Red, $r.CourtBlack, $r.CourtRed, $r.Seam)
$shared = New-Object System.Collections.Generic.List[object]
foreach ($x in $exported[$reference]) {
    $face = [Layers]::Lift([Img]::Load($x.file), $bases[$reference].img, $inks)
    $file = Join-Path $facesDir ([IO.Path]::GetFileName($x.file))
    $face.SavePng($file, $true)
    $shared.Add([pscustomobject]@{ file = $file; img = [Img]::Load($file); label = $x.label })
}

# ------------------------------------------------------------------------------ 5. checks
Write-Host 'Checking...'
$qa = New-Object System.Collections.Generic.List[string]
$failed = 0
foreach ($d in $styles.Keys) {
    $list = $exported[$d]
    $names = @($list | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.file) } | Sort-Object -Unique)
    $line = "${d}: $($list.Count) faces ($($names.Count) unique)"
    if ($names.Count -ne 52) { $line += '  FAIL'; $failed++ }
    $qa.Add($line)
    foreach ($x in $list) {
        $errs = [Qa]::Check($x.file, $Width, $Height, $mask, $null, $null)
        if ($errs.Count) { $failed++; $qa.Add("  FAIL $([IO.Path]::GetFileName($x.file)): $($errs -join '; ')") }
    }
}
$qa.Add("all files $Width x $Height, 8-bit RGBA, corners clear, centre opaque, alpha identical to the shared mask: $(if ($failed) { 'see failures' } else { 'yes' })")
# Pip counts: the number cards carry exactly their rank in pips (the pack's 5 of spades had nine).
foreach ($n in 2..10) { if ([Composer]::Pips($n).Count -ne $n) { $failed++; $qa.Add("FAIL layout of $n has $([Composer]::Pips($n).Count) pips") } }
$qa.Add('pip layouts 2-10: one pip per rank point')
# The layers against the whole cards: the reference deck must come back to within one level; the
# other decks show the reference deck's inks (their frames and paper are their own bases).
foreach ($d in $styles.Keys) {
    $worst = 0.0; $psnr = 99.0; $off = 0.0; $at = ''
    for ($i = 0; $i -lt $shared.Count; $i++) {
        $whole = [Img]::Load($exported[$d][$i].file)
        $m = [Layers]::Compare([Layers]::Over($bases[$d].img, $shared[$i].img), $whole)
        if ($m[0] -gt $worst) { $worst = $m[0]; $at = $shared[$i].label }
        $psnr = [Math]::Min($psnr, $m[1]); $off = [Math]::Max($off, $m[2])
    }
    $line = ('{0} base + shared faces vs its whole cards: largest difference {1} levels ({2}), PSNR at least {3:0.0} dB, at most {4:0.00} % of pixels more than 3 apart' -f $d, $worst, $at, $psnr, $off)
    if ($d -eq $reference -and $worst -gt 1) { $line += '  FAIL'; $failed++ }
    $qa.Add($line)
}

# ------------------------------------------------------------------------------ 6. review sheets
$dark = [System.Drawing.Color]::FromArgb(24, 24, 24); $light = [System.Drawing.Color]::White
$table = [System.Drawing.ColorTranslator]::FromHtml('#E9D5BC')
$handOrder = @('Spades A', 'Spades K', 'Spades J', 'Spades 9', 'Spades 4', 'Hearts A', 'Hearts J', 'Hearts 8', 'Diamonds K', 'Diamonds 10', 'Diamonds 5', 'Clubs Q', 'Clubs 7')
$zoomPick = @('Spades A', 'Hearts 7', 'Diamonds 4', 'Clubs 8', 'Spades K', 'Hearts Q', 'Diamonds 10', 'Clubs J')
New-Item -ItemType Directory -Force $verifyDir | Out-Null
foreach ($d in $styles.Keys) {
    $byKey = @{}; foreach ($x in $exported[$d]) { $byKey[$x.label] = $x.img }
    $imgs = @($exported[$d] | ForEach-Object img); $labs = @($exported[$d] | ForEach-Object label)
    foreach ($mode in 'dark', 'light') {
        $c = [Review]::Contact([Img[]]$imgs, [string[]]$labs, 13, 12, $(if ($mode -eq 'dark') { $dark } else { $light }), $(if ($mode -eq 'dark') { $light } else { $dark }))
        $c.SavePng((Join-Path $reviewDir "${d}_contact_$mode.png"))
        $c.Shrink(2).SavePng((Join-Path $reviewDir "${d}_contact_${mode}_half.png"))
    }
    ([Review]::CornerZooms([Img[]]@($zoomPick | ForEach-Object { $byKey[$_] }), [string[]]$zoomPick, 24, 4)).SavePng((Join-Path $reviewDir "${d}_corners_x4.png"))
    ([Review]::Hand([Img[]]@($handOrder | ForEach-Object { $byKey[$_] }), 0.5, $table, 40)).SavePng((Join-Path $reviewDir "${d}_hand.png"))
    Get-ChildItem $reviewDir -Filter "${d}_*.png" | Where-Object { $_.Name -like '*_half.png' -or $_.Name -like '*_corners_x4.png' -or $_.Name -like '*_hand.png' } |
        Copy-Item -Destination $verifyDir -Force
}

# ------------------------------------------------------------------------------ 7. report + handoff
$report = New-Object System.Collections.Generic.List[string]
$report.Add("compose-decks.ps1  $Width x $Height (master $([Composer]::W) x $([Composer]::H))  ($(Get-Date -Format s))")
$report.Add(('pack inks: black {0}, red {1}; coverage floor {2}' -f ($black -join ','), ($red -join ','), $floor))
$report.Add(('index glyph scale {0:0.000} (K {1} px tall in the pack)' -f $glyphScale, $glyphs['K'].H))
foreach ($q in $qa) { $report.Add($q) }
$report.Add("QA: $(if ($failed) { "$failed FAILURES" } else { 'passed' })")
$report | Set-Content -Encoding UTF8 (Join-Path $work 'compose-report.txt')
Copy-Item (Join-Path $work 'compose-report.txt') (Join-Path $verifyDir 'compose-report.txt') -Force
$report | ForEach-Object { Write-Host $_ }

# handoff.csv: the rows of the faces and bases are replaced (UTF-8 with BOM, CRLF); the backs' rows stay.
# The game no longer carries the whole cards of each deck, so their rows go.
$lines = [IO.File]::ReadAllLines($handoff, [Text.Encoding]::UTF8)
$keep = @($lines | Where-Object { $line = $_; -not ($styles.Keys | Where-Object { $line -like "Cards/$_/Card_*" }) })
$keep = @($keep | Where-Object { $_ -notlike 'Cards/Faces/*' -and $_ -notlike 'Cards/*/Base_*' })
foreach ($x in $shared) { $keep += "Cards/Faces/$([IO.Path]::GetFileName($x.file)),card,${Width}x$Height,,all,lifted off $reference's card (ADR 054),review,premultiplied alpha in linear light; drawn over the deck's base" }
foreach ($d in $styles.Keys) { $keep += "Cards/$d/Base_$d.png,card,${Width}x$Height,,all,composed from the vintage pack,review,$d frame on its paper; under the shared faces (ADR 054)" }
[IO.File]::WriteAllText($handoff, (($keep -join "`r`n") + "`r`n"), (New-Object Text.UTF8Encoding $true))

# ------------------------------------------------------------------------------ 8. install
if ($Install) {
    if ($failed) { throw "QA failed ($failed); not copying into Assets." }
    $dst = Join-Path $cardsRoot 'Faces'
    New-Item -ItemType Directory -Force $dst | Out-Null
    foreach ($x in $shared) { Copy-Item $x.file (Join-Path $dst ([IO.Path]::GetFileName($x.file))) -Force }
    Write-Host "installed $($shared.Count) shared faces into $dst"
    foreach ($d in $styles.Keys) {
        Copy-Item $bases[$d].file (Join-Path $cardsRoot "$d\Base_$d.png") -Force
        Write-Host "installed Base_$d.png"
    }
}
