using System;
using System.Threading.Tasks;
using Survos.Sqlite.Tests;
internal static class Program
{
    private static async Task<int> Main()
    {
        try { await SqliteChecks.Run(Console.WriteLine, Environment.GetEnvironmentVariable("FOLIO_TEST_PATH")); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
