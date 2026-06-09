# MagicBattery

English | [简体中文](README.md)

A Windows system-tray battery indicator for wireless devices: it shows the battery level of **Apple Magic Trackpad 2 / Magic Keyboard** (used with the [mac-precision-touchpad](https://github.com/imbushuo/mac-precision-touchpad) driver) and the **Nintendo Switch Pro Controller**. It lives in the system tray, shows multiple devices at once, with hover tooltips and low-battery alerts. (Magic Mouse 2 pending real-device calibration.)

> Pure user-mode: **no driver modification, no admin rights, no driver-signature bypass, read-only so it never interferes with the device**. Portable-first.

## Download

A prebuilt **portable single file** (self-contained .NET 8 runtime, double-click to run, no install) is on [**Releases**](https://github.com/EncoreQ/MagicBattery/releases/latest).
Requires Windows 10 21H2+ / Windows 11, with the mac-precision-touchpad driver installed (for the Magic devices).

## Status

All phases are complete. Every battery protocol is validated against real hardware (trackpad / keyboard / Switch Pro controller).

| Phase | Content | Status |
|---|---|---|
| Phase 0 | Protocol spec docs (`docs/protocol-spec.md`) | ✅ |
| Phase 1 | Battery reader library `MagicBattery.Hid` (pure library + unit tests) | ✅ |
| Phase 2 | WPF tray app `MagicBattery.Tray` | ✅ |
| Phase 3 | Low-battery alerts + multi-device + config persistence | ✅ |
| Phase 4 | Nintendo Switch Pro Controller support | ✅ |

## Features

- **Multi-device**: shows the trackpad + keyboard + Switch Pro controller together. The tray icon reflects the device with the **lowest battery**; the right-click menu lists every device as "name + battery + connection".
- **Two display modes**: Magic devices report an exact percentage (icon shows the number, color in 5 tiers); the controller only exposes 5 coarse levels (Full / High / Medium / Low / Critical), so its icon shows battery cells and the menu shows the level name — **no fabricated fake percentage**.
- A charging bolt is overlaid on the icon while charging. The hover tooltip lists each device's battery + connection + last-update time.
- **Low-battery alerts**: precise devices at 20% / 10% / 5%, coarse devices on entering "Low / Critical" — Toast notification (can be turned off in the menu), fires only while discharging and re-arms after recovery / charging.
- Right-click menu: Refresh now / Low-battery alerts toggle / Run at startup / Exit.
- Polls every 15 minutes by default; listens for device hot-plug (`WM_DEVICECHANGE`) so device add/remove and connection changes take effect within seconds.
- Run-at-startup uses the HKCU `Run` key (**no admin required**); other settings live in `%APPDATA%\MagicBattery\config.json`.

> Magic Mouse 2, USB-connected Switch controllers, and Joy-Cons are not yet included (not calibrated, or would require writing to the device); the code keeps extension points for them.

## Battery protocol (real-device calibrated)

**Magic devices** ([`docs/protocol-spec.md`](docs/protocol-spec.md), Trackpad 2 PID `0x0265` / Keyboard `0x029C`):
battery lives in HID **Input report `0x90`** (3 bytes), same mechanism over USB and Bluetooth, read via `HidD_GetInputReport`;
`byte[1]` = charging flag, `byte[2]` = battery percentage (read directly, 0–100). Connection is told apart by VID (`0x05AC` USB / `0x004C` Bluetooth).

**Switch Pro Controller** ([`docs/switch-pro-spec.md`](docs/switch-pro-spec.md), VID `0x057E` PID `0x2009`, Bluetooth):
standard full input report **`0x30`** (streamed at 60 Hz, read one frame); in `byte[2]`, `bit4` = charging and `bits 5-7` (`>>5`) = battery level 0–4.
**Read-only — no writes, no mode switching** — so it does not interfere with using the controller in games.

> All protocols are based on existing implementations (Linux kernel `hid-magicmouse.c` / `hid-nintendo.c`, dekuNukem's reverse engineering, the Bluetooth SIG, etc.) plus real-device calibration — **no blind packet sniffing**.

## Build & run

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# Build + test
dotnet build MagicBattery.sln -c Release
dotnet test  MagicBattery.sln -c Release

# Run the tray app directly
dotnet run --project src/MagicBattery.Tray -c Release

# Publish a self-contained single-file exe (portable, win-x64)
dotnet publish src/MagicBattery.Tray -p:PublishProfile=win-x64
# Output: src/MagicBattery.Tray/bin/Release/net8.0-windows/win-x64/publish/MagicBattery.exe
```

## Project layout

```
src/
  MagicBattery.Hid/     Battery reader layer (pure library, no UI dependency)
  MagicBattery.Tray/    WPF tray app
tests/
  MagicBattery.Hid.Tests/    Reader unit tests (recorded reports as fixtures)
  MagicBattery.Tray.Tests/   Tray core logic tests (polling/tiers/text/alerts/config/autostart)
  fixtures/                  Report bytes recorded from real devices
docs/
  protocol-spec.md      Magic-device battery protocol spec (with real-device corrections)
  switch-pro-spec.md    Switch Pro controller battery protocol spec
```

The reader layer is fully decoupled from the UI: all HID calls go through mock-friendly interfaces, and the parsing/orchestration logic is unit-tested — **it does not rely on "just plug in a device and see"**.

## Tech choices

| Area | Choice |
|---|---|
| UI framework | WPF (.NET 8) |
| Tray library | [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) |
| HID access | [HidSharp](https://github.com/IntergatedCircuits/HidSharp) enumeration + P/Invoke `HidD_GetInputReport` |
| Unit tests | xUnit + FluentAssertions |

## Non-goals

No re-implementing trackpad gestures (the driver handles those), no macOS version, no telemetry / auto-update, not shipping to the Microsoft Store.

## License

[MIT](LICENSE) © 2026 EncoreQ
