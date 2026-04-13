using System.Runtime.CompilerServices;

namespace CombinedEffect.Infrastructure;

internal sealed class ResourceRegistry : IDisposable
{
    private static readonly Lazy<ResourceRegistry> _instance = new(() => new ResourceRegistry());
    public static ResourceRegistry Instance => _instance.Value;

    private readonly ConditionalWeakTable<IDisposable, object?> _resources = new();
    private bool _disposed;

    private ResourceRegistry() { }

    public void Register(IDisposable resource)
    {
        if (_disposed) return;
        _resources.AddOrUpdate(resource, null);
    }

    public void Unregister(IDisposable resource)
    {
        _resources.Remove(resource);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var resource in _resources)
        {
            try { resource.Key.Dispose(); }
            catch { }
        }
        _resources.Clear();
    }
}