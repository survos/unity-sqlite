# Native components

SQLite 3.53.4 is public domain; see https://www.sqlite.org/copyright.html and `sqlite/README.md` for pinned source provenance.

The included Windows binary was cross-compiled using Zig 0.14.1's Clang/MinGW toolchain. Runtime support from that toolchain may be linked into the DLL; the toolchain's license and MinGW notices are retained verbatim in `licenses/Zig-LICENSE.txt` and `licenses/MinGW-COPYING.txt`. The DLL imports Windows system/UCRT APIs. The compiler itself is not part of the package.

Rebuilding with MSVC uses its C runtime instead. Native build scripts and per-binary provenance records identify the actual compiler used.
