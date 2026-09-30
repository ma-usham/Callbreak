#!/usr/bin/env bash
# Moves a ChatGPT download into the Panels working folder, under the shared download lock
# (other agents download into the same folder at the same time).
#   dl.sh begin                 take the lock and remember what is in Downloads
#   (click the download arrow in the ChatGPT lightbox)
#   dl.sh end <Asset>_aN        wait for the new "ChatGPT Image*.png", move it to
#                               ArtSource/Working/Kit/Panels/chatgpt/<Asset>_aN.png, release the lock
#   dl.sh abort                 release the lock without moving anything
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../../.." && pwd)"
lock="$repo/ArtSource/Working/_download.lock"
snap="$repo/Temp/KitPanels.downloads.before"
down="/c/Users/maush/Downloads"
dest="$repo/ArtSource/Working/Kit/Panels/chatgpt"
mkdir -p "$repo/Temp" "$dest"

list() { ls -1 "$down" 2>/dev/null | grep -E '^ChatGPT Image.*\.png$' | sort; }

case "${1:-}" in
  begin)
    waited=0
    until mkdir "$lock" 2>/dev/null; do
      # A lock older than three minutes belongs to a run that died.
      if [ -d "$lock" ] && [ $(( $(date +%s) - $(stat -c %Y "$lock" 2>/dev/null || echo 0) )) -gt 180 ]; then rmdir "$lock" 2>/dev/null; continue; fi
      if [ $waited -ge 120 ]; then echo "gave up waiting for the download lock"; exit 5; fi
      sleep 2; waited=$((waited + 2))
    done
    echo "panels $$ $(date +%H:%M:%S)" > "$lock/owner" 2>/dev/null
    list > "$snap"
    echo "lock taken (waited ${waited}s); $(wc -l < "$snap") ChatGPT images already in Downloads"
    ;;
  end)
    name="${2:?asset name, e.g. UI_Panel_Large_a1}"
    new=""
    for i in $(seq 1 45); do
      new="$(comm -13 "$snap" <(list) | tail -1)"
      if [ -n "$new" ] && ! ls "$down" | grep -q '\.crdownload$'; then break; fi
      new=""; sleep 1
    done
    if [ -z "$new" ]; then rm -f "$lock/owner"; rmdir "$lock" 2>/dev/null; echo "no new download appeared"; exit 4; fi
    sleep 1
    mv "$down/$new" "$dest/$name.png" && echo "moved '$new' -> ArtSource/Working/Kit/Panels/chatgpt/$name.png ($(stat -c %s "$dest/$name.png") bytes)"
    rm -f "$lock/owner"; rmdir "$lock" 2>/dev/null
    ;;
  abort)
    rm -f "$lock/owner"; rmdir "$lock" 2>/dev/null; echo "lock released"
    ;;
  *) echo "usage: dl.sh begin | end <name> | abort"; exit 2 ;;
esac
