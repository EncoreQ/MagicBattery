using MagicBattery.Hid.Usb;

namespace MagicBattery.Hid.Tests.Fakes;

/// <summary>
/// 手写的 <see cref="IUsbHidConnection"/> 测试替身(避免引入 mock 库)。
/// 要么返回预置字节,要么抛指定异常(模拟设备未就绪)。
/// </summary>
internal sealed class FakeUsbHidConnection : IUsbHidConnection
{
    private readonly byte[]? _response;
    private readonly Exception? _throw;

    public int GetFeatureCallCount { get; private set; }
    public bool Disposed { get; private set; }

    private FakeUsbHidConnection(byte[]? response, Exception? toThrow)
    {
        _response = response;
        _throw = toThrow;
    }

    public static FakeUsbHidConnection Returning(byte[] response) => new(response, null);

    public static FakeUsbHidConnection Throwing(Exception toThrow) => new(null, toThrow);

    public byte[] GetFeatureReport(byte reportId, int reportLength)
    {
        GetFeatureCallCount++;
        if (_throw is not null)
        {
            throw _throw;
        }

        return _response!;
    }

    public void Dispose() => Disposed = true;
}
