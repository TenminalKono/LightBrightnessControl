namespace LightBrightnessControl;

/// <summary>
/// 本地化管理器 - 支持中英文即时切换
/// </summary>
public static class LocalizationManager
{
    public static string CurrentLanguage { get; private set; } = "zh-CN";

    public static event EventHandler? LanguageChanged;

    private static readonly Dictionary<string, string> ChineseStrings = new()
    {
        // 窗口标题
        ["AppTitle"] = "亮度控制 v2.2",
        ["AppTitleWithMonitors"] = "亮度控制 v2.2 - 检测到 {0} 个显示器",

        // 显示器区域
        ["MonitorBrightness"] = "显示器亮度",
        ["SyncAllMonitors"] = "同步调整所有显示器",
        ["RefreshMonitors"] = "刷新显示器",
        ["Calibration"] = "曲线校准...",
        ["Monitor"] = "显示器",
        ["PrimaryTag"] = "[主]",
        ["NoMonitorDetected"] = "未检测到支持 DDC/CI 的显示器",

        // 快捷键预设区域
        ["GlobalHotkeys"] = "全局快捷键预设（按亮度从低到高排序）",
        ["Preset"] = "预设",
        ["ClickToSet"] = "点击设置...",
        ["PressHotkey"] = "按下快捷键组合...",
        ["Clear"] = "清除",
        ["AddPreset"] = "+ 添加预设",
        ["DeletePresetTooltip"] = "删除此预设",

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
        ["TrayTooltip"] = "亮度控制 v2.2",

        // 多点曲线校准对话框
        ["CalibrationTitle"] = "多点亮度曲线校准",
        ["CalibrationMonitor"] = "编辑显示器:",
        ["ShowOtherCurves"] = "叠加其他屏曲线",
        ["LivePreview"] = "实时预览（肉眼对齐）",
        ["ResetLinear"] = "重置 1:1",
        ["SyncFromPresets"] = "同步预设锚点",
        ["TargetInput"] = "目标:",
        ["ActualOutput"] = "实际输出:",
        ["AddPoint"] = "+ 添加点",
        ["DeletePoint"] = "- 删除点",
        ["PreviewPoint"] = "预览此档",
        ["CalibrationPoints"] = "锚点列表（支持键盘直接修改数值）",
        ["CalibrationHint"] = "画布操作：拖动锚点 / 双击空白添点 / 右键删点 / 方向键微调",
        ["AxisTarget"] = "目标亮度 (%)",
        ["AxisOutput"] = "实际输出 (%)",
        ["ColTarget"] = "目标 (%)",
        ["ColOutput"] = "输出 (%)",
        ["NoMonitorForCalibration"] = "当前未检测到支持 DDC/CI 的显示器，无法进行校准。",
        ["OK"] = "确定",
        ["Cancel"] = "取消"
    };

    private static readonly Dictionary<string, string> EnglishStrings = new()
    {
        // Window title
        ["AppTitle"] = "LightBrightnessControl v2.2",
        ["AppTitleWithMonitors"] = "LightBrightnessControl v2.2 - {0} monitor(s) detected",

        // Monitor section
        ["MonitorBrightness"] = "Monitor Brightness",
        ["SyncAllMonitors"] = "Sync all monitors",
        ["RefreshMonitors"] = "Refresh Monitors",
        ["Calibration"] = "Calibration...",
        ["Monitor"] = "Monitor",
        ["PrimaryTag"] = "[Primary]",
        ["NoMonitorDetected"] = "No DDC/CI compatible monitor detected",

        // Hotkey presets section
        ["GlobalHotkeys"] = "Global Hotkey Presets (Low to High)",
        ["Preset"] = "Preset",
        ["ClickToSet"] = "Click to set...",
        ["PressHotkey"] = "Press hotkey combo...",
        ["Clear"] = "Clear",
        ["AddPreset"] = "+ Add Preset",
        ["DeletePresetTooltip"] = "Remove preset",

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
        ["TrayTooltip"] = "Brightness Control v2.2",

        // Multi-point curve calibration dialog
        ["CalibrationTitle"] = "Multi-Point Brightness Calibration",
        ["CalibrationMonitor"] = "Edit Monitor:",
        ["ShowOtherCurves"] = "Overlay others",
        ["LivePreview"] = "Live preview (eye match)",
        ["ResetLinear"] = "Reset 1:1",
        ["SyncFromPresets"] = "Sync Presets",
        ["TargetInput"] = "Target:",
        ["ActualOutput"] = "Output:",
        ["AddPoint"] = "+ Add",
        ["DeletePoint"] = "- Delete",
        ["PreviewPoint"] = "Preview",
        ["CalibrationPoints"] = "Anchor Table (Direct Keyboard Input)",
        ["CalibrationHint"] = "Graph: Drag node / Double-click add / Right-click delete / Arrow keys nudge",
        ["AxisTarget"] = "Target (%)",
        ["AxisOutput"] = "Output (%)",
        ["ColTarget"] = "Target (%)",
        ["ColOutput"] = "Output (%)",
        ["NoMonitorForCalibration"] = "No DDC/CI compatible monitor detected for calibration.",
        ["OK"] = "OK",
        ["Cancel"] = "Cancel"
    };

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

    public static string GetString(string key)
    {
        var strings = CurrentLanguage == "en-US" ? EnglishStrings : ChineseStrings;
        return strings.TryGetValue(key, out var value) ? value : key;
    }

    public static string GetString(string key, params object[] args)
    {
        var template = GetString(key);
        return string.Format(template, args);
    }

    public static bool IsEnglish => CurrentLanguage == "en-US";
    public static bool IsChinese => CurrentLanguage == "zh-CN";
}
