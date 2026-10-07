using System;
using System.IO;
using System.Threading.Tasks;
using Survos.Sqlite;

public static class BasicQuery
{
    // Call from your application's async entry point. No database manager component is needed.
    public static async Task RunAsync(string writableDirectory, Action<string> log)
    {
        await using var db = await SqliteDatabase.OpenAsync(Path.Combine(writableDirectory, "museum.folio"));
        await db.ExecuteAsync("CREATE TABLE IF NOT EXISTS object(id INTEGER PRIMARY KEY, title TEXT NOT NULL)");
        await db.TransactionAsync(async tx =>
        {
            await tx.ExecuteAsync("INSERT OR IGNORE INTO object VALUES(?, ?)", 1, "Newspaper");
            await tx.ExecuteAsync("INSERT OR IGNORE INTO object VALUES(?, ?)", 2, "Photograph");
            await tx.ExecuteAsync("INSERT OR IGNORE INTO object VALUES(?, ?)", 3, "Map");
        });
        await foreach (var row in db.QueryAsync("SELECT id, title FROM object ORDER BY id LIMIT ?", 10))
            log(row.GetInt64("id") + ": " + row.GetString("title"));
    }
}
