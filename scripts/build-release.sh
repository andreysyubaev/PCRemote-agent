#!/usr/bin/env bash
# Builds GitHub-ready release archives for PCRemote.
#
# Usage:
#   ./scripts/build-release.sh
#   VERSION=1.0.1 ./scripts/build-release.sh
#   CREATE_DMG=1 ./scripts/build-release.sh
#
# Environment variables:
#   VERSION       1.0.0 (default)
#   MAC_RIDS      "osx-arm64 osx-x64" (default)
#   WINDOWS_RIDS  "win-x64" (default)
#   CREATE_DMG    1 to also create .dmg files for macOS (default: 0)
#   MAC_SIGN_IDENTITY  optional real signing identity for the .app bundles

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

PROJECT="${REPO_ROOT}/src/PCRemote.Desktop/PCRemote.Desktop.csproj"
VERSION="${VERSION:-1.0.0}"
RELEASE_DIR="${RELEASE_DIR:-${REPO_ROOT}/artifacts/release/${VERSION}}"

MAC_RIDS="${MAC_RIDS:-osx-arm64 osx-x64}"
WINDOWS_RIDS="${WINDOWS_RIDS:-win-x64}"
CREATE_DMG="${CREATE_DMG:-0}"

PUBLISH_MAC_SCRIPT="${SCRIPT_DIR}/publish-mac.sh"

if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "This script must be run on macOS." >&2
    exit 1
fi

for tool in zip ditto shasum file; do
    if ! command -v "${tool}" >/dev/null 2>&1; then
        echo "Required tool not found: ${tool}" >&2
        exit 1
    fi
done

if [[ ! -x "${PUBLISH_MAC_SCRIPT}" ]]; then
    echo "Missing executable script: ${PUBLISH_MAC_SCRIPT}" >&2
    exit 1
fi

mkdir -p "${RELEASE_DIR}"
rm -f "${RELEASE_DIR}"/PCRemote-"${VERSION}"-*.zip \
      "${RELEASE_DIR}"/PCRemote-"${VERSION}"-*.dmg \
      "${RELEASE_DIR}/SHA256SUMS.txt"

echo "=== macOS ==="
for rid in ${MAC_RIDS}; do
    RID="${rid}" \
    OUTPUT_DIR="${RELEASE_DIR}/mac-${rid}" \
    VERSION="${VERSION}" \
        "${PUBLISH_MAC_SCRIPT}"

    app="${RELEASE_DIR}/mac-${rid}/PCRemote.app"
    mac_exe="${app}/Contents/MacOS/PCRemote.Desktop"
    zip_file="${RELEASE_DIR}/PCRemote-${VERSION}-macos-${rid}.zip"

    case "${rid}" in
        osx-arm64) expected_arch="arm64" ;;
        osx-x64) expected_arch="x86_64" ;;
    esac

    if ! file "${mac_exe}" | grep -q "${expected_arch}"; then
        echo "Unexpected architecture in ${mac_exe}; expected ${expected_arch}." >&2
        exit 1
    fi

    rm -f "${zip_file}"
    ditto -c -k --sequesterRsrc --keepParent "${app}" "${zip_file}"
    echo "Archive: ${zip_file}"

    if [[ "${CREATE_DMG}" == "1" ]]; then
        dmg_file="${RELEASE_DIR}/PCRemote-${VERSION}-macos-${rid}.dmg"
        rm -f "${dmg_file}"
        hdiutil create \
            -volname "PCRemote" \
            -srcfolder "${app}" \
            -ov \
            -format UDZO \
            "${dmg_file}" >/dev/null
        echo "Image: ${dmg_file}"
    fi
done

echo
echo "=== Windows ==="
for rid in ${WINDOWS_RIDS}; do
    platform_dir="${RELEASE_DIR}/windows-${rid}"
    publish_dir="${platform_dir}/publish"
    zip_file="${RELEASE_DIR}/PCRemote-${VERSION}-windows-${rid}.zip"

    rm -rf "${platform_dir}"
    mkdir -p "${publish_dir}"

    dotnet publish "${PROJECT}" \
        -c Release \
        -r "${rid}" \
        --self-contained true \
        -p:Version="${VERSION}" \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:IncludeAllContentForSelfExtract=true \
        -p:DebugType=none \
        --nologo \
        -o "${publish_dir}"

    if [[ ! -f "${publish_dir}/PCRemote.Desktop.exe" ]]; then
        echo "Expected Windows executable was not published: ${publish_dir}/PCRemote.Desktop.exe" >&2
        exit 1
    fi

    # Single-file publish leaves optional content and package PDBs next to the exe.
    # The user-facing archive should contain only the executable.
    find "${publish_dir}" -mindepth 1 -maxdepth 1 \
        ! -name 'PCRemote.Desktop.exe' -exec rm -rf {} +

    if ! file "${publish_dir}/PCRemote.Desktop.exe" | grep -q "PE32"; then
        echo "Unexpected Windows executable format: ${publish_dir}/PCRemote.Desktop.exe" >&2
        exit 1
    fi

    (
        cd "${publish_dir}"
        zip -q -r "${zip_file}" .
    )

    echo "Archive: ${zip_file}"
done

echo
echo "=== Checksums ==="
artifacts=("${RELEASE_DIR}"/PCRemote-"${VERSION}"-*)
(
    cd "${RELEASE_DIR}"
    shasum -a 256 "$(basename "${artifacts[0]}")" > SHA256SUMS.txt
    for artifact in "${artifacts[@]:1}"; do
        shasum -a 256 "$(basename "${artifact}")" >> SHA256SUMS.txt
    done
)

cat > "${RELEASE_DIR}/RELEASE_NOTES.md" <<EOF
# PCRemote ${VERSION}

## Files

- \`PCRemote-${VERSION}-macos-osx-arm64.zip\` — macOS, Apple Silicon, self-contained \`.app\`
- \`PCRemote-${VERSION}-macos-osx-x64.zip\` — macOS, Intel, self-contained \`.app\`
- \`PCRemote-${VERSION}-windows-win-x64.zip\` — Windows x64, self-contained single-file \`PCRemote.Desktop.exe\`

The macOS bundles are ad-hoc signed unless \`MAC_SIGN_IDENTITY\` was provided during the build.
On macOS, right-click the app and choose **Open** on first launch if Gatekeeper warns.

Verify downloads with:

\`\`\`bash
shasum -a 256 -c SHA256SUMS.txt
\`\`\`
EOF

echo
echo "Готово: ${RELEASE_DIR}"
echo
echo "Файлы релиза:"
printf '  %s\n' "${artifacts[@]}"
echo
echo "Контрольные суммы: ${RELEASE_DIR}/SHA256SUMS.txt"
echo
echo "Пример публикации:"
echo "  gh release create v${VERSION} ${RELEASE_DIR}/PCRemote-${VERSION}-* ${RELEASE_DIR}/SHA256SUMS.txt \\"
echo "    --title \"PCRemote ${VERSION}\" \\"
echo "    --notes-file ${RELEASE_DIR}/RELEASE_NOTES.md"
