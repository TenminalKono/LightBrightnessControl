namespace LightBrightnessControl;

/// <summary>
/// 全屏检测器 - 检测前台窗口是否处于全屏状态
/// </summary>
public static class FullscreenDetector
{
    /// <summary>
    /// 检查前台窗口是否全屏
    /// </summary>
    public static bool IsFullscreenAppRunning()
    {
        try
        {
            // 获取前台窗口句柄
            IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return false;

            // 获取窗口矩形
            if (!NativeMethods.GetWindowRect(foregroundWindow, out var windowRect))
                return false;

            // 获取窗口所在显示器
            IntPtr monitor = NativeMethods.MonitorFromWindow(
                foregroundWindow,
                NativeMethods.MONITOR_DEFAULTTONEAREST);

            if (monitor == IntPtr.Zero)
                return false;

            // 获取显示器信息
            var monitorInfo = new NativeMethods.MONITORINFOEX();
            monitorInfo.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(monitorInfo);

            if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
                return false;

            // 比较窗口大小与显示器大小
            var screenRect = monitorInfo.rcMonitor;

            // 如果窗口覆盖了整个显示器，判定为全屏
            return windowRect.Left <= screenRect.Left &&
                   windowRect.Top <= screenRect.Top &&
                   windowRect.Right >= screenRect.Right &&
                   windowRect.Bottom >= screenRect.Bottom;
        }
        catch
        {
            return false;
        }
    }
}
