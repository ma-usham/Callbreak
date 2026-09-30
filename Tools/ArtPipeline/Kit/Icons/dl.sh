#!/usr/bin/env bash
# The shared download lock around one ChatGPT image download (see Docs/Tasks/UI-Art-ChatGPT-Brief.md).
#   dl.sh lock            take ArtSource/Working/_download.lock (waits up to 90 s)
#   dl.sh take <dest>     move the image that arrived in Downloads since "lock" to <dest>, then release
#   dl.sh unlock          release without taking anything
# Between "lock" and "take", click Download in the ChatGPT viewer. Never hold the lock over a minute.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../../.." && pwd)"
lock="$repo/ArtSource/Working/_download.lock"
mark="${TMPDIR:-/tmp}/icons-download.mark"
downloads="/c/Users/maush/Downloads"
case "${1:-}" in
  lock)
    for i in $(seq 1 45); do
      if mkdir "$lock" 2>/dev/null; then touch "$mark"; echo "locked"; exit 0; fi
      # A lock older than three minutes belongs to a run that died.
      if [ -d "$lock" ] && [ $(( $(date +%s) - $(stat -c %Y "$lock" 2>/dev/null || echo 0) )) -gt 180 ]; then rmdir "$lock" 2>/dev/null; continue; fi
      sleep 2
    done
    echo "could not take the download lock"; exit 1 ;;
  take)
    dest="${2:?destination file}"
    for i in $(seq 1 40); do
      f="$(find "$downloads" -maxdepth 1 -name 'ChatGPT Image*.png' -newer "$mark" 2>/dev/null | head -1)"
      if [ -n "$f" ] && ! ls "$downloads"/*.crdownload >/dev/null 2>&1; then
        sleep 1
        mkdir -p "$(dirname "$dest")"
        mv "$f" "$dest" && echo "took: $(basename "$f") -> $dest"
        rmdir "$lock" 2>/dev/null
        exit 0
      fi
      sleep 1
    done
    rmdir "$lock" 2>/dev/null
    echo "no new ChatGPT image arrived in $downloads"; exit 1 ;;
  unlock)
    rmdir "$lock" 2>/dev/null; echo "released" ;;
  *) echo "usage: dl.sh lock | take <dest> | unlock"; exit 2 ;;
esac
