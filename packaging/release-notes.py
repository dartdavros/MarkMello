"""Generate Skaro-style notes; publication also requires verified complete assets."""

import argparse
import json
import re
from pathlib import Path

from release_assets import download_table, validate_assets


def render_notes(tag: str, source_commit: str, summary: str, unsigned_windows: bool = False) -> str:
    if not re.fullmatch(r"v\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", tag):
        raise ValueError("Expected a semantic version tag such as v0.7.0")
    if not re.fullmatch(r"[0-9a-f]{40}", source_commit):
        raise ValueError("Expected the full source commit SHA")
    if not summary.strip():
        raise ValueError("A release summary is required")
    return "\n\n".join((
        f"MarkMello {tag[1:]}.",
        summary.strip(),
        download_table(tag),
        ("Windows installers are unsigned; Windows may show an unknown publisher warning. "
         if unsigned_windows else "Windows installers are signed through SignPath Foundation. ") +
        "macOS builds use an ad-hoc signature and are not notarized; "
        "macOS may require approval on first launch.",
        "Download the installer matching your system. SHA256 checksum files accompany all downloads.",
        f"Source commit: {source_commit}",
    )) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--unsigned-windows", action="store_true")
    parser.add_argument("--release-metadata", type=Path)
    parser.add_argument("--checksum-directory", type=Path)
    args = parser.parse_args()
    if bool(args.release_metadata) != bool(args.checksum_directory):
        parser.error("Asset metadata and downloaded checksums must be supplied together")
    version_core = args.tag.removeprefix("v").split("-", 1)[0]
    if not re.fullmatch(r"\d+\.\d+\.\d+", version_core):
        parser.error("Invalid version")
    summary = (Path(__file__).parent / "release-notes" / f"{version_core}.md").read_text(encoding="utf-8")
    notes = render_notes(args.tag, args.source_commit, summary, args.unsigned_windows)
    if args.release_metadata:
        metadata = json.loads(args.release_metadata.read_text(encoding="utf-8"))
        if metadata.get("tag_name") != args.tag or not metadata.get("draft"):
            raise ValueError("Only the requested draft release can be finalized")
        if metadata.get("target_commitish") != args.source_commit:
            raise ValueError("The draft release targets a different source commit")
        validate_assets(metadata, args.checksum_directory)
    args.output.write_text(notes, encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
