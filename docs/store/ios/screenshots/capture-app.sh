#!/bin/zsh
# Captures the app screens the App Store slides are built from, on the iPhone and iPad simulators.
#
# Build the simulator app first:
#   dotnet build src/DailyPlants/DailyPlants.csproj -c Release -f net10.0-ios -r iossimulator-arm64
# Needs Xcode and Maestro (brew install mobile-dev-inc/tap/maestro openjdk@17).
# It replaces the app's data in the simulators with demo data, so don't use them for anything you want to keep.
#
# Usage: ./capture-app.sh [iphone|ipad]...
set -euo pipefail

here=${0:A:h}
repo=${here:h:h:h:h}
app=${APP_PATH:-$repo/src/DailyPlants/bin/Release/net10.0-ios/iossimulator-arm64/DailyPlants.app}
bundle=dev.mzikmund.dailyplants
export JAVA_HOME=${JAVA_HOME:-/opt/homebrew/opt/openjdk@17}
export MAESTRO_CLI_NO_ANALYTICS=1

typeset -A simulators=(iphone 'iPhone 17 Pro Max' ipad 'iPad Pro 13-inch (M5)')
typeset -A orientations=(iphone PORTRAIT ipad LANDSCAPE_LEFT)

[[ -d $app ]] || { print -u2 "No app at $app. Build the iOS simulator head first."; exit 1 }

settings=(
    DailyDozenEnabled System.Boolean:True
    TwentyOneTweaksEnabled System.Boolean:False
    WeightTrackingEnabled System.Boolean:True
    GoalWeight System.Double:72
    HeightCm System.Double:178
    SeenTips System.String:diary-log-serving,diary-day-progress,diary-past-days
    __Uno.PrimaryLanguageOverride System.String:en
)

# Maestro's launchApp returns while the splash screen is still up, so launch the app here and wait.
run_flow() {
    local device=$1 flow=$2 out
    xcrun simctl launch $udid $bundle >/dev/null
    sleep 10
    out=$(mktemp -d)
    maestro --device $udid test --test-output-dir $out -e ORIENTATION=${orientations[$device]} $here/$flow
    find $out -name '*.png' -exec cp {} $here/captures/$device/ \;
    rm -rf $out
}

devices=($@)
(( $# )) || devices=(iphone ipad)
for device in $devices; do
    name=${simulators[$device]}
    udid=$(xcrun simctl list devices available -j | python3 -c "
import json, sys
devices = [d for r in json.load(sys.stdin)['devices'].values() for d in r if d['name'] == sys.argv[1]]
print(devices[0]['udid'] if devices else '')" "$name")
    [[ -n $udid ]] || { print -u2 "No '$name' simulator. Add one in Xcode."; exit 1 }

    print "== $name"
    xcrun simctl boot $udid 2>/dev/null || true
    open -a Simulator
    xcrun simctl bootstatus $udid -b >/dev/null
    xcrun simctl install $udid $app
    # The first launch creates the database.
    xcrun simctl launch $udid $bundle >/dev/null
    sleep 8
    xcrun simctl terminate $udid $bundle
    sleep 3

    data=$(xcrun simctl get_app_container $udid $bundle data)
    # The app's own plist, written through the simulator's cfprefsd so the app doesn't read a stale copy.
    prefs=$data/Library/Preferences/$bundle
    for key value in $settings ThemePreference System.Int32:1; do
        xcrun simctl spawn $udid defaults write $prefs $key -string $value
    done
    xcrun simctl spawn $udid defaults write $prefs AppleLanguages -array en
    (cd $here/../../windows/screenshots && dotnet run seed-demo-data.cs -- "$data/Documents/DailyPlants/dailyplants.db")
    xcrun simctl status_bar $udid override --time 9:41 --batteryState charged --batteryLevel 100 \
        --cellularMode active --cellularBars 4 --wifiBars 3 --dataNetwork wifi

    mkdir -p $here/captures/$device
    run_flow $device $device.yaml

    xcrun simctl terminate $udid $bundle
    # The app saves its settings as it quits, so let it finish before changing the theme.
    sleep 3
    xcrun simctl spawn $udid defaults write $prefs ThemePreference -string System.Int32:2
    [[ $(xcrun simctl spawn $udid defaults read $prefs ThemePreference) == System.Int32:2 ]] || { print -u2 "Couldn't switch to the dark theme."; exit 1 }
    run_flow $device dark.yaml

    xcrun simctl terminate $udid $bundle
    xcrun simctl status_bar $udid clear
done
print "Captures are in $here/captures"
