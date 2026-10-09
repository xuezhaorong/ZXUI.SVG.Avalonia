using System.Globalization;
using System.Xml.Linq;
using SkiaSharp;

namespace ZXUI.Svg.Core;

/// <summary>
/// SVG 解析结果 —— 把 <c>polyline</c> / <c>polygon</c> / <c>line</c> / <c>path</c> /
/// <c>circle</c> / <c>ellipse</c> / <c>rect</c> 转成 <see cref="SKPath"/> 列表，
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
                    // path mini-language（M/L/H/V/C/S/Q/T/A/Z）交给 SkiaSharp 原生解析
                    var d = el.Attribute("d")?.Value;
                    if (!string.IsNullOrWhiteSpace(d))
                    {
                        var pathData = SKPath.ParseSvgPathData(d);
                        if (pathData is not null) paths.Add(pathData);
                    }
                    break;

                case "circle":
                    if (TryParseCircle(el, out var circlePath))
                        paths.Add(circlePath);
                    break;

                case "ellipse":
                    if (TryParseEllipse(el, out var ellipsePath))
                        paths.Add(ellipsePath);
                    break;

                case "rect":
                    if (TryParseRect(el, out var rectPath))
                        paths.Add(rectPath);
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

    /// <summary>
    /// 解析 <c>&lt;circle cx cy r&gt;</c>。cx/cy 缺省视为 0；r 必须为正数（否则视为无效，跳过）。
    /// </summary>
    private static bool TryParseCircle(XElement el, out SKPath path)
    {
        path = null!;
        if (!double.TryParse(el.Attribute("r")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
            || r <= 0)
        {
            return false;
        }

        double.TryParse(el.Attribute("cx")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cx);
        double.TryParse(el.Attribute("cy")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cy);

        path = new SKPath();
        path.AddCircle((float)cx, (float)cy, (float)r, SKPathDirection.Clockwise);
        return true;
    }

    /// <summary>
    /// 解析 <c>&lt;ellipse cx cy rx ry&gt;</c>。cx/cy 缺省视为 0；rx/ry 必须为正数（否则视为无效，跳过）。
    /// </summary>
    private static bool TryParseEllipse(XElement el, out SKPath path)
    {
        path = null!;
        if (!double.TryParse(el.Attribute("rx")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rx)
            || !double.TryParse(el.Attribute("ry")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ry)
            || rx <= 0 || ry <= 0)
        {
            return false;
        }

        double.TryParse(el.Attribute("cx")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cx);
        double.TryParse(el.Attribute("cy")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cy);

        path = new SKPath();
        path.AddOval(
            new SKRect((float)(cx - rx), (float)(cy - ry), (float)(cx + rx), (float)(cy + ry)),
            SKPathDirection.Clockwise);
        return true;
    }

    /// <summary>
    /// 解析 <c>&lt;rect x y width height rx ry&gt;</c>。x/y 缺省视为 0；
    /// width/height 必须为正数；rx/ry 缺省视为 0；
    /// SVG 规范：rx/ry 只写一个时，另一个取其值；
    /// rx/ry 超过对应边一半时钳到一半；负值视作 0。
    /// </summary>
    private static bool TryParseRect(XElement el, out SKPath path)
    {
        path = null!;
        if (!double.TryParse(el.Attribute("width")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
            || !double.TryParse(el.Attribute("height")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
            || w <= 0 || h <= 0)
        {
            return false;
        }

        double.TryParse(el.Attribute("x")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x);
        double.TryParse(el.Attribute("y")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y);

        double rx = 0, ry = 0;
        var hasRx = double.TryParse(el.Attribute("rx")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out rx);
        var hasRy = double.TryParse(el.Attribute("ry")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out ry);
        if (hasRx && !hasRy) ry = rx;
        else if (hasRy && !hasRx) rx = ry;
        if (rx < 0) rx = 0;
        if (ry < 0) ry = 0;
        rx = Math.Min(rx, w / 2);
        ry = Math.Min(ry, h / 2);

        var rect = new SKRect((float)x, (float)y, (float)(x + w), (float)(y + h));
        path = new SKPath();
        if (rx > 0 || ry > 0)
            path.AddRoundRect(rect, (float)rx, (float)ry, SKPathDirection.Clockwise);
        else
            path.AddRect(rect, SKPathDirection.Clockwise);
        return true;
    }
}
