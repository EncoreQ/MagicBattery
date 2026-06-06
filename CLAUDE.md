# MagicBattery — Magic Trackpad 2 电量显示工具

## 项目目标

为 Windows 上使用 mac-precision-touchpad 的 Apple Magic Trackpad 2（未来可扩展到 Magic Mouse 2 / Magic Keyboard）提供电量显示，弥补开源驱动缺失的电量上报。

MVP：系统托盘图标 + 悬停显示电量百分比 + 低电量通知。

## 硬约束

- **不修改 mac-precision-touchpad 驱动代码**。所有功能在用户态完成。改了驱动就失去 Microsoft 签名，得不偿失。
- **不使用 test signing 或任何需要禁用驱动签名验证的方案**。
- **不需要管理员权限运行**。绿色版优先。
- 目标平台：Windows 11（主），Windows 10 21H2+（次）。
- 目标运行时：.NET 8 LTS。

## 技术选型

| 模块 | 选型 | 备注 |
|---|---|---|
| UI 框架 | **WPF** | 不用 WinUI 3，tray 支持仍不稳定 |
| 托盘库 | `H.NotifyIcon.Wpf` | 比自己 P/Invoke Shell_NotifyIcon 省心 |
| BLE 访问 | WinRT `Windows.Devices.Bluetooth` | 通过 `Microsoft.Windows.SDK.NET` 引入 |
| HID 访问 | `HidSharp` | 跨连接方式抽象优先 |
| 单元测试 | xUnit + FluentAssertions | |
| 打包 | self-contained single-file exe | 不上 Microsoft Store |

任何新增依赖必须先讨论，不要静默 `dotnet add package`。

## 协议参考

电量协议**不要自己抓包逆向**，全部以下列已有实现为准。Phase 0 的任务就是把这些参考代码读懂、整理成中立文档：

1. Linux 内核 `drivers/hid/hid-magicmouse.c` — 完整的 USB + BLE 解析参考
   <https://github.com/torvalds/linux/blob/master/drivers/hid/hid-magicmouse.c>
2. José Expósito 2021/11 USB 电量补丁 — 修了 USB 下 report descriptor 并加主动轮询
3. Julius Lehmann 2026/02 修复补丁（LKML）— 最新已知问题修正
4. Bluetooth SIG Battery Service `0x180F` / characteristic `0x2A19` — BLE 路径走标准 GATT
5. mac-precision-touchpad release notes 中 "Battery status indicator is still WIP" 的相关 issue 讨论

## 阶段划分

每个阶段独立可交付、可单测。完成一个 Phase 提交一次，不要混。每个 Phase 开一个 feature 分支，主分支不直接 commit。

### Phase 0 — 协议规约文档（不写代码）

产出物：`docs/protocol-spec.md`

需要明确记录：

- USB 连接下 Magic Trackpad 2 的 VID/PID
- HID feature report 的 report ID 和字节布局
- 电量百分比的计算公式，含已知边界（充电中、设备睡眠时返回的怪值等）
- BLE 路径下的 service / characteristic UUID
- USB vs BLE 的差异和运行时检测方法
- Magic Mouse 2 / Magic Keyboard 的对应字段（为 Phase 3 留扩展点）

**这一步不动一行代码**。先从参考实现里读懂协议、写文档，让我 review，再进 Phase 1。

### Phase 1 — 电量读取层

产出物：`src/MagicBattery.Hid/`，纯类库，无 UI 依赖

接口草案（最终签名以 review 为准）：

```csharp
public interface IBatteryReader
{
    Task<BatteryStatus> ReadAsync(CancellationToken ct);
    IObservable<BatteryStatus> Changes { get; }
}

public record BatteryStatus(
    int Percentage,
    bool IsCharging,
    DeviceConnection Connection,
    DateTimeOffset Timestamp);

public enum DeviceConnection { Usb, Bluetooth, Disconnected }
```

要求：

- BLE 实现和 USB 实现是两个独立的 `IBatteryReader`，上层根据当前连接选择
- 所有 HID/BLE 调用都过一层 mock 友好的接口
- **解析逻辑必须有单测，用录制的 report 字节做测试数据**。不要靠"接上设备试一下"
- 录制的测试数据放 `tests/fixtures/`，每条数据注明来源（哪台设备、什么状态）

### Phase 2 — 托盘程序

产出物：`src/MagicBattery.Tray/`，WPF

功能：

- 托盘图标按电量分 5 档变化（>75 / 50 / 25 / 10 / <10）
- 悬停 tooltip：百分比 + 连接方式 + 最后更新时间
- 右键菜单：立即刷新 / 开机自启 / 退出
- 默认轮询 15 分钟（对齐 Magic Utilities 的频率），可配置

### Phase 3 — 通知 + 多设备

- Windows Toast 低电量告警（20% / 10% / 5% 三档，可关）
- 扩展支持 Magic Mouse 2 + Magic Keyboard（沿用 Phase 0 文档预留的格式）
- 配置持久化到 `%APPDATA%\MagicBattery\config.json`

## 工作约定

- **先写 spec 或接口签名，再写实现**。设计 review 通过再下手。
- **不引入选型表外的依赖**，要加先讨论。
- **不要为了"完整性"实现没列在阶段里的功能**。Scope creep 是这种小工具的头号杀手。
- Commit 信息走 conventional commits：`feat(hid): ...` / `fix(tray): ...` / `docs(spec): ...`
- 代码注释 / 文档用中文 OK，标识符 / API 命名用英文。
- 不要在回答里复述这份 CLAUDE.md 的内容来"确认理解"——直接干活。

## 明确不做的事

- 不重新实现触控板手势（驱动已搞定）
- 不做 macOS 版本（系统自带）
- 不做动画、毛玻璃、主题切换——这是个 < 20MB 内存的常驻工具
- 不做遥测、统计、自动更新
- 不上 Microsoft Store（避免 MSIX 复杂度）
- 不实现设备配对、连接管理（交给 Windows 蓝牙设置）

## 验收场景

Phase 2 完成后至少跑通：

- USB 直连读到电量
- BLE 连接读到电量
- USB 拔出后切换到 BLE 读取
- 设备睡眠唤醒后能正确恢复读取
- 电量 < 5% 触发告警（Phase 3）
- 自用一周再进 Phase 3，期间记录所有边角问题
