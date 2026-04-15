using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CombinedEffect.Infrastructure;

internal sealed class AsyncDebouncer : IDisposable
{
    private sealed record WriteRequest(string Key, TimeSpan Delay, Func<Task> Action);

    private readonly Channel<WriteRequest> _channel = Channel.CreateUnbounded<WriteRequest>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new();
    private readonly CancellationTokenSource _workerCts = new();
    private bool _disposed;

    public AsyncDebouncer() => _ = RunWorkerAsync();

    public void DebounceAsync(string key, TimeSpan delay, Func<Task> action)
    {
        if (_disposed) return;
        _channel.Writer.TryWrite(new WriteRequest(key, delay, action));
    }

    private async Task RunWorkerAsync()
    {
        try
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(_workerCts.Token).ConfigureAwait(false))
            {
                if (_pending.TryRemove(request.Key, out var existingCts))
                {
                    existingCts.Cancel();
                    existingCts.Dispose();
                }

                var cts = new CancellationTokenSource();
                _pending[request.Key] = cts;

                _ = ExecuteAfterDelayAsync(request.Key, request.Delay, request.Action, cts);
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private async Task ExecuteAfterDelayAsync(string key, TimeSpan delay, Func<Task> action, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(delay, cts.Token).ConfigureAwait(false);
            if (!cts.Token.IsCancellationRequested)
                await action().ConfigureAwait(false);
        }
        catch { }
        finally
        {
            _pending.TryRemove(new KeyValuePair<string, CancellationTokenSource>(key, cts));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _channel.Writer.Complete();
        _workerCts.Cancel();
        _workerCts.Dispose();
        foreach (var cts in _pending.Values)
        {
            try { cts.Cancel(); cts.Dispose(); } catch { }
        }
        _pending.Clear();
    }
}