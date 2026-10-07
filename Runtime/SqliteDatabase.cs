using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Survos.Sqlite.Native;

namespace Survos.Sqlite
{
    /// <summary>One native connection, one worker. Dispose query enumerators before reusing the connection.</summary>
    public sealed class SqliteDatabase : IDisposable, IAsyncDisposable
    {
        internal SafeSqliteDatabaseHandle Handle;
        private readonly SqliteWorker worker = new SqliteWorker();
        private readonly SemaphoreSlim lease = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly object disposeLock = new object();
        private Task disposeTask;
        private int disposed;
        private CancellationToken activeToken; // Only accessed on the worker.
        private static readonly SqliteNative.ProgressCallback progress = Progress;
        private static readonly AsyncLocal<SqliteDatabase> transactionScope = new AsyncLocal<SqliteDatabase>();
        private SqliteDatabase() { }

        public static string LibraryVersion => SqliteNative.Text(SqliteNative.sqlite3_libversion());
        public string SqliteVersion => LibraryVersion;

        public static Task<SqliteDatabase> OpenAsync(string path, CancellationToken cancellationToken)
            => OpenAsync(path, SqliteOpenMode.ReadWriteCreate, cancellationToken);

        public static async Task<SqliteDatabase> OpenAsync(string path, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A path is required.", nameof(path));
            byte[] encoded = SqliteNative.Utf8(path);
            int flags;
            switch (mode)
            {
                case SqliteOpenMode.ReadOnly: flags = 1; break;
                case SqliteOpenMode.ReadWrite: flags = 2; break;
                case SqliteOpenMode.ReadWriteCreate: flags = 6; break;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
            cancellationToken.ThrowIfCancellationRequested();
            var database = new SqliteDatabase();
            try
            {
                await database.worker.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int code = SqliteNative.sqlite3_open_v2(encoded, out var pointer, flags | 0x10000, IntPtr.Zero);
                    database.Handle = new SafeSqliteDatabaseHandle(pointer);
                    database.Check(code);
                    database.Check(SqliteNative.sqlite3_busy_timeout(database.Handle, 250));
                    return 0;
                }).ConfigureAwait(false);
                // Force a schema read: open_v2 alone accepts non-SQLite files until first access.
                await database.ScalarAsync<long>("PRAGMA schema_version", cancellationToken).ConfigureAwait(false);
                return database;
            }
            catch { await database.DisposeAsync().ConfigureAwait(false); throw; }
        }

        internal void Check(int code, string sql = null)
        {
            if (code == 0) return;
            if ((code & 255) == 9 && activeToken.IsCancellationRequested)
                throw new OperationCanceledException(activeToken);
            bool valid = Handle != null && !Handle.IsInvalid;
            throw new SqliteException(code, valid ? SqliteNative.sqlite3_extended_errcode(Handle) : code,
                valid ? SqliteNative.Text(SqliteNative.sqlite3_errmsg(Handle)) : "Unable to allocate SQLite connection.", sql);
        }

        // Static reverse P/Invoke is rooted for IL2CPP; no Unity APIs run on the worker.
        [MonoPInvokeCallback(typeof(SqliteNative.ProgressCallback))]
        private static int Progress(IntPtr context)
            => ((CancellationToken)GCHandle.FromIntPtr(context).Target).IsCancellationRequested ? 1 : 0;

        internal Task<T> Work<T>(Func<T> action, CancellationToken token)
        {
            return worker.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var context = GCHandle.Alloc(token);
                activeToken = token;
                try
                {
                    SqliteNative.sqlite3_progress_handler(Handle, 1000, progress, GCHandle.ToIntPtr(context));
                    return action();
                }
                finally
                {
                    SqliteNative.sqlite3_progress_handler(Handle, 0, null, IntPtr.Zero);
                    context.Free();
                    activeToken = default;
                }
            });
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(SqliteDatabase));
            if (transactionScope.Value == this) throw new InvalidOperationException("Use the tx argument inside a transaction callback.");
        }

        public Task<int> ExecuteAsync(string sql, params object[] parameters) => ExecuteAsync(sql, default, parameters);
        public async Task<int> ExecuteAsync(string sql, CancellationToken cancellationToken, params object[] parameters)
        {
            ThrowIfDisposed();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            await lease.WaitAsync(linked.Token).ConfigureAwait(false);
            try { ThrowIfDisposed(); return await ExecuteCore(sql, parameters, linked.Token).ConfigureAwait(false); }
            finally { lease.Release(); }
        }
        internal Task<int> ExecuteCore(string sql, object[] parameters, CancellationToken token, bool transactionControl = false, bool requireTransaction = false)
            => Work(() =>
            {
                if (requireTransaction) CheckTransaction();
                using var statement = new SqliteStatement(this, sql, parameters ?? Array.Empty<object>(), transactionControl);
                while (statement.Step()) token.ThrowIfCancellationRequested();
                return SqliteNative.sqlite3_changes(Handle);
            }, token);

        public Task<T> ScalarAsync<T>(string sql, params object[] parameters) => ScalarAsync<T>(sql, default, parameters);
        public async Task<T> ScalarAsync<T>(string sql, CancellationToken cancellationToken, params object[] parameters)
        {
            ThrowIfDisposed();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            await lease.WaitAsync(linked.Token).ConfigureAwait(false);
            try { ThrowIfDisposed(); return await ScalarCore<T>(sql, parameters, linked.Token).ConfigureAwait(false); }
            finally { lease.Release(); }
        }
        internal Task<T> ScalarCore<T>(string sql, object[] parameters, CancellationToken token, bool requireTransaction = false)
            => Work(() =>
            {
                if (requireTransaction) CheckTransaction();
                using var statement = new SqliteStatement(this, sql, parameters ?? Array.Empty<object>());
                if (!statement.Step()) throw new InvalidOperationException("Scalar query returned no rows.");
                var value = statement.Read()[0];
                if (value is T result) return result;
                if (value == null)
                {
                    if (default(T) == null) return default;
                    throw new InvalidCastException("SQL NULL cannot be read into a non-nullable value type.");
                }
                return (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), CultureInfo.InvariantCulture);
            }, token);

        private void CheckTransaction()
        {
            if (SqliteNative.sqlite3_get_autocommit(Handle) != 0)
                throw new InvalidOperationException("SQLite has already rolled back this transaction; no further operations are allowed in its scope.");
        }

        public IAsyncEnumerable<SqliteRow> QueryAsync(string sql, params object[] parameters) => QueryAsync(sql, default, parameters);
        public async IAsyncEnumerable<SqliteRow> QueryAsync(string sql,
            [EnumeratorCancellation] CancellationToken cancellationToken, params object[] parameters)
        {
            ThrowIfDisposed();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            await lease.WaitAsync(linked.Token).ConfigureAwait(false);
            SqliteStatement statement = null;
            try
            {
                ThrowIfDisposed();
                statement = await Work(() => new SqliteStatement(this, sql, parameters ?? Array.Empty<object>()), linked.Token).ConfigureAwait(false);
                bool done = false;
                while (!done)
                {
                    var batch = await Work(() =>
                    {
                        var rows = new List<SqliteRow>(64);
                        for (int i = 0; i < 64; i++)
                        {
                            linked.Token.ThrowIfCancellationRequested();
                            if (!statement.Step()) { done = true; break; }
                            rows.Add(statement.Read());
                        }
                        return rows;
                    }, linked.Token).ConfigureAwait(false);
                    foreach (var row in batch)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        yield return row;
                    }
                }
            }
            finally
            {
                // Cleanup ignores cancellation, and always precedes releasing the connection lease.
                try { if (statement != null) await worker.Run(() => { statement.Dispose(); return 0; }).ConfigureAwait(false); }
                finally { lease.Release(); }
            }
        }

        public async Task<string[]> GetCompileOptionsAsync(CancellationToken cancellationToken = default)
        {
            var options = new List<string>();
            await foreach (var row in QueryAsync("PRAGMA compile_options", cancellationToken).ConfigureAwait(false)) options.Add(row.GetString(0));
            return options.ToArray();
        }

        public async Task TransactionAsync(Func<SqliteTransaction, Task> action, CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            ThrowIfDisposed();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            await lease.WaitAsync(linked.Token).ConfigureAwait(false);
            var transaction = new SqliteTransaction(this, linked.Token);
            bool begun = false;
            try
            {
                ThrowIfDisposed();
                await ExecuteCore("BEGIN IMMEDIATE", Array.Empty<object>(), linked.Token, true).ConfigureAwait(false);
                begun = true;
                var previousScope = transactionScope.Value;
                transactionScope.Value = this;
                try { await action(transaction).ConfigureAwait(false); }
                finally { transactionScope.Value = previousScope; }
                await transaction.Finish().ConfigureAwait(false);
                await ExecuteCore("COMMIT", Array.Empty<object>(), linked.Token, true, true).ConfigureAwait(false);
                begun = false;
            }
            catch (Exception original)
            {
                await transaction.Finish().ConfigureAwait(false);
                if (begun)
                {
                    try
                    {
                        // SQLITE_INTERRUPT and some storage errors may already have rolled back SQLite's transaction.
                        if (await worker.Run(() => SqliteNative.sqlite3_get_autocommit(Handle) == 0).ConfigureAwait(false))
                            await ExecuteCore("ROLLBACK", Array.Empty<object>(), CancellationToken.None, true).ConfigureAwait(false);
                    }
                    catch (Exception rollback) { throw new AggregateException("Transaction and rollback both failed.", original, rollback); }
                }
                throw;
            }
            finally { lease.Release(); }
        }

        public ValueTask DisposeAsync()
        {
            if (transactionScope.Value == this) throw new InvalidOperationException("Dispose the database after the transaction callback returns.");
            lock (disposeLock)
            {
                if (disposeTask == null)
                {
                    Interlocked.Exchange(ref disposed, 1);
                    lifetime.Cancel();
                    disposeTask = Close();
                }
                return new ValueTask(disposeTask);
            }
        }
        private async Task Close()
        {
            await lease.WaitAsync().ConfigureAwait(false);
            try
            {
                await worker.Run(() => { Handle?.Dispose(); return 0; }).ConfigureAwait(false);
                await worker.Stop().ConfigureAwait(false);
            }
            finally { lease.Release(); }
        }
        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
