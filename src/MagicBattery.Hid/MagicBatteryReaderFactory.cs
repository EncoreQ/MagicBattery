using System.Runtime.Versioning;

namespace MagicBattery.Hid;

/// <summary>
/// 探测并创建当前可用的电量读取器。USB 优先,其次蓝牙(见 <see cref="MagicHidDeviceEnumerator"/>)。
/// 同步即可:USB 与蓝牙都走 HID 枚举,不再有异步 GATT。
///
/// 注意:连接切换(USB 拔出后回到蓝牙)的去抖编排属于 Phase 2,本工厂只做一次性选路。
/// </summary>
[SupportedOSPlatform("windows")]
public static class MagicBatteryReaderFactory
{
    /// <summary>
    /// 创建读取器。
    /// </summary>
    /// <returns>可用时返回 <see cref="MagicBatteryReader"/>;无设备时返回 <c>null</c>(对应 Disconnected)。</returns>
    public static IBatteryReader? Create()
    {
        IHidInputReportSource? source = MagicHidDeviceEnumerator.TryOpenFirst();
        return source is null ? null : new MagicBatteryReader(source);
    }
}
