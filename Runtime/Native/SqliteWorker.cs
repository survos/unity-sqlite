using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Survos.Sqlite.Native
{
    internal sealed class SqliteWorker
    {
        private readonly BlockingCollection<Action> queue = new BlockingCollection<Action>();
        private readonly TaskCompletionSource<bool> stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal SqliteWorker()
        {
            new Thread(() =>
            {
                try { foreach (var action in queue.GetConsumingEnumerable()) action(); }
                finally { queue.Dispose(); stopped.TrySetResult(true); }
            }) { IsBackground = true, Name = "Survos SQLite" }.Start();
        }
        internal Task<T> Run<T>(Func<T> action)
        {
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Add(() =>
            {
                try { completion.SetResult(action()); }
                catch (OperationCanceledException) { completion.SetCanceled(); }
                catch (Exception e) { completion.SetException(e); }
            });
            return completion.Task;
        }
        internal Task Stop() { queue.CompleteAdding(); return stopped.Task; }
    }
}
