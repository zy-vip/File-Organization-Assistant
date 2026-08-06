using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FileTidy.Core;

/// <summary>激活码编解码：FTID- + Base32(payload) + - + Base32(RSA 签名)</summary>
public static class LicenseCodec
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>生成随机载荷（版本+随机串）</summary>
    public static string GeneratePayload()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return JsonSerializer.Serialize(new { v = 1, s = Convert.ToHexString(bytes).ToLowerInvariant() });
    }

    /// <summary>用私钥签名载荷，返回完整激活码</summary>
    public static string Sign(string payload, RSA rsa)
    {
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"FTID-{Base32Encode(Encoding.UTF8.GetBytes(payload))}-{Base32Encode(signature)}";
    }

    /// <summary>验证激活码，返回载荷；无效返回 null</summary>
    public static string? Verify(string code, RSA rsa)
    {
        if (string.IsNullOrEmpty(code)) return null;
        code = code.Trim();
        // 前缀忽略大小写：Base32 编码段本就大小写宽容，手输小写 ftid- 不应被拒
        if (code.Length <= 5 || !code[..5].Equals("FTID-", StringComparison.OrdinalIgnoreCase)) return null;
        var parts = code.Split('-');
        if (parts.Length != 3) return null;
        try
        {
            var payloadBytes = Base32Decode(parts[1]);
            var signature = Base32Decode(parts[2]);
            var payload = Encoding.UTF8.GetString(payloadBytes);
            if (!rsa.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) return null;
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.GetProperty("v").GetInt32() == 1 ? payload : null;
        }
        catch (Exception) { return null; }
    }

    /// <summary>生成密钥对（PKCS#8 私钥 / SPKI 公钥 PEM）</summary>
    public static (string PrivatePem, string PublicPem) CreateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportSubjectPublicKeyInfoPem());
    }

    internal static string Base32Encode(byte[] bytes)
    {
        var sb = new StringBuilder();
        int bits = 0, value = 0;
        foreach (var b in bytes)
        {
            value = (value << 8) | b; bits += 8;
            while (bits >= 5) { sb.Append(Alphabet[(value >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(Alphabet[(value << (5 - bits)) & 31]);
        return sb.ToString();
    }

    internal static byte[] Base32Decode(string s)
    {
        var result = new List<byte>();
        int bits = 0, value = 0;
        foreach (var c in s.ToUpperInvariant())
        {
            var idx = Alphabet.IndexOf(c);
            if (idx < 0) throw new FormatException("非法 Base32 字符");
            value = (value << 5) | idx; bits += 5;
            if (bits >= 8) { result.Add((byte)((value >> (bits - 8)) & 255)); bits -= 8; }
        }
        return result.ToArray();
    }
}