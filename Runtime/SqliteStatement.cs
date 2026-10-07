using System;
using System.Runtime.InteropServices;
using System.Text;
using Survos.Sqlite.Native;

namespace Survos.Sqlite
{
    // Worker-only. Public callers cannot accidentally step a native handle on Unity's thread.
    internal sealed class SqliteStatement : IDisposable
    {
        internal readonly SafeSqliteStatementHandle Handle;
        private readonly SqliteDatabase database;
        private readonly string sql;
        private string[] names;
        private static readonly SqliteNative.AuthorizerCallback authorizer = Authorize;
        [MonoPInvokeCallback(typeof(SqliteNative.AuthorizerCallback))]
        private static int Authorize(IntPtr context, int action, IntPtr arg1, IntPtr arg2, IntPtr db, IntPtr trigger)
            => action == 22 || action == 32 ? 1 : 0; // SQLITE_TRANSACTION / SQLITE_SAVEPOINT -> SQLITE_DENY

        internal SqliteStatement(SqliteDatabase database, string sql, object[] parameters, bool transactionControl = false)
        {
            this.database = database;
            this.sql = sql;
            byte[] bytes = SqliteNative.Utf8(sql);
            var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                if (!transactionControl) database.Check(SqliteNative.sqlite3_set_authorizer(database.Handle, authorizer, IntPtr.Zero));
                var start = pinned.AddrOfPinnedObject();
                int code = SqliteNative.sqlite3_prepare_v2(database.Handle, start, bytes.Length, out var pointer, out var tail);
                Handle = new SafeSqliteStatementHandle(pointer);
                database.Check(code, sql);
                if (Handle.IsInvalid) throw new ArgumentException("SQL must contain one statement.", nameof(sql));
                // Parse the tail too: allow comments, but never silently ignore a second statement.
                int offset = checked((int)(tail.ToInt64() - start.ToInt64()));
                while (offset < bytes.Length - 1)
                {
                    code = SqliteNative.sqlite3_prepare_v2(database.Handle, IntPtr.Add(start, offset), bytes.Length - offset, out pointer, out tail);
                    using (var extra = new SafeSqliteStatementHandle(pointer))
                    {
                        database.Check(code, sql);
                        if (!extra.IsInvalid) throw new ArgumentException("Only one SQL statement is allowed per call.", nameof(sql));
                    }
                    int next = checked((int)(tail.ToInt64() - start.ToInt64()));
                    if (next <= offset) break;
                    offset = next;
                }
                if (SqliteNative.sqlite3_bind_parameter_count(Handle) != parameters.Length)
                    throw new ArgumentException("Parameter count does not match SQL.", nameof(parameters));
                for (int i = 0; i < parameters.Length; i++) Bind(i + 1, parameters[i]);
            }
            catch { Handle?.Dispose(); throw; }
            finally
            {
                SqliteNative.sqlite3_set_authorizer(database.Handle, null, IntPtr.Zero);
                pinned.Free();
            }
        }
        private void Bind(int index, object value)
        {
            int code;
            switch (value)
            {
                case null: code = SqliteNative.sqlite3_bind_null(Handle, index); break;
                case bool b: code = SqliteNative.sqlite3_bind_int64(Handle, index, b ? 1 : 0); break;
                case int n: code = SqliteNative.sqlite3_bind_int64(Handle, index, n); break;
                case long n: code = SqliteNative.sqlite3_bind_int64(Handle, index, n); break;
                case float n: code = SqliteNative.sqlite3_bind_double(Handle, index, n); break;
                case double n: code = SqliteNative.sqlite3_bind_double(Handle, index, n); break;
                case string text:
                    var bytes = Encoding.UTF8.GetBytes(text + "\0");
                    code = SqliteNative.sqlite3_bind_text(Handle, index, bytes, bytes.Length - 1, SqliteNative.Transient); break;
                case byte[] blob:
                    code = SqliteNative.sqlite3_bind_blob(Handle, index, blob.Length == 0 ? new byte[1] : blob, blob.Length, SqliteNative.Transient); break;
                default: throw new ArgumentException("Unsupported parameter type: " + value.GetType().FullName);
            }
            database.Check(code, sql);
        }
        internal bool Step()
        {
            int code = SqliteNative.sqlite3_step(Handle);
            if (code == 100) return true;
            if (code == 101) return false;
            database.Check(code, sql);
            return false;
        }
        internal SqliteRow Read()
        {
            // sqlite3_step may automatically reprepare after a schema change on another connection.
            // Read column metadata after the first successful step, not at prepare time.
            if (names == null)
            {
                names = new string[SqliteNative.sqlite3_column_count(Handle)];
                for (int i = 0; i < names.Length; i++) names[i] = SqliteNative.Text(SqliteNative.sqlite3_column_name(Handle, i));
            }
            var values = new object[names.Length];
            for (int i = 0; i < values.Length; i++)
            {
                switch (SqliteNative.sqlite3_column_type(Handle, i))
                {
                    case 1: values[i] = SqliteNative.sqlite3_column_int64(Handle, i); break;
                    case 2: values[i] = SqliteNative.sqlite3_column_double(Handle, i); break;
                    case 3:
                        var text = SqliteNative.sqlite3_column_text(Handle, i);
                        values[i] = Encoding.UTF8.GetString(SqliteNative.Bytes(text, SqliteNative.sqlite3_column_bytes(Handle, i))); break;
                    case 4:
                        var blob = SqliteNative.sqlite3_column_blob(Handle, i);
                        values[i] = SqliteNative.Bytes(blob, SqliteNative.sqlite3_column_bytes(Handle, i)); break;
                }
            }
            return new SqliteRow(names, values);
        }
        public void Dispose() => Handle.Dispose();
    }
}
