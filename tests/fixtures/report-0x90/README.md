# report-0x90 真机录制数据

HID Input report `0x90`(3 字节),Magic Trackpad 2 实测。这是 **USB 与蓝牙通用**的
电量报文(见 `docs/protocol-spec.md` 顶部「实测更正」节)。

读取方式:`HidD_GetInputReport`(控制管道 GET_REPORT(Input))。
字节布局:`byte[0]`=report id 0x90,`byte[1]`=充电标志,`byte[2]`=电量%(直读 0-100)。

| 文件 | 字节 | 含义 | source |
|---|---|---|---|
| `mt2_bt_2pct.hex` | `90 00 02` | 蓝牙(VID 0x004C)、拔线、byte[1]=0x00(未充电)、byte[2]=2 | 真机:Magic Trackpad 2 (PID 0x0265),2026-06-08,ground truth 2% |
| `mt2_usb_charging_3pct.hex` | `90 03 03` | USB(VID 0x05AC)、充电、byte[1]=0x03(充电中)、byte[2]=3 | 真机:同一台,USB 插线充电,2026-06-08,ground truth 3% |
| `mt2_bt_full_charging_100pct.hex` | `90 03 64` | **蓝牙(VID 0x004C)、充电中、byte[1]=0x03、byte[2]=0x64=100** —— 充电时数据仍走蓝牙,证明 byte[1] 充电标志独立于 VID/传输 | 真机:同一台,充满后采集,2026-06-08 |
| `mt2_bt_full_unplugged_100pct.hex` | `90 00 64` | **蓝牙、拔线满电、byte[1]=0x00、byte[2]=100** —— 与上一行仅差 byte[1],拔线后从 0x03 翻 0x00,干净坐实 byte[1] 即充电标志 | 真机:同一台,从满电拔线后立即采集,2026-06-08 |
| `mt2_garbage_oob.hex` | `90 00 C8` | byte[2]=200 > 100,越界怪值,应判为 Unavailable | SYNTHETIC(构造的边界用例) |

## 待补录(后续在对应状态下采集)

- ~~中/高电量点~~ 已补:满电 100% = `90 03 64`(充电)/ `90 00 64`(拔线),byte[2] 高值直读确认。
- ~~真正拔线满电~~ 已补:`90 00 64`(byte[1]=0x00、byte[2]=100)。
- 设备睡眠/刚唤醒的怪值(spec §8 U5)。
- `byte[1]` 充电标志的逐位精确含义(目前已知 0x00=未充电、0x03=充电;0x03 与 VID 无关,仅取决于是否接入外部供电)。

## 说明

这两条数据将在 Phase 1 重构(改用 report 0x90 + HidD_GetInputReport)后接入解析单测。
当前 `tests/fixtures/usb/*.hex` 是旧的 **SYNTHETIC** 占位数据,驱动现版本(待重构)的测试,
重构时一并替换/删除。
