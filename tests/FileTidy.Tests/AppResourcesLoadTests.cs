// tests/FileTidy.Tests/AppResourcesLoadTests.cs
using System.Threading;
using System.Windows.Threading;
using FileTidy.App;
using AppType = FileTidy.App.App;

namespace FileTidy.Tests;

/// <summary>资源加载回归防线：App.xaml 合并字典加载不抛异常（防样式笔误导致启动白屏）。
/// WPF 同一 AppDomain 只允许一个 Application 实例，故所有用例共享一个 App：
/// 首个用例在专用 STA 线程创建（仅执行 InitializeComponent，不触发 OnStartup/Run）并开启消息泵；
/// 后续断言通过 Dispatcher 切回该线程执行（WPF 对象有线程亲和性）。</summary>
public class AppResourcesLoadTests
{
    private static readonly object Sync = new();
    private static AppType? _app;
    private static Exception? _initError;

    /// <summary>在 STA 线程创建共享 App 实例并运行消息泵（幂等，只执行一次）</summary>
    private static void EnsureApp()
    {
        if (_app is not null || _initError is not null) return;
        lock (Sync)
        {
            if (_app is not null || _initError is not null) return;
            Exception? error = null;
            var ready = new ManualResetEventSlim();
            var t = new Thread(() =>
            {
                try
                {
                    _app = new AppType();
                    _app.InitializeComponent(); // Main() 里才调用；测试手动执行以加载 App.xaml 资源
                }
                catch (Exception ex) { error = ex; }
                finally
                {
                    ready.Set();
                    if (error is null) Dispatcher.Run(); // 保持消息泵；后台线程随进程结束退出
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            Assert.True(ready.Wait(TimeSpan.FromSeconds(15)), "App 创建超时");
            _initError = error;
        }
    }

    /// <summary>把断言调度到 App 所在 STA 线程执行，并断言未抛异常</summary>
    private static void RunSta(Action<AppType> action)
    {
        EnsureApp();
        Assert.Null(_initError);
        Exception? thrown = null;
        try
        {
            _app!.Dispatcher.Invoke(() =>
            {
                try { action(_app!); }
                catch (Exception ex) { thrown = ex; }
            });
        }
        catch (Exception ex) { thrown = ex; }
        Assert.Null(thrown);
    }

    [Fact]
    public void Colors_AllKeys_Exist()
        => RunSta(app =>
        {
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
        => RunSta(app => Assert.Equal(3, app.Resources.MergedDictionaries.Count));

    [Fact]
    public void Controls_AllCoreKeys_Exist()
        => RunSta(app =>
        {
            foreach (var key in new[]
            {
                "BaseButton", "AccentButton", "SecondaryButton", "IconButton",
                "FormTextBox", "FormComboBox", "FormCheckBox",
                "CardBorder", "ProBadge", "CardTitleText", "FieldLabel",
                "PageTitleText", "PageSubtitleText"
            })
                Assert.NotNull(app.Resources[key]);
        });
}