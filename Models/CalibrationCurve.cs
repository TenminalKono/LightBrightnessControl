namespace LightBrightnessControl;

/// <summary>
/// 校准曲线锚点（输入目标亮度 -> 实际下发给显示器的 DDC 亮度）
/// </summary>
public sealed class CalibrationPoint
{
    public int Input { get; set; }
    public int Output { get; set; }

    public CalibrationPoint() { }

    public CalibrationPoint(int input, int output)
    {
        Input = Math.Clamp(input, 0, 100);
        Output = Math.Clamp(output, 0, 100);
    }
}

/// <summary>
/// 分段线性亮度校准曲线
/// 内部预计算 101 字节查找表 (LUT)，运行时 O(1) 零分配查表，极致轻量
/// </summary>
public sealed class CalibrationCurve
{
    public static readonly int[] DefaultAnchorInputs = { 0, 25, 50, 75, 100 };

    private readonly List<CalibrationPoint> _points = new();
    private readonly byte[] _lut = new byte[101];

    public IReadOnlyList<CalibrationPoint> Points => _points;

    public CalibrationCurve()
    {
        ResetToDefault();
    }

    public CalibrationCurve(IEnumerable<CalibrationPoint> points)
    {
        SetPoints(points);
    }

    /// <summary>
    /// 创建默认 1:1 线性校准曲线（包含常用 5 个锚点）
    /// </summary>
    public static CalibrationCurve CreateDefault()
    {
        return new CalibrationCurve();
    }

    /// <summary>
    /// 从旧版本单比例系数无损迁移
    /// </summary>
    public static CalibrationCurve CreateFromLegacyRatio(double ratio)
    {
        double clamped = Math.Clamp(ratio, 0.1, 1.0);
        var pts = new List<CalibrationPoint>(DefaultAnchorInputs.Length);
        foreach (int x in DefaultAnchorInputs)
        {
            pts.Add(new CalibrationPoint(x, (int)Math.Round(x * clamped)));
        }
        return new CalibrationCurve(pts);
    }

    /// <summary>
    /// 重置为 1:1 直线
    /// </summary>
    public void ResetToDefault()
    {
        _points.Clear();
        foreach (int x in DefaultAnchorInputs)
        {
            _points.Add(new CalibrationPoint(x, x));
        }
        RebuildLut();
    }

    /// <summary>
    /// 批量设置锚点并自动规范化（保证包含 0 和 100 端点，按 Input 升序去重）
    /// </summary>
    public void SetPoints(IEnumerable<CalibrationPoint>? points)
    {
        var map = new SortedDictionary<int, int>();
        if (points != null)
        {
            foreach (var p in points)
            {
                int x = Math.Clamp(p.Input, 0, 100);
                int y = Math.Clamp(p.Output, 0, 100);
                map[x] = y;
            }
        }

        if (!map.ContainsKey(0))
        {
            map[0] = 0;
        }
        if (!map.ContainsKey(100))
        {
            map[100] = 100;
        }

        _points.Clear();
        foreach (var kvp in map)
        {
            _points.Add(new CalibrationPoint(kvp.Key, kvp.Value));
        }

        RebuildLut();
    }

    /// <summary>
    /// 新增或更新指定输入点的输出值，返回最终排序后的索引
    /// </summary>
    public int SetPoint(int input, int output)
    {
        int x = Math.Clamp(input, 0, 100);
        int y = Math.Clamp(output, 0, 100);

        int existingIndex = _points.FindIndex(p => p.Input == x);
        if (existingIndex >= 0)
        {
            _points[existingIndex].Output = y;
        }
        else
        {
            _points.Add(new CalibrationPoint(x, y));
            _points.Sort((a, b) => a.Input.CompareTo(b.Input));
        }

        RebuildLut();
        return _points.FindIndex(p => p.Input == x);
    }

    /// <summary>
    /// 移动已有锚点（0 和 100 端点不可修改 Input，只允许修改 Output）
    /// </summary>
    public int MovePoint(int index, int newInput, int newOutput)
    {
        if (index < 0 || index >= _points.Count)
            return -1;

        var point = _points[index];
        int clampedY = Math.Clamp(newOutput, 0, 100);

        // 0 和 100 端点锁定横坐标
        if (point.Input == 0 || point.Input == 100)
        {
            point.Output = clampedY;
            RebuildLut();
            return index;
        }

        int clampedX = Math.Clamp(newInput, 1, 99);
        // 避免与其他点横坐标冲突
        while (_points.Where((p, i) => i != index).Any(p => p.Input == clampedX))
        {
            if (clampedX < 99) clampedX++;
            else break;
        }
        if (_points.Where((p, i) => i != index).Any(p => p.Input == clampedX))
        {
            clampedX = point.Input;
        }

        point.Input = clampedX;
        point.Output = clampedY;
        _points.Sort((a, b) => a.Input.CompareTo(b.Input));
        RebuildLut();

        return _points.FindIndex(p => p.Input == clampedX);
    }

    /// <summary>
    /// 删除锚点（0 和 100 端点不可删除）
    /// </summary>
    public bool RemovePointAt(int index)
    {
        if (index <= 0 || index >= _points.Count - 1)
            return false;

        _points.RemoveAt(index);
        RebuildLut();
        return true;
    }

    /// <summary>
    /// 将预设亮度点同步为锚点（保留当前曲线形状）
    /// </summary>
    public void SyncAnchorInputs(IEnumerable<int> presetInputs)
    {
        var inputs = new SortedSet<int> { 0, 100 };
        foreach (int raw in presetInputs)
        {
            inputs.Add(Math.Clamp(raw, 0, 100));
        }

        var newPoints = new List<CalibrationPoint>(inputs.Count);
        foreach (int x in inputs)
        {
            newPoints.Add(new CalibrationPoint(x, Evaluate(x)));
        }

        SetPoints(newPoints);
    }

    /// <summary>
    /// O(1) 查表计算校准后的实际亮度
    /// </summary>
    public int Evaluate(int targetBrightness)
    {
        return _lut[Math.Clamp(targetBrightness, 0, 100)];
    }

    /// <summary>
    /// 根据某台显示器的实际物理输出反推对应的统一目标亮度 (0..100)
    /// 用于同步拖动某台显示器滑条时：确保被拖动显示器严格保持用户指定物理亮度，同时其余屏按各自曲线协调同步
    /// </summary>
    public int InverseEvaluate(int actualOutput)
    {
        int clamped = Math.Clamp(actualOutput, 0, 100);
        int bestX = clamped;
        int bestDiff = int.MaxValue;

        for (int x = 0; x <= 100; x++)
        {
            int diff = Math.Abs(_lut[x] - clamped);
            if (diff < bestDiff || (diff == bestDiff && Math.Abs(x - clamped) < Math.Abs(bestX - clamped)))
            {
                bestDiff = diff;
                bestX = x;
            }
        }

        return bestX;
    }

    /// <summary>
    /// 是否为未校准的 1:1 直线
    /// </summary>
    public bool IsIdentity()
    {
        for (int i = 0; i <= 100; i++)
        {
            if (_lut[i] != i)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 克隆曲线副本（用于对话框编辑与取消回滚）
    /// </summary>
    public CalibrationCurve Clone()
    {
        var copy = new List<CalibrationPoint>(_points.Count);
        foreach (var p in _points)
        {
            copy.Add(new CalibrationPoint(p.Input, p.Output));
        }
        return new CalibrationCurve(copy);
    }

    /// <summary>
    /// 导出可序列化的锚点列表
    /// </summary>
    public List<CalibrationPoint> ToSerializablePoints()
    {
        var list = new List<CalibrationPoint>(_points.Count);
        foreach (var p in _points)
        {
            list.Add(new CalibrationPoint(p.Input, p.Output));
        }
        return list;
    }

    /// <summary>
    /// 分段线性插值生成 0..100 查找表
    /// </summary>
    private void RebuildLut()
    {
        if (_points.Count < 2)
        {
            for (int i = 0; i <= 100; i++)
                _lut[i] = (byte)i;
            return;
        }

        int segIndex = 0;
        for (int x = 0; x <= 100; x++)
        {
            while (segIndex < _points.Count - 2 && x > _points[segIndex + 1].Input)
            {
                segIndex++;
            }

            var p0 = _points[segIndex];
            var p1 = _points[segIndex + 1];

            if (p1.Input == p0.Input)
            {
                _lut[x] = (byte)Math.Clamp(p0.Output, 0, 100);
            }
            else
            {
                double t = (double)(x - p0.Input) / (p1.Input - p0.Input);
                double y = p0.Output + t * (p1.Output - p0.Output);
                _lut[x] = (byte)Math.Clamp((int)Math.Round(y), 0, 100);
            }
        }
    }
}
