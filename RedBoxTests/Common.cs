using System.Security.Cryptography;
using System.Text;

namespace RedBoxTests;

public static class Common
{
    public const string RedBoxServerAddress = "http://localhost:5200/";
    public const string KeychainServerAddress = "http://localhost:5000";
    public const string AdminUser = "admin";
    public const string Password = "password";
    public const string User = "user";
    public const string UserId = "64cff02e6a0ead1fa8a17ccb";

    public static byte[] CreateAesKey(int keySize = 256)
    {
        using var aes = Aes.Create();
        aes.KeySize = keySize;
        aes.GenerateKey();
        return aes.Key;
    }

    public static byte[] Hash(string rawData)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return bytes;
    }

    public static async Task<(byte[] EncData, byte[] Iv)> AesEncryptAsync(byte[] clearText, byte[] key,
        byte[]? iv = null,
        int keySize = 256)
    {
        using var aes = Aes.Create();
        aes.KeySize = keySize;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        if (iv is not null)
            aes.IV = iv;
        else
            aes.GenerateIV();

        using MemoryStream output = new();
        await using CryptoStream cryptoStream = new(output, aes.CreateEncryptor(), CryptoStreamMode.Write);
        await cryptoStream.WriteAsync(clearText);
        await cryptoStream.FlushFinalBlockAsync();

        return (output.ToArray(), aes.IV);
    }

    public static async Task<byte[]> AesDecryptAsync(byte[] encrypted, byte[] key, byte[] iv, int keySize = 256)
    {
        using var aes = Aes.Create();
        aes.KeySize = keySize;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using MemoryStream input = new(encrypted);
        await using CryptoStream cryptoStream = new(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using MemoryStream output = new();
        await cryptoStream.CopyToAsync(output);
        return output.ToArray();
    }

    public static byte[] RsaEncrypt(byte[] plainText, byte[] publicKey, int keySize = 6144)
    {
        var rsa = RSA.Create(keySize);
        rsa.ImportRSAPublicKey(publicKey, out _);
        return rsa.Encrypt(plainText, RSAEncryptionPadding.Pkcs1);
    }

    public static byte[] RsaDecrypt(byte[] encrypted, byte[] privateKey, int keySize = 6144)
    {
        var rsa = RSA.Create(keySize);
        rsa.ImportRSAPrivateKey(privateKey, out _);
        return rsa.Decrypt(encrypted, RSAEncryptionPadding.Pkcs1);
    }

    public static (byte[] PubKey, byte[] PrivKey) CreateKeyPair(int rsaKeySize = 6144)
    {
        using var crypto = RSA.Create(rsaKeySize);
        var privateKey = crypto.ExportRSAPrivateKey();
        var pub = crypto.ExportRSAPublicKey();

        return (PubKey: pub, PrivKey: privateKey);
    }
}