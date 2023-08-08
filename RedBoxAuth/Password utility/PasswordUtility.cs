using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using RedBoxAuth.Settings;

namespace RedBoxAuth.Password_utility;

/// <inheritdoc />
public class PasswordUtility : IPasswordUtility
{
    private readonly SecurityOptions _hashingOptions;

#pragma warning disable CS1591
    public PasswordUtility(IOptions<SecurityOptions> authOptions)
#pragma warning restore CS1591
    {
        _hashingOptions = authOptions.Value;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public bool VerifyPassword(string password, byte[] salt, byte[] hash)
    {
        var newHash = HashPassword(password, salt);
        return hash.SequenceEqual(newHash);
    }

    /// <inheritdoc />
    public byte[] CreateSalt()
    {
        return RandomNumberGenerator.GetBytes(_hashingOptions.HashSaltSize);
    }

    public string GeneratePassword()
    {

        const string pool = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        int index;
        StringBuilder sb = new StringBuilder();
        
        for (int i = 0; i < 15; i++)
        {
            index = RandomNumberGenerator.GetInt32(pool.Length);
            sb.Append(pool[index]);
        }

        return sb.ToString();
    }
}