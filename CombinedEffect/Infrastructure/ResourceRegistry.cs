using System.Collections.Concurrent;

namespace CombinedEffect.Infrastructure;

internal sealed class ResourceRegistry : IDisposable
{
    private static readonly Lazy<ResourceRegistry> _instance = new(() => new ResourceRegistry());
    public static ResourceRegistry Instance => _instance.Value;

    private readonly ConcurrentDictionary<Guid, WeakReference<IDisposable>> _resources = new();
    private bool _disposed;

    private ResourceRegistry() { }

    public void Register(IDisposable resource)
    {
        if (_disposed) return;
        _resources[Guid.NewGuid()] = new WeakReference<IDisposable>(resource);
    }

    public void Unregister(IDisposable resource)
    {
        foreach (var (key, weakRef) in _resources)
        {
            if (weakRef.TryGetTarget(out var target) && ReferenceEquals(target, resource))
            {
                _resources.TryRemove(key, out _);
                return;
            }
        }
    }

    public void Purge()
    {
        foreach (var (key, weakRef) in _resources)
        {
            if (!weakRef.TryGetTarget(out _))
                _resources.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var (_, weakRef) in _resources)
        {
            if (weakRef.TryGetTarget(out var resource))
            {
                try { resource.Dispose(); }
                catch { }
            }
        }
        _resources.Clear();
    }
}
