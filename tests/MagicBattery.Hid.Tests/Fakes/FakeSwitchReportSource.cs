namespace MagicBattery.Hid.Tests.Fakes;

/// <summary>手写的 <see cref="ISwitchReportSource"/> 替身:返回预置报文或抛异常。</summary>
internal sealed class FakeSwitchReportSource : ISwitchReportSource
{
    private readonly byte[]? _response;
    private readonly Exception? _throw;

    public DeviceConnection Connection { get; } = DeviceConnection.Bluetooth;
    public DeviceKind Kind { get; } = DeviceKind.Gamepad;
    public string DeviceKey { get; } = "fake-pad";
    public bool Disposed { get; private set; }

    private FakeSwitchReportSource(byte[]? response, Exception? toThrow)
    {
        _response = response;
        _throw = toThrow;
    }

    public static FakeSwitchReportSource Returning(byte[] response) => new(response, null);

    public static FakeSwitchReportSource Throwing(Exception toThrow) => new(null, toThrow);

    public byte[] ReadStandardReport()
    {
        if (_throw is not null)
        {
            throw _throw;
        }

        return _response!;
    }

    public void Dispose() => Disposed = true;
}
