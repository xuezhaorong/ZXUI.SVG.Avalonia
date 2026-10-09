# ZXUI.Svg.Core

`ZXUI.Svg` 的**纯 .NET 矢量解析核心**：解析 SVG、输出 SkiaSharp 矢量对象。零 UI 框架依赖，也**不做任何位图 / PNG 编码** —— 唯一的依赖是 **SkiaSharp**。

如果你在 Avalonia 项目里做界面，通常直接装 [`ZXUI.Svg`](../ZXUI.Svg/README.md)（会自动带上本包）。本包适合：

- 不使用 Avalonia，只想在 .NET 代码里把 SVG 解析成矢量图元
- 拿到 `SKPath` 后自己决定怎么用：画到自定义画布、嵌入 PDF、做几何运算等

---

## 边界：只做解析，不碰光栅

Core 的职责严格限定为 **SVG → 矢量对象**：

| 在范围内 | 不在范围内 |
|---|---|
| 加载 SVG（文件 / 流 / `XDocument`） | PNG / JPEG 等位图编码 |
| 解析 `viewBox` 与图形元素 | 光栅化到像素并导出文件 |
| 输出 `SKPath` / `SKPicture` | 任何 UI 框架相关的东西 |
| 把矢量内容画到任意 `SKCanvas` | |

需要位图输出的调用方，可以自己用 `Paths` + `SKCanvas` 组合 —— 这属于上层选择，不是核心职责。

---

## 架构定位

```
┌──────────────────────────────────────────────┐
│            ZXUI.Svg （Avalonia 绑定层）        │
│      SvgSource / SvgIcon / {z:Svg}           │
└───────────────────┬──────────────────────────┘
                    │ SvgDocument / SKPath
┌───────────────────▼──────────────────────────┐
│              ZXUI.Svg.Core（本包）             │
│                                              │
│   SvgDocument   加载 / 缓存 / 矢量绘制          │
│   SvgParser     SVG XML → SKPath              │
│   SvgSize       纯 .NET 尺寸值类型             │
│                                              │
│   依赖：仅 SkiaSharp                          │
└──────────────────────────────────────────────┘
```

| 类型 | 职责 |
|---|---|
| `SvgDocument` | SVG 文档模型：加载、几何体缓存、矢量绘制入口 |
| `SvgParser` | `XDocument` → `SKPath` 列表（internal） |
| `SvgSize` | 不依赖任何框架的尺寸值类型（`readonly record struct`） |

---

## 安装

```bash
dotnet add package ZXUI.Svg.Core
```

> 目标框架 net10.0，依赖 SkiaSharp。

---

## 加载方式

| 方法 | 适用场景 |
|---|---|
| `SvgDocument.Load(string path)` | 从本地文件路径加载（支持 `file://`） |
| `SvgDocument.Load(Stream stream, string path = "<stream>")` | 从任意流加载（最通用，`path` 仅作标识） |
| `SvgDocument.Load(XDocument doc, string path = "<xdocument>")` | 直接从已有的 `XDocument` 加载 |

> `avares://` 是 Avalonia 的资源协议，纯 Core 不认识。在 Avalonia 环境下应由上层用 `AssetLoader` 打开流，再调用 `Load(stream)`。

```csharp
using ZXUI.Svg.Core;

using var doc1 = SvgDocument.Load("/assets/icon.svg");

using var fs = File.OpenRead("/assets/icon.svg");
using var doc2 = SvgDocument.Load(fs, "/assets/icon.svg");

var xdoc = XDocument.Parse("<svg viewBox=\"0 0 24 24\">...</svg>");
using var doc3 = SvgDocument.Load(xdoc);
```

---

## 获取矢量对象

```csharp
using ZXUI.Svg.Core;

using var doc = SvgDocument.Load("/assets/icon.svg");

SvgSize size = doc.Size;                    // viewBox 固有尺寸
IReadOnlyList<SKPath>? paths = doc.Paths;   // 几何路径（访问触发懒解析）
SKRect viewBox = doc.ViewBox;               // viewBox 矩形
SKPicture? picture = doc.Picture;           // 用默认 paint 录制的矢量对象
```

拿到 `SKPath` 后可以自由处理：测量边界、几何运算、录制进自己的 `SKPicture`、嵌入 PDF 等。

## 画到任意 SKCanvas

```csharp
using var surface = SKSurface.Create(new SKImageInfo(128, 128));
SKCanvas canvas = surface.Canvas;

using var paint = new SKPaint
{
    Style       = SKPaintStyle.Stroke,
    StrokeCap   = SKStrokeCap.Round,
    StrokeJoin  = SKStrokeJoin.Round,
    StrokeWidth = 3f,
    Color       = SKColors.Red,
    IsAntialias = true,
};
doc.DrawToCanvas(canvas, width: 128, height: 128, background: null, paint: paint);
```

`DrawToCanvas` 只负责把矢量内容画到画布，不编码、不存盘。不传 paint 时用默认灰色 2px；传入 paint 时颜色 / 粗细 / cap / join 由调用方决定。后续如何使用该画布（继续绘制、截图、嵌入 PDF 等）完全由调用方决定。

---

## SvgDocument API 一览

### 属性

| 属性 | 类型 | 说明 |
|---|---|---|
| `Path` | `string` | 资源标识（加载时传入） |
| `Size` | `SvgSize` | viewBox 固有尺寸，构造时即确定，失败回退 24×24 |
| `Paths` | `IReadOnlyList<SKPath>?` | 几何路径，懒解析 |
| `ViewBox` | `SKRect` | viewBox 矩形，懒解析 |
| `Picture` | `SKPicture?` | 用默认 paint 录制的矢量对象，懒构建 |

### 方法

| 方法 | 说明 |
|---|---|
| `static Load(string / Stream / XDocument)` | 三种加载入口 |
| `void DrawToCanvas(SKCanvas, int w, int h, SKColor? bg = null)` | 画到画布（默认 paint） |
| `void DrawToCanvas(SKCanvas, int w, int h, SKColor? bg, SKPaint? paint)` | 画到画布（自定义 paint） |
| `void Dispose()` | 释放内部 `SKPicture` 等资源 |

---

## 支持的 SVG 子集

| 支持 | 暂不支持（按需扩展） |
|---|---|
| `<polyline>` / `<polygon>` / `<line>` | `<g>` / `transform` / `<defs>` / `<use>` |
| `<path d="...">`（1.0.1 起，path mini-language） | 渐变 / 滤镜 / 遮罩 / 动画 / `<text>` |
| `<circle>` / `<ellipse>` / `<rect>`（1.0.4 起；rect 支持 rx/ry 圆角） | |
| `viewBox` | |
| `stroke` / `stroke-width` | |

针对 Feather / Lucide 一类线条图标设计。几何体只在首次访问时解析一次并缓存。

---

## 许可证

```
MIT License

Copyright (c) 2026 ZXUI Authors
```

再分发时保留版权声明与协议全文即可。完整文本见包内 `LICENSE` 文件。
