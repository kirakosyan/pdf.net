"""Run a pinned development-only EPUBCheck against controlled original fixtures."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def archive(path):
    if path.exists():
        raw = path.read_bytes()
        previous = path.with_name(path.name + ".previous-" + hashlib.sha256(raw).hexdigest())
        if previous.exists():
            require(previous.read_bytes() == raw, "Evidence archive changed")
        else:
            previous.write_bytes(raw)
        path.unlink()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("fixture_directory", type=Path)
    parser.add_argument("report_directory", type=Path)
    parser.add_argument("--lib-directory", required=True, type=Path)
    args = parser.parse_args()
    expected_path = Path(__file__).parent / "format-checks/epubcheck/tool-jars.json"
    expected = json.loads(expected_path.read_text())
    actual = [{"file": path.name, "sha256": digest(path)} for path in sorted(args.lib_directory.glob("*.jar"))]
    require(actual == expected["jars"], "Provisioned validator JAR inventory differs from pinned analysis identity")
    args.report_directory.mkdir(parents=True, exist_ok=True)
    summary = args.report_directory / "independent-epub.json"
    archive(summary)
    java = subprocess.run(["java", "-version"], capture_output=True, text=True, check=True)
    records = []
    for name in ["fixed-book.epub", "reflow-book.epub"]:
        path = args.fixture_directory / "format-fixtures" / name
        report = args.report_directory / (name + ".epubcheck.json")
        archive(report)
        result = subprocess.run(["java", "-Djava.awt.headless=true", "-cp", str(args.lib_directory.resolve() / "*"),
                                 "com.adobe.epubcheck.tool.Checker", str(path.resolve()), "--json", str(report.resolve())],
                                capture_output=True, text=True)
        log = args.report_directory / (name + ".epubcheck.log")
        archive(log)
        log.write_text(result.stdout + result.stderr)
        require(report.exists(), "The validator produced no actual report")
        parsed = json.loads(report.read_text())
        require(result.returncode == 0, f"EPUBCheck rejected {name}; see retained report")
        checker = parsed.get("checker", {})
        require(checker.get("checkerVersion") == "5.3.0" and all(checker.get(key) == 0 for key in ["nFatal", "nError", "nWarning"]), "The actual pinned validator did not report zero errors/warnings")
        require(not any(item.get("severity") in ["ERROR", "FATAL"] for item in parsed.get("messages", [])), "Validator report contains errors")
        records.append({"artifact": name, "sha256": digest(path), "report": report.name, "reportSha256": digest(report), "exitCode": result.returncode,
                        "checker": parsed.get("checker"), "messages": parsed.get("messages", [])})
    summary.write_text(json.dumps({"schemaVersion": 1, "purpose": "controlled-original-epub-source-fixtures", "validator": "EPUBCheck", "version": "5.3.0",
                                  "javaRuntime": java.stderr.strip(), "validatorJarInventorySha256": digest(expected_path), "fixtures": records,
                                  "productionQualified": False}, indent=2) + "\n")
    print("Independent EPUBCheck source fixture validation passed.")


if __name__ == "__main__":
    main()
