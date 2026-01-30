namespace LightBrightnessControl;

/// <summary>
/// 主窗体 - 纯代码构建UI，不使用 Designer.cs
/// 遵循极致轻量原则，使用系统原生控件样式
/// </summary>
public sealed class MainForm : Form
{
    #region 服务与管理器

    private readonly MonitorService _monitorService;
    private readonly SettingsManager _settingsManager;
    private HotkeyManager? _hotkeyManager;
    private IdleWatcher? _idleWatcher;

    #endregion

    #region UI 控件

    // 显示器控制区域
    private GroupBox _monitorGroupBox = null!;
    private Panel _monitorPanel = null!;
    private CheckBox _syncCheckBox = null!;
    private Button _calibrateButton = null!;
    private readonly List<MonitorControlRow> _monitorRows = new();

    // 快捷键设置区域
    private GroupBox _hotkeyGroupBox = null!;
    private readonly List<HotkeyRow> _hotkeyRows = new();
    private readonly List<Label> _hotkeyLabels = new();

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

    // 语言切换
    private Label _languageLabel = null!;
    private ComboBox _languageComboBox = null!;

    // 系统托盘
    private NotifyIcon _trayIcon = null!;
    private ContextMenuStrip _trayMenu = null!;

    #endregion

    #region 闲置变暗状态

    private int _preDimBrightness = 100; // 变暗前的亮度值

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

    /// <summary>
    /// 纯代码构建UI
    /// </summary>
    private void InitializeComponent()
    {
        // 窗体基本设置
        Text = LocalizationManager.GetString("AppTitle");
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(450, 560);
        Icon = CreateDefaultIcon();

        int y = 10;

        // ========== 语言切换区域 ==========
        _languageLabel = new Label
        {
            Text = LocalizationManager.GetString("Language"),
            Location = new Point(10, y + 3),
            AutoSize = true
        };
        Controls.Add(_languageLabel);

        _languageComboBox = new ComboBox
        {
            Location = new Point(120, y),
            Size = new Size(100, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _languageComboBox.Items.Add("中文");
        _languageComboBox.Items.Add("English");
        _languageComboBox.SelectedIndex = LocalizationManager.IsChinese ? 0 : 1;
        _languageComboBox.SelectedIndexChanged += OnLanguageChanged;
        Controls.Add(_languageComboBox);

        y += 35;

        // ========== 显示器控制区域 ==========
        _monitorGroupBox = new GroupBox
        {
            Text = LocalizationManager.GetString("MonitorBrightness"),
            Location = new Point(10, y),
            Size = new Size(415, 150),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _monitorPanel = new Panel
        {
            Location = new Point(10, 20),
            Size = new Size(395, 90),
            AutoScroll = true
        };
        _monitorGroupBox.Controls.Add(_monitorPanel);

        // 同步控制复选框
        _syncCheckBox = new CheckBox
        {
            Text = LocalizationManager.GetString("SyncAllMonitors"),
            Location = new Point(10, 115),
            AutoSize = true
        };
        _syncCheckBox.CheckedChanged += OnSyncCheckChanged;
        _monitorGroupBox.Controls.Add(_syncCheckBox);

        // 校准按钮
        _calibrateButton = new Button
        {
            Text = LocalizationManager.GetString("Calibration"),
            Location = new Point(280, 112),
            Size = new Size(120, 25)
        };
        _calibrateButton.Click += OnCalibrateClick;
        _monitorGroupBox.Controls.Add(_calibrateButton);

        Controls.Add(_monitorGroupBox);
        y += 160;

        // ========== 快捷键设置区域 ==========
        _hotkeyGroupBox = new GroupBox
        {
            Text = LocalizationManager.GetString("GlobalHotkeys"),
            Location = new Point(10, y),
            Size = new Size(415, 140)
        };

        int hotkeyY = 20;
        int[] defaultBrightness = { 0, 30, 60, 100 };

        for (int i = 0; i < 4; i++)
        {
            var row = CreateHotkeyRow(i, defaultBrightness[i], hotkeyY);
            _hotkeyRows.Add(row);
            hotkeyY += 28;
        }

        Controls.Add(_hotkeyGroupBox);
        y += 150;

        // ========== 自动化设置区域 ==========
        _automationGroupBox = new GroupBox
        {
            Text = LocalizationManager.GetString("AutomationStrategies"),
            Location = new Point(10, y),
            Size = new Size(415, 160)
        };

        int autoY = 22;

        // 闲置变暗
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
            Location = new Point(130, autoY + 2),
            AutoSize = true
        };
        _automationGroupBox.Controls.Add(_idleTimeLabel);

        _idleTimeNumeric = new NumericUpDown
        {
            Location = new Point(230, autoY),
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
            Location = new Point(350, autoY),
            Size = new Size(50, 23),
            Minimum = 0,
            Maximum = 100,
            Value = 10,
            Enabled = false
        };
        _automationGroupBox.Controls.Add(_idleBrightnessNumeric);

        autoY += 30;

        // 媒体播放时不执行
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

        // 全屏时不执行
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

        // 分隔线
        var separator = new Label
        {
            BorderStyle = BorderStyle.Fixed3D,
            Location = new Point(10, autoY),
            Size = new Size(395, 2)
        };
        _automationGroupBox.Controls.Add(separator);
        autoY += 10;

        // 开机自启动
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
    /// 创建快捷键设置行
    /// </summary>
    private HotkeyRow CreateHotkeyRow(int index, int defaultBrightness, int y)
    {
        var label = new Label
        {
            Text = $"{LocalizationManager.GetString("Preset")} {index + 1}:",
            Location = new Point(10, y + 3),
            AutoSize = true,
            Tag = $"Preset_{index}" // 用于语言切换时识别
        };
        _hotkeyGroupBox.Controls.Add(label);
        _hotkeyLabels.Add(label);

        var textBox = new TextBox
        {
            Location = new Point(70, y),
            Size = new Size(150, 23),
            ReadOnly = true,
            Text = LocalizationManager.GetString("ClickToSet"),
            Tag = index
        };
        textBox.Click += OnHotkeyTextBoxClick;
        textBox.KeyDown += OnHotkeyTextBoxKeyDown;
        _hotkeyGroupBox.Controls.Add(textBox);

        var numericUpDown = new NumericUpDown
        {
            Location = new Point(230, y),
            Size = new Size(55, 23),
            Minimum = 0,
            Maximum = 100,
            Value = defaultBrightness
        };
        numericUpDown.ValueChanged += OnBrightnessValueChanged;
        _hotkeyGroupBox.Controls.Add(numericUpDown);

        var percentLabel = new Label
        {
            Text = "%",
            Location = new Point(290, y + 3),
            AutoSize = true
        };
        _hotkeyGroupBox.Controls.Add(percentLabel);

        var clearButton = new Button
        {
            Text = LocalizationManager.GetString("Clear"),
            Location = new Point(310, y),
            Size = new Size(50, 23),
            Tag = index
        };
        clearButton.Click += OnClearHotkeyClick;
        _hotkeyGroupBox.Controls.Add(clearButton);

        return new HotkeyRow
        {
            Index = index,
            TextBox = textBox,
            BrightnessNumeric = numericUpDown,
            ClearButton = clearButton
        };
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        _settingsManager.Load();
        var settings = _settingsManager.Settings;

        // 先加载语言设置（需要在 UI 更新之前）
        LocalizationManager.SetLanguage(settings.Language);
        _languageComboBox.SelectedIndex = LocalizationManager.IsChinese ? 0 : 1;

        _syncCheckBox.Checked = settings.SyncBrightness;
        _idleDimmingCheckBox.Checked = settings.EnableIdleDimming;
        _idleTimeNumeric.Value = Math.Clamp(settings.IdleTimeMinutes, 1, 60);
        _idleBrightnessNumeric.Value = Math.Clamp(settings.IdleBrightness, 0, 100);
        _pauseOnMediaCheckBox.Checked = settings.PauseOnMediaPlay;
        _pauseOnFullscreenCheckBox.Checked = settings.PauseOnFullscreen;
        _autoStartCheckBox.Checked = _settingsManager.IsAutoStartEnabled();

        // 加载热键配置
        for (int i = 0; i < Math.Min(settings.Hotkeys.Count, _hotkeyRows.Count); i++)
        {
            var hotkeyConfig = settings.Hotkeys[i];
            _hotkeyRows[i].BrightnessNumeric.Value = hotkeyConfig.BrightnessValue;
            _hotkeyRows[i].Modifiers = hotkeyConfig.Modifiers;
            _hotkeyRows[i].Key = hotkeyConfig.Key;

            if (hotkeyConfig.Key != 0)
            {
                _hotkeyRows[i].TextBox.Text = GetHotkeyDisplayText(hotkeyConfig.Modifiers, hotkeyConfig.Key);
            }
        }

        // 更新界面语言
        UpdateUILanguage();
    }

    /// <summary>
    /// 保存设置
    /// </summary>
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

        // 保存热键配置
        settings.Hotkeys.Clear();
        foreach (var row in _hotkeyRows)
        {
            settings.Hotkeys.Add(new HotkeySettingItem
            {
                BrightnessValue = (int)row.BrightnessNumeric.Value,
                Modifiers = row.Modifiers,
                Key = row.Key
            });
        }

        // 保存显示器比例
        foreach (var monitor in _monitorService.Monitors)
        {
            settings.MonitorRatios[monitor.DeviceName] = monitor.BrightnessRatio;
        }

        _settingsManager.Save();
    }

    /// <summary>
    /// 初始化显示器列表
    /// </summary>
    private void InitializeMonitors()
    {
        _monitorService.EnumerateMonitors();

        // 应用保存的比例设置
        foreach (var monitor in _monitorService.Monitors)
        {
            monitor.BrightnessRatio = _settingsManager.GetMonitorRatio(monitor.DeviceName);
        }

        RefreshMonitorPanel();

        // 更新窗口标题，显示检测状态
        int total = _monitorService.Monitors.Count;
        if (total > 0)
        {
            Text = LocalizationManager.GetString("AppTitleWithMonitors", total);
        }
        else
        {
            Text = LocalizationManager.GetString("AppTitle");
        }

        // 初始化热键管理器
        _hotkeyManager = new HotkeyManager(Handle);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        RegisterHotkeys();

        // 如果闲置变暗已启用，启动监视
        if (_idleDimmingCheckBox.Checked)
        {
            StartIdleWatcher();
        }
    }

    /// <summary>
    /// 语言切换事件处理
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        string newLanguage = _languageComboBox.SelectedIndex == 0 ? "zh-CN" : "en-US";
        LocalizationManager.SetLanguage(newLanguage);
        UpdateUILanguage();
        SaveSettings();
    }

    /// <summary>
    /// 更新界面所有文本为当前语言
    /// </summary>
    private void UpdateUILanguage()
    {
        // 窗口标题
        int total = _monitorService.Monitors.Count;
        if (total > 0)
        {
            Text = LocalizationManager.GetString("AppTitleWithMonitors", total);
        }
        else
        {
            Text = LocalizationManager.GetString("AppTitle");
        }

        // 显示器区域
        _monitorGroupBox.Text = LocalizationManager.GetString("MonitorBrightness");
        _syncCheckBox.Text = LocalizationManager.GetString("SyncAllMonitors");
        _calibrateButton.Text = LocalizationManager.GetString("Calibration");

        // 刷新显示器面板
        RefreshMonitorPanel();

        // 快捷键区域
        _hotkeyGroupBox.Text = LocalizationManager.GetString("GlobalHotkeys");

        // 更新快捷键行的标签和按钮
        for (int i = 0; i < _hotkeyLabels.Count; i++)
        {
            _hotkeyLabels[i].Text = $"{LocalizationManager.GetString("Preset")} {i + 1}:";
        }

        // 更新热键行的清除按钮和未设置的文本框
        foreach (var row in _hotkeyRows)
        {
            row.ClearButton.Text = LocalizationManager.GetString("Clear");
            if (row.Key == 0)
            {
                row.TextBox.Text = LocalizationManager.GetString("ClickToSet");
            }
        }

        // 自动化区域
        _automationGroupBox.Text = LocalizationManager.GetString("AutomationStrategies");
        _idleDimmingCheckBox.Text = LocalizationManager.GetString("EnableIdleDimming");
        _idleTimeLabel.Text = LocalizationManager.GetString("IdleTime");
        _idleBrightnessLabel.Text = LocalizationManager.GetString("TargetBrightness");
        _pauseOnMediaCheckBox.Text = LocalizationManager.GetString("PauseOnMedia");
        _pauseOnFullscreenCheckBox.Text = LocalizationManager.GetString("PauseOnFullscreen");
        _autoStartCheckBox.Text = LocalizationManager.GetString("AutoStart");

        // 托盘图标
        UpdateTrayIconLanguage();
    }

    /// <summary>
    /// 更新托盘图标语言
    /// </summary>
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

    /// <summary>
    /// 刷新显示器控制面板
    /// </summary>
    private void RefreshMonitorPanel()
    {
        _monitorPanel.Controls.Clear();
        _monitorRows.Clear();

        if (_monitorService.Monitors.Count == 0)
        {
            // 无检测到支持DDC的显示器
            var noMonLabel = new Label
            {
                Text = LocalizationManager.GetString("NoMonitorDetected"),
                Location = new Point(10, 10),
                Size = new Size(350, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray
            };
            _monitorPanel.Controls.Add(noMonLabel);
            return;
        }

        int y = 10;
        int rowIndex = 0;
        foreach (var monitor in _monitorService.Monitors)
        {
            var row = CreateMonitorRow(monitor, y, rowIndex);
            _monitorRows.Add(row);
            y += 60; // 参照工作代码的行高
            rowIndex++;
        }

        // 调整面板高度
        _monitorPanel.AutoScrollMinSize = new Size(0, y);
    }

    /// <summary>
    /// 创建显示器控制行
    /// </summary>
    private MonitorControlRow CreateMonitorRow(MonitorInfo monitor, int y, int rowIndex)
    {
        // 显示器名称标签
        var nameLabel = new Label
        {
            Text = $"{LocalizationManager.GetString("Monitor")} {monitor.Index}",
            Location = new Point(10, y + 3),
            Size = new Size(80, 20),
            Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold)
        };
        _monitorPanel.Controls.Add(nameLabel);

        // 百分比标签（放在滑条右边）
        var percentLabel = new Label
        {
            Text = $"{monitor.CurrentBrightness}%",
            Location = new Point(300, y + 3),
            Size = new Size(50, 20),
            TextAlign = ContentAlignment.MiddleRight
        };
        _monitorPanel.Controls.Add(percentLabel);

        // 亮度滑条
        var trackBar = new TrackBar
        {
            Location = new Point(95, y),
            Size = new Size(200, 45),
            Minimum = 0,
            Maximum = 100,
            Value = monitor.CurrentBrightness,
            TickFrequency = 10,
            Tag = rowIndex
        };
        trackBar.ValueChanged += OnBrightnessTrackBarScroll;
        _monitorPanel.Controls.Add(trackBar);

        return new MonitorControlRow
        {
            Monitor = monitor,
            NameLabel = nameLabel,
            TrackBar = trackBar,
            PercentLabel = percentLabel
        };
    }

    /// <summary>
    /// 截断显示器名称
    /// </summary>
    private static string TruncateName(string name, int maxLength)
    {
        if (name.Length <= maxLength)
            return name;
        return name[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// 设置系统托盘图标
    /// </summary>
    private void SetupTrayIcon()
    {
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add(LocalizationManager.GetString("ShowMainWindow"), null, (s, e) => ShowMainWindow());
        _trayMenu.Items.Add(LocalizationManager.GetString("Exit"), null, (s, e) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = LocalizationManager.GetString("TrayTooltip"),
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += (s, e) => ShowMainWindow();
    }

    /// <summary>
    /// 创建默认图标（优先使用自定义图标）
    /// </summary>
    private static Icon CreateDefaultIcon()
    {
        try
        {
            // 尝试加载自定义图标
            string? exeDir = Path.GetDirectoryName(AppContext.BaseDirectory);
            if (exeDir != null)
            {
                string iconPath = Path.Combine(exeDir, "64x64.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
            }
        }
        catch
        {
            // 加载失败时使用系统默认
        }
        
        // 使用系统应用程序图标作为后备
        return SystemIcons.Application;
    }

    /// <summary>
    /// 显示主窗口
    /// </summary>
    private void ShowMainWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    /// <summary>
    /// 退出应用程序
    /// </summary>
    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        SaveSettings();
        Application.Exit();
    }

    #region 事件处理

    /// <summary>
    /// 亮度滑条滚动事件
    /// </summary>
    private void OnBrightnessTrackBarScroll(object? sender, EventArgs e)
    {
        if (sender is not TrackBar trackBar) return;

        int index = (int)trackBar.Tag!;
        int brightness = trackBar.Value;

        if (_syncCheckBox.Checked)
        {
            // 同步模式：调整所有显示器
            _monitorService.SetAllBrightness(brightness);
            foreach (var row in _monitorRows)
            {
                row.TrackBar.Value = brightness;
                row.PercentLabel.Text = $"{brightness}%";
            }
        }
        else
        {
            // 独立模式：只调整当前显示器
            _monitorService.SetBrightness(index, brightness);
            _monitorRows[index].PercentLabel.Text = $"{brightness}%";
        }
    }

    /// <summary>
    /// 同步复选框改变事件
    /// </summary>
    private void OnSyncCheckChanged(object? sender, EventArgs e)
    {
        _settingsManager.Settings.SyncBrightness = _syncCheckBox.Checked;
    }

    /// <summary>
    /// 校准按钮点击事件
    /// </summary>
    private void OnCalibrateClick(object? sender, EventArgs e)
    {
        using var dialog = new CalibrationDialog(_monitorService.Monitors.ToList());
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            // 应用新的比例设置
            foreach (var monitor in _monitorService.Monitors)
            {
                if (dialog.Ratios.TryGetValue(monitor.DeviceName, out double ratio))
                {
                    monitor.BrightnessRatio = ratio;
                    _settingsManager.SetMonitorRatio(monitor.DeviceName, ratio);
                }
            }
            SaveSettings();
        }
    }

    /// <summary>
    /// 快捷键文本框点击事件
    /// </summary>
    private void OnHotkeyTextBoxClick(object? sender, EventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.Text = LocalizationManager.GetString("PressHotkey");
            textBox.Focus();
        }
    }

    /// <summary>
    /// 快捷键文本框按键事件
    /// </summary>
    private void OnHotkeyTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        int index = (int)textBox.Tag!;
        var row = _hotkeyRows[index];

        // 忽略单独的修饰键
        if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey ||
            e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
        {
            return;
        }

        // 转换修饰符
        uint modifiers = 0;
        if (e.Control) modifiers |= NativeMethods.MOD_CONTROL;
        if (e.Alt) modifiers |= NativeMethods.MOD_ALT;
        if (e.Shift) modifiers |= NativeMethods.MOD_SHIFT;

        row.Modifiers = modifiers;
        row.Key = (uint)e.KeyCode;

        textBox.Text = GetHotkeyDisplayText(modifiers, (uint)e.KeyCode);

        // 重新注册热键
        RegisterHotkeys();

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    /// <summary>
    /// 清除快捷键按钮点击
    /// </summary>
    private void OnClearHotkeyClick(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;

        int index = (int)button.Tag!;
        var row = _hotkeyRows[index];

        row.Modifiers = 0;
        row.Key = 0;
        row.TextBox.Text = LocalizationManager.GetString("ClickToSet");

        RegisterHotkeys();
    }

    /// <summary>
    /// 亮度百分比值修改事件 - 立即重新注册热键
    /// </summary>
    private void OnBrightnessValueChanged(object? sender, EventArgs e)
    {
        // 重新注册热键，使新的亮度值立即生效
        RegisterHotkeys();
    }

    /// <summary>
    /// 获取热键显示文本
    /// </summary>
    private static string GetHotkeyDisplayText(uint modifiers, uint key)
    {
        var parts = new List<string>();

        if ((modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");

        if (key != 0)
        {
            parts.Add(((Keys)key).ToString());
        }

        return parts.Count > 0 ? string.Join(" + ", parts) : "点击设置...";
    }

    /// <summary>
    /// 注册所有热键
    /// </summary>
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

    /// <summary>
    /// 热键触发事件
    /// </summary>
    private void OnHotkeyPressed(int brightness)
    {
        // 应用亮度
        _monitorService.SetAllBrightness(brightness);

        // 更新UI（如果可见）
        if (Visible)
        {
            foreach (var row in _monitorRows)
            {
                row.TrackBar.Value = brightness;
                row.PercentLabel.Text = $"{brightness}%";
            }
        }
    }

    /// <summary>
    /// 闲置变暗复选框改变事件
    /// </summary>
    private void OnIdleDimmingChanged(object? sender, EventArgs e)
    {
        bool enabled = _idleDimmingCheckBox.Checked;

        _idleTimeNumeric.Enabled = enabled;
        _idleBrightnessNumeric.Enabled = enabled;
        _pauseOnMediaCheckBox.Enabled = enabled;
        _pauseOnFullscreenCheckBox.Enabled = enabled;

        if (enabled)
        {
            StartIdleWatcher();
        }
        else
        {
            StopIdleWatcher();
        }
    }

    /// <summary>
    /// 闲置设置改变事件
    /// </summary>
    private void OnIdleSettingsChanged(object? sender, EventArgs e)
    {
        _idleWatcher?.UpdateThreshold((int)_idleTimeNumeric.Value);
    }

    /// <summary>
    /// 启动闲置监视
    /// </summary>
    private void StartIdleWatcher()
    {
        if (_idleWatcher != null) return;

        _idleWatcher = new IdleWatcher();
        _idleWatcher.ShouldPauseCheck = ShouldPauseIdleDimming;
        _idleWatcher.IdleEntered += OnIdleEntered;
        _idleWatcher.IdleExited += OnIdleExited;
        _idleWatcher.Start((int)_idleTimeNumeric.Value);
    }

    /// <summary>
    /// 停止闲置监视
    /// </summary>
    private void StopIdleWatcher()
    {
        if (_idleWatcher == null) return;

        _idleWatcher.Dispose();
        _idleWatcher = null;
    }

    /// <summary>
    /// 检查是否应该暂停闲置变暗
    /// </summary>
    private bool ShouldPauseIdleDimming()
    {
        try
        {
            // 检查媒体播放
            if (_pauseOnMediaCheckBox.Checked && MediaDetector.IsMediaPlaying())
                return true;

            // 检查全屏
            if (_pauseOnFullscreenCheckBox.Checked && FullscreenDetector.IsFullscreenAppRunning())
                return true;
        }
        catch
        {
            // 忽略检测异常，不暂停闲置变暗
        }

        return false;
    }

    /// <summary>
    /// 进入闲置状态
    /// </summary>
    private void OnIdleEntered(object? sender, EventArgs e)
    {
        try
        {
            // 记录当前亮度
            if (_monitorRows.Count > 0)
            {
                _preDimBrightness = _monitorRows[0].TrackBar.Value;
            }

            // 降低亮度
            int targetBrightness = (int)_idleBrightnessNumeric.Value;
            _monitorService.SetAllBrightness(targetBrightness);

            if (Visible)
            {
                foreach (var row in _monitorRows)
                {
                    row.TrackBar.Value = targetBrightness;
                    row.PercentLabel.Text = $"{targetBrightness}%";
                }
            }
        }
        catch
        {
            // 忽略异常，防止程序崩溃
        }
    }

    /// <summary>
    /// 离开闲置状态
    /// </summary>
    private void OnIdleExited(object? sender, EventArgs e)
    {
        try
        {
            // 恢复亮度
            _monitorService.SetAllBrightness(_preDimBrightness);

            if (Visible)
            {
                foreach (var row in _monitorRows)
                {
                    row.TrackBar.Value = _preDimBrightness;
                    row.PercentLabel.Text = $"{_preDimBrightness}%";
                }
            }
        }
        catch
        {
            // 忽略异常，防止程序崩溃
        }
    }

    /// <summary>
    /// 开机自启动复选框改变事件
    /// </summary>
    private void OnAutoStartChanged(object? sender, EventArgs e)
    {
        _settingsManager.SetAutoStart(_autoStartCheckBox.Checked);
    }

    #endregion

    #region 窗口生命周期

    /// <summary>
    /// 重写 WndProc 处理热键和显示器变化消息
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeMethods.WM_HOTKEY:
                _hotkeyManager?.ProcessHotkey(m.WParam.ToInt32());
                break;

            case NativeMethods.WM_DISPLAYCHANGE:
                // 显示器配置改变，重新枚举
                _monitorService.EnumerateMonitors();
                RefreshMonitorPanel();
                break;
        }

        base.WndProc(ref m);
    }

    /// <summary>
    /// 窗口关闭事件 - 最小化到托盘而非退出
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
        else
        {
            // 真正退出时释放资源
            CleanupResources();
        }

        base.OnFormClosing(e);
    }

    /// <summary>
    /// 隐藏到托盘
    /// </summary>
    private void HideToTray()
    {
        Hide();
        SaveSettings();

        // 内存优化：最小化时释放工作集
        TrimMemory();
    }

    /// <summary>
    /// 释放工作集内存
    /// </summary>
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
        catch
        {
            // 静默处理
        }
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    private void CleanupResources()
    {
        _hotkeyManager?.Dispose();
        _idleWatcher?.Dispose();
        _monitorService.Dispose();
        _trayIcon?.Dispose();
        _trayMenu?.Dispose();
    }

    #endregion

    #region 内部类

    /// <summary>
    /// 显示器控制行
    /// </summary>
    private sealed class MonitorControlRow
    {
        public MonitorInfo Monitor { get; set; } = null!;
        public Label NameLabel { get; set; } = null!;
        public TrackBar TrackBar { get; set; } = null!;
        public Label PercentLabel { get; set; } = null!;
    }

    /// <summary>
    /// 热键设置行
    /// </summary>
    private sealed class HotkeyRow
    {
        public int Index { get; set; }
        public TextBox TextBox { get; set; } = null!;
        public NumericUpDown BrightnessNumeric { get; set; } = null!;
        public Button ClearButton { get; set; } = null!;
        public uint Modifiers { get; set; }
        public uint Key { get; set; }
    }

    #endregion
}
