# LightBrightnessControl

[English Documentation / 英文文档](README.md)

轻量级 Windows 显示器亮度控制工具，使用 DDC/CI 协议直接控制显示器硬件亮度。

---

## 开发动机

在 Windows 上调整外接显示器的亮度通常很不方便 - 你需要使用每个显示器上的物理按钮。虽然 [Twinkle Tray](https://github.com/xanderfrangos/twinkle-tray) 是一个出色的解决方案，但由于其基于 Electron 架构，内存占用较高（50-100MB+）。

**LightBrightnessControl** 作为一个轻量级替代方案而诞生，专注于最小化资源占用，同时提供必要的亮度控制功能。

---

## 功能特性

- **多显示器支持** - 自动检测所有连接的显示器
- **DDC/CI 直接通信** - 控制真实硬件亮度，而非软件遮罩层
- **同步调整** - 通过单个滑条控制所有显示器
- **全局快捷键** - 4组可自定义快捷键，快速切换预设亮度
- **闲置自动变暗** - 系统闲置一段时间后自动降低亮度
- **系统托盘集成** - 后台静默运行
- **开机自启动** - 可选通过注册表随 Windows 启动
- **双语界面** - 支持英文和中文

---

## 性能表现

| 指标 | 数值 |
|------|------|
| 内存占用 | 小于 2.5 MB |
| 可执行文件大小 | 约 150-200 KB（框架依赖版） |
| CPU 占用 | 闲置时接近于零 |

---

## 系统要求

- **操作系统**：Windows 10 / Windows 11
- **运行时**：.NET 8.0 运行时或更高版本
- **显示器**：支持 DDC/CI 的外接显示器（大多数现代显示器都支持）

注意：笔记本内置屏幕通常不支持 DDC/CI，无法通过本软件控制。

---

## 安装说明

### 第一步：安装 .NET 8.0 运行时

如果您尚未安装 .NET 8.0，请从微软官网下载：

[下载 .NET 8.0 运行时](https://dotnet.microsoft.com/download/dotnet/8.0)

选择 **".NET Desktop Runtime"** Windows 版本。

### 第二步：下载 LightBrightnessControl

从 [Releases](https://github.com/TenminalKono/LightBrightnessControl/releases) 页面下载最新版本。

### 第三步：解压并运行

1. 将下载的 ZIP 文件解压到您喜欢的位置
2. 运行 `LightBrightnessControl.exe`
3. 程序将出现在系统托盘中

---

## 使用方法

1. **调整亮度**：使用滑条调整每个显示器的亮度
2. **同步模式**：勾选"同步调整所有显示器"以统一控制所有显示器
3. **快捷键**：配置最多4个全局快捷键用于预设亮度
4. **闲置变暗**：启用系统闲置时自动变暗功能
5. **开机自启**：启用"开机自动启动"以在启动时自动运行
6. **语言切换**：通过下拉菜单切换英文和中文界面

---

## 从源码构建

### 前置要求

- Visual Studio 2022 或更高版本
- .NET 8.0 SDK

### 构建步骤

```bash
# 克隆仓库
git clone https://github.com/TenminalKono/LightBrightnessControl.git
cd LightBrightnessControl

# 构建 Release 版本
dotnet build -c Release

# 或发布为单文件
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

---

## 许可证

本项目采用 **MIT 许可证** - 详情请参阅 [LICENSE](LICENSE) 文件。

**使用、修改或分发本软件时，您必须遵守 MIT 许可证的条款。**

---

## 致谢

特别感谢 [Twinkle Tray](https://github.com/xanderfrangos/twinkle-tray) 项目提供的灵感。Twinkle Tray 是一款功能丰富的亮度控制应用程序，是创建这个轻量级替代方案的动力来源。

---

## 贡献

欢迎贡献代码！请随时提交 Issue 或 Pull Request。

---

## 版本历史

- **v2.0** - 完全重写，纯代码构建 UI，双语支持，极致轻量化
