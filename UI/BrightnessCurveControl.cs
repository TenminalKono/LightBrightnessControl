using System.Drawing.Drawing2D;

namespace LightBrightnessControl;

/// <summary>
/// 轻量级 GDI+ 交互式亮度校准曲线控件（零第三方图表库依赖）
/// 支持：鼠标拖拽锚点、双击空白添点、右键删点、键盘方向键微调、多显示器曲线叠加对比
/// </summary>
public sealed class BrightnessCurveControl : Control
{
    public sealed record ReferenceCurveItem(string Label, CalibrationCurve Curve, Color Color);

    private const int MarginLeft = 42;
    private const int MarginRight = 18;
    private const int MarginTop = 18;
    private const int MarginBottom = 34;
    private const int HitRadius = 11;

    private CalibrationCurve? _activeCurve;
    private Color _activeColor = Color.FromArgb(0, 120, 215);
    private List<ReferenceCurveItem> _referenceCurves = new();
    private bool _showReferenceCurves = true;
    private int _selectedPointIndex;
    private bool _isDragging;

    public string XAxisLabel { get; set; } = "Target (%)";
    public string YAxisLabel { get; set; } = "Output (%)";

    public CalibrationCurve? ActiveCurve
    {
        get => _activeCurve;
        set
        {
            _activeCurve = value;
            if (_activeCurve != null && _selectedPointIndex >= _activeCurve.Points.Count)
            {
                _selectedPointIndex = Math.Max(0, _activeCurve.Points.Count - 1);
            }
            Invalidate();
        }
    }

    public Color ActiveColor
    {
        get => _activeColor;
        set
        {
            _activeColor = value;
            Invalidate();
        }
    }

    public List<ReferenceCurveItem> ReferenceCurves
    {
        get => _referenceCurves;
        set
        {
            _referenceCurves = value ?? new();
            Invalidate();
        }
    }

    public bool ShowReferenceCurves
    {
        get => _showReferenceCurves;
        set
        {
            _showReferenceCurves = value;
            Invalidate();
        }
    }

    public int SelectedPointIndex
    {
        get => _selectedPointIndex;
        set
        {
            if (_activeCurve == null || _activeCurve.Points.Count == 0)
            {
                _selectedPointIndex = 0;
                return;
            }
            int clamped = Math.Clamp(value, 0, _activeCurve.Points.Count - 1);
            if (_selectedPointIndex != clamped)
            {
                _selectedPointIndex = clamped;
                Invalidate();
            }
        }
    }

    public event Action<int>? SelectedPointChanged;
    public event Action<int>? CurveChanged;

    public BrightnessCurveControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        TabStop = true;
        BackColor = Color.White;
        Cursor = Cursors.Cross;
    }

    private Rectangle GetPlotRect()
    {
        int w = Math.Max(40, ClientSize.Width - MarginLeft - MarginRight);
        int h = Math.Max(40, ClientSize.Height - MarginTop - MarginBottom);
        return new Rectangle(MarginLeft, MarginTop, w, h);
    }

    private PointF ValueToPixel(int input, int output, Rectangle plot)
    {
        float x = plot.Left + (input / 100f) * plot.Width;
        float y = plot.Bottom - (output / 100f) * plot.Height;
        return new PointF(x, y);
    }

    private (int Input, int Output) PixelToValue(Point pt, Rectangle plot)
    {
        float rx = (pt.X - plot.Left) / (float)plot.Width;
        float ry = (plot.Bottom - pt.Y) / (float)plot.Height;
        int x = Math.Clamp((int)Math.Round(rx * 100f), 0, 100);
        int y = Math.Clamp((int)Math.Round(ry * 100f), 0, 100);
        return (x, y);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var plot = GetPlotRect();

        // 绘图区底色
        using (var bgBrush = new SolidBrush(Color.FromArgb(250, 251, 253)))
        {
            g.FillRectangle(bgBrush, plot);
        }

        // 网格线与刻度 (0, 25, 50, 75, 100)
        using var gridPen = new Pen(Color.FromArgb(228, 232, 238), 1f);
        using var axisFont = new Font("Microsoft YaHei UI", 7.5f);
        using var textBrush = new SolidBrush(Color.FromArgb(100, 108, 120));

        for (int v = 0; v <= 100; v += 25)
        {
            var pX = ValueToPixel(v, 0, plot);
            var pY = ValueToPixel(0, v, plot);

            g.DrawLine(gridPen, pX.X, plot.Top, pX.X, plot.Bottom);
            g.DrawLine(gridPen, plot.Left, pY.Y, plot.Right, pY.Y);

            string tick = v.ToString();
            var sz = g.MeasureString(tick, axisFont);
            g.DrawString(tick, axisFont, textBrush, pX.X - sz.Width / 2f, plot.Bottom + 3);
            g.DrawString(tick, axisFont, textBrush, plot.Left - sz.Width - 5, pY.Y - sz.Height / 2f);
        }

        // 坐标轴标题
        var xLabelSize = g.MeasureString(XAxisLabel, axisFont);
        g.DrawString(XAxisLabel, axisFont, textBrush,
            plot.Left + (plot.Width - xLabelSize.Width) / 2f,
            ClientSize.Height - xLabelSize.Height - 1);

        // 1:1 基准虚线
        using (var diagPen = new Pen(Color.FromArgb(185, 192, 204), 1f) { DashStyle = DashStyle.Dot })
        {
            g.DrawLine(diagPen, plot.Left, plot.Bottom, plot.Right, plot.Top);
        }

        // 外框（聚焦时高亮边框）
        using (var borderPen = new Pen(Focused ? Color.FromArgb(0, 120, 215) : Color.FromArgb(190, 198, 210), 1f))
        {
            g.DrawRectangle(borderPen, plot);
        }

        // 叠加绘制其他显示器参考曲线
        if (_showReferenceCurves && _referenceCurves.Count > 0)
        {
            foreach (var item in _referenceCurves)
            {
                DrawCurvePolyline(g, item.Curve, plot, Color.FromArgb(140, item.Color), 1.6f, DashStyle.Dash, drawNodes: true, isReference: true);
            }
        }

        // 绘制当前编辑显示器的主曲线与锚点
        if (_activeCurve != null && _activeCurve.Points.Count >= 2)
        {
            DrawCurvePolyline(g, _activeCurve, plot, _activeColor, 2.5f, DashStyle.Solid, drawNodes: true, isReference: false);

            // 显示选中锚点的悬浮数值标签
            if (_selectedPointIndex >= 0 && _selectedPointIndex < _activeCurve.Points.Count)
            {
                var pt = _activeCurve.Points[_selectedPointIndex];
                var px = ValueToPixel(pt.Input, pt.Output, plot);
                string badge = $"{pt.Input}% → {pt.Output}%";
                var badgeSize = g.MeasureString(badge, axisFont);
                float bx = Math.Clamp(px.X + 10, plot.Left + 4, plot.Right - badgeSize.Width - 8);
                float by = px.Y - badgeSize.Height - 10;
                if (by < plot.Top + 4) by = px.Y + 10;

                var badgeRect = new RectangleF(bx - 3, by - 2, badgeSize.Width + 6, badgeSize.Height + 4);
                using var badgeBg = new SolidBrush(Color.FromArgb(235, 34, 40, 49));
                using var badgeFg = new SolidBrush(Color.White);
                g.FillRectangle(badgeBg, badgeRect);
                g.DrawString(badge, axisFont, badgeFg, bx, by);
            }
        }
    }

    private void DrawCurvePolyline(
        Graphics g,
        CalibrationCurve curve,
        Rectangle plot,
        Color color,
        float width,
        DashStyle dashStyle,
        bool drawNodes,
        bool isReference)
    {
        var pts = curve.Points;
        if (pts.Count < 2) return;

        var screenPts = new PointF[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            screenPts[i] = ValueToPixel(pts[i].Input, pts[i].Output, plot);
        }

        // 仅主曲线填充极淡底色，增强可读性
        if (!isReference)
        {
            var poly = new PointF[pts.Count + 2];
            Array.Copy(screenPts, poly, pts.Count);
            poly[pts.Count] = new PointF(screenPts[^1].X, plot.Bottom);
            poly[pts.Count + 1] = new PointF(screenPts[0].X, plot.Bottom);
            using var fillBrush = new SolidBrush(Color.FromArgb(24, color));
            g.FillPolygon(fillBrush, poly);
        }

        using (var pen = new Pen(color, width) { DashStyle = dashStyle })
        {
            g.DrawLines(pen, screenPts);
        }

        if (!drawNodes) return;

        for (int i = 0; i < screenPts.Length; i++)
        {
            var p = screenPts[i];
            if (isReference)
            {
                float r = 3.2f;
                using var refBrush = new SolidBrush(Color.White);
                using var refPen = new Pen(color, 1.3f);
                g.FillEllipse(refBrush, p.X - r, p.Y - r, r * 2, r * 2);
                g.DrawEllipse(refPen, p.X - r, p.Y - r, r * 2, r * 2);
            }
            else
            {
                bool selected = i == _selectedPointIndex;
                float r = selected ? 6.5f : 5.0f;
                using var outerBrush = new SolidBrush(selected ? Color.FromArgb(255, 255, 255) : color);
                using var innerBrush = new SolidBrush(selected ? color : Color.White);
                using var ringPen = new Pen(color, selected ? 2.4f : 1.8f);

                g.FillEllipse(outerBrush, p.X - r, p.Y - r, r * 2, r * 2);
                g.DrawEllipse(ringPen, p.X - r, p.Y - r, r * 2, r * 2);

                float ir = selected ? 3.2f : 2.2f;
                g.FillEllipse(innerBrush, p.X - ir, p.Y - ir, ir * 2, ir * 2);
            }
        }
    }

    private int HitTestPoint(Point mouseLocation)
    {
        if (_activeCurve == null) return -1;

        var plot = GetPlotRect();
        int bestIdx = -1;
        float bestDistSq = HitRadius * HitRadius;

        for (int i = 0; i < _activeCurve.Points.Count; i++)
        {
            var p = ValueToPixel(_activeCurve.Points[i].Input, _activeCurve.Points[i].Output, plot);
            float dx = mouseLocation.X - p.X;
            float dy = mouseLocation.Y - p.Y;
            float d2 = dx * dx + dy * dy;
            if (d2 <= bestDistSq)
            {
                bestDistSq = d2;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (_activeCurve == null) return;

        int hit = HitTestPoint(e.Location);

        if (e.Button == MouseButtons.Right)
        {
            // 右键删除非端点锚点
            if (hit > 0 && hit < _activeCurve.Points.Count - 1)
            {
                if (_activeCurve.RemovePointAt(hit))
                {
                    _selectedPointIndex = Math.Clamp(hit - 1, 0, _activeCurve.Points.Count - 1);
                    Invalidate();
                    SelectedPointChanged?.Invoke(_selectedPointIndex);
                    CurveChanged?.Invoke(_selectedPointIndex);
                }
            }
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            if (hit >= 0)
            {
                _selectedPointIndex = hit;
                _isDragging = true;
                Invalidate();
                SelectedPointChanged?.Invoke(_selectedPointIndex);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_activeCurve == null) return;

        if (!_isDragging)
        {
            int hit = HitTestPoint(e.Location);
            Cursor = hit >= 0 ? Cursors.SizeAll : Cursors.Cross;
            return;
        }

        var plot = GetPlotRect();
        var (valX, valY) = PixelToValue(e.Location, plot);

        if (_selectedPointIndex < 0 || _selectedPointIndex >= _activeCurve.Points.Count)
            return;

        var current = _activeCurve.Points[_selectedPointIndex];
        int targetX = current.Input;

        // 非端点允许在相邻两锚点之间自由水平拖动，防止节点交叉
        if (_selectedPointIndex > 0 && _selectedPointIndex < _activeCurve.Points.Count - 1)
        {
            int minX = _activeCurve.Points[_selectedPointIndex - 1].Input + 1;
            int maxX = _activeCurve.Points[_selectedPointIndex + 1].Input - 1;
            if (minX <= maxX)
            {
                targetX = Math.Clamp(valX, minX, maxX);
            }
        }

        if (targetX != current.Input || valY != current.Output)
        {
            _selectedPointIndex = _activeCurve.MovePoint(_selectedPointIndex, targetX, valY);
            Invalidate();
            CurveChanged?.Invoke(_selectedPointIndex);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _isDragging = false;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left || _activeCurve == null) return;

        var plot = GetPlotRect();
        if (!plot.Contains(e.Location)) return;

        var (valX, valY) = PixelToValue(e.Location, plot);
        if (valX <= 0 || valX >= 100) return;

        _selectedPointIndex = _activeCurve.SetPoint(valX, valY);
        Invalidate();
        SelectedPointChanged?.Invoke(_selectedPointIndex);
        CurveChanged?.Invoke(_selectedPointIndex);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        var keyCode = keyData & Keys.KeyCode;
        if (keyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Delete)
            return true;
        return base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_activeCurve == null || _activeCurve.Points.Count == 0) return;

        int step = e.Shift ? 5 : 1;

        switch (e.KeyCode)
        {
            case Keys.Left:
                if (_selectedPointIndex > 0)
                {
                    _selectedPointIndex--;
                    Invalidate();
                    SelectedPointChanged?.Invoke(_selectedPointIndex);
                }
                e.Handled = true;
                break;

            case Keys.Right:
                if (_selectedPointIndex < _activeCurve.Points.Count - 1)
                {
                    _selectedPointIndex++;
                    Invalidate();
                    SelectedPointChanged?.Invoke(_selectedPointIndex);
                }
                e.Handled = true;
                break;

            case Keys.Up:
            case Keys.Down:
            {
                var pt = _activeCurve.Points[_selectedPointIndex];
                int delta = e.KeyCode == Keys.Up ? step : -step;
                int newY = Math.Clamp(pt.Output + delta, 0, 100);
                if (newY != pt.Output)
                {
                    _selectedPointIndex = _activeCurve.MovePoint(_selectedPointIndex, pt.Input, newY);
                    Invalidate();
                    CurveChanged?.Invoke(_selectedPointIndex);
                }
                e.Handled = true;
                break;
            }

            case Keys.Delete:
            case Keys.Back:
                if (_selectedPointIndex > 0 && _selectedPointIndex < _activeCurve.Points.Count - 1)
                {
                    if (_activeCurve.RemovePointAt(_selectedPointIndex))
                    {
                        _selectedPointIndex = Math.Clamp(_selectedPointIndex - 1, 0, _activeCurve.Points.Count - 1);
                        Invalidate();
                        SelectedPointChanged?.Invoke(_selectedPointIndex);
                        CurveChanged?.Invoke(_selectedPointIndex);
                    }
                }
                e.Handled = true;
                break;
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }
}
