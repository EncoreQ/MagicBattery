namespace MagicBattery.Hid.Tests.Fakes;

/// <summary>
/// 手写的 <see cref="IHidInputReportSource"/> 测试替身(避免引入 mock 库)。
/// 要么返回预置字节,要么抛指定异常(模拟设备未就绪)。
/// </summary>
internal sealed class FakeHidInputReportSource : IHidInputReportSource
{
    private readonly byte[]? _response;
    private readonly Exception? _throw;

    public DeviceConnection Connection { get; }
    public int CallCount { get; private set; }
    public bool Disposed { get; private set; }

    private FakeHidInputReportSource(DeviceConnection connection, byte[]? response, Exception? toThrow)
    {
        Connection = connection;
        _response = response;
        _throw = toThrow;
    }

    public static FakeHidInputReportSource Returning(byte[] response, DeviceConnection connection) =>
        new(connection, response, null);

    public static FakeHidInputReportSource Throwing(Exception toThrow,
        DeviceConnection connection = DeviceConnection.Usb) =>
        new(connection, null, toThrow);

    public byte[] GetInputReport(byte reportId, int length)
    {
        CallCount++;
        if (_throw is not null)
        {
            throw _throw;
        }

        return _response!;
    }

    public void Dispose() => Disposed = true;
}
