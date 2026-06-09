using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>一次低电量告警的判定结果。</summary>
public sealed record AlertDecision(string DeviceKey, DeviceKind Kind, int Threshold, int Percentage);

/// <summary>
/// 低电量告警状态机(纯逻辑,可单测)。按设备独立维护已触发的阈值集合。
///
/// 规则(CLAUDE.md:20/10/5 三档,可关):
///   - 未充电且电量跌到某阈值、且该阈值对该设备尚未触发 → 触发;一次跌穿多档只弹**最严重**那档。
///   - 充电中不弹,并清空该设备已触发集(视为回升,重新武装)。
///   - 未充电的缓慢回升:电量高于某阈值即把该阈值重新武装。
///   - 关闭告警时不弹,但回升/充电的「重新武装」照常,避免重新开启后补弹历史。
/// </summary>
public sealed class LowBatteryAlerter
{
    private readonly IReadOnlyList<int> _thresholds; // 降序
    private readonly Func<bool> _enabled;
    private readonly Dictionary<string, HashSet<int>> _fired = new();

    public LowBatteryAlerter(IEnumerable<int> thresholds, Func<bool> enabled)
    {
        _thresholds = thresholds.Distinct().OrderByDescending(t => t).ToArray();
        _enabled = enabled ?? throw new ArgumentNullException(nameof(enabled));
    }

    /// <summary>喂入一台设备的一次读数,返回需要弹出的告警(无则 null)。</summary>
    public AlertDecision? Evaluate(string deviceKey, DeviceKind kind, int percentage, bool isCharging)
    {
        if (!_fired.TryGetValue(deviceKey, out HashSet<int>? fired))
        {
            fired = new HashSet<int>();
            _fired[deviceKey] = fired;
        }

        // 充电中:不弹并重新武装(在回升)
        if (isCharging)
        {
            fired.Clear();
            return null;
        }

        // 电量回升过某阈值 → 重新武装(未充电的缓慢回升)
        fired.RemoveWhere(t => percentage > t);

        if (!_enabled())
        {
            return null;
        }

        // 本次新跌穿的阈值:<=阈值 且尚未触发
        List<int> newlyCrossed = _thresholds.Where(t => percentage <= t && !fired.Contains(t)).ToList();
        if (newlyCrossed.Count == 0)
        {
            return null;
        }

        foreach (int t in newlyCrossed)
        {
            fired.Add(t); // 一次跌穿多档全部记为已触发
        }

        int worst = newlyCrossed.Min(); // 只弹最严重那档
        return new AlertDecision(deviceKey, kind, worst, percentage);
    }
}
