#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

publish_dir=""
runtime_id=""
version=""
build_number=""
bundle_id="com.markmello.app"
output_dir="$script_dir/dist"
app_name="MarkMello"
icon_path="$script_dir/MarkMello.icns"
template_path="$script_dir/Info.plist"

usage() {
  cat <<'EOF'
Usage:
  build-app-bundle.sh \
    --publish-dir <path> \
    --runtime-id <osx-x64|osx-arm64> \
    --version <bundle-short-version> \
    --build-number <bundle-build-number> \
    [--bundle-id <bundle-id>] \
    [--output-dir <path>]
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
    --build-number)
      build_number="$2"
      shift 2
      ;;
    --bundle-id)
      bundle_id="$2"
      shift 2
      ;;
    --output-dir)
      output_dir="$2"
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

if [[ -z "$publish_dir" || -z "$runtime_id" || -z "$version" || -z "$build_number" ]]; then
  usage >&2
  exit 1
fi

case "$runtime_id" in
  osx-x64|osx-arm64)
    ;;
  *)
    echo "Unsupported runtime id: $runtime_id" >&2
    exit 1
    ;;
esac

if [[ ! -d "$publish_dir" ]]; then
  echo "Publish directory not found: $publish_dir" >&2
  exit 1
fi

if [[ ! -f "$icon_path" ]]; then
  echo "Bundle icon not found: $icon_path" >&2
  exit 1
fi

if [[ ! -f "$template_path" ]]; then
  echo "Info.plist template not found: $template_path" >&2
  exit 1
fi

mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"
publish_dir="$(cd "$publish_dir" && pwd)"

bundle_path="$output_dir/$app_name.app"
contents_path="$bundle_path/Contents"
macos_path="$contents_path/MacOS"
resources_path="$contents_path/Resources"

rm -rf "$bundle_path"
mkdir -p "$macos_path" "$resources_path"

cp -R "$publish_dir"/. "$macos_path/"
cp "$icon_path" "$resources_path/$app_name.icns"

sed \
  -e "s|\$(MARKMELLO_BUNDLE_ID)|$bundle_id|g" \
  -e "s|\$(MARKMELLO_VERSION)|$version|g" \
  -e "s|\$(MARKMELLO_BUILD_NUMBER)|$build_number|g" \
  "$template_path" > "$contents_path/Info.plist"

if [[ -f "$macos_path/$app_name" ]]; then
  chmod +x "$macos_path/$app_name"
fi

echo "$bundle_path"
