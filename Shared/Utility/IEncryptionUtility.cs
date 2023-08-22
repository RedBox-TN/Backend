namespace Shared.Utility;

public interface IEncryptionUtility
{
	public Task<(byte[] EncData, byte[] Iv)> AesEncryptAsync(string clearText, byte[] key, int keySize = 256);

	public Task<byte[]> AesDecryptAsync(byte[] encrypted, byte[] key, byte[] iv, int keySize);
	public byte[] DeriveKey(string password, int keySize = 256);
}