using System;
using System.Threading;
using System.Threading.Tasks;

namespace Survos.Sqlite
{
    /// <summary>Scoped transaction access. Await all work inside the transaction callback.</summary>
    public sealed class SqliteTransaction
    {
        private readonly SqliteDatabase database;
        private readonly CancellationToken token;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private int finished;
        internal SqliteTransaction(SqliteDatabase database, CancellationToken token) { this.database = database; this.token = token; }
        private async Task<T> Run<T>(Func<Task<T>> action)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref finished) != 0) throw new InvalidOperationException("Transaction scope has ended.");
                return await action().ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
        public Task<int> ExecuteAsync(string sql, params object[] parameters)
            => Run(() => database.ExecuteCore(sql, parameters, token, requireTransaction: true));
        public Task<T> ScalarAsync<T>(string sql, params object[] parameters)
            => Run(() => database.ScalarCore<T>(sql, parameters, token, requireTransaction: true));
        internal async Task Finish()
        {
            Interlocked.Exchange(ref finished, 1);
            await gate.WaitAsync().ConfigureAwait(false);
            gate.Release();
        }
    }
}
