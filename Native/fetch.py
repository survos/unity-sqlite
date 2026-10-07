#!/usr/bin/env python3
"""Fetch the official pinned source; verify before extraction."""
import hashlib, pathlib, urllib.request, zipfile
root = pathlib.Path(__file__).resolve().parent / 'sqlite'
root.mkdir(exist_ok=True)
archive = root / 'amalgamation.zip'
url = 'https://www.sqlite.org/2026/sqlite-amalgamation-3530400.zip'
expected = '628a44cfe82c66aed1ccbbe85a562d2e33ebe64b3288981ed76285612227934e'
if not archive.exists():
    urllib.request.urlretrieve(url, archive)
if hashlib.sha3_256(archive.read_bytes()).hexdigest() != expected:
    raise SystemExit('SQLite source checksum mismatch; delete archive and retry')
with zipfile.ZipFile(archive) as z:
    for name in ('sqlite3.c', 'sqlite3.h'):
        (root / name).write_bytes(z.read('sqlite-amalgamation-3530400/' + name))
print('Verified SQLite 3.53.4 amalgamation')
