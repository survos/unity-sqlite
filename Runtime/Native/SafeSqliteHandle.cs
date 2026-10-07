using System;
using Microsoft.Win32.SafeHandles;

namespace Survos.Sqlite.Native
{
    internal sealed class SafeSqliteDatabaseHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeSqliteDatabaseHandle(IntPtr pointer) : base(true) { SetHandle(pointer); }
        protected override bool ReleaseHandle() { return SqliteNative.sqlite3_close_v2(handle) == 0; }
    }
    internal sealed class SafeSqliteStatementHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeSqliteStatementHandle(IntPtr pointer) : base(true) { SetHandle(pointer); }
        // finalize frees the statement even when returning its last execution error.
        protected override bool ReleaseHandle() { SqliteNative.sqlite3_finalize(handle); return true; }
    }
}
