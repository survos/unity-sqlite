using System;
using System.Threading.Tasks;
using UnityEngine;
using Survos.Sqlite.Tests;

public static class SmokeRunner
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static async void Start()
    {
        try
        {
            await SqliteChecks.Run(Debug.Log, Environment.GetEnvironmentVariable("FOLIO_TEST_PATH"));
            Debug.Log("SURVOS_IL2CPP_SMOKE_PASS");
            Application.Quit(0);
        }
        catch (Exception e) { Debug.LogException(e); Application.Quit(1); }
    }
}
