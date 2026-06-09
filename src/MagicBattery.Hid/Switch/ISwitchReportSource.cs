namespace MagicBattery.Hid;

/// <summary>
/// 对单个已打开的 Switch 手柄的最小抽象:读一帧标准完整输入报文(0x30)。
/// 把真实 HidStream IO 挡在接口后,让 <see cref="SwitchProBatteryReader"/> 能用 fake 脱离真机单测。
/// </summary>
public interface ISwitchReportSource : IDisposable
{
    /// <summary>连接类型(本期手柄走蓝牙)。</summary>
    DeviceConnection Connection { get; }

    /// <summary>设备类别(<see cref="DeviceKind.Gamepad"/>)。</summary>
    DeviceKind Kind { get; }

    /// <summary>设备稳定标识(序列号 / 路径)。</summary>
    string DeviceKey { get; }

    /// <summary>
    /// 从输入流读到一帧标准完整报文 <c>0x30</c> 并返回。
    /// </summary>
    /// <exception cref="System.IO.IOException">超时 / 只收到基础模式报文(0x3F,无电量)/ 读失败。</exception>
    byte[] ReadStandardReport();
}
