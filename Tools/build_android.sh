#!/usr/bin/env bash
# Builds the Android app with Unity in batch mode, via AndroidBuilder.BuildFromCommandLine.
#   default  .aab for Google Play -> Builds/Android/BettaFishAquarium3D-<version>-<code>.aab
#   --apk    .apk to sideload instead
#   --install  (with --apk) install it on the connected device with adb
#   --no-content-upload  don't upload the remote content (ServerData/Android/<version>) to R2 after the build
# The Unity editor must be closed; with it open, use Lumaquarium > Build > Android ... instead.
# Env:
#   BUILD_NUMBER  versionCode to use (default: current version code + 1)
#   ANDROID_KEYSTORE, ANDROID_KEYSTORE_PASS, ANDROID_KEY_ALIAS, ANDROID_KEY_PASS  upload key (required for Google Play)
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$PROJECT_DIR/Builds/Android"
LOG_DIR="$PROJECT_DIR/Builds/logs"
FORMAT="aab"
INSTALL=0
CONTENT_UPLOAD=1

for arg in "$@"; do
  case "$arg" in
    --apk) FORMAT="apk" ;;
    --install) INSTALL=1 ;;
    --no-content-upload) CONTENT_UPLOAD=0 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done
if [[ $INSTALL -eq 1 && $FORMAT != "apk" ]]; then
  echo "--install needs --apk" >&2
  exit 2
fi

UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt")"
UNITY_APP="/Applications/Unity/Hub/Editor/$UNITY_VERSION"
UNITY="$UNITY_APP/Unity.app/Contents/MacOS/Unity"
[[ -x "$UNITY" ]] || { echo "Unity $UNITY_VERSION not found at $UNITY" >&2; exit 1; }
if pgrep -f "Unity.app/Contents/MacOS/Unity -projectpath $PROJECT_DIR" >/dev/null; then
  echo "The Unity editor has this project open. Close it, or build from Lumaquarium > Build > Android ..." >&2
  exit 1
fi
if [[ $FORMAT == "aab" && -z "${ANDROID_KEYSTORE:-}" ]]; then
  echo "warning: ANDROID_KEYSTORE not set; unless Player Settings has a keystore, Google Play will reject this .aab." >&2
fi

mkdir -p "$LOG_DIR"
STARTED="$(date +%s)"
echo "==> Unity: building .$FORMAT (log: $LOG_DIR/unity-android.log)"
UNITY_ARGS=(-batchmode -quit -projectPath "$PROJECT_DIR" -buildTarget Android
  -executeMethod AndroidBuilder.BuildFromCommandLine -format "$FORMAT" -logFile "$LOG_DIR/unity-android.log")
[[ -n "${BUILD_NUMBER:-}" ]] && UNITY_ARGS+=(-buildNumber "$BUILD_NUMBER")
"$UNITY" "${UNITY_ARGS[@]}" || { echo "Unity build failed, see $LOG_DIR/unity-android.log" >&2; exit 1; }

# Newest artifact of this format written by this run.
ARTIFACT="$(find "$OUT_DIR" -maxdepth 1 -name "*.$FORMAT" -newermt "@$STARTED" -print 2>/dev/null | sort | tail -1)"
[[ -n "$ARTIFACT" ]] || { echo "Build finished but no .$FORMAT in $OUT_DIR" >&2; exit 1; }
echo "==> Built $ARTIFACT"

# The app downloads MainAquarium, fish and decor from R2 (see RemoteContent).
if [[ $CONTENT_UPLOAD -eq 1 ]]; then
  "$PROJECT_DIR/Tools/upload_content_r2.sh" Android
fi

if [[ $INSTALL -eq 1 ]]; then
  ADB="$UNITY_APP/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
  command -v adb >/dev/null && ADB="$(command -v adb)"
  echo "==> adb install"
  "$ADB" install -r "$ARTIFACT"
fi

if [[ $FORMAT == "aab" ]]; then
  echo "==> Upload it in Google Play Console > Testing > Internal testing > Create new release."
fi
