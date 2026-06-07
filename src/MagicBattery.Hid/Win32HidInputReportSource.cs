using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace MagicBattery.Hid;

/// <summary>
/// <see cref="IHidInputReportSource"/> 的 Windows 实现:用 <c>HidD_GetInputReport</c>
/// 走控制管道按需取 Input report(HidSharp 不包这个 API,故直接 P/Invoke)。
/// 这是真机校准验证过的取报文方式(USB 与蓝牙都走它;<c>HidD_GetFeature</c> 不通)。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32HidInputReportSource : IHidInputReportSource
{
    private readonly SafeFileHandle _handle;

    public DeviceConnection Connection { get; }

    /// <param name="devicePath">HidSharp 给出的设备路径(<c>\\?\hid#...</c>)。</param>
    /// <param name="connection">由 VID 判定的连接类型。</param>
    public Win32HidInputReportSource(string devicePath, DeviceConnection connection)
    {
        Connection = connection;
        _handle = CreateFileW(devicePath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_RW,
            IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (_handle.IsInvalid)
        {
            throw new IOException(
                $"打开 HID 设备失败 (err={Marshal.GetLastWin32Error()}): {devicePath}");
        }
    }

    public byte[] GetInputReport(byte reportId, int length)
    {
        var buffer = new byte[length];
        buffer[0] = reportId;
        if (!HidD_GetInputReport(_handle, buffer, buffer.Length))
        {
            throw new IOException(
                $"HidD_GetInputReport(0x{reportId:X2}) 失败 (err={Marshal.GetLastWin32Error()})");
        }

        return buffer;
    }

    public void Dispose() => _handle.Dispose();

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_RW = 0x00000003;
    private const uint OPEN_EXISTING = 3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share,
        IntPtr sa, uint create, uint flags, IntPtr template);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetInputReport(SafeFileHandle h, byte[] buffer, int bufferLength);
}
