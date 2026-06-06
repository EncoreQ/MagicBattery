# MagicBattery 电量协议规约（Protocol Spec）

> Phase 0 产出物。本文档**只整理协议**，不含任何实现代码。
> 所有内容以 CLAUDE.md「协议参考」中列出的已有实现为准，**未做任何自行抓包逆向**。
> 凡是无法从参考实现确证、必须实测确认的字段，统一收敛到文末
> [§8 待 Phase 1 实测确认](#8-待-phase-1-实测确认known-unknowns)，请勿在实现中凭印象硬编码。

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

| # | 待确认项 | 为什么不确定 | 实测方法 |
|---|---|---|---|
| U1 | USB 电量 feature **report ID 数值** | 内核运行时从 descriptor 解析 `hdev->battery.report_id`，不硬编码 | HidSharp 枚举 feature report / dump descriptor，找 Battery Strength usage 所在 report ID |
| U2 | USB 电量字节**偏移与报文总长度** | descriptor 有缺陷需 fixup，公开文本无字节图 | 对 U1 的 report ID 发 GetFeature，比对不同电量下变化的字节 |
| U3 | USB 电量字段 **Logical Min/Max**（决定是否 raw==percent） | 公开文本未给 | dump descriptor 读该 usage 的 Logical Min/Max |
| U4 | descriptor 缺陷在 **Windows/HidSharp** 下是否影响解析 | Windows 不做内核那套 fixup | 实测 HidSharp 解析出的 report 元数据是否正确，决定 §2.3 双轨策略走哪条 |
| U5 | 睡眠/唤醒/满电时的**具体怪值**（0？陈旧值？255？） | §3.2 仅给方向，无确切值 | 录制各状态报文做 fixture |
| U6 | BLE 路径设备**识别方式**（按名称？地址段？） | 取决于 Windows 上设备如何呈现 | WinRT 枚举实测 |
| U7 | MK（键盘）USB 电量字段布局 | 走 `hid-apple` 另一套补丁，Phase 3 才需要 | Phase 3 以 `HID: apple:` 补丁为参考再整理 |

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
