using System.Security.Cryptography;
using System.Text;

namespace RedBoxTests;

public static class Common
{
	public const string RedBoxServerAddress = "http://localhost:5200/";
	public const string KeychainServerAddress = "http://localhost:5000";
	public const string User = "admin";
	public const string Password = "password";

	public static byte[] CreateAesKey(string password, out byte[] iv, int keySize = 256)
	{
		using var aes = Aes.Create();
		aes.KeySize = keySize;
		aes.GenerateKey();

		var key = EncryptAsync(aes.Key, ComputeSha256Hash(password), aes.IV).Result;

		iv = aes.IV;
		return key;
	}

	public static byte[] ComputeSha256Hash(string rawData)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
		return bytes;
	}

	public static async Task<byte[]> EncryptAsync(byte[] clearText, byte[] passphrase, byte[] iv, int keySize = 256)
	{
		using var aes = Aes.Create();
		aes.Key = passphrase;
		aes.IV = iv;
		aes.KeySize = keySize;
		using MemoryStream output = new();
		await using CryptoStream cryptoStream = new(output, aes.CreateEncryptor(), CryptoStreamMode.Write);
		await cryptoStream.WriteAsync(clearText);
		await cryptoStream.FlushFinalBlockAsync();
		return output.ToArray();
	}

	public static async Task<string> DecryptAsync(byte[] encrypted, byte[] passphrase, byte[] iv, int keySize = 256)
	{
		using var aes = Aes.Create();
		aes.Key = passphrase;
		aes.IV = iv;
		aes.KeySize = keySize;
		using MemoryStream input = new(encrypted);
		await using CryptoStream cryptoStream = new(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
		using MemoryStream output = new();
		await cryptoStream.CopyToAsync(output);
		return Encoding.Unicode.GetString(output.ToArray());
	}

	public static (byte[] PubKey, byte[] PrivKey) CreateKeyPair(int rsaKeySize = 6144)
	{
		using var crypto = RSA.Create(rsaKeySize);
		var priv = crypto.ExportRSAPrivateKey();
		var pub = crypto.ExportRSAPublicKey();

		return (PubKey: pub, PrivKey: priv);
	}
}