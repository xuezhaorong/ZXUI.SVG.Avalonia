using System.Xml.Linq;
using SkiaSharp;

namespace ZXUI.Svg.Core;

/// <summary>
/// ZXUI.Svg 的纯 .NET 核心文档。零 UI 框架依赖，只用 <see cref="SkiaSharp"/>。
/// <para>
/// 只负责 SVG 的<strong>矢量解析</strong>：加载后得到 <see cref="Paths"/>（<see cref="SKPath"/> 列表），
/// 可通过 <see cref="DrawToCanvas"/> 画到任意 <see cref="SKCanvas"/>。本类型不做任何位图 / PNG 编码。
/// </para>
/// </summary>
public sealed class SvgDocument : IDisposable
{
    private SvgGeometry? _geometry;

    /// <summary>资源路径（avares:// / file:// / http:// 等，由加载方提供）。</summary>
    public string Path { get; }

    /// <summary>从 viewBox 解析出的固有尺寸。构造时同步读出（peek），失败回退 24×24。</summary>
    public SvgSize Size { get; }

    /// <summary>解析后的 SkiaSharp 矢量对象（懒构建）。可重复使用，跨多次渲染调用。</summary>
    public SKPicture? Picture => _picture;

    /// <summary>解析后的几何路径列表（懒构建，访问时触发解析）。</summary>
    public IReadOnlyList<SKPath>? Paths
    {
        get
        {
            EnsureGeometry();
            return _geometry?.Paths;
        }
    }

    /// <summary>viewBox 原始矩形（懒构建）。</summary>
    public SKRect ViewBox
    {
        get
        {
            EnsureGeometry();
            return _geometry is null ? SKRect.Empty : _geometry.ToSKRect();
        }
    }

    private SKPicture? _picture;
    private bool _disposed;

    private SvgDocument(string path, SvgSize size)
    {
        Path = path;
        Size = size;
    }

    // —— 加载入口 ——

    /// <summary>从 URI 字符串加载（支持 avares:// / file:// 等 Uri 协议）。</summary>
    public static SvgDocument Load(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
        var size = TryPeekSize(path);
        return new SvgDocument(path, size);
    }

    /// <summary>从 <see cref="Stream"/> 加载（path 仅作标识用，方便调试）。</summary>
    public static SvgDocument Load(Stream stream, string path = "<stream>")
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("Stream is not readable.", nameof(stream));

        // 用 MemoryStream 包一层以便多次访问（构造 peek + 后续解析）
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        XDocument doc;
        try { doc = XDocument.Load(ms); }
        catch (Exception ex) { throw new InvalidDataException($"Failed to parse SVG XML: {ex.Message}", ex); }

        var size = SvgParser.PeekSize(doc.Root);
        var doc2 = new SvgDocument(path, size) { _preloadedDoc = doc };
        return doc2;
    }

    /// <summary>从 <see cref="XDocument"/> 加载（最高级入口）。</summary>
    public static SvgDocument Load(XDocument doc, string path = "<xdocument>")
    {
        ArgumentNullException.ThrowIfNull(doc);
        var size = SvgParser.PeekSize(doc.Root);
        var d = new SvgDocument(path, size) { _preloadedDoc = doc };
        return d;
    }

    private XDocument? _preloadedDoc;

    /// <summary>
    /// 把 SVG 矢量内容画到调用方提供的 <see cref="SKCanvas"/> 上。用默认 paint（Gray + 2px Round）。
    /// </summary>
    public void DrawToCanvas(SKCanvas canvas, int width, int height, SKColor? background = null)
        => DrawToCanvas(canvas, width, height, background, paint: null);

    /// <summary>把 SVG 画到调用方提供的 <see cref="SKCanvas"/> 上，描边用调用方自定义 paint。</summary>
    /// <remarks>
    /// 调用方传入的 <paramref name="paint"/> 只需配置 <see cref="SKPaint.Style"/>、
    /// <see cref="SKPaint.Color"/>、<see cref="SKPaint.StrokeWidth"/>、cap/join 等
    /// "笔触相关"属性 —— <see cref="SKPaint.Typeface"/> / <see cref="SKPaint.Shader"/> /
    /// <see cref="SKPaint.ImageFilter"/> 等"填充相关"属性会被忽略（SVG 是 stroke-only）。
    /// 传 null 时使用默认 paint（Gray + 2px Round）。
    /// </remarks>
    public void DrawToCanvas(SKCanvas canvas, int width, int height, SKColor? background, SKPaint? paint)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (width <= 0 || height <= 0) throw new ArgumentException("width/height must be positive.");

        var geom = EnsureGeometry();
        if (geom is null || geom.Paths.Count == 0) return;

        canvas.Save();
        if (background.HasValue) canvas.Clear(background.Value);

        // viewBox 映射到目标矩形
        var scaleX = (float)(width  / geom.ViewBoxWidth);
        var scaleY = (float)(height / geom.ViewBoxHeight);
        canvas.Translate((float)(-geom.ViewBoxX * scaleX), (float)(-geom.ViewBoxY * scaleY));
        canvas.Scale(scaleX, scaleY);

        using var ownedPaint = paint is null ? new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = 2f,
            Color = SKColors.Gray,
            IsAntialias = true,
        } : null!;

        var activePaint = paint ?? ownedPaint;
        foreach (var path in geom.Paths)
            canvas.DrawPath(path, activePaint);

        canvas.Restore();
    }

    // —— 内部 ——

    private SvgGeometry? EnsureGeometry()
    {
        if (_geometry is not null) return _geometry;
        if (_disposed) return null;

        try
        {
            XDocument doc;
            if (_preloadedDoc is not null)
            {
                doc = _preloadedDoc;
            }
            else
            {
                using var stream = OpenStream();
                doc = XDocument.Load(stream);
            }
            _geometry = SvgParser.Parse(doc);

            // 顺便填充 Picture 缓存（按 geom 一次性录制）
            if (_geometry is not null)
            {
                using var recorder = new SKPictureRecorder();
                using (var canvas = recorder.BeginRecording(new SKRect(
                           (float)_geometry.ViewBoxX, (float)_geometry.ViewBoxY,
                           (float)(_geometry.ViewBoxX + _geometry.ViewBoxWidth),
                           (float)(_geometry.ViewBoxY + _geometry.ViewBoxHeight))))
                using (var paint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeJoin = SKStrokeJoin.Round,
                    StrokeWidth = 2f,
                    Color = SKColors.Gray,
                    IsAntialias = true,
                })
                {
                    foreach (var p in _geometry.Paths)
                        canvas.DrawPath(p, paint);
                }
                _picture = recorder.EndRecording();
            }
        }
        catch
        {
            _geometry = null;
        }
        return _geometry;
    }

    private Stream OpenStream()
    {
        var uri = new Uri(Path, UriKind.RelativeOrAbsolute);
        // 简单协议支持：file:// 走本地 IO，其他走 HttpClient（avares:// 等 URI 由调用方包成 stream 再走 Load(stream)）
        if (uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeFile)
        {
            return File.OpenRead(uri.LocalPath);
        }
        // 兜底：当成相对路径
        return File.OpenRead(Path);
    }

    private static SvgSize TryPeekSize(string path)
    {
        try
        {
            var uri = new Uri(path, UriKind.RelativeOrAbsolute);
            Stream? stream = null;
            if (uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeFile)
                stream = File.OpenRead(uri.LocalPath);
            else
                stream = File.OpenRead(path);

            using (stream)
            {
                var doc = XDocument.Load(stream);
                return SvgParser.PeekSize(doc.Root);
            }
        }
        catch
        {
            return new SvgSize(24, 24);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _picture?.Dispose();
        _picture = null;
        _geometry = null;
        _preloadedDoc = null;
    }
}
