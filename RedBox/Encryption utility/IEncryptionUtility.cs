namespace RedBox.Encryption_utility;

public interface IEncryptionUtility
{
	public Task<(byte[] EncData, byte[] Iv)> EncryptAsync(byte[] clearText, byte[] key, int keySize);

	public Task<byte[]> DecryptAsync(byte[] encrypted, byte[] key, byte[] iv, int keySize);
	public byte[] DeriveKey(string password, int keySize);
}