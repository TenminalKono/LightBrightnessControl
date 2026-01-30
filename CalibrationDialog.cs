namespace LightBrightnessControl;

/// <summary>
/// 比例校准对话框 - 纯代码构建
/// </summary>
public sealed class CalibrationDialog : Form
{
    private readonly List<MonitorInfo> _monitors;
    private readonly Dictionary<string, NumericUpDown> _ratioControls = new();

    /// <summary>
    /// 获取配置的比例值
    /// </summary>
    public Dictionary<string, double> Ratios { get; } = new();

    public CalibrationDialog(List<MonitorInfo> monitors)
    {
        _monitors = monitors;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = LocalizationManager.GetString("CalibrationTitle");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(400, 150 + _monitors.Count * 35);

        // 说明文字
        var descLabel = new Label
        {
            Text = LocalizationManager.GetString("CalibrationDesc"),
            Location = new Point(15, 15),
            Size = new Size(360, 40),
            AutoSize = false
        };
        Controls.Add(descLabel);

        int y = 65;

        // 为每个显示器创建比例设置
        foreach (var monitor in _monitors)
        {
            var label = new Label
            {
                Text = $"{monitor.Index}. {TruncateName(monitor.Name, 20)}:",
                Location = new Point(15, y + 3),
                Size = new Size(180, 20),
                AutoEllipsis = true
            };
            Controls.Add(label);

            var numericUpDown = new NumericUpDown
            {
                Location = new Point(200, y),
                Size = new Size(80, 23),
                Minimum = 10,
                Maximum = 100,
                Value = (decimal)(monitor.BrightnessRatio * 100),
                DecimalPlaces = 0
            };
            Controls.Add(numericUpDown);
            _ratioControls[monitor.DeviceName] = numericUpDown;

            var percentLabel = new Label
            {
                Text = "%",
                Location = new Point(285, y + 3),
                AutoSize = true
            };
            Controls.Add(percentLabel);

            y += 35;
        }

        // 按钮区域
        y += 10;

        var okButton = new Button
        {
            Text = LocalizationManager.GetString("OK"),
            Location = new Point(200, y),
            Size = new Size(80, 28),
            DialogResult = DialogResult.OK
        };
        okButton.Click += OnOkClick;
        Controls.Add(okButton);

        var cancelButton = new Button
        {
            Text = LocalizationManager.GetString("Cancel"),
            Location = new Point(290, y),
            Size = new Size(80, 28),
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void OnOkClick(object? sender, EventArgs e)
    {
        // 收集所有比例值
        foreach (var kvp in _ratioControls)
        {
            Ratios[kvp.Key] = (double)kvp.Value.Value / 100.0;
        }
    }

    private static string TruncateName(string name, int maxLength)
    {
        if (name.Length <= maxLength)
            return name;
        return name[..(maxLength - 3)] + "...";
    }
}
