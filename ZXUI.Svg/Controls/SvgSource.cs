using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;
using ZXUI.Svg.Core;

namespace ZXUI.Svg.Controls;

/// <summary>
/// ZXUI 轻量 SVG 的 Avalonia 绑定层 —— 把 Core 的 <see cref="SvgDocument"/> 包成 Avalonia <see cref="IImage"/>。
/// 矢量保真（通过 <see cref="SvgDrawOperation"/> 直接画 SkiaSharp 路径，无 PNG 光栅化）。
/// </summary>
public sealed class SvgSource : IImage
{
    /// <summary>底层 Core 文档。</summary>
    public SvgDocument Document { get; }

    /// <summary>原始资源 URI。</summary>
    public string Path => Document.Path;

    /// <summary>viewBox 固有尺寸。</summary>
    public Size Size
    {
        get
        {
            var s = Document.Size;
            return new Size(s.Width, s.Height);
        }
    }

    public SvgSource(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
        using var stream = AssetLoader.Open(new Uri(path));
        Document = SvgDocument.Load(stream, path);
    }

    public SvgSource(SvgDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <inheritdoc/>
    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        var paths = Document.Paths;
        if (paths is null || paths.Count == 0) return;

        // paint 所有权交给 Op（deferred 渲染，不能提前 Dispose）
        var paint = BuildDefaultPaint();
        context.Custom(new SvgDrawOperation(paths, Document.ViewBox, destRect, paint, 2));
    }

    internal static SKPaint BuildDefaultPaint() => new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        StrokeWidth = 2f,
        Color = SKColors.Gray,
        IsAntialias = true,
    };
}
