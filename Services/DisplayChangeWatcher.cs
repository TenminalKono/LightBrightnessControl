using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LightBrightnessControl;

/// <summary>
/// 纯事件驱动的显示器状态监视器（方案 3A）
/// 监听 WM_DISPLAYCHANGE / WM_DEVICECHANGE (GUID_DEVINTERFACE_MONITOR) /
/// SystemEvents.DisplaySettingsChanged / 电源显示唤醒通知。
/// 仅在收到系统变更事件时启动一次性防抖与延迟重试计时器，完成后立即销毁，
/// 平时后台无常驻轮询定时器，CPU 占用 0%。
/// </summary>
public sealed class DisplayChangeWatcher : IDisposable
{
    // 防抖与延迟重试时间表（毫秒）：应对 DisplaySwitch /extend 后副屏 DDC/CI 就绪延迟
    private static readonly int[] RetryDelaysMs = { 650, 1500, 2800 };

    private readonly IntPtr _windowHandle;
    private readonly Action<Action> _invokeOnUi;
    private readonly Func<Task<bool>> _refreshAndCheckAllReadyAsync;

    private IntPtr _deviceNotifyHandle;
    private IntPtr _powerNotifyHandle;
    private System.Windows.Forms.Timer? _oneShotTimer;
    private int _retryStep;
    private int _sequenceId;
    private bool _refreshRunning;
    private bool _pendingReschedule;
    private bool _disposed;

    /// <param name="windowHandle">主窗体 HWND</param>
    /// <param name="invokeOnUi">切回 UI 线程执行回调</param>
    /// <param name="refreshAndCheckAllReadyAsync">执行一次异步刷新，返回 true 表示所有逻辑显示器均已就绪（无需继续重试）</param>
    public DisplayChangeWatcher(
        IntPtr windowHandle,
        Action<Action> invokeOnUi,
        Func<Task<bool>> refreshAndCheckAllReadyAsync)
    {
        _windowHandle = windowHandle;
        _invokeOnUi = invokeOnUi;
        _refreshAndCheckAllReadyAsync = refreshAndCheckAllReadyAsync;

        RegisterNotifications();
    }

    private void RegisterNotifications()
    {
        // 1. 注册显示器设备接口变更通知（即使窗体隐藏在托盘也能可靠接收）
        IntPtr buffer = IntPtr.Zero;
        try
        {
            var dbi = new NativeMethods.DEV_BROADCAST_DEVICEINTERFACE
            {
                dbcc_size = Marshal.SizeOf<NativeMethods.DEV_BROADCAST_DEVICEINTERFACE>(),
                dbcc_devicetype = NativeMethods.DBT_DEVTYP_DEVICEINTERFACE,
                dbcc_reserved = 0,
                dbcc_classguid = NativeMethods.GUID_DEVINTERFACE_MONITOR
            };

            buffer = Marshal.AllocHGlobal(dbi.dbcc_size);
            Marshal.StructureToPtr(dbi, buffer, false);
            _deviceNotifyHandle = NativeMethods.RegisterDeviceNotification(
                _windowHandle,
                buffer,
                NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);
        }
        catch
        {
            // 静默回退到其他系统事件
        }
        finally
        {
            if (buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(buffer);
        }

        // 2. 注册控制台显示器开关/唤醒通知
        try
        {
            Guid guid = NativeMethods.GUID_CONSOLE_DISPLAY_STATE;
            _powerNotifyHandle = NativeMethods.RegisterPowerSettingNotification(
                _windowHandle,
                ref guid,
                NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);
        }
        catch
        {
            // 静默回退
        }

        // 3. 监听 .NET 系统显示设置变化事件
        SystemEvents.DisplaySettingsChanged += OnSystemDisplaySettingsChanged;
    }

    /// <summary>
    /// 在主窗体 WndProc 中调用，识别显示器相关系统消息
    /// </summary>
    public void ProcessWindowMessage(ref Message m)
    {
        if (_disposed) return;

        switch (m.Msg)
        {
            case NativeMethods.WM_DISPLAYCHANGE:
                ScheduleRefreshSequence();
                break;

            case NativeMethods.WM_DEVICECHANGE:
            {
                int eventCode = m.WParam.ToInt32();
                if (eventCode == NativeMethods.DBT_DEVICEARRIVAL ||
                    eventCode == NativeMethods.DBT_DEVICEREMOVECOMPLETE ||
                    eventCode == NativeMethods.DBT_DEVNODES_CHANGED)
                {
                    ScheduleRefreshSequence();
                }
                break;
            }

            case NativeMethods.WM_POWERBROADCAST:
            {
                int wParam = m.WParam.ToInt32();
                if (wParam == NativeMethods.PBT_APMRESUMEAUTOMATIC ||
                    wParam == NativeMethods.PBT_APMRESUMESUSPEND)
                {
                    ScheduleRefreshSequence();
                }
                else if (wParam == NativeMethods.PBT_POWERSETTINGCHANGE && m.LParam != IntPtr.Zero)
                {
                    try
                    {
                        var setting = Marshal.PtrToStructure<NativeMethods.POWERBROADCAST_SETTING>(m.LParam);
                        // 仅在显示器从关闭/休眠恢复开启 (Data == 1) 时刷新
                        if (setting.PowerSetting == NativeMethods.GUID_CONSOLE_DISPLAY_STATE && setting.Data == 1)
                        {
                            ScheduleRefreshSequence();
                        }
                    }
                    catch
                    {
                        // 忽略解析异常
                    }
                }
                break;
            }
        }
    }

    private void OnSystemDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        _invokeOnUi(ScheduleRefreshSequence);
    }

    /// <summary>
    /// 启动或重置防抖刷新序列（必须在 UI 线程调用）
    /// </summary>
    public void ScheduleRefreshSequence()
    {
        if (_disposed) return;

        _sequenceId++;
        _retryStep = 0;

        if (_refreshRunning)
        {
            _pendingReschedule = true;
            DisposeTimer();
            return;
        }

        ArmOneShotTimer(RetryDelaysMs[0]);
    }

    private void ArmOneShotTimer(int delayMs)
    {
        DisposeTimer();
        if (_disposed) return;

        _oneShotTimer = new System.Windows.Forms.Timer
        {
            Interval = delayMs
        };
        _oneShotTimer.Tick += OnOneShotTimerTick;
        _oneShotTimer.Start();
    }

    private async void OnOneShotTimerTick(object? sender, EventArgs e)
    {
        DisposeTimer();
        if (_disposed) return;

        int currentSeq = _sequenceId;
        _refreshRunning = true;
        bool allReady = true;

        try
        {
            allReady = await _refreshAndCheckAllReadyAsync();
        }
        catch
        {
            // 防止未处理异常导致崩溃
        }
        finally
        {
            _refreshRunning = false;
        }

        if (_disposed) return;

        // 若刷新期间又收到了新的显示变更事件，重新开始序列
        if (_pendingReschedule || currentSeq != _sequenceId)
        {
            _pendingReschedule = false;
            _retryStep = 0;
            ArmOneShotTimer(RetryDelaysMs[0]);
            return;
        }

        // 若尚有逻辑显示器未完成 DDC 就绪且未超重试次数，按延迟表补试一次
        if (!allReady && _retryStep + 1 < RetryDelaysMs.Length)
        {
            _retryStep++;
            ArmOneShotTimer(RetryDelaysMs[_retryStep]);
        }
        // 否则不保留任何计时器，后台回到 0% 开销状态
    }

    private void DisposeTimer()
    {
        if (_oneShotTimer != null)
        {
            _oneShotTimer.Stop();
            _oneShotTimer.Tick -= OnOneShotTimerTick;
            _oneShotTimer.Dispose();
            _oneShotTimer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DisposeTimer();
        SystemEvents.DisplaySettingsChanged -= OnSystemDisplaySettingsChanged;

        if (_deviceNotifyHandle != IntPtr.Zero)
        {
            NativeMethods.UnregisterDeviceNotification(_deviceNotifyHandle);
            _deviceNotifyHandle = IntPtr.Zero;
        }

        if (_powerNotifyHandle != IntPtr.Zero)
        {
            NativeMethods.UnregisterPowerSettingNotification(_powerNotifyHandle);
            _powerNotifyHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~DisplayChangeWatcher()
    {
        Dispose();
    }
}
