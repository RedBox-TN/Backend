namespace RedBox.Encryption_utility;

public interface IEncryptionUtility
{
    public Task<(byte[] EncData, byte[] Iv)> EncryptAsync(string clearText, byte[] key, DateTime? expiration,
        int keySize);

    public Task<byte[]> DecryptAsync(byte[] encrypted, byte[] key, byte[] iv, int keySize);
    public byte[] DeriveKey(string password, int keySize);
}