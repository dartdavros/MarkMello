"""Release matrix and integrity checks for the real uploaded GitHub assets."""

import hashlib
import re
from pathlib import Path

REPOSITORY = "dartdavros/MarkMello"
PLATFORMS = (
    ("Windows", "MarkMello-setup-win-x64.exe", "MarkMello-setup-win-arm64.exe"),
    ("macOS", "MarkMello-macos-x64.dmg", "MarkMello-macos-arm64.dmg"),
    ("Linux", "MarkMello-linux-x86_64.AppImage", None),
)
PAYLOADS = tuple(name for _, *names in PLATFORMS for name in names if name)
EXPECTED_ASSETS = frozenset(name for payload in PAYLOADS for name in (payload, payload + ".sha256"))


def validate_assets(metadata: dict, checksum_directory: Path) -> None:
    assets = metadata.get("assets", [])
    names = [asset["name"] for asset in assets]
    if len(names) != len(set(names)) or set(names) != EXPECTED_ASSETS:
        missing = sorted(EXPECTED_ASSETS - set(names))
        extra = sorted(set(names) - EXPECTED_ASSETS)
        raise ValueError(f"Incomplete release: missing={missing}, unexpected={extra}")
    by_name = {asset["name"]: asset for asset in assets}
    for asset in assets:
        if not isinstance(asset.get("size"), int) or asset["size"] <= 0:
            raise ValueError(f"Empty or invalid asset: {asset['name']}")
        if not re.fullmatch(r"sha256:[0-9a-fA-F]{64}", asset.get("digest") or ""):
            raise ValueError(f"Missing GitHub SHA256 digest: {asset['name']}")
    for payload in PAYLOADS:
        checksum_name = payload + ".sha256"
        raw = (checksum_directory / checksum_name).read_bytes()
        if "sha256:" + hashlib.sha256(raw).hexdigest() != by_name[checksum_name]["digest"].lower():
            raise ValueError(f"Downloaded checksum does not match GitHub digest: {checksum_name}")
        # Accept sha256sum output, a bare hash, or a packaging runner's absolute file path.
        match = re.fullmatch(r"([0-9a-fA-F]{64})(?:\s+\*?(.+))?\s*", raw.decode("utf-8-sig").strip())
        if not match or "sha256:" + match[1].lower() != by_name[payload]["digest"].lower():
            raise ValueError(f"Payload SHA256 mismatch: {payload}")
        if match[2] and match[2].replace("\\", "/").rsplit("/", 1)[-1] != payload:
            raise ValueError(f"Checksum names another payload: {checksum_name}")


def download_table(tag: str) -> str:
    def link(name: str | None) -> str:
        return f"[Download](https://github.com/{REPOSITORY}/releases/download/{tag}/{name})" if name else "—"

    rows = ["| System | x64 | arm64 |", "| --- | --- | --- |"]
    rows.extend(f"| {system} | {link(x64)} | {link(arm64)} |" for system, x64, arm64 in PLATFORMS)
    return "\n".join(rows)
