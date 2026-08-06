using System.Security.Cryptography;
using FileTidy.Core;

namespace FileTidy.LicenseTool;

/// <summary>激活码生成器（开发者专用，不随商品发布）。用法：
/// keygen                     —— 生成新密钥对：写 private_key.pem，打印公钥 PEM
/// generate                   —— 从 private_key.pem 生成一个激活码
/// validate &lt;code&gt;       —— 用内置公钥验证激活码</summary>
public static class Program
{
    private const string KeyPath = "private_key.pem";

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "keygen")
            {
                var (priv, pub) = LicenseCodec.CreateKeyPair();
                File.WriteAllText(KeyPath, priv);
                Console.WriteLine($"私钥已写入 {Path.GetFullPath(KeyPath)}");
                Console.WriteLine("公钥（填入 src/FileTidy.Core/LicenseKeys.cs）：");
                Console.WriteLine(pub);
                return 0;
            }
            if (args.Length == 0 || args[0] == "generate")
            {
                if (!File.Exists(KeyPath)) { Console.Error.WriteLine($"缺少私钥文件 {KeyPath}"); return 1; }
                using var rsa = RSA.Create();
                rsa.ImportFromPem(File.ReadAllText(KeyPath));
                Console.WriteLine(LicenseCodec.Sign(LicenseCodec.GeneratePayload(), rsa));
                return 0;
            }
            if (args[0] == "validate")
            {
                if (args.Length < 2) { Console.Error.WriteLine("用法：validate <激活码>"); return 1; }
                if (string.IsNullOrEmpty(LicenseKeys.AppPublicKeyPem)) { Console.Error.WriteLine("LicenseKeys.AppPublicKeyPem 为空，请先执行 keygen 并填入公钥"); return 1; }
                using var rsa = RSA.Create();
                rsa.ImportFromPem(LicenseKeys.AppPublicKeyPem);
                var payload = LicenseCodec.Verify(args[1], rsa);
                Console.WriteLine(payload is null ? "无效" : "有效：" + payload);
                return payload is null ? 1 : 0;
            }
            Console.Error.WriteLine("未知命令：" + args[0]); return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("错误：" + ex.Message); return 1;
        }
    }
}