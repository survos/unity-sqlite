# Working on Survos SQLite

This repository is the root of the Unity Package Manager package `com.survos.sqlite`.
Read `README.md` and `HANDOFF.md` before changing behavior or integrating it into an application.

## Scope and architecture

- Target Unity 6, C# 9, and .NET Standard 2.1. Initial native platforms are macOS ARM64 and Windows x64.
- Keep `Runtime/` ordinary C#, without UnityEngine dependencies, database MonoBehaviours, UI, configuration assets, ORMs, or reflection-based mapping.
- One connection owns one serialized worker and an asynchronous connection lease. All native database work belongs on that worker.
- Queries pull 64-row batches and yield owned row snapshots. Never replace streaming with unbounded materialization.
- Active enumerators hold the connection lease. Do not await another operation on the same connection inside its query loop.
- Transactions hold the lease for the complete callback. Use the scoped transaction argument; never let operations silently escape an automatically rolled-back transaction.
- Native handles use SafeHandle. Cleanup must run even after cancellation or exceptions. Keep UTF-8, byte counts, empty BLOBs, embedded text NULs, and native pointer lifetimes correct.
- Preserve static reverse P/Invoke delegates and MonoPInvokeCallback attributes. A managed or Editor test passing does not establish IL2CPP compatibility.
- Folio is an application convention for SQLite files. Do not add Folio schemas, imgproxy integration, image decoders, or ThumbHash algorithms to the core.
- Linux, Web/sqlite-wasm, pools, named parameters, backup APIs, and mapping are future work; do not add them without an explicit request.

## Find the relevant code

For unfamiliar code, start with one focused `jbcontext search "<descriptive query>"` if available; switch to direct reads after a relevant hit. Use exact `rg` searches when the file or symbol is already known. If jbcontext is unavailable, use `rg` and targeted reads.

For broader multi-step discovery, use a read-only `context_explorer` subagent if the current agent supports it. Give it a narrowly scoped question and wait for its report before duplicating its exploration. Do not assume every consuming Unity agent has these tools.

## Repository map

- `Runtime/SqliteDatabase.cs`: public API, leases, streaming, cancellation, transactions, disposal.
- `Runtime/SqliteStatement.cs`: preparation, binding, row reads, transaction SQL guard.
- `Runtime/Native/`: internal P/Invoke, handles, worker, AOT marker.
- `Runtime/Plugins/`: distributed native binaries, platform-specific importer settings, provenance records.
- `Native/`: pinned official SQLite source fetcher, native build scripts, license notices.
- `Tests/Runtime/SqliteChecks.cs`: shared integration checks for .NET, Unity, and the IL2CPP smoke player.
- `Tests~/Harness/`: standalone .NET 8 harness.
- `Tools~/`: API compilation check, smoke project generator, metadata helper.
- `Samples~/BasicQuery/`: minimal ordinary C# usage.

## Verification

Run checks appropriate to the changed behavior:

```sh
dotnet run --project Tests~/Harness/Harness.csproj
```

On a Mac with the Unity bundled SDK, `python3 Tools~/run-harness.py` is an alternative. Its optional argument is the SDK root. `python3 Tools~/compile-unity-api.py` compiles the runtime, sample, shared checks, and smoke scripts against installed Unity references; pass the Unity `Contents/Resources/Scripting` directory when the default installation differs.

For a real IL2CPP build, run `python3 Tools~/prepare-unity-smoke.py` and follow the build/player commands in README. Require the player exit code 0 and `SURVOS_IL2CPP_SMOKE_PASS` marker. Set `FOLIO_TEST_PATH` to an existing genuine archive to enable the otherwise-skipped read-only fixture test.

Keep regression tests for changes to native lifetime, cancellation, concurrency, transactions, UTF-8, and BLOB behavior. Verify FTS5, JSON, RTree, and actual compile options whenever replacing a native binary. Report which platform/runtime actually ran; never turn pending Windows or IL2CPP checks into a success claim.

## Native builds and packaging

- SQLite is pinned to 3.53.4. Verify the source checksum before compilation; update fetcher, version assertions, metadata, docs, and provenance together for a version change.
- macOS: `bash Native/build-macos.sh`.
- Windows: `Native/build-windows.ps1` in an x64 MSVC developer shell with Python and Windows SDK.
- Optional macOS-to-Windows build: `python3 Native/build-windows-cross.py`; tools/cache stay under `.work/`.
- Commit both intentional native binaries, their `.meta` files, provenance, and required license notices. Do not commit fetched amalgamation files, compiler downloads, debug outputs, caches, or generated validation projects.
- Preserve Unity GUIDs and plugin CPU/OS restrictions. `Tools~/generate-meta.py` creates missing metadata without replacing existing GUIDs.
- Survos code is MIT; SQLite is public domain. Preserve the separate Windows toolchain runtime notices.

## Handoff and product decisions

The owner is evaluating Unity, Godot, and a PWA for the consuming product. This package is useful infrastructure, not a commitment to a Unity application architecture. Keep the database format portable and the wrapper independent of application UI. Do not migrate engines or implement a Web backend as part of routine package maintenance.

When reporting changes, give the behavior changed, tests actually run, and remaining validation gaps. Update the handoff notes when a previously pending platform check is completed.
