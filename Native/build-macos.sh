#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
python3 fetch.py
out=../Runtime/Plugins/macOS/libsurvos_sqlite.dylib
clang -arch arm64 -mmacosx-version-min=11.0 -O2 -dynamiclib \
  -DSQLITE_THREADSAFE=1 -DSQLITE_ENABLE_FTS5 -DSQLITE_ENABLE_RTREE \
  -DSQLITE_OMIT_LOAD_EXTENSION -DSQLITE_DQS=0 \
  -Wl,-install_name,@rpath/libsurvos_sqlite.dylib sqlite/sqlite3.c -o "$out"
codesign --force --sign - "$out"
{ clang --version; echo 'arm64; macOS >= 11.0; flags: see Native/build-macos.sh'; shasum -a 256 "$out"; } > "$out.provenance.txt"
