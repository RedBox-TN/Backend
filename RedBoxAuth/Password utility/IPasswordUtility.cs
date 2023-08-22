namespace RedBoxAuth.Password_utility;

/// <summary>
///     Expose methods for handling passwords
/// </summary>
public interface IPasswordUtility
{
	/// <summary>
	///     Check if the password correspond with the hash saved
	/// </summary>
	/// <param name="password">The password as string</param>
	/// <param name="salt">The salt used for the salting the password</param>
	/// <param name="hash">The hash of the password</param>
	/// <returns>Bool result of the check</returns>
	public bool VerifyPassword(string password, byte[] salt, byte[] hash);

	/// <summary>
	///     Produce the hash form the password
	/// </summary>
	/// <param name="password">The Password</param>
	/// <param name="salt">The salt</param>
	/// <returns>The hash as byte array</returns>
	public byte[] HashPassword(string password, byte[] salt);

	/// <summary>
	///     Produce a random salt from a cryptographically safe generator
	/// </summary>
	/// <returns>Salt as byte array</returns>
	public byte[] CreateSalt();

	/// <summary>
	///     Produces a somewhat weak password with random numbers, lowercase and uppercase letters
	/// </summary>
	/// <returns>New generated password</returns>
	public string GeneratePassword();
}