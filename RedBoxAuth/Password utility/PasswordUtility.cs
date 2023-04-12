using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using RedBoxAuth.Settings;

namespace RedBoxAuth.Password_utility;

public class PasswordUtility : IPasswordUtility
{
	private readonly AuthenticationOptions _hashingOptions;

	public PasswordUtility(IOptions<AuthenticationOptions> authOptions)
	{
		_hashingOptions = authOptions.Value;
	}

	public byte[] HashPassword(string password, byte[] salt)
	{
		using var argon2 = new Argon2id(Encoding.UTF32.GetBytes(password));
		argon2.Salt = salt;
		argon2.DegreeOfParallelism = _hashingOptions.Argon2IdDegreeOfParallelism;
		argon2.Iterations = _hashingOptions.Argon2IdIterations;
		argon2.MemorySize = _hashingOptions.Argon2IdMemorySizeKb;
		argon2.KnownSecret = Encoding.UTF32.GetBytes(_hashingOptions.Argon2IdPepper);

		return argon2.GetBytes(_hashingOptions.Argon2IdHashSize);
	}

	public bool VerifyPassword(string password, byte[] hash, byte[] salt)
	{
		var newHash = HashPassword(password, salt);
		return hash.SequenceEqual(newHash);
	}

	public byte[] CreateSalt()
	{
		return RandomNumberGenerator.GetBytes(_hashingOptions.HashSaltSize);
	}
}