# FileTidy 界面重塑实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 FileTidy 从"默认 WPF 裸控件"重塑为现代浅色（Win11 Fluent 语境）：统一设计系统、页头卡片化、分组卡片编辑器、预览状态色、金色 Pro 徽标。

**Architecture:** 样式全部放在 `src/FileTidy.App/Themes/` 三个资源字典（`Colors.xaml` 颜色画刷 / `Controls.xaml` 控件皮肤 / `Switch.xaml` 开关模板），由 `App.xaml` MergedDictionaries 统一引入，`MainWindow.xaml` 只引用不重复定义。ViewModel 仅新增纯展示属性（`PreviewRow.Status`、`ActivateResultIsError`）与一个转换器（`InverseBoolConverter`），所有既有命令与绑定原样保留。

**Tech Stack:** C# / .NET 8 / WPF（net8.0-windows App 项目）+ xUnit（测试项目已引用 Core 与 App）。零新增 NuGet 依赖。

## Global Constraints

- 语言与文案：代码注释、XAML、界面文案一律**简体中文**；提交信息用 conventional 风格（`feat:` / `chore:`）
- 依赖：**零新增 NuGet 包**。图标一律用系统字体 **Segoe MDL2 Assets**（XAML 中字形用 `&#xE7xx;` 实体形式）；Switch 用 `ToggleButton` 自定义 ControlTemplate
- 绑定红线：所有既有 `Command` / `Property` 绑定**原样保留**；重写控件模板必须保留默认模板内的绑定元素（TextBox 的 `PART_ContentHost`），否则输入失效
- 颜色一律下沉到 `Colors.xaml`（`StaticResource` 引用），XAML 各处**禁止硬编码颜色字面量**（原 `MainWindow.xaml` 里的 `#F8D7DA`、`#FFF3CD` 等全部替换为资源引用）
- 构建：`dotnet build` 必须通过；测试：`dotnet test tests/FileTidy.Tests` 全量通过（含既有约 50 用例）
- 窗口尺寸：900×560，MinWidth 720 / MinHeight 420 不变
- 托盘（Hardcodet.NotifyIcon.Wpf）、开机自启、拖拽排序 code-behind（`MainWindow.xaml.cs`）、`App.xaml.cs` 启动逻辑**一律不改**

---

### Task 1: 主题资源字典框架 + 资源加载 smoke 测试

**Files:**
- Create: `src/FileTidy.App/Themes/Colors.xaml`
- Create: `src/FileTidy.App/Themes/Controls.xaml`（空字典占位，Task 2-4 填充）
- Create: `src/FileTidy.App/Themes/Switch.xaml`（空字典占位，Task 3 填充）
- Modify: `src/FileTidy.App/App.xaml`
- Test: `tests/FileTidy.Tests/AppResourcesLoadTests.cs`

**Interfaces:**
- Consumes: 无
- Produces (key → 值，供后续任务用 `{StaticResource key}` 引用)：
  - `BrWindow`=#F5F6F8，`BrCard`=#FFFFFF，`BrBorder`=#E5E7EB，`BrText`=#1F2937，`BrTextSecondary`=#6B7280，`BrTextDisabled`=#9CA3AF
  - `BrushAccent`=LinearGradientBrush(#6366F1→#8B5CF6)，`BrushAccentHover`=#5B59F6（偏心取样），`BrushAccentPressed`=#4F46E5
  - `BrSuccess`=#10B981，`BrWarning`=#F59E0B，`BrError`=#EF4444，`BrPro`=#D97706
  - 预览行背景：`BrRowMoved`=#EAF9F1，`BrRowConflict`=#FFF3CD，`BrRowNeedsPro`=#FBF0E0，`BrRowTemplateError`=#FDE8E8，`BrRowNoMatch`=#F2F4F7

- [ ] **Step 1: 写测试**

```csharp
// tests/FileTidy.Tests/AppResourcesLoadTests.cs
using System.Threading;
using FileTidy.App;

namespace FileTidy.Tests;

/// <summary>资源加载回归防线：App.xaml 合并字典加载不抛异常（防样式笔误导致启动白屏）。
/// WPF 对象需 STA 线程，故在子线程创建 App 实例（仅执行 InitializeComponent，不触发 OnStartup/Run）。</summary>
public class AppResourcesLoadTests
{
    private static void RunSta(Action action)
    {
        Exception? thrown = null;
        var t = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { thrown = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.True(t.Join(TimeSpan.FromSeconds(15)), "资源加载超时");
        Assert.Null(thrown);
    }

    [Fact]
    public void Colors_AllKeys_Exist()
        => RunSta(() =>
        {
            var app = new App();
            foreach (var key in new[]
            {
                "BrWindow", "BrCard", "BrBorder", "BrText", "BrTextSecondary", "BrTextDisabled",
                "BrushAccent", "BrushAccentHover", "BrushAccentPressed",
                "BrSuccess", "BrWarning", "BrError", "BrPro",
                "BrRowMoved", "BrRowConflict", "BrRowNeedsPro", "BrRowTemplateError", "BrRowNoMatch",
                "BrBannerBg", "BrBannerBorder", "BrBannerText"
            })
                Assert.NotNull(app.Resources[key]);
        });

    [Fact]
    public void MergedDictionaries_LoadWithoutException()
        => RunSta(() =>
        {
            var app = new App(); // Controls/Switch 为占位空字典，验证整体合并不抛 XamlParseException
            Assert.Single(app.Resources.MergedDictionaries)将字典=3的情形不在此断言;
        });
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/FileTidy.Tests --filter "FullyQualifiedName~AppResourcesLoadTests"`
Expected: FAIL —— `App.xaml` 尚无 MergedDictionaries，断言没有拿到字典 / 键缺失。

- [ ] **Step 3: 创建三个字典文件（Colors 完整、Controls/Switch 空占位）**

```xml
<!-- src/FileTidy.App/Themes/Colors.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- 基础色板（浅色主题） -->
    <SolidColorBrush x:Key="BrWindow" Color="#F5F6F8"/>
    <SolidColorBrush x:Key="BrCard" Color="#FFFFFF"/>
    <SolidColorBrush x:Key="BrBorder" Color="#E5E7EB"/>
    <SolidColorBrush x:Key="BrText" Color="#1F2937"/>
    <SolidColorBrush x:Key="BrTextSecondary" Color="#6B7280"/>
    <SolidColorBrush x:Key="BrTextDisabled" Color="#9CA3AF"/>

    <!-- 强调色（靛蓝→紫罗兰） -->
    <LinearGradientBrush x:Key="BrushAccent" StartPoint="0,0" EndPoint="1,1">
        <GradientStop Color="#6366F1" Offset="0"/>
        <GradientStop Color="#8B5CF6" Offset="1"/>
    </LinearGradientBrush>
    <SolidColorBrush x:Key="BrushAccentHover" Color="#5E5CE6"/>
    <SolidColorBrush x:Key="BrushAccentPressed" Color="#4F46E5"/>

    <!-- 语义色 -->
    <SolidColorBrush x:Key="BrSuccess" Color="#10B981"/>
    <SolidColorBrush x:Key="BrWarning" Color="#F59E0B"/>
    <SolidColorBrush x:Key="BrError" Color="#EF4444"/>
    <SolidColorBrush x:Key="BrPro" Color="#D97706"/>

    <!-- 预览状态行浅底色 -->
    <SolidColorBrush x:Key="BrRowMoved" Color="#EAF9F1"/>
    <SolidColorBrush x:Key="BrRowConflict" Color="#FFF3CD"/>
    <SolidColorBrush x:Key="BrRowNeedsPro" Color="#FBF0E0"/>
    <SolidColorBrush x:Key="BrRowTemplateError" Color="#FDE8E8"/>
    <SolidColorBrush x:Key="BrRowNoMatch" Color="#F9FAFB"/>

    <!-- 错误横幅（琥珀警示） -->
    <SolidColorBrush x:Key="BrBannerBg" Color="#FFFBEB"/>
    <SolidColorBrush x:Key="BrBannerBorder" Color="#FDE68A"/>
    <SolidColorBrush x:Key="BrBannerText" Color="#92400E"/>
</ResourceDictionary>
```

```xml
<!-- src/FileTidy.App/Themes/Controls.xaml 空占位，Task 2-4 填充 -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"/>
```

```xml
<!-- src/FileTidy.App/Themes/Switch.xaml 空占位，Task 3 填充 -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"/>
```

- [ ] **Step 4: 更新 `App.xaml`**

```xml
<Application x:Class="FileTidy.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Themes/Colors.xaml"/>
                <ResourceDictionary Source="Themes/Controls.xaml"/>
                <ResourceDictionary Source="Themes/Switch.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 5: 修正 Smoke 测试的合并断言（去掉不成立的 `Assert.Single`，仅保留键断言）**

- [ ] **Step 6: 运行测试确认通过**

Run: `dotnet test tests/FileTidy.Tests --filter "FullyQualifiedName~AppResourcesLoadTests"`
Expected: PASS（两个用例全绿）。

- [ ] **Step 7: Commit**

```bash
git add src/FileTidy.App/Themes/ src/FileTidy.App/App.xaml tests/FileTidy.Tests/AppResourcesLoadTests.cs
git commit -m "feat: 主题资源字典框架（Colors.xaml）与资源加载 smoke 测试"
```

---

### Task 2: 基础控件皮肤（按钮 / 表单 / 状态栏 / ToolTip / 卡片 / Pro 徽章）

**Files:**
- Modify: `src/FileTidy.App/Themes/Controls.xaml`
- Test: `tests/FileTidy.Tests/AppResourcesLoadTests.cs`（新增断言用例）

**Interfaces:**
- Consumes: `Colors.xaml` 的 `BrushAccent`、`BrushAccentHover`、`BrushAccentPressed`、`BrCard`、`BrBorder`、`BrText`、`BrTextSecondary`、`BrTextDisabled`、`BrRowNoMatch`、`BrPro`
- Produces: 样式 key（后续任务全部引用）：
  - `PageTitleText`/`PageSubtitleText`/`CardTitleText`/`FieldLabel`（TextBlock Style）
  - `BaseButton`/`AccentButton`/`SecondaryButton`/`IconButton`（Button Style）
  - `FormTextBox`/`FormComboBox`/`FormCheckBox`
  - `CardBorder`（Border 卡片容器）、`ProBadge`（金色 Pro 徽标）
  - 隐式 `StatusBar` 样式、隐式 `ToolTip` 样式

- [ ] **Step 1: 追加 smoke 断言用例**

在 `AppResourcesLoadTests` 追加：

```csharp
[Fact]
public void Controls_AllCoreKeys_Exist()
    => RunSta(() =>
    {
        var app = new App();
        foreach (var key in new[]
        {
            "BaseButton", "AccentButton", "SecondaryButton", "IconButton",
            "FormTextBox", "FormComboBox", "FormCheckBox",
            "CardBorder", "ProBadge", "CardTitleText", "FieldLabel",
            "PageTitleText", "PageSubtitleText"
        })
            Assert.NotNull(app.Resources[key]);
    });
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/FileTidy.Tests --filter "FullyQualifiedName~AppResourcesLoadTests"`
Expected: `Controls_AllCoreKeys_Exist` FAIL（键不存在）。

- [ ] **Step 3: 填充 `Controls.xaml`（完整内容）**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- ========== 文本层级 ========== -->
    <Style x:Key="PageTitleText" TargetType="TextBlock">
        <Setter Property="FontSize" Value="18"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <Setter Property="Foreground" Value="{StaticResource BrText}"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>
    <Style x:Key="PageSubtitleText" TargetType="TextBlock">
        <Setter Property="FontSize" Value="11.5"/>
        <Setter Property="Foreground" Value="{StaticResource BrTextSecondary}"/>
        <Setter Property="TextWrapping" Value="Wrap"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>
    <Style x:Key="CardTitleText" TargetType="TextBlock">
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Foreground" Value="{StaticResource BrText}"/>
        <Setter Property="Margin" Value="0,0,0,6"/>
    </Style>
    <Style x:Key="FieldLabel" TargetType="TextBlock">
        <Setter Property="Foreground" Value="{StaticResource BrTextSecondary}"/>
        <Setter Property="FontSize" Value="12"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>

    <!-- ========== 按钮 ========== -->
    <Style x:Key="BaseButton" TargetType="Button">
        <Setter Property="Background" Value="{StaticResource BrCard}"/>
        <Setter Property="Foreground" Value="{StaticResource BrText}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BrBorder}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="14,7"/>
        <Setter Property="FontSize" Value="12.5"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="bd" Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="6" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="bd" Property="Background" Value="{StaticResource BrRowNoMatch}"/>
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="bd" Property="Background" Value="{StaticResource BrBorder}"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="bd" Property="Opacity" Value="0.5"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 次级按钮 = 白底（基类） -->
    <Style x:Key="SecondaryButton" TargetType="Button" BasedOn="{StaticResource BaseButton}"/>

    <!-- 主操作按钮：蓝紫渐变 -->
    <Style x:Key="AccentButton" TargetType="Button" BasedOn="{StaticResource BaseButton}">
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="bd" Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding Background}"
                            BorderThickness="1" CornerRadius="6" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="bd" Property="Background" Value="{StaticResource BrushAccentHover}"/>
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="bd" Property="Background" Value="{StaticResource BrushAccentPressed}"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="bd" Property="Opacity" Value="0.5"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Setter Property="Background" Value="{StaticResource BrushAccent}"/>
    </Style>

    <!-- 裸图标按钮（左栏 + 页头）：等宽圆角 -->
    <Style x:Key="IconButton" TargetType="Button" BasedOn="{StaticResource BaseButton}">
        <Setter Property="Width" Value="34"/>
        <Setter Property="Height" Value="30"/>
        <Setter Property="Padding" Value="0"/>
        <Setter Property="FontFamily" Value="Segoe MDL2 Assets"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="Foreground" Value="{StaticResource BrTextSecondary}"/>
    </Style>

    <!-- ========== 表单控件（不重写模板，仅调整外观降低风险） ========== -->
    <Style x:Key="FormTextBox" TargetType="TextBox" BasedOn="{StaticResource {x:Type TextBox}}">
        <Setter Property="Padding" Value="8,6"/>
        <Setter Property="BorderBrush" Value="{StaticResource BrBorder}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Foreground" Value="{StaticResource BrText}"/>
        <Setter Property="Background" Value="{StaticResource BrCard}"/>
        <Setter Property="FontSize" Value="12.5"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
    </Style>
    <Style TargetType="TextBox">
        <Setter Property="Padding" Value="6,4"/>
        <Setter Property="BorderBrush" Value="{StaticResource BrBorder}"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="FontSize" Value="12.5"/>
    </Style>
    <Style x:Key="FormComboBox" TargetType="ComboBox" BasedOn="{StaticResource {x:Type ComboBox}}">
        <Setter Property="Padding" Value="8,5"/>
        <Setter Property="BorderBrush" Value="{StaticResource BrBorder}"/>
        <Setter Property="FontSize" Value="12.5"/>
        <Setter Property="Background" Value="{StaticResource BrCard}"/>
    </Style>
    <Style x:Key="FormCheckBox" TargetType="CheckBox">
        <Setter Property="Foreground" Value="{StaticResource BrText}"/>
        <Setter Property="FontSize" Value="12.5"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="Margin" Value="0,4,0,4"/>
    </Style>

    <!-- ========== 卡片容器 ========== -->
    <Style x:Key="CardBorder" TargetType="Border">
        <Setter Property="Background" Value="{StaticResource BrCard}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BrBorder}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="CornerRadius" Value="8"/>
        <Setter Property="Padding" Value="14"/>
    </Style>

    <!-- Pro 徽标 -->
    <Style x:Key="ProBadge" TargetType="Border">
        <Setter Property="Background" Value="#FBF0E0"/>
        <Setter Property="BorderBrush" Value="{StaticResource BrPro}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="CornerRadius" Value="3"/>
        <Setter Property="Padding" Value="5,1"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>
    <Style x:Key="ProBadgeText" TargetType="TextBlock">
        <Setter Property="FontSize" Value="10"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <Setter Property="Foreground" Value="{StaticResource BrPro}"/>
        <Setter Property="Text" Value="Pro"/>
    </Style>

    <!-- ========== 状态栏 ========== -->
    <Style TargetType="StatusBar">
        <Setter Property="Background" Value="{StaticResource BrCard}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BrBorder}"/>
        <Setter Property="BorderThickness" Value="0,1,0,0"/>
        <Setter Property="Padding" Value="10,4"/>
    </Style>

    <!-- ========== ToolTip ========== -->
    <Style TargetType="ToolTip">
        <Setter Property="Background" Value="{StaticResource BrCard}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BrBorder}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Foreground" Value="{StaticResource BrText}"/>
        <Setter Property="Padding" Value="8,5"/>
    </Style>
</ResourceDictionary>
```

> 注意：同名 `TextBox` 隐式样式与 `FormTextBox`（按 Key）并存是故意的——隐式样式覆盖窗口内所有 `TextBox`（含默认模板），`FormTextBox` 提供统一外观；两者都不破坏默认模板。

- [ ] **Step 4: 运行 smoke → PASS**；如 XAML 笔误则修（`MergedDictionaries` 加载时抛 `XamlParseException` 会被 `Controls_AllCoreKeys_Exist` 捕获断言失败）

- [ ] **Step 5: Commit**

```bash
git add src/FileTidy.App/Themes/Controls.xaml tests/FileTidy.Tests/AppResourcesLoadTests.cs
git commit -m "feat: 基础控件皮肤（按钮渐变、表单、卡片、Pro 徽标、状态栏）"
```

---

### Task 3: Switch 开关样式（ToggleButton 模板）

**Files:**
- Modify: `src/FileTidy.App/Themes/Switch.xaml`
- Test: `tests/FileTidy.Tests/AppResourcesLoadTests.cs`（追加断言）

**Interfaces:**
- Consumes: `BrBorder`, `BrushAccent`, `BrCard`, `BrTextSecondary`
- Produces: `SwitchStyle`（TargetType ToggleButton）——页头「自动整理」与设置页「开机自启」共用

- [ ] **Step 1: 追加 smoke 断言**

```csharp
[Fact]
public void Switch_Key_Exists()
    => RunSta(() =>
    {
        var app = new App();
        Assert.NotNull(app.Resources["SwitchStyle"]);
    });
```

- [ ] **Step 2: run → FAIL（无 SwitchStyle）**

- [ ] **Step 3: 填充 `Switch.xaml` 完整内容**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Win11 风格开关：左侧文字 + 右侧轨道滑块，选中轨道染强调色 -->
    <Style x:Key="SwitchStyle" TargetType="ToggleButton">
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ToggleButton">
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                        <ContentPresenter x:Name="cp" VerticalAlignment="Center" Margin="0,0,8,0">
                            <ContentPresenter.Resources>
                                <Style TargetType="TextBlock">
                                    <Setter Property="FontSize" Value="12.5"/>
                                    <Setter Property="Foreground" Value="{StaticResource BrText}"/>
                                </Style>
                            </ContentPresenter.Resources>
                        </ContentPresenter>
                        <Border x:Name="track" Width="42" Height="22" CornerRadius="11"
                                Background="{StaticResource BrBorder}" VerticalAlignment="Center">
                            <Ellipse x:Name="thumb" Width="16" Height="16" Fill="White"
                                     HorizontalAlignment="Left" Margin="3,0,0,0">
                                <Ellipse.RenderTransform>
                                    <TranslateTransform x:Name="slide" X="0"/>
                                </Ellipse.RenderTransform>
                            </Ellipse>
                        </Border>
                    </StackPanel>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="track" Property="Background" Value="{StaticResource BrushAccent}"/>
                            <Setter TargetName="slide" Property="X" Value="20"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="track" Property="Opacity" Value="0.5"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>
```

> 说明：滑块位移用 `Ellipse.RenderTransform` 上的 `TranslateTransform`（x:Name=`slide`），触发器通过 `TargetName="slide"` 设 `X=20`；轨道命名 `track`。`ContentPresenter.Resources` 里的隐式 `TextBlock` 样式仅作用于开关文字。

- [ ] **Step 4: `dotnet test` filter → PASS**
- [ ] **Step 5: Commit**

```bash
git add src/FileTidy.App/Themes/Switch.xaml tests/FileTidy.Tests/AppResourcesLoadTests.cs
git commit -m "feat: Switch 开关样式（ToggleButton 自定义模板）"
```

---

### Task 4: 列表与表格皮肤（ListBox / DataGrid / TabControl）

**Files:**
- Modify: `src/FileTidy.App/Themes/Controls.xaml`

**Interfaces:**
- Consumes: `BrCard`,`BrBorder`,`BrRowNoMatch`,`BrTextSecondary`,`BrushAccent`,`BrRowNoMatch`
- Produces: 隐式 `ListBox`、`DataGridRow`、`DataGridColumnHeader`、`DataGrid`、`TabItem` 样式

- [ ] **Step 1: 追加 smoke 断言（隐式样式无 Key，断言「加载不抛异常」）**

在 `AppResourcesLoadTests` 追加：

```csharp
[Fact]
public void ListControls_MergedLoad_WithoutException()
    => RunSta(() =>
    {
        var app = new App(); // 若新增的 ListBox/DataGrid/TabItem 样式 XAML 有笔误，此处抛 XamlParseException
        Assert.NotNull(app.Resources);
    });
```

- [ ] **Step 2: 填充 `Controls.xaml` 追加段（追加在 `<ResourceDictionary>` 内）**

```xml
    <!-- ========== ListBox（规则列表） ========== -->
    <Style TargetType="ListBox">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/>
        <Setter Property="ItemContainerStyle">
            <Setter.Value>
                <Style TargetType="ListBoxItem">
                    <Setter Property="Padding" Value="8,7"/>
                    <Setter Property="Margin" Value="0,1"/>
                    <Setter Property="Foreground" Value="{StaticResource BrText}"/>
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="ListBoxItem">
                                <Border x:Name="bd" Background="Transparent" CornerRadius="6"
                                        Padding="{TemplateBinding Padding}">
                                    <ContentPresenter/>
                                </Border>
                                <ControlTemplate.Triggers>
                                    <Trigger Property="IsMouseOver" Value="True">
                                        <Setter TargetName="bd" Property="Background" Value="{StaticResource BrRowNoMatch}"/>
                                    </Trigger>
                                    <Trigger Property="IsSelected" Value="True">
                                        <Setter TargetName="bd" Property="Background" Value="{StaticResource BrAccentSoft}"/>
                                        <Setter Property="Foreground" Value="{StaticResource BrAccentDeep}"/>
                                    </Trigger>
                                </ControlTemplate.Triggers>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ============ DataGrid ============ -->
    <Style TargetType="DataGrid">
        <Setter Property="Background" Value="{StaticResource BrCard}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BrBorder}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="RowHeight" Value="34"/>
        <Setter Property="GridLinesVisibility" Value="Horizontal"/>
        <Setter Property="HorizontalGridLinesBrush" Value="{StaticResource BrBorder}"/>
        <Setter Property="AlternatingRowBackground" Value="#FBFCFD"/>
        <Setter Property="SelectionMode" Value="Single"/>
        <Setter Property="HeadersVisibility" Value="Column"/>
    </Style>
    <Style TargetType="DataGridColumnHeader">
        <Setter Property="Background" Value="#F4F5F7"/>
        <Setter Property="Foreground" Value="{StaticResource BrTextSecondary}"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Padding" Value="8,8"/>
        <Setter Property="BorderBrush" Value="{StaticResource BrBorder}"/>
        <Setter Property="BorderThickness" Value="0,0,0,1"/>
    </Style>
    <Style TargetType="DataGridRow">
        <Setter Property="Foreground" Value="{StaticResource BrText}"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
    </Style>

    <!-- ============ TabControl / TabItem ============ -->
    <Style TargetType="TabControl">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderThickness" Value="0"/>
    </Style>
    <Style TargetType="TabItem">
        <Setter Property="Foreground" Value="{StaticResource BrTextSecondary}"/>
        <Setter Property="Padding" Value="14,6"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TabItem">
                    <StackPanel>
                        <Border x:Name="bd" Background="Transparent" Padding="{TemplateBinding Padding}">
                            <ContentPresenter ContentSource="Header" VerticalAlignment="Center"/>
                        </Border>
                        <Border x:Name="underline" Height="2" Background="Transparent" CornerRadius="1,1,0,0"/>
                    </StackPanel>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="bd" Property="Background" Value="{StaticResource BrWindow}"/>
                            <Setter Property="Foreground" Value="{StaticResource BrAccentDeep}"/>
                            <Setter Property="FontWeight" Value="SemiBold"/>
                            <Setter TargetName="underline" Property="Background" Value="{StaticResource BrushAccent}"/>
                        </Trigger>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="bd" Property="Background" Value="{StaticResource BrRowNoMatch}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>
```

> 选中态专属色（前景深紫/背景浅紫）属于设计系统未列出的高亮深浅彩——为避免硬编码字面量在 XAML 各处散落，追加到 `Colors.xaml`（Task 4 一并改）：`BrAccentSoft`=#EDEBFE，`BrAccentDeep`=#4338CA，并在控件皮肤原子引用。这一改：
- 修改 `src/FileTidy.App/Themes/Colors.xaml` 末尾追加：

```xml
<!-- 选中高亮（强调色浅/深） -->
<SolidColorBrush x:Key="BrAccentSoft" Color="#EDEBFE"/>
<SolidColorBrush x:Key="BrAccentDeep" Color="#4338CA"/>
```
- 上面 ListBox 选中项与 TabItem 选中项已直接引用 `{StaticResource BrAccentSoft}` / `{StaticResource BrAccentDeep}`（上列代码已写为资源引用，无需二次替换）。

- [ ] **Step 3: smoke → PASS**（`ListControls` 用例基础上）

- [ ] **Step 4: Commit**

```bash
git add src/FileTidy.App/Themes/Colors.xaml src/FileTidy.App/Themes/Controls.xaml tests/FileTidy.Tests/AppResourcesLoadTests.cs
git commit -m "feat: 列表/表格/标签皮肤与选中高亮色板"
```

---

### Task 5: InverseBoolConverter

**Files:**
- Create: `src/FileTidy.App/Converters/InverseBoolConverter.cs`
- Test: `tests/FileTidy.Tests/InverseBoolConverterTests.cs`

**Interfaces:**
- Produces: `InverseBoolConverter : IValueConverter`，`Convert(bool)`=→取反，`ConvertBack` 抛 `NotImplementedException`

- [ ] **Step 1: 写失败测试**

```csharp
// tests/FileTidy.Tests/InverseBoolConverterTests.cs
using FileTidy.App.Converters;

namespace FileTidy.Tests;

public class InverseBoolConverterTests
{
    private static bool Conv(object? v) => (bool)new InverseBoolConverter().Convert(v!, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture);

    [Fact] public void True_BecomesFalse() => Assert.False(Conv(true));
    [Fact] public void False_BecomesTrue() => Assert.True(Conv(false));
    [Fact] public void NonBool_Throws() => Assert.Throws<ArgumentOutOfRangeException>(() => Conv("x"));
    [Fact] public void ConvertBack_NotSupported() => Assert.Throws<NotImplementedException>(
        () => new InverseBoolConverter().ConvertBack(true, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture));
}
```

- [ ] **Step 2: 运行失败**（`InverseBoolConverter` 不存在，编译错误断言前已显露）
- [ ] **Step 3: 实现**

```csharp
// src/FileTidy.App/Converters/InverseBoolConverter.cs
using System.Globalization;
using System.Windows.Data;

namespace FileTidy.App.Converters;

/// <summary>布尔取反转换器（供「Busy 时禁用按钮」绑定 IsEnabled 取反）</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : throw new ArgumentOutOfRangeException(nameof(value), "值必须为 bool");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

- [ ] **Step 4: 运行通过**
- [ ] **Step 5: Commit（`feat: 反布尔转换器（Busy 禁用支撑）`）**

---

### Task 6: ViewModel 展示属性（PreviewRow.Status、ActivateResultIsError）

**Files:**
- Modify: `src/FileTidy.App/ViewModels/MainViewModel.cs`
- Test: `tests/FileTidy.Tests/MainViewModelTests.cs`（追加用例）

**Interfaces:**
- Consumes: `LicenseCodec.CreateKeyPair()`、`LicenseService(pub, licenseFile, trialFile)`、`PreviewStatus`（均有既有用法）
- Produces:
  - `PreviewRow` 新增 `public required PreviewStatus Status { get; init; }`
  - `MainViewModel.ActivateResultIsError: bool`（public get / private set，绑定色）
  - `ActivateResult`（既有）配合错误标志：成功→false、失败→true

- [ ] **Step 1: 追加测试（MainViewModelTests 尾部）**

```csharp
[Fact]
public void Activate_BadCode_SetsErrorFlag()
{
    var dir = Directory.CreateTempSubdirectory("vmAct").FullName;
    try
    {
        var (_, pub) = LicenseCodec.CreateKeyPair();
        var vm = new MainViewModel(new SettingsService(Path.Combine(dir, "config.json")),
            license: new LicenseService(pub, Path.Combine(dir, "license.json"), Path.Combine(dir, "trial.json")));
        vm.ActivationCode = "FTID-INVALID";
        vm.ActivateCommand.Execute(null);
        Assert.True(vm.ActivateResultIsError);
        Assert.Contains("无效", vm.ActivateResult ?? "");
    }
    finally { Directory.Delete(dir, true); }
}

[Fact]
public void Activate_ValidCode_ClearsErrorIsError()
{
    var dir = Directory.CreateTempSubdirectory("vmAct2").FullName;
    try
    {
        var (priv, pub) = LicenseCodec.CreateKeyPair();
        var vm = new MainViewModel(new SettingsService(Path.Combine(dir, "config.json")),
            license: new LicenseService(pub, Path.Combine(dir, "license.json"), Path.Combine(dir, "trial.json")));
        using var rsa = System.Security.Cryptography.RSA.Create();
        rsa.ImportFromPem(priv);
        vm.ActivationCode = LicenseCodec.Sign(LicenseCodec.GeneratePayload(), rsa);
        vm.ActivateCommand.Execute(null);
        Assert.False(vm.ActivateResultIsError);
        Assert.Contains("成功", vm.ActivateResult ?? "");
    }
    finally { Directory.Delete(dir, true); }
}

[Fact]
public async Task Preview_PopulatesStatusEnum()
{
    var dir = Directory.CreateTempSubdirectory("vmPrev").FullName;
    try
    {
        var src = Path.Combine(dir, "src"); var target = Path.Combine(dir, "target");
        Directory.CreateDirectory(src); Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(src, "a.jpg"), "x");

        var vm = NewVm(Path.Combine(dir, "ops"));
        vm.AddRule();
        vm.SelectedEditor!.Name = "图片"; vm.SelectedEditor.SourcePath = src; vm.SelectedEditor.TargetPath = target;
        vm.SelectedEditor.AddExtension("jpg");

        await vm.PreviewCommand.ExecuteAsync();

        Assert.Single(vm.PreviewRows);
        Assert.Equal(PreviewStatus.Moved, vm.PreviewRows[0].Status);
        Assert.False(vm.PreviewRows[0].Warned);
    }
    finally { Directory.Delete(dir, true); }
}
```

- [ ] **Step 2: 运行失败**（`Status` / `ActivateResultIsError` 未定义 → 编译错误）

- [ ] **Step 3: 修改 `MainViewModel.cs`**

a) `PreviewRow` 类追加属性（第 9-15 行之间结尾）：
```csharp
public required PreviewStatus Status { get; init; }
```

b) `ActivateCommand` 重写（原 79-85 行）：
```csharp
ActivateCommand = new RelayCommand(() =>
{
    var (ok, message) = _license.Activate(ActivationCode);
    ActivateResult = message;
    ActivateResultIsError = !ok;
    RefreshLicenseState();
    return Task.CompletedTask;
});
```

c) 新增属性（放在 `ActivateResult` 属性定义之后，约第 208 行）：
```csharp
/// <summary>激活结果是否为失败（供显示层着色：成功绿 / 失败红）</summary>
public bool ActivateResultIsError { get => _actErr; private set => SetProperty(ref _actErr, value); }
private bool _actErr;
```

d) `RenderPreviews`（第 288-304 行）`PreviewRow` 初始化追加 `Status`：
```csharp
PreviewRows.Add(new PreviewRow
{
    Source = p.File.FullPath,
    Dest = p.DestPath,
    StatusText = p.Status == PreviewStatus.Moved ? "将移动"
               : p.Status == PreviewStatus.Conflict ? "冲突"
               : p.Status == PreviewStatus.TemplateError ? "模板错误"
               : p.Status == PreviewStatus.NeedsPro ? "需解锁 Pro"
               : "未命中",
    Warned = p.Status != PreviewStatus.Moved,
    Status = p.Status
});
```

- [ ] **Step 4: `dotnet test tests/FileTidy.Tests --filter "FullyQualifiedName~MainViewModel"` 全部通过（含既有用例）**
- [ ] **Step 5: Commit**

```bash
git add src/FileTidy.App/ViewModels/MainViewModel.cs tests/FileTidy.Tests/MainViewModelTests.cs
git commit -m "feat: 预览状态枚举与激活失败标志（界面状态色支撑）"
```

---

### Task 7: MainWindow 页头 + 错误横幅 + 左栏规则列表

> 本任务起改 `MainWindow.xaml`。无自动化单元断言——验证 = `dotnet build`（编译拦 XAML 错）+ Task 1 smoke 测试（资源加载）。**不改变任何既有绑定**。

**Files:**
- Modify: `src/FileTidy.App/MainWindow.xaml`
- Modify: `src/FileTidy.App/App.xaml`（注册 `InverseBoolConverter` 资源）

**Interfaces:**
- Consumes: `BrushAccent`、`BatteryStyles`（`AccentButton`/`SecondaryButton`/`IconButton`/`CardBorder`/`PageTitleText`/`PageSubtitleText`）、`SwitchStyle`、`InverseBool`
- Produces: 页头卡片容器、错误横幅（琥珀）、左栏规则列表卡片

- [ ] **Step 1: `App.xaml` 注册转换器**

在 `<Application.Resources>` 的 `ResourceDictionary` 内、`MergedDictionaries` 之后追加：
```xml
<ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Themes/Colors.xaml"/>
        <ResourceDictionary Source="Themes/Controls.xaml"/>
        <ResourceDictionary Source="Themes/Switch.xaml"/>
    </ResourceDictionary.MergedDictionaries>
    <conv:InverseBoolConverter x:Key="InverseBool"/>
</ResourceDictionary>
```
`xmlns` 补在 `Application` 根：
```xml
xmlns:conv="clr-namespace:FileTidy.App.Converters"
```

- [ ] **Step 2: 重写 `MainWindow.xaml`（完整内容）**

```xml
<Window x:Class="FileTidy.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:conv="clr-namespace:FileTidy.App.Converters"
        Title="文件整理助手" Height="560" Width="900" MinWidth="720" MinHeight="420"
        Background="{StaticResource BrWindow}">
    <Window.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis" />
    </Window.Resources>
    <DockPanel>
        <!-- 底部状态栏 -->
        <StatusBar DockPanel.Dock="Bottom">
            <StatusBarItem>
                <TextBlock Text="{Binding StatusText}" />
            </StatusBarItem>
        </StatusBar>

        <!-- 错误/失败明细横幅（琥珀警示卡片，仅出错时显示） -->
        <Border DockPanel.Dock="Top" Margin="10,4,10,0" Padding="12,8"
                Background="{StaticResource BrBannerBg}" BorderBrush="{StaticResource BrBannerBorder}" BorderThickness="1" CornerRadius="8"
                Visibility="{Binding HasErrorDetails, Converter={StaticResource BoolToVis}}">
            <TextBlock Text="{Binding ErrorDetails}" Foreground="{StaticResource BrBannerText}" TextWrapping="Wrap" MaxHeight="90"/>
        </Border>

        <!-- 页头卡片 -->
        <Border DockPanel.Dock="Top" Margin="10,8,10,0" Style="{StaticResource CardBorder}">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column="0">
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="&#xE945;" FontFamily="Segoe MDL2 Assets" FontSize="20"
                                   Foreground="{StaticResource BrushAccent}" VerticalAlignment="Center" Margin="0,0,8,0"/>
                        <TextBlock Text="文件整理助手" Style="{StaticResource PageTitleText}"/>
                    </StackPanel>
                    <TextBlock Style="{StaticResource PageSubtitleText}" Margin="0,4,0,0">
                        <Run Text="{Binding LicenseStateText}"/><Run Text=" · "/><Run Text="{Binding EditorVms.Count}"/><Run Text=" 条规则"/>
                    </TextBlock>
                </StackPanel>
                <StackPanel Grid.Column="1" VerticalAlignment="Center" Orientation="Horizontal">
                    <Button Content="预览结果" Command="{Binding PreviewCommand}"
                            Style="{StaticResource SecondaryButton}" Margin="0,0,8,0"
                            IsEnabled="{Binding Busy, Converter={StaticResource InverseBool}}"/>
                    <Button Content="立即整理" Command="{Binding TidyCommand}"
                            Style="{StaticResource AccentButton}" Margin="0,0,8,0"
                            IsEnabled="{Binding Busy, Converter={StaticResource InverseBool}}"/>
                    <Button Content="撤销上次" Command="{Binding UndoCommand}"
                            Style="{StaticResource SecondaryButton}" Margin="0,0,8,0"
                            IsEnabled="{Binding Busy, Converter={StaticResource InverseBool}}"/>
                    <ToggleButton Content="自动整理" Style="{StaticResource SwitchStyle}"
                                  IsChecked="{Binding AutoTidy}"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- 主区 -->
        <Grid Margin="10,8">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="240"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- 左栏：规则列表卡片 -->
            <Border Grid.Column="0" Style="{StaticResource CardBorder}">
                <DockPanel>
                    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,0,0,8">
                        <TextBlock Text="规则" FontWeight="SemiBold" Foreground="{StaticResource BrText}"
                                   VerticalAlignment="Center" Margin="0,0,8,0"/>
                        <Button Command="{Binding AddRuleCommand}" Style="{StaticResource IconButton}"
                                ToolTip="新建规则" Content="&#xE710;" Margin="0,0,4,0"/>
                        <Button Command="{Binding MoveRuleUpCommand}" Style="{StaticResource IconButton}"
                                ToolTip="上移" Content="&#xE70E;" Margin="0,0,4,0"/>
                        <Button Command="{Binding MoveRuleDownCommand}" Style="{StaticResource IconButton}"
                                ToolTip="下移" Content="&#xE70D;" Margin="0,0,4,0"/>
                        <Button Command="{Binding DeleteRuleCommand}" Style="{StaticResource IconButton}"
                                ToolTip="删除规则" Content="&#xE74D;"/>
                    </StackPanel>
                    <ListBox ItemsSource="{Binding EditorVms}" DisplayMemberPath="DisplayName"
                             SelectedItem="{Binding SelectedEditor}"
                             PreviewMouseLeftButtonDown="RuleList_PreviewMouseLeftButtonDown"
                             MouseMove="RuleList_MouseMove" Drop="RuleList_Drop" AllowDrop="True"/>
                </DockPanel>
            </Border>

            <!-- 右栏 -->
            <Grid Grid.Column="1" Margin="10,0,0,0">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>
                <StackPanel Grid.Row="0">
                    <TextBlock Text="{Binding SelectedEditor.Name, FallbackValue=未选择规则}" FontWeight="SemiBold"
                               Foreground="{StaticResource BrText}"/>
                    <TextBlock Text="{Binding SelectedEditor.ErrorSummary, FallbackValue=请在左侧选择或新建规则}"
                               Foreground="{StaticResource BrError}" TextWrapping="Wrap" Margin="0,2,0,0"/>
                </StackPanel>
                <!-- 预览表：Task 9 补状态色 RowStyle -->
                <DataGrid Grid.Row="1" MaxHeight="220" Margin="0,8,0,8" AutoGenerateColumns="False"
                          ItemsSource="{Binding PreviewRows}" IsReadOnly="True">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="源文件" Binding="{Binding Source}" Width="*"/>
                        <DataGridTextColumn Header="目标路径" Binding="{Binding Dest}" Width="*"/>
                        <DataGridTextColumn Header="状态" Binding="{Binding StatusText}" Width="80"/>
                    </DataGrid.Columns>
                </DataGrid>
                <TabControl Grid.Row="2">
                    <!-- 规则编辑 / 设置内容在 Task 8、9 分别替换，此处保留既有 Tab 定义 -->
                </TabControl>
            </Grid>
        </Grid>
    </DockPanel>
</Window>
```

> 注意点：
> - 页头标题用 `<Run Text="{Binding ...}">` 拼接（`TextBlock` 可绑定多个 `Run.Text`）；`EditorVms.Count` 对 `ObservableCollection` 的 Count 在集合变化时会自动刷新（WPF 订阅 `INotifyCollectionChanged`，Count 属性一起刷新）。
> - `&#xE945;`（闪电）/`E710`（加号）/`E70E`/`E70D`（上下箭头）/`E74D`（垃圾桶）为 Segoe MDL2 知名码点。
> - 篇幅原因：右栏 Tab 内容（原 74-122 行）暂不删，任务先替换页头 + 左侧（以上完整窗口含 TabControl 占位壳，Tab 内容保留原样即可，后续 Task 覆盖）。

- [ ] **Step 3: build + smoke**

Run: `dotnet build`; `dotnet test tests/FileTidy.Tests --filter "FullyQualifiedName~AppResourcesLoadTests"`
Expected: build 成功、smoke 通过。

- [ ] **Step 4: Commit**

```bash
git add src/FileTidy.App/App.xaml src/FileTidy.App/MainWindow.xaml
git commit -m "feat: 页头卡片化、Busy 禁用态与左栏规则列表卡片"
```

---

### Task 8: 规则编辑器分组卡片（文件条件 / 处理动作 / 选项）

**Files:**
- Modify: `src/FileTidy.App/MainWindow.xaml`（TabItem「规则」内 StackPanel 区）

**Interfaces:**
- Consumes: `CardBorder`/`CardTitleText`/`FieldLabel`/`FormTextBox`/`FormComboBox`/`FormCheckBox`/`ProBadge`/`ProBadgeText`
- Produces: 编辑器分组卡片

- [ ] **Step 1: 替换 `<TabItem Header="规则">` 内部完整内容**

```xml
<TabItem Header="规则">
    <ScrollViewer VerticalScrollBarVisibility="Auto"
                  Background="{StaticResource BrWindow}">
        <StackPanel Margin="4">
            <!-- 卡 1：文件条件 -->
            <Border Style="{StaticResource CardBorder}" Margin="0,0,0,10">
                <StackPanel>
                    <TextBlock Text="文件条件" Style="{StaticResource CardTitleText}"/>
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>
                        <Grid.Resources>
                            <Style TargetType="TextBlock" BasedOn="{StaticResource FieldLabel}">
                                <Setter Property="Margin" Value="0,0,10,6"/>
                            </Style>
                            <Style TargetType="TextBox" BasedOn="{StaticResource FormTextBox}">
                                <Setter Property="Margin" Value="0,0,0,6"/>
                            </Style>
                        </Grid.Resources>

                        <TextBlock Grid.Row="0" Text="名称"/>
                        <TextBox Grid.Column="1" Grid.Row="0"
                                 Text="{Binding SelectedEditor.Name, UpdateSourceTrigger=PropertyChanged}"/>

                        <TextBlock Grid.Row="1" Text="源文件夹"/>
                        <TextBox Grid.Column="1" Grid.Row="1"
                                Text="{Binding SelectedEditor.SourcePath, UpdateSourceTrigger=PropertyChanged}"/>

                        <TextBlock Grid.Row="2" Text="目标文件夹"/>
                        <TextBox Grid.Column="1" Grid.Row="2"
                                Text="{Binding SelectedEditor.TargetPath, UpdateSourceTrigger=PropertyChanged}"/>

                        <TextBlock Grid.Row="3" Text="扩展名（逗号）"/>
                        <Grid Grid.Column="1" Grid.Row="3">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="12"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBox Grid.Column="0"
                                    Text="{Binding SelectedEditor.Extensions, UpdateSourceTrigger=PropertyChanged}"/>
                            <TextBox Grid.Column="2"
                                    Text="{Binding SelectedEditor.Keywords, UpdateSourceTrigger=PropertyChanged}"/>
                        </Grid>

                        <TextBlock Grid.Row="4" Text="N 天前（0 禁用）"/>
                        <Grid Grid.Column="1" Grid.Row="4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="Auto"/>
                                <ColumnDefinition Width="12"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBox Grid.Column="0" Width="72"
                                    Text="{Binding SelectedEditor.AgeDays, UpdateSourceTrigger=PropertyChanged}"/>
                            <TextBox Grid.Column="2"
                                    Text="{Binding SelectedEditor.RegexPattern, UpdateSourceTrigger=PropertyChanged}"/>
                        </Grid>
                    </Grid>
                    <StackPanel Orientation="Horizontal" Margin="0,2,0,0">
                        <Border Style="{StaticResource ProBadge}">
                            <TextBlock Style="{StaticResource ProBadgeText}"/>
                        </Border>
                        <TextBlock Text="正则表达式（匹配完整文件名）" Style="{StaticResource FieldLabel}" Margin="6,0,0,0"/>
                        <CheckBox Content="区分大小写" Style="{StaticResource FormCheckBox}" Margin="12,0,0,0"
                                  IsChecked="{Binding SelectedEditor.RegexCaseSensitive}"/>
                    </StackPanel>
                </StackPanel>
            </Border>

            <!-- 卡 2：处理动作 -->
            <Border Style="{StaticResource CardBorder}" Margin="0,0,0,10">
                <StackPanel>
                    <TextBlock Text="处理动作" Style="{StaticResource CardTitleText}"/>
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Text="动作" Style="{StaticResource FieldLabel}" Margin="0,0,10,6" VerticalAlignment="Center"/>
                        <StackPanel Grid.Column="1" Orientation="Horizontal">
                            <ComboBox x:Name="ActionCombo" Style="{StaticResource FormComboBox}" MinWidth="200"
                                      SelectedValuePath="Tag"
                                      SelectedValue="{Binding SelectedEditor.ActionType}">
                                <ComboBoxItem Content="仅移动" Tag="move"/>
                                <ComboBoxItem Content="移动并重命名" Tag="moveRename"/>
                            </ComboBox>
                            <Border Style="{StaticResource ProBadge}" Margin="8,0,0,0" VerticalAlignment="Center">
                                <TextBlock Style="{StaticResource ProBadgeText}"/>
                            </Border>
                        </StackPanel>
                    </Grid>
                    <!-- 重命名模板：仅「移动并重命名」显示 -->
                    <StackPanel x:Name="RenameBox" Margin="0,8,0,0">
                        <StackPanel.Style>
                            <Style TargetType="StackPanel">
                                <Setter Property="Visibility" Value="Collapsed"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding SelectedValue, ElementName=ActionCombo}" Value="moveRename">
                                        <Setter Property="Visibility" Value="Visible"/>
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </StackPanel.Style>
                        <TextBlock Text="重命名模板" Style="{StaticResource FieldLabel}" Margin="0,0,0,4"/>
                        <TextBox Style="{StaticResource FormTextBox}"
                                 Text="{Binding SelectedEditor.RenameTemplate, UpdateSourceTrigger=PropertyChanged}"/>
                    </StackPanel>
                </StackPanel>
            </Border>

            <!-- 卡 3：选项 -->
            <Border Style="{StaticResource CardBorder}">
                <StackPanel>
                    <TextBlock Text="选项" Style="{StaticResource CardTitleText}"/>
                    <CheckBox Content="包含子文件夹" Style="{StaticResource FormCheckBox}"
                              IsChecked="{Binding SelectedEditor.IncludeSubfolders}"/>
                    <CheckBox Content="排除目标文件夹树" Style="{StaticResource FormCheckBox}"
                              IsChecked="{Binding SelectedEditor.ExcludeTargetTree}"/>
                    <CheckBox Content="冲突时自动追加序号" Style="{StaticResource FormCheckBox}"
                              IsChecked="{Binding SelectedEditor.AutoRenameOnConflict}"/>
                </StackPanel>
            </Border>
        </StackPanel>
    </ScrollViewer>
</TabItem>
```

> 注意点：
> - 原编辑器 Grid 的 10 行字段全部保留（名称/源/目标/扩展名/关键词/N天/正则/大小写/动作/模板 + 复选框），绑定**一字不改**，仅换容器与样式。
> - 「扩展名+关键词」与「N天+正则」各占一行两列；正则标签放到输入框下方的 Pro 徽标行，属于布局微调不影响绑定。
> - `RenameBox` 的显隐靠 `ElementName=ActionCombo` 的 `SelectedValue`（Tag 字符串 `moveRename`）触发，不动 ViewModel。

- [ ] **Step 2: 确认卡片结构完整**（上方案例中 N 天/正则一行两列后即闭合 Grid，正则说明与区分大小写统一放在卡片底部的 Pro 徽标行——已在上方案例代码中）

- [ ] **Step 3: `dotnet build` + smoke → PASS**

Run: `dotnet build`; `dotnet test tests/FileTidy.Tests --filter "FullyQualifiedName~AppResourcesLoadTests"`

- [ ] **Step 4: 确认编辑器绑定路径未变**（`SelectedEditor.*` 与 Task 6 起的 `MainViewModelTests` 继续全绿）

Run: `dotnet test tests/FileTidy.Tests --filter "FullyQualifiedName~MainViewModel"`
Expected: PASS（含既有用例，证明 VM 行为未受 XAML 重构影响——XAML 不参与逻辑，此步 `MainViewModel` 用例本身不覆盖 XAML，但 `build` 验证 XAML 正确）。

- [ ] **Step 5: Commit**

```bash
git add src/FileTidy.App/MainWindow.xaml
git commit -m "feat: 规则编辑器分组卡片（文件条件/处理动作/选项）"
```

---

### Task 9: 预览表状态色 + 设置页分组卡片

**Files:**
- Modify: `src/FileTidy.App/MainWindow.xaml`（DataGrid 行样式 + TabItem「设置」）

**Interfaces:**
- Consumes: `BrRow*`、`PreviewStatus`（Task 6 的 `PreviewRow.Status`）、`CardBorder`、激活颜色逻辑（`ActivateResultIsError`）
- Produces: 行状态高亮、设置页分组卡片、激活成功/失败着色

- [ ] **Step 1: DataGrid 状态色 RowStyle**

替换 Task 7 中 `DataGrid` 块补上 `RowStyle`：

```xml
<DataGrid.RowStyle>
    <Style TargetType="DataGridRow">
        <Style.Triggers>
            <DataTrigger Binding="{Binding Status}" Value="Conflict">
                <Setter Property="Background" Value="{StaticResource BrRowConflict}"/>
            </DataTrigger>
            <DataTrigger Binding="{Binding Status}" Value="NeedsPro">
                <Setter Property="Background" Value="{StaticResource BrRowNeedsPro}"/>
            </DataTrigger>
            <DataTrigger Binding="{Binding Status}" Value="TemplateError">
                <Setter Property="Background" Value="{StaticResource BrRowTemplateError}"/>
            </DataTrigger>
            <DataTrigger Binding="{Binding Status}" Value="NoMatch">
                <Setter Property="Background" Value="{StaticResource BrRowNoMatch}"/>
            </DataTrigger>
        </Style.Triggers>
    </Style>
</DataGrid.RowStyle>
```

- [ ] **Step 2: 替换 `<TabItem Header="设置">` 内部内容**

```xml
<TabItem Header="设置">
    <ScrollViewer VerticalScrollBarVisibility="Auto" Background="{StaticResource BrWindow}">
        <StackPanel Margin="4">
            <!-- 账户 / 激活 -->
            <Border Style="{StaticResource CardBorder}" Margin="0,0,0,10">
                <StackPanel>
                    <TextBlock Text="账户 / 激活" Style="{StaticResource CardTitleText}"/>
                    <TextBlock Text="{Binding LicenseStateText}" FontWeight="SemiBold" Foreground="{StaticResource BrText}"
                               Margin="0,0,0,8"/>
                    <DockPanel>
                        <Button DockPanel.Dock="Right" Content="激活" Command="{Binding ActivateCommand}"
                                Style="{StaticResource AccentButton}" Margin="8,0,0,0" Padding="18,7"/>
                        <TextBox Style="{StaticResource FormTextBox}"
                                 Text="{Binding ActivationCode, UpdateSourceTrigger=PropertyChanged}"/>
                    </DockPanel>
                    <!-- 激活结果：成功绿 / 失败红 -->
                    <TextBlock Text="{Binding ActivateResult}" TextWrapping="Wrap" Margin="0,8,0,0">
                        <TextBlock.Style>
                            <Style TargetType="TextBlock">
                                <Setter Property="Foreground" Value="{StaticResource BrSuccess}"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding ActivateResultIsError}" Value="True">
                                        <Setter Property="Foreground" Value="{StaticResource BrError}"/>
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </TextBlock.Style>
                    </TextBlock>
                </StackPanel>
            </Border>

            <!-- 常规 -->
            <Border Style="{StaticResource CardBorder}">
                <StackPanel>
                    <TextBlock Text="常规" Style="{StaticResource CardTitleText}"/>
                    <ToggleButton Content="开机自启" Style="{StaticResource SwitchStyle}"
                                  IsChecked="{Binding StartWithWindows}" Margin="0,0,0,8"/>
                    <CheckBox Content="新规则默认冲突时自动追加序号" Style="{StaticResource FormCheckBox}"
                              IsChecked="{Binding AutoRenameOnConflict}"/>
                    <TextBlock Text="Pro 功能：正则条件、重命名模板、重复文件检测"
                               Foreground="{StaticResource BrTextSecondary}" FontSize="11" Margin="0,12,0,0"/>
                </StackPanel>
            </Border>
        </StackPanel>
    </ScrollViewer>
</TabItem>
```

> 注意点：
> - 「自动整理」开关只在页头（Task 7），设置页不重复——本任务去掉原设置 Tab 里的 `CheckBox`「自动整理」，避免双入口。
> - 激活结果 `TextBlock` 用 `DataTrigger` 绑定 `ActivateResultIsError` 着红/绿。
> - 移动端不需要的 `ProBadge`（激活卡）不误——Pro 状态已有文字展示，不必加徽标。

- [ ] **Step 3: `dotnet build` + smoke → PASS**
- [ ] **Step 4: Commit**

```bash
git add src/FileTidy.App/MainWindow.xaml
git commit -m "feat: 预览结果状态色与设置页分组卡片"
```

---

### Task 10: 全量验证与手动验收清单

**Files:** 无新增

- [ ] **Step 1: `dotnet build`** → 通过
- [ ] **Step 2: `dotnet test tests/FileTidy.Tests`** → 全绿
- [ ] **Step 3: 手动验收（对照规格 7.2）**
  1. 预览 / 立即整理 / 撤销 / 自动整理 / 规则增删移均可点且行为正确
  2. 规则拖拽排序与上移 / 下移正常
  3. 正则与模板的 Pro 徽标正确显示
  4. 激活流程走通（输入 → 激活），成功绿 / 失败红着色正确
  5. 预览状态色覆盖（将移动 / 冲突 / 需解锁 Pro 金 / 模板错误 / 未命中）
  6. 窗口缩放至 MinWidth 720 不破版
  7. 托盘（双击 / 立即整理 / 退出 / 完成通知）不受影响
  8. Busy（执行中）时页头操作按钮置灰不可点
  9. 设置页无自动整理开关（单一入口在页头）
- [ ] **Step 4: Commit**

```bash
git add -A  （若存在未提交改动）
git commit -m "chore: 界面重塑验证与收尾"
```

---

## 自检

### 设计规格 → 任务覆盖

| 设计规格章节 | 任务 |
|---|---|
| §2.1 色板（含渐变 / 语义色 / 预览行浅色） | Task 1 |
| §2.2 圆角 / 字体 / MDL2 图标 | Task 1（Brush）/ Task 2（样式）/ Task 7（页头图） |
| §2.3 各类控件皮肤 | Task 2（按钮/表单/卡片/徽章/状态栏）/ Task 3（Switch）/ Task 4（ListBox/DataGrid/Tab） |
| §3 页头卡片 + 副标题（授权+条数） | Task 7 |
| §3 Busy 禁用态 | Task 5（转换器）+ Task 7（绑定） |
| §3 错误横幅琥珀卡 | Task 7 |
| §3 规则列表卡片 | Task 7 |
| §4 编辑器分组卡片 / ElementName 触发 / Pro 徽标 | Task 8（+ Task 2 徽章） |
| §5 设置页分组 / 去重自动整理 / 激活着色 | Task 9 |
| §6 PreviewRow.Status / ActivateResultIsError / InverseBool | Task 5、Task 6 |
| §7 smoke 自动化 / 手动验收 | Task 1、Task 10 |

### 类型与命名一致性

- `BrushAccentHover` / `BrushAccentPressed`：Task 1 定义，Task 2 的 `AccentButton` 使用，全链路一致；
- `PreviewRow.Status: PreviewStatus`：Task 6 定义生产，Task 9 `DataTrigger Value="Conflict"/"NeedsPro"...` 使用（WPF 枚举字符串匹配）；
- `ActivateResultIsError`：Task 6 定义生产，Task 9 使用；
- `InverseBoolConverter` / `InverseBool`：Task 5 定义、Task 7 注册为 App 资源并绑定使用；
- 样式 key（`CardBorder`/`FormTextBox`/`ProBadge`/`SwitchStyle` 等）Task 2/7/8/9 统一引用。

### 占位符声明

计划里 Task 4 提到的 `BrAccentSoft`/`BrAccentDeep` 已在「下一处改 Colors.xaml」段落实为实际文件修改；Task 3 触发器命名已给出最终正确模板（`slide`）。无 TBD。