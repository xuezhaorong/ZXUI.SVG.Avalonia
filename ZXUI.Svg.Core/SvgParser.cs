using System.Globalization;
using System.Xml.Linq;
using SkiaSharp;

namespace ZXUI.Svg.Core;

/// <summary>
/// SVG 解析结果 —— 把 <c>polyline</c> / <c>polygon</c> / <c>line</c> 转成 <see cref="SKPath"/> 列表，
/// 同时记下 viewBox 用于坐标系映射。
/// </summary>
internal sealed record SvgGeometry(
    double ViewBoxX,
    double ViewBoxY,
    double ViewBoxWidth,
    double ViewBoxHeight,
    IReadOnlyList<SKPath> Paths)
{
    public SKRect ToSKRect() => new(
        (float)ViewBoxX, (float)ViewBoxY,
        (float)(ViewBoxX + ViewBoxWidth),
        (float)(ViewBoxY + ViewBoxHeight));
}

/// <summary>
/// SVG 解析器 —— <see cref="XDocument"/> → <see cref="SvgGeometry"/>。
/// </summary>
internal static class SvgParser
{
    /// <summary>完整解析一个 SVG 文档。</summary>
    public static SvgGeometry? Parse(XDocument doc)
    {
        var (vbX, vbY, vbW, vbH) = ReadViewBox(doc.Root);
        if (doc.Root is null) return null;

        var paths = new List<SKPath>();
        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };

        foreach (var el in doc.Root.Elements())
        {
            switch (el.Name.LocalName)
            {
                case "polyline":
                case "polygon":
                    if (TryParsePoints(el.Attribute("points")?.Value, out var pts))
                        paths.Add(BuildPolylinePath(pts, el.Name.LocalName == "polygon"));
                    break;

                case "line":
                    if (TryParseLine(el, out var linePath))
                        paths.Add(linePath);
                    break;

                case "path":
                    // 暂不解析 path d="..." 完整 mini-language（按需后续加）
                    break;
            }
        }
        return new SvgGeometry(vbX, vbY, vbW, vbH, paths);
    }

    /// <summary>只探 viewBox，不构造几何。供 <see cref="SvgDocument"/> 构造时同步设 <see cref="SvgDocument.Size"/>。</summary>
    public static SvgSize PeekSize(XElement? root)
    {
        if (root is null) return new SvgSize(24, 24);
        var (_, _, w, h) = ReadViewBox(root);
        return new SvgSize(w, h);
    }

    private static (double X, double Y, double W, double H) ReadViewBox(XElement? root)
    {
        if (root is null) return (0, 0, 24, 24);

        var viewBox = root.Attribute("viewBox")?.Value;
        if (!string.IsNullOrEmpty(viewBox))
        {
            var parts = viewBox.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
                && w > 0 && h > 0)
            {
                return (x, y, w, h);
            }
        }

        var wAttr = ParseDoubleOr(root.Attribute("width")?.Value, 24);
        var hAttr = ParseDoubleOr(root.Attribute("height")?.Value, 24);
        return (0, 0, wAttr, hAttr);
    }

    private static double ParseDoubleOr(string? s, double fallback)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static bool TryParsePoints(string? value, out (double X, double Y)[] points)
    {
        points = Array.Empty<(double, double)>();
        if (string.IsNullOrEmpty(value)) return false;

        var nums = value.Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var pts = new List<(double, double)>(nums.Length / 2);
        for (int i = 0; i + 1 < nums.Length; i += 2)
        {
            if (double.TryParse(nums[i],     NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                && double.TryParse(nums[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                pts.Add((x, y));
            }
        }
        points = pts.ToArray();
        return points.Length >= 2;
    }

    private static bool TryParseLine(XElement el, out SKPath path)
    {
        path = null!;
        if (double.TryParse(el.Attribute("x1")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x1)
            && double.TryParse(el.Attribute("y1")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y1)
            && double.TryParse(el.Attribute("x2")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x2)
            && double.TryParse(el.Attribute("y2")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y2))
        {
            var p = new SKPath();
            p.MoveTo((float)x1, (float)y1);
            p.LineTo((float)x2, (float)y2);
            path = p;
            return true;
        }
        return false;
    }

    private static SKPath BuildPolylinePath((double X, double Y)[] points, bool closed)
    {
        var p = new SKPath();
        p.MoveTo((float)points[0].X, (float)points[0].Y);
        for (int i = 1; i < points.Length; i++)
            p.LineTo((float)points[i].X, (float)points[i].Y);
        if (closed) p.LineTo((float)points[0].X, (float)points[0].Y);
        return p;
    }
}
