$repo = 'D:\UnityProjects\Callbreak'
Add-Type -AssemblyName System.Drawing
Add-Type -Path "$repo\Tools\ArtPipeline\DeckTools.cs" -ReferencedAssemblies System.Drawing
$fd = "$repo\Assets\ArtRefrences\card-designs\full-decks"
foreach ($d in 'classic-crimson', 'himalayan-ivory', 'kathmandu-gold') {
    foreach ($s in 'spades', 'hearts', 'diamonds', 'clubs') {
        $im = [FImage]::Load("$fd\$d\$s.png")
        $cards = [DeckTools]::FindCards($im, 110)
        $line = ($cards | ForEach-Object { '{0},{1},{2}x{3}' -f $_[0], $_[1], $_[2], $_[3] }) -join '  '
        '{0,-16} {1,-9} n={2}  {3}' -f $d, $s, $cards.Count, $line
    }
}
