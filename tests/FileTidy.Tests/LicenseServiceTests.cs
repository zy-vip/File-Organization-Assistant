// tests/FileTidy.Tests/LicenseServiceTests.cs
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using FileTidy.Core;
using FileTidy.Core.Models;

namespace FileTidy.Tests;

public class LicenseServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("lic").FullName;
    private readonly (string PrivatePem, string PublicPem) _keys = LicenseCodec.CreateKeyPair();
    public void Dispose() => Directory.Delete(_dir, true);

    private LicenseService NewService(int? tidyLimit = null, Func<DateTime>? now = null)
        => new(_keys.PublicPem, Path.Combine(_dir, "license.json"), Path.Combine(_dir, "trial.json"),
               trialTidyLimit: tidyLimit ?? 20, now: now);

    [Fact]
    public void CreateKeyPair_ProducesValidPem()
    {
        Assert.Contains("PRIVATE KEY", _keys.PrivatePem);
        Assert.Contains("PUBLIC KEY", _keys.PublicPem);
    }

    [Fact]
    public void SignAndVerify_RoundTrip()
    {
        var payload = LicenseCodec.GeneratePayload();
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_keys.PrivatePem);
        var code = LicenseCodec.Sign(payload, rsa);
        Assert.StartsWith("FTID-", code);

        using var pub = RSA.Create();
        pub.ImportFromPem(_keys.PublicPem);
        Assert.Equal(payload, LicenseCodec.Verify(code, pub));
    }

    [Fact]
    public void Verify_TamperedCodeFails()
    {
        var payload = LicenseCodec.GeneratePayload();
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_keys.PrivatePem);
        var code = LicenseCodec.Sign(payload, rsa);
        var tampered = code[..^2] + (code[^1] == 'A' ? 'B' : 'A');
        using var pub = RSA.Create();
        pub.ImportFromPem(_keys.PublicPem);
        Assert.Null(LicenseCodec.Verify(tampered, pub));
    }

    [Fact]
    public void GetState_FreshIsTrial()
    {
        Assert.Equal(LicenseState.Trial, NewService().GetState());
    }

    [Fact]
    public void TrialInfo_CountsDownTidyAndDays()
    {
        var now = new DateTime(2026, 8, 6, 12, 0, 0);
        var svc = NewService(now: () => now);
        for (var i = 0; i < 3; i++) svc.RecordTidyUse();
        var info = svc.GetTrialInfo();
        Assert.Equal(14, info!.RemainingDays);
        Assert.Equal(17, info.RemainingTidy);

        var late = NewService(now: () => now.AddDays(14));
        Assert.Equal(0, late.GetTrialInfo()!.RemainingDays);
        Assert.Equal(LicenseState.Free, late.GetState());
    }

    [Fact]
    public void TidyLimitExhausted_LocksProFreeAllowed()
    {
        var svc = NewService(tidyLimit: 2);
        svc.RecordTidyUse(); svc.RecordTidyUse();
        Assert.Equal(LicenseState.Free, svc.GetState());
        Assert.False(svc.IsAllowed(ProFeature.RenameTemplate));
        Assert.False(svc.IsAllowed(ProFeature.RegularExpression));
    }

    [Fact]
    public void Activate_SetsProState()
    {
        var svc = NewService();
        var code = MakeCode();
        var (ok, _) = svc.Activate(code);
        Assert.True(ok);
        Assert.Equal(LicenseState.Pro, svc.GetState());
        Assert.True(svc.IsAllowed(ProFeature.RenameTemplate));
    }

    [Fact]
    public void Activate_LowercasePrefixAccepted()
    {
        // 手输小写前缀 ftid- 应被宽容接受（Base32 编码段本已不区分大小写）
        var svc = NewService();
        var (ok, _) = svc.Activate(MakeCode().ToLowerInvariant());
        Assert.True(ok);
        Assert.Equal(LicenseState.Pro, svc.GetState());
    }

    [Fact]
    public void Activate_InvalidCodeFails()
    {
        var svc = NewService();
        var (ok, message) = svc.Activate("FTID-AAAA-BBBB");
        Assert.False(ok);
        Assert.NotEmpty(message);
        Assert.Equal(LicenseState.Trial, svc.GetState());
    }

    [Fact]
    public void ActivateThenRecordTidyUse_DoesNotCountTrial()
    {
        var svc = NewService();
        var (ok, _) = svc.Activate(MakeCode());
        Assert.True(ok);
        svc.RecordTidyUse();
        Assert.Equal(LicenseState.Pro, svc.GetState());
        Assert.Equal(20, svc.GetTrialInfo()!.RemainingTidy);
    }

    [Fact]
    public void RequiredFeature_AggregatesRule()
    {
        var rule = new Rule
        {
            Conditions = { new RegexCondition { Pattern = "x" } },
            Actions = { new MoveAndRenameAction { Template = "{name}{ext}" } }
        };
        var features = NewService().RequiredFeature(rule);
        Assert.Contains(ProFeature.RegularExpression, features);
        Assert.Contains(ProFeature.RenameTemplate, features);
    }

    [Fact]
    public void TrialFileCorrupted_RestartsTrial()
    {
        File.WriteAllText(Path.Combine(_dir, "trial.json"), "{broken");
        var svc = NewService();
        Assert.Equal(LicenseState.Trial, svc.GetState());
        Assert.NotNull(svc.GetTrialInfo());
    }

    [Fact]
    public void LicenseFileWithBadCode_NotActivated()
    {
        // 手工伪造 license.json（未附有效签名）不得生效，仍按试用处理
        var dir2 = Directory.CreateTempSubdirectory("licbad").FullName;
        try
        {
            var licensePath = Path.Combine(dir2, "license.json");
            File.WriteAllText(licensePath, JsonSerializer.Serialize(new { Code = "FTID-AAAA-BBBB", ActivatedAt = DateTime.UtcNow }));
            var svc = new LicenseService(_keys.PublicPem, licensePath, Path.Combine(dir2, "trial.json"));
            Assert.Equal(LicenseState.Trial, svc.GetState());
        }
        finally { Directory.Delete(dir2, true); }
    }

    [Fact]
    public void TrialInfo_ClockRollbackFutureStart_ClampsDaysToLimit()
    {
        // 时钟回拨导致试用开始时间在未来时，剩余天数不得超过试用上限
        var dir2 = Directory.CreateTempSubdirectory("licdays").FullName;
        try
        {
            var now = new DateTime(2026, 8, 6);
            var licensePath = Path.Combine(dir2, "license.json");
            var trialPath = Path.Combine(dir2, "trial.json");
            var seed = new LicenseService(_keys.PublicPem, licensePath, trialPath, now: () => now);
            seed.RecordTidyUse();
            var svc = new LicenseService(_keys.PublicPem, licensePath, trialPath, now: () => now.AddDays(-30));
            Assert.Equal(14, svc.GetTrialInfo()!.RemainingDays);
        }
        finally { Directory.Delete(dir2, true); }
    }

    [Fact]
    public void GetState_InvalidatesCacheAfterRecordTidyUse()
    {
        // 状态缓存必须在试用计数耗尽后失效，反映为 Free
        var dir2 = Directory.CreateTempSubdirectory("licstate").FullName;
        try
        {
            var svc = new LicenseService(_keys.PublicPem, Path.Combine(dir2, "license.json"), Path.Combine(dir2, "trial.json"), trialTidyLimit: 2);
            Assert.Equal(LicenseState.Trial, svc.GetState());
            svc.RecordTidyUse(); svc.RecordTidyUse();
            Assert.Equal(LicenseState.Free, svc.GetState());
        }
        finally { Directory.Delete(dir2, true); }
    }

    [Fact]
    public void Activate_CreatesMissingDirectory()
    {
        var dir2 = Directory.CreateTempSubdirectory("licdir").FullName;
        try
        {
            var nested = Path.Combine(dir2, "sub", "nested", "license.json");
            var svc = new LicenseService(_keys.PublicPem, nested, Path.Combine(dir2, "sub", "trial.json"));
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_keys.PrivatePem);
            var code = LicenseCodec.Sign(LicenseCodec.GeneratePayload(), rsa);
            var (ok, _) = svc.Activate(code);
            Assert.True(ok);
            Assert.True(File.Exists(nested), "Activate 应创建缺失目录");
            Assert.Equal(LicenseState.Pro, svc.GetState());
        }
        finally { Directory.Delete(dir2, true); }
    }

    private string MakeCode()
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_keys.PrivatePem);
        return LicenseCodec.Sign(LicenseCodec.GeneratePayload(), rsa);
    }
}