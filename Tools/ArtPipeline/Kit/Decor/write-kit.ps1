# Writes the Decor family's hand-over files from the table below:
#   ArtSource/Kit/Decor.json      import settings (path under Assets/_Project/Art, ppu, nine)
#   ArtSource/Kit/Decor.map.csv   swap map for the screen engineers
# and, with -Handoff, appends the family's rows to ArtSource/handoff.csv (once; rows already
# present are skipped). Only sprites whose file exists are listed in the manifest.
param([switch] $Handoff)
. (Join-Path $PSScriptRoot 'decor.ps1')
$kit = "$DecorRepo\ArtSource\Kit"; New-Item -ItemType Directory -Force $kit | Out-Null

# --- finished sprites: path under Assets/_Project/Art, ppu, used_on, prompt, note
$sprites = @(
    @('Backgrounds/BG_Menu_Wide.png', 100, '00-06 09-12 14-16 (every menu screen)', 'BG-2 a4', 'ChatGPT outpaint for the 320 px side and 180 px top/bottom margins; the centre 1920x1080 is BG_Menu_Clean pixel for pixel'),
    @('UI/Decor/Deco_SpadeMedal.png', 200, '09 16', 'UI-7 a1', 'medal of 09 and seal of 16; made exactly round; colour matched to the mockup'),
    @('UI/Decor/Deco_Trophy.png', 200, '10', 'UI-7 a1', 'cup and laurel branches without the sparkles and without the glow and rays; colour matched to the mockup'),
    @('UI/Decor/Deco_TrophySparkle.png', 200, '10', 'UI-7 a1', 'one four-point sparkle with its glow, used three times; slightly softened'),
    @('UI/Decor/Deco_Crown.png', 200, '10', 'UI-7 ornament sheet a1', 'winner crown; colour matched to the mockup'),
    @('UI/Decor/Deco_MedalLine.png', 200, '09 16', 'UI-7 ornament sheet a1', 'line with diamond; left-hand version, flip for the right; line length, strength and ink measured on the mockup'),
    @('UI/Decor/Deco_FooterArrow.png', 200, '09', 'UI-7 ornament sheet a1', 'short line with diamond; left-hand version, flip for the right'),
    @('UI/Decor/Deco_RoundOrnament.png', 200, '07 08', 'UI-7 ornament sheet a1', 'near-black line with diamond beside ROUND n / 5; left-hand version, flip for the right'),
    @('UI/Decor/Deco_PillOrnament.png', 200, '12', 'UI-7 ornament sheet a1', 'woven gold diamond at both ends of the win pill'),
    @('UI/Decor/Deco_SearchArc.png', 200, '05', 'UI-7 a1', 'search ring without the spade; square, ring centred, so it rotates true'),
    @('UI/Decor/Deco_AceOverKing.png', 200, '12', 'as drawn a1', 'the two tilted cards as one sprite; no drop shadow'),
    @('UI/Decor/Deco_AceOverKing_Shadow.png', 200, '12', 'rebuilt', 'soft drop shadow of the two cards: blurred silhouette; spread 36.4 drop 11 opacity 0.40'),
    @('UI/Decor/Deco_TitleDivider.png', 200, '01 04 10 15 (and 02 05 06 11 12 14 16 as a fallback)', 'UI-7 divider sheet a1', 'line with centre diamond fading at both ends; proportions of screen 01'),
    @('UI/Decor/Deco_TitleDivider_Diamond.png', 200, '02 05 06 11 12 14 16', 'UI-7 divider sheet a1', 'the title divider diamond alone; never stretched'),
    @('UI/Decor/Deco_TitleDivider_Line.png', 200, '02 05 06 11 12 14 16', 'UI-7 divider sheet a1', 'one fading line of the title divider (right-hand; flip for the left); stretch to length'),
    @('UI/Decor/Deco_LobbyDivider.png', 200, '04', 'UI-7 divider sheet a1', 'long pale line with centre diamond'),
    @('UI/Decor/Deco_ProfileDivider.png', 200, '14', 'UI-7 divider sheet a1', 'long pale line with centre diamond and gaps beside it'),
    @('UI/Decor/Deco_ProfileDividerVertical.png', 200, '14', 'UI-7 divider sheet a1', 'vertical line with diamond'),
    @('UI/Decor/Logo_Callbreak_NoFlag.png', 200, '07 08 09 10 16', 'UI-6 a1', 'small logo without the flag from 08: dark PLAY TOGETHER and grey rules'),
    @('UI/Decor/Deco_MountainFooter.png', 200, '01', 'UI-7 a1', 'faint mountains along the bottom of the option cards; transparent overlay matted from a flat ivory ground'),
    @('UI/Avatars/Avatar_01.png', 200, '14 (picker) and seats', 'IC-2 avatar sheet a1', 'warm-grey variant; exact circle; silhouette sized like Avatar_Default'),
    @('UI/Avatars/Avatar_02.png', 200, '14 (picker) and seats', 'IC-2 avatar sheet a1', 'bronze variant'),
    @('UI/Avatars/Avatar_03.png', 200, '14 (picker) and seats', 'IC-2 avatar sheet a1', 'crimson variant'),
    @('UI/Avatars/Avatar_04.png', 200, '14 (picker) and seats', 'IC-2 avatar sheet a1', 'ivory variant with a thin brown outline')
)
Add-Type -AssemblyName System.Drawing
$entries = @(); $sizes = @{}
foreach ($s in $sprites) {
    $p = Join-Path $DecorArt ($s[0] -replace '/', '\')
    if (-not (Test-Path $p)) { continue }
    $img = [System.Drawing.Image]::FromFile($p); $sizes[$s[0]] = "{0}x{1}" -f $img.Width, $img.Height; $img.Dispose()
    $entries += ('    {{ "path": "{0}", "ppu": {1} }}' -f $s[0], $s[1])
}
$json = "{`r`n  `"sprites`": [`r`n" + ($entries -join ",`r`n") + "`r`n  ]`r`n}`r`n"
[System.IO.File]::WriteAllText("$kit\Decor.json", $json, (New-Object System.Text.UTF8Encoding $false))
"Decor.json: {0} sprites" -f $entries.Count

# --- the swap map: rows are read from map-rows.csv next to this script (edited by hand as
# assets are finished), so the map can be regenerated without touching this file.
$rows = Join-Path $PSScriptRoot 'map-rows.csv'
if (Test-Path $rows) {
    Copy-Item $rows "$kit\Decor.map.csv" -Force
    "Decor.map.csv: {0} rows" -f ((Get-Content $rows).Count - 1)
}

if ($Handoff) {
    $h = "$DecorRepo\ArtSource\handoff.csv"
    $have = Get-Content $h -Encoding UTF8
    $new = @()
    foreach ($s in $sprites) {
        if (-not $sizes.ContainsKey($s[0])) { continue }
        $file = ($s[0] -replace '^UI/Decor/', 'Decor/' -replace '^UI/Avatars/', 'Decor/')
        if ($have | Where-Object { $_.StartsWith("$file,") -and $_ -match ',' + [regex]::Escape($s[3]) + ',' }) { continue }
        $type = if ($s[0] -like 'Backgrounds/*') { 'bg' } elseif ($s[0] -like 'UI/Avatars/*') { 'avatar' } else { 'decor' }
        # no commas inside a field: the file is read as plain comma-separated lines
        $used = $s[2] -replace ',', ';'
        $note = ("Assets: {0}. {1}" -f $s[0], $s[4]) -replace ',', ';'
        $new += ('{0},{1},{2},,{3},{4},review,{5}' -f $file, $type, $sizes[$s[0]], $used, $s[3], $note)
    }
    if ($new.Count -gt 0) {
        [System.IO.File]::AppendAllText($h, (($new -join "`r`n") + "`r`n"), (New-Object System.Text.UTF8Encoding $false))
        "handoff.csv: appended {0} rows" -f $new.Count
    }
    else { "handoff.csv: nothing new" }
}
