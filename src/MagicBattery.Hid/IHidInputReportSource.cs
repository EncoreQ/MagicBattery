namespace MagicBattery.Hid;

/// <summary>
/// 对单个已打开的 Magic HID 设备的最小抽象:按需取 Input report。
/// USB 与蓝牙共用同一抽象(取法相同,只是设备来自不同 VID)。
/// 把真实 P/Invoke 挡在接口后,让 <see cref="MagicBatteryReader"/> 能用 fake 脱离真机单测。
/// </summary>
public interface IHidInputReportSource : IDisposable
{
    /// <summary>此设备的连接类型(由 VID 判定)。</summary>
    DeviceConnection Connection { get; }

    /// <summary>设备类别(由 PID 判定)。</summary>
    DeviceKind Kind { get; }

    /// <summary>设备稳定标识(优先序列号、回退设备路径),用于多设备区分与跨连接的状态/告警连续。</summary>
    string DeviceKey { get; }

    /// <summary>
    /// 发起一次 GET_REPORT(Input)(控制管道),取当前 Input report。
    /// </summary>
    /// <param name="reportId">目标 report id(电量为 0x90)。</param>
    /// <param name="length">报文长度(含首字节 reportId)。</param>
    /// <returns>原始报文字节(首字节为 reportId)。</returns>
    /// <exception cref="System.IO.IOException">设备未就绪 / 读取失败时抛出,由上层转 Unavailable。</exception>
    byte[] GetInputReport(byte reportId, int length);
}
