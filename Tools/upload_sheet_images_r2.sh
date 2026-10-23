#!/usr/bin/env bash
# Uploads the shop portraits for the Google Sheet price list (=IMAGE() cells, see DecorCatalogSheet).
#   Assets/_Project/Art/Aquarium3D/DecorShop/*.png  ->  s3://$R2_BUCKET/sheet-images/decor/
#   Assets/_Project/UI/Portraits/Shop/<fish>.png     ->  s3://$R2_BUCKET/sheet-images/fish/
#   Assets/_Project/UI/Portraits/Shop/surface_*.png  ->  s3://$R2_BUCKET/sheet-images/surface/
# Kept apart from the app's remote content (<platform>/<version>/), which this never touches.
#
# Usage: Tools/upload_sheet_images_r2.sh
# Credentials: Tools/r2.env (gitignored, copy Tools/r2.env.example) or the same variables in the environment.
# Needs the AWS CLI (R2 speaks the S3 API): brew install awscli
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
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
export AWS_ACCESS_KEY_ID="$R2_ACCESS_KEY_ID" AWS_SECRET_ACCESS_KEY="$R2_SECRET_ACCESS_KEY" AWS_DEFAULT_REGION=auto

SRC="$PROJECT_DIR/Assets/_Project/Art/Aquarium3D/DecorShop"
ls "$SRC"/*.png >/dev/null 2>&1 || { echo "No portraits in $SRC. Build the decoration shop first." >&2; exit 1; }
DEST="s3://$R2_BUCKET/sheet-images/decor"
# Fish shop portraits (FishShopScreenBuilder), named by shop id; the surface swatches beside them are left out.
FISH_SRC="$PROJECT_DIR/Assets/_Project/UI/Portraits/Shop"
FISH_DEST="s3://$R2_BUCKET/sheet-images/fish"
# The wall and floor swatches (AquariumSurfaceBuilder) share that folder.
SURFACE_DEST="s3://$R2_BUCKET/sheet-images/surface"

# Newer AWS CLIs send checksums R2 may reject; only send them when the API requires one.
export AWS_REQUEST_CHECKSUM_CALCULATION=when_required AWS_RESPONSE_CHECKSUM_VALIDATION=when_required
S3=(aws s3 --endpoint-url "https://$R2_ACCOUNT_ID.r2.cloudflarestorage.com")

# The sheet's links carry ?v=<hash>, so a changed portrait shows up even if an old copy is cached.
echo "==> Uploading portraits to $DEST"
"${S3[@]}" sync "$SRC" "$DEST" --exclude "*" --include "*.png" --content-type image/png --cache-control "public, max-age=3600"
echo "==> Uploading fish portraits to $FISH_DEST"
"${S3[@]}" sync "$FISH_SRC" "$FISH_DEST" --exclude "*" --include "*.png" --exclude "surface_*" --content-type image/png --cache-control "public, max-age=3600"
echo "==> Uploading wall and floor portraits to $SURFACE_DEST"
"${S3[@]}" sync "$FISH_SRC" "$SURFACE_DEST" --exclude "*" --include "surface_*.png" --content-type image/png --cache-control "public, max-age=3600"
echo "==> Done."
