namespace FileTidy.Core;

/// <summary>内置公钥（与应用一起发布，用于激活码验证）。私钥在 tools/FileTidy.LicenseTool/private_key.pem，绝不随应用发布。</summary>
public static class LicenseKeys
{
    public const string AppPublicKeyPem =
        "-----BEGIN PUBLIC KEY-----\n" +
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAyWP1upD4yYXbUcE7ahk2\n" +
        "rguvncNmYjP4jtANGPj/rPuHcyyehUU85rl+g4XUeiL1qXQqZatcqQ/K+TNC8a8U\n" +
        "yguSPRHdZWCih4TfeE7VY00PFlmoEGjY1Be4xUEKcdFUNShDs71qmFDNu70oa9MV\n" +
        "EuH0j/yYCtz8MAkY63by9mxYxUaFywnYauiv9uTGoZdGw3gNYOfUVykB9pcBaSnR\n" +
        "ExXzrscBh5z6WOqHx9Sivx4RWSa2jrQV/kS3prIMXbu8k10ke0pxvS7wKJm0LxUR\n" +
        "gm2QWvkoeRweQgMLYrv+19ekFmwtQL/bdAPcpuNziebyCF2iCifgT2rE51nPo9nZ\n" +
        "TQIDAQAB\n" +
        "-----END PUBLIC KEY-----";
}
