namespace RedBoxAuth.Password_utility;

public interface IPasswordUtility
{
	public bool VerifyPassword(string password, byte[] hash, byte[] salt);
	public byte[] HashPassword(string password, byte[] salt);
	public byte[] CreateSalt();
}