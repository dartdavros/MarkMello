#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

app_bundle=""
runtime_id=""
output_dir="$script_dir/dist"
volume_name="MarkMello"

usage() {
  cat <<'EOF'
Usage:
  build-dmg.sh \
    --app-bundle <path> \
    --runtime-id <osx-x64|osx-arm64> \
    [--output-dir <path>] \
    [--volume-name <name>]
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --app-bundle)
      app_bundle="$2"
      shift 2
      ;;
    --runtime-id)
      runtime_id="$2"
      shift 2
      ;;
    --output-dir)
      output_dir="$2"
      shift 2
      ;;
    --volume-name)
      volume_name="$2"
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

if [[ -z "$app_bundle" || -z "$runtime_id" ]]; then
  usage >&2
  exit 1
fi

case "$runtime_id" in
  osx-arm64)
    asset_suffix="macos-arm64"
    ;;
  osx-x64)
    asset_suffix="macos-x64"
    ;;
  *)
    echo "Unsupported runtime id: $runtime_id" >&2
    exit 1
    ;;
esac

if [[ ! -d "$app_bundle" ]]; then
  echo "App bundle not found: $app_bundle" >&2
  exit 1
fi

mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"
app_bundle="$(cd "$app_bundle/.." && pwd)/$(basename "$app_bundle")"

staging_dir="$(mktemp -d "${TMPDIR:-/tmp}/markmello-dmg.XXXXXX")"
cleanup() {
  rm -rf "$staging_dir"
}
trap cleanup EXIT

cp -R "$app_bundle" "$staging_dir/MarkMello.app"
ln -s /Applications "$staging_dir/Applications"

dmg_path="$output_dir/MarkMello-$asset_suffix.dmg"
rm -f "$dmg_path"

hdiutil create \
  -volname "$volume_name" \
  -srcfolder "$staging_dir" \
  -ov \
  -format UDZO \
  "$dmg_path" >/dev/null

echo "$dmg_path"
