#!/usr/bin/env python3
"""Compile using an installed .NET SDK, without NuGet or first-run machine writes."""
import json, os, pathlib, shutil, subprocess, sys
root = pathlib.Path(__file__).resolve().parent.parent
sdk = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else pathlib.Path('/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/Resources/Scripting/DotNetSdk')
out = root / '.work' / 'harness'
out.mkdir(parents=True, exist_ok=True)
env = dict(os.environ, TMPDIR=str(root / '.work' / 'tmp'), DOTNET_CLI_HOME=str(root / '.work' / 'dotnet'))
refs = sorted((sdk / 'packs' / 'Microsoft.NETCore.App.Ref').glob('*/ref/net8.0/*.dll'))
compiler = next((sdk / 'sdk').glob('*/Roslyn/bincore/csc.dll'))
args = ['/nologo', '/nostdlib+', '/langversion:9', '/target:exe', '/out:"' + str(out / 'Harness.dll') + '"']
args += ['/r:"' + str(p) + '"' for p in refs]
args += ['"' + str(p) + '"' for p in (list((root / 'Runtime').rglob('*.cs')) + [root / 'Tests/Runtime/SqliteChecks.cs', root / 'Tests~/Harness/Program.cs'])]
(out / 'compile.rsp').write_text('\n'.join(args))
subprocess.run([str(sdk / 'dotnet'), str(compiler), '@' + str(out / 'compile.rsp')], env=env, check=True)
(out / 'Harness.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': '8.0.0'}}}))
for plugin in list((root / 'Runtime/Plugins').rglob('*.dylib')) + list((root / 'Runtime/Plugins').rglob('*.dll')):
    shutil.copy2(plugin, out)
subprocess.run([str(sdk / 'dotnet'), str(out / 'Harness.dll')], env=env, check=True)
