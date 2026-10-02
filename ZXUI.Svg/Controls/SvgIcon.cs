using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using SkiaSharp;
using ZXUI.Svg.Core;

namespace ZXUI.Svg.Controls;

/// <summary>
/// ZXUI 矢量图标控件。基于 <see cref="SvgSource"/>，叠加视觉覆盖：
/// <see cref="TemplatedControl.Foreground"/> 描边颜色、<see cref="TemplatedControl.Background"/> 底色、
/// <see cref="StrokeWidth"/> 描边粗细。
/// </summary>
/// <remarks>
/// 真矢量路径：通过 <see cref="SvgDrawOperation"/> 在 Avalonia GPU 渲染层直接画 SkiaSharp SKPath，
/// 不光栅化到 PNG，任何 DPI 下都锐利。
/// </remarks>
public class SvgIcon : TemplatedControl
{
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<SvgIcon, IImage?>(nameof(Source));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<SvgIcon, Stretch>(nameof(Stretch), Stretch.Uniform);

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<SvgIcon, double>(nameof(StrokeWidth), 2.0);

    static SvgIcon()
    {
        AffectsRender<SvgIcon>(SourceProperty, StretchProperty, StrokeWidthProperty);
        AffectsRender<SvgIcon>(TemplatedControl.ForegroundProperty);
        AffectsRender<SvgIcon>(TemplatedControl.BackgroundProperty);
    }

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    public SvgIcon()
    {
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment   = global::Avalonia.Layout.VerticalAlignment.Center;
    }

    public override void Render(DrawingContext context)
    {
        // Render 时 context 已平移到控件自身原点，所以本地坐标系的 X/Y=0。
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;

        var src = Source;
        if (src is null) return;

        var srcSize = src.Size;
        if (srcSize.Width <= 0 || srcSize.Height <= 0) return;

        var srcRect = new Rect(srcSize);
        var destRect = ComputeDestRect(new Rect(size), srcRect, Stretch);

        var bg = Background;
        if (bg is not null) context.FillRectangle(bg, destRect);

        if (src is SvgSource svgSource)
        {
            var paths = svgSource.Document.Paths;
            if (paths is null || paths.Count == 0) return;

            var brush = ResolveForeground();
            if (brush is null) return;

            // paint 不能用 using：Avalonia 是 deferred 渲染，Op.Render 在本方法返回后才执行，
            // using 会提前 Dispose paint 的 native 资源导致画不出来。
            // 所有权交给 SvgDrawOperation，由其 Dispose 统一释放。
            var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeWidth = (float)StrokeWidth,
                Color = ToSKColor(brush),
                IsAntialias = true,
            };
            context.Custom(new SvgDrawOperation(paths, svgSource.Document.ViewBox, destRect, paint, StrokeWidth));
            return;
        }

        src.Draw(context, srcRect, destRect);
    }

    private IBrush? ResolveForeground()
    {
        if (Foreground is { } f) return f;
        return Brushes.Gray;
    }

    private static SKColor ToSKColor(IBrush brush)
    {
        // 注意：必须用 ISolidColorBrush，不能只判断 SolidColorBrush ——
        // XAML 里 "White" 等字符串会解析成 ImmutableSolidColorBrush（冻结优化版），
        // 它不是 SolidColorBrush 子类，但同样实现 ISolidColorBrush。
        if (brush is ISolidColorBrush scb)
        {
            var c = scb.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        return SKColors.Black;
    }

    private static Rect ComputeDestRect(Rect dest, Rect src, Stretch stretch)
    {
        if (dest.Width <= 0 || dest.Height <= 0 || src.Width <= 0 || src.Height <= 0)
            return dest;

        var sx = dest.Width  / src.Width;
        var sy = dest.Height / src.Height;

        switch (stretch)
        {
            case Stretch.None: return new Rect(dest.X, dest.Y, src.Width, src.Height);
            case Stretch.Fill: return dest;
            case Stretch.Uniform:
            {
                var s = Math.Min(sx, sy);
                var w = src.Width  * s;
                var h = src.Height * s;
                return new Rect(dest.X + (dest.Width  - w) / 2,
                                dest.Y + (dest.Height - h) / 2, w, h);
            }
            case Stretch.UniformToFill:
            {
                var s = Math.Max(sx, sy);
                var w = src.Width  * s;
                var h = src.Height * s;
                return new Rect(dest.X + (dest.Width  - w) / 2,
                                dest.Y + (dest.Height - h) / 2, w, h);
            }
            default: return dest;
        }
    }
}
