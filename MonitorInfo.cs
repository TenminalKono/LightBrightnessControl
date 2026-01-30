namespace LightBrightnessControl;

/// <summary>
/// 显示器信息数据类
/// 不再保存物理显示器句柄，而是每次操作时重新获取
/// </summary>
public sealed class MonitorInfo
{
    /// <summary>
    /// 显示器索引（从1开始）
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 显示器名称/描述
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 设备名称（如 \\.\DISPLAY1）
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// HMONITOR 句柄（逻辑显示器句柄，用于获取物理显示器）
    /// </summary>
    public IntPtr HMonitor { get; set; }

    /// <summary>
    /// 当前亮度值 (0-100)
    /// </summary>
    public int CurrentBrightness { get; set; }

    /// <summary>
    /// 是否支持 DDC/CI（初始检测时确定）
    /// </summary>
    public bool SupportsDDC { get; set; }

    /// <summary>
    /// 亮度比例系数（用于同步调整时的校准）
    /// </summary>
    public double BrightnessRatio { get; set; } = 1.0;

    /// <summary>
    /// 屏幕分辨率宽度
    /// </summary>
    public int ScreenWidth { get; set; }

    /// <summary>
    /// 屏幕分辨率高度
    /// </summary>
    public int ScreenHeight { get; set; }

    /// <summary>
    /// 设置亮度（每次操作时重新获取物理显示器句柄）
    /// </summary>
    public bool SetBrightness(int percentage)
    {
        if (!SupportsDDC || HMonitor == IntPtr.Zero)
            return false;

        // 应用比例系数
        int adjustedPercentage = (int)(percentage * BrightnessRatio);
        adjustedPercentage = Math.Clamp(adjustedPercentage, 0, 100);

        // 获取物理显示器句柄
        uint numMonitors = 0;
        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(HMonitor, out numMonitors) || numMonitors == 0)
            return false;

        var physicalMonitors = new NativeMethods.PHYSICAL_MONITOR[numMonitors];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(HMonitor, numMonitors, physicalMonitors))
            return false;

        // 设置亮度
        bool success = NativeMethods.SetMonitorBrightness(physicalMonitors[0].hPhysicalMonitor, (uint)adjustedPercentage);

        // 立即销毁句柄
        for (int i = 0; i < numMonitors; i++)
        {
            NativeMethods.DestroyPhysicalMonitor(physicalMonitors[i].hPhysicalMonitor);
        }

        if (success)
        {
            CurrentBrightness = adjustedPercentage;
        }

        return success;
    }

    /// <summary>
    /// 获取当前亮度百分比
    /// </summary>
    public int GetBrightnessPercentage()
    {
        return CurrentBrightness;
    }

    /// <summary>
    /// 刷新当前亮度值（每次操作时重新获取物理显示器句柄）
    /// </summary>
    public bool RefreshBrightness()
    {
        if (!SupportsDDC || HMonitor == IntPtr.Zero)
            return false;

        uint numMonitors = 0;
        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(HMonitor, out numMonitors) || numMonitors == 0)
            return false;

        var physicalMonitors = new NativeMethods.PHYSICAL_MONITOR[numMonitors];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(HMonitor, numMonitors, physicalMonitors))
            return false;

        uint min = 0, current = 0, max = 0;
        bool success = NativeMethods.GetMonitorBrightness(physicalMonitors[0].hPhysicalMonitor, out min, out current, out max);

        // 立即销毁句柄
        for (int i = 0; i < numMonitors; i++)
        {
            NativeMethods.DestroyPhysicalMonitor(physicalMonitors[i].hPhysicalMonitor);
        }

        if (success && max > 0)
        {
            CurrentBrightness = (int)current;
        }

        return success;
    }

    public override string ToString()
    {
        return $"显示器 {Index}: {Name} ({ScreenWidth}x{ScreenHeight})";
    }
}
