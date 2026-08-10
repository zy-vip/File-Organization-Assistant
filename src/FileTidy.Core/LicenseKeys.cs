namespace FileTidy.Core;

/// <summary>内置公钥（与应用一起发布，用于激活码验证）。私钥在 tools/FileTidy.LicenseTool/private_key.pem，绝不随应用发布。</summary>
public static class LicenseKeys
{
    public const string AppPublicKeyPem =
        "-----BEGIN PUBLIC KEY-----\n" +
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA20zTETQKrBy0NywWHHOe\n" +
        "cln0L3x2rJZgmAjO2XOGPHOSgkusjgLoi8MfQocaCFH2c+5+ZLY+6z9zf4imr/Gu\n" +
        "E98D7XHOgqXS3/kdFRcDebV7D7ZXNTOe2FklBc+/yGu/GVtzXWUEYBQkKoGHrAcH\n" +
        "uUkAr1Urmgk4cd9KvtUyXyooD9FNOLda3TNRO8sGAOo9Tk1IqPhEOOL+jzmN72Hi\n" +
        "AmOwSRQGcwQl8bTkXOYc83IweRPWG3PCqqrUbl6Zc/VhdmfKid26l5DazmHCKsze\n" +
        "TDdTdgT5HA59y0Ha+7TKug5gAs/pUAAHKMCxO+iIkwaoBNoQ60k0BpER20YVirKP\n" +
        "cQIDAQAB\n" +
        "-----END PUBLIC KEY-----";
}
