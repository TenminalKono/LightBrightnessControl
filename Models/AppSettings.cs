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
    /// 旧版单比例设置（保留用于从 v2.0/v2.1 配置文件自动迁移）
    /// </summary>
    public Dictionary<string, double> MonitorRatios { get; set; } = new();

    /// <summary>
    /// 新版多点亮度校准曲线 (MonitorId 或 DeviceName -> 校准点列表)
    /// </summary>
    public Dictionary<string, List<CalibrationPoint>> MonitorCurves { get; set; } = new();

    /// <summary>
    /// 热键预设配置（默认 5 档，按低亮度到高亮度排序）
    /// </summary>
    public List<HotkeySettingItem> Hotkeys { get; set; } = CreateDefaultHotkeys();

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

    public static List<HotkeySettingItem> CreateDefaultHotkeys()
    {
        const uint defaultModifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
        return new()
        {
            new() { BrightnessValue = 0, Modifiers = defaultModifiers, Key = (uint)Keys.D1 },
            new() { BrightnessValue = 25, Modifiers = defaultModifiers, Key = (uint)Keys.D2 },
            new() { BrightnessValue = 50, Modifiers = defaultModifiers, Key = (uint)Keys.D3 },
            new() { BrightnessValue = 75, Modifiers = defaultModifiers, Key = (uint)Keys.D4 },
            new() { BrightnessValue = 100, Modifiers = defaultModifiers, Key = (uint)Keys.D5 }
        };
    }
}

/// <summary>
/// 热键预设项
/// </summary>
public sealed class HotkeySettingItem
{
    public int BrightnessValue { get; set; }
    public uint Modifiers { get; set; }
    public uint Key { get; set; }
}
