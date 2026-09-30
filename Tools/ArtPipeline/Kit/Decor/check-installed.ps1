# Checks the INSTALLED Decor sprites (Assets/_Project/Art/UI/Decor) against the mockups at the
# rectangles given in the swap map: writes ArtSource/Working/Kit/Decor/fit/<Name>_check.png
# (mockup | sprite on its ground | difference x4) and <Name>_alpha.png, and prints the mean error
# (0-255) inside the rectangle plus a margin. Nothing is exported or changed.
. (Join-Path $PSScriptRoot 'decor.ps1')
$fitDir = "$DecorWork\fit"
$table = "$DecorRepo\ArtSource\Working\Table\chatgpt\BG_Table_Clean_a2.png"
$results = "$DecorRepo\ArtSource\Working\MatchResults\chatgpt\BG_MatchResults_Clean_a2.png"
# name, screen, x, y, w, h, margin, plate or '#RRGGBB' ground or ''
$list = @(
    @('Deco_SpadeMedal', '09', 796.6, 203.0, 78.2, 78.0, 6, ''),
    @('Deco_Trophy', '10', 728.1, 18.0, 217.0, 120.2, 14, $results),
    @('Deco_TrophySparkle', '10', 673.3, 68.0, 30.5, 36.0, 8, $results),
    @('Deco_Crown', '10', 671.0, 410.0, 32.9, 26.5, 6, ''),
    @('Deco_MedalLine', '09', 584.0, 236.4, 183.8, 15.4, 8, ''),
    @('Deco_FooterArrow', '09', 602.0, 854.1, 108.4, 7.8, 8, ''),
    @('Deco_RoundOrnament', '08', 643.0, 34.2, 78.3, 9.2, 8, $table),
    @('Deco_PillOrnament', '12', 602.6, 748.3, 26.0, 26.6, 6, ''),
    @('Deco_SearchArc', '05', 755.4, 222.3, 161.0, 161.0, 6, ''),
    @('Deco_AceOverKing', '12', 1007.0, 390.5, 277.5, 282.3, 10, ''),
    @('Deco_TitleDivider', '01', 672.0, 204.3, 330.0, 13.0, 8, ''),
    @('Deco_LobbyDivider', '04', 462.5, 641.4, 747.1, 13.3, 8, ''),
    @('Deco_ProfileDivider', '14', 451.8, 586.6, 767.9, 16.1, 8, ''),
    @('Deco_ProfileDividerVertical', '14', 746.6, 260.8, 13.3, 290.8, 8, ''),
    @('Logo_Callbreak_NoFlag', '08', 183.4, 33.2, 333.0, 82.4, 10, $table),
    @('Deco_MountainFooter', '01', 368.0, 622.4, 460.0, 93.2, 4, '#F5EBE0')
)
foreach ($e in $list) {
    $sprite = [DecorTools]::Load("$DecorArt\UI\Decor\$($e[0]).png")
    $mock = [DecorTools]::Load((Get-Mockup $e[1]))
    $bg = $null
    if ($e[7] -like '#*') { $bg = [DecorTools]::Flat($mock.Width, $mock.Height, [System.Drawing.ColorTranslator]::FromHtml($e[7])) }
    elseif ($e[7]) { $bg = [DecorTools]::Load($e[7]) }
    $err = [DecorTools]::CompositeError($mock, $bg, $sprite, $e[2], $e[3], $e[4], $e[5], $e[6])
    $scale = [int][Math]::Max(2, [Math]::Min(6, [Math]::Floor(620 / ($e[4] + 2 * $e[6]))))
    $sheet = [DecorTools]::CheckSheet($mock, $bg, $sprite, $e[2], $e[3], $e[4], $e[5], $e[6], $scale, 4)
    [DecorTools]::Save($sheet, "$fitDir\$($e[0])_check.png"); $sheet.Dispose()
    $alpha = [DecorTools]::AlphaSheet($sprite, 760); [DecorTools]::Save($alpha, "$fitDir\$($e[0])_alpha.png"); $alpha.Dispose()
    "{0,-28} {1}  {2,4}x{3,-4}  at {4},{5} {6}x{7}  error {8:F2}  (check at {9}x)" -f $e[0], $e[1], $sprite.Width, $sprite.Height, $e[2], $e[3], $e[4], $e[5], $err, $scale
    $sprite.Dispose(); $mock.Dispose(); if ($bg) { $bg.Dispose() }
}
