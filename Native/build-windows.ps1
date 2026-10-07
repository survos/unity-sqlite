# Run in an x64 Native Tools Command Prompt for Visual Studio, with Python 3.
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    python fetch.py
    if ($LASTEXITCODE -ne 0) { throw 'Source verification failed' }
    $out = '../Runtime/Plugins/Windows/survos_sqlite.dll'
    cl /nologo /O2 /MT /LD '/DSQLITE_API=__declspec(dllexport)' /DSQLITE_THREADSAFE=1 /DSQLITE_ENABLE_FTS5 /DSQLITE_ENABLE_RTREE /DSQLITE_OMIT_LOAD_EXTENSION /DSQLITE_DQS=0 sqlite/sqlite3.c /link /MACHINE:X64 /OUT:$out
    if ($LASTEXITCODE -ne 0) { throw 'Native build failed' }
    @('SQLite 3.53.4; MSVC x64; flags: Native/build-windows.ps1', (Get-FileHash $out -Algorithm SHA256).Hash) | Set-Content "$out.provenance.txt"
} finally { Pop-Location }
