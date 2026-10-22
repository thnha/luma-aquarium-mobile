#!/usr/bin/env bash
# Content-only update: rebuilds the remote content (MainAquarium, fish, decor) and uploads it to R2.
# Installed apps get it on their next launch, without a new TestFlight / Play build.
# Only for art, scene and data changes. Changed or new scripts need a new app build (Tools/build_testflight.sh).
#
# Usage: Tools/publish_content.sh <ios|android|all>
# The Unity editor must be closed; with it open, use Lumaquarium > Remote Content > Build Content (Active Platform)
# and then Tools/upload_content_r2.sh <iOS|Android>.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
LOG_DIR="$PROJECT_DIR/Builds/logs"
case "${1:-}" in
  ios) TARGETS=(iOS) ;;
  android) TARGETS=(Android) ;;
  all) TARGETS=(iOS Android) ;;
  *) echo "Usage: $0 <ios|android|all>" >&2; exit 2 ;;
esac

UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt")"
UNITY="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
[[ -x "$UNITY" ]] || { echo "Unity $UNITY_VERSION not found at $UNITY" >&2; exit 1; }
if pgrep -f "Unity.app/Contents/MacOS/Unity -projectpath $PROJECT_DIR" >/dev/null; then
  echo "The Unity editor has this project open. Close it, or build from Lumaquarium > Remote Content." >&2
  exit 1
fi
mkdir -p "$LOG_DIR"

for target in "${TARGETS[@]}"; do
  log="$LOG_DIR/unity-content-$target.log"
  echo "==> Unity: building $target content (log: $log)"
  "$UNITY" -batchmode -quit -projectPath "$PROJECT_DIR" -buildTarget "$target" \
    -executeMethod RemoteContent.BuildFromCommandLine -logFile "$log" \
    || { echo "Content build failed, see $log" >&2; exit 1; }
  "$PROJECT_DIR/Tools/upload_content_r2.sh" "$target"
done
