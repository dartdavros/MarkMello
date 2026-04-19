from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = ROOT / "packaging" / "assets" / "base-1024.png"
WINDOW_ICON = ROOT / "src" / "MarkMello.Presentation" / "Assets" / "Icons" / "markmello.ico"
INSTALLER_ICON = ROOT / "packaging" / "windows" / "markmello-installer.ico"
LINUX_ICON = ROOT / "packaging" / "linux" / "markmello.png"
MAC_ICON = ROOT / "packaging" / "macos" / "MarkMello.icns"
MAC_ICONSET = ROOT / "packaging" / "macos" / "AppIcon.iconset"

ICO_SIZES = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)]
MAC_ICONSET_SPECS = [
    ("icon_16x16.png", 16),
    ("icon_16x16@2x.png", 32),
    ("icon_32x32.png", 32),
    ("icon_32x32@2x.png", 64),
    ("icon_128x128.png", 128),
    ("icon_128x128@2x.png", 256),
    ("icon_256x256.png", 256),
    ("icon_256x256@2x.png", 512),
    ("icon_512x512.png", 512),
    ("icon_512x512@2x.png", 1024),
]
ICNS_SIZES = [(16, 16), (32, 32), (64, 64), (128, 128), (256, 256), (512, 512), (1024, 1024)]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate Windows, macOS, and Linux app icons from one master image.")
    parser.add_argument(
        "--source",
        type=Path,
        default=DEFAULT_SOURCE,
        help=f"PNG master icon source. Defaults to {DEFAULT_SOURCE}",
    )
    return parser.parse_args()


def make_square(image: Image.Image) -> Image.Image:
    side = max(image.size)
    if image.size == (side, side):
        return image

    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    offset = ((side - image.width) // 2, (side - image.height) // 2)
    square.alpha_composite(image, offset)
    return square


def prepare_image(source: Path) -> Image.Image:
    image = Image.open(source).convert("RGBA")
    return make_square(image)


def resize(image: Image.Image, edge: int) -> Image.Image:
    return image.resize((edge, edge), Image.Resampling.LANCZOS)


def ensure_parent(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


def save_ico(image: Image.Image, path: Path) -> None:
    ensure_parent(path)
    image.save(path, format="ICO", sizes=ICO_SIZES)


def save_linux_png(image: Image.Image, path: Path) -> None:
    ensure_parent(path)
    resize(image, 512).save(path, format="PNG")


def save_macos_icons(image: Image.Image) -> None:
    MAC_ICONSET.mkdir(parents=True, exist_ok=True)

    for filename, edge in MAC_ICONSET_SPECS:
        resize(image, edge).save(MAC_ICONSET / filename, format="PNG")

    ensure_parent(MAC_ICON)
    image.save(MAC_ICON, format="ICNS", sizes=ICNS_SIZES)


def main() -> None:
    args = parse_args()
    source = args.source.resolve()
    if not source.is_file():
        raise SystemExit(f"Icon source not found: {source}")

    image = prepare_image(source)

    save_ico(image, WINDOW_ICON)
    save_ico(image, INSTALLER_ICON)
    save_linux_png(image, LINUX_ICON)
    save_macos_icons(image)

    print(f"Generated app icons from {source}")
    print(f"- {WINDOW_ICON}")
    print(f"- {INSTALLER_ICON}")
    print(f"- {LINUX_ICON}")
    print(f"- {MAC_ICON}")
    print(f"- {MAC_ICONSET}")


if __name__ == "__main__":
    main()
