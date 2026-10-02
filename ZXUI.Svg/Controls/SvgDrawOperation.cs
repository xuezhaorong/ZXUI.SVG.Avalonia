using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using ZXUI.Svg.Core;

namespace ZXUI.Svg.Controls;

/// <summary>
/// 矢量 SVG 绘制操作 —— 通过 SkiaSharp lease 直接在 Avalonia GPU 渲染层画 SKPath，
/// 不光栅化到 PNG，任何 DPI 都保持矢量锐利。
/// </summary>
internal sealed class SvgDrawOperation : ICustomDrawOperation
{
    private readonly IReadOnlyList<SKPath> _paths;
    private readonly SKRect _viewBox;
    private readonly Rect _bounds;
    private readonly SKPaint _paint;
    private readonly double _strokeWidth;

    public SvgDrawOperation(
        IReadOnlyList<SKPath> paths,
        SKRect viewBox,
        Rect bounds,
        SKPaint paint,
        double strokeWidth)
    {
        _paths = paths;
        _viewBox = viewBox;
        _bounds = bounds;
        _paint = paint;
        _strokeWidth = strokeWidth;
    }

    /// <summary>
    /// Bounds 是相对当前 transform 的本地矩形。
    /// Render 被调用时 DrawingContext 已平移到控件自身原点，所以 (0,0,...) 即可。
    /// </summary>
    public Rect Bounds => _bounds;

    public void Dispose() => _paint.Dispose();

    public bool HitTest(Point p) => _bounds.Contains(p);

    // 强制不去重 —— 不同实例 / 不同 paint 状态都应被独立绘制。
    // 之前的 ReferenceEquals(paths) 实现可能让渲染器误判为"没变化"而跳过。
    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (lease is null) return;
        using var handle = lease.Lease();
        var canvas = handle.SkCanvas;

        var srcW = _viewBox.Width;
        var srcH = _viewBox.Height;
        if (srcW <= 0 || srcH <= 0) return;

        var scaleX = (float)(_bounds.Width  / srcW);
        var scaleY = (float)(_bounds.Height / srcH);

        canvas.Save();
        canvas.Translate((float)(-_viewBox.Left * scaleX), (float)(-_viewBox.Top * scaleY));
        canvas.Scale(scaleX, scaleY);
        foreach (var path in _paths)
            canvas.DrawPath(path, _paint);
        canvas.Restore();
    }
}
