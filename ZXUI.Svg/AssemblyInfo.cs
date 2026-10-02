using Avalonia.Metadata;

// 跟 ZXUI.Controls 共用同一 XAML 命名空间 URI —— 调用方只需声明
// xmlns:z="https://zxml.dev/controls"，就能同时见到 Controls 与 Svg 的类。
[assembly: XmlnsDefinition("https://zxml.dev/controls", "ZXUI.Svg.Controls")]
[assembly: XmlnsPrefix("https://zxml.dev/controls", "z")]
