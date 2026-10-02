# ZXUI.Svg

一套**轻量、矢量、零负担**的 Avalonia SVG 图标方案。直接解析 SVG 并通过 SkiaSharp 在 GPU 渲染层绘制，不引入任何重型 SVG 框架，**不光栅化到 PNG**，任意 DPI / 缩放下都保持锐利。

- ✅ **真矢量**：画的是 `SKPath` 路径命令，不是位图
- ✅ **运行时换色 / 改粗细**：颜色与描边宽度由控件属性驱动，无需为每个色号复制资源
- ✅ **Avalonia-native**：`Source` 就是标准 `IImage`，能塞进 `Image`、`SvgIcon`、`ImageBrush`
- ✅ **核心可独立使用**：底层 `ZXUI.Svg.Core` 是纯 .NET 库，控制台 / 服务器也能用

---

## 整体架构

方案拆成**两个包**，按"纯逻辑核心"与"UI 绑定层"分离：

```
┌─────────────────────────────────────────────────────────────┐
│                      你的 Avalonia 应用                      │
│                                                             │
│   <Image Source="{z:Svg ...}">   <z:SvgIcon Foreground=..> │
└───────────┬──────────────────────────────┬──────────────────┘
            │ IImage                        │ Control
┌───────────▼──────────────────────────────▼──────────────────┐
│                    ZXUI.Svg  （UI 绑定层）                    │
│                                                             │
│   SvgSource  : IImage     SvgIcon : TemplatedControl        │
│   SvgExtension            SvgDrawOperation : ICustomDrawOp  │
└───────────┬─────────────────────────────────────────────────┘
            │ SvgDocument / SKPath
┌───────────▼─────────────────────────────────────────────────┐
│                  ZXUI.Svg.Core  （纯 .NET 核心）              │
│                                                             │
│   SvgDocument    加载 / 解析 / 渲染入口                        │
│   SvgParser      SVG XML  →  SKPath                          │
│   SvgSize        纯 .NET 尺寸结构                             │
│                                                             │
│   依赖：仅 SkiaSharp（无 Avalonia / 无 UI 框架）               │
└─────────────────────────────────────────────────────────────┘
```

**依赖方向是单向的**：

```
ZXUI.Svg.Core  →  SkSharp
ZXUI.Svg       →  ZXUI.Svg.Core + Avalonia
```

Core 不知道 Avalonia 的存在，因此可以脱离 UI 在任意 .NET 环境运行（生成 PNG、服务端渲染、拿到 `SKPicture` 二次加工等）；Avalonia 绑定层只负责协议翻译（`avares://` 资源）和把矢量路径接入 Avalonia 渲染管线。

### 模块职责

| 包 | 关键类型 | 职责 |
|---|---|---|
| **ZXUI.Svg.Core** | `SvgDocument` | SVG 文档模型：加载、缓存、渲染 |
| | `SvgParser` | 把 SVG XML 解析为 `SKPath` 列表 |
| | `SvgSize` | 不依赖任何框架的尺寸值类型 |
| **ZXUI.Svg** | `SvgSource` | 把 `SvgDocument` 包装成 Avalonia `IImage` |
| | `SvgIcon` | 真正的控件，提供 `Foreground` / `Background` / `StrokeWidth` |
| | `SvgExtension` | XAML 简写 `{z:Svg uri}` |
| | `SvgDrawOperation` | 把 `SKPath` 注入 Avalonia GPU 渲染的自定义绘制操作 |

---

## 安装

Avalonia 项目只需装主包，Core 会作为依赖自动带入：

```bash
dotnet add package ZXUI.Svg
```

只想要纯 .NET 核心（不引 Avalonia）：

```bash
dotnet add package ZXUI.Svg.Core
```

> 运行要求：Avalonia 11+，目标框架 net10.0。

---

## 三种用法

### ① 普通 `Image`：直接用 `SvgSource` 当数据源

```xml
<Image Width="20" Height="20"
       Source="{z:Svg /Assets/Icons/chevron-down.svg}" />
```

### ② `SvgIcon` 控件：放进任意 Content 区，支持换色 / 改粗细

```xml
<Button>
    <z:SvgIcon Width="12" Height="12" StrokeWidth="2"
               Source="{z:Svg /Assets/Icons/chevron-down.svg}"
               Foreground="{DynamicResource MyTextMutedBrush}" />
</Button>
```

### ③ 当作画刷

```xml
<Border Width="24" Height="24"
        Background="{z:Svg /Assets/Icons/chevron-down.svg}" />
```

---

## SvgIcon 属性

| 属性 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `Source` | `IImage?` | `null` | SVG 数据。通常填 `SvgSource`（用 `{z:Svg}` 简写）；任意 `IImage` 也可 |
| `Width` / `Height` | `double` | — | 显示尺寸 |
| `Foreground` | `IBrush?` | `null` | 描边颜色，覆盖 SVG 里的 `currentColor`；未设置时回退灰色 |
| `Background` | `IBrush?` | `null` | 图标底色，在路径之前填充 |
| `StrokeWidth` | `double` | `2.0` | 描边宽度 |
| `Stretch` | `Stretch` | `Uniform` | None / Fill / Uniform / UniformToFill |
| `HorizontalAlignment` / `VerticalAlignment` | 枚举 | `Center` | 固定尺寸的图标在更大的父容器里默认居中 |

`Foreground` / `Background` 是继承自 `TemplatedControl` 的属性，用 Style 的 `Setter` 修改会自动触发重绘。

---

## 渲染是怎么发生的（数据流）

一次 `SvgIcon` 绘制的完整链路：

```
XAML 字符串 "{z:Svg avares://.../chevron-down.svg}"
        │
        ▼ SvgExtension.ProvideValue()
SvgSource(string path)
        │ ① Avalonia AssetLoader 打开 avares:// 资源流（协议翻译，Core 不认 avares）
        ▼ SvgDocument.Load(stream, path)
SvgDocument（缓存原始 XDocument）
        │
        ▼ 布局完成，触发 SvgIcon.Render()
SvgIcon.Render(DrawingContext)
        │ ② 读取 Document.Paths（首次访问触发 SvgParser 懒解析）
        │ ③ 用 Foreground + StrokeWidth 构造 SKPaint
        ▼ context.Custom(new SvgDrawOperation(...))
SvgDrawOperation 被提交给 Avalonia 场景图
        │ ④ Avalonia 在真正出帧时回调 Op.Render（deferred）
        ▼
Op.Render(ImmediateDrawingContext)
        │ ⑤ 租用 ISkiaSharpApiLeaseFeature，直接拿到底层 SKCanvas（GPU 画布）
        │ ⑥ 矩阵变换：把 viewBox 坐标系映射到目标矩形
        ▼ canvas.DrawPath(skPath, skPaint)
矢量路径命令直接进入 GPU —— 完成，无位图，与 DPI 无关
```

### 关键设计点

**1. 几何体懒解析并缓存，颜色 / 粗细不重新解析**

`SvgParser` 只在首次访问 `Paths` 时跑一次，结果（`SKPath` 列表）缓存在 `SvgDocument` 上。`Foreground` / `StrokeWidth` 改变只影响新建的 `SKPaint`，不会重新解析 XML —— 几何体与外观解耦。

**2. Paint 的所有权交给 DrawOperation**

Avalonia 是 **deferred（延迟）渲染**：`Render()` 返回后才真正出帧。如果用 `using var paint`，方法返回时 paint 的 native 资源就被释放了，等 Op 真正绘制时拿到的是已释放的 paint，什么都画不出来。因此 paint 不用 `using`，所有权移交给 `SvgDrawOperation`，在 `Op.Dispose()`（场景图淘汰节点）时统一释放。

**3. 颜色提取走 `ISolidColorBrush` 接口**

XAML 里 `Foreground="White"` 这类字符串会被 Avalonia 解析成 `ImmutableSolidColorBrush`（冻结的高性能实现），它**不是** `SolidColorBrush` 的子类，但两者都实现 `ISolidColorBrush`。把 brush 转成 `SKColor` 时必须判断接口，否则字符串颜色会错误地落到黑色兜底。

**4. 坐标映射在 GPU 层完成**

`SvgDrawOperation` 通过 `Translate + Scale` 矩阵把 SVG 的 `viewBox` 坐标系压到目标矩形，所有变换都是 GPU 矩阵运算，路径本身不被修改或光栅化，因此任意尺寸都清晰。

---

## 支持的 SVG 子集

| 支持 | 暂不支持（按需扩展） |
|---|---|
| `<polyline points="x y ...">` | `<path d="...">` 完整 path mini-language |
| `<polygon points="...">` | `<circle>` / `<ellipse>` / `<rect>` |
| `<line x1 y1 x2 y2>` | `<g>` 嵌套 / `transform` / `<defs>` / `<use>` / `<symbol>` |
| `viewBox` | 渐变 / 滤镜 / 遮罩 / 图案 |
| `stroke`（含 `currentColor`） | `<style>` CSS、`<text>`、`<image>`、动画 |
| `stroke-width` | |

这覆盖了 Feather / Lucide 一类**线条图标库**的绝大多数形态。解析器在 `SvgParser` 中按元素分发，新增一种元素只需加一个分支，不影响现有调用方。

### 推荐的 SVG 文件写法

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none"
     stroke="currentColor" stroke-width="2"
     stroke-linecap="round" stroke-linejoin="round">
    <polyline points="6 9 12 15 18 9"></polyline>
</svg>
```

- `viewBox` 定义坐标系，实际显示尺寸由控件的 `Width / Height` 决定
- `stroke="currentColor"` 把颜色交给控件 `Foreground`
- 文件打成 Avalonia 资源：

```xml
<AvaloniaResource Include="Assets/**/*.svg" />
```

---

## 在非 UI 场景使用 Core

底层 `ZXUI.Svg.Core` 不依赖 Avalonia，**只做矢量解析、不碰位图 / PNG**。在控制台 / 服务端可直接拿到矢量对象：

```csharp
using ZXUI.Svg.Core;

using var doc = SvgDocument.Load("/path/to/icon.svg");

IReadOnlyList<SKPath> paths = doc.Paths;   // 矢量几何
SKRect viewBox = doc.ViewBox;

// 画到自己的 SKCanvas（如何导出由你决定，Core 不做编码）
doc.DrawToCanvas(canvas, width: 64, height: 64, paint: myPaint);
```

Core 的完整 API 见 `ZXUI.Svg.Core` 的 README。

---

## 许可证

```
MIT License

Copyright (c) 2026 ZXUI Authors
```

集体署名，不涉及具体个人。再分发时保留版权声明与 MIT 协议全文即可（商用、修改、闭源使用均允许）。完整文本见包内 `LICENSE` 文件。
