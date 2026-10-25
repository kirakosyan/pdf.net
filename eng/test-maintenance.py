"""Offline source maintenance drill; never creates or publishes SDK packages."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    output = Path(args.output).resolve()
    if output.exists():
        raise ValueError('Use a new output directory; previous evidence is immutable')
    output.mkdir(parents=True)
    report = {'schemaVersion': 1, 'purpose': 'source-maintenance-drill',
              'productionQualified': False, 'sourceCommit': subprocess.check_output(
                  ['git', 'rev-parse', 'HEAD'], cwd=ROOT).decode().strip(),
              'commands': [], 'apiCases': [], 'releaseGates': []}

    def run(name, command):
        result = subprocess.run(command, cwd=ROOT, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=180)
        log = output / (name + '.log')
        log.write_bytes(result.stdout)
        report['commands'].append({'name': name, 'arguments': command,
                                   'exitCode': result.returncode,
                                   'log': log.name, 'sha256': sha(log)})
        return result.returncode, result.stdout.decode('utf-8', errors='replace')

    try:
        sources = {
            'baseline': 'public static class Api { public static int Count(int value) => value; }',
            'patch': 'public static class Api { public static int Count(int value) => checked(value + 0); }',
            'addition': 'public static class Api { public static int Count(int value) => value; public static int Extra() => 1; }',
            'removal': 'public static class Api { }',
            'parameter': 'public static class Api { public static int Count(int renamed) => renamed; }',
        }
        assemblies = {}
        for name, source in sources.items():
            folder = output / name
            folder.mkdir()
            (folder / 'Api.cs').write_text(source, encoding='utf-8')
            project = folder / 'Fixture.csproj'
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                               '<TargetFramework>net10.0</TargetFramework><AssemblyName>MaintenanceFixture</AssemblyName>'
                               '<IsPackable>false</IsPackable><EnableNETAnalyzers>false</EnableNETAnalyzers>'
                               '</PropertyGroup></Project>', encoding='utf-8')
            config = folder / 'NuGet.Config'
            config.write_text('<configuration><packageSources><clear /></packageSources></configuration>', encoding='utf-8')
            code, _ = run('build-' + name, ['dotnet', 'build', str(project), '-c', 'Release',
                          '-p:ImportDirectoryBuildProps=false', '-p:ImportDirectoryBuildTargets=false',
                          '-p:UseSharedCompilation=false', '-p:RestoreConfigFile=' + str(config)])
            if code:
                raise ValueError('Fixture build failed: ' + name)
            assemblies[name] = folder / 'bin/Release/net10.0/MaintenanceFixture.dll'
        for name in ('baseline', 'patch', 'addition', 'removal', 'parameter'):
            code, log = run('api-' + name, ['dotnet', 'apicompat', '--left', str(assemblies['baseline']),
                            '--right', str(assemblies[name]), '--enable-rule-cannot-change-parameter-name'])
            expected = {'removal': 'CP0002', 'parameter': 'CP0017'}.get(name)
            if (expected and (code == 0 or expected not in log)) or (not expected and code != 0):
                raise ValueError('Unexpected API result: ' + name)
            report['apiCases'].append({'case': name, 'exitCode': code, 'expectedDiagnostic': expected,
                                       'leftSha256': sha(assemblies['baseline']), 'rightSha256': sha(assemblies[name])})
        old = output / 'dependency-before.txt'
        new = output / 'dependency-after.txt'
        old.write_bytes(b'original synthetic dependency revision 1\n')
        new.write_bytes(b'original synthetic dependency revision 2\n')
        recorded_hash = sha(old)
        if recorded_hash == sha(new):
            raise ValueError('Changed dependency unexpectedly reused prior hash')
        report['dependencySimulation'] = {'syntheticOnly': True, 'oldSha256': recorded_hash,
                                          'newSha256': sha(new), 'priorEvidenceApplicable': False,
                                          'actualDependencyUpdated': False}
        for stage in ('BeforePack', 'Publish'):
            code, log = run('gate-' + stage, ['pwsh', '-NoProfile', '-File',
                            str(ROOT / 'eng/assert-production-ready.ps1'), '-Stage', stage])
            if code == 0 or 'readiness is false' not in log:
                raise ValueError('Unqualified release gate did not fail for expected reason: ' + stage)
            report['releaseGates'].append({'stage': stage, 'blocked': True})
        report['passed'] = True
    finally:
        (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('MAINTENANCE_SOURCE_DRILL_PASS;5-api-cases;2-release-gates;not-production-qualification')


if __name__ == '__main__':
    main()
