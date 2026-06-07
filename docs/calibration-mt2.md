# Magic Trackpad 2 真机校准 Runbook

> 目的:用一台真实 Magic Trackpad 2,录一次真实的 USB / BLE 电量报文,
> 把 Phase 1 留下的两张 **SYNTHETIC 欠条**还掉:
> 1. `UsbBatteryReportLayout.MagicTrackpad2Synthetic` 的占位常量(report id / 偏移 / Logical Min-Max);
> 2. `tests/fixtures/usb/*.hex` 的合成数据。
>
> 对应 `docs/protocol-spec.md` §8 的 U1–U6。**全程不需要管理员权限、不改驱动**。
> 设备到手后照本清单走一遍即可,不必重读协议。

---

## 0. 前置

- 一台 Magic Trackpad 2(记下是 Lightning `0x0265` 还是 USB-C `0x0324` 版本)。
- 一根能传数据的线(USB 录制用)。
- 已装 mac-precision-touchpad 驱动(触控正常即可)。
- **Ground truth 电量**从哪看:macOS 蓝牙菜单、或 iPad、或 Magic Utilities 试用版。
  录每条数据时都记下当时的"真实百分比",作为校验基准。

建议至少覆盖这几个**电量/状态点**(§8 U5),每个点 USB、BLE 各录一次:

| 状态 | 为什么要 |
|---|---|
| 中间值(~40–60%) | 主测试基准 |
| 高/满电(>95%,接线充电) | 验证满电与 IsCharging |
| 低电(<15%) | Phase 3 告警阈值附近 |
| 刚从睡眠唤醒(静置几分钟后动一下立刻读) | 录 §3.2 的怪值,确认是 0 / 陈旧值 / 越界 |

---

## 1. USB 录制

### 1.1 拿到真实原始 report descriptor(§8 U1/U2/U3)

电量在某个 feature report 里,report id / 偏移 / Logical Min-Max 必须从描述符读出。

**首选:USB Device Viewer(UsbView)** —— Windows SDK 自带,免费,显示**设备真实**
report descriptor 原始字节。

1. 插 USB,打开 UsbView,找到 Apple 设备(VID `05AC`)。
2. 展开各 HID 接口(会有 Device Management / Trackpad / ... 多个),
   找含 **Battery Strength**(Usage Page `06` Generic Device Controls,Usage `20`)的那个 feature report。
3. 记下:
   - 该 feature report 的 **Report ID**(描述符里 `0x85` item 后面那个字节);
   - Battery Strength 字段在报文中的**字节偏移**;
   - 该字段的 **Logical Minimum / Logical Maximum**;
   - 整个 feature report 的**总字节数**。

> ⚠️ 不要只用 HidSharp 的 `GetRawReportDescriptor()` 当真值:在 Windows 上它是从
> HIDP 预解析数据**重建**的,看不到 Phase 0 §2.2 那个 Apple 的 fixup 怪字节,
> 偏移可能和设备真实描述符差一位。HidSharp 的解析结果可作交叉验证,**基准以 UsbView 为准**。
> 若有 Linux 双系统,`sudo usbhid-dump -d 05ac: ` 能直接拿到最干净的原始描述符。

### 1.2 录真实 feature report 字节(各电量点)

对 1.1 找到的 Report ID 发一次 HID Get Feature,把返回字节抄成 hex。两种方式任选:

- **方式 A(推荐,可复用本仓库):** 用一个 dump 小工具调
  `UsbHidDeviceEnumerator.TryOpenFirst()` + `IUsbHidConnection.GetFeatureReport(reportId, length)`,
  打印 hex。(这个工具尚未建,见本文末「可选工具」。)
- **方式 B:** 任意 HID 工具(如 `hidapitester --get-feature-report`)对该 report id 取 feature。

每个电量点记录一行:`<hex 字节>  | 真实% = NN  | 状态`。

---

## 2. BLE 录制

BLE 走标准 GATT,值直接 0–100,基本不需要"校准",但要确认 Apple 设备的真实行为(§8 U6)。

1. Windows 蓝牙设置里**配对**好 Magic Trackpad 2(配对交给系统,本项目不做)。
2. 用 **Bluetooth LE Explorer**(Microsoft Store 免费)或 nRF Connect:
   - 找到设备,读 Service `0x180F` → Characteristic `0x2A19`;
   - 记下读到的**单字节值**,和当时真实% 对照(应当一致或差 1);
   - 试**订阅 notify**,看 Apple 设备是否真的推送、隔多久推一次。
3. 记录:设备在 Windows 里的**名称/地址**长什么样(给 `BleDeviceLocator` 的识别用)。

---

## 3. 把数据喂回代码

按下列**精确位置**改,改完不动解析逻辑与测试结构,只换数据:

### 3.1 改 layout 常量

文件 `src/MagicBattery.Hid/Usb/UsbBatteryReportLayout.cs`,
把 `MagicTrackpad2Synthetic` 的占位值换成 1.1 实测值,并把 XML 注释里的 `SYNTHETIC`
改成真机来源(设备型号 + 固件/序列号 + 日期)。建议顺手把字段名从 `...Synthetic`
改为 `MagicTrackpad2`(同步改引用处:`MagicBatteryReaderFactory.cs`)。

### 3.2 换 fixtures

`tests/fixtures/usb/*.hex`:用 1.2 录的真实字节替换三条占位数据
(中间值 / 满电 / 一条怪值)。同步更新 `tests/fixtures/README.md` 的表格,
把 `source` 列从 `SYNTHETIC` 改成真实设备 + 状态,并删掉顶部的「全部为 SYNTHETIC」警告。

### 3.3 对齐测试期望值

`tests/MagicBattery.Hid.Tests/UsbBatteryParserTests.cs` 里的断言期望值
(如 `Be(50)`、`Be(100)`)按真实数据改。若真机 Logical 范围不是 0..100,
`Parse_scales_by_logical_min_max` 的用例也据实调整。

### 3.4 (可选)补 BLE fixture / 识别规则

- 若想给 BLE 也留固定回归数据,可加 `tests/fixtures/ble/mt2_level.hex`(单字节)。
- 按第 2 步记录的名称/地址,收紧 `BleDeviceLocator.LooksLikeMagicDevice` 的识别(§5.1)。

---

## 4. 回归 + 真机冒烟

1. `dotnet test` —— 换数据后必须仍全绿(解析逻辑没动,只是数据与期望同步)。
2. **真机冒烟**(本阶段第一次真跑硬件):
   - USB 插上 → `MagicBatteryReaderFactory.CreateAsync` 应返回 `UsbBatteryReader`,
     `ReadAsync` 读到的 % 与 ground truth 一致;
   - 拔 USB、走蓝牙 → 应返回 `BleBatteryReader`,读数一致;
   - 验证 spec 验收场景:USB 拔出后切到 BLE、睡眠唤醒后能恢复读取。
3. 把冒烟结果(各状态读数 vs 真实%)记一笔,作为校准完成证据。

---

## 5. 校准完成 Checklist

- [ ] 记录设备版本(Lightning `0x0265` / USB-C `0x0324`)
- [ ] UsbView dump 到真实 report descriptor,确认 Report ID / 偏移 / Logical Min-Max / 长度
- [ ] 录到中间值 / 满电 / 低电 / 唤醒怪值 四个点的 USB 报文
- [ ] BLE 读到 `0x2A19`,确认值域与 notify 行为,记录设备名称/地址
- [ ] `UsbBatteryReportLayout` 占位常量已替换为真机值并重命名,注释标真实来源
- [ ] `tests/fixtures/usb/*.hex` + README 已换真机数据,移除 SYNTHETIC 警告
- [ ] 测试期望值已对齐,`dotnet test` 全绿
- [ ] 真机冒烟通过(USB / BLE / 切换 / 睡眠唤醒),读数与 ground truth 一致
- [ ] protocol-spec.md §8 的 U1–U6 标记为已确认(或回填真实数值)

---

## 可选工具:dump 小程序

第 1.2 步要反复对设备发 Get Feature 并打印 hex,手工不便。可建一个**仅供开发**的
控制台小工具 `tools/MagicBattery.Dump`(只依赖已有的 HidSharp + MagicBattery.Hid,
**不引入新依赖**),功能:枚举匹配 (VID,PID) 的设备 → 打印 `GetRawReportDescriptor()`
重建的描述符 → 对候选 report id 发 GetFeature → 输出可直接粘进 `.hex` 的字节。

是否建这个工具单独决定(它不在既定 Phase 列表内,属校准辅助)。没有它也能用方式 B 完成录制。
