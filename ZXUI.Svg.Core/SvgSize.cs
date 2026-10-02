namespace ZXUI.Svg.Core;

/// <summary>
/// 纯 .NET 二维尺寸结构（无 Avalonia / SkiaSharp / System.Drawing 依赖）。
/// 描述 SVG viewBox 的内在尺寸，或任意渲染目标的像素尺寸。
/// </summary>
/// <param name="Width">宽度（像素 / 用户单位）。</param>
/// <param name="Height">高度（像素 / 用户单位）。</param>
public readonly record struct SvgSize(double Width, double Height)
{
    public static readonly SvgSize Empty = new(0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static SvgSize FromPixels(int width, int height) => new(width, height);
}
