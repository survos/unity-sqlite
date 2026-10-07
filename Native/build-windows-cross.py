#!/usr/bin/env python3
"""Optional reproducible Windows x64 build on Apple Silicon using workspace-local Zig."""
import hashlib, os, pathlib, platform, shutil, subprocess, tarfile, urllib.request
root = pathlib.Path(__file__).resolve().parent.parent
if platform.system() != 'Darwin' or platform.machine() != 'arm64':
    raise SystemExit('This bootstrap targets Apple Silicon. On Windows use build-windows.ps1.')
work = root / '.work'
work.mkdir(exist_ok=True)
archive = work / 'zig-aarch64-macos-0.14.1.tar.xz'
expected = '39f3dc5e79c22088ce878edc821dedb4ca5a1cd9f5ef915e9b3cc3053e8faefa'
if not archive.exists():
    urllib.request.urlretrieve('https://ziglang.org/download/0.14.1/' + archive.name, archive)
if hashlib.sha256(archive.read_bytes()).hexdigest() != expected:
    raise SystemExit('Zig checksum mismatch')
zig = work / 'zig-aarch64-macos-0.14.1' / 'zig'
if not zig.exists():
    with tarfile.open(archive) as tar:
        tar.extractall(work, filter='data')
subprocess.run(['python3', str(root / 'Native/fetch.py')], check=True)
build = work / 'windows-build'
build.mkdir(exist_ok=True)
out = build / 'survos_sqlite.dll'
flags = ['-target', 'x86_64-windows-gnu', '-shared', '-O2', '-DSQLITE_API=__declspec(dllexport)',
         '-DSQLITE_THREADSAFE=1', '-DSQLITE_ENABLE_FTS5', '-DSQLITE_ENABLE_RTREE', '-DSQLITE_OMIT_LOAD_EXTENSION', '-DSQLITE_DQS=0']
env = dict(os.environ, ZIG_GLOBAL_CACHE_DIR=str(work / 'zig-cache'), ZIG_LOCAL_CACHE_DIR=str(work / 'zig-local'), TMPDIR=str(work / 'tmp'))
(work / 'tmp').mkdir(exist_ok=True)
subprocess.run([str(zig), 'cc'] + flags + [str(root / 'Native/sqlite/sqlite3.c'), '-o', str(out)], check=True, env=env)
destination = root / 'Runtime/Plugins/Windows/survos_sqlite.dll'
shutil.copy2(out, destination)
out = destination
out.with_name(out.name + '.provenance.txt').write_text('SQLite 3.53.4; Zig 0.14.1 (Clang/MinGW); x86_64-windows-gnu\n'
    + 'Zig archive SHA256: ' + expected + '\nFlags: ' + ' '.join(flags) + '\nDLL SHA256: ' + hashlib.sha256(out.read_bytes()).hexdigest() + '\n')
print(out)
