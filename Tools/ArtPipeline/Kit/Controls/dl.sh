#!/usr/bin/env bash
# Moves a ChatGPT download into the Controls working folder, under the shared download lock
# (other agents download into the same Downloads folder at the same time; see
# Docs/Tasks/UI-Art-ChatGPT-Brief.md). The lock is the plain directory the brief describes
# (mkdir / rmdir); that this run holds it is remembered in Temp/, not inside the lock.
#   dl.sh begin                 take the lock and remember what is in Downloads
#   (click the download arrow in the ChatGPT lightbox)
#   dl.sh end <Asset>_aN        wait for the new "ChatGPT Image*.png", move it to
#                               ArtSource/Working/Kit/Controls/chatgpt/<Asset>_aN.png, release the lock
#   dl.sh abort                 release the lock without moving anything
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../../.." && pwd)"
lock="$repo/ArtSource/Working/_download.lock"
snap="$repo/Temp/KitControls.downloads.before"
mine="$repo/Temp/KitControls.lock.mine"
down="/c/Users/maush/Downloads"
dest="$repo/ArtSource/Working/Kit/Controls/chatgpt"
mkdir -p "$repo/Temp" "$dest"

list() { ls -1 "$down" 2>/dev/null | grep -E '^ChatGPT Image.*\.png$' | sort; }
release() { if [ -f "$mine" ]; then rm -f "$mine"; rmdir "$lock" 2>/dev/null; fi; }

case "${1:-}" in
  begin)
    waited=0
    until mkdir "$lock" 2>/dev/null; do
      # A lock older than three minutes belongs to a run that died (the Panels script leaves an
      # "owner" note inside its lock; remove that too, or the directory cannot go).
      if [ -d "$lock" ] && [ $(( $(date +%s) - $(stat -c %Y "$lock" 2>/dev/null || echo 0) )) -gt 180 ]; then rm -f "$lock/owner"; rmdir "$lock" 2>/dev/null; continue; fi
      if [ $waited -ge 120 ]; then echo "gave up waiting for the download lock"; exit 5; fi
      sleep 2; waited=$((waited + 2))
    done
    date +%s > "$mine"
    list > "$snap"
    echo "lock taken (waited ${waited}s); $(wc -l < "$snap") ChatGPT images already in Downloads"
    ;;
  end)
    name="${2:?asset name, e.g. C1_a1}"
    if [ ! -f "$mine" ]; then echo "this run does not hold the download lock (dl.sh begin first)"; exit 3; fi
    new=""
    for i in $(seq 1 45); do
      new="$(comm -13 "$snap" <(list) | tail -1)"
      if [ -n "$new" ] && ! ls "$down" | grep -q '\.crdownload$'; then break; fi
      new=""; sleep 1
    done
    if [ -z "$new" ]; then release; echo "no new download appeared"; exit 4; fi
    sleep 1
    mv "$down/$new" "$dest/$name.png" && echo "moved '$new' -> ArtSource/Working/Kit/Controls/chatgpt/$name.png ($(stat -c %s "$dest/$name.png") bytes)"
    release
    ;;
  abort)
    if [ -f "$mine" ]; then release; echo "lock released"; else echo "this run does not hold the lock; left alone"; fi
    ;;
  *) echo "usage: dl.sh begin | end <name> | abort"; exit 2 ;;
esac
