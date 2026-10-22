#!/usr/bin/env bash
# Uploads the remote content (Addressables bundles + catalog) built by RemoteContent to Cloudflare R2.
#   ServerData/<platform>/<version>/  ->  s3://$R2_BUCKET/<platform>/<version>/
# The app reads the same path from RemoteContent.BaseUrl, the bucket's public URL.
#
# Usage: Tools/upload_content_r2.sh <iOS|Android> [version]
#   version  default: Player Settings > Version (bundleVersion), as used by the build
# Credentials: Tools/r2.env (gitignored, copy Tools/r2.env.example) or the same variables in the environment.
# Needs the AWS CLI (R2 speaks the S3 API): brew install awscli
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PLATFORM="${1:-}"
case "$PLATFORM" in
  iOS|Android) ;;
  *) echo "Usage: $0 <iOS|Android> [version]" >&2; exit 2 ;;
esac
VERSION="${2:-$(sed -n 's/^  bundleVersion: *//p' "$PROJECT_DIR/ProjectSettings/ProjectSettings.asset")}"

ENV_FILE="$PROJECT_DIR/Tools/r2.env"
if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi
for name in R2_ACCOUNT_ID R2_ACCESS_KEY_ID R2_SECRET_ACCESS_KEY R2_BUCKET; do
  [[ -n "${!name:-}" ]] || { echo "$name is not set (Tools/r2.env, see Tools/r2.env.example)." >&2; exit 1; }
done
command -v aws >/dev/null || { echo "The AWS CLI is missing: brew install awscli" >&2; exit 1; }

SRC="$PROJECT_DIR/ServerData/$PLATFORM/$VERSION"
[[ -d "$SRC" ]] || { echo "Nothing to upload: $SRC does not exist. Build the content first." >&2; exit 1; }
ls "$SRC"/catalog_*.hash >/dev/null 2>&1 || { echo "No catalog in $SRC; the content build did not finish." >&2; exit 1; }
DEST="s3://$R2_BUCKET/$PLATFORM/$VERSION"

export AWS_ACCESS_KEY_ID="$R2_ACCESS_KEY_ID" AWS_SECRET_ACCESS_KEY="$R2_SECRET_ACCESS_KEY" AWS_DEFAULT_REGION=auto
# Newer AWS CLIs send checksums R2 may reject; only send them when the API requires one.
export AWS_REQUEST_CHECKSUM_CALCULATION=when_required AWS_RESPONSE_CHECKSUM_VALIDATION=when_required
S3=(aws s3 --endpoint-url "https://$R2_ACCOUNT_ID.r2.cloudflarestorage.com")

# Bundles first: their names carry a content hash and never change. Then the catalog that points at them, so an
# app never sees a catalog whose bundles are not there yet. Old bundles stay (installs on older catalogs need them).
echo "==> Uploading bundles to $DEST"
"${S3[@]}" sync "$SRC" "$DEST" --exclude "catalog_*" --size-only \
  --cache-control "public, max-age=31536000, immutable"
echo "==> Uploading catalog"
"${S3[@]}" cp "$SRC" "$DEST" --recursive --exclude "*" --include "catalog_*" --cache-control "no-cache"
echo "==> Done. Apps pick it up on their next launch (loading screen)."
