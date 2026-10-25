#!/usr/bin/env python3
"""Execute the complete offline source bundle on an owned, hardware-backed guest.

The VM lifecycle pattern follows MIT-licensed word.net at
c723fb8aa4ed70fda5562da7f472e6fef54740b7. See third-party/framework-vm/README.md.
Source execution and consistency checks never qualify final SDK packages.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import platform
import re
import shutil
import signal
import stat
import subprocess
import time
import uuid
from urllib.parse import quote
import zipfile

ROOT = Path(__file__).resolve().parents[1]
IMAGE = "dockurr/windows:6.06@sha256:16fd7c39a3e48bb946d29c59dc6015942e5b900a1fa5d7e5552053bfc48ee2ea"
SETUP_FILES = {"source-bundle.zip", "qualify.ps1", "install.bat", "baseline-x86.trx",
               "baseline-x64.trx", "baseline-x86-execution.json", "baseline-x64-execution.json",
               "vc_redist.x86.exe", "vc_redist.x64.exe"}
MAX_BYTES = 10 * 1024**3
MAX_FILES = 30000


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def safe_path(name):
    require(isinstance(name, str) and name and len(name) <= 1024 and
            "\\" not in name and ":" not in name and "\0" not in name,
            "Invalid portable evidence path")
    path = PurePosixPath(name)
    require(not path.is_absolute() and all(p not in {"", ".", ".."} and p == p.rstrip(" .")
                                          for p in name.split("/")), "Escaping or aliased evidence path")
    devices = {"CON", "PRN", "AUX", "NUL"} | {"COM" + str(n) for n in range(1, 10)} | {"LPT" + str(n) for n in range(1, 10)}
    require(not any(p.split(".")[0].upper() in devices for p in path.parts), "Windows device path")
    return path


def checker():
    spec = importlib.util.spec_from_file_location("pdf_runtime_evidence", ROOT / "eng/check-runtime-evidence.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def control_source(name):
    payload = (ROOT / "eng/framework-vm" / name).read_bytes()
    # Match the pinned OEM batch transport without changing any other bytes.
    return payload.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n") if name == "install.bat" else payload


def validate_execution(execution, identity, counts, inputs, architecture, minimum):
    require(execution.get("executionPassed") is True and execution.get("exitCode") == 0
            and execution.get("timedOut") is False and execution.get("filtered") is False
            and execution.get("requireFramework472") is minimum and execution.get("architecture") == architecture
            and execution.get("installedFrameworkRelease") == identity["frameworkRelease"]
            and execution.get("testAssemblySha256") == inputs["tests/Fluent.Pdf.Contract.Tests.dll"]
            and execution.get("runnerSha256") == inputs["testplatform/tools/net462/Common7/IDE/Extensions/TestPlatform/vstest.console.exe"]
            and execution.get("runtimeIdentity") == identity and execution.get("counters") == counts
            and execution.get("productionQualified") is False and execution.get("exactPackageConsumersExecuted") is False,
            "Offline process report is incomplete or differs from its actual TRX")


def validate_setup(setup, commit):
    manifest = read_json(setup / "setup.json")
    require(manifest.get("schemaVersion") == 1 and manifest.get("purpose") == "framework472-vm-source-setup"
            and manifest.get("sourceCommit") == commit and manifest.get("productionQualified") is False
            and manifest.get("exactPackageConsumers") is False, "Wrong source VM setup identity")
    records = manifest.get("files", [])
    require(len(records) == len(SETUP_FILES) and {f["path"] for f in records} == SETUP_FILES,
            "Incomplete or duplicate VM setup inventory")
    require({p.name for p in setup.iterdir()} == SETUP_FILES | {"setup.json"}, "Unmanifested VM setup input")
    for record in records:
        safe_path(record["path"])
        path = setup / record["path"]
        require(path.is_file() and not path.is_symlink() and path.stat().st_size == record["bytes"]
                and digest(path) == record["sha256"], "VM setup file changed")
        if record["path"] in {"qualify.ps1", "install.bat"}:
            require(record["sha256"] == hashlib.sha256(control_source(record["path"])).hexdigest(), "Guest control source differs from this checkout")
    with zipfile.ZipFile(setup / "source-bundle.zip") as archive:
        entries = [e for e in archive.infolist() if not e.is_dir()]
        require(0 < len(entries) <= MAX_FILES and sum(e.file_size for e in entries) <= MAX_BYTES,
                "Source bundle extent exceeded")
        keys = [str(safe_path(e.filename)).casefold() for e in entries]
        require(len(keys) == len(set(keys)), "Duplicate source ZIP path")
        for entry in archive.infolist():
            require(not stat.S_ISLNK(entry.external_attr >> 16), "Source ZIP symlink")
            safe_path(entry.filename.rstrip("/"))
        raw = archive.read("bundle.json")
        require(hashlib.sha256(raw).hexdigest() == manifest["bundleManifestSha256"], "Source manifest hash mismatch")
        bundle = json.loads(raw.decode("utf-8-sig"))
        require(bundle.get("schemaVersion") == 1 and bundle.get("purpose") == "offline-framework-source-bundle"
                and bundle.get("sourceCommit") == commit and bundle.get("productionQualified") is False
                and bundle.get("exactPackageConsumers") is False, "Not the current offline source bundle")
        files = bundle.get("files", [])
        require(files and len({f["path"].casefold() for f in files}) == len(files)
                and {e.filename for e in entries} == {f["path"] for f in files} | {"bundle.json"},
                "Source file inventory differs from the ZIP")
        for file in files:
            safe_path(file["path"])
            entry = archive.getinfo(file["path"])
            require(entry.file_size == file["bytes"], "Source file extent mismatch")
            with archive.open(entry) as stream:
                require(hashlib.file_digest(stream, "sha256").hexdigest() == file["sha256"], "Source file hash mismatch")
        source_hashes = {f["path"]: f["sha256"] for f in files}
        for script in ("run-framework-contracts.ps1", "run-framework-bundle.ps1"):
            require(source_hashes.get(script) == digest(ROOT / "eng" / script), "Offline runner source differs from this checkout")
    runtime = checker()
    matrix = read_json(ROOT / "specs/runtime-lanes.json")["lanes"]
    sdk_names = {p.parent.name for p in (ROOT / "src").glob("*/PublicAPI.Shipped.txt")}
    for architecture in ("x86", "x64"):
        identity, counts, _ = runtime.read_trx(setup / ("baseline-" + architecture + ".trx"))
        runtime.validate_identity(identity, next(x for x in matrix if x["id"] == "win-" + architecture + "-framework-current"), sdk_names)
        validate_execution(read_json(setup / ("baseline-" + architecture + "-execution.json")),
                           identity, counts, source_hashes, architecture, False)
        for asset in identity["sdkAssets"]:
            require(asset["sha256"] == source_hashes.get("tests/" + asset["name"] + ".dll"), "Baseline loaded a foreign SDK")
    return manifest, bundle


def validate_results(results, setup, manifest, bundle):
    status = read_json(results / "status.json")
    require(status.get("schemaVersion") == 1 and status.get("purpose") == "framework472-vm-source-execution"
            and status.get("sourceCommit") == manifest["sourceCommit"] and status.get("passed") is True
            and status.get("stage") == "complete" and status.get("productionQualified") is False
            and status.get("exactPackageConsumers") is False and status.get("runtimeOverrideNames") == []
            and status.get("installedFrameworkRelease") in {461808, 461814}, "Guest source execution failed or changed runtime")
    lanes = status.get("lanes", [])
    require(len(lanes) == 2 and {x["architecture"] for x in lanes} == {"x86", "x64"}
            and all(x.get("passed") is True for x in lanes), "Missing minimum-runtime architecture")
    inputs = {f["path"]: f["sha256"] for f in bundle["files"]}
    cpp = status.get("cppRuntimes", [])
    setup_hashes = {f["path"]: f["sha256"] for f in manifest["files"]}
    require(len(cpp) == 2 and {x["architecture"] for x in cpp} == {"x86", "x64"} and
            all(x.get("exitCode") in {0, 1638} and x.get("sha256") == setup_hashes["vc_redist." + x["architecture"] + ".exe"]
                for x in cpp), "Incomplete explicit guest prerequisite observations")
    runtime = checker()
    matrix = read_json(ROOT / "specs/runtime-lanes.json")["lanes"]
    sdk_names = {p.parent.name for p in (ROOT / "src").glob("*/PublicAPI.Shipped.txt")}
    observations = []
    for architecture in ("x86", "x64"):
        lane_id = "win-" + architecture + "-framework472"
        directory = results / architecture
        execution = read_json(directory / "framework-execution.json")
        origin = read_json(directory / "bundle-origin.json")
        identity, counts, tests = runtime.read_trx(directory / "runtime-contracts.trx")
        runtime.validate_identity(identity, next(x for x in matrix if x["id"] == lane_id), sdk_names)
        baseline, _, expected_tests = runtime.read_trx(setup / ("baseline-" + architecture + ".trx"))
        runtime.validate_identity(baseline, next(x for x in matrix if x["id"] == "win-" + architecture + "-framework-current"), sdk_names)
        require({(x["testId"], x["testName"]) for x in tests} ==
                {(x["testId"], x["testName"]) for x in expected_tests}, "Guest suite differs from the complete same-binary baseline")
        for source_identity in (identity, baseline):
            for asset in source_identity["sdkAssets"]:
                require(asset["sha256"] == inputs.get("tests/" + asset["name"] + ".dll"), "Loaded SDK differs from the source bundle")
        validate_execution(execution, identity, counts, inputs, architecture, True)
        require(origin.get("sourceCommit") == manifest["sourceCommit"] and
                origin.get("bundleManifestSha256") == manifest["bundleManifestSha256"] and
                origin.get("productionQualified") is False, "Foreign source bundle origin")
        observations.append({"lane": lane_id, "passed": counts["passed"], "total": counts["total"],
                             "failed": 0, "skipped": 0, "frameworkRelease": identity["frameworkRelease"]})
    return {"schemaVersion": 1, "purpose": "framework472-source-consistency", "sourceCommit": manifest["sourceCommit"],
            "lanes": observations, "productionQualified": False, "exactPackageConsumers": False}


def copy_export(source, target):
    files = list(source.rglob("*"))
    require(len(files) <= MAX_FILES and not any(p.is_symlink() for p in files), "Unsafe guest evidence inventory")
    regular = [p for p in files if p.is_file()]
    require(sum(p.stat().st_size for p in regular) <= MAX_BYTES, "Guest evidence extent exceeded")
    for path in regular:
        relative = str(path.relative_to(source).as_posix())
        portable = safe_path(relative)
        require(portable.parts[0] in {"status.json", "setup.log", "export-complete.json", "export-complete.tmp",
                                     "x86", "x64", "fixtures-x86", "fixtures-x64"}, "Unexpected guest export root")
        destination = target / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, destination)


def archive_reports(results):
    files = sorted(p for p in results.rglob("*") if p.is_file())
    index = {"schemaVersion": 1, "purpose": "source-vm-evidence-archive",
             "files": [{"path": p.relative_to(results).as_posix(), "bytes": p.stat().st_size, "sha256": digest(p)} for p in files],
             "productionQualified": False}
    index_path = results / "archive-index.json"
    index_path.write_text(json.dumps(index, indent=2) + "\n")
    archive = results / "evidence.zip"
    with zipfile.ZipFile(archive, "x", zipfile.ZIP_DEFLATED) as output:
        for path in files + [index_path]:
            output.write(path, path.relative_to(results))
    with zipfile.ZipFile(archive) as output:
        require(output.testzip() is None, "Evidence archive CRC verification failed")
        for record in index["files"]:
            with output.open(record["path"]) as stream:
                require(hashlib.file_digest(stream, "sha256").hexdigest() == record["sha256"], "Evidence archive hash mismatch")
    return digest(archive)


def run(args):
    setup, work, results = (p.resolve() for p in (args.setup, args.work, args.results))
    for left, right in ((setup, work), (setup, results), (work, results)):
        require(left != right and left not in right.parents and right not in left.parents, "Use separate setup/state/evidence directories")
    require(not work.exists() and not results.exists(), "Never overwrite existing VM state or reports")
    results.mkdir(parents=True)
    owner = uuid.uuid4().hex
    project = "fluent-pdf-f472-" + owner[:12]
    password = uuid.uuid4().hex + "aA1!"
    owned = False
    started = False
    export_copied = False
    failure = None
    receipt = {"purpose": "owned-source-vm-cleanup", "project": project, "image": IMAGE,
               "imageRetained": True, "stateRemoved": False, "containersRemoved": False,
               "networkRemoved": False, "evidenceArchivedAndVerified": False, "productionQualified": False}
    docker_env = dict(os.environ)
    for key in ("DOCKER_HOST", "DOCKER_CONTEXT", "DOCKER_TLS", "DOCKER_TLS_VERIFY", "DOCKER_CERT_PATH"):
        docker_env.pop(key, None)

    def command(*values, check=True, timeout=120):
        return subprocess.run(["docker", "--host=unix:///var/run/docker.sock", *values], env=docker_env,
                              capture_output=True, text=True, check=check, timeout=timeout)

    compose = ("compose", "--project-name", project, "--env-file", str(work / "secrets.env"), "-f", str(work / "compose.json"))

    def interrupt(_number, _frame):
        raise KeyboardInterrupt("Owned source VM interrupted")

    previous_signal = signal.signal(signal.SIGTERM, interrupt)

    def redact(value):
        return value.replace(password, "[redacted]").replace(quote(password, safe=""), "[redacted]")

    try:
        manifest, bundle = validate_setup(setup, args.commit)
        shutil.copyfile(setup / "setup.json", results / "input-setup.json")
        (results / "input-bundle.json").write_text(json.dumps(bundle, indent=2) + "\n")
        require(platform.system() == "Linux" and platform.machine() in {"x86_64", "amd64"}, "Native Linux x64 is required")
        require(os.environ.get("GITHUB_ACTIONS") == "true" and os.environ.get("RUNNER_ENVIRONMENT") == "github-hosted",
                "This runner owns only a disposable GitHub-hosted machine")
        require(Path("/dev/kvm").exists() and stat.S_ISCHR(Path("/dev/kvm").stat().st_mode) and
                os.access("/dev/kvm", os.R_OK | os.W_OK), "Usable hardware KVM is required; no emulation fallback")
        work.parent.mkdir(parents=True, exist_ok=True)
        require(shutil.disk_usage(work.parent).free >= 60 * 1024**3, "At least 60 GiB of free disposable runner storage is required")
        engine = json.loads(command("info", "--format", "{{json .}}").stdout)
        require(engine["OSType"] == "linux" and engine["Architecture"] in {"x86_64", "amd64"} and
                engine["MemTotal"] >= 6 * 1024**3, "Linux x64 Docker with at least 6 GiB memory is required")
        command("compose", "version")
        work.mkdir(mode=0o700)
        owned = True
        (work / ".owner").write_text(owner)
        for directory in ("oem", "shared", "storage"):
            (work / directory).mkdir()
        for name in SETUP_FILES | {"setup.json"}:
            shutil.copyfile(setup / name, work / "oem" / name)
        secret = work / "secrets.env"
        secret.write_text("FLUENT_PDF_VM_PASSWORD=" + password + "\n")
        secret.chmod(0o600)
        service = {"image": IMAGE, "platform": "linux/amd64",
                   "environment": {"VERSION": "2019", "VERIFY": "Y", "RAM_SIZE": "4G", "CPU_CORES": "2",
                                   "DISK_SIZE": "40G", "DISK_FMT": "qcow2", "NETWORK": "user", "KVM": "Y",
                                   "USERNAME": "FluentPdf", "PASSWORD": "$" + "{FLUENT_PDF_VM_PASSWORD}", "LOG": "Y"},
                   "devices": ["/dev/kvm:/dev/kvm"],
                   "volumes": ["./storage:/storage", "./oem:/oem:ro", "./shared:/shared"],
                   "restart": "no", "stop_grace_period": "30s"}
        (work / "compose.json").write_text(json.dumps({"services": {"windows": service}}, indent=2))
        command("pull", "--platform", "linux/amd64", IMAGE, timeout=600)
        command("run", "--rm", "--network", "none", "--device", "/dev/kvm", "--entrypoint", "/bin/sh",
                IMAGE, "-c", "test -r /dev/kvm && test -w /dev/kvm")
        command(*compose, "config", "--quiet")
        started = True  # Cleanup also reconciles a partially successful create.
        command(*compose, "up", "-d")
        deadline = time.monotonic() + args.timeout_seconds
        exported = work / "shared/results"
        while time.monotonic() < deadline:
            marker = exported / "export-complete.json"
            if marker.is_file():
                copy_export(exported, results)
                export_copied = True
                completed = read_json(results / "export-complete.json")
                if completed.get("exitCode") != 0:
                    guest = read_json(results / "status.json")
                    summary = {k: guest.get(k) for k in ("stage", "installedFrameworkRelease", "error", "lanes")}
                    raise ValueError("Guest source execution failed: " + json.dumps(summary, ensure_ascii=True)[:4000])
                require(completed.get("sourceCommit") == args.commit, "Guest export source commit differs")
                verification = validate_results(results, setup, manifest, bundle)
                (results / "verification.json").write_text(json.dumps(verification, indent=2) + "\n")
                break
            state = command(*compose, "ps", "--all", "--format", "json").stdout
            require(state.strip() and not re.search(r'"State"\s*:\s*"(exited|dead)"', state), "VM exited before exporting complete source reports")
            time.sleep(10)
        else:
            raise TimeoutError("Source VM boot/tests exceeded the external deadline")
    except (Exception, KeyboardInterrupt) as error:
        failure = str(error)
        if isinstance(error, subprocess.CalledProcessError):
            failure += "\n" + (error.stderr or "")
        (results / "host-error.txt").write_text(redact(failure) + "\n")
        print("Source VM failure: " + redact(failure).replace("\n", " ")[:6000], flush=True)
    finally:
        signal.signal(signal.SIGTERM, previous_signal)
        # Failed log/archive reads must never skip owned container teardown.
        try:
            if started:
                try:
                    logs = command(*compose, "logs", "--no-color", "--tail", "1500", check=False).stdout
                    (results / "vm.log").write_text(redact(logs))
                except Exception as error:
                    (results / "log-error.txt").write_text(redact(str(error)))
                partial = work / "shared/results"
                if not export_copied and partial.is_dir():
                    copy_export(partial, results)
            receipt["archiveSha256"] = archive_reports(results)
            receipt["evidenceArchivedAndVerified"] = True
        except Exception as error:
            receipt["archiveError"] = redact(str(error))
            failure = failure or "Source evidence could not be archived"
        try:
            if started:
                command(*compose, "down", "--timeout", "30")
                label = "label=com.docker.compose.project=" + project
                receipt["containersRemoved"] = not command("ps", "-aq", "--filter", label).stdout.strip()
                receipt["networkRemoved"] = not command("network", "ls", "-q", "--filter", label).stdout.strip()
            else:
                receipt.update(containersRemoved=True, networkRemoved=True)
            if owned and receipt["containersRemoved"] and receipt["networkRemoved"] and receipt["evidenceArchivedAndVerified"]:
                require(not work.is_symlink() and (work / ".owner").read_text() == owner, "VM state ownership changed; retain it")
                shutil.rmtree(work)
            receipt["stateRemoved"] = not owned or not work.exists()
        except Exception as error:
            receipt["error"] = redact(str(error))
            failure = failure or "Owned VM cleanup/archive failed"
        (results / "cleanup.json").write_text(json.dumps(receipt, indent=2) + "\n")
    require(not failure and all(receipt[k] for k in ("stateRemoved", "containersRemoved", "networkRemoved", "evidenceArchivedAndVerified")),
            "Minimum-runtime source execution did not complete; retain its failure reports")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--setup", type=Path, required=True)
    parser.add_argument("--work", type=Path)
    parser.add_argument("--results", type=Path)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--check-setup", action="store_true")
    parser.add_argument("--timeout-seconds", type=int, default=4500)
    args = parser.parse_args()
    require(re.fullmatch("[0-9a-f]{40}", args.commit) and 60 <= args.timeout_seconds <= 5400, "Full commit and bounded deadline required")
    if args.check_setup:
        validate_setup(args.setup.resolve(), args.commit)
        print("VM source setup consistency passed; no Windows or Docker execution.")
    else:
        require(args.work is not None and args.results is not None, "Fresh VM state and evidence paths are required")
        run(args)


if __name__ == "__main__":
    main()
