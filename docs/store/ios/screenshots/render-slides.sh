#!/bin/zsh
# Renders slides.html into images/iphone/<N>.png and images/ipad/<N>.png with headless Edge or Chrome.
# Usage: ./render-slides.sh [iphone|ipad]...
set -euo pipefail

here=${0:A:h}
images=${here:h}/images
browser=${BROWSER:-}
for candidate in '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge' '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'; do
    [[ -z $browser && -x $candidate ]] && browser=$candidate
done
[[ -n $browser ]] || { print -u2 'Install Edge or Chrome, or set BROWSER.'; exit 1 }

# The order they appear in the App Store.
slides=(hero details statistics achievements resources dark)
# Canvas size in points and the scale that gives the App Store's screenshot size.
typeset -A sizes=(iphone 440,956 ipad 1376,1032)
typeset -A scales=(iphone 3 ipad 2)

profile=$(mktemp -d)
trap 'rm -rf $profile' EXIT

devices=($@)
(( $# )) || devices=(iphone ipad)
for device in $devices; do
    mkdir -p $images/$device
    for i in {1..${#slides}}; do
        out=$images/$device/$i.png
        rm -f $out
        # Headless Edge on macOS keeps running after the screenshot, so stop it once the file is there.
        $browser --headless=new --disable-gpu --hide-scrollbars --allow-file-access-from-files --user-data-dir=$profile \
            --force-device-scale-factor=${scales[$device]} --window-size=${sizes[$device]} --virtual-time-budget=3000 \
            --screenshot=$out "file://$here/slides.html?device=$device&slide=${slides[$i]}" >/dev/null 2>&1 &
        pid=$!
        for _ in {1..60}; do [[ -s $out ]] && break; sleep 0.5; done
        sleep 0.5
        kill $pid 2>/dev/null || true
        wait $pid 2>/dev/null || true
        [[ -s $out ]] || { print -u2 "Rendering $device ${slides[$i]} failed."; exit 1 }
        print "$device ${slides[$i]} -> $out"
    done
done
