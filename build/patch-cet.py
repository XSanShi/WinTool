"""Clear the PE IMAGE_DEBUG_TYPE_EX_DLLCHARACTERISTICS (type 20) value.

Some older Windows 10 installations cannot start the .NET SDK's CET-marked
csc.exe or VBCSCompiler.exe. Run this script against copies of those files to
clear the marker. Never modify the files under Program Files.

Usage:
    python build/patch-cet.py <exe> [<exe> ...]
"""

import struct
import sys


def find_type20(data: bytes) -> list[int]:
    e_lfanew = struct.unpack_from("<I", data, 0x3C)[0]
    coff = e_lfanew + 4
    num_sections = struct.unpack_from("<H", data, coff + 2)[0]
    opt_size = struct.unpack_from("<H", data, coff + 16)[0]
    opt = coff + 20
    magic = struct.unpack_from("<H", data, opt)[0]
    data_dir = opt + (112 if magic == 0x20B else 96)
    dbg_rva, dbg_size = struct.unpack_from("<II", data, data_dir + 6 * 8)

    sections = []
    sec_table = opt + opt_size
    for i in range(num_sections):
        vsize, vaddr, raw_size, raw_ptr = struct.unpack_from(
            "<IIII", data, sec_table + i * 40 + 8
        )
        sections.append((vaddr, vsize, raw_ptr, raw_size))

    def rva_to_offset(rva: int) -> int | None:
        for vaddr, vsize, raw_ptr, raw_size in sections:
            if vaddr <= rva < vaddr + max(vsize, raw_size):
                return raw_ptr + (rva - vaddr)
        return None

    offsets = []
    if not dbg_size:
        return offsets
    base = rva_to_offset(dbg_rva)
    if base is None:
        return offsets
    for i in range(dbg_size // 28):
        entry = base + i * 28
        _ch, _ts, _maj, _min, typ, _sz, _addr, ptr = struct.unpack_from(
            "<IIHHIIII", data, entry
        )
        if typ == 20:
            offsets.append(ptr)
    return offsets


def patch(path: str) -> None:
    with open(path, "rb") as fh:
        data = bytearray(fh.read())
    offsets = find_type20(data)
    if not offsets:
        print(f"{path}: no type 20 debug entry, skipped")
        return
    for off in offsets:
        value = struct.unpack_from("<I", data, off)[0]
        struct.pack_into("<I", data, off, 0)
        print(f"{path}: type 20 @ {off} 0x{value:x} -> 0x0")
    with open(path, "wb") as fh:
        fh.write(data)


if __name__ == "__main__":
    for arg in sys.argv[1:]:
        patch(arg)
