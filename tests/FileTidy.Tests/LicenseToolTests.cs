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
        // 生成器侧：私钥文件位于本机工具目录（不入库，缺失则跳过本用例）
        var keyPath = Path.Combine(FindRepoRoot(), "tools", "FileTidy.LicenseTool", "private_key.pem");
        if (!File.Exists(keyPath)) return; // 克隆仓库无私钥，属正常，跳过
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(keyPath));

        var code = LicenseCodec.Sign(LicenseCodec.GeneratePayload(), rsa);

        // 应用侧：内置公钥验证
        using var pub = RSA.Create();
        pub.ImportFromPem(LicenseKeys.AppPublicKeyPem);
        Assert.NotNull(LicenseCodec.Verify(code, pub));
    }

    [Fact]
    public void KeyPairRoundTrip_SignAndVerify()
    {
        // 自生成临时密钥对验证签核往返，不依赖仓库内密钥文件
        var (priv, pub) = LicenseCodec.CreateKeyPair();
        using var rsa = RSA.Create();
        rsa.ImportFromPem(priv);
        using var verify = RSA.Create();
        verify.ImportFromPem(pub);

        var code = LicenseCodec.Sign(LicenseCodec.GeneratePayload(), rsa);
        Assert.NotNull(LicenseCodec.Verify(code, verify));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FileTidy.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir.FullName;
    }
}