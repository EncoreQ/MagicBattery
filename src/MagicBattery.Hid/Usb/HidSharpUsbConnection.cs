using HidSharp;

namespace MagicBattery.Hid.Usb;

/// <summary>
/// <see cref="IUsbHidConnection"/> 的 HidSharp 实现(真机适配层)。
///
/// ⚠️ 待真机手测:本类不在 Phase 1 跑真机。GetFeature 的缓冲区约定、报文长度
/// 是否需对齐设备 MaxFeatureReportLength,均须拿到设备后验证(protocol-spec.md §8 U4)。
/// </summary>
public sealed class HidSharpUsbConnection : IUsbHidConnection
{
    private readonly HidStream _stream;

    public HidSharpUsbConnection(HidStream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public byte[] GetFeatureReport(byte reportId, int reportLength)
    {
        // HidSharp 约定:缓冲区首字节为 Report ID,GetFeature 会就地填充其余字节。
        var buffer = new byte[reportLength];
        buffer[0] = reportId;
        _stream.GetFeature(buffer);
        return buffer;
    }

    public void Dispose() => _stream.Dispose();
}
