#!/usr/bin/env python3
"""Exact executable-source excerpts and complete literal fragment inventory.

The inventory never calls an unbound narrative fragment an executed sample.
Package/runtime/provider qualification remains a separate mandatory gate.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--update", action="store_true")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    version = json.loads((root / "specs/release-readiness.json").read_text())["packageVersion"]
    source = root / "examples/Fluent.Pdf.Recipes/Program.cs"
    text = source.read_text(encoding="utf-8")
    page = "# Executable source recipes\n\nVersion `" + version + "`. This complete application is compiled and executed by the [recipe project](../examples/Fluent.Pdf.Recipes/Fluent.Pdf.Recipes.csproj). It calls both compiled C# and VB examples and uses original fixture input copied beside the executable. Pass one output-directory argument. See the [developer guide](developer-guide.md) for providers, ownership, source/package mode and remaining qualification. The literal excerpt must match its compiled source exactly.\n\n```csharp\n" + text.rstrip() + "\n```\n"
    generated = root / "docs/recipes.md"
    if args.update:
        generated.write_text(page, encoding="utf-8")
    elif not generated.exists() or generated.read_text(encoding="utf-8") != page:
        raise RuntimeError("Executable recipe excerpt differs from its compiled source.")
    files = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard"], cwd=root, text=True).splitlines()
    records = []
    pattern = re.compile(r"^```(csharp|cs|vb|visualbasic)\s*\n(.*?)^```\s*$", re.MULTILINE | re.DOTALL)
    for name in sorted(set(files)):
        if not name.endswith(".md") or not (name == "README.md" or name.startswith(("docs/", "examples/"))):
            continue
        content = (root / name).read_text(encoding="utf-8")
        for index, match in enumerate(pattern.finditer(content), 1):
            executable = name == "docs/recipes.md"
            records.append(dict(path=name, ordinal=index, line=content[:match.start()].count("\n") + 1,
                                language=match.group(1), sha256=hashlib.sha256(match.group(2).encode()).hexdigest(),
                                classification="complete-source-application" if executable else "narrative-fragment-requires-caller-bindings",
                                compileExecution="examples/Fluent.Pdf.Recipes/Fluent.Pdf.Recipes.csproj" if executable else None,
                                literalFragmentExecutionQualified=False, publishedPackageExecutionQualified=False))
    value = dict(schemaVersion=1, packageVersion=version, snippets=records,
                 scope="Every C#/VB literal fence is inventoried. Only the complete recipe application has exact source/execution binding here; all other fragments retain their explicit missing caller/provider/package qualification.", productionQualified=False)
    catalog = root / "specs/documentation-snippets.json"
    content = json.dumps(value, indent=2) + "\n"
    if args.update:
        catalog.write_text(content, encoding="utf-8")
    elif not catalog.exists() or catalog.read_text(encoding="utf-8") != content:
        raise RuntimeError("Literal documentation snippet inventory drift.")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(dict(schemaVersion=1, packageVersion=version, literalFragments=len(records),
                                          exactExecutableSourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                                          catalogSha256=hashlib.sha256(catalog.read_bytes()).hexdigest(),
                                          narrativeFragmentsStillRequiringBinding=sum(r["compileExecution"] is None for r in records),
                                          publishedPackagesQualified=False, productionQualified=False), indent=2) + "\n")
    print("DOCUMENTATION_SNIPPETS_PASS;" + str(len(records)) + "-inventoried;exact-source-recipe;remaining-bindings-explicit")


if __name__ == "__main__":
    main()
