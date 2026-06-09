# Switch Pro Controller 电量协议规约

> Phase 4 产出物。与 `protocol-spec.md`(Apple Magic)并列,记录 Nintendo Switch Pro 手柄的电量读取协议。
> 协议以现成实现为准 + 真机校准,**未自行盲抓**。

## 来源

- Linux 内核 `drivers/hid/hid-nintendo.c`，函数 `joycon_parse_battery_status`（权威字段定义）。
- dekuNukem《Nintendo_Switch_Reverse_Engineering》标准输入报文文档。
- 本机 spike 真机校准（2026-06-09，Switch Pro Controller，蓝牙）。

## 设备识别

| 项 | 值 |
|---|---|
| VID | `0x057E`（Nintendo） |
| PID | `0x2009`（Pro Controller）。Joy-Con `0x2006`/`0x2007`、NES `0x2017` 同协议，本期未启用 |
| 连接 | 蓝牙（本期仅蓝牙；USB 需握手切协议，侵入性，未做） |
| DeviceKey | HID 序列号（蓝牙 MAC，跨连接稳定；实测 `483177508f6a`） |

> 识别按 VID/PID，不靠设备名（实测产品名为通用的 "Wireless Gamepad"）。

## 报文与读取方式

- 蓝牙下手柄以 **~60Hz 流式**发送**标准完整输入报文 `0x30`**（实测无需任何写操作即在此模式；
  spike 5 秒收到 334 帧）。
- **读法**：打开 HID 输入流（HidSharp `HidStream`），读到一帧 `0x30`，取 `byte[2]`，关流。
  **纯只读、共享打开、不写设备、不切模式** —— 不干扰手柄正常作为游戏手柄使用。
- 若只收到基础模式报文 `0x3F`（不含电量），本期不主动写命令切换 → 记为 Unavailable。

## 电量字节 `byte[2]`（bat_con）

```text
byte[0] = 0x30 (report id)
byte[1] = timer
byte[2] = bat_con:
    bit0      = host powered（外部供电存在）
    bit4 (0x10) = 充电中
    bits5-7 (>>5) = 电量档 0..4
```

电量档（`byte[2] >> 5`，对应 Linux `POWER_SUPPLY_CAPACITY_LEVEL_*`）：

| raw | 含义 | 本项目 BatteryLevel |
|---|---|---|
| 0 | empty / critical | Critical |
| 1 | low | Low |
| 2 | medium / normal | Medium |
| 3 | high | High |
| 4 | full | Full |

**手柄只给这 5 粗档，无精确百分比**，故读数 `Percentage` 为 null，UI 按档位显示（满/高/中/低/危 + 电量格子）。
这 5 档与本项目 Magic 设备用的 `BatteryLevel` 1:1，因此手柄与 Magic 设备共用同一显示/告警管线。

## 真机校准

| 报文 | 解码 | ground truth |
|---|---|---|
| `byte[2] = 0x60`（`30 4C 60 …`） | `0x60>>5 = 3` = 高档；`0x60 & 0x10 = 0` 未充电 | Switch 主机显示近乎全满，未充电 ✓ |

fixture：`tests/fixtures/switch-pro/pro_bt_high.hex`。

## 与 Magic 协议的差异

| | Magic（触控板/键盘） | Switch Pro 手柄 |
|---|---|---|
| 报文 | Input report `0x90`，按需 `HidD_GetInputReport` 拉取 | Input report `0x30`，60Hz 流式，开流读一帧 |
| 电量 | 精确 0–100% | 5 粗档（0–4） |
| 充电 | byte[1] != 0 | byte[2] bit4 |
| 写设备 | 否（纯只读） | 否（纯只读，未切模式） |

## 本期不做

USB 路径（需 `0x80` 握手序列）、主动切报文模式（写设备）、Joy-Con / NES 手柄。
