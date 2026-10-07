#!/usr/bin/env python3
"""Compile against Unity's actual .NET Standard 2.1 reference assemblies (no Editor launch)."""
import os, pathlib, subprocess, sys
root = pathlib.Path(__file__).resolve().parent.parent
unity = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else pathlib.Path('/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/Resources/Scripting')
out = root / '.work/unity-api'
out.mkdir(parents=True, exist_ok=True)
sdk = unity / 'DotNetSdk'
refs = list((unity / 'NetStandard/ref/2.1.0').glob('*.dll'))
compiler = next((sdk / 'sdk').glob('*/Roslyn/bincore/csc.dll'))
args = ['/nologo', '/nostdlib+', '/langversion:9', '/define:UNITY_5_3_OR_NEWER', '/target:library', '/out:"' + str(out / 'Survos.Sqlite.dll') + '"']
args += ['/r:"' + str(p) + '"' for p in refs]
args += ['"' + str(p) + '"' for p in (root / 'Runtime').rglob('*.cs')]
(out / 'compile.rsp').write_text('\n'.join(args))
subprocess.run([str(sdk / 'dotnet'), str(compiler), '@' + str(out / 'compile.rsp')], env=dict(os.environ, TMPDIR=str(root / 'ValidationTemp')), check=True)
engine = unity / 'Managed/UnityEngine'
extra = ['/nologo', '/nostdlib+', '/langversion:9', '/target:library', '/out:"' + str(out / 'Smoke.dll') + '"']
extra += ['/r:"' + str(p) + '"' for p in refs + list(engine.glob('*.dll')) + [out / 'Survos.Sqlite.dll']]
extra += ['"' + str(root / p) + '"' for p in ('Tools~/UnitySmoke/SmokeBuild.cs', 'Tools~/UnitySmoke/SmokeRunner.cs', 'Tests/Runtime/SqliteChecks.cs', 'Samples~/BasicQuery/BasicQuery.cs')]
(out / 'smoke.rsp').write_text('\n'.join(extra))
subprocess.run([str(sdk / 'dotnet'), str(compiler), '@' + str(out / 'smoke.rsp')], env=dict(os.environ, TMPDIR=str(root / 'ValidationTemp')), check=True)
