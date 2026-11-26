namespace LightBrightnessControl;

/// <summary>
/// 多点亮度曲线校准对话框（方案 2C）
/// 结合：轻量 GDI+ 交互曲线画布 + 键盘/鼠标直接数值编辑表 + 滑条微调 + 多屏叠加对比 + 节流实时肉眼预览
/// </summary>
public sealed class CalibrationDialog : Form
{
    private static readonly Color[] MonitorPalette =
    {
        Color.FromArgb(0, 120, 215),   // 蓝
        Color.FromArgb(230, 126, 34),  // 橙
        Color.FromArgb(39, 174, 96),   // 绿
        Color.FromArgb(142, 68, 173),  // 紫
        Color.FromArgb(192, 57, 43),   // 红
        Color.FromArgb(22, 160, 133)   // 青
    };

    private readonly List<MonitorInfo> _monitors;
    private readonly IReadOnlyList<int> _presetBrightnessValues;
    private readonly Dictionary<string, CalibrationCurve> _workingCurves = new();
    private readonly Dictionary<string, int> _initialRawBrightness = new();

    // 顶部控制栏
    private ComboBox _monitorComboBox = null!;
    private Panel _colorSwatch = null!;
    private CheckBox _overlayCheckBox = null!;
    private CheckBox _livePreviewCheckBox = null!;
    private Button _resetLinearButton = null!;

    // 中部：左曲线 + 右锚点表
    private BrightnessCurveControl _curveControl = null!;
    private DataGridView _pointsGrid = null!;

    // 下部：当前锚点精准数值与滑条微调
    private NumericUpDown _targetNumeric = null!;
    private NumericUpDown _outputNumeric = null!;
    private TrackBar _outputTrackBar = null!;
    private Button _addPointButton = null!;
    private Button _deletePointButton = null!;
    private Button _syncPresetsButton = null!;
    private Button _previewPointButton = null!;

    // 节流实时预览状态（防 DDC/CI 总线拥堵）
    private System.Windows.Forms.Timer? _previewThrottleTimer;
    private int? _pendingPreviewTarget;
    private bool _hasSentPreview;
    private bool _suppressUiEvents;

    /// <summary>
    /// 点击确定后的校准曲线结果 (CalibrationKey -> CalibrationCurve)
    /// </summary>
    public Dictionary<string, CalibrationCurve> ResultCurves { get; } = new();

    public CalibrationDialog(List<MonitorInfo> monitors, IReadOnlyList<int> presetBrightnessValues)
    {
        _monitors = monitors;
        _presetBrightnessValues = presetBrightnessValues;

        foreach (var monitor in _monitors)
        {
            _workingCurves[monitor.CalibrationKey] = monitor.CalibrationCurve.Clone();
            _initialRawBrightness[monitor.CalibrationKey] = monitor.CurrentBrightness;
        }

        InitializeComponent();

        if (_monitors.Count > 0)
        {
            _monitorComboBox.SelectedIndex = 0;
        }
    }

    private void InitializeComponent()
    {
        Text = LocalizationManager.GetString("CalibrationTitle");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(650, 520);

        if (_monitors.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = LocalizationManager.GetString("NoMonitorForCalibration"),
                Location = new Point(20, 40),
                Size = new Size(610, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(emptyLabel);

            var closeBtn = new Button
            {
                Text = LocalizationManager.GetString("OK"),
                Location = new Point(285, 100),
                Size = new Size(80, 28),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(closeBtn);
            ClientSize = new Size(460, 150);
            emptyLabel.Size = new Size(420, 40);
            closeBtn.Location = new Point(190, 100);
            return;
        }

        // ========== 顶部：多显示器选择与预览选项 ==========
        var monitorLabel = new Label
        {
            Text = LocalizationManager.GetString("CalibrationMonitor"),
            Location = new Point(14, 16),
            AutoSize = true
        };
        Controls.Add(monitorLabel);

        _colorSwatch = new Panel
        {
            Location = new Point(98, 15),
            Size = new Size(14, 18),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = MonitorPalette[0]
        };
        Controls.Add(_colorSwatch);

        _monitorComboBox = new ComboBox
        {
            Location = new Point(118, 12),
            Size = new Size(340, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (var monitor in _monitors)
        {
            string primaryTag = monitor.IsPrimary ? $" {LocalizationManager.GetString("PrimaryTag")}" : "";
            _monitorComboBox.Items.Add($"{monitor.Index}. {monitor.Name} ({monitor.ScreenWidth}x{monitor.ScreenHeight}){primaryTag}");
        }
        _monitorComboBox.SelectedIndexChanged += OnMonitorSelectionChanged;
        Controls.Add(_monitorComboBox);

        _resetLinearButton = new Button
        {
            Text = LocalizationManager.GetString("ResetLinear"),
            Location = new Point(468, 11),
            Size = new Size(82, 25)
        };
        _resetLinearButton.Click += OnResetLinearClick;
        Controls.Add(_resetLinearButton);

        _syncPresetsButton = new Button
        {
            Text = LocalizationManager.GetString("SyncFromPresets"),
            Location = new Point(555, 11),
            Size = new Size(82, 25)
        };
        _syncPresetsButton.Click += OnSyncPresetsClick;
        Controls.Add(_syncPresetsButton);

        // 第二行开关
        _livePreviewCheckBox = new CheckBox
        {
            Text = LocalizationManager.GetString("LivePreview"),
            Location = new Point(14, 44),
            AutoSize = true,
            Checked = true
        };
        Controls.Add(_livePreviewCheckBox);

        _overlayCheckBox = new CheckBox
        {
            Text = LocalizationManager.GetString("ShowOtherCurves"),
            Location = new Point(220, 44),
            AutoSize = true,
            Checked = _monitors.Count > 1,
            Enabled = _monitors.Count > 1
        };
        _overlayCheckBox.CheckedChanged += (_, _) =>
        {
            _curveControl.ShowReferenceCurves = _overlayCheckBox.Checked;
        };
        Controls.Add(_overlayCheckBox);

        // ========== 中部左侧：GDI+ 曲线编辑器 ==========
        _curveControl = new BrightnessCurveControl
        {
            Location = new Point(14, 72),
            Size = new Size(390, 290),
            XAxisLabel = LocalizationManager.GetString("AxisTarget"),
            YAxisLabel = LocalizationManager.GetString("AxisOutput"),
            ShowReferenceCurves = _overlayCheckBox.Checked
        };
        _curveControl.SelectedPointChanged += OnCurvePointSelected;
        _curveControl.CurveChanged += OnCurveEditedFromCanvas;
        Controls.Add(_curveControl);

        var hintLabel = new Label
        {
            Text = LocalizationManager.GetString("CalibrationHint"),
            Location = new Point(14, 366),
            Size = new Size(395, 20),
            ForeColor = Color.FromArgb(95, 102, 114),
            Font = new Font("Microsoft YaHei UI", 8f)
        };
        Controls.Add(hintLabel);

        // ========== 中部右侧：键盘/鼠标直接数值编辑表 ==========
        var gridTitle = new Label
        {
            Text = LocalizationManager.GetString("CalibrationPoints"),
            Location = new Point(416, 72),
            Size = new Size(222, 18),
            Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold)
        };
        Controls.Add(gridTitle);

        _pointsGrid = new DataGridView
        {
            Location = new Point(416, 94),
            Size = new Size(222, 268),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = false,
            MultiSelect = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            EditMode = DataGridViewEditMode.EditOnEnter,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        var colTarget = new DataGridViewTextBoxColumn
        {
            Name = "Target",
            HeaderText = LocalizationManager.GetString("ColTarget"),
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        var colOutput = new DataGridViewTextBoxColumn
        {
            Name = "Output",
            HeaderText = LocalizationManager.GetString("ColOutput"),
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        _pointsGrid.Columns.Add(colTarget);
        _pointsGrid.Columns.Add(colOutput);
        _pointsGrid.SelectionChanged += OnGridSelectionChanged;
        _pointsGrid.CellEndEdit += OnGridCellEndEdit;
        Controls.Add(_pointsGrid);

        // ========== 下部：当前锚点精准数值输入与滑条微调 ==========
        var editorBox = new GroupBox
        {
            Location = new Point(14, 392),
            Size = new Size(624, 78),
            Text = string.Empty
        };

        var targetLabel = new Label
        {
            Text = LocalizationManager.GetString("TargetInput"),
            Location = new Point(12, 18),
            AutoSize = true
        };
        editorBox.Controls.Add(targetLabel);

        _targetNumeric = new NumericUpDown
        {
            Location = new Point(56, 15),
            Size = new Size(56, 23),
            Minimum = 0,
            Maximum = 100
        };
        _targetNumeric.ValueChanged += OnNumericEditorChanged;
        editorBox.Controls.Add(_targetNumeric);

        var targetPct = new Label { Text = "%", Location = new Point(114, 18), AutoSize = true };
        editorBox.Controls.Add(targetPct);

        var outputLabel = new Label
        {
            Text = LocalizationManager.GetString("ActualOutput"),
            Location = new Point(142, 18),
            AutoSize = true
        };
        editorBox.Controls.Add(outputLabel);

        _outputNumeric = new NumericUpDown
        {
            Location = new Point(208, 15),
            Size = new Size(56, 23),
            Minimum = 0,
            Maximum = 100
        };
        _outputNumeric.ValueChanged += OnNumericEditorChanged;
        editorBox.Controls.Add(_outputNumeric);

        var outputPct = new Label { Text = "%", Location = new Point(266, 18), AutoSize = true };
        editorBox.Controls.Add(outputPct);

        _outputTrackBar = new TrackBar
        {
            Location = new Point(290, 10),
            Size = new Size(320, 35),
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10
        };
        _outputTrackBar.ValueChanged += OnOutputTrackBarChanged;
        editorBox.Controls.Add(_outputTrackBar);

        _addPointButton = new Button
        {
            Text = LocalizationManager.GetString("AddPoint"),
            Location = new Point(12, 46),
            Size = new Size(80, 24)
        };
        _addPointButton.Click += OnAddPointClick;
        editorBox.Controls.Add(_addPointButton);

        _deletePointButton = new Button
        {
            Text = LocalizationManager.GetString("DeletePoint"),
            Location = new Point(98, 46),
            Size = new Size(80, 24)
        };
        _deletePointButton.Click += OnDeletePointClick;
        editorBox.Controls.Add(_deletePointButton);

        _previewPointButton = new Button
        {
            Text = LocalizationManager.GetString("PreviewPoint"),
            Location = new Point(184, 46),
            Size = new Size(86, 24)
        };
        _previewPointButton.Click += (_, _) => TriggerPreviewForSelectedPoint(force: true);
        editorBox.Controls.Add(_previewPointButton);

        Controls.Add(editorBox);

        // ========== 底部确定/取消按钮 ==========
        var okButton = new Button
        {
            Text = LocalizationManager.GetString("OK"),
            Location = new Point(468, 480),
            Size = new Size(80, 28),
            DialogResult = DialogResult.OK
        };
        okButton.Click += OnOkClick;
        Controls.Add(okButton);

        var cancelButton = new Button
        {
            Text = LocalizationManager.GetString("Cancel"),
            Location = new Point(558, 480),
            Size = new Size(80, 28),
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private MonitorInfo? CurrentMonitor =>
        _monitorComboBox.SelectedIndex >= 0 && _monitorComboBox.SelectedIndex < _monitors.Count
            ? _monitors[_monitorComboBox.SelectedIndex]
            : null;

    private CalibrationCurve? CurrentCurve =>
        CurrentMonitor != null ? _workingCurves[CurrentMonitor.CalibrationKey] : null;

    private static Color GetMonitorColor(int index) =>
        MonitorPalette[index % MonitorPalette.Length];

    private void OnMonitorSelectionChanged(object? sender, EventArgs e)
    {
        int idx = _monitorComboBox.SelectedIndex;
        if (idx < 0 || idx >= _monitors.Count) return;

        var monitor = _monitors[idx];
        var color = GetMonitorColor(idx);
        _colorSwatch.BackColor = color;

        var refs = new List<BrightnessCurveControl.ReferenceCurveItem>();
        for (int i = 0; i < _monitors.Count; i++)
        {
            if (i == idx) continue;
            var other = _monitors[i];
            refs.Add(new BrightnessCurveControl.ReferenceCurveItem(
                other.Name,
                _workingCurves[other.CalibrationKey],
                GetMonitorColor(i)));
        }

        _curveControl.ActiveColor = color;
        _curveControl.ReferenceCurves = refs;
        _curveControl.ActiveCurve = _workingCurves[monitor.CalibrationKey];

        int defaultSelect = _curveControl.ActiveCurve.Points.Count > 2 ? 2 : 0;
        _curveControl.SelectedPointIndex = defaultSelect;

        RefreshGridAndEditor(defaultSelect);
    }

    private void RefreshGridAndEditor(int selectedIndex)
    {
        var curve = CurrentCurve;
        if (curve == null) return;

        _suppressUiEvents = true;
        try
        {
            _pointsGrid.Rows.Clear();
            for (int i = 0; i < curve.Points.Count; i++)
            {
                var pt = curve.Points[i];
                int rowIdx = _pointsGrid.Rows.Add(pt.Input, pt.Output);
                // 0% 与 100% 端点的目标横坐标锁定不可改
                bool isEndpoint = i == 0 || i == curve.Points.Count - 1;
                _pointsGrid.Rows[rowIdx].Cells[0].ReadOnly = isEndpoint;
                if (isEndpoint)
                {
                    _pointsGrid.Rows[rowIdx].Cells[0].Style.ForeColor = Color.Gray;
                }
            }

            int clamped = Math.Clamp(selectedIndex, 0, curve.Points.Count - 1);
            if (clamped < _pointsGrid.Rows.Count)
            {
                _pointsGrid.ClearSelection();
                _pointsGrid.Rows[clamped].Selected = true;
                _pointsGrid.CurrentCell = _pointsGrid.Rows[clamped].Cells[1];
            }

            SyncBottomEditor(clamped);
        }
        finally
        {
            _suppressUiEvents = false;
        }
    }

    private void SyncBottomEditor(int pointIndex)
    {
        var curve = CurrentCurve;
        if (curve == null || pointIndex < 0 || pointIndex >= curve.Points.Count) return;

        var pt = curve.Points[pointIndex];
        bool isEndpoint = pointIndex == 0 || pointIndex == curve.Points.Count - 1;

        _targetNumeric.Enabled = !isEndpoint;
        _targetNumeric.Value = pt.Input;
        _outputNumeric.Value = pt.Output;
        _outputTrackBar.Value = pt.Output;
        _deletePointButton.Enabled = !isEndpoint;
    }

    private void OnCurvePointSelected(int index)
    {
        if (_suppressUiEvents) return;

        _suppressUiEvents = true;
        try
        {
            if (index >= 0 && index < _pointsGrid.Rows.Count)
            {
                _pointsGrid.ClearSelection();
                _pointsGrid.Rows[index].Selected = true;
                _pointsGrid.CurrentCell = _pointsGrid.Rows[index].Cells[1];
            }
            SyncBottomEditor(index);
        }
        finally
        {
            _suppressUiEvents = false;
        }

        TriggerPreviewForSelectedPoint(force: false);
    }

    private void OnCurveEditedFromCanvas(int index)
    {
        if (_suppressUiEvents) return;
        RefreshGridAndEditor(index);
        TriggerPreviewForSelectedPoint(force: false);
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressUiEvents || _pointsGrid.SelectedRows.Count == 0) return;

        int rowIdx = _pointsGrid.SelectedRows[0].Index;
        if (CurrentCurve == null || rowIdx < 0 || rowIdx >= CurrentCurve.Points.Count) return;

        _suppressUiEvents = true;
        try
        {
            _curveControl.SelectedPointIndex = rowIdx;
            SyncBottomEditor(rowIdx);
        }
        finally
        {
            _suppressUiEvents = false;
        }

        TriggerPreviewForSelectedPoint(force: false);
    }

    private void OnGridCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressUiEvents || CurrentCurve == null) return;
        if (e.RowIndex < 0 || e.RowIndex >= CurrentCurve.Points.Count) return;

        var pt = CurrentCurve.Points[e.RowIndex];
        string? rawText = _pointsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString()?.Trim();

        if (!int.TryParse(rawText, out int parsed))
        {
            RefreshGridAndEditor(e.RowIndex);
            return;
        }

        int newInput = e.ColumnIndex == 0 ? parsed : pt.Input;
        int newOutput = e.ColumnIndex == 1 ? parsed : pt.Output;

        int newIndex = CurrentCurve.MovePoint(e.RowIndex, newInput, newOutput);
        _curveControl.SelectedPointIndex = newIndex;
        _curveControl.Invalidate();

        // DataGridView 的 CellEndEdit 期间通过 BeginInvoke 安全刷新列表
        BeginInvoke(() =>
        {
            RefreshGridAndEditor(newIndex);
            TriggerPreviewForSelectedPoint(force: false);
        });
    }

    private void OnNumericEditorChanged(object? sender, EventArgs e)
    {
        if (_suppressUiEvents || CurrentCurve == null) return;

        int idx = _curveControl.SelectedPointIndex;
        if (idx < 0 || idx >= CurrentCurve.Points.Count) return;

        int newInput = (int)_targetNumeric.Value;
        int newOutput = (int)_outputNumeric.Value;

        int newIndex = CurrentCurve.MovePoint(idx, newInput, newOutput);
        _curveControl.SelectedPointIndex = newIndex;
        _curveControl.Invalidate();
        RefreshGridAndEditor(newIndex);
        TriggerPreviewForSelectedPoint(force: false);
    }

    private void OnOutputTrackBarChanged(object? sender, EventArgs e)
    {
        if (_suppressUiEvents || CurrentCurve == null) return;

        int idx = _curveControl.SelectedPointIndex;
        if (idx < 0 || idx >= CurrentCurve.Points.Count) return;

        var pt = CurrentCurve.Points[idx];
        int newOutput = _outputTrackBar.Value;

        int newIndex = CurrentCurve.MovePoint(idx, pt.Input, newOutput);
        _curveControl.SelectedPointIndex = newIndex;
        _curveControl.Invalidate();

        _suppressUiEvents = true;
        try
        {
            _outputNumeric.Value = newOutput;
            if (newIndex < _pointsGrid.Rows.Count)
            {
                _pointsGrid.Rows[newIndex].Cells[1].Value = newOutput;
            }
        }
        finally
        {
            _suppressUiEvents = false;
        }

        TriggerPreviewForSelectedPoint(force: false);
    }

    private void OnAddPointClick(object? sender, EventArgs e)
    {
        var curve = CurrentCurve;
        if (curve == null || curve.Points.Count >= 15) return;

        // 寻找最大间隔的中点自动插入，方便用户直接微调
        int bestGap = 0;
        int insertX = 50;
        for (int i = 0; i < curve.Points.Count - 1; i++)
        {
            int gap = curve.Points[i + 1].Input - curve.Points[i].Input;
            if (gap > bestGap)
            {
                bestGap = gap;
                insertX = curve.Points[i].Input + gap / 2;
            }
        }

        if (bestGap < 2) return;

        int y = curve.Evaluate(insertX);
        int newIndex = curve.SetPoint(insertX, y);
        _curveControl.SelectedPointIndex = newIndex;
        _curveControl.Invalidate();
        RefreshGridAndEditor(newIndex);
        TriggerPreviewForSelectedPoint(force: false);
    }

    private void OnDeletePointClick(object? sender, EventArgs e)
    {
        var curve = CurrentCurve;
        if (curve == null) return;

        int idx = _curveControl.SelectedPointIndex;
        if (curve.RemovePointAt(idx))
        {
            int nextIdx = Math.Clamp(idx - 1, 0, curve.Points.Count - 1);
            _curveControl.SelectedPointIndex = nextIdx;
            _curveControl.Invalidate();
            RefreshGridAndEditor(nextIdx);
            TriggerPreviewForSelectedPoint(force: false);
        }
    }

    private void OnResetLinearClick(object? sender, EventArgs e)
    {
        var curve = CurrentCurve;
        if (curve == null) return;

        curve.ResetToDefault();
        _curveControl.SelectedPointIndex = 2;
        _curveControl.Invalidate();
        RefreshGridAndEditor(2);
        TriggerPreviewForSelectedPoint(force: false);
    }

    private void OnSyncPresetsClick(object? sender, EventArgs e)
    {
        if (CurrentCurve == null || _presetBrightnessValues.Count == 0) return;

        CurrentCurve.SyncAnchorInputs(_presetBrightnessValues);
        int nextIdx = Math.Min(1, CurrentCurve.Points.Count - 1);
        _curveControl.SelectedPointIndex = nextIdx;
        _curveControl.Invalidate();
        RefreshGridAndEditor(nextIdx);
        TriggerPreviewForSelectedPoint(force: false);
    }

    /// <summary>
    /// 节流实时预览：将所有显示器同步切至当前选中锚点的目标亮度（各自走自己的当前曲线）
    /// </summary>
    private void TriggerPreviewForSelectedPoint(bool force)
    {
        if (!force && !_livePreviewCheckBox.Checked) return;

        var curve = CurrentCurve;
        if (curve == null) return;

        int idx = _curveControl.SelectedPointIndex;
        if (idx < 0 || idx >= curve.Points.Count) return;

        _pendingPreviewTarget = curve.Points[idx].Input;

        if (_previewThrottleTimer == null)
        {
            _previewThrottleTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _previewThrottleTimer.Tick += OnPreviewThrottleTick;
        }

        if (!_previewThrottleTimer.Enabled)
        {
            _previewThrottleTimer.Start();
        }
    }

    private void OnPreviewThrottleTick(object? sender, EventArgs e)
    {
        _previewThrottleTimer?.Stop();
        if (_pendingPreviewTarget is not int targetX) return;
        _pendingPreviewTarget = null;

        _hasSentPreview = true;

        // 抓取待下发的快照并在后台线程发送 DDC/CI，保持 UI 拖动丝滑
        var jobs = _monitors
            .Select(m => (Monitor: m, Brightness: _workingCurves[m.CalibrationKey].Evaluate(targetX)))
            .ToList();

        _ = Task.Run(() =>
        {
            foreach (var job in jobs)
            {
                job.Monitor.SetRawBrightness(job.Brightness);
            }
        });
    }

    private void OnOkClick(object? sender, EventArgs e)
    {
        ResultCurves.Clear();
        foreach (var kvp in _workingCurves)
        {
            ResultCurves[kvp.Key] = kvp.Value.Clone();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_previewThrottleTimer != null)
        {
            _previewThrottleTimer.Stop();
            _previewThrottleTimer.Tick -= OnPreviewThrottleTick;
            _previewThrottleTimer.Dispose();
            _previewThrottleTimer = null;
        }

        // 若取消且曾进行过实时预览，同步恢复各显示器原始亮度，确保主界面滑条立即显示真实亮度
        if (DialogResult != DialogResult.OK && _hasSentPreview)
        {
            foreach (var m in _monitors)
            {
                if (_initialRawBrightness.TryGetValue(m.CalibrationKey, out int raw))
                {
                    m.SetRawBrightness(raw);
                }
            }
        }

        base.OnFormClosing(e);
    }
}
