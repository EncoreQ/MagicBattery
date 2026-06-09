# switch-pro 真机录制数据

Switch Pro Controller 标准完整输入报文 `0x30`(蓝牙,~60Hz 流式)。这里只保留电量解析相关的前几字节。

字节布局(见 `docs/switch-pro-spec.md`):`byte[0]`=0x30,`byte[1]`=timer,
`byte[2]`=bat_con(bit0 外部供电 / bit4 充电 / bits5-7 电量档 0..4)。

| 文件 | 字节 | 含义 | source |
|---|---|---|---|
| `pro_bt_high.hex` | `30 4C 60 00 00 00` | byte[2]=0x60 → `0x60>>5=3`=高档、未充电(bit4=0)、无外部供电 | 真机:Switch Pro Controller(序列号 483177508f6a),蓝牙,近乎全满,2026-06-09 |

> 完整报文 362 字节(蓝牙),其余为按键/摇杆/IMU 数据,与电量无关,未保留。
