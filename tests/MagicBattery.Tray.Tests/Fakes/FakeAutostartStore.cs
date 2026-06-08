using MagicBattery.Tray.Core;

namespace MagicBattery.Tray.Tests.Fakes;

/// <summary>内存版 <see cref="IAutostartStore"/>,替代注册表用于单测。</summary>
internal sealed class FakeAutostartStore : IAutostartStore
{
    private readonly Dictionary<string, string> _values = new();

    public string? Get(string name) => _values.TryGetValue(name, out string? v) ? v : null;

    public void Set(string name, string value) => _values[name] = value;

    public void Remove(string name) => _values.Remove(name);

    public bool Contains(string name) => _values.ContainsKey(name);
}
