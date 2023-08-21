namespace RedBox.Encryption_utility;

public interface IEncryptionUtility
{
	public Task<(byte[] EncData, byte[] Iv)> AesEncryptAsync(string clearText, byte[] key, int keySize);

	public Task<byte[]> AesDecryptAsync(byte[] encrypted, byte[] key, byte[] iv, int keySize);
	public byte[] DeriveKey(string password, int keySize);
}