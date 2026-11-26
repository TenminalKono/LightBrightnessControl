using System.Runtime.InteropServices;

namespace LightBrightnessControl;

/// <summary>
/// 显示器枚举结果快照
/// </summary>
public sealed class MonitorEnumerationResult
{
    public List<MonitorInfo> Monitors { get; init; } = new();
    public int TotalLogicalMonitors { get; init; }
}

/// <summary>
/// 显示器服务 - 负责枚举与 DDC/CI 亮度控制
/// 支持稳定硬件标识提取、线程安全枚举以及失败检测
/// </summary>
public sealed class MonitorService : IDisposable
{
    private readonly object _syncLock = new();
    private List<MonitorInfo> _monitors = new();
    private int _totalLogicalMonitors;
    private bool _disposed;

    /// <summary>
    /// 获取当前支持 DDC/CI 的显示器快照
    /// </summary>
    public IReadOnlyList<MonitorInfo> Monitors
    {
        get
        {
            lock (_syncLock)
            {
                return _monitors.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// 当前 Windows 报告的活动逻辑显示器总数（含不支持 DDC 的屏幕）
    /// </summary>
    public int TotalLogicalMonitors
    {
        get
        {
            lock (_syncLock)
            {
                return _totalLogicalMonitors;
            }
        }
    }

    /// <summary>
    /// 显示器列表变更事件
    /// </summary>
    public event EventHandler? MonitorsChanged;

    /// <summary>
    /// 同步枚举所有显示器并应用校准配置
    /// </summary>
    public MonitorEnumerationResult EnumerateMonitors(SettingsManager? settingsManager = null)
    {
        var result = EnumerateMonitorsSnapshot();
        CommitEnumerationResult(result, settingsManager);
        return result;
    }

    /// <summary>
    /// 在后台线程执行显示器枚举（不阻塞 UI 线程），返回快照供 UI 线程提交
    /// </summary>
    public static MonitorEnumerationResult EnumerateMonitorsSnapshot()
    {
        var found = new List<MonitorInfo>();
        int logicalCount = 0;

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
            {
                logicalCount++;

                // 获取逻辑设备名称与主屏标志
                string deviceName = string.Empty;
                bool isPrimary = false;
                var monitorInfoEx = new NativeMethods.MONITORINFOEX
                {
                    cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
                };
                if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfoEx))
                {
                    deviceName = monitorInfoEx.szDevice ?? string.Empty;
                    isPrimary = (monitorInfoEx.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                }

                // 尝试通过 DDC/CI 读取当前亮度，只有支持并成功响应的显示器才加入列表
                if (TryGetMonitorBrightness(hMonitor, out int brightness, out string physicalDesc))
                {
                    string monitorId = ResolveStableMonitorId(deviceName, out string deviceString);
                    string displayName = BuildFriendlyMonitorName(physicalDesc, deviceString, monitorId, found.Count + 1);

                    found.Add(new MonitorInfo
                    {
                        Index = found.Count + 1,
                        HMonitor = hMonitor,
                        Name = displayName,
                        DeviceName = deviceName,
                        MonitorId = monitorId,
                        IsPrimary = isPrimary,
                        CurrentBrightness = brightness,
                        SupportsDDC = true,
                        ScreenWidth = lprcMonitor.Width,
                        ScreenHeight = lprcMonitor.Height
                    });
                }

                return true;
            }, IntPtr.Zero);

        // 主显示器优先排在前面，其余按屏幕左到右排序，保证多屏顺序直观稳定
        found.Sort((a, b) =>
        {
            if (a.IsPrimary != b.IsPrimary)
                return a.IsPrimary ? -1 : 1;
            return string.Compare(a.DeviceName, b.DeviceName, StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < found.Count; i++)
        {
            found[i].Index = i + 1;
        }

        return new MonitorEnumerationResult
        {
            Monitors = found,
            TotalLogicalMonitors = logicalCount
        };
    }

    /// <summary>
    /// 提交枚举快照并绑定已保存的校准曲线
    /// </summary>
    public void CommitEnumerationResult(MonitorEnumerationResult result, SettingsManager? settingsManager = null)
    {
        if (settingsManager != null)
        {
            foreach (var monitor in result.Monitors)
            {
                monitor.CalibrationCurve = settingsManager.GetMonitorCurve(monitor.MonitorId, monitor.DeviceName);
            }
        }

        lock (_syncLock)
        {
            _monitors = result.Monitors;
            _totalLogicalMonitors = result.TotalLogicalMonitors;
        }

        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 生成当前显示器拓扑签名，用于判断刷新结果是否真正发生变化
    /// </summary>
    public string GetTopologySignature()
    {
        lock (_syncLock)
        {
            return string.Join("|", _monitors.Select(m =>
                $"{m.CalibrationKey}@{m.HMonitor}:{m.ScreenWidth}x{m.ScreenHeight}"));
        }
    }

    /// <summary>
    /// 解析物理显示器的稳定硬件标识（EDID / 设备接口路径）
    /// </summary>
    private static string ResolveStableMonitorId(string deviceName, out string deviceString)
    {
        deviceString = string.Empty;
        if (string.IsNullOrEmpty(deviceName))
            return string.Empty;

        try
        {
            // 先使用 EDD_GET_DEVICE_INTERFACE_NAME 获取包含硬件型号与端口实例的唯一接口路径
            string? fallbackId = null;
            for (uint i = 0; i < 8; i++)
            {
                var dd = new NativeMethods.DISPLAY_DEVICE
                {
                    cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>()
                };

                if (!NativeMethods.EnumDisplayDevices(deviceName, i, ref dd, NativeMethods.EDD_GET_DEVICE_INTERFACE_NAME))
                    break;

                bool isActive = (dd.StateFlags & NativeMethods.DISPLAY_DEVICE_ACTIVE) != 0;
                if (!string.IsNullOrWhiteSpace(dd.DeviceID))
                {
                    if (isActive)
                    {
                        deviceString = dd.DeviceString?.Trim() ?? string.Empty;
                        return dd.DeviceID.Trim();
                    }
                    fallbackId ??= dd.DeviceID.Trim();
                }
            }

            if (!string.IsNullOrEmpty(fallbackId))
                return fallbackId;

            // 回退不带接口标志的 DeviceID
            var ddSimple = new NativeMethods.DISPLAY_DEVICE
            {
                cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>()
            };
            if (NativeMethods.EnumDisplayDevices(deviceName, 0, ref ddSimple, 0))
            {
                deviceString = ddSimple.DeviceString?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(ddSimple.DeviceID))
                    return ddSimple.DeviceID.Trim();
            }
        }
        catch
        {
            // 忽略异常，回退到 DeviceName
        }

        return deviceName;
    }

    /// <summary>
    /// 构造易识别的显示器名称（当系统只返回 Generic PnP Monitor 时自动附带 EDID 型号码）
    /// </summary>
    private static string BuildFriendlyMonitorName(string physicalDesc, string deviceString, string monitorId, int fallbackIndex)
    {
        string baseName = !string.IsNullOrWhiteSpace(physicalDesc)
            ? physicalDesc.Trim()
            : (!string.IsNullOrWhiteSpace(deviceString) ? deviceString.Trim() : $"Monitor {fallbackIndex}");

        string? modelCode = ExtractModelCodeFromMonitorId(monitorId);
        if (!string.IsNullOrEmpty(modelCode) &&
            !baseName.Contains(modelCode, StringComparison.OrdinalIgnoreCase) &&
            (baseName.Contains("Generic", StringComparison.OrdinalIgnoreCase) ||
             baseName.Contains("通用", StringComparison.OrdinalIgnoreCase) ||
             baseName.StartsWith("Monitor ", StringComparison.OrdinalIgnoreCase)))
        {
            return $"{baseName} [{modelCode}]";
        }

        return baseName;
    }

    /// <summary>
    /// 从 \\?\DISPLAY#DEL419A#... 或 MONITOR\DEL419A\... 中提取 7 位厂商型号代码
    /// </summary>
    private static string? ExtractModelCodeFromMonitorId(string monitorId)
    {
        if (string.IsNullOrWhiteSpace(monitorId))
            return null;

        // 形如 \\?\DISPLAY#DEL419A#5&...
        if (monitorId.Contains('#'))
        {
            var parts = monitorId.Split('#', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[1].Length >= 4 && parts[1].Length <= 16)
                return parts[1];
        }

        // 形如 MONITOR\DEL419A\{...}
        if (monitorId.Contains('\\'))
        {
            var parts = monitorId.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Equals("MONITOR", StringComparison.OrdinalIgnoreCase))
                return parts[1];
        }

        return null;
    }

    /// <summary>
    /// 尝试通过 DDC/CI 获取物理显示器亮度
    /// </summary>
    private static bool TryGetMonitorBrightness(IntPtr hMonitor, out int brightness, out string name)
    {
        brightness = 50;
        name = string.Empty;

        uint numMonitors = 0;
        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out numMonitors) || numMonitors == 0)
            return false;

        var physicalMonitors = new NativeMethods.PHYSICAL_MONITOR[numMonitors];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, numMonitors, physicalMonitors))
            return false;

        if (!string.IsNullOrWhiteSpace(physicalMonitors[0].szPhysicalMonitorDescription))
        {
            name = physicalMonitors[0].szPhysicalMonitorDescription.Trim();
        }

        uint min = 0, current = 0, max = 0;
        bool success = false;
        try
        {
            success = NativeMethods.GetMonitorBrightness(
                physicalMonitors[0].hPhysicalMonitor,
                out min,
                out current,
                out max);

            if (success && max > 0)
            {
                brightness = Math.Clamp((int)current, 0, 100);
            }
        }
        finally
        {
            for (int i = 0; i < numMonitors; i++)
            {
                NativeMethods.DestroyPhysicalMonitor(physicalMonitors[i].hPhysicalMonitor);
            }
        }

        return success;
    }

    /// <summary>
    /// 设置单个显示器亮度
    /// </summary>
    /// <param name="monitorIndex">列表索引</param>
    /// <param name="percentage">亮度百分比</param>
    /// <param name="applyCalibration">独立拖动滑条为 false（直接设物理亮度）；同步/热键/闲置为 true</param>
    public bool SetBrightness(int monitorIndex, int percentage, bool applyCalibration = false)
    {
        MonitorInfo? target;
        lock (_syncLock)
        {
            if (monitorIndex < 0 || monitorIndex >= _monitors.Count)
                return false;
            target = _monitors[monitorIndex];
        }

        return target.SetBrightness(percentage, applyCalibration);
    }

    /// <summary>
    /// 设置所有显示器亮度（默认应用各显示器多点校准曲线）
    /// 返回是否全部成功；若任意显示器句柄失效返回 false，便于调用方触发懒刷新重试
    /// </summary>
    public bool SetAllBrightness(int percentage, bool applyCalibration = true)
    {
        List<MonitorInfo> snapshot;
        lock (_syncLock)
        {
            snapshot = _monitors.ToList();
        }

        if (snapshot.Count == 0)
            return false;

        bool allSucceeded = true;
        foreach (var monitor in snapshot)
        {
            if (monitor.SupportsDDC)
            {
                if (!monitor.SetBrightness(percentage, applyCalibration))
                {
                    allSucceeded = false;
                }
            }
        }

        return allSucceeded;
    }

    /// <summary>
    /// 刷新所有显示器的亮度值
    /// </summary>
    public void RefreshAllBrightness()
    {
        List<MonitorInfo> snapshot;
        lock (_syncLock)
        {
            snapshot = _monitors.ToList();
        }

        foreach (var monitor in snapshot)
        {
            monitor.RefreshBrightness();
        }
    }

    public int GetDDCSupportedCount()
    {
        lock (_syncLock)
        {
            return _monitors.Count(m => m.SupportsDDC);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_syncLock)
        {
            _monitors.Clear();
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~MonitorService()
    {
        Dispose();
    }
}
