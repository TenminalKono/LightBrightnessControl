using System.Text.Json;
using Microsoft.Win32;

namespace LightBrightnessControl;

/// <summary>
/// 设置管理器 - 轻量级 JSON 配置存储与旧配置自动迁移
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
            else
            {
                _settings = new AppSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }

        _settings.MonitorRatios ??= new Dictionary<string, double>();
        _settings.MonitorCurves ??= new Dictionary<string, List<CalibrationPoint>>();
        if (_settings.Hotkeys == null || _settings.Hotkeys.Count == 0)
        {
            _settings.Hotkeys = AppSettings.CreateDefaultHotkeys();
        }
        else
        {
            // 确保预设按亮度从低到高有序
            foreach (var h in _settings.Hotkeys)
            {
                h.BrightnessValue = Math.Clamp(h.BrightnessValue, 0, 100);
            }
            _settings.Hotkeys.Sort((a, b) => a.BrightnessValue.CompareTo(b.BrightnessValue));
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
    /// 当带版本号的新 EXE 启动时，若已开启自启但注册表指向旧路径，自动更新为当前路径
    /// </summary>
    public void SyncAutoStartPathIfEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, true);
            if (key == null) return;

            var existing = key.GetValue(AppName) as string;
            if (string.IsNullOrEmpty(existing)) return;

            string exePath = Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exePath)) return;

            string expected = $"\"{exePath}\"";
            if (!string.Equals(existing, expected, StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(AppName, expected);
            }
        }
        catch
        {
            // 静默处理
        }
    }

    /// <summary>
    /// 获取显示器的多点校准曲线（优先硬件 MonitorId，回退 DeviceName，兼容旧版单比例自动迁移）
    /// </summary>
    public CalibrationCurve GetMonitorCurve(string monitorId, string deviceName)
    {
        // 1. 优先通过稳定硬件 ID 查找新版曲线
        if (!string.IsNullOrEmpty(monitorId) &&
            _settings.MonitorCurves.TryGetValue(monitorId, out var curvePoints) &&
            curvePoints != null && curvePoints.Count >= 2)
        {
            return new CalibrationCurve(curvePoints);
        }

        // 2. 回退通过 DeviceName 查找新版曲线，并迁移到 MonitorId
        if (!string.IsNullOrEmpty(deviceName) &&
            _settings.MonitorCurves.TryGetValue(deviceName, out var legacyDevicePoints) &&
            legacyDevicePoints != null && legacyDevicePoints.Count >= 2)
        {
            var curve = new CalibrationCurve(legacyDevicePoints);
            if (!string.IsNullOrEmpty(monitorId))
            {
                _settings.MonitorCurves[monitorId] = curve.ToSerializablePoints();
            }
            return curve;
        }

        // 3. 兼容旧版单比例配置自动迁移
        if (!string.IsNullOrEmpty(monitorId) &&
            _settings.MonitorRatios.TryGetValue(monitorId, out double ratioById))
        {
            var migrated = CalibrationCurve.CreateFromLegacyRatio(ratioById);
            _settings.MonitorCurves[monitorId] = migrated.ToSerializablePoints();
            return migrated;
        }

        if (!string.IsNullOrEmpty(deviceName) &&
            _settings.MonitorRatios.TryGetValue(deviceName, out double ratioByDevice))
        {
            var migrated = CalibrationCurve.CreateFromLegacyRatio(ratioByDevice);
            string targetKey = !string.IsNullOrEmpty(monitorId) ? monitorId : deviceName;
            _settings.MonitorCurves[targetKey] = migrated.ToSerializablePoints();
            return migrated;
        }

        // 4. 默认 1:1 曲线
        return CalibrationCurve.CreateDefault();
    }

    /// <summary>
    /// 保存显示器的多点校准曲线
    /// </summary>
    public void SetMonitorCurve(string calibrationKey, CalibrationCurve curve)
    {
        if (string.IsNullOrEmpty(calibrationKey)) return;
        _settings.MonitorCurves[calibrationKey] = curve.ToSerializablePoints();
    }
}
