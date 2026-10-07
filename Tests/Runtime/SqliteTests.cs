using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Survos.Sqlite.Tests
{
    public sealed class SqliteTests
    {
        [UnityTest]
        public IEnumerator NativeIntegration()
        {
            var task = SqliteChecks.Run(UnityEngine.Debug.Log, System.Environment.GetEnvironmentVariable("FOLIO_TEST_PATH"));
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception;
            Assert.That(task.IsCanceled, Is.False);
        }
    }
}
