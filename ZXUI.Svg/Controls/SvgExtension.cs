using Avalonia.Markup.Xaml;

namespace ZXUI.Svg.Controls;

/// <summary>
/// XAML 简写：传入资源路径直接得到一个 <see cref="SvgSource"/>。
/// </summary>
/// <remarks>
/// 用法示例（在 XAML 中作为 markup extension 使用）：
/// <code lang="xml">
/// <Image Source="{}{zml:Svg /Assets/chevron-down.svg}" />
/// </code>
/// </remarks>
public sealed class SvgExtension : MarkupExtension
{
    /// <summary>avares:// 资源 URI（或相对路径，会被 <see cref="SvgSource"/> 内部按 avares 协议解析）。</summary>
    public string Path { get; set; } = "";

    /// <summary>无参构造（用于 XAML 属性语法 <c>{z:Svg Path=...}</c>）。</summary>
    public SvgExtension() { }

    /// <summary>构造时直接指定路径（用于 XAML <c>{z:Svg path}</c> 位置参数）。</summary>
    public SvgExtension(string path) { Path = path; }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider)
        => new SvgSource(Path);
}

