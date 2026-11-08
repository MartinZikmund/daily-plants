#!/bin/bash
# Builds the Mac App Store package: a universal (arm64 + x64) sandboxed app bundle, signed and wrapped in an installer .pkg.
#
# Usage: package-app.sh <provisioning profile>
# Environment:
#   CODESIGN_KEY  the "Apple Distribution" identity (name or SHA-1), default "Apple Distribution"
#   INSTALLER_KEY the "3rd Party Mac Developer Installer" identity, default "3rd Party Mac Developer Installer"
# Writes artifacts/store/macos/DailyPlants.pkg.
set -euo pipefail

PROFILE=${1:?Pass the Mac App Store provisioning profile}
CODESIGN_KEY=${CODESIGN_KEY:-Apple Distribution}
INSTALLER_KEY=${INSTALLER_KEY:-3rd Party Mac Developer Installer}

ROOT=$(cd "$(dirname "$0")/../../.." && pwd)
PROJECT="$ROOT/src/DailyPlants/DailyPlants.csproj"
BIN="$ROOT/src/DailyPlants/bin/Release/net10.0-desktop"
OUT="$ROOT/artifacts/store/macos"
APP_NAME="Daily Plants.app"
mkdir -p "$OUT"

# Only the desktop target, and self-contained at restore too, or restore misses the runtime packs.
for rid in osx-arm64 osx-x64; do
  dotnet publish "$PROJECT" -c Release -f net10.0-desktop -r "$rid" \
    -p:TargetFrameworks=net10.0-desktop -p:SelfContained=true -p:PackageFormat=app
done

# Uno only imports its merge, sign and package targets while publishing.
UNO_TARGET="-p:TargetFramework=net10.0-desktop -p:TargetFrameworks=net10.0-desktop -p:_IsPublishing=true"

APP="$OUT/$APP_NAME"
MERGE="$OUT/merge"
rm -rf "$APP" "$MERGE"
mkdir -p "$MERGE/arm64" "$MERGE/x64"

# UnoMergeBundles passes the paths to lipo unquoted, so it merges copies without the space in the name.
cp -R "$BIN/osx-arm64/publish/$APP_NAME" "$MERGE/arm64/DailyPlants.app"
cp -R "$BIN/osx-x64/publish/$APP_NAME" "$MERGE/x64/DailyPlants.app"
dotnet msbuild "$PROJECT" -t:UnoMergeBundles $UNO_TARGET \
  -p:UnoArm64Bundle="$MERGE/arm64/DailyPlants.app" \
  -p:UnoX64Bundle="$MERGE/x64/DailyPlants.app" \
  -p:UnoFatBundle="$MERGE/DailyPlants.app"
mv "$MERGE/DailyPlants.app" "$APP"
rm -rf "$MERGE"

# The merge moves the assemblies into Resources/.arm64 and .x64 but leaves Assets and the other files in Resources,
# and Uno resolves ms-appx:/// from the entry assembly's folder, so link them into both.
RESOURCES="$APP/Contents/Resources"
for arch in "$RESOURCES/.arm64" "$RESOURCES/.x64"; do
  for entry in "$RESOURCES"/*; do
    name=$(basename "$entry")
    case "$name" in
      *.dll|*.pdb) ;;
      *) [ -e "$arch/$name" ] || ln -s "../$name" "$arch/$name" ;;
    esac
  done
done

cp "$PROFILE" "$APP/Contents/embedded.provisionprofile"

# The app's entitlements plus the identifiers from the profile, which the App Store checks against it.
ENTITLEMENTS="$OUT/Entitlements.plist"
PROFILE_PLIST="$OUT/profile.plist"
cp "$ROOT/src/DailyPlants/Platforms/Desktop/macOS/Entitlements.plist" "$ENTITLEMENTS"
security cms -D -i "$PROFILE" > "$PROFILE_PLIST"
for key in com.apple.application-identifier com.apple.developer.team-identifier; do
  value=$(/usr/libexec/PlistBuddy -c "Print :Entitlements:$key" "$PROFILE_PLIST")
  /usr/libexec/PlistBuddy -c "Add :$key string $value" "$ENTITLEMENTS"
done
rm "$PROFILE_PLIST"

dotnet msbuild "$PROJECT" -t:UnoSignAppBundle $UNO_TARGET \
  -p:AppBundlePath="$APP" -p:CodesignKey="$CODESIGN_KEY" -p:UnoMacOSEntitlements="$ENTITLEMENTS"

dotnet msbuild "$PROJECT" -t:UnoPackageAppBundle $UNO_TARGET \
  -p:AppBundlePath="$APP" -p:PackageSigningKey="$INSTALLER_KEY"

mv "$OUT/Daily Plants.pkg" "$OUT/DailyPlants.pkg"
codesign --verify --strict --deep "$APP"
pkgutil --check-signature "$OUT/DailyPlants.pkg"
echo "Wrote $OUT/DailyPlants.pkg"
