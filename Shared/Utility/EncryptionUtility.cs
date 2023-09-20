using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Shared.Utility;

public class EncryptionUtility : IEncryptionUtility
{
    public async Task<(byte[] EncData, byte[] Iv)> AesEncryptAsync(string clearText, byte[] key, int keySize = 256)
    {
        using var aes = Aes.Create();
        aes.KeySize = keySize;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;

        using MemoryStream output = new();
        await using CryptoStream cryptoStream = new(output, aes.CreateEncryptor(), CryptoStreamMode.Write);
        await cryptoStream.WriteAsync(Encoding.UTF8.GetBytes(clearText));
        await cryptoStream.FlushFinalBlockAsync();

        return (output.ToArray(), aes.IV);
    }

    public async Task<byte[]> AesDecryptAsync(byte[] encrypted, byte[] key, byte[] iv, int keySize = 256)
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

    public byte[] DeriveKey(string password, int keySize = 256)
    {
        using var argon2 = new Argon2id(Encoding.UTF32.GetBytes(password));
        argon2.Iterations = 20;
        argon2.MemorySize = 512;
        argon2.DegreeOfParallelism = 4;

        return argon2.GetBytes(keySize / 8);
    }
}