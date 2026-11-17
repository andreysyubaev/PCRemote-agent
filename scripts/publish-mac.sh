#!/usr/bin/env bash
# Builds a macOS .app bundle for PCRemote using the icon already in the repo.
#
# Usage:
#   ./scripts/publish-mac.sh
#   RID=osx-x64 ./scripts/publish-mac.sh
#   VERSION=1.0.0 ./scripts/publish-mac.sh
#   MAC_SIGN_IDENTITY="Developer ID Application: ..." ./scripts/publish-mac.sh
#
# Environment variables:
#   RID                osx-arm64 (default) or osx-x64
#   CONFIGURATION      Release (default)
#   SELF_CONTAINED     true (default) or false
#   VERSION            1.0.0 (default)
#   OUTPUT_DIR         default: <repo>/artifacts/mac/<RID>
#   MAC_SIGN_IDENTITY  optional real signing identity; ad-hoc sign when empty

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

PROJECT="${REPO_ROOT}/src/PCRemote.Desktop/PCRemote.Desktop.csproj"
ICON_SOURCE="${REPO_ROOT}/src/PCRemote.Desktop/Assets/Icons/App/app-icon.icns"

RID="${RID:-osx-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"
SELF_CONTAINED="${SELF_CONTAINED:-true}"
VERSION="${VERSION:-1.0.0}"
OUTPUT_DIR="${OUTPUT_DIR:-${REPO_ROOT}/artifacts/mac/${RID}}"

APP_NAME="PCRemote"
EXECUTABLE_NAME="PCRemote.Desktop"
BUNDLE_ID="com.andreysyubaev.pcremote"

PUBLISH_DIR="${OUTPUT_DIR}/publish"
APP_BUNDLE="${OUTPUT_DIR}/${APP_NAME}.app"

case "${RID}" in
    osx-arm64|osx-x64) ;;
    *)
        echo "Unsupported RID '${RID}'. Use osx-arm64 or osx-x64." >&2
        exit 1
        ;;
esac

if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "This script must be run on macOS." >&2
    exit 1
fi

if [[ ! -f "${PROJECT}" ]]; then
    echo "Project not found: ${PROJECT}" >&2
    exit 1
fi

if [[ ! -f "${ICON_SOURCE}" ]]; then
    echo "macOS icon not found: ${ICON_SOURCE}" >&2
    exit 1
fi

if ! command -v plutil >/dev/null 2>&1; then
    echo "plutil is required to validate Info.plist." >&2
    exit 1
fi

echo "Publishing PCRemote ${VERSION} / ${RID} (${CONFIGURATION}, self-contained=${SELF_CONTAINED})..."
rm -rf "${PUBLISH_DIR}" "${APP_BUNDLE}"
mkdir -p "${PUBLISH_DIR}" \
         "${APP_BUNDLE}/Contents/MacOS" \
         "${APP_BUNDLE}/Contents/Resources"

dotnet publish "${PROJECT}" \
    -c "${CONFIGURATION}" \
    -r "${RID}" \
    --self-contained "${SELF_CONTAINED}" \
    -p:Version="${VERSION}" \
    --nologo \
    -o "${PUBLISH_DIR}"

if [[ ! -f "${PUBLISH_DIR}/${EXECUTABLE_NAME}" ]]; then
    echo "Expected executable '${EXECUTABLE_NAME}' was not published." >&2
    echo "Update EXECUTABLE_NAME in this script if the assembly name changed." >&2
    exit 1
fi

cp -R "${PUBLISH_DIR}/." "${APP_BUNDLE}/Contents/MacOS/"
cp "${ICON_SOURCE}" "${APP_BUNDLE}/Contents/Resources/${APP_NAME}.icns"
chmod +x "${APP_BUNDLE}/Contents/MacOS/${EXECUTABLE_NAME}"

cat > "${APP_BUNDLE}/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>${APP_NAME}</string>
    <key>CFBundleExecutable</key>
    <string>${EXECUTABLE_NAME}</string>
    <key>CFBundleIconFile</key>
    <string>${APP_NAME}.icns</string>
    <key>CFBundleIdentifier</key>
    <string>${BUNDLE_ID}</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>${APP_NAME}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.utilities</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

plutil -lint "${APP_BUNDLE}/Contents/Info.plist" >/dev/null

if [[ -n "${MAC_SIGN_IDENTITY:-}" ]]; then
    echo "Signing with: ${MAC_SIGN_IDENTITY}"
    codesign --force --deep --sign "${MAC_SIGN_IDENTITY}" "${APP_BUNDLE}"
else
    echo "Signing ad-hoc..."
    codesign --force --deep --sign - "${APP_BUNDLE}"
fi

codesign --verify --deep "${APP_BUNDLE}"

echo
echo "Готово: ${APP_BUNDLE}"
echo "Запуск: open \"${APP_BUNDLE}\""
