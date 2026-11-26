using System.Reflection;

namespace LightBrightnessControl;

/// <summary>
/// 主窗体 - 纯代码构建原生 WinForms UI，无第三方依赖
/// v2.2：动态预设亮度(1A+1-S1)、多点非线性曲线校准(2C)、事件驱动自动刷新(3A)
/// </summary>
public sealed class MainForm : Form
{
    private const int MinPresetCount = 1;
    private const int MaxPresetCount = 15;
    private const int PresetRowHeight = 28;
    private const int MinVisiblePresetRows = 5;
    private const int MaxVisiblePresetRows = 6;

    #region 服务与管理器

    private readonly MonitorService _monitorService;
    private readonly SettingsManager _settingsManager;
    private HotkeyManager? _hotkeyManager;
    private IdleWatcher? _idleWatcher;
    private DisplayChangeWatcher? _displayWatcher;

    #endregion

    #region UI 控件

    // 语言切换
    private Label _languageLabel = null!;
    private ComboBox _languageComboBox = null!;

    // 显示器控制区域
    private GroupBox _monitorGroupBox = null!;
    private Panel _monitorPanel = null!;
    private CheckBox _syncCheckBox = null!;
    private Button _refreshButton = null!;
    private Button _calibrateButton = null!;
    private readonly List<MonitorControlRow> _monitorRows = new();
    private ToolTip _toolTip = null!;

    // 动态预设快捷键区域
    private GroupBox _hotkeyGroupBox = null!;
    private Panel _hotkeyPanel = null!;
    private Button _addPresetButton = null!;
    private readonly List<HotkeyRow> _hotkeyRows = new();

    // 自动化设置区域
    private GroupBox _automationGroupBox = null!;
    private CheckBox _idleDimmingCheckBox = null!;
    private Label _idleTimeLabel = null!;
    private NumericUpDown _idleTimeNumeric = null!;
    private Label _idleBrightnessLabel = null!;
    private NumericUpDown _idleBrightnessNumeric = null!;
    private CheckBox _pauseOnMediaCheckBox = null!;
    private CheckBox _pauseOnFullscreenCheckBox = null!;
    private CheckBox _autoStartCheckBox = null!;

    // 系统托盘
    private NotifyIcon _trayIcon = null!;
    private ContextMenuStrip _trayMenu = null!;

    #endregion

    #region 状态标志

    private readonly Dictionary<string, int> _preDimMonitorBrightness = new();
    private int _preDimFallbackBrightness = 100;
    private readonly object _ddcQueueLock = new();
    private (long Version, Action Work)? _pendingDdcWork;
    private bool _ddcWorkerRunning;
    private long _brightnessCommandVersion;
    private bool _suppressMonitorSliderEvents;
    private bool _suppressPresetEvents;
    private bool _lazyRecoveryInFlight;
    private Icon? _appIcon;

    #endregion

    public MainForm()
    {
        _monitorService = new MonitorService();
        _settingsManager = new SettingsManager();

        InitializeComponent();
        LoadSettings();
        InitializeMonitors();
        SetupTrayIcon();
    }

    private void InitializeComponent()
    {
        _toolTip = new ToolTip();
        _appIcon = LoadAppIcon();

        Text = LocalizationManager.GetString("AppTitle");
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(444, 590);
        Icon = _appIcon;

        int y = 10;

        // ========== 语言切换区域 ==========
        _languageLabel = new Label
        {
            Text = LocalizationManager.GetString("Language"),
            Location = new Point(12, y + 3),
            AutoSize = true
        };
        Controls.Add(_languageLabel);

        _languageComboBox = new ComboBox
        {
            Location = new Point(122, y),
            Size = new Size(100, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _languageComboBox.Items.Add("中文");
        _languageComboBox.Items.Add("English");
        _languageComboBox.SelectedIndex = LocalizationManager.IsChinese ? 0 : 1;
        _languageComboBox.SelectedIndexChanged += OnLanguageChanged;
        Controls.Add(_languageComboBox);

        y += 34;

        // ========== 显示器控制区域 ==========
        _monitorGroupBox = new GroupBox
        {
            Text = LocalizationManager.GetString("MonitorBrightness"),
            Location = new Point(12, y),
            Size = new Size(420, 152)
        };

        _monitorPanel = new Panel
        {
            Location = new Point(8, 20),
            Size = new Size(404, 92),
            AutoScroll = true
        };
        _monitorGroupBox.Controls.Add(_monitorPanel);

        _syncCheckBox = new CheckBox
        {
            Text = LocalizationManager.GetString("SyncAllMonitors"),
            Location = new Point(10, 118),
            AutoSize = true
        };
        _syncCheckBox.CheckedChanged += OnSyncCheckChanged;
        _monitorGroupBox.Controls.Add(_syncCheckBox);

        _refreshButton = new Button
        {
            Text = LocalizationManager.GetString("RefreshMonitors"),
            Location = new Point(182, 115),
            Size = new Size(110, 25)
        };
        _refreshButton.Click += OnRefreshMonitorsClick;
        _monitorGroupBox.Controls.Add(_refreshButton);

        _calibrateButton = new Button
        {
            Text = LocalizationManager.GetString("Calibration"),
            Location = new Point(298, 115),
            Size = new Size(112, 25)
        };
        _calibrateButton.Click += OnCalibrateClick;
        _monitorGroupBox.Controls.Add(_calibrateButton);

        Controls.Add(_monitorGroupBox);
        y += 162;

        // ========== 动态全局快捷键预设区域 (1A) ==========
        _hotkeyGroupBox = new GroupBox
        {
            Text = LocalizationManager.GetString("GlobalHotkeys"),
            Location = new Point(12, y),
            Size = new Size(420, 200)
        };

        _hotkeyPanel = new Panel
        {
            Location = new Point(8, 20),
            Size = new Size(404, MinVisiblePresetRows * PresetRowHeight + 4),
            AutoScroll = true
        };
        _hotkeyGroupBox.Controls.Add(_hotkeyPanel);

        _addPresetButton = new Button
        {
            Text = LocalizationManager.GetString("AddPreset"),
            Location = new Point(10, _hotkeyPanel.Bottom + 6),
            Size = new Size(105, 24)
        };
        _addPresetButton.Click += OnAddPresetClick;
        _hotkeyGroupBox.Controls.Add(_addPresetButton);

        Controls.Add(_hotkeyGroupBox);

        // ========== 自动化设置区域 ==========
        _automationGroupBox = new GroupBox
        {
            Text = LocalizationManager.GetString("AutomationStrategies"),
            Location = new Point(12, _hotkeyGroupBox.Bottom + 10),
            Size = new Size(420, 160)
        };

        int autoY = 22;

        _idleDimmingCheckBox = new CheckBox
        {
            Text = LocalizationManager.GetString("EnableIdleDimming"),
            Location = new Point(10, autoY),
            AutoSize = true
        };
        _idleDimmingCheckBox.CheckedChanged += OnIdleDimmingChanged;
        _automationGroupBox.Controls.Add(_idleDimmingCheckBox);

        _idleTimeLabel = new Label
        {
            Text = LocalizationManager.GetString("IdleTime"),
            Location = new Point(132, autoY + 2),
            AutoSize = true
        };
        _automationGroupBox.Controls.Add(_idleTimeLabel);

        _idleTimeNumeric = new NumericUpDown
        {
            Location = new Point(232, autoY),
            Size = new Size(50, 23),
            Minimum = 1,
            Maximum = 60,
            Value = 5,
            Enabled = false
        };
        _idleTimeNumeric.ValueChanged += OnIdleSettingsChanged;
        _automationGroupBox.Controls.Add(_idleTimeNumeric);

        _idleBrightnessLabel = new Label
        {
            Text = LocalizationManager.GetString("TargetBrightness"),
            Location = new Point(290, autoY + 2),
            AutoSize = true
        };
        _automationGroupBox.Controls.Add(_idleBrightnessLabel);

        _idleBrightnessNumeric = new NumericUpDown
        {
            Location = new Point(352, autoY),
            Size = new Size(52, 23),
            Minimum = 0,
            Maximum = 100,
            Value = 10,
            Enabled = false
        };
        _automationGroupBox.Controls.Add(_idleBrightnessNumeric);

        autoY += 30;

        _pauseOnMediaCheckBox = new CheckBox
        {
            Text = LocalizationManager.GetString("PauseOnMedia"),
            Location = new Point(10, autoY),
            AutoSize = true,
            Enabled = false,
            Checked = true
        };
        _automationGroupBox.Controls.Add(_pauseOnMediaCheckBox);
        autoY += 26;

        _pauseOnFullscreenCheckBox = new CheckBox
        {
            Text = LocalizationManager.GetString("PauseOnFullscreen"),
            Location = new Point(10, autoY),
            AutoSize = true,
            Enabled = false,
            Checked = true
        };
        _automationGroupBox.Controls.Add(_pauseOnFullscreenCheckBox);
        autoY += 30;

        var separator = new Label
        {
            BorderStyle = BorderStyle.Fixed3D,
            Location = new Point(10, autoY),
            Size = new Size(398, 2)
        };
        _automationGroupBox.Controls.Add(separator);
        autoY += 10;

        _autoStartCheckBox = new CheckBox
        {
            Text = LocalizationManager.GetString("AutoStart"),
            Location = new Point(10, autoY),
            AutoSize = true
        };
        _autoStartCheckBox.CheckedChanged += OnAutoStartChanged;
        _automationGroupBox.Controls.Add(_autoStartCheckBox);

        Controls.Add(_automationGroupBox);
    }

    /// <summary>
    /// 根据当前预设行数动态调整预设区域与窗口高度（方案 1A：5~6 行自适应，超出则内部滚动）
    /// </summary>
    private void UpdatePresetSectionLayout()
    {
        int visibleRows = Math.Clamp(_hotkeyRows.Count, MinVisiblePresetRows, MaxVisiblePresetRows);
        int panelHeight = visibleRows * PresetRowHeight + 4;

        _hotkeyPanel.Height = panelHeight;
        _addPresetButton.Top = _hotkeyPanel.Bottom + 6;
        _hotkeyGroupBox.Height = _addPresetButton.Bottom + 10;

        _automationGroupBox.Top = _hotkeyGroupBox.Bottom + 10;
        ClientSize = new Size(444, _automationGroupBox.Bottom + 12);

        _addPresetButton.Enabled = _hotkeyRows.Count < MaxPresetCount;
        bool canDelete = _hotkeyRows.Count > MinPresetCount;
        foreach (var row in _hotkeyRows)
        {
            row.DeleteButton.Enabled = canDelete;
        }
    }

    /// <summary>
    /// 按给定列表重建所有预设行 UI
    /// </summary>
    private void RebuildPresetRows(List<HotkeySettingItem> items, HotkeySettingItem? focusItem = null)
    {
        _suppressPresetEvents = true;
        _hotkeyPanel.SuspendLayout();
        try
        {
            foreach (Control ctrl in _hotkeyPanel.Controls)
            {
                ctrl.Dispose();
            }
            _hotkeyPanel.Controls.Clear();
            _hotkeyRows.Clear();

            int y = 2;
            NumericUpDown? controlToFocus = null;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var row = CreateHotkeyRow(i, item, y);
                _hotkeyRows.Add(row);

                if (ReferenceEquals(item, focusItem))
                {
                    controlToFocus = row.BrightnessNumeric;
                }

                y += PresetRowHeight;
            }

            _hotkeyPanel.AutoScrollMinSize = new Size(0, y);
            UpdatePresetSectionLayout();

            if (controlToFocus != null)
            {
                _hotkeyPanel.ScrollControlIntoView(controlToFocus);
                controlToFocus.Focus();
                controlToFocus.Select(0, controlToFocus.Text.Length);
            }
        }
        finally
        {
            _hotkeyPanel.ResumeLayout();
            _suppressPresetEvents = false;
        }
    }

    private HotkeyRow CreateHotkeyRow(int index, HotkeySettingItem item, int y)
    {
        var row = new HotkeyRow
        {
            Index = index,
            Modifiers = item.Modifiers,
            Key = item.Key
        };

        var label = new Label
        {
            Text = $"{LocalizationManager.GetString("Preset")} {index + 1}:",
            Location = new Point(2, y + 4),
            Size = new Size(58, 20),
            AutoEllipsis = true
        };
        _hotkeyPanel.Controls.Add(label);
        row.Label = label;

        var textBox = new TextBox
        {
            Location = new Point(62, y),
            Size = new Size(138, 23),
            ReadOnly = true,
            Text = item.Key != 0
                ? GetHotkeyDisplayText(item.Modifiers, item.Key)
                : LocalizationManager.GetString("ClickToSet"),
            Tag = row
        };
        textBox.Click += OnHotkeyTextBoxClick;
        textBox.KeyDown += OnHotkeyTextBoxKeyDown;
        _hotkeyPanel.Controls.Add(textBox);
        row.TextBox = textBox;

        var numericUpDown = new NumericUpDown
        {
            Location = new Point(206, y),
            Size = new Size(54, 23),
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(item.BrightnessValue, 0, 100),
            Tag = row
        };
        numericUpDown.ValueChanged += OnBrightnessValueChanged;
        // 方案 1-S1：失焦或按回车提交后再排序，防止敲数字时行跳动
        numericUpDown.Leave += OnPresetBrightnessCommit;
        numericUpDown.KeyDown += OnPresetBrightnessKeyDown;
        _hotkeyPanel.Controls.Add(numericUpDown);
        row.BrightnessNumeric = numericUpDown;

        var percentLabel = new Label
        {
            Text = "%",
            Location = new Point(262, y + 4),
            AutoSize = true
        };
        _hotkeyPanel.Controls.Add(percentLabel);

        var clearButton = new Button
        {
            Text = LocalizationManager.GetString("Clear"),
            Location = new Point(282, y),
            Size = new Size(54, 23),
            Tag = row
        };
        clearButton.Click += OnClearHotkeyClick;
        _hotkeyPanel.Controls.Add(clearButton);
        row.ClearButton = clearButton;

        var deleteButton = new Button
        {
            Text = "-",
            Location = new Point(342, y),
            Size = new Size(28, 23),
            Tag = row
        };
        _toolTip.SetToolTip(deleteButton, LocalizationManager.GetString("DeletePresetTooltip"));
        deleteButton.Click += OnDeletePresetClick;
        _hotkeyPanel.Controls.Add(deleteButton);
        row.DeleteButton = deleteButton;

        return row;
    }

    private List<HotkeySettingItem> CollectPresetItemsFromUi()
    {
        return _hotkeyRows.Select(r => new HotkeySettingItem
        {
            BrightnessValue = (int)r.BrightnessNumeric.Value,
            Modifiers = r.Modifiers,
            Key = r.Key
        }).ToList();
    }

    /// <summary>
    /// 按低亮度在上、高亮度在下排序并刷新界面与热键注册
    /// </summary>
    private void SortAndRebuildPresets(HotkeySettingItem? focusItem = null)
    {
        var items = CollectPresetItemsFromUi();
        SortAndRebuildPresets(items, focusItem);
    }

    private void SortAndRebuildPresets(List<HotkeySettingItem> items, HotkeySettingItem? focusItem = null)
    {
        items.Sort((a, b) => a.BrightnessValue.CompareTo(b.BrightnessValue));
        RebuildPresetRows(items, focusItem);
        RegisterHotkeys();
        SaveSettings();
    }

    private void LoadSettings()
    {
        _settingsManager.Load();
        // 如果已启用自启且升级了带版本号的新 EXE，自动更新注册表路径
        _settingsManager.SyncAutoStartPathIfEnabled();

        var settings = _settingsManager.Settings;

        LocalizationManager.SetLanguage(settings.Language);
        _languageComboBox.SelectedIndex = LocalizationManager.IsChinese ? 0 : 1;

        _syncCheckBox.Checked = settings.SyncBrightness;
        _idleDimmingCheckBox.Checked = settings.EnableIdleDimming;
        _idleTimeNumeric.Value = Math.Clamp(settings.IdleTimeMinutes, 1, 60);
        _idleBrightnessNumeric.Value = Math.Clamp(settings.IdleBrightness, 0, 100);
        _pauseOnMediaCheckBox.Checked = settings.PauseOnMediaPlay;
        _pauseOnFullscreenCheckBox.Checked = settings.PauseOnFullscreen;
        _autoStartCheckBox.Checked = _settingsManager.IsAutoStartEnabled();

        RebuildPresetRows(settings.Hotkeys);
        UpdateUILanguage();
    }

    private void SaveSettings()
    {
        var settings = _settingsManager.Settings;

        settings.SyncBrightness = _syncCheckBox.Checked;
        settings.EnableIdleDimming = _idleDimmingCheckBox.Checked;
        settings.IdleTimeMinutes = (int)_idleTimeNumeric.Value;
        settings.IdleBrightness = (int)_idleBrightnessNumeric.Value;
        settings.PauseOnMediaPlay = _pauseOnMediaCheckBox.Checked;
        settings.PauseOnFullscreen = _pauseOnFullscreenCheckBox.Checked;
        settings.Language = LocalizationManager.CurrentLanguage;

        settings.Hotkeys = CollectPresetItemsFromUi();
        settings.Hotkeys.Sort((a, b) => a.BrightnessValue.CompareTo(b.BrightnessValue));

        foreach (var monitor in _monitorService.Monitors)
        {
            _settingsManager.SetMonitorCurve(monitor.CalibrationKey, monitor.CalibrationCurve);
        }

        _settingsManager.Save();
    }

    private void InitializeMonitors()
    {
        _monitorService.EnumerateMonitors(_settingsManager);
        RefreshMonitorPanel();
        UpdateWindowTitle();

        // 初始化全局热键管理器
        _hotkeyManager = new HotkeyManager(Handle);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        RegisterHotkeys();

        // 初始化纯事件驱动显示器状态监视器（方案 3A，0 后台轮询开销）
        _displayWatcher = new DisplayChangeWatcher(
            Handle,
            InvokeOnUiSafe,
            RefreshMonitorsAsync);

        if (_idleDimmingCheckBox.Checked)
        {
            StartIdleWatcher();
        }
    }

    /// <summary>
    /// 后台异步枚举显示器并在 UI 线程提交，返回是否所有活动逻辑屏均已就绪
    /// </summary>
    private async Task<bool> RefreshMonitorsAsync()
    {
        string previousSignature = _monitorService.GetTopologySignature();
        var result = await Task.Run(MonitorService.EnumerateMonitorsSnapshot);

        if (IsDisposed || Disposing)
            return true;

        _monitorService.CommitEnumerationResult(result, _settingsManager);
        string currentSignature = _monitorService.GetTopologySignature();

        // 若拓扑变化或行数不一致则重建面板；否则重新绑定最新 MonitorInfo 实例并同步真实亮度
        if (!string.Equals(previousSignature, currentSignature, StringComparison.Ordinal) ||
            _monitorRows.Count != result.Monitors.Count)
        {
            RefreshMonitorPanel();
            UpdateWindowTitle();
        }
        else
        {
            SyncMonitorSlidersFromService();
        }

        return result.TotalLogicalMonitors > 0 && result.Monitors.Count >= result.TotalLogicalMonitors;
    }

    private void InvokeOnUiSafe(Action action)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
            return;

        if (InvokeRequired)
        {
            try { BeginInvoke(action); } catch { }
        }
        else
        {
            action();
        }
    }

    private void UpdateWindowTitle()
    {
        int total = _monitorService.Monitors.Count;
        Text = total > 0
            ? LocalizationManager.GetString("AppTitleWithMonitors", total)
            : LocalizationManager.GetString("AppTitle");
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        string newLanguage = _languageComboBox.SelectedIndex == 0 ? "zh-CN" : "en-US";
        LocalizationManager.SetLanguage(newLanguage);
        UpdateUILanguage();
        SaveSettings();
    }

    private void UpdateUILanguage()
    {
        UpdateWindowTitle();

        _monitorGroupBox.Text = LocalizationManager.GetString("MonitorBrightness");
        _syncCheckBox.Text = LocalizationManager.GetString("SyncAllMonitors");
        _refreshButton.Text = LocalizationManager.GetString("RefreshMonitors");
        _calibrateButton.Text = LocalizationManager.GetString("Calibration");

        RefreshMonitorPanel();

        _hotkeyGroupBox.Text = LocalizationManager.GetString("GlobalHotkeys");
        _addPresetButton.Text = LocalizationManager.GetString("AddPreset");

        for (int i = 0; i < _hotkeyRows.Count; i++)
        {
            var row = _hotkeyRows[i];
            row.Label.Text = $"{LocalizationManager.GetString("Preset")} {i + 1}:";
            row.ClearButton.Text = LocalizationManager.GetString("Clear");
            _toolTip.SetToolTip(row.DeleteButton, LocalizationManager.GetString("DeletePresetTooltip"));
            if (row.Key == 0)
            {
                row.TextBox.Text = LocalizationManager.GetString("ClickToSet");
            }
        }

        _automationGroupBox.Text = LocalizationManager.GetString("AutomationStrategies");
        _idleDimmingCheckBox.Text = LocalizationManager.GetString("EnableIdleDimming");
        _idleTimeLabel.Text = LocalizationManager.GetString("IdleTime");
        _idleBrightnessLabel.Text = LocalizationManager.GetString("TargetBrightness");
        _pauseOnMediaCheckBox.Text = LocalizationManager.GetString("PauseOnMedia");
        _pauseOnFullscreenCheckBox.Text = LocalizationManager.GetString("PauseOnFullscreen");
        _autoStartCheckBox.Text = LocalizationManager.GetString("AutoStart");

        UpdateTrayIconLanguage();
    }

    private void UpdateTrayIconLanguage()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Text = LocalizationManager.GetString("TrayTooltip");
        }

        if (_trayMenu != null && _trayMenu.Items.Count >= 2)
        {
            _trayMenu.Items[0].Text = LocalizationManager.GetString("ShowMainWindow");
            _trayMenu.Items[1].Text = LocalizationManager.GetString("Exit");
        }
    }

    private void RefreshMonitorPanel()
    {
        _suppressMonitorSliderEvents = true;
        _monitorPanel.SuspendLayout();
        try
        {
            foreach (Control ctrl in _monitorPanel.Controls)
            {
                ctrl.Dispose();
            }
            _monitorPanel.Controls.Clear();
            _monitorRows.Clear();

            var monitors = _monitorService.Monitors;
            if (monitors.Count == 0)
            {
                var noMonLabel = new Label
                {
                    Text = LocalizationManager.GetString("NoMonitorDetected"),
                    Location = new Point(10, 26),
                    Size = new Size(375, 30),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Gray
                };
                _monitorPanel.Controls.Add(noMonLabel);
                _monitorPanel.AutoScrollMinSize = Size.Empty;
                return;
            }

            int y = 6;
            for (int i = 0; i < monitors.Count; i++)
            {
                var row = CreateMonitorRow(monitors[i], y, i);
                _monitorRows.Add(row);
                y += 46;
            }

            _monitorPanel.AutoScrollMinSize = new Size(0, y);
        }
        finally
        {
            _monitorPanel.ResumeLayout();
            _suppressMonitorSliderEvents = false;
        }
    }

    private MonitorControlRow CreateMonitorRow(MonitorInfo monitor, int y, int rowIndex)
    {
        string primaryTag = monitor.IsPrimary ? $" {LocalizationManager.GetString("PrimaryTag")}" : "";
        var nameLabel = new Label
        {
            Text = $"{LocalizationManager.GetString("Monitor")} {monitor.Index}{primaryTag}",
            Location = new Point(4, y + 6),
            Size = new Size(106, 20),
            AutoEllipsis = true,
            Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold)
        };
        _toolTip.SetToolTip(nameLabel, monitor.GetDisplayLabel());
        _monitorPanel.Controls.Add(nameLabel);

        var trackBar = new TrackBar
        {
            Location = new Point(112, y),
            Size = new Size(216, 40),
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(monitor.CurrentBrightness, 0, 100),
            TickFrequency = 10,
            Tag = rowIndex
        };
        trackBar.ValueChanged += OnBrightnessTrackBarScroll;
        _monitorPanel.Controls.Add(trackBar);

        var percentLabel = new Label
        {
            Text = $"{monitor.CurrentBrightness}%",
            Location = new Point(332, y + 6),
            Size = new Size(46, 20),
            TextAlign = ContentAlignment.MiddleRight
        };
        _monitorPanel.Controls.Add(percentLabel);

        return new MonitorControlRow
        {
            Monitor = monitor,
            NameLabel = nameLabel,
            TrackBar = trackBar,
            PercentLabel = percentLabel
        };
    }

    /// <summary>
    /// 始终以 MonitorService 最新显示器实例的真实物理亮度刷新所有顶部滑条与百分比显示
    /// </summary>
    private void SyncMonitorSlidersFromService()
    {
        var monitors = _monitorService.Monitors;
        if (_monitorRows.Count != monitors.Count)
        {
            RefreshMonitorPanel();
            UpdateWindowTitle();
            return;
        }

        _suppressMonitorSliderEvents = true;
        try
        {
            for (int i = 0; i < monitors.Count; i++)
            {
                var monitor = monitors[i];
                var row = _monitorRows[i];
                row.Monitor = monitor;

                int val = Math.Clamp(monitor.CurrentBrightness, 0, 100);
                if (row.TrackBar.Value != val)
                {
                    row.TrackBar.Value = val;
                }
                row.PercentLabel.Text = $"{val}%";
            }
        }
        finally
        {
            _suppressMonitorSliderEvents = false;
        }
    }

    /// <summary>
    /// 使用最新覆盖（Latest-Wins）策略调度 DDC/CI 下发：
    /// 空闲时 0 常驻线程/定时器；高频拖动滑条时自动合并中间帧，保证最新操作（尤其是顶部滑条）绝对优先
    /// </summary>
    private void EnqueueDdcWork(long version, Action work)
    {
        bool startWorker = false;
        lock (_ddcQueueLock)
        {
            _pendingDdcWork = (version, work);
            if (!_ddcWorkerRunning)
            {
                _ddcWorkerRunning = true;
                startWorker = true;
            }
        }

        if (startWorker)
        {
            _ = Task.Run(ProcessDdcQueueLoop);
        }
    }

    private void ProcessDdcQueueLoop()
    {
        while (true)
        {
            (long Version, Action Work) next;
            lock (_ddcQueueLock)
            {
                if (_pendingDdcWork == null)
                {
                    _ddcWorkerRunning = false;
                    return;
                }
                next = _pendingDdcWork.Value;
                _pendingDdcWork = null;
            }

            // 已被更新的手动滑条或热键指令取代时直接跳过，避免旧指令覆盖新亮度
            if (next.Version != Volatile.Read(ref _brightnessCommandVersion))
                continue;

            try
            {
                next.Work();
            }
            catch
            {
                // 静默处理 DDC 瞬态异常
            }
        }
    }

    private void SetupTrayIcon()
    {
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add(LocalizationManager.GetString("ShowMainWindow"), null, (_, _) => ShowMainWindow());
        _trayMenu.Items.Add(LocalizationManager.GetString("Exit"), null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Icon = _appIcon ?? SystemIcons.Application,
            Text = LocalizationManager.GetString("TrayTooltip"),
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    /// <summary>
    /// 优先从内嵌资源加载图标（确保单文件 EXE 独立运行，不依赖外部 64x64.ico）
    /// </summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("LightBrightnessControl.Resources.64x64.ico");
            if (stream != null)
            {
                return new Icon(stream);
            }

            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            {
                var associated = Icon.ExtractAssociatedIcon(processPath);
                if (associated != null)
                    return associated;
            }
        }
        catch
        {
            // 静默回退
        }

        return SystemIcons.Application;
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        SyncMonitorSlidersFromService();

        // 打开主界面时在后台轻量读取一次硬件真实亮度（若期间用户拖动滑条或按热键则自动放弃回写）
        long versionAtOpen = Volatile.Read(ref _brightnessCommandVersion);
        _ = Task.Run(() =>
        {
            _monitorService.RefreshAllBrightness();
            if (versionAtOpen == Volatile.Read(ref _brightnessCommandVersion))
            {
                InvokeOnUiSafe(SyncMonitorSlidersFromService);
            }
        });
    }

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        SaveSettings();
        Application.Exit();
    }

    #region 事件处理

    /// <summary>
    /// 顶部显示器滑条事件 —— 拥有全软件最高亮度修改优先级：
    /// 1. 立即递增指令版本号，作废一切未完成的旧热键/闲置恢复/后台刷新任务；
    /// 2. 被拖动的滑条严格按用户指定值直接下发真实物理 DDC 亮度，绝不被校准曲线改写或回弹；
    /// 3. 同步模式下其余显示器按校准曲线联动，且所有滑条始终显示各自真实的物理亮度百分比。
    /// </summary>
    private void OnBrightnessTrackBarScroll(object? sender, EventArgs e)
    {
        if (_suppressMonitorSliderEvents || sender is not TrackBar trackBar) return;

        int index = (int)trackBar.Tag!;
        int rawBrightness = Math.Clamp(trackBar.Value, 0, 100);
        var monitors = _monitorService.Monitors;
        if (index < 0 || index >= monitors.Count) return;

        long version = Interlocked.Increment(ref _brightnessCommandVersion);

        if (_syncCheckBox.Checked && monitors.Count > 1)
        {
            var draggedMonitor = monitors[index];
            int inferredTarget = draggedMonitor.CalibrationCurve.InverseEvaluate(rawBrightness);

            // 为每台显示器计算真实物理目标亮度：被拖动屏严格等于 rawBrightness，其余屏按曲线联动
            var plan = new List<(string Key, int Index, int RawBrightness)>(monitors.Count);
            for (int i = 0; i < monitors.Count; i++)
            {
                int targetRaw = i == index
                    ? rawBrightness
                    : monitors[i].CalibrationCurve.Evaluate(inferredTarget);
                plan.Add((monitors[i].CalibrationKey, i, targetRaw));
                _preDimMonitorBrightness[monitors[i].CalibrationKey] = targetRaw;
            }

            // 立即更新界面上所有滑条与百分比为各显示器即将下发的真实物理亮度
            _suppressMonitorSliderEvents = true;
            try
            {
                for (int i = 0; i < Math.Min(plan.Count, _monitorRows.Count); i++)
                {
                    int val = plan[i].RawBrightness;
                    if (i != index && _monitorRows[i].TrackBar.Value != val)
                    {
                        _monitorRows[i].TrackBar.Value = val;
                    }
                    _monitorRows[i].PercentLabel.Text = $"{val}%";
                }
            }
            finally
            {
                _suppressMonitorSliderEvents = false;
            }

            EnqueueDdcWork(version, () =>
            {
                var currentMonitors = _monitorService.Monitors;
                bool allOk = currentMonitors.Count > 0;
                foreach (var item in plan)
                {
                    if (version != Volatile.Read(ref _brightnessCommandVersion))
                        return;

                    var target = currentMonitors.FirstOrDefault(m =>
                        string.Equals(m.CalibrationKey, item.Key, StringComparison.OrdinalIgnoreCase))
                        ?? (item.Index < currentMonitors.Count ? currentMonitors[item.Index] : null);

                    if (target == null || !target.SetRawBrightness(item.RawBrightness))
                    {
                        allOk = false;
                    }
                }

                if (version == Volatile.Read(ref _brightnessCommandVersion))
                {
                    if (allOk)
                    {
                        InvokeOnUiSafe(SyncMonitorSlidersFromService);
                    }
                    else
                    {
                        InvokeOnUiSafe(() => TriggerLazyRecoveryAndRetryPlan(version, plan));
                    }
                }
            });
        }
        else
        {
            // 独立模式：直接控制该显示器的真实物理 DDC 亮度（最高优先级，不经过校准曲线）
            var targetMonitor = monitors[index];
            string key = targetMonitor.CalibrationKey;
            _preDimMonitorBrightness[key] = rawBrightness;

            if (index < _monitorRows.Count)
            {
                _monitorRows[index].PercentLabel.Text = $"{rawBrightness}%";
            }

            var singlePlan = new List<(string Key, int Index, int RawBrightness)>
            {
                (key, index, rawBrightness)
            };

            EnqueueDdcWork(version, () =>
            {
                bool ok = _monitorService.SetBrightness(index, rawBrightness, applyCalibration: false);
                if (version == Volatile.Read(ref _brightnessCommandVersion))
                {
                    if (ok)
                    {
                        InvokeOnUiSafe(SyncMonitorSlidersFromService);
                    }
                    else
                    {
                        InvokeOnUiSafe(() => TriggerLazyRecoveryAndRetryPlan(version, singlePlan));
                    }
                }
            });
        }
    }

    /// <summary>
    /// 懒刷新兜底：当 DDC 调用因句柄失效失败时，自动重枚举并按指定物理亮度计划重试（若已被更新操作取代则放弃）
    /// </summary>
    private async void TriggerLazyRecoveryAndRetryPlan(long version, List<(string Key, int Index, int RawBrightness)> plan)
    {
        if (_lazyRecoveryInFlight || version != Volatile.Read(ref _brightnessCommandVersion))
            return;

        _lazyRecoveryInFlight = true;
        try
        {
            await RefreshMonitorsAsync();
            if (IsDisposed || version != Volatile.Read(ref _brightnessCommandVersion))
                return;

            var monitors = _monitorService.Monitors;
            await Task.Run(() =>
            {
                foreach (var item in plan)
                {
                    if (version != Volatile.Read(ref _brightnessCommandVersion))
                        return;

                    var target = monitors.FirstOrDefault(m =>
                        string.Equals(m.CalibrationKey, item.Key, StringComparison.OrdinalIgnoreCase))
                        ?? (item.Index < monitors.Count ? monitors[item.Index] : null);

                    target?.SetRawBrightness(item.RawBrightness);
                }
            });

            if (!IsDisposed && version == Volatile.Read(ref _brightnessCommandVersion))
            {
                SyncMonitorSlidersFromService();
            }
        }
        catch { }
        finally
        {
            _lazyRecoveryInFlight = false;
        }
    }

    private async void TriggerLazyRecoveryAndRetryAll(long version, int targetBrightness)
    {
        if (_lazyRecoveryInFlight || version != Volatile.Read(ref _brightnessCommandVersion))
            return;

        _lazyRecoveryInFlight = true;
        try
        {
            await RefreshMonitorsAsync();
            if (IsDisposed || version != Volatile.Read(ref _brightnessCommandVersion))
                return;

            if (_monitorService.Monitors.Count > 0)
            {
                await Task.Run(() => _monitorService.SetAllBrightness(targetBrightness, applyCalibration: true));
                if (!IsDisposed && version == Volatile.Read(ref _brightnessCommandVersion))
                {
                    SyncMonitorSlidersFromService();
                }
            }
        }
        catch { }
        finally
        {
            _lazyRecoveryInFlight = false;
        }
    }

    private void OnSyncCheckChanged(object? sender, EventArgs e)
    {
        _settingsManager.Settings.SyncBrightness = _syncCheckBox.Checked;
    }

    private async void OnRefreshMonitorsClick(object? sender, EventArgs e)
    {
        _refreshButton.Enabled = false;
        try
        {
            await RefreshMonitorsAsync();
        }
        finally
        {
            if (!IsDisposed)
                _refreshButton.Enabled = true;
        }
    }

    private void OnCalibrateClick(object? sender, EventArgs e)
    {
        var presetValues = _hotkeyRows
            .Select(r => (int)r.BrightnessNumeric.Value)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        using var dialog = new CalibrationDialog(_monitorService.Monitors.ToList(), presetValues);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            foreach (var monitor in _monitorService.Monitors)
            {
                if (dialog.ResultCurves.TryGetValue(monitor.CalibrationKey, out var curve))
                {
                    monitor.CalibrationCurve = curve;
                    _settingsManager.SetMonitorCurve(monitor.CalibrationKey, curve);
                }
            }
            SaveSettings();
        }

        // 无论确定还是取消（取消会恢复预览前亮度），都立即同步顶部滑条为各屏真实亮度
        SyncMonitorSlidersFromService();
    }

    #region 动态预设事件

    private void OnAddPresetClick(object? sender, EventArgs e)
    {
        if (_hotkeyRows.Count >= MaxPresetCount) return;

        var items = CollectPresetItemsFromUi();
        int newBrightness = PickSmartNewPresetBrightness(items);
        var newItem = new HotkeySettingItem
        {
            BrightnessValue = newBrightness,
            Modifiers = 0,
            Key = 0
        };
        items.Add(newItem);

        SortAndRebuildPresets(items, focusItem: newItem);
    }

    /// <summary>
    /// 在现有预设值的最大间隔中自动挑选一个新亮度值，减少手动改数成本
    /// </summary>
    private static int PickSmartNewPresetBrightness(List<HotkeySettingItem> items)
    {
        if (items.Count == 0) return 50;

        var sorted = items.Select(i => i.BrightnessValue).Distinct().OrderBy(v => v).ToList();
        int bestGap = sorted[0];
        int candidate = sorted[0] / 2;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            int gap = sorted[i + 1] - sorted[i];
            if (gap > bestGap)
            {
                bestGap = gap;
                candidate = sorted[i] + gap / 2;
            }
        }

        int topGap = 100 - sorted[^1];
        if (topGap > bestGap)
        {
            candidate = sorted[^1] + (topGap + 1) / 2;
        }

        return Math.Clamp(candidate, 0, 100);
    }

    private void OnDeletePresetClick(object? sender, EventArgs e)
    {
        if (_hotkeyRows.Count <= MinPresetCount) return;
        if (sender is not Button button || button.Tag is not HotkeyRow targetRow) return;

        int removeIdx = _hotkeyRows.IndexOf(targetRow);
        if (removeIdx < 0) return;

        var items = CollectPresetItemsFromUi();
        items.RemoveAt(removeIdx);
        SortAndRebuildPresets(items);
    }

    private void OnHotkeyTextBoxClick(object? sender, EventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.Text = LocalizationManager.GetString("PressHotkey");
            textBox.Focus();
        }
    }

    private void OnHotkeyTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not HotkeyRow row) return;

        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
            return;

        uint modifiers = 0;
        if (e.Control) modifiers |= NativeMethods.MOD_CONTROL;
        if (e.Alt) modifiers |= NativeMethods.MOD_ALT;
        if (e.Shift) modifiers |= NativeMethods.MOD_SHIFT;

        row.Modifiers = modifiers;
        row.Key = (uint)e.KeyCode;
        textBox.Text = GetHotkeyDisplayText(modifiers, (uint)e.KeyCode);

        RegisterHotkeys();
        SaveSettings();

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void OnClearHotkeyClick(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not HotkeyRow row) return;

        row.Modifiers = 0;
        row.Key = 0;
        row.TextBox.Text = LocalizationManager.GetString("ClickToSet");

        RegisterHotkeys();
        SaveSettings();
    }

    private void OnBrightnessValueChanged(object? sender, EventArgs e)
    {
        if (_suppressPresetEvents) return;
        // 数值变化时立刻更新热键目标值，但不立即重排行（方案 1-S1）
        RegisterHotkeys();
    }

    private void OnPresetBrightnessKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            CommitPresetOrderIfNeeded();
        }
    }

    private void OnPresetBrightnessCommit(object? sender, EventArgs e)
    {
        if (_suppressPresetEvents) return;
        CommitPresetOrderIfNeeded();
    }

    private void CommitPresetOrderIfNeeded()
    {
        bool alreadySorted = true;
        for (int i = 0; i < _hotkeyRows.Count - 1; i++)
        {
            if (_hotkeyRows[i].BrightnessNumeric.Value > _hotkeyRows[i + 1].BrightnessNumeric.Value)
            {
                alreadySorted = false;
                break;
            }
        }

        if (!alreadySorted)
        {
            SortAndRebuildPresets();
        }
        else
        {
            SaveSettings();
        }
    }

    private static string GetHotkeyDisplayText(uint modifiers, uint key)
    {
        var parts = new List<string>();
        if ((modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
        if (key != 0) parts.Add(((Keys)key).ToString());

        return parts.Count > 0 ? string.Join(" + ", parts) : LocalizationManager.GetString("ClickToSet");
    }

    private void RegisterHotkeys()
    {
        if (_hotkeyManager == null) return;

        _hotkeyManager.UnregisterAllHotkeys();
        foreach (var row in _hotkeyRows)
        {
            if (row.Key != 0)
            {
                _hotkeyManager.RegisterHotkey(row.Modifiers, row.Key, (int)row.BrightnessNumeric.Value);
            }
        }
    }

    private void OnHotkeyPressed(int brightness)
    {
        long version = Interlocked.Increment(ref _brightnessCommandVersion);
        var monitors = _monitorService.Monitors;

        // 立即在顶部滑条预显示每台显示器经校准曲线后的真实物理亮度（0ms 视觉反馈）
        if (_monitorRows.Count == monitors.Count)
        {
            _suppressMonitorSliderEvents = true;
            try
            {
                for (int i = 0; i < monitors.Count; i++)
                {
                    int actualRaw = monitors[i].CalibrationCurve.Evaluate(brightness);
                    _monitorRows[i].Monitor = monitors[i];
                    _monitorRows[i].TrackBar.Value = actualRaw;
                    _monitorRows[i].PercentLabel.Text = $"{actualRaw}%";
                    _preDimMonitorBrightness[monitors[i].CalibrationKey] = actualRaw;
                }
            }
            finally
            {
                _suppressMonitorSliderEvents = false;
            }
        }

        // 通过最新覆盖队列异步下发 DDC，完成后再以 MonitorService 确认后的真实值同步 UI
        EnqueueDdcWork(version, () =>
        {
            bool ok = _monitorService.SetAllBrightness(brightness, applyCalibration: true);
            if (version == Volatile.Read(ref _brightnessCommandVersion))
            {
                if (ok)
                {
                    InvokeOnUiSafe(SyncMonitorSlidersFromService);
                }
                else
                {
                    InvokeOnUiSafe(() => TriggerLazyRecoveryAndRetryAll(version, brightness));
                }
            }
        });
    }

    #endregion

    #region 闲置变暗与自启动

    private void OnIdleDimmingChanged(object? sender, EventArgs e)
    {
        bool enabled = _idleDimmingCheckBox.Checked;

        _idleTimeNumeric.Enabled = enabled;
        _idleBrightnessNumeric.Enabled = enabled;
        _pauseOnMediaCheckBox.Enabled = enabled;
        _pauseOnFullscreenCheckBox.Enabled = enabled;

        if (enabled)
            StartIdleWatcher();
        else
            StopIdleWatcher();
    }

    private void OnIdleSettingsChanged(object? sender, EventArgs e)
    {
        _idleWatcher?.UpdateThreshold((int)_idleTimeNumeric.Value);
    }

    private void StartIdleWatcher()
    {
        if (_idleWatcher != null) return;

        _idleWatcher = new IdleWatcher
        {
            ShouldPauseCheck = ShouldPauseIdleDimming
        };
        _idleWatcher.IdleEntered += OnIdleEntered;
        _idleWatcher.IdleExited += OnIdleExited;
        _idleWatcher.Start((int)_idleTimeNumeric.Value);
    }

    private void StopIdleWatcher()
    {
        if (_idleWatcher == null) return;

        _idleWatcher.Dispose();
        _idleWatcher = null;
    }

    private bool ShouldPauseIdleDimming()
    {
        try
        {
            if (_pauseOnMediaCheckBox.Checked && MediaDetector.IsMediaPlaying())
                return true;

            if (_pauseOnFullscreenCheckBox.Checked && FullscreenDetector.IsFullscreenAppRunning())
                return true;
        }
        catch { }

        return false;
    }

    private void OnIdleEntered(object? sender, EventArgs e)
    {
        try
        {
            long version = Interlocked.Increment(ref _brightnessCommandVersion);
            _preDimMonitorBrightness.Clear();
            var monitors = _monitorService.Monitors;
            foreach (var m in monitors)
            {
                _preDimMonitorBrightness[m.CalibrationKey] = m.CurrentBrightness;
            }
            if (monitors.Count > 0)
            {
                _preDimFallbackBrightness = monitors[0].CurrentBrightness;
            }

            int targetBrightness = (int)_idleBrightnessNumeric.Value;
            EnqueueDdcWork(version, () =>
            {
                bool ok = _monitorService.SetAllBrightness(targetBrightness, applyCalibration: true);
                if (version == Volatile.Read(ref _brightnessCommandVersion))
                {
                    InvokeOnUiSafe(SyncMonitorSlidersFromService);
                    if (!ok)
                    {
                        InvokeOnUiSafe(() => TriggerLazyRecoveryAndRetryAll(version, targetBrightness));
                    }
                }
            });
        }
        catch { }
    }

    private void OnIdleExited(object? sender, EventArgs e)
    {
        try
        {
            long version = Interlocked.Increment(ref _brightnessCommandVersion);
            var monitors = _monitorService.Monitors;
            var restorePlan = new List<(string Key, int Index, int RawBrightness)>(monitors.Count);
            for (int i = 0; i < monitors.Count; i++)
            {
                var m = monitors[i];
                int restoreRaw = _preDimMonitorBrightness.TryGetValue(m.CalibrationKey, out int saved)
                    ? saved
                    : m.CalibrationCurve.Evaluate(_preDimFallbackBrightness);
                restorePlan.Add((m.CalibrationKey, i, restoreRaw));
            }

            EnqueueDdcWork(version, () =>
            {
                bool allOk = restorePlan.Count > 0;
                var current = _monitorService.Monitors;
                foreach (var item in restorePlan)
                {
                    if (version != Volatile.Read(ref _brightnessCommandVersion))
                        return;

                    var target = current.FirstOrDefault(m =>
                        string.Equals(m.CalibrationKey, item.Key, StringComparison.OrdinalIgnoreCase))
                        ?? (item.Index < current.Count ? current[item.Index] : null);

                    if (target == null || !target.SetRawBrightness(item.RawBrightness))
                    {
                        allOk = false;
                    }
                }

                if (version == Volatile.Read(ref _brightnessCommandVersion))
                {
                    InvokeOnUiSafe(SyncMonitorSlidersFromService);
                    if (!allOk)
                    {
                        InvokeOnUiSafe(() => TriggerLazyRecoveryAndRetryPlan(version, restorePlan));
                    }
                }
            });
        }
        catch { }
    }

    private void OnAutoStartChanged(object? sender, EventArgs e)
    {
        _settingsManager.SetAutoStart(_autoStartCheckBox.Checked);
    }

    #endregion

    #endregion

    #region 窗口生命周期

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            _hotkeyManager?.ProcessHotkey(m.WParam.ToInt32());
        }
        else
        {
            _displayWatcher?.ProcessWindowMessage(ref m);
        }

        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
        else
        {
            CleanupResources();
        }

        base.OnFormClosing(e);
    }

    private void HideToTray()
    {
        CommitPresetOrderIfNeeded();
        Hide();
        SaveSettings();
        TrimMemory();
    }

    private static void TrimMemory()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            NativeMethods.SetProcessWorkingSetSize(
                NativeMethods.GetCurrentProcess(),
                new IntPtr(-1),
                new IntPtr(-1));
        }
        catch { }
    }

    private void CleanupResources()
    {
        _displayWatcher?.Dispose();
        _hotkeyManager?.Dispose();
        _idleWatcher?.Dispose();
        _monitorService.Dispose();
        _trayIcon?.Dispose();
        _trayMenu?.Dispose();
        _toolTip?.Dispose();
        _appIcon?.Dispose();
    }

    #endregion

    #region 内部类

    private sealed class MonitorControlRow
    {
        public MonitorInfo Monitor { get; set; } = null!;
        public Label NameLabel { get; set; } = null!;
        public TrackBar TrackBar { get; set; } = null!;
        public Label PercentLabel { get; set; } = null!;
    }

    private sealed class HotkeyRow
    {
        public int Index { get; set; }
        public Label Label { get; set; } = null!;
        public TextBox TextBox { get; set; } = null!;
        public NumericUpDown BrightnessNumeric { get; set; } = null!;
        public Button ClearButton { get; set; } = null!;
        public Button DeleteButton { get; set; } = null!;
        public uint Modifiers { get; set; }
        public uint Key { get; set; }
    }

    #endregion
}
