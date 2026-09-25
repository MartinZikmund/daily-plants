#!/bin/zsh
# Renders slides.html into images/<N>.png (2880x1800) with headless Edge or Chrome.
# Usage: ./render-slides.sh
set -euo pipefail

here=${0:A:h}
images=${here:h}/images
browser=${BROWSER:-}
for candidate in '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge' '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'; do
    [[ -z $browser && -x $candidate ]] && browser=$candidate
done
[[ -n $browser ]] || { print -u2 'Install Edge or Chrome, or set BROWSER.'; exit 1 }

# The order they appear in the Mac App Store.
slides=(hero details statistics achievements resources dark)

profile=$(mktemp -d)
trap 'rm -rf $profile' EXIT

mkdir -p $images
for i in {1..${#slides}}; do
    out=$images/$i.png
    rm -f $out
    # Headless Edge on macOS keeps running after the screenshot, so stop it once the file is there.
    $browser --headless=new --disable-gpu --hide-scrollbars --allow-file-access-from-files --user-data-dir=$profile \
        --force-device-scale-factor=2 --window-size=1440,900 --virtual-time-budget=3000 \
        --screenshot=$out "file://$here/slides.html?slide=${slides[$i]}" >/dev/null 2>&1 &
    pid=$!
    for _ in {1..60}; do [[ -s $out ]] && break; sleep 0.5; done
    sleep 0.5
    kill $pid 2>/dev/null || true
    wait $pid 2>/dev/null || true
    [[ -s $out ]] || { print -u2 "Rendering ${slides[$i]} failed."; exit 1 }
    print "${slides[$i]} -> $out"
done
