"""Terminate Inno's fixed VERSIONINFO placeholders before signing, preserving the overlay.

Inno copies strings into fixed buffers without their terminator:
https://github.com/jrsoftware/issrc/blob/main/Projects/Src/Compiler.ExeUpdateFunc.pas
Never resize resources or use BeginUpdateResource on a self-extracting installer.
"""

import argparse
import ctypes
import struct
from pathlib import Path


def normalize(path: Path, product_version: str, file_version: str) -> None:
    data = bytearray(path.read_bytes())

    def word(offset: int) -> int:
        return struct.unpack_from("<H", data, offset)[0]

    def dword(offset: int) -> int:
        return struct.unpack_from("<I", data, offset)[0]

    if data[:2] != b"MZ":
        raise ValueError("Not a Windows executable")
    pe = dword(0x3C)
    if data[pe:pe + 4] != b"PE\0\0":
        raise ValueError("Invalid PE signature")
    optional = pe + 24
    magic = word(optional)
    if magic not in (0x10B, 0x20B):
        raise ValueError("Unsupported PE optional header")
    directories = optional + (96 if magic == 0x10B else 112)
    if dword(directories + 4 * 8 + 4):
        raise ValueError("Refusing to modify an already signed installer")
    resource_rva, resource_size = struct.unpack_from("<II", data, directories + 2 * 8)
    sections = optional + word(pe + 20)

    def file_offset(rva: int, size: int) -> int:
        for index in range(word(pe + 6)):
            entry = sections + index * 40
            address = dword(entry + 12)
            raw_size, raw_offset = struct.unpack_from("<II", data, entry + 16)
            if address <= rva and rva + size <= address + raw_size:
                offset = raw_offset + rva - address
                if offset + size <= len(data):
                    return offset
        raise ValueError("Resource points outside the PE sections")

    resource = file_offset(resource_rva, resource_size)

    def entries(relative: int):
        if relative < 0 or relative + 16 > resource_size:
            raise ValueError("Invalid resource directory")
        directory = resource + relative
        count = word(directory + 12) + word(directory + 14)
        if relative + 16 + count * 8 > resource_size:
            raise ValueError("Truncated resource directory")
        for index in range(count):
            yield struct.unpack_from("<II", data, directory + 16 + index * 8)

    def subdirectory(relative: int, identifier: int) -> int:
        matches = [value for name, value in entries(relative) if name == identifier]
        if len(matches) != 1 or not matches[0] & 0x80000000:
            raise ValueError("Expected one VERSIONINFO resource directory")
        return matches[0] & 0x7FFFFFFF

    versions = subdirectory(subdirectory(0, 16), 1)
    expected = {"ProductName": "MarkMello", "ProductVersion": product_version, "FileVersion": file_version}
    modified = 0

    def align(value: int) -> int:
        return (value + 3) & ~3

    def visit(position: int, limit: int, parents: tuple[str, ...], found: set[str]) -> None:
        length, value_length, kind = struct.unpack_from("<HHH", data, position)
        end = position + length
        if length < 6 or end > limit:
            raise ValueError("Invalid version node length")
        cursor = position + 6
        key_start = cursor
        while cursor + 2 <= end and word(cursor) != 0:
            cursor += 2
        if cursor + 2 > end:
            raise ValueError("Unterminated version key")
        key = data[key_start:cursor].decode("utf-16-le")
        value_start = align(cursor + 2)
        value_size = value_length * 2 if kind == 1 else value_length
        if value_start + value_size > end:
            raise ValueError("Invalid version value length")
        children = align(value_start + value_size)
        if len(parents) == 3 and parents[:2] == ("VS_VERSION_INFO", "StringFileInfo") and key in expected:
            current = data[value_start:value_start + value_size].decode("utf-16-le").rstrip("\0")
            if current.rstrip(" ") != expected[key]:
                raise ValueError(f"Unexpected {key}: {current!r}")
            replacement = (expected[key] + "\0").encode("utf-16-le")
            if kind != 1 or len(replacement) > value_size:
                raise ValueError(f"{key} does not fit the compiler's version allocation")
            data[value_start:value_start + value_size] = replacement + bytes(value_size - len(replacement))
            struct.pack_into("<H", data, position + 2, len(replacement) // 2)
            found.add(key)
        else:
            while children + 6 <= end:
                child_length = word(children)
                if child_length == 0:
                    break
                visit(children, end, parents + (key,), found)
                children = align(children + child_length)

    for _, leaf in entries(versions):
        if leaf & 0x80000000 or leaf + 16 > resource_size:
            raise ValueError("Invalid version resource data entry")
        rva, size = struct.unpack_from("<II", data, resource + leaf)
        offset = file_offset(rva, size)
        found: set[str] = set()
        visit(offset, offset + size, (), found)
        if found != expected.keys():
            raise ValueError("Missing required product/version fields")
        modified += 1
    if not modified:
        raise ValueError("Missing version resources")

    # Only existing buffers change; PE section offsets and appended installer data stay intact.
    path.write_bytes(data)
    imagehlp = ctypes.WinDLL("imagehlp", use_last_error=True)
    checksum = imagehlp.MapFileAndCheckSumW
    checksum.argtypes = [ctypes.c_wchar_p, ctypes.POINTER(ctypes.c_uint32), ctypes.POINTER(ctypes.c_uint32)]
    checksum.restype = ctypes.c_uint32
    old_sum, new_sum = ctypes.c_uint32(), ctypes.c_uint32()
    result = checksum(str(path.resolve()), ctypes.byref(old_sum), ctypes.byref(new_sum))
    if result != 0:
        raise OSError(f"MapFileAndCheckSumW failed: {result}")
    with path.open("r+b") as stream:
        stream.seek(optional + 64)
        stream.write(struct.pack("<I", new_sum.value))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("installer", type=Path)
    parser.add_argument("--product-version", required=True)
    parser.add_argument("--file-version", required=True)
    options = parser.parse_args()
    normalize(options.installer, options.product_version, options.file_version)
