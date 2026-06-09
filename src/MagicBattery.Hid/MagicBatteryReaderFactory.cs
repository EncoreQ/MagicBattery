using System.Runtime.Versioning;

namespace MagicBattery.Hid;

/// <summary>
/// 探测并创建当前可用的电量读取器。多设备:<see cref="CreateAll"/> 给出所有在线 Magic
/// 电量设备各一个 reader;<see cref="Create"/> 保留单设备入口(USB 优先)。
/// 同步即可:USB 与蓝牙都走 HID 枚举,不再有异步 GATT。
/// </summary>
[SupportedOSPlatform("windows")]
public static class MagicBatteryReaderFactory
{
    /// <summary>
    /// 创建第一个可用读取器(USB 优先);无设备返回 <c>null</c>。
    /// </summary>
    public static IBatteryReader? Create()
    {
        IHidInputReportSource? source = MagicHidDeviceEnumerator.TryOpenFirst();
        return source is null ? null : new MagicBatteryReader(source);
    }

    /// <summary>
    /// 创建**所有**在线 Magic 电量设备的读取器,每台一个。无设备返回空列表。
    /// </summary>
    public static IReadOnlyList<IBatteryReader> CreateAll() =>
        MagicHidDeviceEnumerator.OpenAll()
            .Select(source => (IBatteryReader)new MagicBatteryReader(source))
            .ToArray();
}
