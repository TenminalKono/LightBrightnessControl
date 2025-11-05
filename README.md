# LightBrightnessControl
Ultra-lightweight brightness control tool for external monitors on Windows

## Features

- **Minimal Memory Footprint** - Less than 5MB RAM usage during runtime
- **Single Purpose** - Does one thing well: adjusts monitor brightness
- **Portable** - Download and run, single EXE file, no installation required
- **Multi-Monitor Support** - Control multiple external displays simultaneously
- **System Tray Integration** - Near-zero CPU usage when minimized
- **Auto-Start Option** - Optional startup with Windows

## System Requirements

- Windows 7 or later
- .NET Framework 4.7.2 or later (pre-installed on Windows 10/11)
- External monitors with DDC/CI support

## Usage

1. Download the latest `LightBrightnessControl.exe` from [Releases](../../releases)
2. Run the executable
3. The application minimizes to the system tray (bottom-right corner)
4. Double-click the tray icon to open the main window
5. Use sliders to adjust brightness for each monitor (0-100%)
6. Optional: Enable "Auto-Start" for startup with Windows

## Important Notes

- This software only works with **external monitors**, not laptop built-in displays
- Monitors must support DDC/CI protocol (most modern monitors do)
- If monitors are not detected, enable DDC/CI in your monitor's OSD menu
- First-time auto-start setup may require administrator privileges

## Building from Source

### Prerequisites

- Visual Studio 2019 or later
- .NET Framework 4.7.2 SDK

### Build Instructions

```bash
# Clone the repository
git clone https://github.com/yourusername/light-brightness-control.git

# Navigate to project directory
cd light-brightness-control

# Open LightBrightnessControl.sln in Visual Studio
# Or build via command line:
msbuild LightBrightnessControl.sln /p:Configuration=Release
```

The compiled EXE will be located in the `bin/Release/` directory

## Technical Details

- Built with WinForms, zero third-party dependencies
- Direct hardware control via DDC/CI protocol (dxva2.dll)
- Auto-start managed through Windows Registry
- Minimizes to system tray for reduced resource consumption

## License

MIT License - see [LICENSE](LICENSE) file for details

## Acknowledgments

Inspired by [Twinkle Tray](https://github.com/xanderfrangos/twinkle-tray), this project focuses on providing an even lighter alternative.

---

[中文文档](README_CN.md)
