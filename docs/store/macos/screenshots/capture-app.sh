#!/bin/zsh
# Captures the app screens the Mac App Store slides are built from.
#
# Publish the app bundle first:
#   dotnet publish src/DailyPlants/DailyPlants.csproj -c Release -f net10.0-desktop -r osx-arm64 \
#     -p:TargetFrameworks=net10.0-desktop -p:SelfContained=true -p:PackageFormat=app
# The terminal needs the Accessibility and Screen Recording permissions (System Settings > Privacy & Security).
# The script switches the main display to a HiDPI mode for sharp 2x captures and switches it back at the end.
# It backs up your Daily Plants data, seeds demo data and restores your data afterwards.
# It clicks in the app window, so leave the mouse alone for the minute it runs.
#
# Usage: ./capture-app.sh
set -euo pipefail

here=${0:A:h}
repo=${here:h:h:h:h}
app=${APP_PATH:-"$repo/src/DailyPlants/bin/Release/net10.0-desktop/osx-arm64/publish/Daily Plants.app"}
captures=$here/captures
data="$HOME/Library/Application Support/DailyPlants"
work=$(mktemp -d)
tool=$work/mac

[[ -d $app ]] || { print -u2 "No app at $app. Publish the app bundle first."; exit 1 }
pgrep -xq DailyPlants && { print -u2 "Close Daily Plants first."; exit 1 }

swiftc -O $here/mac.swift -o $tool

# The app's settings live in a folder named after the WinUI package, under a folder with a GUID name.
settings_dir() {
    local dir=( "$HOME/Library/Application Support"/*/5143MartinZikmund.DailyPlants/Settings(N) )
    print -r -- ${dir[1]:-}
}

# Local.dat is a count followed by key/value strings, each prefixed with its 7-bit encoded UTF-8 length.
write_settings() {
    python3 - "$1" "${@:2}" <<'EOF'
import struct, sys
path, pairs = sys.argv[1], sys.argv[2:]
def read_str(b, i):
    n = s = 0
    while True:
        c = b[i]; i += 1; n |= (c & 0x7F) << s; s += 7
        if c < 0x80: break
    return b[i:i + n].decode(), i + n
def write_str(s):
    b, n, out = s.encode(), len(s.encode()), bytearray()
    while n >= 0x80: out.append(n & 0x7F | 0x80); n >>= 7
    out.append(n)
    return bytes(out) + b
entries = {}
try:
    b = open(path, "rb").read(); i = 4
    for _ in range(struct.unpack_from("<i", b)[0]):
        k, i = read_str(b, i); v, i = read_str(b, i); entries[k] = v
except FileNotFoundError:
    pass
entries.update(zip(pairs[::2], pairs[1::2]))
with open(path, "wb") as f:
    f.write(struct.pack("<i", len(entries)))
    for k, v in entries.items(): f.write(write_str(k) + write_str(v))
EOF
}

launch() {
    open $app
    for i in {1..60}; do pgrep -xq DailyPlants && break; sleep 0.5; done
    sleep 8
    # Tall enough for the Diary's Done today group; the screen may shorten it a little.
    $tool frame 1600 988
    # A click on the title bar makes the window key, so the traffic lights show in colour.
    $tool click 800 14
    sleep 2
}

quit() {
    pkill -x DailyPlants || true
    # The app saves its settings as it quits.
    sleep 3
}

capture() {
    sleep 1.5
    $tool capture $captures/$1.png
    print "Captured $1"
}

# Positions are in points from the window's top-left corner, title bar included.
nav() {
    case $1 in
        diary) $tool click 64 95 ;;
        resources) $tool click 80 135 ;;
        statistics) $tool click 78 175 ;;
        achievements) $tool click 92 215 ;;
    esac
    sleep 2
}

display_mode=$($tool mode)
backup=$work/backup
mkdir -p $backup/db $backup/settings $captures
cleanup() {
    pkill -x DailyPlants || true
    sleep 2
    $tool mode ${=display_mode} || print -u2 "Couldn't switch the display back to ${display_mode}."
    rm -f "$data"/dailyplants.db*(N)
    cp $backup/db/*(N) "$data"/ 2>/dev/null || true
    local dir=$(settings_dir)
    [[ -n $dir ]] && rm -f "$dir"/*(N) && cp $backup/settings/*(N) "$dir"/ 2>/dev/null || true
    rm -rf $work
    print "Restored your data and the display mode."
}
trap cleanup EXIT INT TERM

# The first launch creates the database and the settings folder.
cp "$data"/dailyplants.db*(N) $backup/db/ 2>/dev/null || true
[[ -n $(settings_dir) ]] && cp $(settings_dir)/*(N) $backup/settings/ 2>/dev/null || true
open $app
sleep 10
quit

(cd $here/../../windows/screenshots && dotnet run seed-demo-data.cs -- "$data/dailyplants.db")
settings=$(settings_dir)/Local.dat
write_settings $settings \
    DailyDozenEnabled System.Boolean:True \
    TwentyOneTweaksEnabled System.Boolean:False \
    WeightTrackingEnabled System.Boolean:True \
    GoalWeight System.Double:72 \
    HeightCm System.Double:178 \
    SeenTips System.String:diary-log-serving,diary-day-progress,diary-past-days \
    __Uno.PrimaryLanguageOverride System.String:en \
    ThemePreference System.Int32:1

# 3840x1080 HiDPI draws the app at 2x on the 5120x1440 screen this was set up on.
$tool mode 3840 1080 1 || print -u2 "No 3840x1080 HiDPI mode, capturing at the current scale."
sleep 3

launch
capture diary

# Beans
$tool click 458 290
sleep 2
capture details
$tool click 926 743
sleep 1.5

nav statistics
capture statistics
$tool scroll 1000 600 -40
sleep 1.5
capture statistics-weight
$tool scroll 1000 600 40

nav achievements
capture achievements

nav resources
# The Recipes tab
$tool click 800 223
sleep 5
capture resources

quit
write_settings $settings ThemePreference System.Int32:2
launch
capture diary-dark

# The captures are committed, so keep them small. Lossless, so the slides don't change.
if command -v oxipng >/dev/null; then
    oxipng --quiet --opt 4 --strip safe $captures/*.png
else
    print -u2 "Install oxipng (brew install oxipng) to shrink the captures before committing them."
fi
print "Captures are in $captures"
