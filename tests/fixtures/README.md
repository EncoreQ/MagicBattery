# 测试 fixtures

每条数据是一份录制的原始 report 字节,以**十六进制文本**(`.hex`)存放,
loader 会去掉空白后按字节解析。每条都注明来源(哪台设备、什么状态)。

## 目录

- **`report-0x90/`** — ✅ **真机录制**(2026-06-08,Magic Trackpad 2)。USB/蓝牙通用的
  HID Input report 0x90。详见该目录 README 与 `docs/protocol-spec.md` 顶部「实测更正」。
- **`usb/`** — ⚠️ 旧的 **SYNTHETIC** 占位数据(下文)。真机校准已证明其布局假设有误
  (电量在 byte[2] 不是 byte[1]、取法是 GetInputReport 不是 GetFeature)。
  暂留用于驱动现版本测试,**Phase 1 重构时替换/删除**。

## ⚠️ 当前全部为 SYNTHETIC(合成,非真机)

Phase 1 阶段没有真机。下列数据是依据 `docs/protocol-spec.md` §2/§8 的**假设布局**
(`UsbBatteryReportLayout.MagicTrackpad2Synthetic`:ReportId=0x90、长度 3、电量在
byte[1]、Logical 0..100)**人工构造**的占位数据,**不是从设备抓取**。

拿到真机后必须:
1. 按 spec §8 U1/U2/U3 dump 出真实 report id / 偏移 / Logical Min-Max;
2. 用真实报文替换下列 `.hex` 文件,并改 `source` 为真实设备 + 状态;
3. 校正 `MagicTrackpad2Synthetic` 常量。

## usb/

| 文件 | 字节 | 含义 | source |
|---|---|---|---|
| `mt2_50pct.hex` | `90 32 00` | 电量 50%(0x32=50) | SYNTHETIC |
| `mt2_full.hex` | `90 64 00` | 满电 100%(0x64=100) | SYNTHETIC |
| `mt2_oob.hex` | `90 C8 00` | 越界怪值(0xC8=200 > Logical Max 100),应判为 Unavailable | SYNTHETIC |
