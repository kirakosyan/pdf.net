"""Explicit task-local analysis policy only; never installs or changes daemon/kernel policy."""
import argparse
import difflib
import hashlib
import json
from pathlib import Path


def prepare(original, license_file, output):
    raw = original.read_bytes()
    if hashlib.sha256(raw).hexdigest() != "6416b47770785a41ac59073cdc77d9fe98517df2799dc83ef207e622de3053f6" or hashlib.sha1(b"blob " + str(len(raw)).encode() + b"\0" + raw).hexdigest() != "6e5e47e242892aaa290f916ba1045f9213ef0b84":
        raise ValueError("Unrecognized original analysis policy")
    license_bytes = license_file.read_bytes()
    if hashlib.sha1(b"blob " + str(len(license_bytes)).encode() + b"\0" + license_bytes).hexdigest() != "d645695673349e3947e8e5ae42332d0ac3164cd7":
        raise ValueError("Unrecognized upstream Apache-2.0 license")
    policy = json.loads(raw)
    if policy["defaultAction"] != "SCMP_ACT_ERRNO":
        raise ValueError("Default-deny prerequisite missing")
    policy["syscalls"].extend([
        {"names": ["clone"], "action": "SCMP_ACT_ALLOW", "args": [{"index": 0, "value": 235012096, "valueTwo": 0, "op": "SCMP_CMP_MASKED_EQ"}], "includes": {"arches": ["amd64", "arm64"]}, "comment": "SOURCE FIXTURE MODIFICATION: permit user/PID/network namespaces without outer capabilities; other namespace flags stay denied."},
        {"names": ["unshare"], "action": "SCMP_ACT_ALLOW", "args": [{"index": 0, "value": 268435456, "op": "SCMP_CMP_EQ"}], "includes": {"arches": ["amd64", "arm64"]}, "comment": "SOURCE FIXTURE MODIFICATION: exact unprivileged user namespace request for the inner browser sandbox."},
        {"names": ["chroot"], "action": "SCMP_ACT_ALLOW", "comment": "SOURCE FIXTURE MODIFICATION: nonroot with no outer capabilities; chroot requires entering its unprivileged user namespace."},
    ])
    modified = (json.dumps(policy, indent=2) + "\n").encode()
    output.mkdir(parents=True, exist_ok=False)
    (output / "original.json").write_bytes(raw)
    (output / "fixture.json").write_bytes(modified)
    (output / "LICENSE").write_bytes(license_bytes)
    (output / "modification.diff").write_text("".join(difflib.unified_diff(raw.decode().splitlines(True), modified.decode().splitlines(True), fromfile="original.json", tofile="fixture.json")), encoding="utf-8")
    record = {"purpose": "bounded-task-local-analysis-policy-modification", "productionApproved": False,
              "upstreamPolicyGitBlob": "6e5e47e242892aaa290f916ba1045f9213ef0b84", "upstreamLicenseGitBlob": "d645695673349e3947e8e5ae42332d0ac3164cd7", "license": "Apache-2.0",
              "originalSha256": hashlib.sha256(raw).hexdigest(), "modifiedSha256": hashlib.sha256(modified).hexdigest(), "licenseSha256": hashlib.sha256(license_bytes).hexdigest(),
              "limitations": ["Source fixtures only; complete syscall/namespace/kernel/daemon review remains required", "No outer capability, no sandbox-disable flag, no global daemon/kernel policy change", "Does not qualify network/security/host distribution or production use"]}
    (output / "modification.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--original", type=Path, required=True)
    parser.add_argument("--license", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    prepare(args.original, args.license, args.output)
