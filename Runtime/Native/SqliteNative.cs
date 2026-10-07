using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Survos.Sqlite.Native
{
    internal static class SqliteNative
    {
        internal const string Library = "survos_sqlite";
        internal static readonly IntPtr Transient = new IntPtr(-1);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int ProgressCallback(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int AuthorizerCallback(IntPtr context, int action, IntPtr arg1, IntPtr arg2, IntPtr database, IntPtr trigger);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_set_authorizer(SafeSqliteDatabaseHandle db, AuthorizerCallback callback, IntPtr context);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_open_v2(byte[] path, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_errmsg(SafeSqliteDatabaseHandle db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_extended_errcode(SafeSqliteDatabaseHandle db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_prepare_v2(SafeSqliteDatabaseHandle db, IntPtr sql, int length, out IntPtr statement, out IntPtr tail);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_step(SafeSqliteStatementHandle statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_finalize(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_parameter_count(SafeSqliteStatementHandle statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_null(SafeSqliteStatementHandle statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_int64(SafeSqliteStatementHandle statement, int index, long value);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_double(SafeSqliteStatementHandle statement, int index, double value);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_text(SafeSqliteStatementHandle statement, int index, byte[] value, int size, IntPtr destructor);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_blob(SafeSqliteStatementHandle statement, int index, byte[] value, int size, IntPtr destructor);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_column_count(SafeSqliteStatementHandle statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_name(SafeSqliteStatementHandle statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_column_type(SafeSqliteStatementHandle statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern long sqlite3_column_int64(SafeSqliteStatementHandle statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern double sqlite3_column_double(SafeSqliteStatementHandle statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_text(SafeSqliteStatementHandle statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_blob(SafeSqliteStatementHandle statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_column_bytes(SafeSqliteStatementHandle statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_changes(SafeSqliteDatabaseHandle db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_get_autocommit(SafeSqliteDatabaseHandle db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_busy_timeout(SafeSqliteDatabaseHandle db, int milliseconds);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_libversion();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void sqlite3_progress_handler(SafeSqliteDatabaseHandle db, int instructions, ProgressCallback callback, IntPtr context);

        internal static byte[] Utf8(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.IndexOf('\0') >= 0) throw new ArgumentException("NUL is not allowed in paths or SQL.");
            return Encoding.UTF8.GetBytes(text + "\0");
        }
        internal static string Text(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return "";
            int count = 0;
            while (Marshal.ReadByte(pointer, count) != 0) count++;
            return Encoding.UTF8.GetString(Bytes(pointer, count));
        }
        internal static byte[] Bytes(IntPtr pointer, int count)
        {
            var bytes = new byte[count];
            if (count != 0) Marshal.Copy(pointer, bytes, 0, count);
            return bytes;
        }
    }
}
