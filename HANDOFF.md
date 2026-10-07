# Unity SQLite implementation and integration handoff

## Delivery snapshot — 2026-10-07

The repository root is a UPM package named `com.survos.sqlite`, version `0.1.0` (unreleased). It was created in `/Users/tac/unity/sqlite` for use from ordinary Unity 6 C# classes. This is an implementation handoff, not a declaration that all Phase 1 release criteria are satisfied.

The consuming product may ultimately use Unity, Godot, or a PWA. The owner's current direction is to retain this package for situations where native Unity SQLite is useful while reconsidering the application platform separately. The generic SQLite/Folio data format remains portable.

## What is implemented

- Native SQLite 3.53.4 from a checksum-pinned official amalgamation; no dependence on the host OS SQLite version.
- macOS ARM64 dylib built with Apple Clang, and Windows x64 DLL cross-built with Zig 0.14.1/Clang/MinGW. Both have source/build provenance and Unity importer metadata.
- Internal C API bindings with explicit UTF-8 boundaries, transient bind copies, owned row data, and SafeHandle cleanup.
- Async open, execute, scalar reads, and pull-based streaming in batches of up to 64 rows.
- A serialized worker per connection; independent connections run independently. A lease covers each operation and the lifetime of a streaming enumerator.
- Cancellation while waiting, between rows, and within SQLite execution through a progress callback. Busy timeout is 250 ms.
- Scoped immediate transactions, rollback, reentrancy detection, blocked raw transaction SQL, and rejection of work after SQLite automatically rolls back a transaction.
- Read-only, existing read/write, and create modes; custom extensions such as `.folio` work without special handling.
- Null, boolean, integer, floating-point, string, and BLOB bindings; name/index row access; useful SQLite primary/extended errors.
- FTS5, JSON, and RTree tests; shared integration suite, simple timing diagnostics, Unity test assembly, minimal sample, and standalone IL2CPP smoke-project generator.
- MIT license for package code, separate public-domain SQLite notice, and Windows toolchain runtime notices.

## Validation evidence and limits

| Check | Result at handoff |
| --- | --- |
| macOS ARM64 native compilation | Passed; Apple Clang 21.0.0, ad-hoc signed |
| Windows x64 native compilation | Cross-build passed; inspected as PE32+ x86-64 DLL |
| Windows execution | Pending; no Windows runtime used in this session |
| macOS .NET 8 integration suite | Passed against the distributed dylib |
| Unity API reference compilation | Passed using Unity 6000.6.4f1 .NET Standard 2.1 references, including sample and smoke scripts |
| Clean Unity project import | Pending; Editor initialization failed under the session's filesystem restrictions |
| Unity standalone IL2CPP execution | Pending; the Editor reported a read-only project/disk before building |
| Genuine museum/newspaper Folio | Pending; no real fixture supplied; optional smoke check reports a skip |
| GitHub native workflow | Added; its results must be checked separately after publication |

The integration suite verifies normal and invalid opens, read-only writes, all supported binding types, Unicode and embedded text NULs, empty/nonempty BLOBs, query cleanup, error codes, cancellation, queued work, transaction isolation, automatic rollback, concurrency, long-query disposal, and repeated open/close. It exercises actual FTS5, JSON, and RTree SQL and inspects compile options.

Local timing diagnostics observed approximately 3–5 ms for a 10,000-row generated query. These are harness observations on the development machine, not Unity frame-time guarantees or a formal benchmark. Approximate heap deltas are diagnostics only.

## First actions for the Unity integrator

1. Read `AGENTS.md` and the threading/disposal sections of README.
2. Add this checkout's `package.json` through Package Manager, or use its GitHub URL once published. Set API compatibility to .NET Standard 2.1.
3. Import Basic Query and invoke it from an existing async application entry point with `Application.persistentDataPath` and `Debug.Log`.
4. Enable package tests with the Unity Test Framework and `testables: ["com.survos.sqlite"]`; run the native integration test.
5. Generate the standalone smoke project using `python3 Tools~/prepare-unity-smoke.py`. Build with IL2CPP, run the resulting player, and require exit 0 plus `SURVOS_IL2CPP_SMOKE_PASS`. Repeat on both target platforms.
6. Set `FOLIO_TEST_PATH` to a genuine archive and retrieve real records read-only. Record the Unity version, platform, player backend, binary provenance, and outcomes.
7. Only after these checks pass, update the status in README/HANDOFF and consider a release tag.

Do not open a write/create database in a read-only packaged asset location. Copy a mutable database to persistent data first. Prefer read-only mode for distributed archives.

## Usage constraints worth retaining

- Never await another operation on the same connection inside an active query loop. End enumeration first or use another connection.
- Await and dispose manually acquired enumerators. A paused enumerator owns the connection until released.
- Within a transaction callback, use `tx`, await every operation, and let the callback finish before disposing the database.
- Cancellation does not undo a write that has already committed. Use transactions for atomic groups of changes.
- Batches bound row count, not bytes: a single large BLOB can still allocate substantial memory.
- Keep main-thread Unity work in the application; the core does not invoke Unity APIs.
- Linux, Intel Mac/Rosetta, Web, encryption, incremental BLOB I/O, and typed mapping are not implemented.

## imgproxy and visual placeholders

The owner confirmed that imgproxy Pro can return ThumbHash through `/info`. The importer should obtain it, normalize the response representation to raw bytes (decode base64 when appropriate), and store it in a BLOB beside asset metadata. Unity retrieves the bytes offline and decodes them in the image layer while fetching a larger image. The package tests byte-preserving storage; it does not include an imgproxy client or image decoder. BlurHash text and tiny WebP BLOBs are also ordinary values the wrapper can transport.

## Publication and local checkout

The public repository is `https://github.com/survos/unity-sqlite`. The package is being published through GitHub's Git Database API. The CI workflow is prepared locally at `.github/workflows/native.yml`, but publication of that file is pending the CLI token's `workflow` scope; the remaining package files do not require that additional scope.

This session can write package files but cannot initialize `.git` in the local workspace. Publication can therefore use GitHub's Git Database API to make a real remote commit without local Git metadata. After publication, connect this directory from an unrestricted terminal:

```sh
cd ~/unity/sqlite
git init -b main
git remote add origin git@github.com:survos/unity-sqlite.git
git fetch origin
git reset --mixed origin/main
git branch --set-upstream-to=origin/main main
git status
```

`reset --mixed` establishes the remote commit and index without overwriting working files. Review any differences before committing additional changes. Alternatively, clone into a fresh directory and install that checkout in Unity.

Generated `.work/`, validation projects, compiler downloads, and test logs are intentionally excluded from publication. Native libraries, provenance, source build scripts, licenses, package metadata, tests, samples, and documentation belong in the repository.
