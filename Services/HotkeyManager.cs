namespace LightBrightnessControl;

/// <summary>
/// 热键配置
/// </summary>
public sealed class HotkeyConfig
{
    public int Id { get; set; }
    public uint Modifiers { get; set; }
    public uint Key { get; set; }
    public int BrightnessValue { get; set; }
    public bool IsRegistered { get; set; }

    public string GetDisplayText()
    {
        var parts = new List<string>();
        
        if ((Modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((Modifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((Modifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((Modifiers & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
        
        if (Key != 0)
        {
            parts.Add(((Keys)Key).ToString());
        }
        
        return parts.Count > 0 ? string.Join(" + ", parts) : "未设置";
    }
}

/// <summary>
/// 全局热键管理器
/// 使用 RegisterHotKey API 注册系统级热键
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly IntPtr _windowHandle;
    private readonly Dictionary<int, HotkeyConfig> _hotkeys = new();
    private bool _disposed;
    private int _nextId = 0x0001; // 热键ID起始值

    /// <summary>
    /// 热键触发事件
    /// </summary>
    public event Action<int>? HotkeyPressed; // 参数为亮度值

    public HotkeyManager(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    /// <summary>
    /// 注册热键
    /// </summary>
    public bool RegisterHotkey(uint modifiers, uint key, int brightnessValue)
    {
        int id = _nextId++;
        
        // 添加 MOD_NOREPEAT 防止连续触发
        uint actualModifiers = modifiers | NativeMethods.MOD_NOREPEAT;

        if (NativeMethods.RegisterHotKey(_windowHandle, id, actualModifiers, key))
        {
            var config = new HotkeyConfig
            {
                Id = id,
                Modifiers = modifiers,
                Key = key,
                BrightnessValue = brightnessValue,
                IsRegistered = true
            };
            _hotkeys[id] = config;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 取消注册热键
    /// </summary>
    public bool UnregisterHotkey(int id)
    {
        if (_hotkeys.TryGetValue(id, out var config))
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
            config.IsRegistered = false;
            _hotkeys.Remove(id);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 取消注册所有热键
    /// </summary>
    public void UnregisterAllHotkeys()
    {
        foreach (var id in _hotkeys.Keys.ToList())
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
        }
        _hotkeys.Clear();
    }

    /// <summary>
    /// 处理热键消息（从 WndProc 调用）
    /// </summary>
    public bool ProcessHotkey(int hotkeyId)
    {
        if (_hotkeys.TryGetValue(hotkeyId, out var config))
        {
            HotkeyPressed?.Invoke(config.BrightnessValue);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 获取所有已注册的热键
    /// </summary>
    public IReadOnlyCollection<HotkeyConfig> GetRegisteredHotkeys()
    {
        return _hotkeys.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// 根据亮度值查找热键
    /// </summary>
    public HotkeyConfig? GetHotkeyByBrightness(int brightnessValue)
    {
        return _hotkeys.Values.FirstOrDefault(h => h.BrightnessValue == brightnessValue);
    }

    public void Dispose()
    {
        if (_disposed) return;

        UnregisterAllHotkeys();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~HotkeyManager()
    {
        Dispose();
    }
}
