#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

publish_dir=""
runtime_id=""
version=""
output_dir="$script_dir/dist"
appimagetool="${APPIMAGETOOL:-appimagetool}"

usage() {
  cat <<'EOF'
Usage:
  build-appimage.sh \
    --publish-dir <path> \
    --runtime-id <linux-x64|linux-arm64> \
    --version <x.y.z> \
    [--output-dir <path>] \
    [--appimagetool <path>]
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --publish-dir)
      publish_dir="$2"
      shift 2
      ;;
    --runtime-id)
      runtime_id="$2"
      shift 2
      ;;
    --version)
      version="$2"
      shift 2
      ;;
    --output-dir)
      output_dir="$2"
      shift 2
      ;;
    --appimagetool)
      appimagetool="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ -z "$publish_dir" || -z "$runtime_id" || -z "$version" ]]; then
  usage >&2
  exit 1
fi

case "$runtime_id" in
  linux-x64)
    arch="x86_64"
    ;;
  linux-arm64)
    arch="aarch64"
    ;;
  *)
    echo "Unsupported runtime id: $runtime_id" >&2
    exit 1
    ;;
esac

if [[ ! -x "$publish_dir/MarkMello" ]]; then
  echo "Published executable not found: $publish_dir/MarkMello" >&2
  exit 1
fi

mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"
publish_dir="$(cd "$publish_dir" && pwd)"

staging_dir="$(mktemp -d "${TMPDIR:-/tmp}/markmello-appimage.XXXXXX")"
cleanup() {
  rm -rf "$staging_dir"
}
trap cleanup EXIT

app_dir="$staging_dir/MarkMello.AppDir"
mkdir -p \
  "$app_dir/usr/bin" \
  "$app_dir/usr/share/applications" \
  "$app_dir/usr/share/icons/hicolor/512x512/apps" \
  "$app_dir/usr/share/metainfo"

cp -R "$publish_dir/." "$app_dir/usr/bin/"
chmod +x "$app_dir/usr/bin/MarkMello"

# Версия и архитектура попадают в desktop entry: AppImage читает их оттуда,
# отдельного манифеста у формата нет.
sed \
  -e "s/@VERSION@/$version/" \
  -e "s/@ARCH@/$arch/" \
  "$script_dir/markmello.desktop" \
  > "$app_dir/usr/share/applications/markmello.desktop"

cp "$script_dir/markmello.png" "$app_dir/usr/share/icons/hicolor/512x512/apps/markmello.png"

# Корневые копии обязательны: appimagetool ищет entry и иконку именно в корне AppDir.
cp "$app_dir/usr/share/applications/markmello.desktop" "$app_dir/markmello.desktop"
cp "$script_dir/markmello.png" "$app_dir/markmello.png"
ln -sf markmello.png "$app_dir/.DirIcon"

# AppRun должен пережить и запуск по симлинку, и запуск из каталога с пробелами.
cat > "$app_dir/AppRun" <<'EOF'
#!/bin/sh
here="$(dirname "$(readlink -f "$0")")"
exec "$here/usr/bin/MarkMello" "$@"
EOF
chmod +x "$app_dir/AppRun"

appimage_path="$output_dir/MarkMello-linux-$arch.AppImage"
rm -f "$appimage_path"

# extract-and-run: на CI-раннерах нет FUSE, без этого appimagetool не стартует сам.
ARCH="$arch" APPIMAGE_EXTRACT_AND_RUN=1 \
  "$appimagetool" "$app_dir" "$appimage_path" >/dev/null

chmod +x "$appimage_path"

echo "$appimage_path"
