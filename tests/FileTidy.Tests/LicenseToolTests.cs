// tests/FileTidy.Tests/LicenseToolTests.cs
using System.IO;
using System.Security.Cryptography;
using FileTidy.Core;

namespace FileTidy.Tests;

public class LicenseToolTests
{
    [Fact]
    public void BuiltInPublicKey_VerifiesToolGeneratedCode()
    {
        // 生成器侧：私钥文件真实存在（仓库内）
        var keyPath = Path.Combine(FindRepoRoot(), "tools", "FileTidy.LicenseTool", "private_key.pem");
        Assert.True(File.Exists(keyPath), "缺少私钥文件，请先执行 Task 6 Step 1");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(keyPath));

        var code = LicenseCodec.Sign(LicenseCodec.GeneratePayload(), rsa);

        // 应用侧：内置公钥验证
        using var pub = RSA.Create();
        pub.ImportFromPem(LicenseKeys.AppPublicKeyPem);
        Assert.NotNull(LicenseCodec.Verify(code, pub));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FileTidy.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir.FullName;
    }
}