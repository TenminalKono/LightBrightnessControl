# Light Brightness Control

极致轻量的 Windows 外接显示器亮度调节工具

## 特点

- **极低内存占用** - 运行时仅占用 少于5MB的内存
- **功能专注** - 只做亮度调节，没有任何多余功能
- **免安装** - 下载即用，单个 EXE 文件
- **多显示器支持** - 同时控制多个外接显示器
- **系统托盘运行** - 最小化后几乎零 CPU 占用
- **开机自启动** - 可选的开机自动启动功能

## 系统要求

- Windows 7 或更高版本
- .NET Framework 4.7.2 或更高版本（Windows 10/11 已内置）
- 支持 DDC/CI 协议的外接显示器

## 使用方法

1. 从 [Releases](../../releases) 页面下载最新版本的 `LightBrightnessControl.exe`
2. 双击运行程序
3. 程序将自动最小化到系统托盘（任务栏右下角）
4. 双击托盘图标打开主界面
5. 使用滑条调节各个显示器的亮度（0-100%）
6. 可选：勾选"开机自启动"选项

## 注意事项

- 本软件仅支持**外接显示器**，不支持笔记本内置屏幕
- 显示器必须支持 DDC/CI 协议（大多数现代显示器都支持）
- 如果检测不到显示器，请在显示器 OSD 菜单中启用 DDC/CI 选项
- 首次设置开机自启动时，可能需要以管理员权限运行

## 构建方法

### 环境要求

- Visual Studio 2019 或更高版本
- .NET Framework 4.7.2 SDK

### 构建步骤

```bash
# 克隆仓库
git clone https://github.com/yourusername/light-brightness-control.git

# 打开项目
cd light-brightness-control

# 使用 Visual Studio 打开 LightBrightnessControl.sln
# 或使用命令行构建：
msbuild LightBrightnessControl.sln /p:Configuration=Release
```

生成的 EXE 文件位于 `bin/Release/` 目录

## 技术细节

- 使用 WinForms 构建，无第三方依赖
- 通过 DDC/CI 协议（dxva2.dll）直接控制显示器硬件
- 使用 Windows 注册表管理开机自启动
- 最小化到系统托盘以减少资源占用

## 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

## 致谢

本项目受 [Twinkle Tray](https://github.com/xanderfrangos/twinkle-tray) 启发，专注于提供更轻量化的解决方案。
