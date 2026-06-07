# MagicBattery 电量协议规约（Protocol Spec）

> Phase 0 产出物。本文档**只整理协议**，不含任何实现代码。
> 所有内容以 CLAUDE.md「协议参考」中列出的已有实现为准，**未做任何自行抓包逆向**。
> 凡是无法从参考实现确证、必须实测确认的字段，统一收敛到文末
> [§8 待 Phase 1 实测确认](#8-待-phase-1-实测确认known-unknowns)，请勿在实现中凭印象硬编码。

## ⚠️ 实测更正（2026-06-08 真机校准，权威）

> 本节基于一台真实 Magic Trackpad 2 在 Windows 11 上的实测，**覆盖**下文 §2 / §4 / §5
> 中由 Linux 参考实现**推断**出的协议模型。下文那些章节予以保留（解释当初为何那样推断、
> 以及 Linux 侧的真实行为），但**与本节冲突处，一律以本节为准**。
> 校准设备:Magic Trackpad 2(Lightning,PID 0x0265),ground truth 电量 2%。

**核心更正:Windows 上 USB 与蓝牙是同一套机制,不存在「USB feature report」与「BLE GATT」两条独立路径。**

录到的真实报文:

```text
蓝牙(VID 0x004C, 拔线, 真实 2%):  report 0x90 = 90 00 02
USB (VID 0x05AC, 插线充电, 真实 3%): report 0x90 = 90 03 03
                                              ↑b1 ↑b2
```

统一模型(USB / 蓝牙通用):

| 项 | 实测值 |
|---|---|
| 电量承载 | HID **Input report `0x90`**,共 **3 字节**(USB 与蓝牙完全相同) |
| 取报文方式 | **`HidD_GetInputReport`**(走控制管道发 GET_REPORT(Input))。`HidD_GetFeature` 不通(err=1,无 feature report);中断管道 20s 内不主动推送 |
| `byte[0]` | report id = `0x90` |
| `byte[1]` | **充电/电源标志位**(描述符 usage 0x61/0x44/0x46)。拔线蓝牙=`0x00`,USB 充电=`0x03`。**`byte[1] != 0` ⟺ 正在充电/接入外部供电** |
| `byte[2]` | **电量百分比,直读 0–100**(2↔2%、3↔3% 两点确认),**无需缩放**;`>100` 视为怪值 |
| 正确 HID 接口 | `mi_00 / col02`(即带 report 0x90 的那个 collection);其余 collection(report 0xC0/0x3F/0xE0/0x9A 等)是触控/其它数据 |
| 连接判别 | 设备 VID:`0x05AC`=USB、`0x004C`=蓝牙;PID 均 `0x0265`。**插上 USB 时蓝牙的 004C HID 接口会消失**(设备切到 USB) |
| 描述符 | report 0x90 在**厂商自定义页**(`06 00 FF`)下,**不是**标准 Battery Strength usage(0x06/0x20)。所以 Windows/HidSharp 不会自动把它识别成电量 —— 必须自己按字节解析 |

实测原始描述符(col02,供参考):

```text
蓝牙 (62B): 0600FF0914A101859005850961150025013500450165005500750195018102094481020946810295058103096525FF463D016513550D750895018102C100
USB  (57B): 0600FF0914A101859005850961150025013500450165005500750195018102094481020946810295058103096525FF4500750895018102C100
```

两者仅 usage 0x65 字段的 physical/unit 元数据不同(蓝牙多 `463D01 6513 550D`),电量字节布局一致。

**为什么和 Linux 参考不一样:** Linux 内核 hid-magicmouse 走自己的 descriptor fixup + power_supply
子系统,在 Linux 上 USB 电量确实表现为 feature report。但 **Windows HID 栈把这条 report 暴露成
Input report,且 USB / 蓝牙表现一致**。本项目在 Windows 用户态,**按 Windows 的现实实现**;
Linux 参考仅用于理解字段含义(report id、电量字段位置),不照搬其取报文方式。

**对 Phase 1 的影响(重构待单独 review):**

- `Ble/` 整个 WinRT GATT 子系统(`IBleBatteryGatt` / `WinRtBleGatt` / `BleDeviceLocator`)方向作废。
- `IUsbHidConnection.GetFeatureReport` → 改为 `HidD_GetInputReport`(HidSharp 不带,需 P/Invoke)。
- USB / 蓝牙合并为**单一「HID report 0x90」读取器**,连接类型只由设备 VID 区分。
- `IsCharging` 从 `byte[1]` 读真值,不再按「USB 必充电」假设。
- `UsbBatteryReportLayout` → reportId=0x90、len=3、电量偏移=2、flags 偏移=1。
- 设备名本地化(实测为「RZha的妙控板」),**不能靠名字含 "Trackpad"/"Magic" 来识别**,应按 VID/PID。

---

## 0. 适用设备与命名

| 设备 | 本文档代号 | 备注 |
|---|---|---|
| Apple Magic Trackpad 2 | MT2 | **MVP 唯一目标** |
| Apple Magic Mouse 2 | MM2 | Phase 3 扩展，本文档预留字段 |
| Apple Magic Keyboard (2015/2021/2024) | MK | Phase 3 扩展，本文档预留字段 |

两条电量读取路径：

- **USB 路径**：设备通过 Lightning / USB-C 线缆直连。电量在 HID **feature report** 里，**不会主动上报，必须主动轮询**。
- **BLE 路径**：设备通过蓝牙连接。电量走**标准 GATT Battery Service**，支持 notify。

---

## 1. USB 连接 — VID / PID

厂商与产品 ID 摘自 Linux 内核 `drivers/hid/hid-ids.h`（torvalds/linux master）。

```text
USB_VENDOR_ID_APPLE                     0x05AC   // USB 直连时的 VID
BT_VENDOR_ID_APPLE                      0x004C   // 蓝牙栈上报时的 VID（HID over BT）
```

| 设备 | 常量名 | PID |
|---|---|---|
| Magic Trackpad 2 (Lightning) | `USB_DEVICE_ID_APPLE_MAGICTRACKPAD2` | `0x0265` |
| Magic Trackpad 2 (USB-C) | `USB_DEVICE_ID_APPLE_MAGICTRACKPAD2_USBC` | `0x0324` |
| Magic Mouse 2 (Lightning) | `USB_DEVICE_ID_APPLE_MAGICMOUSE2` | `0x0269` |
| Magic Mouse 2 (USB-C) | `USB_DEVICE_ID_APPLE_MAGICMOUSE2_USBC` | `0x0323` |
| Magic Mouse 1（无电池上报，仅参考） | `USB_DEVICE_ID_APPLE_MAGICMOUSE` | `0x030D` |
| Magic Trackpad 1（无电池上报，仅参考） | `USB_DEVICE_ID_APPLE_MAGICTRACKPAD` | `0x030E` |
| Magic Keyboard 2015 | `USB_DEVICE_ID_APPLE_MAGIC_KEYBOARD_2015` | `0x0267` |
| Magic Keyboard Numpad 2015 | `..._MAGIC_KEYBOARD_NUMPAD_2015` | `0x026C` |
| Magic Keyboard 2021 | `..._MAGIC_KEYBOARD_2021` | `0x029C` |
| Magic Keyboard 指纹版 2021 | `..._MAGIC_KEYBOARD_FINGERPRINT_2021` | `0x029A` |
| Magic Keyboard Numpad 2021 | `..._MAGIC_KEYBOARD_NUMPAD_2021` | `0x029F` |
| Magic Keyboard 2024 | `..._MAGIC_KEYBOARD_2024` | `0x0320` |
| Magic Keyboard 指纹版 2024 | `..._MAGIC_KEYBOARD_FINGERPRINT_2024` | `0x0321` |
| Magic Keyboard Numpad 2024 | `..._MAGIC_KEYBOARD_NUMPAD_2024` | `0x0322` |

> **MVP 只需匹配 `0x05AC` + (`0x0265` | `0x0324`)。** 其余 PID 写进设备表为 Phase 3 留口，但 Phase 1/2 不实现。
>
> **注意**：内核里仅 `is_usb_magicmouse2()` / `is_usb_magictrackpad2()` 这两族（MM2 / MT2 及其 USB-C 变体）在 USB 下做电量轮询；**Magic Mouse 1 / Trackpad 1 不在内核 USB 电量支持范围内**，不要尝试。

---

## 2. USB 路径 — HID feature report 布局

> ⚠️ **本节已被[实测更正](#-实测更正2026-06-08-真机校准权威)覆盖**:Windows 上 USB 电量不是 feature report,
> 而是 Input report `0x90`,用 `HidD_GetInputReport` 读,byte[2]=电量。以下保留 Linux 侧推理供参考。

### 2.1 电量字段的承载方式

参考实现 = José Expósito 2021/11 补丁《HID: magicmouse: Report battery level over USB》
（Linux 5.16，文件 `drivers/hid/hid-magicmouse.c`）。

关键事实：

1. 电量值用**标准 HID「Battery Strength」usage** 承载，位于一个 **feature report** 中。
   对应 HID usage：`Usage Page 0x06 (Generic Device Controls)` + `Usage 0x20 (Battery Strength)`。
2. 内核**不硬编码 report ID**。它依赖 HID core 解析 report descriptor，把发现的电量 report ID
   存进 `hdev->battery.report_id`（feature report 类型），轮询时用它发 `GET_REPORT`：

   ```c
   report_enum = &hdev->report_enum[bat->report_type];   // 即 HID_FEATURE_REPORT
   report      = report_enum->report_id_hash[bat->report_id];
   hid_hw_request(hdev, report, HID_REQ_GET_REPORT);      // 主动拉取
   ```

3. **report descriptor 有缺陷，内核要先 fixup 才能正确解析出电量字段**（见 §2.2）。

### 2.2 report descriptor 的 fixup（关键坑）

`magicmouse_report_fixup()` 中（已合并 Julius Lehmann 2026/02 修正）：

```c
if ((is_usb_magicmouse2(hdev->vendor, hdev->product) ||
     is_usb_magictrackpad2(hdev->vendor, hdev->product)) &&
    *rsize >= 83 && rdesc[46] == 0x84 && rdesc[58] == 0x85) {
        hid_info(hdev, "fixing up magicmouse battery report descriptor\n");
        *rsize = *rsize - 1;
        rdesc  = rdesc + 1;          // 丢掉首字节，使后续 item 对齐
}
```

含义与对我们的影响：

- 原始 report descriptor 多了一个字节，导致 HID 解析器**无法正确识别出电量 feature report**。
  内核通过「丢掉首字节、长度 -1」把后续 item 对齐回来。
- **`rdesc[46] == 0x84` / `rdesc[58] == 0x85`**：`0x85` 是 HID 全局 item「Report ID」前缀，
  说明电量 report 自带 Report ID（即电量 report **不是** report ID 0 的默认报告）。
  具体 report ID 数值 = 修正后描述符里该 `0x85` item 的数据字节，**需 Phase 1 实测读出**（§8）。
- **Julius Lehmann 2026/02 补丁**只把判定条件从 `*rsize == 83` 放宽成 `*rsize >= 83`：
  新款（USB-C）设备的描述符更长，原本的「精确等于 83」判定漏掉了它们，
  导致 **MT2 USB 下电量读不到**。除此之外不改字节布局。

### 2.3 Windows / 本项目读取方式（与 Linux 的差异）

Linux 靠改 descriptor + 走 power_supply 子系统拿值。**我们在 Windows 用户态，没有这套机制**，因此：

- 我们**不复刻 descriptor fixup**（那是改内核解析器行为）。
- 直接对设备发 **HID Get Feature Report**（HidSharp 的 feature report API），
  指定电量 report ID，把返回 buffer 里的电量字节自己解析出来。
- 但 descriptor 缺陷意味着 **Windows HID 解析器 / HidSharp 解析出的 report 元数据可能也不准**。
  因此 Phase 1 的稳妥策略是双轨：
  1. 先尝试用 HidSharp 解析出的 feature report 集合，找含 Battery Strength usage 的 report；
  2. 若解析不可靠，则对候选 report ID 直接发 `GetFeature`，按实测字节偏移解析。
  实测得到的 report ID、报文长度、电量字节偏移**必须录成 fixture**（见 §8、§9）。

### 2.4 字节布局（模板，待实测填实）

```text
Feature report (GET_REPORT):
  byte[0]            Report ID          = <REPORT_ID, 待实测>
  ...
  byte[BAT_OFFSET]   Battery Strength   = 电量原始值（见 §3 换算）
  ...
```

> `BAT_OFFSET` 与报文总长度在 §8 标注为待实测项。**实现里不得猜值**。

---

## 3. 电量百分比换算

### 3.1 标准换算

电量字段是 HID「Battery Strength」usage。HID 规范下，该 usage 的原始值按
report descriptor 声明的 **Logical Minimum / Logical Maximum** 线性映射到 **0–100%**：

```text
percentage = round( (raw - LogicalMin) * 100 / (LogicalMax - LogicalMin) )
```

- 经验上 Apple 这几款设备的电量字段**通常就是直接的 0–100**（即 LogicalMin=0, LogicalMax=100，
  raw 即百分比），但**这一点必须用实测 descriptor 的 Logical Min/Max 确认**（§8），
  不要默认 raw==percent。
- 结果一律 **clamp 到 [0,100]**。

### 3.2 已知边界与怪值

| 场景 | 现象 / 参考依据 | 处理建议 |
|---|---|---|
| 电量已满 | 内核 `magicmouse_fetch_battery()`：当 `bat->capacity == bat->max` 时**直接 return -1 跳过本次轮询**，不再请求。 | 满电时拿不到「新」值是正常的，按上一次有效值显示，不要当成错误。 |
| 充电中（USB 连着） | USB 直连本身即在充电；电量字段反映当前电量，但读数可能阶梯式/滞后变化。 | `IsCharging` 在 USB 连接下置 true（见 §5）。 |
| 设备睡眠 / 刚唤醒 | 睡眠时不响应或返回陈旧值；唤醒初期可能返回 0 或异常值。 | 读到 0 或越界值时**不要立即上报**，重试一次；连续异常才判为无效。 |
| GET_REPORT 失败 / 超时 | 设备未就绪 / 正在切换连接。 | 视为本次读取失败，保留上次有效值，等下个轮询周期。 |
| BLE 路径 raw=0 | 部分设备未连接/未配对时返回 0。 | 同上，结合连接状态判断而非直接显示 0%。 |

> **设计原则**：电量读取层要区分「读到一个有效新值」「无新值（如满电跳过）」「读取失败」三态，
> 不要把后两者错当成 0%。这会直接影响 Phase 3 低电量告警的误报。

### 3.3 轮询频率

- 内核 USB 轮询周期：`USB_BATTERY_TIMEOUT_SEC = 60`（60 秒）。
- 本项目 Phase 2 默认 **15 分钟**（对齐 Magic Utilities，CLAUDE.md 要求），可配置。
  两者不冲突：内核 60s 是驱动层缓存刷新；我们 15min 是 UI 层展示刷新。

---

## 4. BLE 路径 — GATT Battery Service

> ⚠️ **本节对 Magic Trackpad 2 不成立,已被[实测更正](#-实测更正2026-06-08-真机校准权威)覆盖**:该设备走
> **Bluetooth Classic HID**,电量在 HID Input report `0x90` 的 byte[2],**不暴露 GATT `0x180F` 服务**
> (实测已配对 BLE 设备数=0)。以下 GATT 内容对该设备不适用,仅作通用 GATT 知识留存。

BLE 走**蓝牙标准 GATT**，与设备厂商无关，**不需要任何 Apple 私有协议**。

| 项 | UUID（16-bit / 完整 128-bit） |
|---|---|
| Battery Service | `0x180F` → `0000180F-0000-1000-8000-00805F9B34FB` |
| Battery Level Characteristic | `0x2A19` → `00002A19-0000-1000-8000-00805F9B34FB` |

- **Battery Level characteristic 值**：单字节 `uint8`，**直接就是 0–100 的百分比**，无需换算。
- 支持 **Read** 和 **Notify**：
  - **Read**：主动读一次。
  - **Notify**：订阅后设备电量变化时主动推送 —— 这是 BLE 相对 USB 的核心优势，
    Phase 2 BLE 实现应优先用 notify，省去高频轮询。
- 充电状态：标准 Battery Service **不含充电标志**。BLE 路径下 `IsCharging` 一般置 false
  （蓝牙连接时通常未接线充电）。如需更准状态可读可选的 Battery Level Status characteristic
  `0x2BED`，但 **MVP 不做**，Apple 设备是否实现该特征未知（§8）。

### 4.1 Windows 访问方式

通过 WinRT `Windows.Devices.Bluetooth`（经 `Microsoft.Windows.SDK.NET` 引入）：

- `BluetoothLEDevice` → `GetGattServicesForUuidAsync(0x180F)`
  → `GetCharacteristicsForUuidAsync(0x2A19)`
- `ReadValueAsync()` 读一次；或 `WriteClientCharacteristicConfigurationDescriptorAsync(Notify)`
  + `ValueChanged` 事件订阅。
- 设备需已在 Windows 蓝牙设置里配对/连接（**配对与连接管理交给系统，本项目不做**，见 CLAUDE.md 明确不做事项）。

---

## 5. USB vs BLE 差异与运行时检测

> ⚠️ **下表的「两条独立路径」模型已被[实测更正](#-实测更正2026-06-08-真机校准权威)覆盖**:实测 USB 与蓝牙
> 是同一套 HID report 0x90,只差 VID。运行时检测见更正节(VID 0x05AC=USB / 0x004C=蓝牙,按 VID/PID 而非名字)。
> 以下保留原推断。

| 维度 | USB | BLE |
|---|---|---|
| VID | `0x05AC` | `0x004C`（HID over BT）/ 或经 GATT 直接走 BluetoothLEDevice |
| 电量承载 | HID feature report（Battery Strength usage） | GATT `0x2A19` characteristic |
| 主动上报 | **否，必须轮询 GET_REPORT** | **支持 Notify**（也可 Read） |
| 换算 | 可能需按 Logical Min/Max 换算（§3.1） | 直接 0–100，无换算 |
| 充电状态 | 直连即充电，`IsCharging = true` | 通常 `IsCharging = false` |
| 访问库 | `HidSharp` | WinRT `Windows.Devices.Bluetooth` |
| 访问层 | `IBatteryReader` 的 USB 实现 | `IBatteryReader` 的 BLE 实现 |

### 5.1 运行时检测与路径选择

对应 CLAUDE.md「BLE 实现和 USB 实现是两个独立 `IBatteryReader`，上层根据当前连接选择」：

1. **优先检测 USB**：用 HidSharp 枚举 HID 设备，按 §1 的 (VID, PID) 匹配。
   命中且能打开 → 走 USB reader，`Connection = Usb`。
2. **否则检测 BLE**：用 WinRT 枚举已连接的 BluetoothLEDevice，找暴露 `0x180F` 服务且名字/地址
   匹配 Magic 设备者 → 走 BLE reader，`Connection = Bluetooth`。
3. **都没有** → `Connection = Disconnected`。
4. **USB 拔出后切换**（验收场景）：USB reader 读取失败 + HID 设备消失 → 上层回退到 BLE 探测。
   切换应有去抖，避免拔插瞬间反复横跳。

> 同一台设备 USB 与 BLE 同时可用的情况：USB 优先（数据更直接、且在充电）。

映射到接口草案：

```text
DeviceConnection.Usb          ← HidSharp 命中 (VID,PID)
DeviceConnection.Bluetooth    ← WinRT 命中 0x180F + 设备匹配
DeviceConnection.Disconnected ← 两路都无
```

---

## 6. Magic Mouse 2 / Magic Keyboard 扩展字段（Phase 3 预留）

> 本节为 Phase 3 占位，**Phase 1/2 不实现**。结构与 MT2 一致，差异点如下。

| 设备 | USB (VID,PID) | USB 电量机制 | BLE 电量机制 |
|---|---|---|---|
| MM2 | `0x05AC` + `0x0269`/`0x0323` | 同 MT2：feature report + Battery Strength + descriptor fixup + 主动轮询（`is_usb_magicmouse2`） | GATT `0x180F`/`0x2A19` |
| MT2 | `0x05AC` + `0x0265`/`0x0324` | 同上（`is_usb_magictrackpad2`） | GATT `0x180F`/`0x2A19` |
| MK | `0x05AC` + 见 §1 键盘 PID 表 | **机制与 MM2/MT2 不同**：键盘走的是另一类 Apple HID 电量补丁（`HID: apple:` 系列，如指纹版 2021 的 USB 电量补丁），**不在 magicmouse 驱动内**，字段布局未确认（§8）。 | GATT `0x180F`/`0x2A19` |

要点：

- **MM2 与 MT2 在电量协议上完全同构**（同一驱动、同一 fixup、同一 usage），扩展 MM2 成本最低。
- **MK（键盘）走不同代码路径**（`hid-apple` 而非 `hid-magicmouse`），USB 电量字段需 Phase 3 单独以
  `HID: apple:` 系列补丁为参考整理，不能照搬本文档 USB 布局。BLE 路径则三者统一（标准 GATT）。
- 抽象层因此应让「设备型号 → USB 解析策略」可插拔；BLE 解析策略三型号共用。

---

## 7. 参考来源（References）

1. Linux 内核 `drivers/hid/hid-magicmouse.c`（torvalds/linux master）—— USB+BLE 解析与轮询主参考。
   <https://github.com/torvalds/linux/blob/master/drivers/hid/hid-magicmouse.c>
2. Linux 内核 `drivers/hid/hid-ids.h` —— VID/PID 常量来源。
   <https://github.com/torvalds/linux/blob/master/drivers/hid/hid-ids.h>
3. José Expósito, 2021/11《HID: magicmouse: Report battery level over USB》(Linux 5.16) ——
   USB 电量 descriptor fixup + 主动轮询机制。
   <https://lkml.iu.edu/hypermail/linux/kernel/2201.3/03107.html>
4. José Expósito, 2021/05《HID: magicmouse: Magic Mouse 2 USB battery capacity》—— USB 电量早期实现。
   <https://lkml.iu.edu/hypermail/linux/kernel/2105.1/04332.html>
5. Julius Lehmann, 2026/02《HID: magicmouse: fix battery reporting for Apple Magic Trackpad 2》——
   把 fixup 判定 `*rsize == 83` 放宽为 `*rsize >= 83`，修复新款 MT2 USB 读不到电量。
   <https://lkml.org/lkml/2026/2/14/420>
6. Bluetooth SIG — Battery Service `0x180F` / Battery Level characteristic `0x2A19`（标准 GATT）。
   <https://www.bluetooth.com/specifications/specs/battery-service/>

> 说明：来源 3/4/5 的 LKML 页面受 Anubis 反爬保护，部分内容经镜像/代理读取；
> 其中**报文内具体 report ID 数值、电量字节偏移、Logical Min/Max 未能从公开文本确证**，
> 故收敛到 §8 待实测，未在本文档臆造数值。

---

## 8. 待 Phase 1 实测确认（Known Unknowns）

以下项目**参考实现里靠运行时解析获得、未硬编码**，或公开文本无法确证。
Phase 1 必须实测并**录成 `tests/fixtures/` 数据**，确认前不得在实现中写死：

| # | 待确认项 | 状态 | 实测结论(2026-06-08) |
|---|---|---|---|
| U1 | 电量 **report ID 数值** | ✅ 已确认 | **`0x90`**(Input report,非 feature) |
| U2 | 电量字节**偏移与报文总长度** | ✅ 已确认 | 报文 **3 字节**;`byte[2]`=电量,`byte[1]`=充电标志 |
| U3 | 电量字段 **是否 raw==percent** | ✅ 已确认 | **是**,`byte[2]` 直读 0–100(2↔2%、3↔3%);`>100` 视为怪值 |
| U4 | Windows/HidSharp 解析是否可靠 | ✅ 已确认 | 厂商页(`06 00 FF`),Windows 不自动识别为电量;**改用 P/Invoke `HidD_GetInputReport` 按字节解析**(`HidD_GetFeature` 不通) |
| U5 | 睡眠/唤醒/满电怪值 | ⏳ 部分 | 已知低电(2%)正常;睡眠/满电的怪值仍需在那些状态下补录 |
| U6 | 设备识别方式 | ✅ 已确认 | **按 VID/PID**(0x05AC=USB、0x004C=蓝牙 + PID 0x0265);**不能靠名字**(实测本地化为「RZha的妙控板」) |
| U7 | MK(键盘)电量字段布局 | ✅ 旁证 | 实测 MK(004C/029C)同样暴露 `Input id 0x90 len 3`,**与 MT2 同构**;Phase 3 可复用 report 0x90 模型 |

> 录制的真机报文见 `tests/fixtures/report-0x90/`(`90 00 02` 蓝牙 2% / `90 03 03` USB 充电 3%)。
> 充电状态字段(`byte[1]`)的逐位精确含义、以及睡眠/满电怪值,留待后续在对应状态补录。

---

## 9. 对 Phase 1 接口的约束（衔接）

基于本规约，CLAUDE.md 中 `IBatteryReader` 草案的落地约束：

- `BatteryStatus.Percentage`：经 §3.1 换算并 clamp 到 [0,100] 后的整数。
- `BatteryStatus.IsCharging`：USB 路径默认 true，BLE 路径默认 false（§5）。
- `BatteryStatus.Connection`：按 §5.1 检测结果填 `Usb`/`Bluetooth`/`Disconnected`。
- USB reader 与 BLE reader 各自独立实现 `IBatteryReader`；所有 HID/BLE 原始调用过 mock 友好接口，
  解析逻辑（字节 → BatteryStatus）必须可脱离真机用 §8 录制的 fixture 单测。
- 读取层需明确表达「有效新值 / 无新值跳过 / 读取失败」三态（§3.2），不得把后两者降级成 0%。

---

*Phase 0 完成标准：本文档经 review 通过后方可进入 Phase 1。请重点 review §2、§3、§8 是否准确，
以及 §8 待实测项是否齐全。*
