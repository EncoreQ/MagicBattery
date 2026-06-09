using MagicBattery.Hid;

namespace MagicBattery.Tray.Core;

/// <summary>一次低电量告警的判定结果。<see cref="Percentage"/> 仅精确设备有,粗档设备为 null。</summary>
public sealed record AlertDecision(string DeviceKey, DeviceKind Kind, BatteryLevel Level, int? Percentage);

/// <summary>
/// 低电量告警状态机(纯逻辑,可单测)。按设备独立维护已触发集。两类设备:
///   - **精确设备(Magic)**:按百分比阈值(默认 20/10/5),不回归 Phase 3 行为。
///   - **粗档设备(手柄)**:按档位,进入 <see cref="BatteryLevel.Low"/> 弹一次、进入
///     <see cref="BatteryLevel.Critical"/> 弹一次。
/// 共同规则:未充电才弹;一次跌穿多档只弹最严重;充电/回升重新武装;关闭时不弹但武装照常。
/// </summary>
public sealed class LowBatteryAlerter
{
    private static readonly BatteryLevel[] CoarseThresholds = { BatteryLevel.Low, BatteryLevel.Critical };

    private readonly IReadOnlyList<int> _percentThresholds; // 降序
    private readonly Func<bool> _enabled;
    private readonly Dictionary<string, DeviceState> _devices = new();

    public LowBatteryAlerter(IEnumerable<int> thresholds, Func<bool> enabled)
    {
        _percentThresholds = thresholds.Distinct().OrderByDescending(t => t).ToArray();
        _enabled = enabled ?? throw new ArgumentNullException(nameof(enabled));
    }

    /// <summary>喂入一台设备的一次读数(精确设备传 percentage,粗档设备传 null),返回需弹的告警或 null。</summary>
    public AlertDecision? Evaluate(string deviceKey, DeviceKind kind, BatteryLevel level, int? percentage, bool isCharging)
    {
        DeviceState st = GetOrAdd(deviceKey);

        if (isCharging)
        {
            st.Clear(); // 充电中不弹并重新武装
            return null;
        }

        return percentage is int pct
            ? EvaluatePercent(deviceKey, kind, level, pct, st)
            : EvaluateCoarse(deviceKey, kind, level, st);
    }

    private AlertDecision? EvaluatePercent(string key, DeviceKind kind, BatteryLevel level, int pct, DeviceState st)
    {
        st.FiredPercent.RemoveWhere(t => pct > t); // 回升过阈值 → 重新武装
        if (!_enabled())
        {
            return null;
        }

        List<int> crossed = _percentThresholds.Where(t => pct <= t && !st.FiredPercent.Contains(t)).ToList();
        if (crossed.Count == 0)
        {
            return null;
        }

        foreach (int t in crossed)
        {
            st.FiredPercent.Add(t);
        }

        return new AlertDecision(key, kind, level, pct); // 文案用当前 %
    }

    private AlertDecision? EvaluateCoarse(string key, DeviceKind kind, BatteryLevel level, DeviceState st)
    {
        st.FiredLevel.RemoveWhere(l => level > l); // 回升过某告警档 → 重新武装
        if (!_enabled())
        {
            return null;
        }

        List<BatteryLevel> crossed = CoarseThresholds.Where(l => level <= l && !st.FiredLevel.Contains(l)).ToList();
        if (crossed.Count == 0)
        {
            return null;
        }

        foreach (BatteryLevel l in crossed)
        {
            st.FiredLevel.Add(l);
        }

        BatteryLevel worst = crossed.Min(); // 序号最小 = 最严重
        return new AlertDecision(key, kind, worst, null);
    }

    private DeviceState GetOrAdd(string key)
    {
        if (!_devices.TryGetValue(key, out DeviceState? st))
        {
            st = new DeviceState();
            _devices[key] = st;
        }

        return st;
    }

    private sealed class DeviceState
    {
        public readonly HashSet<int> FiredPercent = new();
        public readonly HashSet<BatteryLevel> FiredLevel = new();

        public void Clear()
        {
            FiredPercent.Clear();
            FiredLevel.Clear();
        }
    }
}
