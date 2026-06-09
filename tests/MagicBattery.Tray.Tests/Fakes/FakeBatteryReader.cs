using MagicBattery.Hid;

namespace MagicBattery.Tray.Tests.Fakes;

/// <summary>
/// 手写的 <see cref="IBatteryReader"/> 测试替身。按脚本逐次返回读取结果(或抛异常);
/// 脚本用尽后重复最后一项。多设备测试用 <see cref="Kind"/>/<see cref="DeviceKey"/> 区分设备。
/// </summary>
internal sealed class FakeBatteryReader : IBatteryReader
{
    private readonly Queue<object> _script; // 每项为 BatteryReadResult 或 Exception

    public DeviceConnection Connection { get; }
    public DeviceKind Kind { get; init; } = DeviceKind.Trackpad;
    public string DeviceKey { get; init; } = "fake";
    public int ReadCount { get; private set; }
    public bool Disposed { get; private set; }

    /// <param name="script">每项 BatteryReadResult 或 Exception(模拟 ReadAsync 抛错)。</param>
    public FakeBatteryReader(DeviceConnection connection, params object[] script)
    {
        Connection = connection;
        _script = new Queue<object>(script.Length == 0 ? new object[] { BatteryReadResult.Unavailable } : script);
    }

    public IObservable<BatteryStatus> Changes { get; } = NullObservable<BatteryStatus>.Instance;

    public Task<BatteryReadResult> ReadAsync(CancellationToken ct)
    {
        ReadCount++;
        object item = _script.Count > 1 ? _script.Dequeue() : _script.Peek();
        return item is Exception ex
            ? Task.FromException<BatteryReadResult>(ex)
            : Task.FromResult((BatteryReadResult)item);
    }

    public void Dispose() => Disposed = true;
}

/// <summary>不推送任何东西的空 observable(替代 Hid 内部的 SimpleSubject)。</summary>
internal sealed class NullObservable<T> : IObservable<T>
{
    public static readonly NullObservable<T> Instance = new();

    public IDisposable Subscribe(IObserver<T> observer) => NullDisposable.Instance;

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}
