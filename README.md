# LightBrightnessControl

[Chinese Documentation / 中文文档](README_CN.md)

A lightweight Windows monitor brightness control tool using DDC/CI protocol.

---

## Motivation

Controlling external monitor brightness on Windows can be inconvenient - you typically need to use the physical buttons on each monitor. While [Twinkle Tray](https://github.com/xanderfrangos/twinkle-tray) is an excellent solution for this problem, it can consume a significant amount of RAM (50-100MB+) due to its Electron-based architecture.

**LightBrightnessControl** was created as a lightweight alternative that focuses on minimal resource usage while providing the essential brightness control features.

---

## Features

- **Multi-monitor support** - Automatically detects all connected monitors
- **DDC/CI direct communication** - Controls actual hardware brightness, not software overlay
- **Synchronized adjustment** - Control all monitors with a single slider
- **Global hotkeys** - 4 customizable hotkey presets for quick brightness switching
- **Idle dimming** - Automatically dims screen after a period of inactivity
- **System tray integration** - Runs silently in the background
- **Auto-start with Windows** - Optional startup with Windows via registry
- **Bilingual interface** - Supports both English and Chinese

---

## Performance

| Metric | Value |
|--------|-------|
| RAM Usage | Less than 2.5 MB |
| Executable Size | Approximately 150-200 KB (framework-dependent) |
| CPU Usage | Near zero when idle |

---

## System Requirements

- **Operating System**: Windows 10 / Windows 11
- **Runtime**: .NET 8.0 Runtime or later
- **Monitor**: DDC/CI compatible external monitor (most modern monitors support this)

Note: Laptop built-in displays typically do not support DDC/CI and cannot be controlled by this software.

---

## Installation

### Step 1: Install .NET 8.0 Runtime

If you don't have .NET 8.0 installed, download it from the official Microsoft website:

[Download .NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

Select **".NET Desktop Runtime"** for Windows.

### Step 2: Download LightBrightnessControl

Download the latest release from the [Releases](https://github.com/TenminalKono/LightBrightnessControl/releases) page.

### Step 3: Extract and Run

1. Extract the downloaded ZIP file to your preferred location
2. Run `LightBrightnessControl.exe`
3. The application will appear in your system tray

---

## Usage

1. **Adjust Brightness**: Use the slider to adjust each monitor's brightness
2. **Sync Mode**: Check "Sync all monitors" to control all monitors together
3. **Hotkeys**: Configure up to 4 global hotkeys for preset brightness levels
4. **Idle Dimming**: Enable automatic dimming when the system is idle
5. **Auto-start**: Enable "Start with Windows" to launch automatically on boot
6. **Language**: Switch between English and Chinese using the dropdown menu

---

## Building from Source

### Prerequisites

- Visual Studio 2022 or later
- .NET 8.0 SDK

### Build Steps

```bash
# Clone the repository
git clone https://github.com/TenminalKono/LightBrightnessControl.git
cd LightBrightnessControl

# Build Release version
dotnet build -c Release

# Or publish as single file
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

---

## License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

**You must comply with the MIT License when using, modifying, or distributing this software.**

---

## Acknowledgments

Special thanks to the [Twinkle Tray](https://github.com/xanderfrangos/twinkle-tray) project for providing inspiration. Twinkle Tray is a feature-rich brightness control application that served as the motivation for creating this lightweight alternative.

---

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

---

## Version History

- **v2.0** - Complete rewrite with pure-code UI, bilingual support, and minimal resource usage
