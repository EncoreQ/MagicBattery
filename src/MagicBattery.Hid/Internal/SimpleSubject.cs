namespace MagicBattery.Hid.Internal;

/// <summary>
/// 极简的 <see cref="IObservable{T}"/> 多播源。手写以避免引入选型表外的 System.Reactive。
/// 只实现热推送 + 退订,够 Phase 1/2 用;不做重放、调度、错误传播等高级语义。
/// 线程安全:订阅集合的增删与推送加锁;订阅者回调在调用 OnNext 的线程上同步执行。
/// </summary>
internal sealed class SimpleSubject<T> : IObservable<T>
{
    private readonly object _gate = new();
    private readonly List<IObserver<T>> _observers = new();

    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        lock (_gate)
        {
            _observers.Add(observer);
        }

        return new Subscription(this, observer);
    }

    /// <summary>向所有当前订阅者推送一个值。</summary>
    public void OnNext(T value)
    {
        IObserver<T>[] snapshot;
        lock (_gate)
        {
            snapshot = _observers.ToArray();
        }

        foreach (var observer in snapshot)
        {
            observer.OnNext(value);
        }
    }

    private void Unsubscribe(IObserver<T> observer)
    {
        lock (_gate)
        {
            _observers.Remove(observer);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private SimpleSubject<T>? _subject;
        private readonly IObserver<T> _observer;

        public Subscription(SimpleSubject<T> subject, IObserver<T> observer)
        {
            _subject = subject;
            _observer = observer;
        }

        public void Dispose()
        {
            _subject?.Unsubscribe(_observer);
            _subject = null;
        }
    }
}
