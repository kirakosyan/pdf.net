#!/usr/bin/env python3
"""Actual source recipe applications; final package mode has a separate gate."""
import argparse
import hashlib
import json
from pathlib import Path
import platform
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--assemblies", type=Path, nargs="+", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--package-mode", action="store_true")
    parser.add_argument("--netstandard", action="store_true")
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.unlink(missing_ok=True)
    records = []
    expected = {"model-snapshot", "language-vector", "language-canvas", "select-and-extract", "limits-and-cancellation", "explicit-provider", "preflight-and-formats", "lossless-optimization"}
    for assembly in args.assemblies:
        directory = (args.output.parent / assembly.parent.name).resolve()
        directory.mkdir(exist_ok=True)
        command = [str(assembly.resolve())] if assembly.suffix == ".exe" else ["dotnet", str(assembly.resolve())]
        result = subprocess.run([*command, str(directory)], check=True, capture_output=True, text=True, timeout=60)
        lines = result.stdout.splitlines()
        cases = [line.split(";", 1)[1] for line in lines if line.startswith("RECIPE_PASS;")]
        assert set(cases) == expected and len(cases) == len(expected) and not result.stderr
        identity = [line for line in lines if line.startswith("RECIPES_RUNTIME;")]
        assert len(identity) == 1
        expected_asset = ".NETStandard,Version=v2.0" if args.netstandard else ".NETFramework,Version=v4.7.2" if assembly.suffix == ".exe" else ".NETCoreApp,Version=v" + ("8.0" if assembly.parent.name == "net8.0" else "10.0")
        assert identity[0].split(";")[3] == expected_asset
        mode = "package-consumer" if args.package_mode else "source-consumer"
        assert identity[0].endswith(";" + mode)
        dependencies = None
        if assembly.suffix == ".dll":
            deps = assembly.with_suffix(".deps.json")
            graph = json.loads(deps.read_bytes())["libraries"]
            root = Path(__file__).resolve().parents[1]
            sdk = {p.parent.name for p in (root / "src").glob("*/PublicAPI.Shipped.txt")}
            selected = {key.split("/", 1)[0]: value["type"] for key, value in graph.items() if key.split("/", 1)[0] in sdk}
            assert set(selected) == sdk and set(selected.values()) == {"package" if args.package_mode else "project"}
            dependencies = dict(path=str(deps), sha256=hashlib.sha256(deps.read_bytes()).hexdigest(), sdkAssets=selected)
        for name in ["cs-vector.pdf", "vb-vector.pdf", "cs-canvas.pdf", "vb-canvas.pdf", "selected.pdf", "optimized.pdf"]:
            assert (directory / name).read_bytes().startswith(b"%PDF-")
        matrix = json.loads((directory / "cs-formats.json").read_bytes())
        assert matrix == json.loads((directory / "vb-formats.json").read_bytes()) and len(matrix["targets"]) == 9 and not matrix["productionQualified"]
        records.append(dict(assembly=str(assembly), assemblySha256=hashlib.sha256(assembly.read_bytes()).hexdigest(), dependencyGraph=dependencies, runtime=identity[0], cases=cases, total=len(cases), passed=len(cases), failed=0, skipped=0,
                            outputs=[dict(path=str(p), sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(directory.iterdir()) if p.suffix in [".pdf", ".json"]]))
        print("DOCUMENTATION_RECIPES_PASS;" + identity[0])
    args.output.write_text(json.dumps(dict(schemaVersion=1, os=platform.platform(), consumerMode="package" if args.package_mode else "source", profiles=records, productionQualified=False), indent=2) + "\n")


if __name__ == "__main__":
    main()
