using System.Text.Json;
using Microsoft.Win32;

namespace LightBrightnessControl;

/// <summary>
/// 应用设置数据类
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// 是否同步调整所有显示器
    /// </summary>
    public bool SyncBrightness { get; set; }

    /// <summary>
    /// 各显示器的亮度比例 (DeviceName -> Ratio)
    /// </summary>
    public Dictionary<string, double> MonitorRatios { get; set; } = new();

    /// <summary>
    /// 热键配置 (BrightnessValue -> KeyCombo)
    /// </summary>
    public List<HotkeySettingItem> Hotkeys { get; set; } = new()
    {
        new() { BrightnessValue = 0, Modifiers = 0, Key = 0 },
        new() { BrightnessValue = 30, Modifiers = 0, Key = 0 },
        new() { BrightnessValue = 60, Modifiers = 0, Key = 0 },
        new() { BrightnessValue = 100, Modifiers = 0, Key = 0 }
    };

    /// <summary>
    /// 是否开启闲置变暗
    /// </summary>
    public bool EnableIdleDimming { get; set; }

    /// <summary>
    /// 闲置时间（分钟）
    /// </summary>
    public int IdleTimeMinutes { get; set; } = 5;

    /// <summary>
    /// 闲置后的目标亮度
    /// </summary>
    public int IdleBrightness { get; set; } = 10;

    /// <summary>
    /// 是否在媒体播放时暂停闲置变暗
    /// </summary>
    public bool PauseOnMediaPlay { get; set; } = true;

    /// <summary>
    /// 是否在全屏时暂停闲置变暗
    /// </summary>
    public bool PauseOnFullscreen { get; set; } = true;

    /// <summary>
    /// 是否开机自启动
    /// </summary>
    public bool AutoStart { get; set; }

    /// <summary>
    /// 界面语言 (zh-CN / en-US)
    /// </summary>
    public string Language { get; set; } = "zh-CN";
}

/// <summary>
/// 热键设置项
/// </summary>
public sealed class HotkeySettingItem
{
    public int BrightnessValue { get; set; }
    public uint Modifiers { get; set; }
    public uint Key { get; set; }
}

/// <summary>
/// 设置管理器 - 轻量级 JSON 配置存储
/// </summary>
public sealed class SettingsManager
{
    private const string SettingsFileName = "settings.json";
    private const string AutoStartRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "LightBrightnessControl";

    private readonly string _settingsPath;
    private AppSettings _settings = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Settings => _settings;

    public SettingsManager()
    {
        // 配置文件存放在 exe 同目录
        string? exeDir = Path.GetDirectoryName(AppContext.BaseDirectory);
        _settingsPath = Path.Combine(exeDir ?? ".", SettingsFileName);
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    /// <summary>
    /// 保存设置
    /// </summary>
    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // 静默处理保存失败
        }
    }

    /// <summary>
    /// 设置开机自启动
    /// </summary>
    public void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, true);
            if (key == null) return;

            if (enable)
            {
                string exePath = Environment.ProcessPath ?? "";
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }

            _settings.AutoStart = enable;
        }
        catch
        {
            // 静默处理注册表操作失败
        }
    }

    /// <summary>
    /// 检查是否已设置开机自启动
    /// </summary>
    public bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取显示器亮度比例
    /// </summary>
    public double GetMonitorRatio(string deviceName)
    {
        return _settings.MonitorRatios.TryGetValue(deviceName, out double ratio) ? ratio : 1.0;
    }

    /// <summary>
    /// 设置显示器亮度比例
    /// </summary>
    public void SetMonitorRatio(string deviceName, double ratio)
    {
        _settings.MonitorRatios[deviceName] = Math.Clamp(ratio, 0.1, 1.0);
    }
}
