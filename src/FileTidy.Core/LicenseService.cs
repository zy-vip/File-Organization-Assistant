using System.Security.Cryptography;
using System.Text.Json;
using FileTidy.Core.Models;

namespace FileTidy.Core;

/// <summary>许可证状态</summary>
public enum LicenseState { Free, Trial, Pro }

/// <summary>试用剩余信息</summary>
public class TrialInfo
{
    public required int RemainingDays { get; init; }
    public required int RemainingTidy { get; init; }
}

/// <summary>许可证服务：激活码验证、试用计数、Pro 功能门控（构造注入以便测试）</summary>
public class LicenseService
{
    private readonly RSA _publicKey;
    private readonly string _licenseFile;
    private readonly string _trialFile;
    private readonly int _trialDays;
    private readonly int _trialTidyLimit;
    private readonly Func<DateTime> _now;

    // 缓存：激活状态只在 Activate 后变化、试用文件只在 RecordTidyUse 后变化，
    // 避免 IsAllowed 对每个文件重复读盘（再校验失败会丢签名，丢失缓存命中）。
    private bool? _activated;
    private TrialFile? _cachedTrial;
    private bool _trialLoaded;

    public LicenseService(string publicKeyPem, string licenseFile, string trialFile,
                          int trialDays = 14, int trialTidyLimit = 20, Func<DateTime>? now = null)
    {
        _publicKey = RSA.Create();
        _publicKey.ImportFromPem(publicKeyPem);
        _licenseFile = licenseFile;
        _trialFile = trialFile;
        _trialDays = trialDays;
        _trialTidyLimit = trialTidyLimit;
        _now = now ?? (() => DateTime.Now);
    }

    private class LicenseFile
    {
        public string Code { get; set; } = "";
        public DateTime ActivatedAt { get; set; }
    }

    private class TrialFile
    {
        public DateTime StartedAt { get; set; }
        public int TidyCount { get; set; }
    }

    /// <summary>当前状态：已激活 Pro；未激活且试用未耗尽 Trial；否则 Free</summary>
    public LicenseState GetState()
    {
        if (IsActivated()) return LicenseState.Pro;
        return GetTrialInfo() is { } t && t.RemainingDays > 0 && t.RemainingTidy > 0
            ? LicenseState.Trial : LicenseState.Free;
    }

    /// <summary>试用剩余信息；未激活时为 null 前保证有默认试用</summary>
    public TrialInfo? GetTrialInfo()
    {
        var trial = LoadTrial();
        if (trial is null) return null;
        var elapsedDays = (int)(_now().Date - trial.StartedAt.Date).TotalDays;
        // 剩余天数 clamp 到 [0, 试用上限]：时钟回拨/手改导致开始时间在未来时不得延展试用
        var remainingDays = Math.Clamp(_trialDays - elapsedDays, 0, _trialDays);
        return new TrialInfo { RemainingDays = remainingDays, RemainingTidy = Math.Max(0, _trialTidyLimit - trial.TidyCount) };
    }

    /// <summary>激活：验证签名成功则写盘；返回 (是否成功, 提示文案)</summary>
    public (bool Ok, string Message) Activate(string code)
    {
        if (LicenseCodec.Verify(code.Trim(), _publicKey) is null)
            return (false, "激活码无效，请检查后重试");
        var dir = Path.GetDirectoryName(_licenseFile);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_licenseFile, JsonSerializer.Serialize(new LicenseFile
        {
            Code = code.Trim(),
            ActivatedAt = _now()
        }));
        _activated = true;
        return (true, "激活成功，已解锁全部 Pro 功能");
    }

    /// <summary>整理触发时调用：试用次数 +1（激活后不再计试用）</summary>
    public void RecordTidyUse()
    {
        if (IsActivated()) return;
        var trial = LoadTrial();
        if (trial is null) return;
        trial.TidyCount++;
        SaveTrial(trial);
    }

    /// <summary>功能是否放行：激活放行；试用未耗尽放行；否则拒绝</summary>
    public bool IsAllowed(ProFeature feature) => GetState() != LicenseState.Free;

    /// <summary>聚合规则所需的全部 Pro 功能（条件 + 动作）</summary>
    public IReadOnlyList<ProFeature> RequiredFeature(Rule rule)
        => rule.Conditions.Select(c => c.RequiredFeature)
               .Concat(rule.Actions.Select(a => a.RequiredFeature))
               .Where(f => f is not null)
               .Select(f => f!.Value)
               .Distinct()
               .ToList();

    private bool IsActivated()
    {
        if (_activated is not null) return _activated.Value;
        _activated = File.Exists(_licenseFile) && LoadLicense() is not null;
        return _activated.Value;
    }

    private LicenseFile? LoadLicense()
    {
        try
        {
            var file = JsonSerializer.Deserialize<LicenseFile>(File.ReadAllText(_licenseFile));
            // 复验存储的激活码签名：手工伪造 license.json 不得生效
            if (file is not null && LicenseCodec.Verify(file.Code, _publicKey) is null) return null;
            return file;
        }
        catch (Exception) { return null; }
    }

    private TrialFile? LoadTrial()
    {
        if (_trialLoaded) return _cachedTrial;
        if (File.Exists(_trialFile))
        {
            try
            {
                _cachedTrial = JsonSerializer.Deserialize<TrialFile>(File.ReadAllText(_trialFile));
                _trialLoaded = true;
                return _cachedTrial;
            }
            catch (Exception) { /* 损坏按新试用重算 */ }
        }
        _cachedTrial = new TrialFile { StartedAt = _now(), TidyCount = 0 };
        SaveTrial(_cachedTrial);
        _trialLoaded = true;
        return _cachedTrial;
    }

    private void SaveTrial(TrialFile trial)
    {
        var dir = Path.GetDirectoryName(_trialFile);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_trialFile, JsonSerializer.Serialize(trial));
    }
}