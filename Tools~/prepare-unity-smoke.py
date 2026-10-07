#!/usr/bin/env python3
"""Prepare a minimal Unity project with a local UPM dependency and standalone smoke runner."""
import json, pathlib, shutil, sys
root = pathlib.Path(__file__).resolve().parent.parent
project = pathlib.Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else root / 'Validation~' / 'UnitySmoke'
for directory in ('Assets/Editor', 'Packages', 'ProjectSettings'):
    (project / directory).mkdir(parents=True, exist_ok=True)
(project / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {'com.survos.sqlite': 'file:' + str(root)}} , indent=2))
shutil.copy2(root / 'Tools~/UnitySmoke/SmokeRunner.cs', project / 'Assets/SmokeRunner.cs')
shutil.copy2(root / 'Tests/Runtime/SqliteChecks.cs', project / 'Assets/SqliteChecks.cs')
shutil.copy2(root / 'Tools~/UnitySmoke/SmokeBuild.cs', project / 'Assets/Editor/SmokeBuild.cs')
print(project)
