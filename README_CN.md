# LightBrightnessControl

[English Documentation / 英文文档](README.md)

极致轻量的 Windows 显示器亮度控制工具，通过 DDC/CI 协议直接调节外接显示器硬件亮度，专注于尽可能低的内存与 CPU 开销。

---

## 开发动机

在 Windows 上通过显示器物理按键调节亮度很不方便，而基于 Electron 的同类工具（如 [Twinkle Tray](https://github.com/xanderfrangos/twinkle-tray)）往往占用 50–100 MB 以上内存。

**LightBrightnessControl** 采用纯 .NET 8 WinForms 与直接 Windows API P/Invoke 编写（零第三方依赖），在保持极致轻量的同时提供完善的多屏硬件亮度控制能力。

---

## 功能特性

- **多显示器 DDC/CI 硬件控制** — 直接调节显示器真实硬件背光亮度，而非软件遮罩层
- **事件驱动自动刷新显示器状态** — 监听显示拓扑变更、设备插拔、屏幕唤醒及 `DisplaySwitch` 切屏，带异步防抖延迟重试与失败懒恢复，后台 **0 轮询开销**
- **独立与同步双模式** — 单独拖动某块屏滑条直接控制其真实 DDC 亮度；同步调整、快捷键预设与闲置变暗自动应用多点校准曲线
- **多点非线性亮度曲线校准** — 轻量 GDI+ 可视化曲线编辑器 + 键盘/鼠标直接数值编辑表，支持实时肉眼对齐预览、多屏曲线叠加对比与硬件 ID 稳定绑定
- **动态全局快捷键预设** — 默认 5 档（`0% / 25% / 50% / 75% / 100%`，默认快捷键 `Ctrl + Shift + D1` 至 `Ctrl + Shift + D5`），支持 `+` / `-` 自由增删，自动按低亮度在上、高亮度在下排序
- **闲置自动变暗** — 系统闲置后自动降亮（支持全屏暂停），恢复操作后按各显示器原亮度精准还原
- **系统托盘与开机自启** — 隐藏到托盘时自动回收工作集内存；带版本号升级 EXE 时自动更新开机自启路径
- **中英双语界面** — 支持一键切换中文与英文

---

## 性能表现

| 指标 | 数值 |
|------|------|
| 后台闲置 CPU 占用 | 接近 0%（纯事件驱动，无显示器轮询定时器） |
| 曲线校准运行开销 | 每台显示器预计算 101 字节查找表（LUT），O(1) 查表 |
| 单文件可执行体积 | 约 200–260 KB（框架依赖单文件版） |
| 第三方依赖数量 | 0 |

---

## 系统要求

- **操作系统**：Windows 10 / Windows 11
- **运行环境**：[.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（Windows x64）
- **显示器**：支持 DDC/CI 的外接显示器（请确认显示器 OSD 菜单中已开启 DDC/CI）

> 注意：笔记本内置屏幕通常不支持 DDC/CI，无法通过本工具控制。

---

## 安装与使用

1. 如尚未安装，请先安装 **.NET 8.0 Desktop Runtime**（Windows 桌面运行时）。
2. 从 [Releases](https://github.com/TenminalKono/LightBrightnessControl/releases) 页面下载最新压缩包。
3. 解压并直接运行 `LightBrightnessControl_V2.2.exe`（真正单文件，无需附带外部图标；配置文件 `settings.json` 自动生成在同目录）。

### 核心功能说明
- **亮度滑条**：未勾选同步时，拖动单台显示器滑条直接下发物理 DDC 亮度；勾选**同步调整所有显示器**后，将通过各显示器的校准曲线同步联动。
- **多点曲线校准**：点击**曲线校准...**进入编辑器，可在画布上拖拽锚点、双击添点、右键删点或用方向键微调，也可在右侧锚点表和下方输入框直接用键盘输入精确数值。勾选**实时预览**可边调边肉眼对齐多台屏幕（支持 2 屏、3 屏及以上叠加对比）。
- **动态预设**：通过 `+ 添加预设` 和每行右侧 `-` 自由增删预设，失焦或回车后自动按低亮度到高亮度排序。

---

## 项目目录结构

```text
LightBrightnessControl/
├── Models/          # 数据模型：MonitorInfo、CalibrationCurve (LUT)、AppSettings
├── Native/          # Windows API (user32 / dxva2 / kernel32) P/Invoke 声明
├── Resources/       # 内嵌应用图标与双语 LocalizationManager
├── Services/        # MonitorService、DisplayChangeWatcher、HotkeyManager、IdleWatcher、SettingsManager
├── UI/              # MainForm、CalibrationDialog、BrightnessCurveControl (GDI+)
├── scripts/         # 一键打包脚本 (build-release.ps1)
├── Program.cs
└── LightBrightnessControl.csproj
```

---

## 从源码构建

### 前置要求
- .NET 8.0 SDK（或更高版本 SDK）

### 编译与一键打包

```powershell
# 克隆仓库
git clone https://github.com/TenminalKono/LightBrightnessControl.git
cd LightBrightnessControl

# Release 编译
dotnet build -c Release

# 一键打包框架依赖单文件版及 ZIP（输出至 dist/ 目录）
.\scripts\build-release.ps1
```

也可直接运行 `dotnet publish`：

```powershell
dotnet publish -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true
```

---

## 许可证

本项目采用 **MIT 许可证** — 详情请参阅 [LICENSE](LICENSE) 文件。

---

## 致谢

灵感源自 [Twinkle Tray](https://github.com/xanderfrangos/twinkle-tray)。

---

## 版本历史

- **v2.2**
  - 动态预设亮度：默认 5 档预设，支持 `+` / `-` 自由增删，自动按低亮度在上、高亮度在下排序
  - 多点非线性亮度曲线校准：原生 GDI+ 可视化曲线 + 键盘/鼠标直接数值表 + 实时肉眼对齐预览 + 多屏（3 屏+）曲线叠加对比 + 101 字节 LUT 极速查表
  - 纯事件驱动自动刷新显示器状态（监听 `WM_DISPLAYCHANGE`、`WM_DEVICECHANGE`、`DisplaySettingsChanged` 与屏幕唤醒），支持防抖异步延迟重试与失败懒恢复，零后台轮询开销
  - 稳定硬件 ID 绑定校准曲线（兼容自动迁移旧版比例设置）、独立滑条直控真实 DDC 亮度、图标内嵌真正单文件化、带版本号可执行文件名（`LightBrightnessControl_V2.2.exe`）及开机自启路径自动同步
  - 工程目录规范化整理（单一代码源 + 分层目录 + 一键打包脚本）
- **v2.1**
  - 优化显示器识别稳定性与界面交互细节
- **v2.0**
  - 完全重写，纯代码构建 WinForms UI，中英双语支持，极致轻量化
