# MagicBattery

为 Windows 上使用 [mac-precision-touchpad](https://github.com/imbushuo/mac-precision-touchpad) 开源驱动的 **Apple Magic Trackpad 2 与 Magic Keyboard** 提供电量显示——弥补该驱动缺失的电量上报。系统托盘常驻，多设备同时显示，悬停看电量、低电量告警。（Magic Mouse 2 待真机校准后加入。）

> 纯用户态实现：**不改驱动、不需要管理员权限、不禁用驱动签名验证**。绿色版优先。

## 下载

预编译的**绿色单文件**（自包含 .NET 8 运行时，双击即用，无需安装）见 [**Releases**](https://github.com/EncoreQ/MagicBattery/releases/latest)。
要求 Windows 10 21H2+ / Windows 11，且已安装 mac-precision-touchpad 驱动。

## 状态

全部阶段已完成：触控板 + 键盘多设备电量、低电量告警、配置持久化。电量协议已用真机校准（触控板 + 键盘）。

| 阶段 | 内容 | 状态 |
|---|---|---|
| Phase 0 | 协议规约文档（`docs/protocol-spec.md`） | ✅ |
| Phase 1 | 电量读取层 `MagicBattery.Hid`（纯类库 + 单测） | ✅ |
| Phase 2 | WPF 托盘程序 `MagicBattery.Tray` | ✅ |
| Phase 3 | 低电量告警 + 多设备 + 配置持久化 | ✅ |

## 功能

- **多设备**：同时显示触控板 + 键盘（Magic Keyboard 系列）电量。托盘图标显示当前**最低电量**的设备，右键菜单逐设备列出「名称 + 电量 + 连接」。
- 托盘图标按电量分 **5 档**变色（>75 / 50 / 25 / 10 / <10），显示百分比数字，充电时叠加闪电。
- 悬停 tooltip 逐设备列出电量 + 连接方式 + 更新时间。
- **低电量告警**：20% / 10% / 5% 三档 Toast 通知（可在菜单关闭），未充电时按档触发、回升后重新武装。
- 右键菜单：立即刷新 / 低电量告警开关 / 开机自启 / 退出。
- 默认 15 分钟轮询；监听设备插拔（`WM_DEVICECHANGE`），设备增减与 USB↔蓝牙切换数秒内生效。
- 开机自启走 HKCU `Run` 键，**无需管理员**；其余配置存 `%APPDATA%\MagicBattery\config.json`。

> Magic Mouse 2 暂未纳入（Windows 下未取得真机校准）；其余协议同构，有设备校准后可一行加入。

## 电量协议（真机校准）

完整记录见 [`docs/protocol-spec.md`](docs/protocol-spec.md)。要点（Magic Trackpad 2，PID `0x0265`，Windows 11 实测）：

- 电量在 HID **Input report `0x90`**（3 字节），USB 与蓝牙**同一套机制**，用 `HidD_GetInputReport` 读取。
- `byte[1]` = 充电标志（`0x00` 未充电 / `0x03` 充电，与传输方式无关），`byte[2]` = 电量百分比（直读 0–100，无缩放）。
- 连接类型由 VID 区分：`0x05AC` = USB，`0x004C` = 蓝牙。
- 校准样本（`tests/fixtures/report-0x90/`）：`90 00 02`（触控板蓝牙 2%）、`90 03 03`（USB 充电 3%）、`90 03 64` / `90 00 64`（满电 充电/拔线）、`90 00 42`（**Magic Keyboard 蓝牙 66%**，证实键盘与触控板同构）。

> 协议全部以 Linux 内核 `hid-magicmouse.c`、Bluetooth SIG 等已有实现为准 + 真机校准，**未做盲目抓包逆向**。

## 构建与运行

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
# 构建 + 测试
dotnet build MagicBattery.sln -c Release
dotnet test  MagicBattery.sln -c Release

# 直接运行托盘程序
dotnet run --project src/MagicBattery.Tray -c Release

# 打包成自包含单文件 exe(绿色版,win-x64)
dotnet publish src/MagicBattery.Tray -p:PublishProfile=win-x64
# 产物:src/MagicBattery.Tray/bin/Release/net8.0-windows/win-x64/publish/MagicBattery.exe
```

## 项目结构

```
src/
  MagicBattery.Hid/     电量读取层(纯类库,无 UI 依赖)
  MagicBattery.Tray/    WPF 托盘程序
tests/
  MagicBattery.Hid.Tests/    读取层单测(录制报文做 fixture)
  MagicBattery.Tray.Tests/   托盘核心逻辑单测(轮询编排/档位/文案/告警/配置/自启)
  fixtures/                  真机录制的 report 字节
docs/
  protocol-spec.md      电量协议规约(含真机校准更正)
```

读取层与 UI 完全解耦：所有 HID 调用过 mock 友好接口，解析与编排逻辑均有单测，**不依赖"接上设备试一下"**。

## 技术选型

| 模块 | 选型 |
|---|---|
| UI 框架 | WPF (.NET 8) |
| 托盘库 | [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) |
| HID 访问 | [HidSharp](https://github.com/IntergatedCircuits/HidSharp) 枚举 + P/Invoke `HidD_GetInputReport` |
| 单元测试 | xUnit + FluentAssertions |

## 不做的事

不重新实现触控板手势（驱动已搞定）、不做 macOS 版、不做遥测/自动更新、不上 Microsoft Store。

## License

[MIT](LICENSE) © 2026 EncoreQ
