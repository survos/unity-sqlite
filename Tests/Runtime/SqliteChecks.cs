using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Survos.Sqlite.Tests
{
    // Shared by the .NET harness, Unity tests, and the actual IL2CPP player.
    public static class SqliteChecks
    {
        private static void Equal<T>(T expected, T actual)
        { if (!Equals(expected, actual)) throw new Exception("Expected " + expected + "; got " + actual); }
        private static async Task Throws<T>(Func<Task> action) where T : Exception
        {
            try { await action(); }
            catch (T) { return; }
            throw new Exception("Expected " + typeof(T).Name);
        }
        public static async Task Run(Action<string> log, string folioPath = null)
        {
            var watch = Stopwatch.StartNew();
            var path = Path.Combine(Path.GetTempPath(), "survos-" + Guid.NewGuid().ToString("N") + ".folio");
            try
            {
                await using (var db = await SqliteDatabase.OpenAsync(path))
                {
                    log("Open: " + watch.ElapsedMilliseconds + " ms; SQLite " + db.SqliteVersion);
                    Equal("3.53.4", db.SqliteVersion);
                    await db.ExecuteAsync("CREATE TABLE item(id INTEGER PRIMARY KEY, title TEXT, value REAL, data BLOB, empty BLOB, optional TEXT)");
                    var thumbhash = new byte[] { 0xdc, 0xe7, 0x11, 0x25, 0x80, 0x78, 0x77, 0x78, 0x7f, 0x88, 0x87, 0x87, 0x78, 0x48, 0x77, 0x78, 0x88, 0x70, 0xfa, 0x3d, 0xc0 };
                    await db.ExecuteAsync("INSERT INTO item VALUES(?, ?, ?, ?, ?, ?)", long.MaxValue, "Frans Blom — 博物館 🗞\0tail", 1.25, thumbhash, Array.Empty<byte>(), null);
                    SqliteRow saved = null;
                    await foreach (var row in db.QueryAsync("SELECT * FROM item"))
                    {
                        Equal(long.MaxValue, row.GetInt64("id"));
                        Equal("Frans Blom — 博物館 🗞\0tail", row.GetString("title"));
                        Equal(1.25, row.GetDouble("value"));
                        Equal(true, thumbhash.SequenceEqual(row.GetBlob("data")));
                        Equal(0, row.GetBlob("empty").Length);
                        Equal(true, row.IsNull("optional"));
                        saved = row;
                    }
                    Equal(long.MaxValue, saved.GetInt64(0));
                    Equal(1L, await db.ScalarAsync<long>("SELECT ?", true));
                    Equal(2L, await db.ScalarAsync<long>("SELECT ?", 2));
                    Equal(2.5, await db.ScalarAsync<double>("SELECT ?", 2.5f));
                    Equal("", await db.ScalarAsync<string>("SELECT ?", ""));
                    Equal<string>(null, await db.ScalarAsync<string>("SELECT NULL"));
                    Equal<long?>(null, await db.ScalarAsync<long?>("SELECT NULL"));
                    await Throws<InvalidCastException>(() => db.ScalarAsync<long>("SELECT NULL"));
                    await Throws<InvalidOperationException>(() => db.ScalarAsync<long>("SELECT 1 WHERE 0"));
                    await Throws<ArgumentException>(() => db.ExecuteAsync("SELECT ?"));
                    await Throws<ArgumentException>(() => db.ExecuteAsync("SELECT ?", DateTime.Now));
                    await Throws<ArgumentException>(() => db.ExecuteAsync("SELECT 1; SELECT 2"));
                    await Throws<ArgumentException>(() => db.ExecuteAsync("-- only a comment"));
                    Equal(1L, await db.ScalarAsync<long>("SELECT 1; -- valid trailing comment"));
                    await Throws<SqliteException>(() => db.ExecuteAsync("SELECT missing FROM item"));
                    bool constraintThrown = false;
                    try { await db.ExecuteAsync("INSERT INTO item(id) VALUES(?)", long.MaxValue); }
                    catch (SqliteException e) { constraintThrown = true; Equal(19, e.ResultCode); Equal(1555, e.ExtendedResultCode); }
                    Equal(true, constraintThrown);
                    await db.ExecuteAsync("CREATE TABLE tx(value INTEGER)");
                    await db.TransactionAsync(async tx =>
                    {
                        await tx.ExecuteAsync("INSERT INTO tx VALUES(?)", 1);
                        Equal(1L, await tx.ScalarAsync<long>("SELECT COUNT(*) FROM tx"));
                    });
                    await Throws<InvalidOperationException>(() => db.TransactionAsync(async tx =>
                    {
                        await tx.ExecuteAsync("INSERT INTO tx VALUES(2)");
                        throw new InvalidOperationException("rollback");
                    }));
                    Equal(1L, await db.ScalarAsync<long>("SELECT COUNT(*) FROM tx"));
                    await Throws<InvalidOperationException>(() => db.TransactionAsync(async tx =>
                    {
                        await Throws<SqliteException>(() => tx.ExecuteAsync("INSERT OR ROLLBACK INTO item(id) VALUES(?)", long.MaxValue));
                        // After SQLite automatically rolls back, subsequent work must never autocommit.
                        await tx.ExecuteAsync("INSERT INTO tx VALUES(100)");
                    }));
                    Equal(1L, await db.ScalarAsync<long>("SELECT COUNT(*) FROM tx"));
                    // Transaction control belongs to the wrapper, including when SQL has leading comments.
                    await Throws<SqliteException>(() => db.ExecuteAsync("/* guard */ BEGIN"));
                    await Throws<SqliteException>(() => db.TransactionAsync(tx => tx.ExecuteAsync("COMMIT")));
                    await Throws<InvalidOperationException>(() => db.TransactionAsync(tx => db.ExecuteAsync("SELECT 1")));
                    // Another operation must wait until the complete callback commits.
                    var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var transactionTask = db.TransactionAsync(async tx =>
                    {
                        await tx.ExecuteAsync("INSERT INTO tx VALUES(7)");
                        entered.SetResult(true);
                        await release.Task;
                    });
                    await entered.Task;
                    var waiting = db.ScalarAsync<long>("SELECT COUNT(*) FROM tx");
                    Equal(false, waiting.IsCompleted);
                    using (var cancel = new CancellationTokenSource())
                    {
                        var queued = db.ExecuteAsync("INSERT INTO tx VALUES(99)", cancel.Token);
                        cancel.Cancel();
                        await Throws<OperationCanceledException>(() => queued);
                    }
                    release.SetResult(true);
                    await transactionTask;
                    Equal(2L, await waiting);
                    await db.ExecuteAsync("DELETE FROM tx WHERE value=7");
                    using (var cancel = new CancellationTokenSource())
                    {
                        await Throws<OperationCanceledException>(() => db.TransactionAsync(async tx =>
                        {
                            await tx.ExecuteAsync("INSERT INTO tx VALUES(8)");
                            cancel.Cancel();
                        }, cancel.Token));
                    }
                    Equal(1L, await db.ScalarAsync<long>("SELECT COUNT(*) FROM tx"));
                    SqliteTransaction escaped = null;
                    await db.TransactionAsync(tx => { escaped = tx; return Task.CompletedTask; });
                    await Throws<InvalidOperationException>(() => escaped.ExecuteAsync("SELECT 1"));
                    using (var cancel = new CancellationTokenSource())
                    {
                        cancel.Cancel();
                        await Throws<OperationCanceledException>(() => db.ExecuteAsync("INSERT INTO tx VALUES(3)", cancel.Token));
                    }
                    using (var cancel = new CancellationTokenSource(50))
                    {
                        await Throws<OperationCanceledException>(() => db.ScalarAsync<long>(
                            "WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<1000000000) SELECT sum(x) FROM n", cancel.Token));
                    }
                    Equal(1L, await db.ScalarAsync<long>("SELECT COUNT(*) FROM tx"));
                    // Early break must finalize and release the connection before its next operation.
                    await foreach (var row in db.QueryAsync("SELECT * FROM item")) break;
                    Equal(1L, await db.ScalarAsync<long>("SELECT COUNT(*) FROM item"));
                    using (var cancel = new CancellationTokenSource())
                    {
                        await Throws<OperationCanceledException>(async () =>
                        {
                            await foreach (var row in db.QueryAsync("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<1000) SELECT x FROM n").WithCancellation(cancel.Token))
                                cancel.Cancel();
                        });
                    }
                    Equal(1L, await db.ScalarAsync<long>("SELECT 1"));
                    await db.ExecuteAsync("CREATE VIRTUAL TABLE article_fts USING fts5(title, body)");
                    await db.ExecuteAsync("INSERT INTO article_fts VALUES(?, ?)", "Newspaper", "A railroad accident in 1950");
                    Equal(1L, await db.ScalarAsync<long>("SELECT rowid FROM article_fts WHERE article_fts MATCH ?", "railroad accident"));
                    Equal("Frans Blom", await db.ScalarAsync<string>("SELECT json_extract(?, '$.creator')", "{\"creator\":\"Frans Blom\"}"));
                    await db.ExecuteAsync("CREATE VIRTUAL TABLE region USING rtree(id, x1, x2, y1, y2)");
                    await db.ExecuteAsync("INSERT INTO region VALUES(1, 10, 20, 30, 40)");
                    Equal(1L, await db.ScalarAsync<long>("SELECT id FROM region WHERE x1 >= 5 AND x2 <= 25"));
                    var options = await db.GetCompileOptionsAsync();
                    foreach (var required in new[] { "ENABLE_FTS5", "ENABLE_RTREE", "THREADSAFE=1", "OMIT_LOAD_EXTENSION", "DQS=0" })
                        Equal(true, options.Contains(required));
                    log("Compile options: " + string.Join(", ", options));
                    await Throws<SqliteException>(() => db.ExecuteAsync("SELECT load_extension('anything')"));
                    for (int size = 1; size <= 10000; size *= 100)
                    {
                        watch.Restart();
                        int count = 0;
                        long before = GC.GetTotalMemory(false);
                        await foreach (var row in db.QueryAsync("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x < ?) SELECT x FROM n", size)) count++;
                        Equal(size, count);
                        log(size + " rows: " + watch.ElapsedMilliseconds + " ms; heap delta (approx): " + (GC.GetTotalMemory(false) - before));
                    }
                    watch.Restart();
                    await db.ScalarAsync<long>("SELECT count(*) FROM article_fts WHERE article_fts MATCH 'railroad'");
                    log("FTS: " + watch.Elapsed.TotalMilliseconds + " ms");
                    watch.Restart();
                    await db.ScalarAsync<byte[]>("SELECT data FROM item");
                    log("BLOB: " + watch.Elapsed.TotalMilliseconds + " ms");
                    await Task.WhenAll(Enumerable.Range(0, 20).Select(i => db.ScalarAsync<long>("SELECT ?", i)));
                }
                await using (var read = await SqliteDatabase.OpenAsync(path, SqliteOpenMode.ReadOnly))
                {
                    Equal(1L, await read.ScalarAsync<long>("SELECT COUNT(*) FROM item"));
                    await Throws<SqliteException>(() => read.ExecuteAsync("INSERT INTO tx VALUES(9)"));
                }
                await using (var rw = await SqliteDatabase.OpenAsync(path, SqliteOpenMode.ReadWrite))
                    Equal(1L, await rw.ScalarAsync<long>("SELECT COUNT(*) FROM item"));
                await Throws<SqliteException>(() => SqliteDatabase.OpenAsync(path + ".missing", SqliteOpenMode.ReadWrite));
                await File.WriteAllTextAsync(path + ".invalid", new string('x', 4096));
                await Throws<SqliteException>(() => SqliteDatabase.OpenAsync(path + ".invalid"));
                await Task.WhenAll(Enumerable.Range(0, 4).Select(async i =>
                {
                    await using var db = await SqliteDatabase.OpenAsync(path, SqliteOpenMode.ReadOnly);
                    Equal(1L, await db.ScalarAsync<long>("SELECT COUNT(*) FROM item"));
                }));
                for (int i = 0; i < 20; i++)
                {
                    var db = await SqliteDatabase.OpenAsync(":memory:");
                    await db.DisposeAsync();
                    await db.DisposeAsync();
                    await Throws<ObjectDisposedException>(() => db.ExecuteAsync("SELECT 1"));
                }
                // Disposal cancels native work and waits for its cleanup.
                var closing = await SqliteDatabase.OpenAsync(":memory:");
                var running = closing.ScalarAsync<long>("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<1000000000) SELECT sum(x) FROM n");
                await Task.Delay(20);
                await closing.DisposeAsync();
                await Throws<OperationCanceledException>(() => running);
                if (!string.IsNullOrEmpty(folioPath))
                {
                    await using var real = await SqliteDatabase.OpenAsync(folioPath, SqliteOpenMode.ReadOnly);
                    Equal("ok", await real.ScalarAsync<string>("PRAGMA quick_check"));
                    string table = await real.ScalarAsync<string>("SELECT name FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name LIMIT 1");
                    int count = 0;
                    await foreach (var row in real.QueryAsync("SELECT * FROM \"" + table.Replace("\"", "\"\"") + "\" LIMIT 10")) count++;
                    log("Real Folio: " + table + ", " + count + " records");
                }
                else log("SKIP real Folio: set FOLIO_TEST_PATH to an existing archive.");
                log("PASS: SQLite integration checks");
            }
            finally
            {
                foreach (var suffix in new[] { "", "-journal", "-wal", "-shm", ".invalid" })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }
        }
    }
}
