"""Read ECMA-335 FieldMarshal blobs directly; managed reflection can omit subtypes.

This verifies emitted metadata, not COM activation or native marshaling behavior.
Only table layouts needed to locate TypeDef/MethodDef/Param/FieldMarshal are read.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def inspect(path):
    data = path.read_bytes()
    def u16(at): return struct.unpack_from("<H", data, at)[0]
    def u32(at): return struct.unpack_from("<I", data, at)[0]
    pe = u32(0x3C)
    require(data[pe:pe + 4] == b"PE\0\0", "PE signature required")
    optional = pe + 24
    sections = optional + u16(pe + 20)
    def rva(value):
        for i in range(u16(pe + 6)):
            at = sections + i * 40
            size, virtual, raw_size, raw = struct.unpack_from("<IIII", data, at + 8)
            if virtual <= value < virtual + max(size, raw_size):
                return raw + value - virtual
        raise RuntimeError("RVA outside bounded PE sections")
    directory = optional + (112 if u16(optional) == 0x20B else 96)
    cli = rva(u32(directory + 14 * 8))
    root = rva(u32(cli + 8))
    require(data[root:root + 4] == b"BSJB", "CLI metadata signature required")
    at = (root + 16 + u32(root + 12) + 3) & ~3
    streams = {}
    count = u16(at + 2)
    at += 4
    for _ in range(count):
        offset, size = u32(at), u32(at + 4)
        name_end = data.index(0, at + 8)
        name = data[at + 8:name_end].decode("ascii")
        streams[name] = (root + offset, size)
        at = (name_end + 4) & ~3
    table = streams["#~"][0]
    heaps = data[table + 6]
    valid = struct.unpack_from("<Q", data, table + 8)[0]
    at = table + 24
    counts = {}
    for i in range(64):
        if valid & (1 << i):
            counts[i] = u32(at)
            at += 4
    def index(kind): return 4 if counts.get(kind, 0) >= 65536 else 2
    def coded(bits, kinds): return 4 if max(counts.get(kind, 0) for kind in kinds) >= (1 << (16 - bits)) else 2
    string = 4 if heaps & 1 else 2
    guid = 4 if heaps & 2 else 2
    blob = 4 if heaps & 4 else 2
    td = coded(2, [2, 1, 27])
    schemas = {
        0: [2, string, guid, guid, guid],
        1: [coded(2, [0, 26, 35, 1]), string, string],
        2: [4, string, string, td, index(4), index(6)],
        3: [index(4)], 4: [2, string, blob], 5: [index(6)],
        6: [4, 2, 2, string, blob, index(8)], 7: [index(8)], 8: [2, 2, string],
        9: [index(2), td], 10: [coded(3, [2, 1, 26, 6, 27]), string, blob],
        11: [1, 1, coded(2, [4, 8, 23]), blob],
        12: [coded(5, [6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32, 35, 38, 39, 40, 42, 44, 43]), coded(3, [6, 10]), blob],
        13: [coded(1, [4, 8]), blob],
    }
    starts = {}
    for kind in range(14):
        starts[kind] = at
        at += counts.get(kind, 0) * sum(schemas[kind])
    def row(kind, number):
        require(1 <= number <= counts[kind], "Metadata row range")
        at = starts[kind] + (number - 1) * sum(schemas[kind])
        values = []
        for width in schemas[kind]:
            values.append(int.from_bytes(data[at:at + width], "little"))
            at += width
        return values
    def text(offset):
        at = streams["#Strings"][0] + offset
        return data[at:data.index(0, at)].decode("utf-8")
    def compressed(at):
        first = data[at]
        if first < 128: return first, at + 1
        if first < 192: return ((first & 63) << 8) | data[at + 1], at + 2
        require(first < 224, "Invalid compressed unsigned metadata value")
        return ((first & 31) << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3], at + 4
    marshals = {}
    for number in range(1, counts.get(13, 0) + 1):
        parent, offset = row(13, number)
        length, begin = compressed(streams["#Blob"][0] + offset)
        marshals[parent] = data[begin:begin + length]
    target = next(number for number in range(1, counts[2] + 1) if text(row(2, number)[1]) == "IPdfAutomation")
    first = row(2, target)[5]
    end = row(2, target + 1)[5] if target < counts[2] else counts[6] + 1
    expected_order = ["Configure", "OpenBytes", "OpenFile", "get_PageCount", "ExtractText", "SelectPages", "SaveBytes", "SaveFile", "GetFieldNames", "Cancel", "Close", "get_LastDiagnosticCode", "get_RuntimeIdentity"]
    require([text(row(6, number)[3]) for number in range(first, end)] == expected_order, "Frozen native dual-interface method order differs")
    found = {}
    for number in range(first, end):
        method = row(6, number)
        name = text(method[3])
        last = row(6, number + 1)[5] if number < counts[6] else counts[8] + 1
        for param in range(method[5], last):
            sequence = row(8, param)[1]
            if (name, sequence) in [("OpenBytes", 1), ("SaveBytes", 0), ("GetFieldNames", 0)]:
                found[name] = marshals[(param << 1) | 1].hex()
    require(found == {"OpenBytes": "1d11", "SaveBytes": "1d11", "GetFieldNames": "1d0c"}, "Exact SAFEARRAY(UI1/VARIANT) native descriptors disagree: " + str(found))
    return {"assembly": str(path), "sha256": hashlib.sha256(data).hexdigest(), "fieldMarshal": found, "actualComActivationQualified": False}


parser = argparse.ArgumentParser()
parser.add_argument("assemblies", nargs="+", type=Path)
parser.add_argument("--output", required=True, type=Path)
args = parser.parse_args()
records = [inspect(path) for path in args.assemblies]
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps({"schemaVersion": 1, "actualComActivationQualified": False, "assemblies": records}, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"assemblies": len(records), "exactSafeArrayDescriptors": True, "actualComActivationQualified": False}))
