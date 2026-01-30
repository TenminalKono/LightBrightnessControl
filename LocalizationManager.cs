namespace LightBrightnessControl;

/// <summary>
/// 本地化管理器 - 支持中英文切换
/// </summary>
public static class LocalizationManager
{
    /// <summary>
    /// 当前语言
    /// </summary>
    public static string CurrentLanguage { get; private set; } = "zh-CN";

    /// <summary>
    /// 语言改变事件
    /// </summary>
    public static event EventHandler? LanguageChanged;

    /// <summary>
    /// 中文字符串
    /// </summary>
    private static readonly Dictionary<string, string> ChineseStrings = new()
    {
        // 窗口标题
        ["AppTitle"] = "亮度控制 v2.0",
        ["AppTitleWithMonitors"] = "亮度控制 v2.0 - 检测到 {0} 个显示器",
        
        // 显示器区域
        ["MonitorBrightness"] = "显示器亮度",
        ["SyncAllMonitors"] = "同步调整所有显示器",
        ["Calibration"] = "比例校准...",
        ["Monitor"] = "显示器",
        ["NoMonitorDetected"] = "未检测到支持DDC/CI的显示器",
        
        // 快捷键区域
        ["GlobalHotkeys"] = "全局快捷键",
        ["Preset"] = "预设",
        ["ClickToSet"] = "点击设置...",
        ["PressHotkey"] = "按下快捷键组合...",
        ["Clear"] = "清除",
        
        // 自动化区域
        ["AutomationStrategies"] = "自动变暗",
        ["EnableIdleDimming"] = "开启闲置变暗",
        ["IdleTime"] = "闲置时间(分钟):",
        ["TargetBrightness"] = "目标亮度:",
        ["PauseOnMedia"] = "媒体播放时不执行闲置变暗",
        ["PauseOnFullscreen"] = "全屏时不执行闲置变暗",
        ["AutoStart"] = "开机自动启动",
        
        // 语言切换
        ["Language"] = "语言/Language:",
        ["Chinese"] = "中文",
        ["English"] = "English",
        
        // 托盘菜单
        ["ShowMainWindow"] = "显示主界面",
        ["Exit"] = "退出",
        ["TrayTooltip"] = "亮度控制",
        
        // 校准对话框
        ["CalibrationTitle"] = "亮度比例校准",
        ["CalibrationDesc"] = "设置每个显示器的峰值亮度比例。\n例如：较亮的显示器设置为80%，表示同步调整时其最大亮度为80%。",
        ["OK"] = "确定",
        ["Cancel"] = "取消"
    };

    /// <summary>
    /// 英文字符串
    /// </summary>
    private static readonly Dictionary<string, string> EnglishStrings = new()
    {
        // Window title
        ["AppTitle"] = "LightBrightnessControl v2.0",
        ["AppTitleWithMonitors"] = "LightBrightnessControl v2.0 - {0} monitor(s) detected",
        
        // Monitor section
        ["MonitorBrightness"] = "Monitor Brightness",
        ["SyncAllMonitors"] = "Sync all monitors",
        ["Calibration"] = "Calibration...",
        ["Monitor"] = "Monitor",
        ["NoMonitorDetected"] = "No DDC/CI compatible monitor detected",
        
        // Hotkey section
        ["GlobalHotkeys"] = "Global Hotkeys",
        ["Preset"] = "Preset",
        ["ClickToSet"] = "Click to set...",
        ["PressHotkey"] = "Press hotkey combo...",
        ["Clear"] = "Clear",
        
        // Automation section
        ["AutomationStrategies"] = "Dimming Automation",
        ["EnableIdleDimming"] = "Enable idle dimming",
        ["IdleTime"] = "Idle time (min):",
        ["TargetBrightness"] = "Target:",
        ["PauseOnMedia"] = "Pause when media playing",
        ["PauseOnFullscreen"] = "Pause when fullscreen",
        ["AutoStart"] = "Start with Windows",
        
        // Language switch
        ["Language"] = "语言/Language:",
        ["Chinese"] = "中文",
        ["English"] = "English",
        
        // Tray menu
        ["ShowMainWindow"] = "Show Main Window",
        ["Exit"] = "Exit",
        ["TrayTooltip"] = "Brightness Control",
        
        // Calibration dialog
        ["CalibrationTitle"] = "Brightness Ratio Calibration",
        ["CalibrationDesc"] = "Set peak brightness ratio for each monitor.\nFor example: set brighter monitor to 80%, meaning max 80% when syncing.",
        ["OK"] = "OK",
        ["Cancel"] = "Cancel"
    };

    /// <summary>
    /// 设置语言
    /// </summary>
    public static void SetLanguage(string language)
    {
        if (language != "zh-CN" && language != "en-US")
            language = "zh-CN";

        if (CurrentLanguage != language)
        {
            CurrentLanguage = language;
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    public static string GetString(string key)
    {
        var strings = CurrentLanguage == "en-US" ? EnglishStrings : ChineseStrings;
        return strings.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// 获取格式化的本地化字符串
    /// </summary>
    public static string GetString(string key, params object[] args)
    {
        var template = GetString(key);
        return string.Format(template, args);
    }

    /// <summary>
    /// 是否为英文
    /// </summary>
    public static bool IsEnglish => CurrentLanguage == "en-US";

    /// <summary>
    /// 是否为中文
    /// </summary>
    public static bool IsChinese => CurrentLanguage == "zh-CN";
}
