#!/usr/bin/env bash
# The recipe: from ChatGPT's sheets (ArtSource/Working/Kit/Controls/chatgpt/C<n>_a<k>.png) to the
# sprites in Assets/_Project/Art/UI/Controls. Every number below was measured: on the mockup
# (Tools/art profile, kc radial, kc rays, kc fit sweeps) and on the returned cell (kc line,
# kc radial); the prompt log (ArtSource/Prompts/Controls_Kit.md) says where each comes from.
#   make.sh cells            cut every sheet into its cells
#   make.sh <asset>          one group: toggle slider dropdown field codebox segment radio round
#                            outline seat pill button
#   make.sh all              everything
# Sprite pixels = mockup pixels x 2.3 (@2x), sizes rounded up to even numbers.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../../.." && pwd)"
cd "$repo"
KC="$here/kc"
W=ArtSource/Working/Kit/Controls
OUT=UI/Controls

cells() {
  $KC cells --in $W/chatgpt/C1_a1.png --def $W/attach/C1_Settings_sheet.cells.json --outdir $W/work/C1_a1
  $KC cells --in $W/chatgpt/C2_a1.png --def $W/attach/C2_Frames_sheet.cells.json --outdir $W/work/C2_a1
  $KC cells --in $W/chatgpt/C3_a1.png --def $W/attach/C3_Round_sheet.cells.json --outdir $W/work/C3_a1
  $KC cells --in $W/chatgpt/C4_a1.png --def $W/attach/C4_Outlines_Seats_sheet.cells.json --outdir $W/work/C4_a1
  $KC cells --in $W/chatgpt/C5_a1.png --def $W/attach/C5_Pills_sheet.cells.json --outdir $W/work/C5_a1
  $KC cells --in $W/chatgpt/C6_a1.png --def $W/attach/C6_Toggles_sheet.cells.json --outdir $W/work/C6_a1
  $KC cells --in $W/chatgpt/C7_a1.png --def $W/attach/C7_Buttons_sheet.cells.json --outdir $W/work/C7_a1
}

# export <work file> <sprite name without .png>
export_sprite() { $KC export --in "$1" --name "$OUT/$2.png"; }

# ---------------------------------------------------------------------------------------------
# UI_Toggle_On / UI_Toggle_Off (11-settings, also 13-match-menu). 190 x 108, both states, from
# sheet C6 (second attempt: the knob casts a soft shadow on the track, as on the mockup).
# Mockup (three ON switches): crimson pill 79.7 x 43.8 with a bright rim about 1.5 px wide, knob
# concentric with the end cap; the knob radius that fits all three is 16.75 px (its edge is soft).
# Sprite: pill box 3.5..186.5 x 3.5..104.5 (caps r 50.5), knob r 38.5 at 136,54 (on) / 54,54 (off).
# The off state has no rim, so its grey pill keeps the same 3.5 px clear margin: both states
# go into the same rectangle.
toggle() {
  local d=$W/work/Toggle; mkdir -p $d
  # ON cell: rim 0.5..7 | crimson 7..562.7 (caps r 149.45) | rim ..569.3; rows 0.5 / 7.3 / 306.2 / 313.5.
  # Knob centre 413,158, r 118.76 -> 113.94 (38.5 sprite px at this cell's scale, 0.3379); it sits
  # 0.25 px left of and 1.25 px below the cap's centre and is moved onto it.
  $KC rwarp --in $W/work/C6_a1/Toggle_On.png --out $d/on_0_knob.png --keep --center 413,158 \
      --map "118.76>113.94;135>135;160>160" --shift 0.25,-1.25 --shiftr 132,146
  # The pill is 2.6% too long: only the plain stretch between the left cap and the knob's cap is squeezed.
  $KC warp --in $d/on_0_knob.png --out $d/on_1_warp.png --size 190,108 \
      --mapx "0.5>0;7>3.5;156.45>54;263.8>85.5;562.7>186.5;569.3>190" --mapy "0.5>0;7.3>3.5;306.2>104.5;313.5>108"
  $KC shape --in $d/on_1_warp.png --out $d/on_2_shape.png --type pill
  $KC crimson --in $d/on_2_shape.png --out $d/on_3_color.png --ramp "120:#65060E;134:#750615;147:#840517;157:#8D0719;161:#920819;170:#970818"
  # OFF cell: grey pill 0.4..563.8 x 0.7..308.6 (caps r 153.95, left cap centre 154.35,154.65).
  # Knob centre 150.2,157, r 119.1 -> 117.8; moved onto the cap's centre (+4.15, -2.35).
  $KC rwarp --in $W/work/C6_a1/Toggle_Off.png --out $d/off_0_knob.png --keep --center 150.2,157 \
      --map "119.1>117.8;135>135;160>160" --shift 4.15,-2.35 --shiftr 133,149
  $KC warp --in $d/off_0_knob.png --out $d/off_1_warp.png --size 190,108 \
      --mapx "0.4>3.5;154.35>54;563.8>186.5" --mapy "0.7>3.5;308.6>104.5"
  $KC shape --in $d/off_1_warp.png --out $d/off_2_shape.png --type pill --rect 3.5,3.5,183,101
  # Grey #A9A9A7 -> #B4B1AE; ChatGPT's shadow under the knob is four times as deep as the mockup's
  # (57 levels against 15 at its darkest), so the dark end of the curve is pulled up.
  $KC curve --in $d/off_2_shape.png --out $d/off_3_color.png --anchors "#70716F>#A5A29F;#A9A9A7>#B4B1AE;#FEFEFD>#FEFEFE"
  export_sprite $d/on_3_color.png UI_Toggle_On
  export_sprite $d/off_3_color.png UI_Toggle_Off
}

# ---------------------------------------------------------------------------------------------
# UI_Slider_Track 64 x 44, UI_Slider_Fill 64 x 50, UI_Slider_Knob 76 x 76 (11-settings).
# Mockup: track 18.5 px high, beige #D9C9BB, no rim; fill crimson 18.8 px high inside a bright rim
# (21.4 in all), lighter at the top; knob a white disc, 32.1 px (its shadow is a separate sprite).
# Track and fill are sliced left and right only: the game scales them to the height of the node.
slider() {
  local d=$W/work/Slider; mkdir -p $d
  local C=$W/work/C1_a1/Slider.png
  # Fill: the left end of the cell (rim 1..3.5, crimson from 3.5, cap r 40.5; rows 33 / 36.5 / 117.5 / 121.5), mirrored.
  $KC crop --in $C --out $d/fill_0_crop.png --box 0,30,140,95
  $KC warp --in $d/fill_0_crop.png --out $d/fill_1_warp.png --size 64,50 --mapx "1>0;3.5>3;44>25;70>32;140>64" --mapy "3>0;6.5>3;87.5>47;91.5>50"
  $KC mirror --in $d/fill_1_warp.png --out $d/fill_2_mirror.png --axis h --from left
  $KC shape --in $d/fill_2_mirror.png --out $d/fill_3_shape.png --type pill
  $KC crimson --in $d/fill_3_shape.png --out $d/fill_4_color.png --ramp "139:#780619;148:#7F061B;159:#85061D"
  $KC nine --in $d/fill_4_color.png --out $d/fill_5_nine.png --border 26,0,26,0 --mid 12,32
  # Track: the right end of the cell (rows 34.5..119.5, right end 657, cap r 42.5), mirrored.
  $KC crop --in $C --out $d/track_0_crop.png --box 520,30,140,95
  $KC warp --in $d/track_0_crop.png --out $d/track_1_warp.png --size 64,44 --mapx "0>0;70>32;94.5>42;137>64" --mapy "4.5>0;89.5>44"
  $KC mirror --in $d/track_1_warp.png --out $d/track_2_mirror.png --axis h --from right
  $KC shape --in $d/track_2_mirror.png --out $d/track_3_shape.png --type pill
  $KC curve --in $d/track_3_shape.png --out $d/track_4_color.png --anchors "#E1CEBE>#D9C9BB"
  $KC nine --in $d/track_4_color.png --out $d/track_5_nine.png --border 22,0,22,0 --mid 20,32
  # Knob: centre 386.7,76.7 r 73.83 in the cell.
  $KC rwarp --in $C --out $d/knob_1.png --center 386.7,76.7 --size 76 --map "73.83>38;80>41.2"
  $KC shape --in $d/knob_1.png --out $d/knob_2_shape.png --type ellipse
  export_sprite $d/track_5_nine.png UI_Slider_Track
  export_sprite $d/fill_5_nine.png UI_Slider_Fill
  export_sprite $d/knob_2_shape.png UI_Slider_Knob
}

# ---------------------------------------------------------------------------------------------
# UI_Dropdown 408 x 126 (11-settings): ivory box, thin brown outline, the chevron in its right end.
# Mockup: outline outer edge 176.8 x 54.4 at 1067.61,362.73; best fit corner radius 12, outline
# 1.3 px (#A69380), fill #F8F1E9; chevron ink box 18.2 x 11.4 centred 25.2 px from the right edge.
# Cell: outline box 5,2.5..597.5,187, outline 6.4 px, corner r 40, chevron zone 466..550 x 66..127.
dropdown() {
  local d=$W/work/Dropdown; mkdir -p $d
  $KC frame --in $W/work/C1_a1/Dropdown.png --out $d/dd_1_frame.png --src 5,2.5,597.5,187 --srcline 6.4 --srcradius 40 \
      --size 408,126 --line 3.0 --radius 27.69 --zonex "466,550>321.83,378.11" --zoney "66,127>43.33,84.2" --linecolor "#AF8A6F"
  $KC curve --in $d/dd_1_frame.png --out $d/dd_2_color.png --anchors "#151D1F>#0F1416;#AF8A6F>#A69380;#FDF8F1>#F8F1E9"
  # The chevron is 12% lighter in weight than the mockup's: every edge out by 0.65 px (best fit).
  $KC thicken --in $d/dd_2_color.png --out $d/dd_3_chevron.png --rect 314,36,72,54 --grow 0.65 --sigma 1.3
  $KC nine --in $d/dd_3_chevron.png --out $d/dd_4_nine.png --border 30,0,98,0 --mid 280,32
  export_sprite $d/dd_4_nine.png UI_Dropdown
}

# ---------------------------------------------------------------------------------------------
# UI_Field 160 x 140 (02-create-room) and UI_Field_Light (14-profile): one frame, two fills.
# Both mockups fit best with corner radius 13.5-14 and an outline of 1.3-1.5 px; they differ in
# colour: 02 fill #F7EDE2 outline #A79588, 14 fill #FCF7EE outline #A8907B.
# Cells: 02 outline box 1.3,0.55..658.3,196.95 (outline 3.5, corner r 39.5);
#        14 outline box 1.35,1.5..651.9,181.8 (outline 3.6, corner r 39).
field() {
  local d=$W/work/Field; mkdir -p $d
  $KC frame --in $W/work/C2_a1/Field_02.png --out $d/f02_1_frame.png --src 1.3,0.55,658.3,196.95 --srcline 3.5 --srcradius 39.5 \
      --size 160,140 --line 3.22 --radius 31.05 --linecolor "#A79588"
  $KC curve --in $d/f02_1_frame.png --out $d/f02_2_color.png --anchors "#A79588>#A79588;#FBF4E7>#F7EDE2;#FEFEFE>#FFF9F0"
  $KC nine --in $d/f02_2_color.png --out $d/f02_3_nine.png --border 36,36,36,36 --mid 88,68
  $KC frame --in $W/work/C2_a1/Field_14.png --out $d/f14_1_frame.png --src 1.35,1.5,651.9,181.8 --srcline 3.6 --srcradius 39 \
      --size 160,140 --line 3.22 --radius 31.05 --linecolor "#A8907B"
  $KC curve --in $d/f14_1_frame.png --out $d/f14_2_color.png --anchors "#A8907B>#A8907B;#FEFBF3>#FCF7EE;#FFFFFF>#FFFDF8"
  $KC nine --in $d/f14_2_color.png --out $d/f14_3_nine.png --border 36,36,36,36 --mid 88,68
  export_sprite $d/f02_3_nine.png UI_Field
  export_sprite $d/f14_3_nine.png UI_Field_Light
}

# ---------------------------------------------------------------------------------------------
# UI_CodeBox_Normal / _Focus / _Error 250 x 252 (03-join-room).
# Mockup (box 2): outline outer edge 108.45 x 109.44 at 719.38,482.35; best fit corner radius 15,
# outline 2.2 px (#987455); fill #FAF2E9 at the top to #F7EDE2 at the bottom.
# Cell: outline box 0.85,0.4..295,293.9, outline 4.2 px, corner r 40.5.
# Focus and error are the same box with another outline (GDD 12: focus = gold outline; errors are
# Heading Burgundy), a third heavier and grown inward, so all three line up to the pixel.
codebox() {
  local d=$W/work/CodeBox; mkdir -p $d
  local fill="#F4ECDD>#F7EDE2;#FCF6EC>#FAF2E9;#FFFFFF>#FFFBF5"
  $KC frame --in $W/work/C2_a1/CodeBox.png --out $d/cb_1_frame.png --src 0.85,0.4,295.0,293.9 --srcline 4.2 --srcradius 40.5 \
      --size 250,252 --line 5.06 --radius 34.5 --linecolor "#987455"
  $KC curve --in $d/cb_1_frame.png --out $d/cb_2_color.png --anchors "#987455>#987455;$fill"
  $KC frame --in $W/work/C2_a1/CodeBox.png --out $d/cbf_1_frame.png --src 0.85,0.4,295.0,293.9 --srcline 4.2 --srcradius 40.5 \
      --size 250,252 --line 6.9 --radius 34.5 --linecolor "#B8914F"
  $KC curve --in $d/cbf_1_frame.png --out $d/cbf_2_color.png --anchors "#B8914F>#B8914F;$fill"
  $KC frame --in $W/work/C2_a1/CodeBox.png --out $d/cbe_1_frame.png --src 0.85,0.4,295.0,293.9 --srcline 4.2 --srcradius 40.5 \
      --size 250,252 --line 6.9 --radius 34.5 --linecolor "#7A1016"
  $KC curve --in $d/cbe_1_frame.png --out $d/cbe_2_color.png --anchors "#7A1016>#7A1016;$fill"
  export_sprite $d/cb_2_color.png UI_CodeBox_Normal
  export_sprite $d/cbf_2_color.png UI_CodeBox_Focus
  export_sprite $d/cbe_2_color.png UI_CodeBox_Error
}

# ---------------------------------------------------------------------------------------------
# UI_Segment_Track 160 x 138, UI_Segment_Selected 132 x 120 (02-create-room, 15 sec | 30 sec).
# Mockup: track outline outer edge 468.97 x 59.4 at 602.15,634.26 (tan #C9B5A4, inner bevel),
# fill #FBF2E9 at the top to #F5EADF at the bottom; selected pill crimson 231.4 x 49.2 at
# 834.1,639.5 (#92141A at the top to #700F13 at the bottom) inside a bright rim 1.5 px wide that
# fades towards the bottom. Both are sliced left and right only.
# Cell: track box 0.6,0.6..724.9,249.7 (outline 3.9); pill 303.5,20.5..702.7,231.5 with a highlight
# ring 1.5 px wide around it.
segment() {
  local d=$W/work/Segment; mkdir -p $d
  local C=$W/work/C2_a1/Segment.png
  # Track: the left end (no pill there), mirrored; the plain stretch 130..290 is pinned so that the
  # left half of the sprite holds nothing of the crimson pill.
  $KC frame --in $C --out $d/track_1_frame.png --src 0.6,0.6,724.9,249.7 --srcline 3.9 --srcradius 124.55 \
      --size 160,138 --line 3.45 --radius 69 --shape pill --linecolor "#C9B5A4" --zonex "130,290>71,81"
  $KC mirror --in $d/track_1_frame.png --out $d/track_2_mirror.png --axis h --from left
  $KC curve --in $d/track_2_mirror.png --out $d/track_3_color.png --anchors "#C9B5A4>#C9B5A4;#DAC5B0>#E1D2C4;#F6EDE0>#F5EADF;#F8F1E6>#F7EDE3;#FCF7EE>#FBF2E9;#FFFFFF>#FFFBF6"
  $KC nine --in $d/track_3_color.png --out $d/track_4_nine.png --border 70,0,70,0 --mid 20,32
  $KC frame --in $C --out $d/sel_1_frame.png --src 302,19,704.3,233 --srcline 1.5 --srcradius 107 \
      --size 132,120 --line 3.4 --radius 60 --shape pill --linecolor "#FDFBF8" --linecolor2 "#F3E9DF"
  $KC crimson --in $d/sel_1_frame.png --out $d/sel_2_color.png --gc 20 --ramp "132:#700F13;143:#790F15;156:#841117;167:#8D1318;171:#92141A" --white "#FDFBF8"
  $KC nine --in $d/sel_2_color.png --out $d/sel_3_nine.png --border 61,0,61,0 --mid 10,32
  export_sprite $d/track_4_nine.png UI_Segment_Track
  export_sprite $d/sel_3_nine.png UI_Segment_Selected
}

# ---------------------------------------------------------------------------------------------
# UI_Radio_On / UI_Radio_Off 98 x 98 (15-card-styles).
# Mockup: brown ring between r 16.6 and 19.9 (selected #604B39, empty #776250), white gap
# #F9F7F6, crimson dot r 12.5 #880711; the empty one is filled #FBF4EC. Around the ring runs the
# thin near-white keyline that every dark element of these mockups has (to r 21.2, #FDFAF5).
# Cells: selected centre 154.0,153.3, edges at 90.45 / 120.9 / 144.95 / 152.97; empty centre
# 156.7,153.4, edges at 120.5 / 144.44 / 152.31. Flat colours: the rings are averaged all the way round.
radio() {
  local d=$W/work/Radio; mkdir -p $d
  $KC rwarp --in $W/work/C3_a1/RadioOn.png --out $d/on_final.png --center 154.0,153.3 --size 98 --avg \
      --map "90.45>28.89;120.9>38.37;144.95>45.99;152.97>49;160>53" \
      --colors "0-86:#880711;95-117:#F9F7F6;125-141:#604B39;148.5-152:#FDFAF5" --outer 49
  $KC rwarp --in $W/work/C3_a1/RadioOff.png --out $d/off_final.png --center 156.7,153.4 --size 98 --avg \
      --map "120.5>38.37;144.44>45.99;152.31>49;160>53" \
      --colors "0-116:#FBF4EC;125-141:#776250;148-151.5:#FDFAF5" --outer 49
  export_sprite $d/on_final.png UI_Radio_On
  export_sprite $d/off_final.png UI_Radio_Off
}

# ---------------------------------------------------------------------------------------------
# The round ones of sheet C3.
# UI_ReadyDot 74 x 74 (04-private-lobby): green disc r 11.6 #2A762F in a white ring to r 15.96 #FBFBFA.
# UI_Ring_Spinner 66 x 66 (busy state of the primary buttons; not on a mockup): ring r 9.5..14 #FEFAF6.
# UI_Disc_Number 142 x 142 (12-how-to-play): satin crimson disc r 29.45 inside a thin near-white
#   keyline (to r 30.75, #FDFBF6).
# UI_Check_Badge 96 x 96 (06-offline-setup): satin crimson disc r 19.7 with the white check, inside
#   the same keyline (to r 21, #FDFBF8).
round() {
  local d=$W/work/Round; mkdir -p $d
  $KC rwarp --in $W/work/C3_a1/ReadyDot.png --out $d/dot_final.png --center 123.9,123.7 --size 74 --avg \
      --map "91.28>26.89;120.43>37;126>40" --colors "0-87:#2A762F;96-118:#FBFBFA" --outer 37
  $KC rwarp --in $W/work/C3_a1/SpinnerRing.png --out $d/spinner_final.png --center 126.6,127.2 --size 66 --avg \
      --map "88.76>22.39;125.93>33;130>36" --colors "93-122:#FEFAF6" --outer 33
  # Disc: cell centre 155.4,156.8, crimson to r 147.35, white keyline to r 154.77.
  $KC rwarp --in $W/work/C3_a1/RuleDisc.png --out $d/disc_1.png --center 155.4,156.8 --size 142 --map "147.35>68;154.77>71.6" --outer 71
  $KC crimson --in $d/disc_1.png --out $d/disc_2_color.png --gc 9 --ramp "140:#750B13;144:#7A0C14;148:#810D15;152:#880D15;156:#8E0E16" --white "#FDFDFD" --whiteto "#FDFBF6"
  # Badge: cell centre 148.1,149.1, crimson to r 139.49, white keyline to r 147.4. The check is
  # 0.7 sprite px heavier all round than the mockup's (best fit), so it is thinned by that much.
  $KC rwarp --in $W/work/C3_a1/CheckBadge.png --out $d/badge_1.png --center 148.1,149.1 --size 96 --map "139.49>45;147.4>48.2" --outer 48
  $KC crimson --in $d/badge_1.png --out $d/badge_2_color.png --gc 7 --ramp "140:#80040D;144:#84050E;148:#88060F" --white "#FDFDFD" --whiteto "#FDFBF8"
  $KC thicken --in $d/badge_2_color.png --out $d/badge_3_check.png --rect 20,24,58,50 --grow -0.7 --sigma 1.3 --ink light
  export_sprite $d/dot_final.png UI_ReadyDot
  export_sprite $d/spinner_final.png UI_Ring_Spinner
  export_sprite $d/disc_2_color.png UI_Disc_Number
  export_sprite $d/badge_3_check.png UI_Check_Badge
}

# ---------------------------------------------------------------------------------------------
# UI_Outline_Selected (06-offline-setup) and UI_Outline_Thin_Selected (15-card-styles), 144 x 144,
# hollow, sliced on all four sides. The two mockups draw the same crimson line at two weights:
# 06: 3.2 px, corner radius 24, #820001; 15: 2.6 px, corner radius 26, #800405 (kc fit sweeps).
# A plain line: ChatGPT's cells give the shape (a rounded line, nothing else); weight, radius and
# colour are set to the measured values.
outline() {
  local d=$W/work/Outline; mkdir -p $d
  $KC frame --in $W/work/C4_a1/Outline_06.png --out $d/o06.png --src 1,0,312,304 --srcline 14 --srcradius 93 \
      --size 144,144 --line 7.36 --radius 55.2 --linecolor "#820001" --hollow
  $KC frame --in $W/work/C4_a1/Outline_15.png --out $d/o15.png --src 0,0,297,294 --srcline 8 --srcradius 105 \
      --size 144,144 --line 5.98 --radius 59.8 --linecolor "#800405" --hollow
  export_sprite $d/o06.png UI_Outline_Selected
  export_sprite $d/o15.png UI_Outline_Thin_Selected
}

# ---------------------------------------------------------------------------------------------
# UI_Seat_Filled 274 x 274 and UI_Seat_Searching 272 x 272 (05-matchmaking).
# Filled, mockup: black silhouette #1A1B1A on a pale disc that runs from #F2E8DE at the top to
# #E5D8CC at the bottom, a white ring (r 52.5..56.6, #FAF3EB) and a thin tan ring (r 57.15..59.25,
# #CAAC92). The mockup's seat is 1.5% wider than high; the sprite is round.
# Cell: centre 183.05,179.4; disc to r 159, white ring to 171.6, gold ring to 180. ChatGPT drew the
# gold ring three times as heavy and more saturated: both rings are redrawn at the measured radii.
# Its silhouette sits within 0.9 px of the mockup's; the body is 3% short (rows 141.5..265.5 ->
# 139.9..266.9), which also closes the hairline between head and shoulders as on the mockup.
# Searching, mockup: the same silhouette in #D5CAC0 on nothing, inside a dashed circle of 36
# dashes (6.4 of every 10 degrees), 1.5 px wide, #A0978E, 3% wider than high.
# Cell: centre 181.6,178.8, silhouette scaled by 0.7626, dashes at r 175.5 (ChatGPT drew 32).
seat() {
  local d=$W/work/Seat; mkdir -p $d
  $KC rwarp --in $W/work/C4_a1/SeatFilled.png --out $d/filled_1.png --center 183.05,179.4 --size 274 \
      --map "159>121.4;171.6>130.9;174.45>132;180>137;186>141" --outer 137
  $KC warp --in $d/filled_1.png --out $d/filled_2_body.png --mapy "139>139;141.5>139.9;265.5>266.9"
  $KC twotone --in $d/filled_2_body.png --out $d/filled_3_tone.png --a "#171815" --b "#E9E2D8" --to1 "#1A1B1A" \
      --to2 "0:#F2E8DE;0.5:#EADED3;1:#E5D8CC" --radius 124
  $KC annulus --in $d/filled_3_tone.png --out $d/filled_4_white.png --r0 121.4 --r1 131.5 --color "#FAF3EB"
  $KC annulus --in $d/filled_4_white.png --out $d/filled_5_gold.png --r0 132.15 --r1 140 --color "#CAAC92"
  $KC shape --in $d/filled_5_gold.png --out $d/filled_6_shape.png --type ellipse
  $KC rwarp --in $W/work/C4_a1/SeatSearching.png --out $d/search_1.png --center 181.6,178.8 --size 272 \
      --map "165>125.83;175.5>134.27" --outer 126
  $KC fill --in $d/search_1.png --out $d/search_2_color.png --color "#D5CAC0"
  $KC dashes --in $d/search_2_color.png --out $d/search_3_dashes.png --count 36 --dash 6.4 --start 0.8 \
      --r0 132.55 --r1 136 --color "#A0978E" --caps butt
  export_sprite $d/filled_6_shape.png UI_Seat_Filled
  export_sprite $d/search_3_dashes.png UI_Seat_Searching
}

# ---------------------------------------------------------------------------------------------
# UI_Pill_RoomCode 160 x 178 (04-private-lobby), sliced on all four sides: ivory #F8F0E7, corner
# radius 27, a 2 px outline that runs from #DDD0C4 at the top to #CDBBAB at the bottom (the
# mockup's bevel). ChatGPT returned it with a gold outline: the outline is redrawn in the
# mockup's colours. Cell: outline box 1,0.8..979.4,205.4, outline 6.2, corner r 69.
# UI_Pill_Highlight 120 x 104 (12-how-to-play), sliced left and right: pale #F6E4D2 pill with a
# 2.1 px gold outline, #D2A05A at the top to #C6924A at the bottom. Cell: box 0.8,1.2..1420.4,134.4.
pill() {
  local d=$W/work/Pill; mkdir -p $d
  $KC frame --in $W/work/C5_a1/Pill_RoomCode.png --out $d/room_1_frame.png --src 1,0.8,979.4,205.4 --srcline 6.2 --srcradius 69 \
      --size 160,178 --line 4.6 --radius 62.1 --linecolor "#DDD0C4" --linecolor2 "#CDBBAB"
  $KC curve --in $d/room_1_frame.png --out $d/room_2_color.png --anchors "#CDBBAB>#CDBBAB;#DDD0C4>#DDD0C4;#FDF8EA>#F8F0E7;#FFFFFF>#FFFAF3"
  $KC nine --in $d/room_2_color.png --out $d/room_3_nine.png --border 68,68,68,68 --mid 24,42
  $KC frame --in $W/work/C5_a1/Pill_Highlight.png --out $d/hl_1_frame.png --src 0.8,1.2,1420.4,134.4 --srcline 6.3 --srcradius 66.6 \
      --size 120,104 --line 4.85 --radius 52 --shape pill --linecolor "#D2A05A" --linecolor2 "#C6924A"
  $KC curve --in $d/hl_1_frame.png --out $d/hl_2_color.png --anchors "#C6924A>#C6924A;#D2A05A>#D2A05A;#FDF6E5>#F6E4D2;#FFFFFF>#FCF0E2"
  $KC nine --in $d/hl_2_color.png --out $d/hl_3_nine.png --border 53,0,53,0 --mid 14,32
  export_sprite $d/room_3_nine.png UI_Pill_RoomCode
  export_sprite $d/hl_3_nine.png UI_Pill_Highlight
}

# ---------------------------------------------------------------------------------------------
# The crimson call-to-action buttons of the inner screens and the chevron on them (sheet C7).
# UI_Button_Crimson 336 x 220, sliced left and right (160,0,160,0): the plain crimson button of
#   01, 04, 06, 07, 09, 10, 11, 12, 14, 15, 16 (no gold rim; soft top-to-bottom crimson; a woven
#   cluster at each end). Drawn from 06's Start Game (the widest). Mockups (kc edges): corner
#   radius / height 0.32..0.48 by screen, 0.385 in the sprite (the median); the cluster reaches
#   0.68 of the height from each end (mockups 0.56..0.76). Colours (kc vprof, kc motif on 06): the
#   plain crimson row by row; the cluster's light diamonds at log2 ratio +0.83/+1.55/+0.36 (R/G/B,
#   95th percentile) over the row, its lattice and crosses on a ground darker by -0.70/-0.63/-0.77
#   at the dark end: ChatGPT drew the diamonds 2.5x too light and left the ground the button's own
#   colour. The top and bottom hairlines come with the row colours.
# UI_Button_Crimson_Shadow 408 x 292 (196,0,196,0): the button's shape blurred (sigma 11 sprite px,
#   0.05 of the height), spread 36; drop and opacity per node (kc bshadow per screen, in the map).
# UI_Button_Crimson_Box 208 x 180 (96,0,96,0): the Game Menu's crimson box (13, Resume Game): outer
#   box [612.2, 358.61, 447.7, 78.12] (corner radius 20.8), a dark edge 2.4 px (2.7 at the bottom)
#   whose inner px is a lighter red, a two-tone keyline 2 px (cream-gold at the top, orange down the
#   sides, gold at the bottom; the inner half lighter), a 1 px orange-red fringe inside it, the
#   crimson face row by row and the line-drawn knot at each end (+1.04/+3.84/+1.99 over the face).
#   Every band is drawn at its measured place and colour (kc line on the mockup, four sides).
# UI_Button_Crimson_Box_Shadow 268 x 240 (126,0,126,0): sigma 9.2 (0.051 of the height), spread 30.
# Icon_Chevron_Thin 38 x 64 (UI/Icons): ChatGPT's chevron at the mockups' weight (stroke 0.135 of
#   the height, 45 degree arms, square ends; the kit's Icon_Chevron_Light is 0.178), in #FEFEFE.
button() {
  local d=$W/work/Button; mkdir -p $d
  # Pill: cell 1278 x 371 (the button 1:1 in its cell); the left end (cluster to column 262) and
  # the plain columns next to it; mirrored, so the two ends are the same drawing.
  $KC warp --in $W/work/C7_a1/Button_Crimson.png --out $d/pill_1_warp.png --size 336,220 \
      --mapx "0>0;269.8>160;330>168;1278>336" --mapy "0>0;371>220"
  $KC mirror --in $d/pill_1_warp.png --out $d/pill_2_mirror.png --axis h --from left
  $KC retone --in $d/pill_2_mirror.png --out $d/pill_3_tone.png --plain 161,175 --screen 06 --col 520,700 --rows 783,93 \
      --from "-0.21,1.33" --light "0.83,1.55,0.36" --dark "-0.70,-0.63,-0.77" --core 0.02 --ground 0.7 --close 12 --soft 2
  $KC shape --in $d/pill_3_tone.png --out $d/pill_4_shape.png --type roundrect --radius 84.7
  $KC nine --in $d/pill_4_shape.png --out $d/pill_5_nine.png --border 160,0,160,0 --mid 16,32
  $KC mkshadow --size 336,220 --radius 84.7 --sigma 11 --spread 36 --out $d/pill_shadow.png
  # Box: cell 627 x 314; the left end (knot to column 156) and the plain columns next to it.
  $KC warp --in $W/work/C7_a1/Button_Crimson_Box.png --out $d/box_1_warp.png --size 208,180 \
      --mapx "0>0;167.5>96;200>104;627>208" --mapy "0>0;314>180"
  $KC mirror --in $d/box_1_warp.png --out $d/box_2_mirror.png --axis h --from left
  $KC retone --in $d/box_2_mirror.png --out $d/box_3_tone.png --plain 97,111 --screen 13 --col 676,714 --rows 358.61,78.12 \
      --rowclamp 364.5,430.5 --from "-1,1.14" --light "1.04,3.84,1.99" --dark "-0.3,-0.3,-0.3" --core 0.03 --tlimit "-0.3,1.12" \
      --radius 48 --edge0 13.5 --edge 15.5
  # The bands, outside in (insets left,top,right,bottom and corner radius, sprite px = mockup px x 2.304).
  $KC ring --in $d/box_3_tone.png --out $d/box_4a.png --outer 0,0,0,0,48 --inner 4.26,3.20,4.26,3.99,44.2 \
      --stops "0:#8D0309;0.2:#71050A;0.5:#6B0302;0.66:#630101;0.8:#560001;1:#650906"
  $KC ring --in $d/box_4a.png --out $d/box_4b.png --outer 4.26,3.20,4.26,3.99,44.2 --inner 6.57,5.51,6.57,6.29,41.9 \
      --stops "0:#A80C0C;0.2:#8A0607;0.34:#A01D13;0.5:#9D2110;0.66:#991D11;0.8:#7C0805;1:#8A0902"
  $KC ring --in $d/box_4b.png --out $d/box_4c.png --outer 6.57,5.51,6.57,6.29,41.9 --inner 8.87,7.81,8.87,8.59,39.6 \
      --stops "0:#F6B989;0.15:#F9A777;0.34:#F89766;0.5:#F88050;0.66:#F77E4C;0.85:#E06E3E;1:#CB5E30"
  $KC ring --in $d/box_4c.png --out $d/box_4d.png --outer 8.87,7.81,8.87,8.59,39.6 --inner 11.17,10.11,11.17,10.90,37.3 \
      --stops "0:#FDE7C2;0.21:#FBAE81;0.34:#EC845A;0.5:#ED7046;0.66:#E66B42;0.79:#F38B55;1:#F9C291"
  $KC ring --in $d/box_4d.png --out $d/box_4e.png --outer 11.17,10.11,11.17,10.90,37.3 --inner 13.48,12.42,13.48,13.20,35.0 \
      --stops "0:#CF3028;0.34:#A41A14;0.5:#A42316;0.66:#8E100C;1:#922418"
  $KC shape --in $d/box_4e.png --out $d/box_5_shape.png --type roundrect --radius 48
  $KC nine --in $d/box_5_shape.png --out $d/box_6_nine.png --border 96,0,96,0 --mid 16,32
  $KC mkshadow --size 208,180 --radius 48 --sigma 9.2 --spread 30 --out $d/box_shadow.png
  # Chevron: cell ink box 0.82..130 x 0.97..217.84.
  $KC warp --in $W/work/C7_a1/Chevron_Thin.png --out $d/chev_1_warp.png --size 38,64 --mapx "0.82>0;130>38" --mapy "0.97>0;217.84>64"
  $KC fill --in $d/chev_1_warp.png --out $d/chev_2_color.png --color "#FEFEFE"
  export_sprite $d/pill_5_nine.png UI_Button_Crimson
  export_sprite $d/pill_shadow.png UI_Button_Crimson_Shadow
  export_sprite $d/box_6_nine.png UI_Button_Crimson_Box
  export_sprite $d/box_shadow.png UI_Button_Crimson_Box_Shadow
  $KC export --in $d/chev_2_color.png --name UI/Icons/Icon_Chevron_Thin.png
}

case "${1:-}" in
  cells) cells ;;
  all) cells; toggle; slider; dropdown; field; codebox; segment; radio; round; outline; seat; pill; button ;;
  "") echo "usage: make.sh cells | all | <asset>"; exit 2 ;;
  *) "$1" ;;
esac
