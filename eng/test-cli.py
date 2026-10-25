#!/usr/bin/env python3
"""Actual CLI process, pipe, atomic output, protected input and batch checks.

Only Python's standard library is required. PDF fidelity is checked separately
by an independent reader; these are application/source boundary observations.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tempfile
import time


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--assemblies", nargs="+", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    source = root / "tests/Fluent.Pdf.Contract.Tests/Fixtures/Automation/original.pdf"
    encrypted = root / "tests/Fluent.Pdf.Contract.Tests/Fixtures/Cli/encrypted.pdf"
    vector = root / "tests/Fluent.Pdf.Contract.Tests/Fixtures/Cli/vector.pdf"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.unlink(missing_ok=True)
    rows = []
    for assembly in args.assemblies:
        assembly = assembly.resolve()
        destination = args.output.parent / assembly.parent.name
        destination.mkdir(exist_ok=True)
        cases = []
        with tempfile.TemporaryDirectory(prefix="fluent-pdf-cli-") as temporary:
            directory = Path(temporary).resolve()
            def run(arguments, expected=0, data=None):
                result = subprocess.run(["dotnet", str(assembly), *map(str, arguments)], input=data,
                                        stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=20)
                assert result.returncode == expected, (arguments[0], result.returncode, result.stderr.decode("utf-8", "replace"))
                diagnostics = [json.loads(line) for line in result.stderr.decode("utf-8").splitlines()]
                assert all(value["schemaVersion"] == 1 for value in diagnostics)
                assert b"DO_NOT_LOG" not in result.stderr and str(directory).encode() not in result.stderr
                return result, diagnostics

            result, _ = run(["--version"])
            identity = json.loads(result.stdout)
            expected = ".NETCoreApp,Version=v" + ("8.0" if assembly.parent.name == "net8.0" else "10.0")
            assert identity["sdkTarget"] == expected and not identity["productionQualified"]
            cases.append("actual-runtime-and-sdk-asset")

            result, reports = run(["select", "--input", "-", "--output", "-", "--pages", "2,1"], data=source.read_bytes())
            assert result.stdout.startswith(b"%PDF-") and reports[0]["published"]
            (destination / "selected.pdf").write_bytes(result.stdout)
            cases.append("binary-pipes-with-separate-json-diagnostics")

            for view in ["raw", "semantic", "layout"]:
                result, _ = run(["text", "--input", source, "--pages", "2,1", "--view", view])
                assert result.stdout.decode("utf-8") == "Second automation page\fOriginal automation Ω"
            cases.append("three-explicit-text-views-and-selection-order")

            result, _ = run(["preflight", "--input", source])
            preflight = json.loads(result.stdout)
            assert source.read_bytes().startswith(b"%PDF-") and sha(source) in result.stdout.decode()
            (destination / "preflight.json").write_bytes(result.stdout)
            result, _ = run(["detect", "--input", source])
            assert json.loads(result.stdout)["format"] == "Pdf"
            result, _ = run(["formats"])
            matrix = json.loads(result.stdout)
            assert not matrix["cliRendererConfigured"] and matrix["cliTargets"] == ["Svg", "StaticEps"] and len(matrix["sdkMatrix"]["targets"]) == 9
            cases.append("read-only-preflight-discovery-and-exact-matrix")
            for target, name in [("Svg", "vector.svg"), ("StaticEps", "vector.eps")]:
                result, _ = run(["convert", "--input", vector, "--target", target])
                assert result.stdout and (b"<svg" in result.stdout if target == "Svg" else result.stdout.startswith(b"%!PS-Adobe"))
                (destination / name).write_bytes(result.stdout)
            cases.append("actual-managed-vector-conversion-profiles")

            result, _ = run(["text", "--input", source, "--max-input-bytes", source.stat().st_size])
            assert result.stdout
            result, reports = run(["text", "--input", source, "--max-input-bytes", source.stat().st_size - 1], 4)
            assert not result.stdout and reports[0]["code"] == "CLI.InputLimit"
            result, reports = run(["text", "--input", source, "--max-output-bytes", 1], 4)
            assert not result.stdout and not reports[0]["published"]
            cases.append("exact-input-and-failing-input-output-boundaries")

            result, reports = run(["convert", "--input", source, "--target", "Png"], 3)
            assert not result.stdout and reports[0]["code"] == "CLI.RendererRequired"
            result, reports = run(["rewrite", "--input", root / "tests/Fluent.Pdf.Contract.Tests/Fixtures/writer/signed-original.pdf"], 3)
            assert not result.stdout and not reports[0]["published"]
            cases.append("unsupported-renderer-and-signature-authority")

            retained = directory / "retained.pdf"
            retained.write_bytes(b"unchanged")
            if os.name != 'nt': retained.chmod(0o644)
            result, reports = run(["rewrite", "--input", source, "--output", retained], 5)
            assert retained.read_bytes() == b"unchanged" and reports[0]["cleanup"] == "removed"
            result, _ = run(["rewrite", "--input", source, "--output", retained, "--overwrite"])
            assert not result.stdout and retained.read_bytes().startswith(b"%PDF-")
            if os.name != 'nt': assert retained.stat().st_mode & 0o7777 == 0o600
            private_output = directory / 'new-private.pdf'
            run(["rewrite", "--input", source, "--output", private_output])
            if os.name != 'nt': assert private_output.stat().st_mode & 0o7777 == 0o600
            shutil.copyfile(retained, destination / "rewritten.pdf")
            assert not list(directory.glob(".fluent-pdf-*.tmp"))
            cases.append("atomic-no-clobber-explicit-overwrite-and-owned-cleanup")

            bad = directory / "DO_NOT_LOG_PATH.pdf"
            bad.write_bytes(b"DO_NOT_LOG_DOCUMENT_PAYLOAD")
            config = directory / "batch.json"
            report = directory / "batch-result.json"
            config.write_text(json.dumps(dict(schemaVersion=1, jobs=[dict(id="configured", operation="text", input=str(source), output="configured.txt")])))
            result, _ = run(["run", "--config", config])
            assert not result.stdout and (directory / "configured.txt").read_text(encoding="utf-8") == "Original automation Ω\fSecond automation page"
            config.write_text(json.dumps(dict(schemaVersion=1, parallelism=2, report=report.name, jobs=[
                dict(id="select", operation="select", input=str(source), output="batch-selected.pdf", pages="2,1"),
                dict(id="broken", operation="rewrite", input=bad.name, output="failed.pdf"),
                dict(id="text", operation="text", input=str(source), output="batch-text.txt")
            ])))
            result, diagnostics = run(["batch", "--config", config], 6)
            value = json.loads(result.stdout)
            assert value == json.loads(report.read_bytes()) and [job["id"] for job in value["jobs"]] == ["select", "broken", "text"]
            assert [job["exitCode"] for job in value["jobs"]] == [0, 3, 0] and not (directory / "failed.pdf").exists()
            assert len(diagnostics) == 3 and all(job["temporaryName"] is None for job in value["jobs"])
            assert not list(directory.glob(".fluent-pdf-*.tmp"))
            cases.append("bounded-partial-batch-with-ordered-durable-outcomes")

            for content in [
                '{"schemaVersion":1,"schemaVersion":1,"jobs":[]}',
                '{"schemaVersion":1,"jobs":[],"unknown":"DO_NOT_LOG_CONFIG"}',
                json.dumps(dict(schemaVersion=1, parallelism=5, report="quota.json", jobs=[dict(id="a", operation="text", input=str(source), output="not-created.txt")])),
                json.dumps(dict(schemaVersion=1, report="collision.json", jobs=[dict(id="a", operation="rewrite", input=str(source), output="same.pdf"), dict(id="b", operation="text", input=str(source), output="same.pdf")]))
            ]:
                config.write_text(content)
                result, _ = run(["batch", "--config", config], 2)
                assert not result.stdout and not (directory / "same.pdf").exists() and not (directory / "not-created.txt").exists()
            result, _ = run(["text", "--input", source, "--password", "DO_NOT_LOG_SECRET"], 2)
            assert not result.stdout
            cases.append("closed-duplicate-key-quota-collision-and-secret-argument-rejection")

            secret = directory / "DO_NOT_LOG_PASSWORD_PATH"
            secret.write_bytes(b"cli-fixture-password")
            if os.name == "nt":
                import getpass
                subprocess.run(["icacls", str(secret), "/setowner", getpass.getuser()], check=True, capture_output=True)
                subprocess.run(["icacls", str(secret), "/inheritance:r", "/grant:r", getpass.getuser() + ":(F)"], check=True, capture_output=True)
            else:
                secret.chmod(0o600)
            result, _ = run(["decrypt", "--input", encrypted, "--password-file", secret])
            assert result.stdout.startswith(b"%PDF-") and b"cli-fixture-password" not in result.stderr
            (destination / "decrypted.pdf").write_bytes(result.stdout)
            secret.write_bytes(b"DO_NOT_LOG_WRONG_PASSWORD")
            result, _ = run(["decrypt", "--input", encrypted, "--password-file", secret], 3)
            assert not result.stdout
            if os.name == "nt":
                subprocess.run(["icacls", str(secret), "/grant", "*S-1-1-0:(R)"], check=True, capture_output=True)
            else:
                secret.chmod(0o644)
            result, reports = run(["decrypt", "--input", encrypted, "--password-file", secret], 2)
            assert not result.stdout and reports[0]["code"] == "CLI.SecretPermissions"
            if os.name == "nt":
                subprocess.run(["icacls", str(secret), "/remove:g", "*S-1-1-0"], check=True, capture_output=True)
            else:
                secret.chmod(0o600)
            secret.write_bytes(b"x" * 4097)
            result, reports = run(["decrypt", "--input", encrypted, "--password-file", secret], 4)
            assert not result.stdout and reports[0]["code"] == "CLI.SecretLimit"
            cases.append("actual-independent-encryption-protected-secret-and-redaction")

            started = time.monotonic()
            canceled = directory / "canceled.txt"
            canceled.write_bytes(b"unchanged")
            child = subprocess.Popen(["dotnet", str(assembly), "text", "--input", "-", "--output", str(canceled), "--overwrite", "--timeout-ms", "100"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            try:
                assert child.wait(timeout=10) == 4
                stdout, stderr = child.stdout.read(), child.stderr.read()
                assert not stdout and json.loads(stderr)["code"] == "CLI.Timeout" and time.monotonic() - started < 10
                assert canceled.read_bytes() == b"unchanged"
            finally:
                if child.poll() is None: child.kill(); child.wait()
                child.stdin.close(); child.stdout.close(); child.stderr.close()
            cases.append("actual-blocked-stdin-cancellation-and-process-exit")
            if os.name != "nt":
                fifo = directory / "input-fifo"
                os.mkfifo(fifo)
                result, reports = run(["text", "--input", fifo, "--timeout-ms", "100"], 4)
                assert not result.stdout and reports[0]["cleanup"] == "input-released-on-process-exit"
                cases.append("actual-blocked-path-admission-supervision")

            child = subprocess.Popen(["dotnet", str(assembly), "rewrite", "--input", str(source)], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            child.stdout.close(); child.stdout = None
            _, stderr = child.communicate(timeout=10)
            assert child.returncode == 5 and json.loads(stderr)["cleanup"] == "stdout-may-be-partial"
            cases.append("actual-closed-output-pipe-failure")
        rows.append(dict(assembly=str(assembly), assemblySha256=sha(assembly), identity=identity, cases=cases, total=len(cases), passed=len(cases), failed=0, skipped=0,
                         outputs=[dict(path=str(destination / name), sha256=sha(destination / name)) for name in ["selected.pdf", "rewritten.pdf", "decrypted.pdf", "preflight.json", "vector.svg", "vector.eps"]]))
        print("CLI_PROCESS_PASS;" + identity["runtime"] + ";" + identity["architecture"] + ";" + str(len(cases)) + "-groups")
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
    dirty = bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=root))
    args.output.write_text(json.dumps(dict(schemaVersion=1, sourceCommit=commit, workingTreeDirty=dirty, os=platform.platform(), sourceSha256=sha(source), encryptedInputSha256=sha(encrypted), profiles=rows, productionQualified=False), indent=2) + "\n")


if __name__ == "__main__":
    main()
