# 基于Google Skia的.NE Core UI双层架构SVG矢量解析库

一个**轻量、矢量、零负担**的 `Avalonia SVG` 图标解析库，直接解析 `SVG `并通过 `SkiaSharp` 在 `GPU` 渲染层绘制，采用`.NET核心` + `UI绑定`双层架构，将`.NET SVG Core`分离，实现UI绑定层的自由替换，可以替换为`WPF`等UI框架。

- ✅ **真矢量**：画的是 `SKPath` 路径命令，不是位图。
- ✅ **运行时换色 / 改粗细**：颜色与描边宽度由控件属性驱动，无需为每个色号复制资源。
- ✅ **Avalonia-native**：`Source` 就是标准 `IImage`，能塞进 `Image`、`SvgIcon`、`ImageBrush`。
- ✅ **核心可独立使用**：底层 `ZXUI.Svg.Core` 是纯 `.NET` 库，控制台 / 服务器也能用。

## 整体架构

方案拆成**两个包**，按**纯逻辑核心**与**UI 绑定层**分离：
<img width="1540" height="1514" alt="image" src="https://github.com/user-attachments/assets/1be010e4-dd8e-4359-a9a6-959e1002351f" />


- `IImage` ：
   `Avalonia`生态的图片接口，给`Avalonia`中任何接收`IImage`接口的控件API（`Image.Source`等）规范数据结构属性，通过 `SvgSource` 实现它，将 `SvgDocument`（代表`SVG`的数据对下）发了一张"我是图片"的通行证，包装成一种特殊的数据结构。
- `Control`：
   库提供给`UI`层展示`SVG`图片的特殊是控件，在 `IImage`之上叠加了交互/外观能力，能够直接参与布局，并保留数据绑定的控件属性。
- `SKPath`：
   矢量几何`SkiaSharp`的对象，表示一条路径的几何形状：moveTo、lineTo、cubicTo、arc…这些指令的集合。它本身不含颜色、粗细，只是"画什么形状"。`SvgParser`把 `SVG` 里的`<path d="M..."/>`等数据解析成 `SKPath`。
- `SvgDocument`
   把 `SVG XML` 解析成 `List<SKPath> + viewBox`，通过懒解析路径，实现**跨多次渲染复用解析结果**。

## 安装使用

Avalonia 项目只需装主包，Core 会作为依赖自动带入：

```c#
dotnet add package ZXUI.Svg
```

一共有三种用法：

1. 普通的 `Image`：直接用 `SvgSource` 当数据源：

```xml
<Image Width="20" Height="20"  
       Source="{z:Svg /Assets/Icons/chevron-down.svg}" />
```

1. 库提供的`SvgIcon` 控件：放进任意 `Content` 区，支持换色 / 改粗细：

```xml
<Button>  
    <z:SvgIcon Width="12" Height="12" StrokeWidth="2"  
               Source="{z:Svg /Assets/Icons/chevron-down.svg}"  
               Foreground="{DynamicResource MyTextMutedBrush}" />  
</Button>
```

- `SvgIcon` 的属性

| 属性                                        | 类型      | 默认      | 说明                                                         |
| ------------------------------------------- | --------- | --------- | ------------------------------------------------------------ |
| `Source`                                    | `IImage?` | `null`    | SVG 数据。通常填 `SvgSource`（用 `{z:Svg}` 简写）；任意 `IImage` 也可 |
| `Width` / `Height`                          | `double`  | —         | 显示尺寸                                                     |
| `Foreground`                                | `IBrush?` | `null`    | 描边颜色，覆盖 SVG 里的 `currentColor`；未设置时回退灰色     |
| `Background`                                | `IBrush?` | `null`    | 图标底色，在路径之前填充                                     |
| `StrokeWidth`                               | `double`  | `2.0`     | 描边宽度                                                     |
| `Stretch`                                   | `Stretch` | `Uniform` | None / Fill / Uniform / UniformToFill                        |
| `HorizontalAlignment` / `VerticalAlignment` | 枚举      | `Center`  | 固定尺寸的图标在更大的父容器里默认居中                       |

`Foreground` / `Background` 是继承自 `TemplatedControl` 的属性，用 Style 的 `Setter` 修改会自动触发重绘。

1. 当作**背景画刷**：

```xml
<Border Width="24" Height="24"  
        Background="{z:Svg /Assets/Icons/chevron-down.svg}" />
```

## 支持的 SVG 子集

| 元素 | 支持情况 |
|---|---|
| `<path d="...">` | ✅ 1.0.1 起，path mini-language（M/L/H/V/C/S/Q/T/A/Z）由 `SKPath.ParseSvgPathData` 原生解析 |
| `<polyline>` / `<polygon>` | ✅ points 解析为 SKPath |
| `<line>` | ✅ x1/y1/x2/y2 |
| `<circle>` | ✅ 1.0.4 起，`SKPath.AddCircle`；缺省 cx/cy=0，r 必须为正 |
| `<ellipse>` | ✅ 1.0.4 起，`SKPath.AddOval`；缺省 cx/cy=0，rx/ry 必须为正 |
| `<rect>` | ✅ 1.0.4 起，`SKPath.AddRect` / `AddRoundRect`；x/y 缺省 0；rx/ry 可只写一个、互相继承；超过 width/2 时自动钳到一半 |
| `viewBox` / `width` / `height` | ✅ 坐标系映射，缺 viewBox 时回退宽高属性 |
| `stroke="currentColor"` | ✅ 颜色由 `SvgIcon.Foreground` 驱动，支持运行时换色 |

> 注意：描边宽度在渲染时统一取 `SvgIcon.StrokeWidth`（默认 2），并随 viewBox 到控件尺寸等比缩放；图标建议按 24×24 viewBox、stroke-width 2 绘制（Feather Icons 风格可直接使用）。

## UI绑定层移植

引入纯 `.NET` 核心（不引 `Avalonia`）：

```c#
dotnet add package ZXUI.Svg.Core
```

一次完整的`SVG`渲染路程如下：
 **阶段一：加载期**
 `<z:SvgIcon Source="{z:Svg avares://ZXUI.Themes.Sample/Assets/Icons/chevron-down.svg}" />`

1. 通过`SvgExtension.ProvideValue`解析上方语法糖，构建`SvgSource`。
2. `SvgSource`调用`AssetLoader`把**SVG资源流**打开，交给`SvgDocument.Load`。
3. `SvgDocument.Load`进行初步的解析，得到**尺寸基本信息**，然后将数据存入到`MemoryStream`数据缓存中。

阶段二：布局与渲染挂载

1. `SvgIcon`在加载时，构建自己的`Bounds`信息，即控件的边界信息，决定布局系统分给它的整块矩形，在视觉树中进行”占位“。
2. `SvgIcon.Render`触发懒加载，通过`SvgParser.Parse`把`<polyline points="6 9 12 15 18 9">`转成一条
    `SKPath`，缓存起来（之后所有重绘复用同一份）。
3. 通过`ComputeDestRect`将数据缓存中的**尺寸基本信息**源矩形等比映射进控件`Bounds`，得`destRect`。
4. 然后用`Foreground` 前景色和 `StrokeWidth`宽度组装` SKPaint`（**包含矢量路径，destRect等等信息**）渲染对象。
5. 将`SkPaint`渲染对象挂载到`Avalonia`的UI渲染线程中，等待回调的时候通过GPU进行绘制。

阶段三：

1. `Avalonia` 渲染器在随后的渲染帧里回调`SvgDrawOperation.Render`绘制操作，借用`GPU`的`SKCanvas`去实现各种**坐标变化以及矢量绘制**。
2. 绘制完成后，`SvgDrawOperation.Dispose`释放绘画资源，`SKPath`随`SvgDocument` 存活，供下一帧复用。

所以UI绑定层需要完成的几个点就是：

| Id   | Key                                    | Function                                   |
| ---- | -------------------------------------- | ------------------------------------------ |
| 1    | SvgSource: IImage                      | 资源加载 + 图片契约适配                    |
| 2    | SvgDraw0peration: ICustomDrawOperation | 拿到 `SKCanvas`，做坐标变化以及矢量绘制    |
| 3    | SvgIcon:TemplatedControl               | 控件外壳：属性、布局、`Stretch`、渲染 挂载 |

1. 资源加载，在`Avalonia`中就是要实现将字符串路径的资源加载成资源流：

```c#
using var stream = AssetLoader.Open(new Uri(path));
```

1. 渲染桥：

```c#
var canvas = /* 从框架拿一个 SKCanvas */;
canvas.Scale(destWidth / viewBox.Width, destHeight / viewBox.Height);  // viewBox → 目标矩形
canvas.Translate(-viewBox.Left, -viewBox.Top);
foreach (var path in document.Paths) canvas.DrawPath(path, paint);
```

1. 控件外壳：
    要在目标控件基类上复刻的成员：

- 5 个属性：`Source`、`Stretch`、`StrokeWidth`、`Foreground`、`Background`
- 属性变更 → 触发重绘
- 布局：`SvgIcon` 里的 `ComputeDestRect`（**None/Fill/Uniform/UniformToFill** 四种 `Stretch`
   算目标矩形）是纯数学、零框架依赖的，整段复制即可
- 重绘入口里组装 `SKPaint`（颜色取自 `Foreground`，粗细取自 `StrokeWidth`）
