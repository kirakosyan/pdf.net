"""Explicit offline source provisioning. No execution, discovery, download or qualification claim."""
import argparse
import ctypes
import hashlib
import json
import mmap
import os
from pathlib import Path, PurePosixPath
import re
import stat
import struct
import sys
import uuid
from urllib.parse import urlsplit


class ProvisionError(ValueError):
    pass


def require(condition, code):
    if not condition:
        raise ProvisionError(code)


def digest(path, maximum=536870912):
    h = hashlib.sha256()
    total = 0
    with path.open("rb") as f:
        while block := f.read(65536):
            total += len(block)
            require(total <= maximum, "Native.AssetExtent")
            h.update(block)
    return h.hexdigest()


def portable(value):
    require(isinstance(value, str) and 0 < len(value) <= 240, "Native.ManifestPath")
    p = PurePosixPath(value)
    require(not p.is_absolute() and str(p) == value and len(p.parts) <= 16, "Native.ManifestPath")
    for part in p.parts:
        require(re.fullmatch(r"[A-Za-z0-9_@+.-]{1,120}", part) is not None and part not in (".", "..") and not part.endswith("."), "Native.ManifestPath")
        require(not re.fullmatch(r"CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]", part.split(".")[0], re.I), "Native.ManifestPath")
    require(p.parts[0].casefold() != "provision-manifest.json", "Native.ManifestPath")
    return value


def protected(path, directory=False):
    require(path.is_absolute(), "Native.AbsolutePathRequired")
    for p in [*reversed(path.parents), path]:
        info = p.lstat()
        require(not stat.S_ISLNK(info.st_mode) and not getattr(info, "st_file_attributes", 0) & 0x400, "Native.UnsafePath")
    info = path.lstat()
    require(stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode) and info.st_nlink == 1, "Native.UnsafePath")
    return info


def binary_metadata(path):
    """Bounded static imports only; dlopen, code signatures and installed-system ABI remain separate."""
    require(0 < path.stat().st_size <= 536870912, "Native.BinaryExtent")
    with path.open("rb") as f, mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ) as data:
        def span(offset, size):
            require(0 <= offset <= len(data) and 0 <= size <= len(data) - offset, "Native.BinaryTruncated")
            return offset
        def unpack(fmt, offset):
            return struct.unpack_from(fmt, data, span(offset, struct.calcsize(fmt)))
        def string(offset, end):
            span(offset, 1)
            end = min(end, offset + 4096, len(data))
            at = data.find(b"\0", offset, end)
            require(at >= offset and at - offset <= 1024, "Native.ImportExtent")
            try:
                value = data[offset:at].decode("ascii", "strict")
            except UnicodeError:
                raise ProvisionError("Native.ImportEncoding") from None
            require(value and all(32 < ord(c) < 127 for c in value), "Native.ImportEncoding")
            return value
        imports, paths = [], []
        if data[:2] == b"MZ":
            pe, = unpack("<I", 60)
            require(data[span(pe, 24):pe + 4] == b"PE\0\0", "Native.BinaryFormat")
            machine, sections = unpack("<HH", pe + 4)
            rid = {0x14c: "win-x86", 0x8664: "win-x64", 0xaa64: "win-arm64"}.get(machine)
            optional_size, = unpack("<H", pe + 20)
            optional = pe + 24
            span(optional, optional_size)
            magic, = unpack("<H", optional)
            require(rid and magic in (0x10b, 0x20b) and 0 < sections <= 96, "Native.BinaryArchitecture")
            require((magic == 0x10b) == (rid == "win-x86"), "Native.BinaryArchitecture")
            count_offset, dirs_offset = (92, 96) if magic == 0x10b else (108, 112)
            count, = unpack("<I", optional + count_offset)
            require(count <= 16 and dirs_offset + count * 8 <= optional_size, "Native.BinaryFormat")
            table = optional + optional_size
            span(table, sections * 40)
            ranges = []
            for n in range(sections):
                virtual_size, virtual, raw_size, raw = unpack("<IIII", table + n * 40 + 8)
                span(raw, raw_size)
                ranges.append((virtual, raw_size, raw))
            def rva(address, size):
                matches = [(v, s, r) for v, s, r in ranges if v <= address and address - v + size <= s]
                require(len(matches) == 1, "Native.BinaryRva")
                v, _, raw = matches[0]
                return span(raw + address - v, size)
            for directory, width in [(1, 20), (13, 32)]:
                if count <= directory: continue
                address, size = unpack("<II", optional + dirs_offset + directory * 8)
                if address == size == 0: continue
                require(address > 0 and width <= size <= 1048576, "Native.ImportExtent")
                base = rva(address, size)
                terminated = False
                for at in range(base, base + size - width + 1, width):
                    values = unpack("<" + "I" * (width // 4), at)
                    if not any(values):
                        terminated = True
                        break
                    if directory == 13:
                        require(values[0] == 1, "Native.DelayImportProfile")
                        name = values[1]
                    else:
                        name = values[3]
                    offset = rva(name, 1)
                    imports.append(string(offset, min(r + s for v, s, r in ranges if v <= name and name - v < s)))
                require(terminated, "Native.ImportTerminator")
        elif data[:4] == b"\x7fELF":
            require(data[4:7] == b"\x02\x01\x01", "Native.BinaryFormat")
            kind, machine = unpack("<HH", 16)
            rid = {62: "linux-x64", 183: "linux-arm64"}.get(machine)
            require(rid and kind in (2, 3), "Native.BinaryArchitecture")
            phoff, = unpack("<Q", 32)
            size, count = unpack("<HH", 54)
            require(size == 56 and 0 < count <= 4096, "Native.BinaryFormat")
            span(phoff, size * count)
            loads, dynamics = [], []
            for n in range(count):
                ptype, _, offset, address, _, file_size, _, _ = unpack("<IIQQQQQQ", phoff + n * size)
                span(offset, file_size)
                if ptype == 1: loads.append((address, file_size, offset))
                if ptype == 2: dynamics.append((offset, file_size))
            require(len(dynamics) <= 1, "Native.BinaryFormat")
            if dynamics:
                offset, size = dynamics[0]
                require(size % 16 == 0 and size <= 1048576, "Native.ImportExtent")
                tags = []
                for at in range(offset, offset + size, 16):
                    tag, value = unpack("<qQ", at)
                    if tag == 0: break
                    tags.append((tag, value))
                else: raise ProvisionError("Native.ImportTerminator")
                string_tables = [v for t, v in tags if t == 5]
                lengths = [v for t, v in tags if t == 10]
                require(len(string_tables) == len(lengths) == 1, "Native.BinaryStringTable")
                address, length = string_tables[0], lengths[0]
                matches = [(v, s, o) for v, s, o in loads if v <= address and address - v + length <= s]
                require(len(matches) == 1, "Native.BinaryStringTable")
                v, _, o = matches[0]
                base = o + address - v
                for tag, value in tags:
                    if tag in (1, 15, 29):
                        require(value < length, "Native.BinaryStringTable")
                        text = string(base + value, base + length)
                        (imports if tag == 1 else paths).append(text)
        elif data[:4] == b"\xcf\xfa\xed\xfe":
            cpu, _, kind, count, size = unpack("<IIIII", 4)
            rid = {0x1000007: "osx-x64", 0x100000c: "osx-arm64"}.get(cpu)
            require(rid and kind in (2, 6, 8) and count <= 8192, "Native.BinaryArchitecture")
            span(32, size)
            at = 32
            for _ in range(count):
                command, length = unpack("<II", at)
                require(length >= 8 and length % 8 == 0 and at + length <= 32 + size, "Native.BinaryCommand")
                if command in (12, 0x80000018, 0x8000001f, 0x80000023, 0x20, 0x8000001c):
                    name, = unpack("<I", at + 8)
                    require(12 <= name < length, "Native.BinaryCommand")
                    (paths if command == 0x8000001c else imports).append(string(at + name, at + length))
                at += length
            require(at == 32 + size, "Native.BinaryCommand")
        else:
            raise ProvisionError("Native.BinaryFormat")
        require(len(imports) <= 4096 and len(paths) <= 128, "Native.ImportExtent")
        return {"rid": rid, "imports": sorted(set(imports)), "searchPaths": sorted(set(paths)), "staticOnly": True}


def pairs(values):
    result = {}
    for key, value in values:
        require(key not in result, "Native.DuplicateJsonProperty")
        result[key] = value
    return result


def text(value, maximum=1024):
    require(isinstance(value, str) and 0 < len(value) <= maximum and all(ord(c) >= 32 for c in value), "Native.ManifestText")
    return value


def load_manifest(path, expected):
    protected(path)
    require(path.stat().st_size <= 4194304 and re.fullmatch(r"[0-9a-f]{64}", expected or ""), "Native.ManifestExtent")
    with path.open("rb") as file:
        raw = file.read(4194305)
    require(len(raw) <= 4194304, "Native.ManifestExtent")
    require(hashlib.sha256(raw).hexdigest() == expected, "Native.ManifestHashMismatch")
    manifest = json.loads(raw.decode("utf-8-sig"), object_pairs_hook=pairs)
    require(set(manifest) == {"schemaVersion", "runtimeIdentifier", "engineId", "engineVersion", "abi", "maximumTotalBytes", "prerequisites", "assets"}, "Native.ManifestShape")
    require(type(manifest["schemaVersion"]) is int and manifest["schemaVersion"] == 1, "Native.ManifestVersion")
    require(manifest["runtimeIdentifier"] in {"win-x86", "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"}, "Native.ManifestRid")
    for field in ("engineId", "engineVersion", "abi"): text(manifest[field], 128)
    require(isinstance(manifest["prerequisites"], list) and 0 < len(manifest["prerequisites"]) <= 64, "Native.ManifestPrerequisites")
    for value in manifest["prerequisites"]: text(value)
    require(type(manifest["maximumTotalBytes"]) is int and 0 < manifest["maximumTotalBytes"] <= 10737418240, "Native.ManifestExtent")
    assets = manifest["assets"]
    require(isinstance(assets, list) and 0 < len(assets) <= 4096, "Native.ManifestExtent")
    names, total = {}, 0
    for asset in assets:
        require(set(asset) == {"path", "length", "sha256", "role", "version", "origin", "license", "notice", "imports", "searchPaths"}, "Native.AssetShape")
        name = portable(asset["path"])
        require(name.casefold() not in names, "Native.DuplicateAsset")
        names[name.casefold()] = asset
        require(type(asset["length"]) is int and 0 < asset["length"] <= 536870912, "Native.AssetExtent")
        total += asset["length"]
        require(total <= manifest["maximumTotalBytes"], "Native.ManifestExtent")
        require(re.fullmatch(r"[0-9a-f]{64}", asset["sha256"]) is not None, "Native.AssetHash")
        require(asset["role"] in {"executable", "library", "font", "model", "data", "notice"}, "Native.AssetRole")
        for field in ("version", "license"): text(asset[field], 256)
        origin = urlsplit(text(asset["origin"], 2048))
        require(origin.scheme in ("https", "urn") and (origin.scheme != "https" or origin.hostname) and not origin.username and not origin.password and not origin.fragment, "Native.AssetOrigin")
        portable(asset["notice"])
        require(isinstance(asset["imports"], list) and len(asset["imports"]) <= 4096 and isinstance(asset["searchPaths"], list) and len(asset["searchPaths"]) <= 128, "Native.ImportExtent")
        for value in asset["searchPaths"]: text(value)
        bindings = set()
        for dependency in asset["imports"]:
            require(set(dependency) == {"name", "kind", "target"} and dependency["kind"] in ("system", "asset"), "Native.ImportBinding")
            name = text(dependency["name"])
            text(dependency["target"])
            require(name not in bindings, "Native.ImportBinding")
            bindings.add(name)
    basenames = {PurePosixPath(a["path"]).name.casefold() for a in assets}
    for name, asset in names.items():
        require(not any(parent.as_posix().casefold() in names for parent in PurePosixPath(asset["path"]).parents if parent.as_posix() != "."), "Native.AssetPathConflict")
        notice = names.get(asset["notice"].casefold())
        require(notice and notice["path"] == asset["notice"] and notice["role"] == "notice", "Native.AssetNotice")
        for binding in asset["imports"]:
            if binding["kind"] == "asset":
                target = names.get(portable(binding["target"]).casefold())
                require(target and target["path"] == binding["target"] and target["role"] == "library", "Native.ImportBinding")
                require(PurePosixPath(binding["name"]).name.casefold() == PurePosixPath(target["path"]).name.casefold(), "Native.ImportBinding")
            else:
                require(binding["target"] == binding["name"] and PurePosixPath(binding["name"]).name.casefold() not in basenames, "Native.ImportBinding")
    return manifest, raw


def verify(manifest, root):
    protected(root, directory=True)
    result = []
    for asset in manifest["assets"]:
        path = root / asset["path"]
        info = protected(path)
        require(info.st_size == asset["length"], "Native.SizeMismatch")
        require(digest(path) == asset["sha256"], "Native.HashMismatch")
        if asset["role"] in ("executable", "library"):
            binary = binary_metadata(path)
            require(binary["rid"] == manifest["runtimeIdentifier"], "Native.ArchitectureMismatch")
            require(binary["imports"] == sorted(b["name"] for b in asset["imports"]) and binary["searchPaths"] == sorted(asset["searchPaths"]), "Native.ImportClosureMismatch")
            require(digest(path, asset["length"]) == asset["sha256"], "Native.HashMismatch")
        else:
            require(not asset["imports"] and not asset["searchPaths"], "Native.ImportBinding")
        result.append({"path": asset["path"], "sha256": asset["sha256"], "role": asset["role"]})
    return {"schemaVersion": 1, "purpose": "offline-source-provisioning-observation", "runtimeIdentifier": manifest["runtimeIdentifier"], "verifiedAssets": result, "staticImportClosureVerified": True, "systemAbiVerified": False, "redistributionApproved": False, "productionQualified": False}


def atomic_new_directory(source, target):
    require(source.parent == target.parent and not target.exists(), "Native.DestinationExists")
    if sys.platform == "win32":
        api = ctypes.WinDLL("kernel32", use_last_error=True).MoveFileExW
        api.argtypes, api.restype = [ctypes.c_wchar_p, ctypes.c_wchar_p, ctypes.c_uint32], ctypes.c_int
        require(api(str(source), str(target), 8) != 0, "Native.AtomicCommitFailed")
    else:
        library = ctypes.CDLL(None, use_errno=True)
        if sys.platform == "darwin":
            api = getattr(library, "renamex_np", None)
            require(api is not None, "Native.AtomicCommitUnavailable")
            api.argtypes, api.restype = [ctypes.c_char_p, ctypes.c_char_p, ctypes.c_uint], ctypes.c_int
            result = api(os.fsencode(source), os.fsencode(target), 4)
        else:
            api = getattr(library, "renameat2", None)
            require(api is not None, "Native.AtomicCommitUnavailable")
            api.argtypes, api.restype = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint], ctypes.c_int
            result = api(-100, os.fsencode(source), -100, os.fsencode(target), 1)
        require(result == 0, "Native.AtomicCommitFailed")


def install(manifest, raw, source, destination):
    protected(destination.parent, directory=True)
    require(not destination.exists() and not destination.is_symlink(), "Native.DestinationExists")
    verify(manifest, source)
    stage = destination.parent / (".fluent-pdf-provision-" + uuid.uuid4().hex)
    stage.mkdir(mode=0o700)
    owned = []
    owned_directories = {stage}
    try:
        for asset in manifest["assets"]:
            target = stage / asset["path"]
            for relative in reversed(PurePosixPath(asset["path"]).parents):
                directory = stage / str(relative)
                if directory not in owned_directories:
                    directory.mkdir(mode=0o700)
                    owned_directories.add(directory)
            protected(target.parent, directory=True)
            protected(source / asset["path"])
            h, count = hashlib.sha256(), 0
            with (source / asset["path"]).open("rb") as input_file, target.open("xb") as output:
                owned.append(target)
                while block := input_file.read(65536):
                    count += len(block)
                    require(count <= asset["length"], "Native.SizeMismatch")
                    h.update(block)
                    output.write(block)
                output.flush()
                os.fsync(output.fileno())
            require(count == asset["length"] and h.hexdigest() == asset["sha256"], "Native.HashMismatch")
            if os.name != "nt": target.chmod(0o500 if asset["role"] == "executable" else 0o400)
        marker = stage / "provision-manifest.json"
        with marker.open("xb") as f:
            owned.append(marker)
            f.write(raw)
            f.flush()
            os.fsync(f.fileno())
        report = verify(manifest, stage)
        atomic_new_directory(stage, destination)
        report["installed"] = True
        return report
    except BaseException:
        # Delete only the unique owned flat inventory and its empty ancestors; never recursively traverse a target.
        for file in reversed(owned):
            require(stage in file.parents, "Native.CleanupBoundary")
            if file.exists(): file.unlink()
        for directory in sorted(owned_directories, key=lambda p: len(p.parts), reverse=True):
            if directory.exists(): directory.rmdir()
        raise


def main():
    p = argparse.ArgumentParser(description=__doc__)
    commands = p.add_subparsers(dest="command", required=True)
    binary = commands.add_parser("inspect-binary")
    binary.add_argument("path", type=Path)
    for command in ("verify", "install"):
        q = commands.add_parser(command)
        q.add_argument("--manifest", type=Path, required=True)
        q.add_argument("--manifest-sha256", required=True)
        q.add_argument("--source", type=Path, required=True)
        q.add_argument("--report", type=Path, required=True)
        if command == "install": q.add_argument("--destination", type=Path, required=True)
    args = p.parse_args()
    if args.command == "inspect-binary":
        protected(args.path)
        print(json.dumps(binary_metadata(args.path), indent=2))
        return
    manifest, raw = load_manifest(args.manifest, args.manifest_sha256)
    protected(args.report.parent, directory=True)
    require(not args.report.exists() and not args.report.is_symlink(), "Native.ReportExists")
    result = install(manifest, raw, args.source, args.destination) if args.command == "install" else verify(manifest, args.source)
    result["manifestSha256"] = hashlib.sha256(raw).hexdigest()
    with args.report.open("x", encoding="utf-8") as report:
        report.write(json.dumps(result, indent=2) + "\n")
    print("NATIVE_SOURCE_PROVISION_PASS;not-production-qualification")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, KeyError, TypeError, struct.error) as failure:
        print(str(failure) if isinstance(failure, ProvisionError) else "Native.ProvisionFailed", file=sys.stderr)
        sys.exit(1)
