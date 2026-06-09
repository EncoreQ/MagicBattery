using System.IO;
using System.Runtime.Versioning;
using HidSharp;

namespace MagicBattery.Hid;

/// <summary>
/// <see cref="ISwitchReportSource"/> 的真实实现:用 HidSharp 打开手柄的 HID 输入流,
/// 读到一帧标准完整报文 <c>0x30</c>。纯只读、共享打开,不写设备、不切模式,因此不干扰手柄正常游戏输入。
///
/// 蓝牙下手柄通常以 ~60Hz 流式发 0x30,首帧即到;若只收到基础模式报文(0x3F,无电量),
/// 读尽尝试后抛 <see cref="IOException"/>(本期不主动写命令切模式)。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class HidStreamSwitchSource : ISwitchReportSource
{
    private const int MaxAttempts = 50; // ~60Hz 下不到 1 秒
    private readonly HidStream _stream;
    private readonly byte[] _buffer;

    public DeviceConnection Connection { get; }
    public DeviceKind Kind { get; }
    public string DeviceKey { get; }

    public HidStreamSwitchSource(HidDevice device, DeviceConnection connection, DeviceKind kind, string deviceKey)
    {
        Connection = connection;
        Kind = kind;
        DeviceKey = deviceKey;

        if (!device.TryOpen(out _stream!))
        {
            throw new IOException($"打开 Switch 手柄失败(可能被独占): {device.DevicePath}");
        }

        _stream.ReadTimeout = 1000;
        _buffer = new byte[device.GetMaxInputReportLength()];
    }

    public byte[] ReadStandardReport()
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            int len;
            try
            {
                len = _stream.Read(_buffer, 0, _buffer.Length);
            }
            catch (TimeoutException)
            {
                throw new IOException("Switch 手柄读取超时(可能未在流模式)");
            }

            if (len > SwitchProReport.BatConOffset && _buffer[0] == SwitchProReport.StandardReportId)
            {
                return _buffer[..len];
            }
        }

        throw new IOException("未收到标准完整报文 0x30(手柄可能处于基础模式)");
    }

    public void Dispose() => _stream.Dispose();
}
