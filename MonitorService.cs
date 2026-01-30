namespace LightBrightnessControl;

/// <summary>
/// 显示器服务 - 负责枚举、控制显示器
/// 使用 DDC/CI 协议直接与显示器通信
/// 参照工作代码的方式：只添加成功获取亮度的显示器
/// </summary>
public sealed class MonitorService : IDisposable
{
    private readonly List<MonitorInfo> _monitors = new();
    private bool _disposed;

    /// <summary>
    /// 获取所有已枚举的显示器
    /// </summary>
    public IReadOnlyList<MonitorInfo> Monitors => _monitors.AsReadOnly();

    /// <summary>
    /// 显示器列表变更事件
    /// </summary>
    public event EventHandler? MonitorsChanged;

    /// <summary>
    /// 枚举所有显示器
    /// </summary>
    public void EnumerateMonitors()
    {
        _monitors.Clear();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
            {
                // 尝试获取该显示器的亮度，只有成功的才添加到列表
                if (TryGetMonitorBrightness(hMonitor, out int brightness, out string name))
                {
                    var info = new MonitorInfo
                    {
                        Index = _monitors.Count + 1,
                        HMonitor = hMonitor,
                        Name = string.IsNullOrEmpty(name) ? $"显示器 {_monitors.Count + 1}" : name,
                        CurrentBrightness = brightness,
                        SupportsDDC = true,
                        ScreenWidth = lprcMonitor.Width,
                        ScreenHeight = lprcMonitor.Height
                    };

                    // 获取设备名称
                    var monitorInfoEx = new NativeMethods.MONITORINFOEX();
                    monitorInfoEx.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(monitorInfoEx);
                    if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfoEx))
                    {
                        info.DeviceName = monitorInfoEx.szDevice;
                    }

                    _monitors.Add(info);
                }
                return true; // 继续枚举
            }, IntPtr.Zero);

        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 尝试获取显示器亮度（参照工作代码的实现方式）
    /// </summary>
    private bool TryGetMonitorBrightness(IntPtr hMonitor, out int brightness, out string name)
    {
        brightness = 50;
        name = string.Empty;

        uint numMonitors = 0;
        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out numMonitors) || numMonitors == 0)
            return false;

        var physicalMonitors = new NativeMethods.PHYSICAL_MONITOR[numMonitors];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, numMonitors, physicalMonitors))
            return false;

        // 获取显示器名称
        if (!string.IsNullOrWhiteSpace(physicalMonitors[0].szPhysicalMonitorDescription))
        {
            name = physicalMonitors[0].szPhysicalMonitorDescription.Trim();
        }

        // 获取亮度
        uint min = 0, current = 0, max = 0;
        bool success = NativeMethods.GetMonitorBrightness(physicalMonitors[0].hPhysicalMonitor, out min, out current, out max);

        if (success && max > 0)
        {
            brightness = (int)current;
        }

        // 立即销毁所有物理显示器句柄
        for (int i = 0; i < numMonitors; i++)
        {
            NativeMethods.DestroyPhysicalMonitor(physicalMonitors[i].hPhysicalMonitor);
        }

        return success;
    }

    /// <summary>
    /// 设置单个显示器亮度
    /// </summary>
    public bool SetBrightness(int monitorIndex, int percentage)
    {
        if (monitorIndex < 0 || monitorIndex >= _monitors.Count)
            return false;

        return _monitors[monitorIndex].SetBrightness(percentage);
    }

    /// <summary>
    /// 设置所有显示器亮度（考虑各自的比例系数）
    /// </summary>
    public void SetAllBrightness(int percentage)
    {
        foreach (var monitor in _monitors)
        {
            if (monitor.SupportsDDC)
            {
                monitor.SetBrightness(percentage);
            }
        }
    }

    /// <summary>
    /// 刷新所有显示器的亮度值
    /// </summary>
    public void RefreshAllBrightness()
    {
        foreach (var monitor in _monitors)
        {
            monitor.RefreshBrightness();
        }
    }

    /// <summary>
    /// 获取支持 DDC/CI 的显示器数量
    /// </summary>
    public int GetDDCSupportedCount()
    {
        return _monitors.Count(m => m.SupportsDDC);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _monitors.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~MonitorService()
    {
        Dispose();
    }
}
