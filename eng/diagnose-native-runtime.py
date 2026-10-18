"""Failure-only trusted ABI initialization probes; never a processing fallback."""
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import struct
import subprocess
import tempfile

if platform.system() != "Darwin":
    raise SystemExit("This diagnostic is scoped to macOS source runners.")

root = Path(__file__).resolve().parent.parent
rid = "osx-arm64" if platform.machine() == "arm64" else "osx-x64"
assets = root / "artifacts" / "native-fixtures" / rid
library = "fluent-pdf-codecs.dylib"
temporary = Path(os.environ["RUNNER_TEMP"]).resolve()
profiles = [(268291082, heap, region, 1048576)
            for heap in (16777216, 33554432, 67108864)
            for region in (33554432, 67108864, 134217728)]
profiles += [(268291082, 33554432, 67108864, 4194304),
             (1073741824, 134217728, 268435456, 4194304)]
records = []
for memory, heap, region, size in profiles:
    with tempfile.TemporaryDirectory(prefix="fluent-pdf-runtime-probe-", dir=temporary) as directory:
        job = Path(directory).resolve()
        if not job.is_relative_to(temporary):
            raise RuntimeError("A probe directory escaped the owned runner root.")
        hashes = {}
        for name in ("worker", library):
            source, target = assets / name, job / name
            shutil.copyfile(source, target)
            hashes[name] = hashlib.sha256(source.read_bytes()).hexdigest()
            if hashlib.sha256(target.read_bytes()).hexdigest() != hashes[name]:
                raise RuntimeError("A probe asset changed during staging.")
            target.chmod(0o500 if name == "worker" else 0o400)
        environment = {"TMPDIR": str(job), "HOME": str(job), "MallocNanoZone": "0",
                       "DOTNET_GCHeapHardLimit": f"{heap:X}", "DOTNET_GCRegionRange": f"{region:X}",
                       "DOTNET_GCRegionSize": f"{size:X}", "DOTNET_GCConserveMemory": "9",
                       "DOTNET_gcServer": "0", "DOTNET_gcConcurrent": "0",
                       "DOTNET_EnableDiagnostics": "0", "DOTNET_PROCESSOR_COUNT": "2"}
        command = [str(job / "worker"), str(job), library, "fluent-pdf-codecs-1", "1.0.0",
                   "1", "48", str(memory), "10000", "00" * 16, str(os.getpid()), "1"]
        process = subprocess.run(command, input=b"\0\0\0\0", env=environment,
                                 cwd=job, capture_output=True, timeout=15)
        frame = process.stdout
        status = struct.unpack_from("<I", frame, 24)[0] if len(frame) >= 32 and frame[:8] == b"FPNW0001" and frame[8:24] == bytes(16) else None
        records.append({"memoryBytes": memory, "gcHeapBytes": heap, "gcRangeBytes": region,
                        "gcRegionBytes": size, "exitCode": process.returncode,
                        "status": status, "stderr": process.stderr.decode("utf-8", errors="replace")[:8192],
                        "assetSha256": hashes})

report = {"schemaVersion": 1, "runtimeIdentifier": rid,
          "scope": "Separate fresh sandboxed ABI initialization probes with empty requests; no PDF processing or qualification claim.",
          "records": records}
output = root / "artifacts" / "contracts" / "native-runtime-init.json"
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report))
