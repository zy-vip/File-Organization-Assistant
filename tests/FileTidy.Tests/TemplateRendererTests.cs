// tests/FileTidy.Tests/TemplateRendererTests.cs
using System.IO;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class TemplateRendererTests
{
    private static FileEntry File(string name) => new()
    {
        FullPath = Path.Combine(@"C:\tmp", name),
        FileName = name,
        Extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant(),
        LastWriteTime = new DateTime(2026, 8, 1)
    };

    private static readonly DateTime Now = new(2026, 8, 6, 12, 0, 0);

    [Fact]
    public void Render_BuiltInVariables()
    {
        var r = TemplateRenderer.Render("{name}_{date:yyyyMMdd}{ext}", File("report.pdf"), null, 1, Now);
        Assert.True(r.Success);
        Assert.Equal("report_20260806.pdf", r.FileName);
        r = TemplateRenderer.Render("{original}_new", File("report.pdf"), null, 1, Now);
        Assert.Equal("report.pdf_new", r.FileName);
    }

    [Fact]
    public void Render_CaptureGroups()
    {
        var match = new RegexMatchResult { Groups = new[] { "2026-08", "2026", "08" } };
        var r = TemplateRenderer.Render("{1}-{2}{ext}", File("x.pdf"), match, 1, Now);
        Assert.True(r.Success);
        Assert.Equal("2026-08.pdf", r.FileName);
    }

    [Fact]
    public void Render_SequenceIncrementsPerRule()
    {
        var r1 = TemplateRenderer.Render("订单{n}{ext}", File("a.pdf"), null, 1, Now);
        var r2 = TemplateRenderer.Render("订单{n}{ext}", File("b.pdf"), null, 2, Now);
        Assert.Equal("订单1.pdf", r1.FileName);
        Assert.Equal("订单2.pdf", r2.FileName);
    }

    [Fact]
    public void Render_MissingCaptureGroupFails()
    {
        var r = TemplateRenderer.Render("{1}{ext}", File("a.pdf"), null, 1, Now);
        Assert.False(r.Success);
        Assert.Contains("捕获组", r.Error);
    }

    [Fact]
    public void Render_UnclosedBraceFails()
    {
        var r = TemplateRenderer.Render("{name{ext}", File("a.pdf"), null, 1, Now);
        Assert.False(r.Success);
    }

    [Fact]
    public void Render_IllegalCharsFail()
    {
        var r = TemplateRenderer.Render("a<b{ext}", File("a.pdf"), null, 1, Now);
        Assert.False(r.Success);
        Assert.Contains("非法字符", r.Error);
    }

    [Fact]
    public void Render_EmptyResultFails()
    {
        // 模板只含 {ext} 且文件无扩展名 → 渲染结果为空串 → 失败
        var r = TemplateRenderer.Render("{ext}", File("noext"), null, 1, Now);
        Assert.False(r.Success);
        Assert.Contains("为空", r.Error);
    }

    [Fact]
    public void Validate_RejectsUnknownVariable()
    {
        var errors = TemplateRenderer.Validate("x{unknown}y");
        Assert.Contains(errors, e => e.Contains("未知变量"));
    }

    [Fact]
    public void Validate_RejectsBadDateFormat()
    {
        var errors = TemplateRenderer.Validate("{date:yyyy}");
        Assert.Empty(errors);
        errors = TemplateRenderer.Validate("{date:Q}");
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_AcceptsPlainText()
    {
        Assert.Empty(TemplateRenderer.Validate("报告"));
    }
}