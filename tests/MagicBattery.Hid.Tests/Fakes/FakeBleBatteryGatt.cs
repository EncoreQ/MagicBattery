using MagicBattery.Hid.Ble;

namespace MagicBattery.Hid.Tests.Fakes;

/// <summary>
/// 手写的 <see cref="IBleBatteryGatt"/> 测试替身。可设定 Read 返回值,
/// 并能手动 <see cref="PushNotification"/> 模拟 GATT notify。
/// </summary>
internal sealed class FakeBleBatteryGatt : IBleBatteryGatt
{
    private readonly Notifier _notifier = new();

    /// <summary>ReadBatteryLevelAsync 的返回值;null 模拟读不到。</summary>
    public byte? Level { get; set; }

    public bool Disposed { get; private set; }

    public IObservable<byte> LevelNotifications => _notifier;

    public Task<byte?> ReadBatteryLevelAsync(CancellationToken ct) => Task.FromResult(Level);

    /// <summary>模拟设备主动推送一条 notify。</summary>
    public void PushNotification(byte level) => _notifier.Push(level);

    public void Dispose() => Disposed = true;

    private sealed class Notifier : IObservable<byte>
    {
        private readonly List<IObserver<byte>> _observers = new();

        public IDisposable Subscribe(IObserver<byte> observer)
        {
            _observers.Add(observer);
            return new Unsub(_observers, observer);
        }

        public void Push(byte value)
        {
            foreach (IObserver<byte> o in _observers.ToArray())
            {
                o.OnNext(value);
            }
        }

        private sealed class Unsub : IDisposable
        {
            private readonly List<IObserver<byte>> _list;
            private readonly IObserver<byte> _observer;

            public Unsub(List<IObserver<byte>> list, IObserver<byte> observer)
            {
                _list = list;
                _observer = observer;
            }

            public void Dispose() => _list.Remove(_observer);
        }
    }
}
