"""Generate tiny synthetic Amcache hives; no real endpoint evidence is included."""
from pathlib import Path
import struct

TIMESTAMP = 134360145301234567  # 2026-10-09T10:15:30.1234567Z, Windows FILETIME


class Hive:
    def __init__(self):
        self.cells = bytearray(32)

    def cell(self, data):
        offset = len(self.cells)
        size = (len(data) + 4 + 7) & ~7
        self.cells += struct.pack("<i", -size) + data + bytes(size - len(data) - 4)
        return offset

    def key(self, name, children=None, values=None, parent=0xFFFFFFFF):
        name_bytes = name.encode("ascii")
        nk = bytearray(76) + name_bytes
        nk[:2] = b"nk"
        struct.pack_into("<H", nk, 2, 0x20 | (2 if parent == 0xFFFFFFFF else 0))
        struct.pack_into("<Q", nk, 4, TIMESTAMP)
        struct.pack_into("<I", nk, 16, parent)
        struct.pack_into("<H", nk, 72, len(name_bytes))
        offset = self.cell(nk)
        subkeys = [self.key(k, **v, parent=offset) for k, v in (children or {}).items()]
        struct.pack_into("<I", nk, 20, len(subkeys))
        struct.pack_into("<I", nk, 28, self.cell(b"li" + struct.pack("<H", len(subkeys)) +
                         b"".join(struct.pack("<I", k) for k in subkeys)) if subkeys else 0xFFFFFFFF)
        struct.pack_into("<I", nk, 32, 0xFFFFFFFF)
        value_offsets = []
        for value_name, value in (values or {}).items():
            value_name_bytes = value_name.encode("ascii")
            data = (str(value) + "\0").encode("utf-16le")
            data_offset = self.cell(data)
            vk = struct.pack("<2sHIIIHH", b"vk", len(value_name_bytes), len(data), data_offset, 1, 1, 0)
            value_offsets.append(self.cell(vk + value_name_bytes))
        struct.pack_into("<I", nk, 36, len(value_offsets))
        struct.pack_into("<I", nk, 40, self.cell(b"".join(struct.pack("<I", v) for v in value_offsets))
                         if value_offsets else 0xFFFFFFFF)
        struct.pack_into("<II", nk, 44, 0xFFFFFFFF, 0xFFFFFFFF)
        self.cells[offset + 4:offset + 4 + len(nk)] = nk
        return offset

    def save(self, path, children):
        root = self.key("Amcache", children={"Root": {"children": children}})
        size = (len(self.cells) + 4 + 4095) & ~4095
        free_size = size - len(self.cells)
        self.cells += struct.pack("<i", free_size) + bytes(free_size - 4)
        self.cells[:4] = b"hbin"
        struct.pack_into("<II", self.cells, 4, 0, size)
        header = bytearray(4096)
        header[:4] = b"regf"
        struct.pack_into("<IIQIIIIIII", header, 4, 1, 1, TIMESTAMP, 1, 5, 0, 1, root, size, 1)
        checksum = 0
        for pos in range(0, 508, 4):
            checksum ^= struct.unpack_from("<I", header, pos)[0]
        struct.pack_into("<I", header, 508, checksum)
        path.write_bytes(header + self.cells)


def fixtures(directory):
    hashes = ["a" * 40, "b" * 40, "a" * 40, "b" * 40]
    files_new, files_old = {}, {}
    for n, sha in enumerate(hashes, 1):
        associated = n > 2
        path = f"C:\\Synthetic\\file{n}.exe"
        files_new[f"file{n}"] = {"values": {
            "LowerCaseLongPath": path, "Name": f"file{n}.exe", "FileId": "0000" + sha,
            "ProgramId": "program-1" if associated else "", "BinaryType": "pe64",
            "BinFileVersion": "1.2.3.4", "BinProductVersion": "1.2.3.4", "Size": "123",
            "LinkDate": "bad date" if n == 1 else "10/09/2026 10:15:30.1234567",
            "IsPeFile": "1", "Description": "Synthetic café\nsecond line"
        }}
        files_old[f"0000{n:04x}"] = {"values": {
            "15": path, "101": "0000" + sha, "100": "program-1" if associated else "",
            "10": "2", "a": "123", "b": "18446744073709551615", "6": "123",
            "12": str(TIMESTAMP), "c": "Synthetic café\nsecond line"
        }}
    Hive().save(directory / "new.hve", {
        "InventoryApplicationFile": {"children": files_new},
        "InventoryApplication": {"children": {"program-1": {"values": {
            "ProgramId": "program-1", "Name": "Synthetic program", "InstallDate": "10/09/2026 10:15:30",
            "InstallDateArpLastModified": "20261009", "InstallDateFromLinkFile": "invalid"
        }}}},
        "InventoryApplicationShortcut": {"children": {"shortcut-1": {"values": {"Path": "C:\\Synthetic\\test.lnk"}}}},
        "InventoryDriverBinary": {"children": {"driver-1": {"values": {"DriverName": "test.sys"}}}},
        "InventoryDeviceContainer": {"children": {"container-1": {"values": {"FriendlyName": "Synthetic device"}}}},
        "InventoryDriverPackage": {"children": {"package-1": {"values": {"Date": "10/09/2026"}}}},
        "InventoryDevicePnp": {"children": {"pnp-1": {"values": {"DriverVerDate": "10/09/2026"}}}}
    })
    Hive().save(directory / "old.hve", {
        "File": {"children": {"volume-1": {"children": files_old}}},
        "Programs": {"children": {"program-1": {"values": {"0": "Synthetic program", "1": "1.2.3.4"}}}}
    })


if __name__ == "__main__":
    fixtures(Path(__file__).parent)
