"""Fail closed on incomplete NuGet advisory reports or any reported finding."""
import argparse
import json
from pathlib import Path


def check(report, expected_projects):
    if report.get('version') != 1 or not report.get('sources') or report.get('problems'):
        raise ValueError('Advisory query missing sources or reported problems')
    projects = report.get('projects', [])
    paths = [str(Path(p['path']).resolve()) for p in projects]
    expected = {str(Path(p).resolve()) for p in expected_projects}
    if not expected or len(paths) != len(set(paths)) or set(paths) != expected:
        raise ValueError('Incomplete/duplicate advisory project coverage')
    findings = []
    for project in projects:
        for framework in project.get('frameworks', []):
            for group in ('topLevelPackages', 'transitivePackages'):
                for package in framework.get(group, []):
                    for finding in package.get('vulnerabilities', []):
                        findings.append({'project': project['path'], 'framework': framework['framework'],
                                         'id': package['id'], 'version': package['resolvedVersion'], **finding})
    if findings:
        raise ValueError('Reported dependency advisories: ' + json.dumps(findings))
    return {'projectCount': len(projects), 'returnedFindings': 0, 'productionQualified': False,
            'limitation': 'No known findings returned by these configured sources at query time; not a proof of safety or full native/model coverage.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--expected-project', action='append', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.unlink(missing_ok=True)
    result = check(json.loads(args.report.read_text(encoding='utf-8-sig')), args.expected_project)
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
