"""Original static-binary fixtures and actual source-fixture offline installs; no shipping engine qualification."""
import argparse
import concurrent.futures
import copy
import importlib.util
import json
import os
from pathlib import Path
import shutil
import socket
import struct
import subprocess
import sys
import uuid

spec = importlib.util.spec_from_file_location("native_distribution", Path(__file__).with_name("native-distribution.py"))
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)


def pe(machine):
    data = bytearray(1024)
    data[:2] = b"MZ"
    struct.pack_into("<I", data, 60, 128)
    data[128:132] = b"PE\0\0"
    size, magic, offset = (224, 0x10b, 92) if machine == 0x14c else (240, 0x20b, 108)
    struct.pack_into("<HH", data, 132, machine, 1)
    struct.pack_into("<H", data, 148, size)
    struct.pack_into("<H", data, 152, magic)
    struct.pack_into("<I", data, 152 + offset, 16)
    struct.pack_into("<IIII", data, 152 + size + 8, 512, 4096, 512, 512)
    return data


def elf(machine):
    data = bytearray(128)
    data[:7] = b"\x7fELF\x02\x01\x01"
    struct.pack_into("<HH", data, 16, 3, machine)
    struct.pack_into("<Q", data, 32, 64)
    struct.pack_into("<HH", data, 54, 56, 1)
    struct.pack_into("<IIQQQQQQ", data, 64, 1, 5, 0, 0, 0, 128, 128, 4096)
    return data


def macho(cpu):
    data = bytearray(32)
    data[:4] = b"\xcf\xfa\xed\xfe"
    struct.pack_into("<IIIII", data, 4, cpu, 0, 6, 0, 0)
    return data


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--output", type=Path, default=Path("artifacts/contracts/native-distribution"))
    args = p.parse_args()
    root = args.output.absolute() / uuid.uuid4().hex
    root.mkdir(parents=True, mode=0o700)
    cases = []
    def passed(name): cases.append({"case": name, "passed": True})
    def reject(name, operation):
        try: operation()
        except (native.ProvisionError, OSError, ValueError, KeyError, TypeError): passed(name)
        else: raise AssertionError("Required rejection absent: " + name)
    fixtures = [("win-x86", pe(0x14c)), ("win-x64", pe(0x8664)), ("win-arm64", pe(0xaa64)), ("linux-x64", elf(62)), ("linux-arm64", elf(183)), ("osx-x64", macho(0x1000007)), ("osx-arm64", macho(0x100000c))]
    for rid, data in fixtures:
        file = root / (rid + ".bin")
        file.write_bytes(data)
        assert native.binary_metadata(file)["rid"] == rid
        passed("original-static-header-" + rid)
        for size in (1, 6, 17):
            bad = root / (rid + "-truncated-" + str(size))
            bad.write_bytes(data[:size])
            reject("truncated-" + rid + "-" + str(size), lambda: native.binary_metadata(bad))
    imported = pe(0x8664)
    struct.pack_into("<II", imported, 152 + 112 + 8, 4096, 40)
    struct.pack_into("<IIIII", imported, 512, 0, 0, 0, 4160, 0)
    imported[576:589] = b"original.dll\0"
    file = root / "pe-import.bin"
    file.write_bytes(imported)
    assert native.binary_metadata(file)["imports"] == ["original.dll"]
    passed("original-pe-import-table")
    imported = bytearray(512)
    imported[:7] = b"\x7fELF\x02\x01\x01"
    struct.pack_into("<HH", imported, 16, 3, 62)
    struct.pack_into("<Q", imported, 32, 64)
    struct.pack_into("<HH", imported, 54, 56, 2)
    struct.pack_into("<IIQQQQQQ", imported, 64, 1, 5, 0, 4096, 0, 512, 512, 4096)
    struct.pack_into("<IIQQQQQQ", imported, 120, 2, 6, 192, 4288, 0, 80, 80, 8)
    strings = b"liboriginal.so\0$ORIGIN\0"
    imported[320:320 + len(strings)] = strings
    for at, tag, value in [(192, 5, 4416), (208, 10, len(strings)), (224, 1, 0), (240, 29, 15), (256, 0, 0)]:
        struct.pack_into("<qQ", imported, at, tag, value)
    file = root / "elf-import.bin"
    file.write_bytes(imported)
    assert native.binary_metadata(file)["imports"] == ["liboriginal.so"] and native.binary_metadata(file)["searchPaths"] == ["$ORIGIN"]
    passed("original-elf-dynamic-import-and-runpath")
    imported = macho(0x1000007)
    name = b"@rpath/original.dylib\0"
    width = ((24 + len(name) + 7) // 8) * 8
    imported.extend(bytearray(width))
    struct.pack_into("<II", imported, 16, 1, width)
    struct.pack_into("<III", imported, 32, 12, width, 24)
    imported[56:56 + len(name)] = name
    file = root / "macho-import.bin"
    file.write_bytes(imported)
    assert native.binary_metadata(file)["imports"] == ["@rpath/original.dylib"]
    passed("original-macho-load-command")
    source = root / "source"
    (source / "bin").mkdir(parents=True)
    (source / "notices").mkdir()
    (source / "bin/engine.bin").write_bytes(pe(0x8664))
    (source / "notices/MIT.txt").write_text("Original inert test fixture, MIT license.\n", encoding="utf-8")
    def asset(path, role):
        file = source / path
        return {"path": path, "length": file.stat().st_size, "sha256": native.digest(file), "role": role, "version": "original-fixture-v1", "origin": "urn:fluent-pdf:original-source-fixture", "license": "MIT", "notice": "notices/MIT.txt", "imports": [], "searchPaths": []}
    manifest = {"schemaVersion": 1, "runtimeIdentifier": "win-x64", "engineId": "original.static.fixture", "engineVersion": "1", "abi": "inert-no-execution", "maximumTotalBytes": 1048576, "prerequisites": ["Source fixture only; no runtime ABI qualification"], "assets": [asset("bin/engine.bin", "library"), asset("notices/MIT.txt", "notice")]}
    def capture(value, name):
        path = root / (name + ".json")
        path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
        return native.load_manifest(path, native.digest(path))
    valid, raw = capture(manifest, "valid")
    assert len(native.verify(valid, source)["verifiedAssets"]) == 2
    passed("exact-manifest-and-notices")
    for name, mutation in [
        ("traversal", lambda m: m["assets"][0].update(path="../outside")),
        ("missing-notice", lambda m: m["assets"][0].update(notice="missing.txt")),
        ("blank-license", lambda m: m["assets"][0].update(license="")),
        ("credential-origin", lambda m: m["assets"][0].update(origin="https://private:secret@example.invalid/file")),
        ("size-budget", lambda m: m.update(maximumTotalBytes=1)),
        ("case-collision", lambda m: m["assets"].append(dict(m["assets"][0], path="BIN/engine.bin"))),
        ("missing-import-target", lambda m: m["assets"][0].update(imports=[{"name":"absent.dll","kind":"asset","target":"absent.dll"}])),
        ("unknown-property", lambda m: m.update(qualified=True)),
    ]:
        value = copy.deepcopy(manifest)
        mutation(value)
        reject(name, lambda: capture(value, name))
    wrong = copy.deepcopy(valid)
    wrong["runtimeIdentifier"] = "linux-x64"
    reject("wrong-runtime-architecture", lambda: native.verify(wrong, source))
    wrong = copy.deepcopy(valid)
    wrong["assets"][0]["sha256"] = "0" * 64
    reject("wrong-file-hash", lambda: native.verify(wrong, source))
    duplicate = root / "duplicate.json"
    duplicate.write_text('{"schemaVersion":1,"schemaVersion":1}', encoding="utf-8")
    reject("duplicate-json-property", lambda: native.load_manifest(duplicate, native.digest(duplicate)))
    for path in ("/absolute", "a//b", "a/../b", "a\\b", "NUL.txt", "a:stream", "a.", "provision-manifest.json"):
        reject("portable-path-" + path, lambda: native.portable(path))
    linked = root / "owned-hardlink.bin"
    os.link(source / "bin/engine.bin", linked)
    try:
        reject("hard-linked-source-file", lambda: native.protected(source / "bin/engine.bin"))
    finally:
        assert root in linked.parents
        linked.unlink()
    real_socket = socket.socket
    calls = []
    def deny_socket(*a, **kw):
        calls.append(True)
        raise AssertionError("Provisioning attempted network access")
    socket.socket = deny_socket
    try:
        installed = root / "installed"
        result = native.install(valid, raw, source, installed)
        assert result["installed"] and not result["productionQualified"] and (installed / "provision-manifest.json").read_bytes() == raw
        passed("nested-offline-atomic-install")
        reject("existing-destination", lambda: native.install(valid, raw, source, installed))
        assert (installed / "bin/engine.bin").read_bytes() == (source / "bin/engine.bin").read_bytes()
        destination = root / "concurrent"
        def contender(_):
            try: return native.install(valid, raw, source, destination)["installed"]
            except native.ProvisionError: return False
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            assert list(pool.map(contender, range(8))).count(True) == 1
        assert native.verify(valid, destination)["staticImportClosureVerified"]
        assert not list(root.glob(".fluent-pdf-provision-*"))
        passed("eight-concurrent-installs-one-commit-no-owned-stage-leak")
        real_commit = native.atomic_new_directory
        def interrupt_commit(*args): raise KeyboardInterrupt()
        native.atomic_new_directory = interrupt_commit
        try:
            try: native.install(valid, raw, source, root / "interrupted")
            except KeyboardInterrupt: pass
            else: raise AssertionError("Required installation interruption absent")
            assert not (root / "interrupted").exists() and not list(root.glob(".fluent-pdf-provision-*"))
            passed("interrupted-before-commit-no-install-or-owned-stage")
        finally:
            native.atomic_new_directory = real_commit
        original_verify = native.verify
        original_bytes = (source / "bin/engine.bin").read_bytes()
        def change_after_verification(m, tree):
            result = original_verify(m, tree)
            if tree == source:
                modified = bytearray(original_bytes)
                modified[-1] ^= 1
                (source / "bin/engine.bin").write_bytes(modified)
            return result
        native.verify = change_after_verification
        try:
            reject("source-changed-after-preflight", lambda: native.install(valid, raw, source, root / "changed-source"))
            assert not (root / "changed-source").exists() and not list(root.glob(".fluent-pdf-provision-*"))
        finally:
            native.verify = original_verify
            (source / "bin/engine.bin").write_bytes(original_bytes)
        actual = json.loads(Path("artifacts/native-fixtures/native-fixtures.json").read_text(encoding="utf-8-sig"))
        actual_installs = []
        for deployment in actual["deployments"]:
            rid = deployment["rid"]
            original = Path("artifacts/native-fixtures") / rid
            original = original.absolute()
            executable = "worker.exe" if rid.startswith("win-") else "worker"
            suffix = "dll" if rid.startswith("win-") else "dylib" if rid.startswith("osx-") else "so"
            names = [executable, "bridge." + suffix, "dependent." + suffix, "required." + suffix]
            tree = root / ("actual-source-" + rid)
            tree.mkdir()
            for name in names:
                pinned = next(f for f in deployment["files"] if f["name"] == name)
                assert native.digest(original / name) == pinned["sha256"].lower() and (original / name).stat().st_size == pinned["length"]
                shutil.copyfile(original / name, tree / name)
            shutil.copyfile(Path("LICENSE"), tree / "MIT.txt")
            entries = []
            for name in names + ["MIT.txt"]:
                file = tree / name
                metadata = native.binary_metadata(file) if name != "MIT.txt" else {"imports": [], "searchPaths": []}
                bindings = []
                for imported in metadata["imports"]:
                    match = next((n for n in names if n.casefold() == imported.split("/")[-1].casefold()), None)
                    bindings.append({"name": imported, "kind": "asset" if match else "system", "target": match or imported})
                entries.append({"path": name, "length": file.stat().st_size, "sha256": native.digest(file), "role": "notice" if name == "MIT.txt" else "executable" if name == executable else "library", "version": "original-source-fixture-v1", "origin": "urn:fluent-pdf:original-source:" + actual["sourceCommit"], "license": "MIT", "notice": "MIT.txt", "imports": bindings, "searchPaths": metadata["searchPaths"]})
            inventory = dict(manifest, runtimeIdentifier=rid, engineId="original.native.worker-fixtures", abi="fluent_pdf_bridge_v1", maximumTotalBytes=sum(x["length"] for x in entries), assets=entries)
            inventory, inventory_raw = capture(inventory, "actual-manifest-" + rid)
            destination = root / ("actual-installed-" + rid)
            report = native.install(inventory, inventory_raw, tree, destination)
            (root / ("actual-install-" + rid + ".json")).write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
            actual_installs.append({"rid": rid, "manifest": "actual-manifest-" + rid + ".json", "manifestSha256": __import__('hashlib').sha256(inventory_raw).hexdigest(), "destination": destination.as_posix(), "verifiedFiles": len(entries)})
            passed("actual-native-source-fixture-install-" + rid)
        assert actual_installs and not calls
    finally:
        socket.socket = real_socket
    shell = shutil.which("pwsh")
    assert shell, "PowerShell7 is required for literal-path source removal checks"
    extra = installed / "caller-owned.txt"
    extra.write_text("preserve this file", encoding="utf-8")
    removal = [shell, "-NoProfile", "-File", str(Path(__file__).with_name("remove-native-installation.ps1").absolute()), "-InstallationDirectory", str(installed), "-ManifestSha256", __import__('hashlib').sha256(raw).hexdigest(), "-PythonExecutable", sys.executable, "-ReportDirectory", str(root / "removal-reports")]
    attempt = subprocess.run(removal, capture_output=True, timeout=30)
    (root / "removal-refused.log").write_bytes(attempt.stdout + attempt.stderr)
    assert attempt.returncode != 0 and extra.read_text(encoding="utf-8") == "preserve this file" and (installed / "bin/engine.bin").exists()
    passed("literal-removal-refuses-unowned-file-without-deletion")
    assert root in extra.parents
    extra.unlink()
    attempt = subprocess.run(removal, capture_output=True, timeout=30)
    (root / "removal-passed.log").write_bytes(attempt.stdout + attempt.stderr)
    assert attempt.returncode == 0 and not installed.exists(), attempt.stderr.decode("utf-8", "replace")
    passed("literal-removal-verifies-and-retains-report")
    report = {"purpose": "offline-source-provisioning-boundary-tests", "productionQualified": False, "cleanAirGappedHostQualification": False, "python": sys.version, "platform": sys.platform, "sourceFixtureCommit": actual["sourceCommit"], "cases": cases, "actualInstalls": actual_installs, "networkSocketAttempts": len(calls)}
    (root / "tests.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("NATIVE_DISTRIBUTION_SOURCE_PASS;" + str(len(cases)) + ";" + root.as_posix())


if __name__ == "__main__":
    main()
