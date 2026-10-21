#!/usr/bin/env bash
# Builds the iOS app and uploads it to TestFlight.
#   1. Unity batch mode -> Builds/iOS (Xcode project), via IosBuilder.BuildFromCommandLine
#   2. xcodebuild archive -> Builds/Lumaquarium.xcarchive
#   3. xcodebuild -exportArchive with destination=upload -> App Store Connect
#
# Usage: Tools/build_testflight.sh [--skip-unity] [--no-upload]
#   --skip-unity  reuse Builds/iOS (e.g. exported from the open editor: Lumaquarium > Build > iOS Xcode Project)
#   --no-upload   stop after exporting the .ipa to Builds/ipa
# Env:
#   BUILD_NUMBER  CFBundleVersion to use (default: current build number + 1)
#   ASC_KEY_PATH, ASC_KEY_ID, ASC_ISSUER_ID  App Store Connect API key; without it the Apple ID signed in to Xcode is used
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
BUILD_DIR="$PROJECT_DIR/Builds"
XCODE_DIR="$BUILD_DIR/iOS"
ARCHIVE="$BUILD_DIR/Lumaquarium.xcarchive"
LOG_DIR="$BUILD_DIR/logs"
SKIP_UNITY=0
UPLOAD=1

for arg in "$@"; do
  case "$arg" in
    --skip-unity) SKIP_UNITY=1 ;;
    --no-upload) UPLOAD=0 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done

mkdir -p "$LOG_DIR"
TEAM_ID="$(sed -n 's/^  appleDeveloperTeamID: *//p' "$PROJECT_DIR/ProjectSettings/ProjectSettings.asset")"
if [[ -z "$TEAM_ID" ]]; then
  echo "Signing Team ID is empty in Player Settings > iOS." >&2
  exit 1
fi

if [[ $SKIP_UNITY -eq 0 ]]; then
  UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt")"
  UNITY="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
  [[ -x "$UNITY" ]] || { echo "Unity $UNITY_VERSION not found at $UNITY" >&2; exit 1; }
  if pgrep -f "Unity.app/Contents/MacOS/Unity -projectpath $PROJECT_DIR" >/dev/null; then
    echo "The Unity editor has this project open. Close it, or export from the editor menu and pass --skip-unity." >&2
    exit 1
  fi

  echo "==> Unity: exporting Xcode project (log: $LOG_DIR/unity.log)"
  UNITY_ARGS=(-batchmode -quit -projectPath "$PROJECT_DIR" -buildTarget iOS
    -executeMethod IosBuilder.BuildFromCommandLine -logFile "$LOG_DIR/unity.log")
  [[ -n "${BUILD_NUMBER:-}" ]] && UNITY_ARGS+=(-buildNumber "$BUILD_NUMBER")
  "$UNITY" "${UNITY_ARGS[@]}" || { echo "Unity build failed, see $LOG_DIR/unity.log" >&2; exit 1; }
fi

[[ -d "$XCODE_DIR" ]] || { echo "No Xcode project at $XCODE_DIR" >&2; exit 1; }

# CocoaPods-based exports come with a workspace; plain ones only with the project.
if [[ -d "$XCODE_DIR/Unity-iPhone.xcworkspace" ]]; then
  XC_TARGET=(-workspace "$XCODE_DIR/Unity-iPhone.xcworkspace")
else
  XC_TARGET=(-project "$XCODE_DIR/Unity-iPhone.xcodeproj")
fi

AUTH=(-allowProvisioningUpdates)
if [[ -n "${ASC_KEY_PATH:-}" ]]; then
  AUTH+=(-authenticationKeyPath "$ASC_KEY_PATH" -authenticationKeyID "$ASC_KEY_ID" -authenticationKeyIssuerID "$ASC_ISSUER_ID")
fi

echo "==> Xcode: archiving (log: $LOG_DIR/archive.log)"
rm -rf "$ARCHIVE"
xcodebuild archive "${XC_TARGET[@]}" -scheme Unity-iPhone -configuration Release \
  -destination "generic/platform=iOS" -archivePath "$ARCHIVE" \
  DEVELOPMENT_TEAM="$TEAM_ID" CODE_SIGN_STYLE=Automatic "${AUTH[@]}" \
  >"$LOG_DIR/archive.log" 2>&1 || { tail -30 "$LOG_DIR/archive.log" >&2; exit 1; }

if [[ $UPLOAD -eq 1 ]]; then DESTINATION="upload"; else DESTINATION="export"; fi
EXPORT_OPTIONS="$BUILD_DIR/ExportOptions.plist"
cat >"$EXPORT_OPTIONS" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>method</key><string>app-store-connect</string>
  <key>destination</key><string>$DESTINATION</string>
  <key>teamID</key><string>$TEAM_ID</string>
  <key>signingStyle</key><string>automatic</string>
  <key>manageAppVersionAndBuildNumber</key><false/>
  <key>uploadSymbols</key><true/>
</dict>
</plist>
PLIST

echo "==> Xcode: $DESTINATION (log: $LOG_DIR/export.log)"
xcodebuild -exportArchive -archivePath "$ARCHIVE" -exportOptionsPlist "$EXPORT_OPTIONS" \
  -exportPath "$BUILD_DIR/ipa" "${AUTH[@]}" \
  >"$LOG_DIR/export.log" 2>&1 || { tail -30 "$LOG_DIR/export.log" >&2; exit 1; }

if [[ $UPLOAD -eq 1 ]]; then
  echo "==> Uploaded. The build shows up in App Store Connect > TestFlight after processing (10-30 min)."
else
  echo "==> Exported to $BUILD_DIR/ipa"
fi
