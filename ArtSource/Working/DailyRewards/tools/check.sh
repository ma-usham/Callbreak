#!/usr/bin/env bash
# Checks of the DailyRewards sprites on mockup 26, 9-sliced the way Unity draws them (Tools/art job
# resizes every sprite whole, so its composite draws the stretched kit card as an oval and the 9-sliced
# tiles, button and wallet with distorted corners). Every layer at its screen.json rect, over the mockup
# itself; words left out. Writes ArtSource/Working/DailyRewards/check/*.png and prints the rms.
# Needs SU (shopui, ArtSource/Working/ShopUI/tools), as make.sh.
set -euo pipefail
cd "$(dirname "$0")/../../../.."
SU=${SU:-shopui}
W=ArtSource/Working/DailyRewards
M=Assets/ArtRefrences/game-screens/26-daily-rewards.png
A=Assets/_Project/Art
P=$A/UI/Panels; D=$A/UI/Decor
N="@nine=132,160,132,44@unit=2.3"
TF="@nine=138,164,138,48@unit=2.3"
# the words (labels) of the panel, left out of every number
WORDS="750,203,172,44;432,275,82,39;679,273,78,36;922,273,78,36;1165,273,78,36;435,493,78,37;678,493,78,37;1034,487,93,43"
WORDS="$WORDS;400,408,148,37;644,408,148,37;884,408,152,37;1127,408,152,37;400,642,148,37;643,642,149,37;1003,654,153,38"
WORDS="$WORDS;1009,621.5,141,28;655,740,358,52;666,832,336,34"

TILES="$P/UI_DayTile_Normal.png@357.705,254.325,231.55,207.95$N;$P/UI_DayTile_TodayFrame.png@355.6,252.9,235.76,210.80$TF"
TILES="$TILES;$P/UI_DayTile_Normal.png@601.20,254.20,231.55,207.95$N;$P/UI_DayTile_Normal.png@844.50,254.30,231.60,207.85$N"
TILES="$TILES;$P/UI_DayTile_Normal.png@1087.85,254.20,230.45,207.95$N;$P/UI_DayTile_Normal.png@357.00,474.40,232.50,225.15$N"
TILES="$TILES;$P/UI_DayTile_Normal.png@600.90,474.40,231.90,224.90$N"
TILES="$TILES;$P/UI_DayTile_Bonus.png@843.85,473.65,474.30,227.50@nine=140,150,140,150@unit=2.3"
COINS="$D/Deco_CoinGlow.png@383,273,180,180"
COINS="$COINS;$D/Deco_DayCoins_1_Shadow.png@415.27,305.82,115.47,117.38;$D/Deco_DayCoins_1.png@439.18,329.73,67.64,69.55"
COINS="$COINS;$D/Deco_DayCoins_2_Shadow.png@639.92,306.92,141.68,120.17;$D/Deco_DayCoins_2.png@663.83,330.83,93.85,72.34"
COINS="$COINS;$D/Deco_DayCoins_3_Shadow.png@877.70,298.26,152.33,125.80;$D/Deco_DayCoins_3.png@901.61,322.17,104.50,77.97"
COINS="$COINS;$D/Deco_DayCoins_4_Shadow.png@1117.42,295.18,167.18,130.95;$D/Deco_DayCoins_4.png@1141.33,319.09,119.35,83.12"
COINS="$COINS;$D/Deco_DayCoins_5_Shadow.png@380.89,522.17,185.23,132.06;$D/Deco_DayCoins_5.png@404.80,546.08,137.40,84.23"
COINS="$COINS;$D/Deco_DayCoins_6_Shadow.png@618.82,517.37,204.58,141.98;$D/Deco_DayCoins_6.png@642.73,541.28,156.75,94.15"
COINS="$COINS;$D/Deco_Laurel.png@930.79,525.98,37.42,96.85;$D/Deco_Laurel.png@1191.8,525.98,37.42,96.85@flipx"
COINS="$COINS;$D/Deco_DayCoins_7_Shadow.png@951.40,505.95,256.23,153.02;$D/Deco_DayCoins_7.png@975.31,529.86,208.40,105.19"
COINS="$COINS;$D/Deco_Ribbon_Gold.png@952.43,615.89,254.15,45.72@nine=72,0,72,0@unit=2.3"
DECO="$D/Deco_HeadingFlourish.png@591.65,212.49,135.90,21.33;$D/Deco_HeadingFlourish.png@945.35,212.49,135.90,21.33@flipx"
DECO="$DECO;$A/UI/Buttons/UI_ButtonPrimary_Normal.png@519.8,720.1,634.2,94.6@nine=202,0,202,0@unit=3.446"
DECO="$DECO;$A/UI/Controls/UI_PrizeRowLine.png@558.5,844.7,91.0,6.09@nine=3,0,3,0@unit=2.3;$A/UI/Controls/UI_PrizeRowLine.png@1022.5,844.7,91.0,6.09@nine=3,0,3,0@unit=2.3"
PANEL="$A/UI/Panels/UI_Panel_Card.png@331.2,184.7,1009.4,694.3@nine=56,56,56,56@unit=1.311"

run() { echo "$1: $($SU scene --screen $M --region $2 --layers "$3" --exclude "$WORDS${6:+;$6}" --zoom ${5:-1} --out $W/check/$4.png | head -1)"; }
run "panel, everything in it" 325,178,1022,708 "$PANEL;$TILES;$COINS;$DECO" rebuild_panel
run "the seven tiles (tiles, overlay, coins, Day 7 decor)" 352,248,972,458 "$TILES;$COINS" rebuild_tiles 2
run "Day 1, today's look" 352,248,242,220 "$TILES;$COINS" rebuild_day1 3
run "Day 2 tile alone (no coins)" 596,249,242,218 "$P/UI_DayTile_Normal.png@601.20,254.20,231.55,207.95$N" tile_day2 3 "645,316,137,98"
run "Day 7 tile alone (no decor)" 838,468,485,238 "$P/UI_DayTile_Bonus.png@843.85,473.65,474.30,227.50@nine=140,150,140,150@unit=2.3" tile_day7 2 "922,520,58,108;1183,520,58,108;958,522,240,98;942,604,280,64"
run "heading flourishes" 585,205,500,36 "$DECO" heading
run "claim button" 512,712,650,112 "$DECO" claim
run "footer hairlines" 550,838,570,16 "$DECO" footer 4
# corner close-ups (mockup on top, sprite below) and the reuse checks
echo "Day 2 tile corners: $($SU nineview --sprite $P/UI_DayTile_Normal.png --screen $M --rect 601.20,254.20,231.55,207.95 --spots corners --size 40 --zoom 6 --out $W/check/tile_normal_corners.png | grep rms | tr '\n' ' ')"
echo "Day 7 tile corners: $($SU nineview --sprite $P/UI_DayTile_Bonus.png --screen $M --rect 843.85,473.65,474.30,227.50 --spots corners --size 50 --zoom 5 --out $W/check/tile_bonus_corners.png | grep rms | tr '\n' ' ')"
for c in 350,247 553,247 350,430 553,430; do run "Day 1 corner at $c (tile + overlay)" $c,40,40 "$TILES" today_corner_${c%%,*}_${c##*,} 6; done
run "the overlay on the bonus tile (no mockup: a look, not a match)" 836,466,490,242 "$P/UI_DayTile_Bonus.png@843.85,473.65,474.30,227.50@nine=140,150,140,150@unit=2.3;$P/UI_DayTile_TodayFrame.png@841.745,472.225,478.51,230.35$TF" today_over_bonus 2
run "reuse: Icon_Coin as Day 1's coin (for comparison with Deco_DayCoins_1)" 425,320,100,95 "$A/UI/Icons/Icon_Coin.png@440.2,330.2,66.6,66.6" reuse_coin_day1 4
run "Deco_DayCoins_1 on Day 1" 425,320,100,95 "$D/Deco_DayCoins_1.png@439.18,329.73,67.64,69.55" coins_day1 4
run "reuse: Deco_CoinGlow behind Day 1's coin" 366,296,214,112 "$TILES;$COINS" glow_day1 3 "436,327,74,76"
run "reuse: wallet (pill, coin)" 1430,46,204,84 "$A/UI/Buttons/UI_ButtonSecondary_Normal.png@1436.6,52.6,190.2,71.2@nine=117,0,117,0@unit=2.977;$A/UI/Icons/Icon_Coin.png@1460.63,58.63,56.45,56.45" wallet 3 "1532,70,68,36"
run "reuse: back disc and icon" 36,28,100,100 "$A/UI/Buttons/UI_ButtonCircle_Normal.png@42.6,34.4,87.0,87.0;$A/UI/Icons/Icon_Back.png@66.17,59.06,39.39,37.43" back 3
run "reuse: title divider" 600,148,470,28 "$A/UI/Decor/Deco_TitleDivider_Line.png@606.0,160.4,221.0,3.4@flipx;$A/UI/Decor/Deco_TitleDivider_Diamond.png@826.5,151.8,17.5,20.6;$A/UI/Decor/Deco_TitleDivider_Line.png@844.0,160.4,221.0,3.4" divider 3
