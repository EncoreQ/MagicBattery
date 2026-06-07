namespace MagicBattery.Hid.Tests;

/// <summary>收集 IObservable 推送值的简单观察者,供断言用。</summary>
internal sealed class CollectingObserver<T> : IObserver<T>
{
    public List<T> Values { get; } = new();

    public void OnNext(T value) => Values.Add(value);

    public void OnCompleted() { }

    public void OnError(Exception error) { }
}
