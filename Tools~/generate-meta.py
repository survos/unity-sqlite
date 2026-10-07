#!/usr/bin/env python3
"""Create stable Unity GUIDs for package assets; never replace an existing GUID."""
import pathlib, uuid
root = pathlib.Path(__file__).resolve().parent.parent
namespace = uuid.UUID('807f14de-99a5-4e74-b676-809a5907dca3')
paths = [root / name for name in ('Runtime', 'Tests', 'Native', 'package.json', 'README.md', 'LICENSE', 'CHANGELOG.md', 'AGENTS.md', 'HANDOFF.md')]
for name in ('Runtime', 'Tests', 'Native'):
    paths += [p for p in (root / name).rglob('*') if not p.name.endswith('.meta') and not (p.parent == root / 'Native/sqlite' and p.name != 'README.md')]
for path in paths:
    meta = path.with_name(path.name + '.meta')
    if meta.exists(): continue
    guid = uuid.uuid5(namespace, path.relative_to(root).as_posix()).hex
    data = 'fileFormatVersion: 2\nguid: ' + guid + '\n'
    if path.is_dir(): data += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
    elif path.suffix == '.cs': data += 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif path.suffix == '.asmdef': data += 'AssemblyDefinitionImporter:\n  externalObjects: {}\n'
    elif path.suffix in ('.dll', '.dylib'):
        mac = path.suffix == '.dylib'
        cpu, osname, target = ('ARM64', 'OSX', 'OSXUniversal') if mac else ('x86_64', 'Windows', 'Win64')
        data += '''PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        CPU: CPU_VALUE
        DefaultValueInitialized: true
        OS: OS_VALUE
  - first:
      Standalone: TARGET_VALUE
    second:
      enabled: 1
      settings:
        CPU: CPU_VALUE
  userData:
  assetBundleName:
  assetBundleVariant:
'''.replace('CPU_VALUE', cpu).replace('OS_VALUE', osname).replace('TARGET_VALUE', target)
    else: data += 'DefaultImporter:\n  externalObjects: {}\n'
    meta.write_text(data)
