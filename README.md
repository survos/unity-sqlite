# Survos SQLite for Unity 6

A small native SQLite package for ordinary C# classes in Unity desktop applications.

For integration and maintenance, start with [HANDOFF.md](HANDOFF.md) and [AGENTS.md](AGENTS.md). The handoff records actual validation evidence and the remaining release checks.

```csharp
using Survos.Sqlite;

await using var db = await SqliteDatabase.OpenAsync("museum.folio");

await foreach (var row in db.QueryAsync("SELECT id, title FROM object LIMIT 10"))
{
    Debug.Log(row.GetString("title"));
}
```

Use a writable absolute path such as `Path.Combine(Application.persistentDataPath, "museum.folio")` when creating a database. Existing archives can use any extension, including `.folio`.

**Status: 0.1.0 implementation, pending desktop release validation.** This repository contains the macOS ARM64 and Windows x64 native libraries and their reproducible build scripts. The macOS .NET integration suite and compilation against Unity's .NET Standard 2.1 references pass. A full Unity Editor import, Windows execution and IL2CPP standalone run have not yet been verified. Do not treat Editor/API compilation as IL2CPP success.

## Requirements and installation

- Unity 6 (`6000.0`+) with .NET Standard 2.1 API compatibility and C# 9.
- macOS Apple Silicon (ARM64 Editor/player, macOS 11+) or Windows x64.
- No runtime managed dependencies and no `UnityEngine` reference in the core assembly.

For this local checkout, choose **Package Manager → Install package from disk**, then select the repository's `package.json`. The UPM package is at the repository root.

Install from Git:

```text
https://github.com/survos/unity-sqlite.git
```

Pin a tested tag or commit in production. Native build scripts do not publish releases. For a scoped registry, publish the same package as `com.survos.sqlite` to your registry, add the `com.survos` scope to Unity's scoped registries, then install the package by name. No public registry is configured yet.

The `Survos.Sqlite` assembly isolates the implementation from application recompilation. Import **Basic Query** from the package Samples section for an ordinary C# example.

## Execute, bind and read

```csharp
await db.ExecuteAsync("CREATE TABLE IF NOT EXISTS asset(id INTEGER PRIMARY KEY, title TEXT, thumbhash BLOB)");
await db.ExecuteAsync("INSERT INTO asset(title, thumbhash) VALUES(?, ?)", "Photograph", thumbhashBytes);

long count = await db.ScalarAsync<long>("SELECT COUNT(*) FROM asset");
await foreach (var row in db.QueryAsync("SELECT id, title, thumbhash FROM asset WHERE id >= ? LIMIT ?", 1, 100))
{
    long id = row.GetInt64("id");
    string title = row.GetString(1);
    byte[] hash = row.GetBlob("thumbhash");
}
```

Parameters accept `null`, `bool`, `int`, `long`, `float`, `double`, `string`, and `byte[]`. Use positional `?` parameters; values are never interpolated into SQL. To bind a single null with the `params` overload, use `(object)null`. Parameter counts must match. Do not mutate parameter arrays or BLOBs until the operation has finished.

One statement is allowed per call; additional statements are rejected, while trailing comments are accepted. Batch changes belong in a transaction. `ExecuteAsync` drains the statement and returns SQLite's `sqlite3_changes` value (meaningful for INSERT/UPDATE/DELETE, excluding trigger changes). Use `ScalarAsync<long>("SELECT last_insert_rowid()")` inside the same transaction callback as the insert if the generated ID is needed.

Rows hold owned snapshots and share a column-name array per query, with no per-row dictionaries. They remain valid after advancing or closing the database. Name matching is ordinal and case-sensitive; duplicate column names resolve to the first column, so alias duplicates. Use `IsNull` before reading a nullable numeric field. Integer values are 64-bit; `GetDouble` also accepts an integer. `GetString`/`GetBlob` return null for SQL NULL. Indexers expose SQLite storage values (`long`, `double`, `string`, `byte[]`, or null). Returned BLOB arrays are owned by the row and may be modified by the caller.

`ScalarAsync<T>` reads the first column of the first row, supports ordinary primitive conversions, and throws when there is no row or when SQL NULL is read into a non-nullable value type. It performs no POCO mapping.

## Threading, streaming and cancellation

One database connection owns one dedicated serialized worker. Native open, prepare, bind, step and close run there; no Unity APIs run on that worker. Independent database instances have independent workers. Keep the number of connections modest; there is no connection pool.

Public operations acquire an asynchronous connection lease **before** queuing worker work. Queries pull up to **64 rows per batch**, then yield snapshots to the caller. There is no producer filling an unbounded queue: the next batch is fetched only when the caller exhausts the current one. Memory is bounded by a batch's data plus any rows the consumer retains, not a fixed byte limit; an individual large BLOB still requires memory for its full contents.

A streaming enumerator holds the connection lease until it completes or is disposed. `await foreach` handles cleanup on `break` and exceptions. **Do not await another operation on the same database inside that loop.** Use a second connection or collect a bounded set of rows, finish the enumeration, and then write. Dispose manually obtained enumerators with `await using`.

```csharp
await foreach (var row in db.QueryAsync("SELECT * FROM asset", cancellationToken))
{
    // Consume one row. Normal await captures the caller's synchronization context.
}
```

`.WithCancellation(token)` is also supported. Cancellation works while waiting for a lease, between rows, and during long SQLite execution through a native progress callback every 1,000 virtual-machine instructions. SQLite's busy timeout is 250 ms; cancellation cannot instantly preempt OS I/O or SQLite's busy-handler sleep. A canceled write may already have completed; use a transaction when atomicity is required. Do not assume canceling an operation undoes a committed write.

Prefer `await using`. `DisposeAsync` rejects new work, cancels in-flight work, waits for leases and finalization, then closes the connection and worker. Consumers must release active enumerators and return from transaction callbacks; disposal does not forcibly unwind arbitrary user code. Never dispose the database from inside its own query loop or transaction callback. Synchronous `Dispose` waits and may block its calling thread.

## Transactions and open modes

```csharp
await db.TransactionAsync(async tx =>
{
    await tx.ExecuteAsync("INSERT INTO asset(title) VALUES(?)", "Newspaper");
    await tx.ExecuteAsync("INSERT INTO asset(title) VALUES(?)", "Map");
}, cancellationToken);

await using var archive = await SqliteDatabase.OpenAsync(path, SqliteOpenMode.ReadOnly);
```

Modes are `ReadOnly`, `ReadWrite` (existing file), and `ReadWriteCreate` (default). Opening reads the schema so invalid database files fail early. `:memory:` is supported. Paths containing NUL are rejected; text values may contain embedded NUL.

Transactions use `BEGIN IMMEDIATE`. The entire callback owns the connection lease, including awaits, so unrelated operations cannot interleave. Use the supplied `tx` for execute/scalar calls and await every operation. A callback exception or cancellation rolls back; rollback failures preserve the original exception in an `AggregateException`. Transaction objects expire when the callback returns. Raw transaction/savepoint SQL is rejected through SQLite's authorizer, and using the database directly in its own transaction callback throws instead of deadlocking. Nested transactions and transaction streaming are not part of 0.1.

## SQLite version and native provenance

Pinned SQLite: **3.53.4**, official [amalgamation](https://www.sqlite.org/amalgamation.html).

- Source URL: `https://www.sqlite.org/2026/sqlite-amalgamation-3530400.zip`
- Archive SHA3-256: `628a44cfe82c66aed1ccbbe85a562d2e33ebe64b3288981ed76285612227934e`
- `SqliteDatabase.LibraryVersion` and `db.SqliteVersion` report the loaded native version.
- Per-binary compiler/architecture/flags/hash records are adjacent to the plugins as `.provenance.txt` files.

Explicit compile flags on both platforms:

```text
SQLITE_THREADSAFE=1
SQLITE_ENABLE_FTS5
SQLITE_ENABLE_RTREE
SQLITE_OMIT_LOAD_EXTENSION
SQLITE_DQS=0
```

JSON functions are built into this SQLite version; there is no `ENABLE_JSON1` requirement. Double-quoted string literals are disabled: use single quotes for literals and bound parameters for values. Extension loading is omitted. Connections request full mutex protection. Defaults such as journal mode and foreign-key enforcement remain SQLite defaults; configure them explicitly for your application.

Inspect the actual distributed library with:

```csharp
string[] options = await db.GetCompileOptionsAsync(); // PRAGMA compile_options
```

Build on Apple Silicon with `bash Native/build-macos.sh`. This uses Apple Clang and ad-hoc signs the dylib; the consuming application's release signing/notarization is separate. On Windows run `Native/build-windows.ps1` in an x64 Visual Studio developer shell with Python 3 and the Windows SDK. Alternatively, on Apple Silicon run `python3 Native/build-windows-cross.py`, which verifies and caches Zig 0.14.1 locally and cross-compiles the x64 DLL. Native sources/build tools are separate from `Runtime/Native`, which contains only C# interop. Source downloads are checksum-verified before extraction.

## Tests and IL2CPP

Run the same integration checks outside Unity with .NET 8:

```sh
dotnet run --project Tests~/Harness/Harness.csproj
```

`Tools~/run-harness.py` can use Unity's bundled .NET SDK without NuGet. Pass a different SDK root if needed. `Tools~/compile-unity-api.py` checks the runtime against Unity's actual .NET Standard 2.1 reference assemblies. The GitHub workflow tests committed binaries on macOS/Windows, rebuilds them and tests again; the workflow has not been run locally.

For the Unity Test Runner, add `"testables": ["com.survos.sqlite"]` and `com.unity.test-framework` to the consuming project's package manifest, then run `Survos.Sqlite.Tests`. Tests cover opening/modes/invalid files, parameter types, UTF-8 and embedded NUL, empty/nonempty BLOBs, errors, statement cleanup, cancellations, transaction isolation/rollback, feature queries, independent connections, disposal and repeated open/close. Diagnostic timings cover opening, 1/100/10,000 rows, FTS and BLOB retrieval. Heap deltas are approximate diagnostics, not a benchmark claim.

For an actual standalone IL2CPP test:

```sh
python3 Tools~/prepare-unity-smoke.py
# Set UNITY to your Unity Editor executable.
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PWD/Validation~/UnitySmoke" -buildTarget StandaloneOSX \
  -executeMethod SmokeBuild.Build -logFile "$PWD/unity-build.log"
"$PWD/Validation~/UnitySmoke/Build/Smoke.app/Contents/MacOS/Smoke" \
  -batchmode -nographics -logFile "$PWD/il2cpp-player.log"
```

On Windows select `StandaloneWindows64` and run `Build/Smoke.exe`. Install Unity's IL2CPP desktop module and the target C++ toolchain. The smoke player runs the same feature/cancellation/transaction tests through IL2CPP, logs `SURVOS_IL2CPP_SMOKE_PASS`, and exits 0 on success. Check both the exit code and marker; building alone does not prove the plugin loaded.

Current local evidence (2026-10-07): macOS ARM64 native build and .NET integration checks passed; Unity .NET Standard reference compilation passed; Windows x64 DLL cross-built, but not executed. Unity 6000.6.4f1 could not initialize its project under this restricted session (Editor reports a read-only project/disk), so clean-project and IL2CPP validation remain **pending**. No full Xcode application is installed here; Apple Command Line Tools are available. No genuine Folio fixture was supplied. Set `FOLIO_TEST_PATH` to an existing archive to run read-only integrity checking and retrieve up to 10 real records from its first application table.

## ThumbHash and Folio assets

An importer can obtain ThumbHash from an imgproxy Pro `/info` response, normalize its response representation to raw bytes, and store it in a `thumbhash BLOB` column beside the asset's dimensions, MIME type and external original URL. If the response represents the hash as base64, decode that representation before binding the bytes. Retrieve it with `row.GetBlob("thumbhash")` and decode/display it in the application's image layer while higher-resolution media loads.

[ThumbHash](https://evanw.github.io/thumbhash/) encodes a compact visual placeholder including aspect ratio and optional alpha. BlurHash can instead be stored as text. Tiny WebP derivatives can be stored as BLOBs too; large originals can stay external. The package provides storage and transport only: it has no imgproxy client, image decoder, ThumbHash algorithm, pHash algorithm or Folio domain schema. A pHash is for similarity, not reconstructing a placeholder.

## Errors, limits and roadmap

`SqliteException` exposes the primary `ResultCode`, `ExtendedResultCode`, native message and relevant SQL (parameters are not interpolated). SQL itself can contain sensitive literals; decide what to log in the application. The core has no logger or Unity `Debug` dependency.

Only ARM64 macOS and x64 Windows desktop are targeted. Other platforms have no bundled backend and will not load this native library. No Intel Mac/Rosetta support, Linux binary, Web/WASM, encryption, ORM, reflection mapping, named-parameter API, connection pooling, custom VFS, backup API or incremental BLOB I/O is implemented. `.folio` is an ordinary SQLite file with a custom extension.

After standalone validation: Linux, then an official `sqlite-wasm` Web backend; later OPFS, named parameters, bulk helpers and backups as needed. No UI systems, MonoBehaviour managers, editor configuration, ScriptableObjects or legacy Unity dependencies are required.

Survos code is MIT licensed. Bundled [SQLite is public domain](https://www.sqlite.org/copyright.html), not MIT licensed. Windows toolchain runtime notices are retained in `Native/THIRD_PARTY_NOTICES.md` and `Native/licenses/`.
