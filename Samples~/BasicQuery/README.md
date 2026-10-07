# Basic query

Import this sample through Package Manager, then call it from your existing application's async entry point:

```csharp
await BasicQuery.RunAsync(Application.persistentDataPath, Debug.Log);
```

Call on Unity's main thread if the supplied logging callback uses Unity APIs. The sample awaits normally so the caller's synchronization context is retained. The SQLite wrapper performs native work on its own worker.
