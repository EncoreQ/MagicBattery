using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>托盘图标的渲染形态。</summary>
public enum TrayIconKind
{
    /// <summary>精确设备:画百分比数字。</summary>
    Number,

    /// <summary>粗档设备(手柄):画 0–4 格电量。</summary>
    Bars,
}

/// <summary>
/// 描述托盘图标「要画什么」的纯数据。精确设备(Magic)画数字,粗档设备(手柄)画电量格子;
/// 两者都按档位上色、充电叠闪电、未连接置灰。
/// 把「画什么」(可单测)与「怎么光栅化」(<c>TrayIconRenderer</c>)分开。
/// </summary>
public sealed record TrayIconModel(
    TrayIconKind Kind,
    string Text,        // Number 模式:显示文本
    int Bars,           // Bars 模式:点亮格数 0–4
    RgbColor Background,
    bool ShowBolt,
    bool Dimmed)
{
    public static TrayIconModel FromState(DeviceBattery state)
    {
        if (state.Availability == BatteryAvailability.Disconnected)
        {
            return new TrayIconModel(TrayIconKind.Number, "?", 0, BatteryPalette.Disconnected,
                ShowBolt: false, Dimmed: true);
        }

        RgbColor color = BatteryPalette.ColorFor(state.Level);

        if (state.Percentage is int pct)
        {
            // 精确设备:数字
            return new TrayIconModel(TrayIconKind.Number, Math.Clamp(pct, 0, 100).ToString(), 0,
                color, state.IsCharging, Dimmed: false);
        }

        // 粗档设备(手柄):0–4 格,格数 = 档位序号(满=4 … 危=0)
        return new TrayIconModel(TrayIconKind.Bars, string.Empty, (int)state.Level,
            color, state.IsCharging, Dimmed: false);
    }
}
