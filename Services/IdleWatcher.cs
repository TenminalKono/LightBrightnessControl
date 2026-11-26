namespace LightBrightnessControl;

/// <summary>
/// 闲置监视器 - 检测用户无操作状态
/// 只有当功能启用时才创建 Timer，否则零开销
/// </summary>
public sealed class IdleWatcher : IDisposable
{
    private System.Windows.Forms.Timer? _checkTimer;
    private bool _isIdle;
    private int _idleThresholdMs;
    private bool _disposed;

    /// <summary>
    /// 进入闲置状态事件
    /// </summary>
    public event EventHandler? IdleEntered;

    /// <summary>
    /// 离开闲置状态事件
    /// </summary>
    public event EventHandler? IdleExited;

    /// <summary>
    /// 检查是否应该暂停（由外部条件决定）
    /// </summary>
    public Func<bool>? ShouldPauseCheck { get; set; }

    /// <summary>
    /// 当前是否处于闲置状态
    /// </summary>
    public bool IsIdle => _isIdle;

    /// <summary>
    /// 启动闲置监视
    /// </summary>
    /// <param name="idleMinutes">闲置阈值（分钟）</param>
    public void Start(int idleMinutes)
    {
        Stop(); // 先停止已有的 Timer

        _idleThresholdMs = idleMinutes * 60 * 1000;
        _isIdle = false;

        // 创建 Timer，每秒检查一次
        _checkTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000 // 1秒检查一次
        };
        _checkTimer.Tick += OnTimerTick;
        _checkTimer.Start();
    }

    /// <summary>
    /// 停止闲置监视并释放 Timer
    /// </summary>
    public void Stop()
    {
        if (_checkTimer != null)
        {
            _checkTimer.Stop();
            _checkTimer.Tick -= OnTimerTick;
            _checkTimer.Dispose();
            _checkTimer = null;
        }
        _isIdle = false;
    }

    /// <summary>
    /// 更新闲置阈值
    /// </summary>
    public void UpdateThreshold(int idleMinutes)
    {
        _idleThresholdMs = idleMinutes * 60 * 1000;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        try
        {
            // 检查是否应该暂停
            bool shouldPause = false;
            try
            {
                shouldPause = ShouldPauseCheck?.Invoke() == true;
            }
            catch
            {
                // 忽略暂停检查中的异常
                shouldPause = false;
            }

            if (shouldPause)
            {
                // 如果之前是闲置状态，需要恢复
                if (_isIdle)
                {
                    _isIdle = false;
                    try
                    {
                        IdleExited?.Invoke(this, EventArgs.Empty);
                    }
                    catch
                    {
                        // 忽略事件处理异常
                    }
                }
                return;
            }

            uint idleTime = GetIdleTimeMs();
            bool wasIdle = _isIdle;
            _isIdle = idleTime >= (uint)_idleThresholdMs;

            if (_isIdle && !wasIdle)
            {
                // 刚进入闲置状态
                try
                {
                    IdleEntered?.Invoke(this, EventArgs.Empty);
                }
                catch
                {
                    // 忽略事件处理异常
                }
            }
            else if (!_isIdle && wasIdle)
            {
                // 离开闲置状态
                try
                {
                    IdleExited?.Invoke(this, EventArgs.Empty);
                }
                catch
                {
                    // 忽略事件处理异常
                }
            }
        }
        catch
        {
            // 捕获所有异常，防止 Timer 回调崩溃导致程序退出
        }
    }

    /// <summary>
    /// 获取用户闲置时间（毫秒）
    /// </summary>
    private static uint GetIdleTimeMs()
    {
        try
        {
            var lastInputInfo = new NativeMethods.LASTINPUTINFO
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>()
            };

            if (NativeMethods.GetLastInputInfo(ref lastInputInfo))
            {
                return NativeMethods.GetTickCount() - lastInputInfo.dwTime;
            }
        }
        catch
        {
            // 忽略异常
        }

        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~IdleWatcher()
    {
        Dispose();
    }
}
