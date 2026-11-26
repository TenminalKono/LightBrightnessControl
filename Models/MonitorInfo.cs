namespace LightBrightnessControl;

/// <summary>
/// 显示器信息数据类
/// 不长期持有物理显示器句柄，每次 DDC 操作时按需获取并立即释放，防止句柄泄漏
/// </summary>
public sealed class MonitorInfo
{
    private readonly object _ddcLock = new();

    /// <summary>
    /// 显示器索引（从 1 开始）
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 显示器名称/描述
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 逻辑设备名称（如 \\.\DISPLAY1）
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// 稳定的物理显示器硬件标识（来自 EnumDisplayDevices 的 DeviceID）
    /// 开关副屏或拓扑重排时保持稳定
    /// </summary>
    public string MonitorId { get; set; } = string.Empty;

    /// <summary>
    /// 用于保存和读取校准曲线的主键
    /// </summary>
    public string CalibrationKey => !string.IsNullOrEmpty(MonitorId) ? MonitorId : DeviceName;

    /// <summary>
    /// 是否为主显示器
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// HMONITOR 逻辑句柄
    /// </summary>
    public IntPtr HMonitor { get; set; }

    /// <summary>
    /// 当前物理 DDC 亮度值 (0-100)
    /// </summary>
    public int CurrentBrightness { get; set; }

    /// <summary>
    /// 是否支持 DDC/CI
    /// </summary>
    public bool SupportsDDC { get; set; }

    /// <summary>
    /// 多点亮度校准曲线（用于同步调整、快捷键预设、闲置变暗）
    /// </summary>
    public CalibrationCurve CalibrationCurve { get; set; } = CalibrationCurve.CreateDefault();

    /// <summary>
    /// 屏幕分辨率宽度
    /// </summary>
    public int ScreenWidth { get; set; }

    /// <summary>
    /// 屏幕分辨率高度
    /// </summary>
    public int ScreenHeight { get; set; }

    /// <summary>
    /// 设置亮度
    /// </summary>
    /// <param name="percentage">目标亮度百分比 (0-100)</param>
    /// <param name="applyCalibration">true: 通过校准曲线映射（同步/热键/闲置）；false: 直接下发原始物理亮度（独立滑条）</param>
    public bool SetBrightness(int percentage, bool applyCalibration = false)
    {
        if (!SupportsDDC || HMonitor == IntPtr.Zero)
            return false;

        int actualBrightness = applyCalibration
            ? CalibrationCurve.Evaluate(percentage)
            : Math.Clamp(percentage, 0, 100);

        return SetRawBrightness(actualBrightness);
    }

    /// <summary>
    /// 直接向显示器下发原始物理 DDC 亮度 (0-100)
    /// </summary>
    public bool SetRawBrightness(int rawBrightness)
    {
        if (!SupportsDDC || HMonitor == IntPtr.Zero)
            return false;

        int clamped = Math.Clamp(rawBrightness, 0, 100);

        lock (_ddcLock)
        {
            uint numMonitors = 0;
            if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(HMonitor, out numMonitors) || numMonitors == 0)
                return false;

            var physicalMonitors = new NativeMethods.PHYSICAL_MONITOR[numMonitors];
            if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(HMonitor, numMonitors, physicalMonitors))
                return false;

            bool success = false;
            try
            {
                success = NativeMethods.SetMonitorBrightness(
                    physicalMonitors[0].hPhysicalMonitor,
                    (uint)clamped);
            }
            finally
            {
                for (int i = 0; i < numMonitors; i++)
                {
                    NativeMethods.DestroyPhysicalMonitor(physicalMonitors[i].hPhysicalMonitor);
                }
            }

            if (success)
            {
                CurrentBrightness = clamped;
            }

            return success;
        }
    }

    /// <summary>
    /// 获取当前亮度百分比
    /// </summary>
    public int GetBrightnessPercentage() => CurrentBrightness;

    /// <summary>
    /// 从显示器重新读取当前物理亮度值
    /// </summary>
    public bool RefreshBrightness()
    {
        if (!SupportsDDC || HMonitor == IntPtr.Zero)
            return false;

        lock (_ddcLock)
        {
            uint numMonitors = 0;
            if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(HMonitor, out numMonitors) || numMonitors == 0)
                return false;

            var physicalMonitors = new NativeMethods.PHYSICAL_MONITOR[numMonitors];
            if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(HMonitor, numMonitors, physicalMonitors))
                return false;

            uint min = 0, current = 0, max = 0;
            bool success = false;
            try
            {
                success = NativeMethods.GetMonitorBrightness(
                    physicalMonitors[0].hPhysicalMonitor,
                    out min,
                    out current,
                    out max);
            }
            finally
            {
                for (int i = 0; i < numMonitors; i++)
                {
                    NativeMethods.DestroyPhysicalMonitor(physicalMonitors[i].hPhysicalMonitor);
                }
            }

            if (success && max > 0)
            {
                CurrentBrightness = Math.Clamp((int)current, 0, 100);
            }

            return success;
        }
    }

    public string GetDisplayLabel()
    {
        return $"{Index}. {Name} ({ScreenWidth}x{ScreenHeight})";
    }

    public override string ToString() => GetDisplayLabel();
}
