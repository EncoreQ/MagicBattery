# 测试 fixtures

每条数据是一份录制的原始 report 字节,以**十六进制文本**(`.hex`)存放,
loader 会去掉空白后按字节解析。每条都注明来源(哪台设备、什么状态)。

## 目录

- **`report-0x90/`** — Magic 设备的 HID Input report 0x90(3 字节),USB/蓝牙通用。
  正常用例为**真机录制**(2026-06-08,Magic Trackpad 2),另含一条合成怪值。
  详见该目录 README 与 `docs/protocol-spec.md` 顶部「实测更正」。

> 历史:早期 `usb/` 下有一批基于错误假设构造的 SYNTHETIC 占位数据(电量误放 byte[1]、
> 取法误为 GetFeature),已在 Phase 1 重构时随旧代码一并删除。
