# LightBrightnessControl

[Chinese Documentation / 中文文档](README_CN.md)

A lightweight Windows monitor brightness control tool using the DDC/CI protocol, designed for minimal memory and CPU overhead.

---

## Motivation

Controlling external monitor brightness on Windows with physical OSD buttons is inconvenient, while Electron-based alternatives like [Twinkle Tray](https://github.com/xanderfrangos/twinkle-tray) typically consume 50–100 MB+ of RAM.

**LightBrightnessControl** is built with pure .NET 8 WinForms and direct Windows API P/Invoke (zero third-party dependencies), delivering essential multi-monitor hardware brightness features with minimal resource footprint.

---

## Features

- **Multi-monitor DDC/CI hardware control** — Direct communication with external monitors (no software dimming overlay)
- **Event-driven auto-refresh** — Automatically detects display topology changes, hot-plugging, monitor wake-up, and `DisplaySwitch` secondary screen toggles with zero background polling
- **Independent & synchronized modes** — Individual sliders control raw hardware DDC brightness directly; sync mode, hotkeys, and idle dimming apply per-monitor calibration curves
- **Multi-point brightness calibration curves** — Visual GDI+ curve editor + direct keyboard/mouse numeric table with live eye-matching preview, multi-monitor overlay comparison, and stable hardware monitor ID binding
- **Dynamic global hotkey presets** — 5 default presets (`0% / 25% / 50% / 75% / 100%`, bound by default to `Ctrl + Shift + D1` through `Ctrl + Shift + D5`), freely add or remove presets (`+` / `-`), automatically sorted from lowest to highest brightness
- **Idle auto-dimming** — Automatically dims screens after inactivity (optional fullscreen pause) and restores each monitor's previous brightness on return
- **System tray & Windows auto-start** — Silent tray operation with working-set memory trimming and automatic registry path sync across versioned EXE upgrades
- **Bilingual UI** — Instant switching between English and Chinese

---

## Performance

| Metric | Value |
|--------|-------|
| Background CPU Usage | ~0% when idle (pure event-driven, no monitor polling) |
| Calibration Runtime Cost | O(1) lookup via precomputed 101-byte LUT per monitor |
| Executable Size | ~200–260 KB (framework-dependent single-file EXE) |
| Third-Party Dependencies | None |

---

## System Requirements

- **OS**: Windows 10 / Windows 11
- **Runtime**: [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows x64)
- **Monitor**: DDC/CI compatible external monitor (ensure DDC/CI is enabled in your monitor's OSD settings)

> Note: Built-in laptop panels typically do not expose DDC/CI and are not controlled by this tool.

---

## Installation & Usage

1. Install **.NET 8.0 Desktop Runtime** if not already installed.
2. Download the latest release ZIP from the [Releases](https://github.com/TenminalKono/LightBrightnessControl/releases) page.
3. Extract and run `LightBrightnessControl_V2.2.exe` (single standalone executable; settings are stored in `settings.json` alongside the EXE).

### Highlights
- **Brightness Sliders**: Dragging an individual monitor slider sets its raw DDC brightness directly. Enable **Sync all monitors** to control all displays together using their calibration curves.
- **Curve Calibration**: Click **Calibration...** to open the multi-point curve editor. Drag nodes on the graph, double-click to add, right-click to remove, nudge with arrow keys, or type exact percentages in the anchor table. Enable **Live preview** to match multiple monitors by eye in real time.
- **Presets**: Use `+ Add Preset` / `-` to manage brightness presets; entries stay sorted from low to high brightness.

---

## Project Structure

```text
LightBrightnessControl/
├── Models/          # MonitorInfo, CalibrationCurve (LUT), AppSettings
├── Native/          # Direct Win32 / dxva2 P/Invoke declarations
├── Resources/       # Embedded app icon and bilingual LocalizationManager
├── Services/        # MonitorService, DisplayChangeWatcher, HotkeyManager, IdleWatcher, SettingsManager
├── UI/              # MainForm, CalibrationDialog, BrightnessCurveControl (GDI+)
├── scripts/         # One-click release packaging script (build-release.ps1)
├── Program.cs
└── LightBrightnessControl.csproj
```

---

## Building from Source

### Prerequisites
- .NET 8.0 SDK (or newer, e.g., .NET 10 SDK)

### Build & Package

```powershell
# Clone the repository
git clone https://github.com/TenminalKono/LightBrightnessControl.git
cd LightBrightnessControl

# Build Release
dotnet build -c Release

# One-click framework-dependent single-file publish + ZIP (outputs to dist/)
.\scripts\build-release.ps1
```

Or run `dotnet publish` directly:

```powershell
dotnet publish -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true
```

---

## License

This project is licensed under the **MIT License** — see [LICENSE](LICENSE) for details.

---

## Acknowledgments

Inspired by [Twinkle Tray](https://github.com/xanderfrangos/twinkle-tray).

---

## Version History

- **v2.2**
  - Dynamic brightness presets: default 5 presets, freely add/remove via `+` / `-`, auto-sorted low-to-high
  - Multi-point nonlinear brightness calibration: pure GDI+ interactive curve + direct keyboard/mouse numeric table + live preview + 3+ monitor overlay support + 101-byte LUT runtime evaluation
  - Event-driven automatic monitor refresh (`WM_DISPLAYCHANGE`, `WM_DEVICECHANGE`, `DisplaySettingsChanged`, display wake-up) with debounced async retry and lazy DDC recovery — zero background polling
  - Stable physical monitor hardware ID binding (`EnumDisplayDevices`), independent raw DDC slider control, embedded icon for true single-file EXE, versioned executable (`LightBrightnessControl_V2.2.exe`), and auto-start path self-healing
  - Modularized repository structure and one-click release script
- **v2.1**
  - Improved monitor detection stability and UI refinements
- **v2.0**
  - Complete rewrite with pure-code WinForms UI, bilingual support, and minimal resource usage
