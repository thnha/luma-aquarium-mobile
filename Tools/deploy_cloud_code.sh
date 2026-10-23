#!/usr/bin/env bash
# Tests and deploys the Cloud Code module LumaquariumShop (the shop server, with ShopCatalog.json inside) to the
# Unity Gaming Services project and environment set with `ugs config`. upload_content_r2.sh runs it before every
# upload, so the server never sells at other prices than the content shows.
#
# Usage: Tools/deploy_cloud_code.sh
# Needs the .NET SDK and the UGS CLI (npm install -g ugs) logged in with a service account (ugs login).
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
MODULE_DIR="$PROJECT_DIR/CloudCode"
command -v dotnet >/dev/null || { echo "The .NET SDK is missing: https://dotnet.microsoft.com/download" >&2; exit 1; }
command -v ugs >/dev/null || { echo "The UGS CLI is missing: npm install -g ugs, then ugs login" >&2; exit 1; }

echo "==> Cloud Code: testing LumaquariumShop"
dotnet test "$MODULE_DIR/LumaquariumShop.Tests" --nologo -v quiet
echo "==> Cloud Code: deploying LumaquariumShop"
ugs deploy "$MODULE_DIR/LumaquariumShop/LumaquariumShop.sln"
